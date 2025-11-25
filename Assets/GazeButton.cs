using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GazeButton : MonoBehaviour
{
    public enum SelectionMode
    {
        DwellOnly,         // dwell だけで確定
        SequentialAreas    // 提案方式：決定エリアを順番に通過
    }

    // 決定エリアの個数
    public enum DecisionAreaCount
    {
        One = 1,   // 決定エリア1個
        Two = 2,   // 決定エリア2個
        Three = 3  // 決定エリア3個
    }

    [Header("モード")]
    public SelectionMode selectionMode = SelectionMode.SequentialAreas;

    [Header("決定エリア数")]
    public DecisionAreaCount decisionAreaCount = DecisionAreaCount.Two;

    [Header("Dwell 設定")]
    public float dwellToConfirm = 0.7f;          // dwell方式: 何秒見たら確定？

    [Header("決定エリア方式設定")]
    public float dwellToShowAreas = 0.3f;        // ボタン上で何秒見たら決定エリアを出すか
    public RectTransform decisionAreaPrefab;     // 決定エリアのプレハブ

    [Header("決定エリアの配置オフセット（ボタン中心からの相対座標）")]
    [SerializeField] private Vector2 oneAreaOffset1    = new Vector2(80f, 0f);

    [SerializeField] private Vector2 twoAreaOffset1    = new Vector2(-80f, 0f);
    [SerializeField] private Vector2 twoAreaOffset2    = new Vector2( 80f, 0f);

    [SerializeField] private Vector2 threeAreaOffset1  = new Vector2(  0f,  80f);
    [SerializeField] private Vector2 threeAreaOffset2  = new Vector2(-80f, -40f);
    [SerializeField] private Vector2 threeAreaOffset3  = new Vector2( 80f, -40f);

    [Header("決定エリアの色")]
    public Color area1Color = Color.red;
    public Color area2Color = Color.green;
    public Color area3Color = Color.blue;

    private RectTransform rect;
    private float gazeTimer = 0f;

    // 提案方式用
    private bool areasSpawned = false;
    private int nextExpectedIndex = 1;               // 次に期待するエリア番号
    private List<GazeDecisionArea> spawnedAreas = new List<GazeDecisionArea>();

    private bool dwellLocked = false;  // Dwell方式：確定後、離脱までロック

    [Header("デバッグ用表示")]
    public TextMeshProUGUI countText;
    static private int confirmCount = 0;

    // ★ 今どのボタンが“決定エリアを出しているか”
    private static GazeButton activeSequentialButton = null;

    [Header("テンキー用設定")]
    public bool isNumberKey = false;  // このボタンが数字キーかどうか
    public int keyValue = 0;          // 数字キーの場合の値（0〜9）

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (GazeManager.Instance == null) return;

        GameObject gazed = GazeManager.Instance.GetGazedUI();

        GazeButton gazedButton = null;

        if (gazed != null)
        {
            // ① まず決定エリアかどうかを見る
            var area = gazed.GetComponent<GazeDecisionArea>();
            if (area != null && area.owner != null)
            {
                // 決定エリアなら、その owner ボタンを「見ているボタン」とする
                gazedButton = area.owner;
            }
            else
            {
                // ② それ以外（普通のUI）は従来通り GazeButton を親から探す
                gazedButton = gazed.GetComponentInParent<GazeButton>();
            }
        }

        bool onThisButton = (gazedButton == this);

        // ★ 他のボタンが見られたら、自分の決定エリアは消す（ミス扱いで初期化）
        if (selectionMode == SelectionMode.SequentialAreas &&
            activeSequentialButton != null &&
            gazedButton != null &&
            gazedButton != activeSequentialButton)
        {
            activeSequentialButton.ResetState();
        }

        if (onThisButton)
        {
            gazeTimer += Time.deltaTime;

            if (selectionMode == SelectionMode.DwellOnly)
            {
                // すでに確定済みなら、離脱するまで何もしない
                if (dwellLocked) return;

                // dwell方式：一定時間見たらそのまま確定
                if (gazeTimer >= dwellToConfirm)
                {
                    Debug.Log("DWELL方式で確定: " + gameObject.name);
                    OnConfirmed();
                    dwellLocked = true; // 離脱するまでロック
                }
            }
            else if (selectionMode == SelectionMode.SequentialAreas)
            {
                // 提案方式：一定時間見たら決定エリアを出す
                if (!areasSpawned && gazeTimer >= dwellToShowAreas)
                {
                    Debug.Log("DWELL完了 → 決定エリア生成: " + gameObject.name);
                    SpawnDecisionAreas();
                    areasSpawned = true;
                    activeSequentialButton = this;   // ★ このボタンがアクティブ
                }
            }
        }
        else  // 視線がこのボタン（およびその決定エリア）から外れた
        {
            gazeTimer = 0f;
            dwellLocked = false;

            // ★ dwell の色リセットだけやる（Sequential は触らない）
            if (selectionMode == SelectionMode.DwellOnly)
            {
                var img = GetComponent<Image>();
                if (img != null)
                    img.color = Color.white;
            }
        }

    }

    // ==========================
    // 決定エリア方式 用の処理
    // ==========================

    private int GetDecisionAreaCountValue()
    {
        return (int)decisionAreaCount; // enum → 1/2/3 の数値
    }

    private void SpawnDecisionAreas()
    {
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
    }

    private void CreateArea(Vector2 offset, int index)
    {
        RectTransform parent = rect.parent as RectTransform;
        RectTransform areaRect = Instantiate(decisionAreaPrefab, parent);

        // ボタンの位置 + 個別オフセット
        areaRect.anchoredPosition = rect.anchoredPosition + offset;

        var img = areaRect.GetComponent<Image>();
        if (img != null)
        {
            if (index == 1)      img.color = area1Color;
            else if (index == 2) img.color = area2Color;
            else if (index == 3) img.color = area3Color;
        }

        var script = areaRect.GetComponent<GazeDecisionArea>();
        script.index = index;
        script.owner = this;

        spawnedAreas.Add(script);
    }

    // ★ 決定エリアが通過されたとき（エリア自身から呼ばれる）
    public void OnAreaPassed(int index, GazeDecisionArea area)
    {
        if (selectionMode != SelectionMode.SequentialAreas) return;

        int total = GetDecisionAreaCountValue();

        // 正しい順番のエリアが通過されたときだけ処理
        if (index == nextExpectedIndex)
        {
            Debug.Log($"決定エリア{index} 通過（正しい順番）");

            // 見た目を消す（正しいエリアだけ）
            area.SetVisible(false);

            if (nextExpectedIndex == total)
            {
                // 必要な数すべて通過 → 確定
                OnConfirmed();
            }
            else
            {
                // 次に期待するエリア番号を進める
                nextExpectedIndex++;
            }
        }
        else
        {
            // 順番違いは無視（見た目も消さない）
            Debug.Log($"決定エリア{index} 通過（順番違い / 期待: {nextExpectedIndex}）");
        }
    }

    private void OnConfirmed()
    {
        Debug.Log("ボタン確定！！ " + gameObject.name);

         // 数字キー → NumberTaskManager に数字を送る
        if (isNumberKey && NumberTaskManager.Instance != null)
        {
            NumberTaskManager.Instance.OnDigitConfirmed(keyValue);
        }

        // カウント増加
        confirmCount++;

        // 画面のUIを更新
        if (countText != null)
            countText.text = "Count: " + confirmCount;

        // 見た目変化（研究用にわかりやすく）
        var img = GetComponent<Image>();
        if (img != null)
            img.color = Color.green;

        // Sequential の時だけ決定エリア関連の状態をリセット
        if (selectionMode == SelectionMode.SequentialAreas)
            ResetState();
    }

    private void ResetState()
    {
        // 決定エリアの削除
        foreach (var area in spawnedAreas)
        {
            if (area != null && area.gameObject != null)
                Destroy(area.gameObject);
        }
        spawnedAreas.Clear();

        areasSpawned      = false;
        nextExpectedIndex = 1;
        gazeTimer         = 0f;

        if (activeSequentialButton == this)
            activeSequentialButton = null;
    
        var img = GetComponent<Image>();
        if (img != null)
        {
            img.color = Color.white;
        }

        Debug.Log("GazeButton を初期状態にリセット: " + gameObject.name);
    }
}
