using System;
using System.IO;
using System.Text;
using UnityEngine;

public class ExperimentLogger : MonoBehaviour
{
    public static ExperimentLogger Instance;

    [Header("メタ情報")]
    public string participantId = "P001";
    public string customFolderName = "Logs";

    private string trialLogPath;
    private string streamLogPath;
    private StringBuilder streamBuffer = new StringBuilder();
    private const int STREAM_WRITE_FREQUENCY = 1000; 
    private int currentStreamLineCount = 0;
    private bool initialized = false;
    private string sessionId;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        if (!initialized) InitLogFiles();
    }

    private void OnApplicationQuit()
    {
        FlushStreamBuffer(); 
    }

    private void InitLogFiles()
    {
        if (initialized) return;

        sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dir = Path.Combine(Application.persistentDataPath, customFolderName);
        Directory.CreateDirectory(dir);

        string taskName = "Unknown";
        if (FindObjectOfType<DotTaskManager>()) taskName = "DotTask";
        else if (FindObjectOfType<RandomizedNumberTaskManager>()) taskName = "RandomTask";

        string conditionName = "Unknown";
        var btn = FindObjectOfType<GazeButton>();
        if (btn != null) conditionName = btn.GetConditionString();

        string filePrefix = $"{participantId}_{taskName}_{conditionName}_{sessionId}";

        // ★列を追加: Area1_EnterTime, Area2_EnterTime
        trialLogPath = Path.Combine(dir, $"TrialLog_{filePrefix}.csv");
        string trialHeader = "TimeStamp,ParticipantID,Condition,TaskType,TrialNumber,TargetID,SelectedID,IsSuccess,SelectionTime,SearchTime,Area1_EnterTime,Area2_EnterTime,TargetPos_X,TargetPos_Y,HitPos_X,HitPos_Y,ErrorType,ResetCount";
        try { File.WriteAllText(trialLogPath, trialHeader + "\n"); }
        catch (Exception e) { Debug.LogError($"Failed to create Trial Log: {e.Message}"); }

        streamLogPath = Path.Combine(dir, $"StreamLog_{filePrefix}.csv");
        string streamHeader = "TimeStamp,GazePos_X,GazePos_Y,TargetID,CurrentState,IsTracking";
        try { File.WriteAllText(streamLogPath, streamHeader + "\n"); }
        catch (Exception e) { Debug.LogError($"Failed to create Stream Log: {e.Message}"); }

        initialized = true;
    }

    public void LogTrialResult(
        string condition, string taskType, int trialNumber, string targetId, string selectedId,
        bool isSuccess, float selectionTime, float searchTime, 
        float area1EnterTime, float area2EnterTime, // ★追加引数
        Vector2 targetPosScreen, Vector2 hitPosScreen, int resetCount, string errorType = ""
    )
    {
        if (!initialized) return;
        string timeStamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string line = string.Join(",",
            timeStamp, participantId, condition, taskType, trialNumber, targetId, selectedId,
            isSuccess ? "1" : "0",
            selectionTime.ToString("F4"),
            searchTime.ToString("F4"),
            area1EnterTime.ToString("F4"), // ★記録
            area2EnterTime.ToString("F4"), // ★記録
            targetPosScreen.x.ToString("F2"), targetPosScreen.y.ToString("F2"),
            hitPosScreen.x.ToString("F2"), hitPosScreen.y.ToString("F2"),
            errorType, resetCount
        );
        AppendToTrialLog(line);
    }

    private void AppendToTrialLog(string line)
    {
        try { File.AppendAllText(trialLogPath, line + "\n"); }
        catch (Exception e) { Debug.LogError($"Failed to write Trial Log: {e.Message}"); }
    }

    public void LogStreamData(Vector2 gazePos, string currentTargetId, string currentState, bool isTracking)
    {
        if (!initialized) return;
        string timeStamp = DateTime.Now.ToString("HH:mm:ss.fff");
        streamBuffer.Append(timeStamp).Append(",")
                    .Append(gazePos.x.ToString("F1")).Append(",").Append(gazePos.y.ToString("F1")).Append(",")
                    .Append(currentTargetId).Append(",").Append(currentState).Append(",")
                    .Append(isTracking ? "1" : "0").Append("\n");
        currentStreamLineCount++;
        if (currentStreamLineCount >= STREAM_WRITE_FREQUENCY) FlushStreamBuffer();
    }

    private void FlushStreamBuffer()
    {
        if (streamBuffer.Length == 0) return;
        try { File.AppendAllText(streamLogPath, streamBuffer.ToString()); streamBuffer.Clear(); currentStreamLineCount = 0; }
        catch (Exception e) { Debug.LogError($"Failed to flush Stream Log: {e.Message}"); }
    }
}