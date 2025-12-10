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
    public Image errorOverlay;
    public float screenFlashDuration = 0.2f;

    [Header("タスク設定")]
    [SerializeField] private string[] targets = { "5372", "149", "80" };
    private int currentTrialIndex = 0;

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
        if (errorOverlay != null) errorOverlay.gameObject.SetActive(false);
        StartTrial();
    }

    // ★ ストリームログ
    private void Update()
    {
        if (currentTrialIndex >= targets.Length) return;
        if (ExperimentLogger.Instance == null || GazeManager.Instance == null) return;

        ExperimentLogger.Instance.LogStreamData(
            GazeManager.Instance.GazeScreenPosition,
            currentTarget,
            "NumberTask_Running",
            true
        );
    }

    private void StartTrial()
    {
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

        // ★ GazeTimeリセット
        var allButtons = FindObjectsOfType<GazeButton>();
        foreach (var btn in allButtons)
        {
            btn.ResetGazeTime();
        }

        Debug.Log($"[NumberTask] Trial {currentTrialIndex} start. Target = {currentTarget}");
    }

    private string GetCurrentConditionString()
    {
        var btn = FindObjectOfType<GazeButton>();
        if (btn == null) return "Unknown";
        return btn.GetConditionString();
    }

    public bool OnDigitConfirmed(int digit, float searchTime, float selectionTime, int resetCount, Vector2 targetPos, Vector2 hitPos)
    {
        if (currentTrialIndex >= targets.Length) return false;

        if (currentInput.Length >= currentTarget.Length)
        {
            HandleError();
            return false;
        }

        int pos = currentInput.Length;               
        int expectedDigit = currentTarget[pos] - '0';
        
        bool correct = (digit == expectedDigit);

        string digitStr = digit.ToString();
        if (digit == 10) digitStr = "a";
        if (digit == 11) digitStr = "b";

        string after = currentInput + digitStr;

        // ★ ログ出力
        if (ExperimentLogger.Instance != null)
        {
            string errorType = correct ? "" : "SelectionError";

            ExperimentLogger.Instance.LogTrialResult(
                condition: GetCurrentConditionString(),
                taskType: "Number",
                trialNumber: currentTrialIndex,
                targetId: expectedDigit.ToString(),
                selectedId: digitStr,
                isSuccess: correct,
                selectionTime: selectionTime,
                searchTime: searchTime,
                targetPosScreen: targetPos,
                hitPosScreen: hitPos,
                resetCount: resetCount,
                errorType: errorType
            );
        }

        currentInput = after;
        if (inputText != null) inputText.text = currentInput;

        if (currentInput.Length == currentTarget.Length)
        {
            currentTrialIndex++;
            StartTrial();
        }

        if (correct)
        {
            return true;
        }
        else
        {
            HandleError();
            return false;
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