using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class RandomizedNumberTaskManager : MonoBehaviour
{
    public static RandomizedNumberTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText; // 現在の1文字だけを表示
    public TextMeshProUGUI inputText;  // ★入力履歴（今回は表示更新しませんが、参照は残します）

    [Header("エラー演出")]
    public Image errorOverlay;
    public float screenFlashDuration = 0.2f;

    [Header("ボタン参照")]
    [SerializeField] private List<GazeButton> buttons; 

    [Header("タスク設定")]
    [SerializeField] private string[] targets = { "5372", "149", "80" };
    private int currentTrialIndex = 0; // どのターゲット文字列か
    private int currentCharIndex = 0;  // その文字列の何文字目か

    [System.Serializable]
    public class KeyLayoutData
    {
        public int[] keys;
    }

    [Header("キー値の配置シーケンス")]
    [SerializeField]
    private List<KeyLayoutData> keyLayoutSequences = new List<KeyLayoutData>()
    {
        new KeyLayoutData { keys = new int[] { 1, 4, 2, 3, 5, 6, 9, 7, 8, 10, 11, 0 } },
        new KeyLayoutData { keys = new int[] { 0, 9, 8, 7, 6, 5, 4, 3, 2, 1, 11, 10 } },
        new KeyLayoutData { keys = new int[] { 10, 11, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 } }
    };

    private string currentInput = ""; 
    private float trialStartTime = 0f;
    private int totalErrorCount = 0;

    public int CurrentTrialIndex => currentTrialIndex;
    public int CurrentInputLength => currentInput.Length;
    public string CurrentTarget => (targets.Length > currentTrialIndex) ? targets[currentTrialIndex] : "";
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
        if (currentTrialIndex >= targets.Length)
        {
            Debug.Log("All Randomized Tasks Completed.");
            if (targetText != null) targetText.text = "Finish";
            return;
        }

        currentCharIndex = 0;
        currentInput = "";

        // ★開始時にテキストを空にする（以降は更新しないので見えなくなります）
        if (inputText != null) inputText.text = "";

        trialStartTime = Time.time;

        ShuffleLayout();
        UpdateUI();

        Debug.Log($"[RandomizedNumberTask] Trial {currentTrialIndex} start. Target String = {targets[currentTrialIndex]}");
    }

    private void UpdateUI()
    {
        if (currentTrialIndex >= targets.Length) return;

        string fullTarget = targets[currentTrialIndex];
        
        // ターゲット（現在の1文字）のみ表示
        if (currentCharIndex < fullTarget.Length)
        {
            if (targetText != null) 
                targetText.text = fullTarget[currentCharIndex].ToString();
        }
        else
        {
            if (targetText != null) targetText.text = "";
        }
    }

    private void ShuffleLayout()
    {
        if (keyLayoutSequences == null || keyLayoutSequences.Count == 0) return;
        if (buttons == null || buttons.Count == 0) return;

        int randIndex = Random.Range(0, keyLayoutSequences.Count);
        int[] currentLayout = keyLayoutSequences[randIndex].keys;

        for (int i = 0; i < buttons.Count && i < currentLayout.Length; i++)
        {
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
        if (buttons != null && buttons.Count > 0 && buttons[0] != null)
            return $"{buttons[0].selectionMode}_Areas{(int)buttons[0].decisionAreaCount}";
        return "Unknown";
    }

    public bool OnDigitConfirmed(int digit, float searchTime, float selectionTime, int resetCount)
    {
        if (currentTrialIndex >= targets.Length) return false;

        string fullTarget = targets[currentTrialIndex];
        if (currentCharIndex >= fullTarget.Length) return false;

        char targetChar = fullTarget[currentCharIndex];
        int expectedDigit = CharToKeyValue(targetChar);
        bool correct = (digit == expectedDigit);

        if (ExperimentLogger.Instance != null)
        {
            ExperimentLogger.Instance.LogKeyEvent(
                taskType: "Number", 
                condition: GetCurrentConditionString(),
                trialIndex: currentTrialIndex,
                keyIndex: currentCharIndex,
                target: fullTarget, 
                expected: expectedDigit.ToString(), 
                inputDigit: digit.ToString(),
                isCorrect: correct,
                currentInputAfter: currentInput + KeyValueToString(digit),
                trialStartTime: trialStartTime,
                totalErrorCount: correct ? totalErrorCount : totalErrorCount + 1,
                searchTime: searchTime,       
                selectionTime: selectionTime, 
                resetCount: resetCount        
            );
        }

        // 共通処理
        currentCharIndex++;
        currentInput += KeyValueToString(digit);

        // ★ inputText.text = currentInput; を削除しました（画面表示しない）
        // if (inputText != null) inputText.text = currentInput; 

        ShuffleLayout();
        UpdateUI();

        if (currentCharIndex >= fullTarget.Length)
        {
            float rt = Time.time - trialStartTime;
            Debug.Log($"[RandomizedNumberTask] Trial {currentTrialIndex} COMPLETED. RT = {rt:F3}");
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