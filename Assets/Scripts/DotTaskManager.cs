using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using System.Collections; 

public class DotTaskManager : MonoBehaviour
{
    public static DotTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText; 
    public TextMeshProUGUI inputText;  

    [Header("エラー演出")]
    public Image errorOverlay;
    public float screenFlashDuration = 0.2f;

    [Header("タスク設定")]
    [SerializeField] private string[] targets = { "5372", "149", "80ab" }; 
    private int currentTrialIndex = 0;

    [Header("色の設定")]
    public Color targetColor = Color.red;   
    public Color normalColor = Color.white; 

    private string currentTarget = "";
    private string currentInput = "";
    private float trialStartTime = 0f;
    private int totalErrorCount = 0;

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
        if (errorOverlay != null) errorOverlay.gameObject.SetActive(false);
        StartTrial();
    }

    private void StartTrial()
    {
        if (currentTrialIndex >= targets.Length)
        {
            Debug.Log("All DotTask Trials Completed.");
            if (targetText != null) targetText.text = "Finish";
            return;
        }

        currentTarget = targets[currentTrialIndex];
        currentInput = "";

        if (targetText != null) targetText.text = currentTarget;
        if (inputText != null) inputText.text = "";

        trialStartTime = Time.time;
        HighlightNextKey();
        Debug.Log($"[DotTask] Trial {currentTrialIndex} start. Target = {currentTarget}");
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

    private void HighlightNextKey()
    {
        if (currentInput.Length >= currentTarget.Length) return;

        char nextChar = currentTarget[currentInput.Length];
        int nextVal = CharToKeyValue(nextChar);

        var buttons = FindObjectsOfType<GazeButton>();
        foreach (var btn in buttons)
        {
            if (!btn.isNumberKey) continue;
            var img = btn.GetComponent<Image>();
            if (img == null) continue;

            if (btn.keyValue == nextVal)
            {
                btn.currentBaseColor = targetColor;
                img.color = targetColor;
            }
            else
            {
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

    public bool OnDigitConfirmed(int digit, float searchTime, float selectionTime, int resetCount)
    {
        if (currentTrialIndex >= targets.Length || currentInput.Length >= currentTarget.Length)
        {
            return false;
        }

        int pos = currentInput.Length;
        char targetChar = currentTarget[pos];
        int expectedDigit = CharToKeyValue(targetChar);

        bool correct = (digit == expectedDigit);
        string digitStr = KeyValueToString(digit);
        
        string after = currentInput + digitStr;

        if (ExperimentLogger.Instance != null)
        {
            ExperimentLogger.Instance.LogKeyEvent(
                taskType: "DotTask",
                condition: GetCurrentConditionString(),
                trialIndex: currentTrialIndex,
                keyIndex: pos,
                target: currentTarget,
                expected: expectedDigit.ToString(), 
                inputDigit: digit.ToString(),      
                isCorrect: correct,
                currentInputAfter: after,
                trialStartTime: trialStartTime,
                totalErrorCount: correct ? totalErrorCount : totalErrorCount + 1,
                searchTime: searchTime,       
                selectionTime: selectionTime, 
                resetCount: resetCount        
            );
        }

        if (correct)
        {
            currentInput += digitStr;
            if (inputText != null) inputText.text = currentInput;

            HighlightNextKey();

            if (currentInput.Length == currentTarget.Length)
            {
                float rt = Time.time - trialStartTime;
                Debug.Log($"[DotTask] Trial {currentTrialIndex} COMPLETED. RT = {rt:F3}");

                currentTrialIndex++;
                StartTrial();
            }
            return true; 
        }
        else
        {
            HandleError(); 
            Debug.Log($"[DotTask] WRONG digit {digit} (expected {expectedDigit}) -> Abort Trial & Next");

            // ★ エラー時は即座に次のトライアルへ
            currentTrialIndex++;
            StartTrial();

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