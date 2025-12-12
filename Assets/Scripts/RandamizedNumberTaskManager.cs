using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AudioSource))]
public class RandomizedNumberTaskManager : MonoBehaviour
{
    public static RandomizedNumberTaskManager Instance;

    [Header("UI 参照")]
    public TextMeshProUGUI targetText; 
    public TextMeshProUGUI inputText;  

    [Header("エラー演出")]
    public Image errorOverlay;
    public float screenFlashDuration = 0.2f;

    [Header("ボタン参照")]
    [SerializeField] private List<GazeButton> buttons; 

    [Header("音声ガイド設定")]
    [Tooltip("0, 1, ..., 9, a(10), e(11) の順でクリップを登録してください")]
    public List<AudioClip> targetClips; 
    private AudioSource audioSource;

    [Header("実験設定")]
    [Range(0, 4)] public int scenarioID = 0; 

    [System.Serializable]
    public class RandomScenarioData
    {
        public string name;
        public int[] targetValues;
        public int[] targetPositions;
    }

    [Header("シナリオデータ (Inspectorで編集可能)")]
    public List<RandomScenarioData> scenarios = new List<RandomScenarioData>()
    {
        new RandomScenarioData { 
            name = "Pattern A", 
            targetValues = new int[] { 11, 4, 10, 3, 2, 6, 8, 5, 7, 5, 8, 9, 1, 0, 11 }, 
            targetPositions = new int[] { 3, 8, 5, 3, 4, 0, 2, 11, 0, 7, 4, 7, 2, 0, 3 } 
        },
        new RandomScenarioData { 
            name = "Pattern B", 
            targetValues = new int[] { 6, 3, 10, 5, 3, 1, 7, 11, 4, 8, 10, 2, 9, 0, 0 }, 
            targetPositions = new int[] { 0, 8, 11, 4, 7, 10, 2, 9, 0, 2, 11, 0, 11, 6, 8 } 
        },
        new RandomScenarioData { 
            name = "Pattern C", 
            targetValues = new int[] { 9, 3, 8, 0, 4, 6, 10, 1, 10, 7, 2, 11, 5, 1, 6 }, 
            targetPositions = new int[] { 2, 0, 6, 3, 11, 1, 11, 8, 10, 3, 11, 9, 3, 0, 2 } 
        },
        new RandomScenarioData { 
            name = "Pattern D", 
            targetValues = new int[] { 1, 0, 4, 6, 10, 1, 9, 2, 7, 10, 3, 3, 5, 8, 11 }, 
            targetPositions = new int[] { 10, 8, 2, 7, 0, 8, 3, 0, 1, 4, 11, 2, 10, 4, 7 } 
        },
        new RandomScenarioData { 
            name = "Pattern E", 
            targetValues = new int[] { 11, 11, 2, 9, 0, 6, 7, 2, 1, 1, 3, 8, 4, 10, 5 }, 
            targetPositions = new int[] { 7, 4, 7, 6, 3, 4, 11, 7, 6, 4, 0, 11, 8, 11, 8 } 
        }
    };

    [Header("配置シャッフル用のシード値")]
    public int[] layoutSeeds = { 12345, 67890, 11111, 22222, 33333 };

    private int currentTrialIndex = 0; 
    private int currentCharIndex = 0;  
    private string currentInput = ""; 
    private float trialStartTime = 0f;
    private int totalErrorCount = 0;

