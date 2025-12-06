using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class RandomizedNumberTaskManager : MonoBehaviour
{
    public static RandomizedNumberTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText;
    public TextMeshProUGUI inputText;

    // --- 【修正箇所 1】 ボタンをInspectorから指定できるように変更 ---
    [Header("ボタン参照 (レイアウト順に合わせて登録)")]
    [Tooltip("パターンの数値が適用される順番でボタンを登録してください。\n例: Element 0 = 左上のボタン")]
    [SerializeField] private List<GazeButton> buttons; 

    [Header("タスク設定")]
    // 数字だけでなく a, b も含められます (例: "53a", "b12")
    [SerializeField] private string[] targets = { "5372", "149", "80" };
    private int currentTrialIndex = 0;

    [System.Serializable]
    public class KeyLayoutData
    {
        public int[] keys;
    }

    [Header("キー値の配置シーケンス")]
    [SerializeField]
    private List<KeyLayoutData> keyLayoutSequences = new List<KeyLayoutData>()
    {
        // パターン 1
        new KeyLayoutData { keys = new int[] { 1, 4, 2, 3, 5, 6, 9, 7, 8, 10, 11, 0 } },
        // パターン 2
        new KeyLayoutData { keys = new int[] { 0, 9, 8, 7, 6, 5, 4, 3, 2, 1, 11, 10 } },
        // パターン 3
        new KeyLayoutData { keys = new int[] { 10, 11, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 } }
    };

    private int currentLayoutIndex = 0; 
    private string currentTarget = "";
    private string currentInput = "";
    private float trialStartTime = 0f;
    private int totalErrorCount = 0;

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
        if (targets.Length == 0) return;

        currentTarget = targets[currentTrialIndex];
        currentInput = "";

        if (targetText != null) targetText.text = currentTarget;
        if (inputText != null) inputText.text = "";

        trialStartTime = Time.time;

        SetKeyLayoutFromSequence();

        Debug.Log($"[RandomizedNumberTask] Trial {currentTrialIndex} start. Target = {currentTarget}, Layout Index = {currentLayoutIndex % keyLayoutSequences.Count}");
    }

    private void SetKeyLayoutFromSequence()
    {
        if (keyLayoutSequences == null || keyLayoutSequences.Count == 0)
        {
            Debug.LogError("[RandomizedNumberTask] Key layout sequences are not defined.");
            return;
        }

        // --- 【修正箇所 2】 事前に登録されたボタンリストがない場合のチェック ---
        if (buttons == null || buttons.Count == 0)
        {
            Debug.LogError("[RandomizedNumberTask] Buttons are not assigned in the Inspector.");
            return;
        }

        int[] currentLayout = keyLayoutSequences[currentLayoutIndex % keyLayoutSequences.Count].keys;

        // --- 【修正箇所 3】 FindObjectsOfType を削除し、this.buttons を使用 ---
        // Inspectorで登録された順序に従って値を割り当てます
        for (int i = 0; i < buttons.Count && i < currentLayout.Length; i++)
        {
            // リスト内のボタンがnull（削除済みなど）でないか確認
            if (buttons[i] == null) continue;

            buttons[i].keyValue = currentLayout[i];

            var buttonText = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                if (buttons[i].keyValue == 10) buttonText.text = "a";
                else if (buttons[i].keyValue == 11) buttonText.text = "b";
                else buttonText.text = buttons[i].keyValue.ToString();
            }
        }
    }

    private int CharToKeyValue(char c)
    {
        if (char.IsDigit(c)) return int.Parse(c.ToString());
        else if (c == 'a' || c == 'A') return 10;
        else if (c == 'b' || c == 'B') return 11;
        return -1;
    }

    private string KeyValueToString(int val)
    {
        if (val >= 0 && val <= 9) return val.ToString();
        if (val == 10) return "a";
        if (val == 11) return "b";
        return "?";
    }

    private string GetCurrentConditionString()
    {
        // 最初のボタンから状態を取得（全ボタン同じ設定と仮定）
        if (buttons != null && buttons.Count > 0 && buttons[0] != null)
        {
            return $"{buttons[0].selectionMode}_Areas{(int)buttons[0].decisionAreaCount}";
        }
        return "Unknown";
    }

    public void OnDigitConfirmed(int digit)
    {
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
        char targetChar = currentTarget[pos];
        int expectedDigit = CharToKeyValue(targetChar);

        bool correct = (digit == expectedDigit);

        string digitStr = KeyValueToString(digit);
        string after = correct ? currentInput + digitStr : currentInput;

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