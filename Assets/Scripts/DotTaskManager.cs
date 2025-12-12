using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using System.Collections.Generic;
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

    [Header("実験設定")]
    [Range(0, 4)] public int scenarioID = 0; 

    [System.Serializable]
    public class ScenarioData { public string[] targets; }

    [Header("シナリオデータ (Inspectorで編集可能)")]
    public List<ScenarioData> scenarios = new List<ScenarioData>()
    {
        new ScenarioData { targets = new string[] { "0", "e", "0", "9", "2", "8", "7", "4", "3", "4", "1", "e", "6", "9", "3", "5", "6", "1", "7", "5", "2", "a", "8", "a" } },
        new ScenarioData { targets = new string[] { "a", "1", "a", "2", "4", "7", "6", "5", "8", "2", "0", "7", "1", "9", "0", "e", "4", "6", "8", "3", "e", "5", "3", "9" } },
        new ScenarioData { targets = new string[] { "1", "5", "0", "6", "5", "2", "8", "6", "4", "3", "9", "a", "7", "2", "9", "7", "8", "3", "4", "e", "1", "a", "0", "e" } },
        new ScenarioData { targets = new string[] { "e", "4", "6", "2", "8", "3", "5", "1", "a", "1", "9", "7", "0", "a", "4", "6", "8", "7", "2", "5", "e", "0", "9", "3" } },
        new ScenarioData { targets = new string[] { "6", "9", "5", "9", "1", "7", "4", "5", "2", "8", "3", "a", "2", "0", "e", "8", "3", "0", "7", "4", "e", "1", "a", "6" } }
    };

    private List<string> currentTargetList = new List<string>();
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
        if (Instance == null) Instance = this; else Destroy(gameObject);
    }

    private void Start()
    {
        if (errorOverlay != null) errorOverlay.gameObject.SetActive(false);
        LoadScenario();
        StartTrial();
    }

    private void Update()
    {
        if (currentTrialIndex >= currentTargetList.Count) return;
        if (ExperimentLogger.Instance == null || GazeManager.Instance == null) return;
        ExperimentLogger.Instance.LogStreamData(GazeManager.Instance.GazeScreenPosition, currentTarget, "DotTask_Running", true);
    }

    private void LoadScenario()
    {
        currentTargetList.Clear();
        int id = Mathf.Clamp(scenarioID, 0, scenarios.Count - 1);
        if (scenarios[id].targets != null) currentTargetList.AddRange(scenarios[id].targets);
        Debug.Log($"[DotTask] Loaded Scenario {id}: {currentTargetList.Count} targets.");
    }

    private void StartTrial()
    {
        if (currentTrialIndex >= currentTargetList.Count)
        {
            Debug.Log("All DotTask Trials Completed.");
            if (targetText != null) targetText.text = "Finish";
            return;
        }
        currentTarget = currentTargetList[currentTrialIndex];
        currentInput = "";
        if (targetText != null) targetText.text = currentTarget;
        if (inputText != null) inputText.text = "";
        trialStartTime = Time.time;
        var allButtons = FindObjectsOfType<GazeButton>();
        foreach (var btn in allButtons) btn.ResetGazeTime();
        HighlightNextKey();
        Debug.Log($"[DotTask] Trial {currentTrialIndex} start. Target = {currentTarget}");
    }

    private int CharToKeyValue(char c)
    {
        if (char.IsDigit(c)) return int.Parse(c.ToString());
        else if (c == 'a' || c == 'A') return 10;
        else if (c == 'e' || c == 'E') return 11;
        return -1;
    }

    private string KeyValueToString(int val)
    {
        if (val >= 0 && val <= 9) return val.ToString();
        if (val == 10) return "a";
        if (val == 11) return "e";
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
            if (btn.keyValue == nextVal) { btn.currentBaseColor = targetColor; img.color = targetColor; }
            else { btn.currentBaseColor = normalColor; img.color = normalColor; }
        }
    }

    private string GetCurrentConditionString()
    {
        var btn = FindObjectOfType<GazeButton>();
        if (btn == null) return "Unknown";
        return btn.GetConditionString();
    }

    public bool OnDigitConfirmed(int digit, float searchTime, float selectionTime, float t1, float t2, int resetCount, Vector2 targetPos, Vector2 hitPos)
    {
        if (currentTrialIndex >= currentTargetList.Count) return false;
        int pos = currentInput.Length;
        if (pos >= currentTarget.Length) return false;
        char targetChar = currentTarget[pos];
        int expectedDigit = CharToKeyValue(targetChar);
        bool correct = (digit == expectedDigit);
        string digitStr = KeyValueToString(digit);
        
        if (ExperimentLogger.Instance != null)
        {
            string errorType = correct ? "" : "SelectionError";
            ExperimentLogger.Instance.LogTrialResult(
                condition: GetCurrentConditionString(),
                taskType: "DotTask",
                trialNumber: currentTrialIndex,
                targetId: expectedDigit.ToString(),
                selectedId: digit.ToString(),
                isSuccess: correct,
                selectionTime: selectionTime,
                searchTime: searchTime,
                area1EnterTime: t1,
                area2EnterTime: t2,
                targetPosScreen: targetPos,
                hitPosScreen: hitPos,
                resetCount: resetCount,
                errorType: errorType
            );
        }

        if (correct)
        {
            currentInput += digitStr;
            if (inputText != null) inputText.text = currentInput;
            HighlightNextKey();
            if (currentInput.Length == currentTarget.Length) { currentTrialIndex++; StartTrial(); }
            return true; 
        }
        else { HandleError(); currentTrialIndex++; StartTrial(); return false; }
    }

    private void HandleError() { totalErrorCount++; StartCoroutine(FlashErrorScreen()); }
    private IEnumerator FlashErrorScreen()
    {
        if (errorOverlay != null) { errorOverlay.gameObject.SetActive(true); yield return new WaitForSeconds(screenFlashDuration); errorOverlay.gameObject.SetActive(false); }
    }
}