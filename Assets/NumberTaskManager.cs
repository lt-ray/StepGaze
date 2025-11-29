using UnityEngine;
using TMPro;

public class NumberTaskManager : MonoBehaviour
{
    public static NumberTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText;
    public TextMeshProUGUI inputText;

    [Header("タスク設定")]
    [SerializeField] private string[] targets = { "5372", "149", "80" };
    private int currentTrialIndex = 0;

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

        Debug.Log($"[NumberTask] Trial {currentTrialIndex} start. Target = {currentTarget}");
    }

    private string GetCurrentConditionString()
    {
        // ★ 今はざっくり。必要なら専用の ExperimentConditionManager を作ってもいい
        var btn = FindObjectOfType<GazeButton>();
        if (btn == null) return "Unknown";
        return $"{btn.selectionMode}_Areas{(int)btn.decisionAreaCount}";
    }

    /// <summary>
    /// GazeButton から数字キーが確定されたときに呼ぶ
    /// </summary>
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

        int pos = currentInput.Length;               // 今の桁位置
        int expectedDigit = currentTarget[pos] - '0';
        bool correct = (digit == expectedDigit);

        string after = correct
            ? currentInput + digit.ToString()
            : currentInput;

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
                Debug.Log($"[NumberTask] Trial {currentTrialIndex} COMPLETED. RT = {rt:F3}, Errors = {totalErrorCount}");

                currentTrialIndex = (currentTrialIndex + 1) % targets.Length;
                StartTrial();
            }
        }
        else
        {
            totalErrorCount++;
            Debug.Log($"[NumberTask] WRONG digit {digit} (expected {expectedDigit}), errors = {totalErrorCount}");
        }
    }
}
