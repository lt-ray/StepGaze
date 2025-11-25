using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GazeButton : MonoBehaviour
{
    public enum SelectionMode
    {
        DwellOnly,         // dwell だけで確定
        SequentialAreas    // 提案方式：決定エリア1→2
    }

    [Header("モード")]
    public SelectionMode selectionMode = SelectionMode.SequentialAreas;

    [Header("Dwell 設定")]
    public float dwellToConfirm = 0.5f;          // dwell方式: 何秒見たら確定？

    [Header("決定エリア方式設定")]
    public float dwellToShowAreas = 0.3f;        // ボタン上で何秒見たら決定エリアを出すか
    public RectTransform decisionAreaPrefab;     // 決定エリアのプレハブ

    [Header("決定エリアの配置オフセット")]
    public Vector2 area1Offset = new Vector2(-80f, 0f);   // エリア1用（デフォルト左）
    public Vector2 area2Offset = new Vector2( 80f, 0f);   // エリア2用（デフォルト右）


    private RectTransform rect;
    private float gazeTimer = 0f;
    

    // 提案方式用
    private bool areasSpawned = false;
    private int sequenceState = 0;               // 0:まだ  1:エリア1 通過  2:確定済み
    private List<GameObject> spawnedAreas = new List<GameObject>();

    private bool dwellLocked = false;  // Dwell方式：確定後、離脱までロック

    [Header("デバッグ用表示")]
    public TextMeshProUGUI countText;
    static private int confirmCount = 0;



    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (GazeManager.Instance == null) return;

        GameObject gazed = GazeManager.Instance.GetGazedUI();

        bool onThisButton = false;
        if (gazed != null)
        {
            // Text / Image など子オブジェクトに当たってもOKにする
            var button = gazed.GetComponentInParent<GazeButton>();
            onThisButton = (button == this);
        }

        if (onThisButton)
        {
            gazeTimer += Time.deltaTime;
            Debug.Log("GazeButton 視線中: " + gameObject.name + "  タイマー: " + gazeTimer.ToString("F2"));

            if (selectionMode == SelectionMode.DwellOnly)
{
                // すでに確定済みなら、離脱するまで何もしない
                if (dwellLocked) return;

                // 🔵 dwell方式：一定時間見たらそのまま確定
                if (gazeTimer >= dwellToConfirm)
                {
                    Debug.Log("DWELL方式で確定: " + gameObject.name);
                    OnConfirmed();

                    // ★ 確定したので、視線が離れるまでロック
                    dwellLocked = true;
                }
            }
            else if (selectionMode == SelectionMode.SequentialAreas)
            {
                // 🟡 提案方式：一定時間見たら決定エリアを出す
                if (!areasSpawned && gazeTimer >= dwellToShowAreas)
                {
                    Debug.Log("DWELL完了 → 決定エリア生成: " + gameObject.name);
                    SpawnDecisionAreas();
                    areasSpawned = true;
                }
            }
        }
        else  // 視線が外れた
        {
            gazeTimer = 0f;
            dwellLocked = false;

            var img = GetComponent<Image>();
            if (img != null)
                img.color = Color.white;
        }

    }

    // ==========================
    // 決定エリア方式 用の処理
    // ==========================

    private void SpawnDecisionAreas()
    {
        CreateArea(area1Offset, 1);
        CreateArea(area2Offset, 2);
    }

    private void CreateArea(Vector2 offset, int index)
    {
        RectTransform parent = rect.parent as RectTransform;
        RectTransform area = Instantiate(decisionAreaPrefab, parent);

        // ボタンの位置 + 個別オフセット
        area.anchoredPosition = rect.anchoredPosition + offset;

        var script = area.GetComponent<GazeDecisionArea>();
        script.index = index;
        script.owner = this;

        spawnedAreas.Add(area.gameObject);
    }

    public void OnAreaPassed(int index)
    {
        if (selectionMode != SelectionMode.SequentialAreas) return;

        if (sequenceState == 0 && index == 1)
        {
            sequenceState = 1;
            Debug.Log("決定エリア1 通過");
        }
        else if (sequenceState == 1 && index == 2)
        {
            sequenceState = 2;
            OnConfirmed();
        }
    }

    private void OnConfirmed()
    {
        Debug.Log("ボタン確定！！ " + gameObject.name);

        // カウント増加
        confirmCount++;

        // 画面のUIを更新
        if (countText != null)
            countText.text = "Count: " + confirmCount;

        // 見た目変化（研究用にわかりやすく）
        var img = GetComponent<Image>();
        if (img != null)
            img.color = Color.green;

        // Sequential の時だけリセット
        if (selectionMode == SelectionMode.SequentialAreas)
            ResetState();
    }


    private void ResetState()
    {
        // 決定エリアの削除（Sequential のときだけ実際に何か入っている）
        foreach (var area in spawnedAreas)
        {
            if (area != null) Destroy(area);
        }
        spawnedAreas.Clear();

        areasSpawned  = false;
        sequenceState = 0;
        gazeTimer     = 0f;

        // 見た目も元に戻したい場合
        var img = GetComponent<Image>();
        if (img != null)
        {
            img.color = Color.white;
        }

        Debug.Log("GazeButton を初期状態にリセット: " + gameObject.name);
    }
}
