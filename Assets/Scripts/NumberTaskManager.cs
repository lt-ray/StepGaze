using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class NumberTaskManager : MonoBehaviour
{
    public static NumberTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText;
    public TextMeshProUGUI inputText;

    [Header("エラー演出")]
    [Tooltip("エラー時に一瞬表示する画面全体の赤いパネル（Image）")]
    public Image errorOverlay;
    [Tooltip("画面全体が赤くなる時間（秒）")]
    public float screenFlashDuration = 0.2f;

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
        if (errorOverlay != null) errorOverlay.gameObject.SetActive(false);
        StartTrial();
    }

    private void StartTrial()
    {
        // 全課題終了チェック
        if (currentTrialIndex >= targets.Length)
        {
            Debug.Log("All NumberTask Trials Completed.");
            if (targetText != null) targetText.text = "Finish";
            return;
        }

        currentTarget = targets[currentTrialIndex];
        currentInput = "";

        if (targetText != null) targetText.text = currentTarget;
        if (inputText != null)  inputText.text  = "";

        trialStartTime = Time.time;

        Debug.Log($"[NumberTask] Trial {currentTrialIndex} start. Target = {currentTarget}");
    }

    private string GetCurrentConditionString()
    {
        var btn = FindObjectOfType<GazeButton>();
        if (btn == null) return "Unknown";
        return $"{btn.selectionMode}_Areas{(int)btn.decisionAreaCount}";
    }

    /// <summary>
    /// GazeButton から数字キーが確定されたときに呼ぶ
    /// 引数を4つ受け取るように変更されています
    /// </summary>
    public bool OnDigitConfirmed(int digit, float searchTime, float selectionTime, int resetCount)
    {
        // 終了チェック
        if (currentTrialIndex >= targets.Length) return false;

        // 入力桁あふれチェック
        if (currentInput.Length >= currentTarget.Length)
        {
            HandleError();
            return false;
        }

        int pos = currentInput.Length;               
        int expectedDigit = currentTarget[pos] - '0'; // char -> int 変換 ('5' -> 5)
        
        bool correct = (digit == expectedDigit);

        // 文字列表現
        string digitStr = digit.ToString();
        if (digit == 10) digitStr = "a";
        if (digit == 11) digitStr = "b";

        string after = currentInput + digitStr;

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
                inputDigit: digitStr,
                isCorrect: correct,
                currentInputAfter: after,
                trialStartTime: trialStartTime,
                totalErrorCount: correct ? totalErrorCount : totalErrorCount + 1,
                searchTime: searchTime,       // ★ここが追加された引数
                selectionTime: selectionTime, // ★
                resetCount: resetCount        // ★
            );
        }

        // --- 共通処理（一発勝負ロジック） ---
        // 正誤に関わらず入力文字を進める
        currentInput = after;

        if (inputText != null)
            inputText.text = currentInput;

        // 桁数が揃ったら次のトライアルへ
        if (currentInput.Length == currentTarget.Length)
        {
            float rt = Time.time - trialStartTime;
            Debug.Log($"[NumberTask] Trial {currentTrialIndex} COMPLETED. RT = {rt:F3}");

            currentTrialIndex++;
            StartTrial();
        }

        if (correct)
        {
            return true; // 正解音
        }
        else
        {
            HandleError();
            Debug.Log($"[NumberTask] WRONG digit {digit} (expected {expectedDigit})");
            return false; // エラー音
        }
    }

    private void HandleError()
    {
        totalErrorCount++;
        StartCoroutine(FlashErrorScreen());
    }

    private IEnumerator FlashErrorScreen()
    {
        if (errorOverlay != null)
        {
            errorOverlay.gameObject.SetActive(true);
            yield return new WaitForSeconds(screenFlashDuration);
            errorOverlay.gameObject.SetActive(false);
        }
    }
}