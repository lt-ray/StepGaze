using UnityEngine;
using TMPro;

public class NumberTaskManager : MonoBehaviour
{
    public static NumberTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText;  // ターゲット数字列の表示
    public TextMeshProUGUI inputText;   // 入力された数字列の表示

    [Header("タスク設定")]
    [SerializeField] private string[] targets = { "5372", "149", "80" };
    private int currentTrialIndex = 0;

    private string currentTarget = "";
    private string currentInput = "";

    private float trialStartTime = 0f;

    // 誤選択カウント（とりあえず全体で1本。必要なら trial ごとに分けてもOK）
    private int totalErrorCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        StartTrial();
    }

    // 1試行開始
    private void StartTrial()
    {
        currentTarget = targets[currentTrialIndex];
        currentInput = "";

        if (targetText != null)
            targetText.text = currentTarget;

        if (inputText != null)
            inputText.text = "";

        trialStartTime = Time.time;

        Debug.Log($"[NumberTask] Trial {currentTrialIndex} start. Target = {currentTarget}");
    }

    // 数字キーが確定されたとき（GazeButtonから呼ぶ）
    public void OnDigitConfirmed(int digit)
    {
        // すでにターゲット長に達している場合：
        // → これ以上正しい入力はできないので「誤選択」としてカウントだけ（無視でも可）
        if (currentInput.Length >= currentTarget.Length)
        {
            totalErrorCount++;
            Debug.Log($"[NumberTask] Extra input (ignored). Digit = {digit}, TotalErrors = {totalErrorCount}");
            return;
        }

        // いま期待している桁の正解値
        int pos = currentInput.Length;              // 0桁目,1桁目,...
        int expectedDigit = currentTarget[pos] - '0';

        if (digit == expectedDigit)
        {
            // ✅ 正しい入力 → input に反映
            currentInput += digit.ToString();

            if (inputText != null)
                inputText.text = currentInput;

            Debug.Log($"[NumberTask] Correct digit {digit} at pos {pos}. Input = {currentInput}");

            // これでターゲット長と等しくなったら trial 終了
            if (currentInput.Length == currentTarget.Length)
            {
                float rt = Time.time - trialStartTime;

                Debug.Log($"[NumberTask] Trial {currentTrialIndex} COMPLETED. " +
                          $"Input = {currentInput}, Target = {currentTarget}, " +
                          $"RT = {rt:F3} sec, TotalErrors = {totalErrorCount}");

                // ★ ここでだけ trial を進める
                currentTrialIndex = (currentTrialIndex + 1) % targets.Length;
                StartTrial();
            }
        }
        else
        {
            // ❌ 誤入力 → 入力欄には反映しない・誤選択だけカウント
            totalErrorCount++;
            Debug.Log($"[NumberTask] WRONG digit {digit} at pos {pos}. " +
                      $"Expected {expectedDigit}. Errors = {totalErrorCount}");
            // currentInput はそのまま
        }
    }
}
