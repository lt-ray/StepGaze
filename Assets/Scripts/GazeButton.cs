using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(AudioSource))]
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
    public float dwellToShowAreas = 0.3f;        
    public RectTransform decisionAreaPrefab;     

    [Header("★ タイムアウト設定")]
    public float autoResetTime = 3.0f; 

    [Header("★ フィードバック時間設定（秒）")]
    public float correctFeedbackDuration = 0.5f; 
    public float errorFeedbackDuration = 1.0f;   

    [Header("★ オーディオ設定")]
    public AudioClip correctSound;   
    public AudioClip incorrectSound; 

    [Header("★ アニメーション設定（視線ホバー時）")]
    public Vector3 hoverScale = new Vector3(1.2f, 1.2f, 1.0f); 
    public float scaleSpeed = 30f; 

    [Header("決定エリアの dwell 秒数（秒）")]
    public float area1Dwell = 0.2f;
    public float area2Dwell = 0.2f;
    public float area3Dwell = 0.2f;

    [Header("決定エリアの配置オフセット")]
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

    [Header("★ 決定済みエリアの色")]
    public Color confirmedAreaColor = new Color(0.5f, 0.5f, 0.5f, 0.7f); 

    [Header("次に見るべき決定エリアのハイライト色")]
    public Color nextAreaColor = Color.yellow;

    [Header("★ ボタンフィードバック色")]
    public Color correctColor = Color.green; 
    public Color errorColor = Color.red;     

    private RectTransform rect;
    private float gazeTimer = 0f;

    // 提案方式用
    private bool areasSpawned = false;
    private int nextExpectedIndex = 1;
    private List<GazeDecisionArea> spawnedAreas = new List<GazeDecisionArea>();
    private float areaLifeTimer = 0f; 

    private bool dwellLocked = false;  
    private bool isFreezing = false;   

    private AudioSource audioSource;
    private Vector3 originalScale; 

    private int originalSiblingIndex = 0;
    private bool isSortedToFront = false;
    private UnityEngine.UI.LayoutGroup parentLayoutGroup; 

    [Header("デバッグ用表示")]
    public TextMeshProUGUI countText;
    static private int confirmCount = 0;

    private static GazeButton activeSequentialButton = null;

    [Header("テンキー用設定")]
    public bool isNumberKey = false;
    public int keyValue = 0;

    [HideInInspector] public Color currentBaseColor = Color.white;

    // --- ログ計測用変数 ---
    private float buttonGazeStartTime = -1f; 
    private float lastPhaseTime       = -1f; 
    
    private float firstGazeTime = -1f; 
    private int localResetCount = 0;   

    private bool wasGazedThisButtonLastFrame = false;
    private TextMeshProUGUI buttonText;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        originalScale = transform.localScale; 

        buttonText = GetComponentInChildren<TextMeshProUGUI>();  
        if (buttonText != null)
        {
            buttonText.text = keyValue.ToString();  
            if(keyValue == 10) buttonText.text = "a";
            else if (keyValue == 11) buttonText.text = "b";
        }
        
        var img = GetComponent<Image>();
        if(img != null) currentBaseColor = img.color;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (transform.parent != null)
        {
            parentLayoutGroup = transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>();
        }
    }
   

    private void Update()
    {
        if (isFreezing) return;
        if (GazeManager.Instance == null) return;

        // ---------------------------------------------------------
        // 1. 視線判定
        // ---------------------------------------------------------
        GameObject gazed = GazeManager.Instance.GetGazedUI();
        GazeButton gazedButton = null;
        GazeDecisionArea gazedArea = null;

        if (gazed != null)
        {
            gazedArea = gazed.GetComponentInParent<GazeDecisionArea>();

            if (gazedArea != null && gazedArea.owner != null)
            {
                gazedButton = gazedArea.owner;
            }
            else
            {
                gazedButton = gazed.GetComponentInParent<GazeButton>();
            }
        }

        bool onThisButton = (gazedButton == this);

        // ★ 拡大縮小 & 手前表示処理
        Vector3 targetScale = originalScale;
        
        if (onThisButton)
        {
            targetScale = hoverScale;
            if (!isSortedToFront)
            {
                SortButtonToFront();
            }
        }
        else
        {
            targetScale = originalScale;
            if (!areasSpawned && isSortedToFront)
            {
                RestoreButtonSort();
            }
        }
        
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);

        // ---------------------------------------------------------
        // 2. タイムアウト監視
        // ---------------------------------------------------------
        if (areasSpawned)
        {
            bool isLookingAtAnyDecisionArea = (gazedArea != null);

            if (onThisButton && !isLookingAtAnyDecisionArea)
            {
                areaLifeTimer = 0f; 
            }
            else
            {
                areaLifeTimer += Time.deltaTime;
            }

            if (areaLifeTimer >= autoResetTime)
            {
                Debug.Log($"[Seq] Time out reset: {gameObject.name}");
                ResetState();
                return; 
            }
        }

        // ---------------------------------------------------------
        // 3. 視線が乗っている時の処理
        // ---------------------------------------------------------
        
        if (selectionMode == SelectionMode.SequentialAreas &&
            activeSequentialButton != null &&
            gazedButton != null &&
            gazedButton != activeSequentialButton)
        {
            activeSequentialButton.ResetState();
        }

        // --- 計測開始 (ボタンを初めて見た瞬間) ---
        if (onThisButton && !wasGazedThisButtonLastFrame)
        {
            buttonGazeStartTime = Time.time;
            lastPhaseTime       = buttonGazeStartTime;

            if (firstGazeTime < 0f)
            {
                firstGazeTime = Time.time;
            }
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
                    OnConfirmed();
                    dwellLocked = true;
                    SetDwellProgress(0f);
                }
            }
            else if (selectionMode == SelectionMode.SequentialAreas)
            {
                if (!areasSpawned)
                {
                    float ratio = gazeTimer / dwellToShowAreas;
                    SetDwellProgress(ratio);
                }

                if (!areasSpawned && gazeTimer >= dwellToShowAreas)
                {
                    SpawnDecisionAreas();
                    areasSpawned = true;
                    activeSequentialButton = this;
                    SetDwellProgress(0f);
                }
            }
        }
        else
        {
            gazeTimer = 0f;
            dwellLocked = false;
            SetDwellProgress(0f);

            if (selectionMode == SelectionMode.DwellOnly)
            {
                var img = GetComponent<Image>();
                if (img != null)
                    img.color = currentBaseColor; 
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

    private void SortButtonToFront()
    {
        if (isSortedToFront) return;
        originalSiblingIndex = transform.GetSiblingIndex();
        if (parentLayoutGroup != null) parentLayoutGroup.enabled = false;
        transform.SetAsLastSibling();
        isSortedToFront = true;
    }

    private void RestoreButtonSort()
    {
        if (!isSortedToFront) return;
        transform.SetSiblingIndex(originalSiblingIndex);
        if (parentLayoutGroup != null) parentLayoutGroup.enabled = true;
        isSortedToFront = false;
    }

    // ==========================
    // 決定エリア方式
    // ==========================

    private int GetDecisionAreaCountValue()
    {
        return (int)decisionAreaCount;
    }

    private void SpawnDecisionAreas()
    {
        areaLifeTimer = 0f;

        if (decisionAreaCount == DecisionAreaCount.One)
            CreateArea(oneAreaOffset1, 1);
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

    public void OnAreaPassed(int index, GazeDecisionArea area)
    {
        if (isFreezing) return;
        if (selectionMode != SelectionMode.SequentialAreas) return;

        int total = GetDecisionAreaCountValue();
        float now = Time.time;

        if (index == nextExpectedIndex)
        {
            if (lastPhaseTime > 0f)
            {
                float segment = now - lastPhaseTime;
                string fromStage = (index == 1) ? "Button" : $"Area{index - 1}";
                string toStage   = $"Area{index}";

                Debug.Log($"[Seq] {fromStage} -> {toStage} : {segment:F3} sec");

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
            area.SetColor(confirmedAreaColor); 

            if (nextExpectedIndex == total)
            {
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
            Debug.Log($"順番違いリセット: {index} (期待: {nextExpectedIndex})");
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
                break;
            }
        }
    }

    private void OnConfirmed()
    {
        float now = Time.time;

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

        float trialStartTime = 0f;
        if (DotTaskManager.Instance != null) trialStartTime = DotTaskManager.Instance.CurrentTrialStartTime;
        else if (RandomizedNumberTaskManager.Instance != null) trialStartTime = RandomizedNumberTaskManager.Instance.CurrentTrialStartTime;
        else if (NumberTaskManager.Instance != null) trialStartTime = NumberTaskManager.Instance.CurrentTrialStartTime;

        float searchTime = (firstGazeTime > 0) ? Mathf.Max(0, firstGazeTime - trialStartTime) : 0;
        float selectionTime = (firstGazeTime > 0) ? (now - firstGazeTime) : 0;

        bool isCorrect = true; 
        if (isNumberKey)
        {
            if (DotTaskManager.Instance != null)
            {
                isCorrect = DotTaskManager.Instance.OnDigitConfirmed(keyValue, searchTime, selectionTime, localResetCount);
            }
            else if (RandomizedNumberTaskManager.Instance != null)
            {
                isCorrect = RandomizedNumberTaskManager.Instance.OnDigitConfirmed(keyValue, searchTime, selectionTime, localResetCount);
            }
            else if (NumberTaskManager.Instance != null)
            {
                isCorrect = NumberTaskManager.Instance.OnDigitConfirmed(keyValue, searchTime, selectionTime, localResetCount);
            }
        }

        if (audioSource != null)
        {
            if (isCorrect && correctSound != null)
                audioSource.PlayOneShot(correctSound);
            else if (!isCorrect && incorrectSound != null)
                audioSource.PlayOneShot(incorrectSound);
        }

        confirmCount++;
        if (countText != null) countText.text = "Count: " + confirmCount;

        var img = GetComponent<Image>();
        if (img != null) img.color = isCorrect ? correctColor : errorColor;

        float waitTime = isCorrect ? correctFeedbackDuration : errorFeedbackDuration;

        StartCoroutine(WaitAndResetRoutine(waitTime));
        
        buttonGazeStartTime = -1f;
        lastPhaseTime       = -1f;
        
        firstGazeTime = -1f;
        localResetCount = 0;
    }

    private IEnumerator WaitAndResetRoutine(float duration)
    {
        isFreezing = true; 
        yield return new WaitForSeconds(duration);
        ResetState();
        isFreezing = false; 
    }

    private void ResetState()
    {
        // ★ ログ記録: 確定演出中でないのにリセットされる＝失敗リセット
        // かつ、nextExpectedIndex > 1 (一つ以上のエリアを通過済み) の場合のみカウント
        if (!isFreezing && areasSpawned && nextExpectedIndex > 1)
        {
            localResetCount++;
            Debug.Log($"[Log] Reset detected (Mid-operation). Count: {localResetCount}");

            if (ExperimentLogger.Instance != null)
            {
                // 現在のタスク情報を取得
                int trialIdx = 0;
                int keyIdx = 0;
                string targetStr = "";
                float trialStart = 0f;
                string tType = "Unknown";

                if (DotTaskManager.Instance != null)
                {
                    trialIdx = DotTaskManager.Instance.CurrentTrialIndex;
                    targetStr = DotTaskManager.Instance.CurrentTarget;
                    keyIdx = DotTaskManager.Instance.CurrentInput.Length;
                    trialStart = DotTaskManager.Instance.CurrentTrialStartTime;
                    tType = "DotTask";
                }
                else if (RandomizedNumberTaskManager.Instance != null)
                {
                    var tm = RandomizedNumberTaskManager.Instance;
                    trialIdx = tm.CurrentTrialIndex;
                    targetStr = tm.CurrentTarget;
                    keyIdx = tm.CurrentInputLength;
                    trialStart = tm.CurrentTrialStartTime;
                    tType = "Number";
                }
                else if (NumberTaskManager.Instance != null)
                {
                    var tm = NumberTaskManager.Instance;
                    trialIdx = tm.CurrentTrialIndex;
                    targetStr = tm.CurrentTarget;
                    keyIdx = tm.CurrentInputLength;
                    trialStart = tm.CurrentTrialStartTime;
                    tType = "Number";
                }

                string btnVal = keyValue.ToString();
                if (keyValue == 10) btnVal = "a";
                if (keyValue == 11) btnVal = "b";

                ExperimentLogger.Instance.LogResetEvent(
                    taskType: tType,
                    condition: GetConditionString(),
                    trialIndex: trialIdx,
                    keyIndex: keyIdx,
                    target: targetStr,
                    buttonValue: btnVal,
                    trialStartTime: trialStart,
                    reachedStage: nextExpectedIndex 
                );
            }
        }

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

        transform.localScale = originalScale;
        RestoreButtonSort();

        if (activeSequentialButton == this)
            activeSequentialButton = null;

        var img = GetComponent<Image>();
        if (img != null)
            img.color = currentBaseColor; 
    }

    private string GetConditionString()
    {
        return $"{selectionMode}_Areas{GetDecisionAreaCountValue()}";
    }
}