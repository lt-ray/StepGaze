using UnityEngine;
using UnityEngine.UI; 
using TMPro;

public class DotTaskManager : MonoBehaviour
{
    public static DotTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText; // 次に押すべき数字列（例: "53a1b"）
    public TextMeshProUGUI inputText;  // 現在の入力状況

    [Header("タスク設定")]
    // a=10, b=11 として扱われます。例: "12ab", "5a9"
    [SerializeField] private string[] targets = { "5372", "149", "80ab" }; 
    private int currentTrialIndex = 0;

    [Header("色の設定")]
    public Color targetColor = Color.red;   // 押すべきキーの色
    public Color normalColor = Color.white; // それ以外のキーの色

    private string currentTarget = "";
    private string currentInput = "";

    private float trialStartTime = 0f;
    private int totalErrorCount = 0;

    // プロパティ
    public int CurrentTrialIndex => currentTrialIndex;
    public string CurrentTarget => currentTarget;
    public string CurrentInput => currentInput;
    public float CurrentTrialStartTime => trialStartTime;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        StartTrial();
    }

    private void StartTrial()
    {
        currentTarget = targets[currentTrialIndex];
        currentInput = "";

        if (targetText != null) targetText.text = currentTarget;
        if (inputText != null) inputText.text = "";

        trialStartTime = Time.time;

        // 最初のターゲットをハイライト
        HighlightNextKey();

        Debug.Log($"[DotTask] Trial {currentTrialIndex} start. Target = {currentTarget}");
    }

    /// <summary>
    /// 文字('0'-'9', 'a', 'b') を KeyValue (0-11) に変換するヘルパー
    /// </summary>
    private int CharToKeyValue(char c)
    {
        if (char.IsDigit(c))
        {
            return int.Parse(c.ToString());
        }
        else if (c == 'a' || c == 'A')
        {
            return 10;
        }
        else if (c == 'b' || c == 'B')
        {
            return 11;
        }
        // それ以外（エラー対応）
        Debug.LogWarning($"[DotTaskManager] Unknown target char: {c}");
        return -1;
    }

    /// <summary>
    /// KeyValue (0-11) を表示用文字文字列 ("0"-"9", "a", "b") に変換するヘルパー
    /// </summary>
    private string KeyValueToString(int val)
    {
        if (val >= 0 && val <= 9) return val.ToString();
        if (val == 10) return "a";
        if (val == 11) return "b";
        return "?";
    }

    // 次に入力すべきキーの色を変える
    private void HighlightNextKey()
    {
        // 既に入力が完了している場合は何もしない
        if (currentInput.Length >= currentTarget.Length) return;

        // 次のターゲットの文字を取得 (例: 'a')
        char nextChar = currentTarget[currentInput.Length];
        
        // 文字をKeyValueに変換 ('a' -> 10)
        int nextVal = CharToKeyValue(nextChar);

        // 全ボタンを検索して色を適用
        var buttons = FindObjectsOfType<GazeButton>();
        foreach (var btn in buttons)
        {
            // 数字キー以外はスキップ（必要に応じて調整）
            if (!btn.isNumberKey) continue;

            var img = btn.GetComponent<Image>();
            if (img == null) continue;

            if (btn.keyValue == nextVal)
            {
                // 正解のキー
                btn.currentBaseColor = targetColor;
                img.color = targetColor;
            }
            else
            {
                // それ以外
                btn.currentBaseColor = normalColor;
                img.color = normalColor;
            }
        }
    }

    private string GetCurrentConditionString()
    {
        var btn = FindObjectOfType<GazeButton>();
        if (btn == null) return "Unknown";
        return $"{btn.selectionMode}_Areas{(int)btn.decisionAreaCount}_DotTask";
    }

    public void OnDigitConfirmed(int digit)
    {
        // ターゲット長を超えている場合
        if (currentInput.Length >= currentTarget.Length)
        {
            totalErrorCount++;
            return;
        }

        int pos = currentInput.Length;
        
        // ターゲット文字から正解の数値を取得 ('a' -> 10)
        char targetChar = currentTarget[pos];
        int expectedDigit = CharToKeyValue(targetChar);

        // 判定
        bool correct = (digit == expectedDigit);

        // 入力後の文字列を作成 (10が来たら "a" を足す)
        string digitStr = KeyValueToString(digit);
        string after = correct ? currentInput + digitStr : currentInput;

        // ログ出力
        if (ExperimentLogger.Instance != null)
        {
            ExperimentLogger.Instance.LogKeyEvent(
                taskType: "DotTask",
                condition: GetCurrentConditionString(),
                trialIndex: currentTrialIndex,
                keyIndex: pos,
                target: currentTarget,
                expected: expectedDigit.ToString(), // ログには "10" と記録されます（必要なら "a" に変更可）
                inputDigit: digit.ToString(),      // 同上
                isCorrect: correct,
                currentInputAfter: after,
                trialStartTime: trialStartTime,
                totalErrorCount: totalErrorCount
            );
        }

        if (correct)
        {
            currentInput = after;
            if (inputText != null) inputText.text = currentInput;

            // ★ 次のターゲットをハイライト更新
            HighlightNextKey();

            // トライアル完了判定
            if (currentInput.Length == currentTarget.Length)
            {
                float rt = Time.time - trialStartTime;
                Debug.Log($"[DotTask] Trial {currentTrialIndex} COMPLETED. RT = {rt:F3}");

                currentTrialIndex = (currentTrialIndex + 1) % targets.Length;
                StartTrial();
            }
        }
        else
        {
            totalErrorCount++;
            Debug.Log($"[DotTask] WRONG digit {digit} (expected {expectedDigit})");
        }
    }
}