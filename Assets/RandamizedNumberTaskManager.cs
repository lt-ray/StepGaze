using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class RandomizedNumberTaskManager : MonoBehaviour
{
    public static RandomizedNumberTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText;
    public TextMeshProUGUI inputText;

    [Header("タスク設定")]
    // 数字だけでなく a, b も含められます (例: "53a", "b12")
    [SerializeField] private string[] targets = { "5372", "149", "80" };
    private int currentTrialIndex = 0;

    [Header("キー値の配置シーケンス")]
    // 0-9 と 10(a), 11(b) の12個のキーを想定
    [SerializeField]
    private List<int[]> keyLayoutSequences = new List<int[]>()
    {
        // パターン 1
        new int[] { 1, 4, 2, 3, 5, 6, 9, 7, 8, 10, 11, 0 }, 
        // パターン 2
        new int[] { 0, 9, 8, 7, 6, 5, 4, 3, 2, 1, 11, 10 }, 
        // パターン 3
        new int[] { 10, 11, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }  
    };

    private int currentLayoutIndex = 0; // 現在使用している配置パターンのインデックス

    private string currentTarget = "";
    private string currentInput = "";

    private float trialStartTime = 0f;

    private int totalErrorCount = 0;  // 累計誤選択数

    // 他スクリプトから参照する用プロパティ
    public int CurrentTrialIndex => currentTrialIndex;
    public int CurrentInputLength => currentInput.Length;
    public string CurrentTarget => currentTarget;
    public string CurrentInput => currentInput;
    public float CurrentTrialStartTime => trialStartTime;
    public int CurrentTotalErrorCount => totalErrorCount;

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
        if (inputText != null)  inputText.text  = "";

        trialStartTime = Time.time;

        // キーの配置をシーケンスから設定
        SetKeyLayoutFromSequence();

        Debug.Log($"[RandomizedNumberTask] Trial {currentTrialIndex} start. Target = {currentTarget}, Layout Index = {currentLayoutIndex % keyLayoutSequences.Count}");
    }

    // キーの位置をシーケンスから設定する
    private void SetKeyLayoutFromSequence()
    {
        if (keyLayoutSequences == null || keyLayoutSequences.Count == 0)
        {
            Debug.LogError("[RandomizedNumberTask] Key layout sequences are not defined.");
            return;
        }

        // 現在のインデックスに基づいて配置パターンを取得
        int[] currentLayout = keyLayoutSequences[currentLayoutIndex % keyLayoutSequences.Count];

        // すべての GazeButton を取得
        var buttons = FindObjectsOfType<GazeButton>();

        // ボタンにシーケンスの値をセットし、テキストを更新
        for (int i = 0; i < buttons.Length && i < currentLayout.Length; i++)
        {
            buttons[i].keyValue = currentLayout[i];
            
            // ボタン上のテキストを更新
            var buttonText = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                if (buttons[i].keyValue == 10) buttonText.text = "a";
                else if (buttons[i].keyValue == 11) buttonText.text = "b";
                else buttonText.text = buttons[i].keyValue.ToString();
            }
        }
    }

    /// <summary>
    /// 文字('0'-'9', 'a', 'b') を KeyValue (0-11) に変換するヘルパー
    /// </summary>
    private int CharToKeyValue(char c)
    {
        if (char.IsDigit(c)) return int.Parse(c.ToString());
        else if (c == 'a' || c == 'A') return 10;
        else if (c == 'b' || c == 'B') return 11;
        return -1;
    }

    /// <summary>
    /// KeyValue (0-11) を表示用文字に変換するヘルパー
    /// </summary>
    private string KeyValueToString(int val)
    {
        if (val >= 0 && val <= 9) return val.ToString();
        if (val == 10) return "a";
        if (val == 11) return "b";
        return "?";
    }

    private string GetCurrentConditionString()
    {
        var btn = FindObjectOfType<GazeButton>();
        if (btn == null) return "Unknown";
        return $"{btn.selectionMode}_Areas{(int)btn.decisionAreaCount}";
    }

    public void OnDigitConfirmed(int digit)
    {
        // すでにターゲット長に達している → 余分な入力として誤りカウントだけ
        if (currentInput.Length >= currentTarget.Length)
        {
            totalErrorCount++;
            if (ExperimentLogger.Instance != null)
            {
                ExperimentLogger.Instance.LogKeyEvent(
                    taskType: "Number",
                    condition: GetCurrentConditionString(),
                    trialIndex: currentTrialIndex,
                    keyIndex: currentInput.Length,
                    target: currentTarget,
                    expected: "",
                    inputDigit: digit.ToString(),
                    isCorrect: false,
                    currentInputAfter: currentInput,
                    trialStartTime: trialStartTime,
                    totalErrorCount: totalErrorCount
                );
            }
            return;
        }

        int pos = currentInput.Length;
        
        // ★ 修正点: ターゲット文字を数値に変換して比較
        char targetChar = currentTarget[pos];
        int expectedDigit = CharToKeyValue(targetChar);

        bool correct = (digit == expectedDigit);

        // ★ 修正点: 表示用文字列も対応関数を使用
        string digitStr = KeyValueToString(digit);
        string after = correct ? currentInput + digitStr : currentInput;

        // ログ出力
        if (ExperimentLogger.Instance != null)
        {
            ExperimentLogger.Instance.LogKeyEvent(
                taskType: "Number",
                condition: GetCurrentConditionString(),
                trialIndex: currentTrialIndex,
                keyIndex: pos,
                target: currentTarget,
                expected: expectedDigit.ToString(),
                inputDigit: digit.ToString(),
                isCorrect: correct,
                currentInputAfter: after,
                trialStartTime: trialStartTime,
                totalErrorCount: totalErrorCount
            );
        }

        if (correct)
        {
            currentInput = after;

            if (inputText != null)
                inputText.text = currentInput;

            if (currentInput.Length == currentTarget.Length)
            {
                float rt = Time.time - trialStartTime;
                Debug.Log($"[RandomizedNumberTask] Trial {currentTrialIndex} COMPLETED. RT = {rt:F3}, Errors = {totalErrorCount}");

                currentTrialIndex = (currentTrialIndex + 1) % targets.Length;
                
                // 次のレイアウトパターンへ
                currentLayoutIndex++;
                
                StartTrial();
            }
        }
        else
        {
            totalErrorCount++;
            Debug.Log($"[RandomizedNumberTask] WRONG digit {digit} (expected {expectedDigit}), errors = {totalErrorCount}");
        }
    }
}