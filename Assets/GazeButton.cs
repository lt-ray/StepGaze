using System.Collections; // コルーチン用に必要
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GazeButton : MonoBehaviour
{
    public enum SelectionMode
    {
        DwellOnly,
        SequentialAreas
    }
    public enum DecisionAreaCount
    {
        One = 1,
        Two = 2,
        Three = 3
    }

    [Header("モード")]
    public SelectionMode selectionMode = SelectionMode.SequentialAreas;

    [Header("決定エリア数")]
    public DecisionAreaCount decisionAreaCount = DecisionAreaCount.Two;

    [Header("Dwell 設定")]
    public float dwellToConfirm = 0.7f;  // DwellOnly用

    [Header("Dwell プログレスリング")]
    public Image dwellRingImage;

    [Header("決定エリア方式設定")]
    public float dwellToShowAreas = 0.3f;        // ボタン上で決定エリアを出すまでのdwell
    public RectTransform decisionAreaPrefab;     // 決定エリアのプレハブ

    [Header("★ タイムアウト設定（StepGaze用）")]
    public float autoResetTime = 3.0f; // この秒数以内に確定しないとリセット

    [Header("★ 確定後の表示維持時間（秒）")]
    public float confirmFeedbackDuration = 0.5f; // 確定色が消えるまでの時間

    [Header("決定エリアの dwell 秒数（秒）")]
    public float area1Dwell = 0.2f;
    public float area2Dwell = 0.2f;
    public float area3Dwell = 0.2f;

    [Header("決定エリアの配置オフセット（ボタン中心からの相対座標）")]
    [SerializeField] private Vector2 oneAreaOffset1   = new Vector2(80f, 0f);
    [SerializeField] private Vector2 twoAreaOffset1   = new Vector2(-80f, 0f);
    [SerializeField] private Vector2 twoAreaOffset2   = new Vector2( 80f, 0f);
    [SerializeField] private Vector2 threeAreaOffset1 = new Vector2(  0f,  80f);
    [SerializeField] private Vector2 threeAreaOffset2 = new Vector2(-80f, -40f);
    [SerializeField] private Vector2 threeAreaOffset3 = new Vector2( 80f, -40f);

    [Header("決定エリアの色")]
    public Color area1Color = Color.red;
    public Color area2Color = Color.green;
    public Color area3Color = Color.blue;

    [Header("次に見るべき決定エリアのハイライト色")]
    public Color nextAreaColor = Color.yellow;

    private RectTransform rect;
    private float gazeTimer = 0f;

    // 提案方式用
    private bool areasSpawned = false;
    private int nextExpectedIndex = 1;
    private List<GazeDecisionArea> spawnedAreas = new List<GazeDecisionArea>();
    private float areaLifeTimer = 0f; // エリアが表示されてからの経過時間

    private bool dwellLocked = false;  // DwellOnly: 確定後、離脱までロック
    private bool isFreezing = false;   // ★ 確定演出中などで入力を受け付けないフラグ

    [Header("デバッグ用表示")]
    public TextMeshProUGUI countText;
    static private int confirmCount = 0;

    // 今どのボタンが「決定エリアを出しているか」
    private static GazeButton activeSequentialButton = null;

    [Header("テンキー用設定")]
    public bool isNumberKey = false;
    public int keyValue = 0;

    // ボタンの基本色（DotTaskManager等から変更可能にする）
    [HideInInspector] public Color currentBaseColor = Color.white;

    // フェーズ計測用
    private float buttonGazeStartTime = -1f;  // Button に視線が乗った時刻 T0
    private float lastPhaseTime       = -1f;  // 直前フェーズ終了時刻
    private bool wasGazedThisButtonLastFrame = false;
    private TextMeshProUGUI buttonText;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        buttonText = GetComponentInChildren<TextMeshProUGUI>();  // ボタンの子オブジェクトにあるText
        if (buttonText != null)
        {
            buttonText.text = keyValue.ToString();  // ボタンに表示する数字を設定
            if(keyValue == 10)
            {
                buttonText.text = "a";
            }else if (keyValue == 11)
            {
                buttonText.text = "b";
            }
        }
        
        // 初期色を保存
        var img = GetComponent<Image>();
        if(img != null) currentBaseColor = img.color;
    }
   

    private void Update()
    {
        // ★ 確定演出中などは処理を止める
        if (isFreezing) return;

        if (GazeManager.Instance == null) return;

        // ---------------------------------------------------------
        // 1. 先に視線判定を行う (タイムアウト判定で onThisButton を使うため位置を移動)
        // ---------------------------------------------------------
        GameObject gazed = GazeManager.Instance.GetGazedUI();
        GazeButton gazedButton = null;

        if (gazed != null)
        {
            // 決定エリアを見ている場合も、その親(Owner)を見ているとみなす
            var area = gazed.GetComponent<GazeDecisionArea>();
            if (area != null && area.owner != null)
            {
                gazedButton = area.owner;
            }
            else
            {
                gazedButton = gazed.GetComponentInParent<GazeButton>();
            }
        }

        bool onThisButton = (gazedButton == this);


        // ---------------------------------------------------------
        // 2. タイムアウト監視（エリアが表示されている場合のみ）
        // ---------------------------------------------------------
        if (areasSpawned)
        {
            // ★ 修正箇所: ボタン（または決定エリア）を見ている間はタイマーを進めない
            if (onThisButton)
            {
                // 視線が乗っている間はタイマーをリセット(0)し続ける
                // ※「離してから3秒」にするため、見ている間は常に0にします
                areaLifeTimer = 0f; 
            }
            else
            {
                // 視線が外れている時だけ時間を進める
                areaLifeTimer += Time.deltaTime;
            }

            // 制限時間を超えたらリセット
            if (areaLifeTimer >= autoResetTime)
            {
                Debug.Log($"[Seq] Time out reset: {gameObject.name}");
                ResetState();
                return; // リセットしたら以降の処理は不要
            }
        }


        // ---------------------------------------------------------
        // 3. 視線が乗っている時の処理 (Dwell / エリア生成)
        // ---------------------------------------------------------
        
        // 他ボタンが見られたら、自分の決定エリアはリセット
        if (selectionMode == SelectionMode.SequentialAreas &&
            activeSequentialButton != null &&
            gazedButton != null &&
            gazedButton != activeSequentialButton)
        {
            activeSequentialButton.ResetState();
        }

        // 提案方式で「このボタンに乗った瞬間」を検出
        if (selectionMode == SelectionMode.SequentialAreas &&
            onThisButton && !wasGazedThisButtonLastFrame)
        {
            buttonGazeStartTime = Time.time;
            lastPhaseTime       = buttonGazeStartTime;

            // ログがうるさければコメントアウトしてください
            // Debug.Log($"[Seq] Button gaze start at {buttonGazeStartTime:F3} on {gameObject.name}");
        }

        if (onThisButton)
        {
            gazeTimer += Time.deltaTime;

            if (selectionMode == SelectionMode.DwellOnly)
            {
                if (dwellLocked)
                {
                    SetDwellProgress(0f);
                    wasGazedThisButtonLastFrame = onThisButton;
                    return;
                }

                float ratio = gazeTimer / dwellToConfirm;
                SetDwellProgress(ratio);

                if (gazeTimer >= dwellToConfirm)
                {
                    Debug.Log("DWELL方式で確定: " + gameObject.name);
                    OnConfirmed();
                    dwellLocked = true;
                    SetDwellProgress(0f);
                }
            }
            else if (selectionMode == SelectionMode.SequentialAreas)
            {
                // まだエリアが出ていない場合のみDwell計算
                if (!areasSpawned)
                {
                    float ratio = gazeTimer / dwellToShowAreas;
                    SetDwellProgress(ratio);
                }

                // 一定時間見つめてエリアを出す
                if (!areasSpawned && gazeTimer >= dwellToShowAreas)
                {
                    Debug.Log("DWELL完了 → 決定エリア生成: " + gameObject.name);
                    SpawnDecisionAreas();
                    areasSpawned = true;
                    activeSequentialButton = this;
                    SetDwellProgress(0f);
                }
            }
        }
        else
        {
            // 視線が外れたら gazeTimer (エリアを出すためのタイマー) はリセット
            gazeTimer = 0f;
            dwellLocked = false;

            SetDwellProgress(0f);

            if (selectionMode == SelectionMode.DwellOnly)
            {
                var img = GetComponent<Image>();
                if (img != null)
                    img.color = currentBaseColor; // 基本色に戻す
            }
        }

        wasGazedThisButtonLastFrame = onThisButton;
    }

    private void SetDwellProgress(float t)
    {
        if (dwellRingImage == null) return;
        t = Mathf.Clamp01(t);
        dwellRingImage.enabled = (t > 0f);
        dwellRingImage.fillAmount = t;
    }

    // ==========================
    // 決定エリア方式 用の処理
    // ==========================

    private int GetDecisionAreaCountValue()
    {
        return (int)decisionAreaCount;
    }

    private void SpawnDecisionAreas()
    {
        // タイマーリセット
        areaLifeTimer = 0f;

        if (decisionAreaCount == DecisionAreaCount.One)
        {
            CreateArea(oneAreaOffset1, 1);
        }
        else if (decisionAreaCount == DecisionAreaCount.Two)
        {
            CreateArea(twoAreaOffset1, 1);
            CreateArea(twoAreaOffset2, 2);
        }
        else if (decisionAreaCount == DecisionAreaCount.Three)
        {
            CreateArea(threeAreaOffset1, 1);
            CreateArea(threeAreaOffset2, 2);
            CreateArea(threeAreaOffset3, 3);
        }

        // 最初に見るべきエリアをハイライト
        nextExpectedIndex = 1;
        HighlightNextArea();
    }

    private void CreateArea(Vector2 offset, int index)
    {
        RectTransform parent = rect.parent as RectTransform;
        RectTransform areaRect = Instantiate(decisionAreaPrefab, parent);

        areaRect.anchoredPosition = rect.anchoredPosition + offset;

        var img = areaRect.GetComponent<Image>();
        if (img != null)
        {
            if      (index == 1) img.color = area1Color;
            else if (index == 2) img.color = area2Color;
            else if (index == 3) img.color = area3Color;
        }

        var script = areaRect.GetComponent<GazeDecisionArea>();
        script.index = index;
        script.owner = this;

        if      (index == 1) script.dwellDuration = area1Dwell;
        else if (index == 2) script.dwellDuration = area2Dwell;
        else if (index == 3) script.dwellDuration = area3Dwell;

        spawnedAreas.Add(script);
    }

    // 決定エリア通過時に呼ばれる
    // 決定エリア通過時に呼ばれる
    public void OnAreaPassed(int index, GazeDecisionArea area)
    {
        // 確定演出中などは無視
        if (isFreezing) return;

        if (selectionMode != SelectionMode.SequentialAreas) return;

        int total = GetDecisionAreaCountValue();
        float now = Time.time;

        // ★ 修正箇所: 正しい順番のエリアか判定
        if (index == nextExpectedIndex)
        {
            // フェーズ時間の計測
            if (lastPhaseTime > 0f)
            {
                float segment = now - lastPhaseTime;
                string fromStage = (index == 1) ? "Button" : $"Area{index - 1}";
                string toStage   = $"Area{index}";

                Debug.Log($"[Seq] {fromStage} -> {toStage} : {segment:F3} sec (button {gameObject.name})");

                if (ExperimentLogger.Instance != null && NumberTaskManager.Instance != null)
                {
                    var ntm = NumberTaskManager.Instance;
                    ExperimentLogger.Instance.LogPhaseEvent(
                        taskType: "Number",
                        condition: GetConditionString(),
                        trialIndex: ntm.CurrentTrialIndex,
                        keyIndex:   ntm.CurrentInputLength,
                        target:     ntm.CurrentTarget,
                        currentInputAfter: ntm.CurrentInput,
                        phaseFrom:  fromStage,
                        phaseTo:    toStage,
                        phaseDuration: segment,
                        trialStartTime: ntm.CurrentTrialStartTime
                    );
                }
            }

            lastPhaseTime = now;

            Debug.Log($"決定エリア{index} 通過（正しい順番）");

            area.SetVisible(false);

            if (nextExpectedIndex == total)
            {
                // 全て通過 → 確定
                OnConfirmed();
            }
            else
            {
                nextExpectedIndex++;
                HighlightNextArea();
            }
        }
        else
        {
            // ★ リセット処理を追加
            // 期待していないエリア（順番飛ばしや戻り）を見てしまったのでリセット
            Debug.Log($"決定エリア{index} 通過（順番違い / 期待: {nextExpectedIndex}） -> リセットします");
            ResetState();
        }
    }

    private void HighlightNextArea()
    {
        foreach (var area in spawnedAreas)
        {
            if (area == null) continue;
            if (area.index == nextExpectedIndex)
            {
                area.SetColor(nextAreaColor);
                Debug.Log($"次に見るべきエリア {nextExpectedIndex} をハイライト");
                break;
            }
        }
    }

    private void OnConfirmed()
    {
        float now = Time.time;

        // ログ処理
        if (selectionMode == SelectionMode.SequentialAreas &&
            lastPhaseTime > 0f &&
            NumberTaskManager.Instance != null)
        {
            float segment = now - lastPhaseTime;
            int total = GetDecisionAreaCountValue();
            string fromStage = $"Area{total}";
            string toStage   = "Confirm";

            var ntm = NumberTaskManager.Instance;
            if (ExperimentLogger.Instance != null)
            {
                ExperimentLogger.Instance.LogPhaseEvent(
                    taskType: "Number",
                    condition: GetConditionString(),
                    trialIndex: ntm.CurrentTrialIndex,
                    keyIndex:   ntm.CurrentInputLength,
                    target:     ntm.CurrentTarget,
                    currentInputAfter: ntm.CurrentInput,
                    phaseFrom:  fromStage,
                    phaseTo:    toStage,
                    phaseDuration: segment,
                    trialStartTime: ntm.CurrentTrialStartTime
                );
            }
        }

        Debug.Log("ボタン確定！！ " + gameObject.name);

        // マネージャーへ通知
        if (isNumberKey)
        {
            if (DotTaskManager.Instance != null)
            {
                DotTaskManager.Instance.OnDigitConfirmed(keyValue);
            }
            else if (RandomizedNumberTaskManager.Instance != null)
            {
                RandomizedNumberTaskManager.Instance.OnDigitConfirmed(keyValue);
            }
            else if (NumberTaskManager.Instance != null)
            {
                NumberTaskManager.Instance.OnDigitConfirmed(keyValue);
            }
        }

        confirmCount++;
        if (countText != null)
            countText.text = "Count: " + confirmCount;

        // ★ 色を変更（緑色など）
        var img = GetComponent<Image>();
        if (img != null)
            img.color = Color.green;

        // StepGazeの場合：少し待ってからリセットするコルーチンを開始
        if (selectionMode == SelectionMode.SequentialAreas)
        {
            StartCoroutine(WaitAndResetRoutine());
        }

        // フェーズ計測リセット
        buttonGazeStartTime = -1f;
        lastPhaseTime       = -1f;
    }

    // ★ 待機してからリセットするコルーチン
    private IEnumerator WaitAndResetRoutine()
    {
        // ガードを有効化（この間は視線入力を受け付けない）
        isFreezing = true;

        // 指定秒数待機
        yield return new WaitForSeconds(confirmFeedbackDuration);

        // リセット実行
        ResetState();

        // ガード解除
        isFreezing = false;
    }

    private void ResetState()
    {
        foreach (var area in spawnedAreas)
        {
            if (area != null && area.gameObject != null)
                Destroy(area.gameObject);
        }
        spawnedAreas.Clear();

        areasSpawned      = false;
        nextExpectedIndex = 1;
        gazeTimer         = 0f;
        areaLifeTimer     = 0f;

        if (activeSequentialButton == this)
            activeSequentialButton = null;

        var img = GetComponent<Image>();
        if (img != null)
            img.color = currentBaseColor; // 基本色に戻す

        Debug.Log("GazeButton を初期状態にリセット: " + gameObject.name);
    }

    private string GetConditionString()
    {
        return $"{selectionMode}_Areas{GetDecisionAreaCountValue()}";
    }
}