using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class GazeButton : MonoBehaviour
{
    public enum SelectionMode { DwellOnly, SequentialAreas }
    public enum DecisionAreaCount { One = 1, Two = 2, Three = 3 }

    [Header("モード")]
    public SelectionMode selectionMode = SelectionMode.SequentialAreas;
    public DecisionAreaCount decisionAreaCount = DecisionAreaCount.Two;

    [Header("Dwell 設定")]
    public float dwellToConfirm = 0.7f;  
    public Image dwellRingImage;

    [Header("StepGaze 設定")]
    public float dwellToShowAreas = 0.3f;        
    public RectTransform decisionAreaPrefab;     
    public float autoResetTime = 3.0f; 

    [Header("フィードバック")]
    public float correctFeedbackDuration = 0.5f; 
    public float errorFeedbackDuration = 1.0f;   
    public AudioClip correctSound;   
    public AudioClip incorrectSound; 
    public Color correctColor = Color.green; 
    public Color errorColor = Color.red;     

    [Header("アニメーション")]
    public Vector3 hoverScale = new Vector3(1.2f, 1.2f, 1.0f); 
    public float scaleSpeed = 30f; 

    [Header("決定エリア Dwell時間 (0で通過型)")]
    public float area1Dwell = 0.0f;
    public float area2Dwell = 0.0f;
    public float area3Dwell = 0.0f;

    [Header("決定エリア オフセット")]
    [SerializeField] private Vector2 oneAreaOffset1   = new Vector2(80f, 0f);
    [SerializeField] private Vector2 twoAreaOffset1   = new Vector2(-80f, 0f);
    [SerializeField] private Vector2 twoAreaOffset2   = new Vector2( 80f, 0f);
    [SerializeField] private Vector2 threeAreaOffset1 = new Vector2(  0f,  80f);
    [SerializeField] private Vector2 threeAreaOffset2 = new Vector2(-80f, -40f);
    [SerializeField] private Vector2 threeAreaOffset3 = new Vector2( 80f, -40f);

    [Header("決定エリア 色")]
    public Color area1Color = Color.red;
    public Color area2Color = Color.green;
    public Color area3Color = Color.blue;
    public Color confirmedAreaColor = new Color(0.5f, 0.5f, 0.5f, 0.7f); 
    public Color nextAreaColor = Color.yellow;

    [Header("キー設定")]
    public bool isNumberKey = false;
    public int keyValue = 0;

    [Header("UI 参照 (必須)")]
    [Tooltip("ここに「前面の明るい数字」のTextコンポーネントをアサインしてください")]
    public TextMeshProUGUI valueText; 

    [HideInInspector] public Color currentBaseColor = Color.white;

    private RectTransform rect;
    private float gazeTimer = 0f;
    private bool areasSpawned = false;
    private int nextExpectedIndex = 1;
    private List<GazeDecisionArea> spawnedAreas = new List<GazeDecisionArea>();
    private float areaLifeTimer = 0f; 
    private bool dwellLocked = false;  
    private bool isFreezing = false;   
    private AudioSource audioSource;
    private Vector3 originalScale; 
    private int originalSiblingIndex = 0;
    private bool isSortedToFront = false;
    private UnityEngine.UI.LayoutGroup parentLayoutGroup; 

    public TextMeshProUGUI countText;
    static private int confirmCount = 0;
    private static GazeButton activeSequentialButton = null;

    // ログ用変数
    private float buttonGazeStartTime = -1f; 
    private float lastPhaseTime       = -1f; 
    private float firstGazeTime = -1f; 
    private int localResetCount = 0;   
    private bool wasGazedThisButtonLastFrame = false;

    // ★エリア突入時間記録用 (試行開始からの経過時間)
    private float enterArea1Time = 0f;
    private float enterArea2Time = 0f;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        originalScale = transform.localScale; 
        if (valueText == null) valueText = GetComponentInChildren<TextMeshProUGUI>();
        if (valueText != null)
        {
            if (keyValue == 10) valueText.text = "a";
            else if (keyValue == 11) valueText.text = "e";
            else valueText.text = keyValue.ToString();
        }
        var img = GetComponent<Image>();
        if(img != null) currentBaseColor = img.color;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        if (transform.parent != null) parentLayoutGroup = transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>();
    }
   
    private void Update()
    {
        if (isFreezing) return;
        if (GazeManager.Instance == null) return;

        GameObject gazed = GazeManager.Instance.GetGazedUI();
        GazeButton gazedButton = null;
        GazeDecisionArea gazedArea = null;

        if (gazed != null)
        {
            gazedArea = gazed.GetComponentInParent<GazeDecisionArea>();
            if (gazedArea != null && gazedArea.owner != null) gazedButton = gazedArea.owner;
            else gazedButton = gazed.GetComponentInParent<GazeButton>();
        }

        bool onThisButton = (gazedButton == this);
        Vector3 targetScale = onThisButton ? hoverScale : originalScale;
        
        if (onThisButton && !isSortedToFront) SortButtonToFront();
        else if (!onThisButton && !areasSpawned && isSortedToFront) RestoreButtonSort();
        
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);

        if (areasSpawned)
        {
            bool isLookingAtAnyDecisionArea = (gazedArea != null);
            if (onThisButton && !isLookingAtAnyDecisionArea) areaLifeTimer = 0f; 
            else areaLifeTimer += Time.deltaTime;

            if (areaLifeTimer >= autoResetTime) { ResetState(); return; }
        }

        if (selectionMode == SelectionMode.SequentialAreas &&
            activeSequentialButton != null && gazedButton != null && gazedButton != activeSequentialButton)
        {
            activeSequentialButton.ResetState();
        }

        if (onThisButton && !wasGazedThisButtonLastFrame)
        {
            buttonGazeStartTime = Time.time;
            lastPhaseTime       = buttonGazeStartTime;
            if (firstGazeTime < 0f) firstGazeTime = Time.time;
        }

        if (onThisButton)
        {
            gazeTimer += Time.deltaTime;
            if (selectionMode == SelectionMode.DwellOnly)
            {
                if (dwellLocked) { SetDwellProgress(0f); wasGazedThisButtonLastFrame = onThisButton; return; }
                SetDwellProgress(gazeTimer / dwellToConfirm);
                if (gazeTimer >= dwellToConfirm) { OnConfirmed(); dwellLocked = true; SetDwellProgress(0f); }
            }
            else if (selectionMode == SelectionMode.SequentialAreas)
            {
                if (!areasSpawned) SetDwellProgress(gazeTimer / dwellToShowAreas);
                if (!areasSpawned && gazeTimer >= dwellToShowAreas)
                {
                    SpawnDecisionAreas();
                    areasSpawned = true;
                    activeSequentialButton = this;
                    SetDwellProgress(0f);
                }
            }
        }
        else
        {
            gazeTimer = 0f; dwellLocked = false; SetDwellProgress(0f);
            if (selectionMode == SelectionMode.DwellOnly) { var img = GetComponent<Image>(); if (img != null) img.color = currentBaseColor; }
        }
        wasGazedThisButtonLastFrame = onThisButton;
    }

    private void SetDwellProgress(float t) { if (dwellRingImage == null) return; t = Mathf.Clamp01(t); dwellRingImage.enabled = (t > 0f); dwellRingImage.fillAmount = t; }
    private void SortButtonToFront() { if (isSortedToFront) return; originalSiblingIndex = transform.GetSiblingIndex(); if (parentLayoutGroup != null) parentLayoutGroup.enabled = false; transform.SetAsLastSibling(); isSortedToFront = true; }
    private void RestoreButtonSort() { if (!isSortedToFront) return; transform.SetSiblingIndex(originalSiblingIndex); if (parentLayoutGroup != null) parentLayoutGroup.enabled = true; isSortedToFront = false; }
    private int GetDecisionAreaCountValue() => (int)decisionAreaCount;

    private void SpawnDecisionAreas()
    {
        areaLifeTimer = 0f;
        enterArea1Time = 0f; // リセット
        enterArea2Time = 0f;

        if (decisionAreaCount == DecisionAreaCount.One) CreateArea(oneAreaOffset1, 1);
        else if (decisionAreaCount == DecisionAreaCount.Two) { CreateArea(twoAreaOffset1, 1); CreateArea(twoAreaOffset2, 2); }
        else if (decisionAreaCount == DecisionAreaCount.Three) { CreateArea(threeAreaOffset1, 1); CreateArea(threeAreaOffset2, 2); CreateArea(threeAreaOffset3, 3); }
        nextExpectedIndex = 1;
        HighlightNextArea();
    }

    private void CreateArea(Vector2 offset, int index)
    {
        RectTransform parent = rect.parent as RectTransform;
        RectTransform areaRect = Instantiate(decisionAreaPrefab, parent);
        areaRect.anchoredPosition = rect.anchoredPosition + offset;
        var img = areaRect.GetComponent<Image>();
        if (img != null) { if (index == 1) img.color = area1Color; else if (index == 2) img.color = area2Color; else if (index == 3) img.color = area3Color; }
        var script = areaRect.GetComponent<GazeDecisionArea>();
        script.index = index;
        script.owner = this;
        if (index == 1) script.dwellDuration = area1Dwell; else if (index == 2) script.dwellDuration = area2Dwell; else if (index == 3) script.dwellDuration = area3Dwell;
        spawnedAreas.Add(script);
    }

    // ★エリアに入った瞬間に呼ばれる
    public void OnAreaEnter(int index)
    {
        float trialStart = 0f;
        if (DotTaskManager.Instance != null) trialStart = DotTaskManager.Instance.CurrentTrialStartTime;
        else if (RandomizedNumberTaskManager.Instance != null) trialStart = RandomizedNumberTaskManager.Instance.CurrentTrialStartTime;

        float elapsed = Time.time - trialStart;

        // 最初の突入時刻だけ記録（チャタリング防止）
        if (index == 1 && enterArea1Time == 0f) enterArea1Time = elapsed;
        else if (index == 2 && enterArea2Time == 0f) enterArea2Time = elapsed;
    }

    public void OnAreaPassed(int index, GazeDecisionArea area)
    {
        if (isFreezing || selectionMode != SelectionMode.SequentialAreas) return;
        int total = GetDecisionAreaCountValue();
        float now = Time.time;

        if (index == nextExpectedIndex)
        {
            lastPhaseTime = now;
            area.SetColor(confirmedAreaColor); 
            if (nextExpectedIndex == total) OnConfirmed();
            else { nextExpectedIndex++; HighlightNextArea(); }
        }
        else { ResetState(); }
    }

    private void HighlightNextArea()
    {
        foreach (var area in spawnedAreas) { if (area == null) continue; if (area.index == nextExpectedIndex) { area.SetColor(nextAreaColor); break; } }
    }

    private void OnConfirmed()
    {
        float now = Time.time;
        Debug.Log("ボタン確定！！ " + gameObject.name);

        float trialStartTime = 0f;
        if (DotTaskManager.Instance != null) trialStartTime = DotTaskManager.Instance.CurrentTrialStartTime;
        else if (RandomizedNumberTaskManager.Instance != null) trialStartTime = RandomizedNumberTaskManager.Instance.CurrentTrialStartTime;

        float searchTime = (firstGazeTime > 0) ? Mathf.Max(0, firstGazeTime - trialStartTime) : 0;
        float selectionTime = (firstGazeTime > 0) ? (now - firstGazeTime) : 0;

        Vector2 hitPos = Vector2.zero;
        if (GazeManager.Instance != null) hitPos = GazeManager.Instance.GazeScreenPosition;

        Vector2 targetPos = Vector2.zero;
        if (rect != null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace) cam = canvas.worldCamera; 
            targetPos = RectTransformUtility.WorldToScreenPoint(cam, rect.position);
        }

        bool isCorrect = true; 
        if (isNumberKey)
        {
            if (DotTaskManager.Instance != null)
                isCorrect = DotTaskManager.Instance.OnDigitConfirmed(keyValue, searchTime, selectionTime, enterArea1Time, enterArea2Time, localResetCount, targetPos, hitPos);
            else if (RandomizedNumberTaskManager.Instance != null)
                isCorrect = RandomizedNumberTaskManager.Instance.OnDigitConfirmed(keyValue, searchTime, selectionTime, enterArea1Time, enterArea2Time, localResetCount, targetPos, hitPos);
        }

        if (audioSource != null)
        {
            if (isCorrect && correctSound != null) audioSource.PlayOneShot(correctSound);
            else if (!isCorrect && incorrectSound != null) audioSource.PlayOneShot(incorrectSound);
        }

        confirmCount++;
        if (countText != null) countText.text = "Count: " + confirmCount;
        var img = GetComponent<Image>();
        if (img != null) img.color = isCorrect ? correctColor : errorColor;

        float waitTime = isCorrect ? correctFeedbackDuration : errorFeedbackDuration;
        StartCoroutine(WaitAndResetRoutine(waitTime));
        
        buttonGazeStartTime = -1f; lastPhaseTime = -1f; firstGazeTime = -1f; localResetCount = 0;
        enterArea1Time = 0f; enterArea2Time = 0f;
    }

    private IEnumerator WaitAndResetRoutine(float duration) { isFreezing = true; yield return new WaitForSeconds(duration); ResetState(); isFreezing = false; }
    private void ResetState()
    {
        if (!isFreezing && areasSpawned && nextExpectedIndex > 1) localResetCount++;
        foreach (var area in spawnedAreas) if (area != null) Destroy(area.gameObject);
        spawnedAreas.Clear();
        areasSpawned = false; nextExpectedIndex = 1; gazeTimer = 0f; areaLifeTimer = 0f;
        transform.localScale = originalScale; RestoreButtonSort();
        if (activeSequentialButton == this) activeSequentialButton = null;
        var img = GetComponent<Image>(); if (img != null) img.color = currentBaseColor;
        enterArea1Time = 0f; enterArea2Time = 0f;
    }

    public void ResetGazeTime()
    {
        firstGazeTime = -1f; buttonGazeStartTime = -1f; lastPhaseTime = -1f; localResetCount = 0;
        enterArea1Time = 0f; enterArea2Time = 0f;
    }

    public string GetConditionString() => $"{selectionMode}_Areas{(int)decisionAreaCount}";
}