    public int CurrentTrialIndex => currentTrialIndex;
    public int CurrentInputLength => currentInput.Length;
    public string CurrentTarget => GetCurrentTargetString();
    public string CurrentInput => currentInput;
    public float CurrentTrialStartTime => trialStartTime;
    public int CurrentTotalErrorCount => totalErrorCount;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        // ★ループ設定をONにする
        audioSource.loop = true;
    }

    private void Start()
    {
        if (errorOverlay != null) errorOverlay.gameObject.SetActive(false);
        if (inputText != null) inputText.text = "";
        
        scenarioID = Mathf.Clamp(scenarioID, 0, scenarios.Count - 1);
        Debug.Log($"[RandomTask] Initialized with Scenario {scenarioID}");

        StartTrial();
    }

    private void Update()
    {
        if (currentTrialIndex >= 15) return; 
        if (ExperimentLogger.Instance == null || GazeManager.Instance == null) return;
        ExperimentLogger.Instance.LogStreamData(GazeManager.Instance.GazeScreenPosition, CurrentTarget, "RandomTask_Searching", true);
    }

    private void StartTrial()
    {
        if (currentTrialIndex >= 15)
        {
            Debug.Log("All Randomized Tasks Completed.");
            if (targetText != null) targetText.text = "Finish";
            
            // ★終了時は音を止める
            if (audioSource.isPlaying) audioSource.Stop();
            
            return;
        }

        currentCharIndex = 0;
        currentInput = "";
        if (inputText != null) inputText.text = "";

        // ★音声再生開始（ループ）
        PlayTargetVoiceLoop();

        trialStartTime = Time.time;

        var allButtons = FindObjectsOfType<GazeButton>();
        foreach (var btn in allButtons) btn.ResetGazeTime();

        ApplyLayoutForCurrentTrial();
        UpdateUI();

        Debug.Log($"[RandomizedNumberTask] Trial {currentTrialIndex} start. Target = {CurrentTarget}");
    }

    // ★ループ再生用メソッドに変更
    private void PlayTargetVoiceLoop()
    {
        if (currentTrialIndex >= 15) return;
        
        int id = Mathf.Clamp(scenarioID, 0, scenarios.Count - 1);
        int val = scenarios[id].targetValues[currentTrialIndex]; 

        if (targetClips != null && val < targetClips.Count)
        {
            if (targetClips[val] != null)
            {
                // 現在再生中の音があれば止めて、新しい音をループ再生する
                audioSource.Stop();
                audioSource.clip = targetClips[val];
                audioSource.loop = true; // ループ有効化
                audioSource.Play();
            }
        }
    }

    private void UpdateUI()
    {
        if (currentTrialIndex >= 15) return;
        string fullTarget = GetCurrentTargetString();
        // 画面上部にも一応表示（不要ならコメントアウト可）
        if (targetText != null) targetText.text = fullTarget; 
    }

    private string GetCurrentTargetString()
    {
        if (currentTrialIndex >= 15) return "";
        int id = Mathf.Clamp(scenarioID, 0, scenarios.Count - 1);
        int val = scenarios[id].targetValues[currentTrialIndex];
        return KeyValueToString(val);
    }

    private void ApplyLayoutForCurrentTrial()
    {
        if (buttons == null || buttons.Count < 12) return;

        int id = Mathf.Clamp(scenarioID, 0, scenarios.Count - 1);
        int targetVal = scenarios[id].targetValues[currentTrialIndex];
        int targetPos = scenarios[id].targetPositions[currentTrialIndex];

        Random.InitState(layoutSeeds[id] + currentTrialIndex);

        List<int> distractors = new List<int>();
        for (int i = 0; i <= 11; i++)
        {
            if (i != targetVal) distractors.Add(i);
        }

        int n = distractors.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            int value = distractors[k];
            distractors[k] = distractors[n];
            distractors[n] = value;
        }

        int distractorIdx = 0;
        for (int i = 0; i < buttons.Count; i++)
        {
            int valToSet;
            if (i == targetPos)
            {
                valToSet = targetVal;
            }
            else
            {
                valToSet = distractors[distractorIdx];
                distractorIdx++;
            }

            buttons[i].keyValue = valToSet;
            
            // valueTextのみ変更
            if (buttons[i].valueText != null)
            {
                buttons[i].valueText.text = KeyValueToString(valToSet);
            }
        }
    }

    private int CharToKeyValue(char c)
    {
        if (char.IsDigit(c)) return int.Parse(c.ToString());
        else if (c == 'a' || c == 'A') return 10;
        else if (c == 'e' || c == 'E') return 11; // 'e'
        return -1;
    }

    private string KeyValueToString(int val)
    {
        if (val >= 0 && val <= 9) return val.ToString();
        if (val == 10) return "a";
        if (val == 11) return "e"; // 'e'
        return "?";
    }

    private string GetCurrentConditionString()
    {
        if (buttons != null && buttons.Count > 0 && buttons[0] != null)
            return buttons[0].GetConditionString();
        return "Unknown";
    }

    public bool OnDigitConfirmed(int digit, float searchTime, float selectionTime, float t1, float t2, int resetCount, Vector2 targetPos, Vector2 hitPos)
    {
        if (currentTrialIndex >= 15) return false;

        string fullTarget = GetCurrentTargetString();
        bool correct = (KeyValueToString(digit) == fullTarget);

        if (ExperimentLogger.Instance != null)
        {
            string errorType = correct ? "" : "MidasTouch";
            ExperimentLogger.Instance.LogTrialResult(
                condition: GetCurrentConditionString(),
                taskType: "RandomNumber",
                trialNumber: currentTrialIndex,
                targetId: fullTarget,
                selectedId: KeyValueToString(digit),
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

        float rt = Time.time - trialStartTime;
        Debug.Log($"[RandomTask] Trial {currentTrialIndex} END. Result={(correct?"OK":"NG")} RT={rt:F3}");
        
        currentTrialIndex++;
        StartTrial(); // 次の試行へ（ここで音が切り替わります）

        if (correct) return true; 
        else { HandleError(); return false; }
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