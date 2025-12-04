using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 実験ログを CSV に書き出すロガー（シングルトン）
/// 1行 = Keyイベント or Phaseイベント
/// </summary>
public class ExperimentLogger : MonoBehaviour
{
    public static ExperimentLogger Instance;

    [Header("メタ情報")]
    public string participantId = "P001";   // 被験者ID（Inspectorで設定）
    public string fileNamePrefix = "log";   // 例: log_P001_20251129_205028.csv
    public string customFolderName = "Logs"; // persistentDataPath の下に作るフォルダ名

    private string sessionId;
    private string filePath;
    private bool initialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitLogFile();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>CSVファイル作成＋ヘッダ書き込み</summary>
    private void InitLogFile()
    {
        if (initialized) return;

        sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string dir = Application.persistentDataPath;
        if (!string.IsNullOrEmpty(customFolderName))
        {
            dir = Path.Combine(dir, customFolderName);
        }
        Directory.CreateDirectory(dir);

        string fileName = $"{fileNamePrefix}_{participantId}_{sessionId}.csv";
        filePath = Path.Combine(dir, fileName);

        Debug.Log($"[ExperimentLogger] Log file: {filePath}");

        // Key と Phase を同じテーブルに入れるため、共通のヘッダを用意
        string header = string.Join(",",
            "participantId",
            "sessionId",
            "eventType",        // "Key" or "Phase"
            "taskType",         // "Number" など
            "condition",        // "DwellOnly_..." など
            "trialIndex",
            "keyIndex",
            "target",
            "expected",
            "inputDigit",
            "isCorrect",
            "currentInputAfter",
            "phaseFrom",
            "phaseTo",
            "phaseDuration",
            "unityTime",
            "rtFromTrialStart",
            "totalErrorCount"
        );

        try
        {
            File.WriteAllText(filePath, header + "\n");
            initialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError("[ExperimentLogger] Failed to create log file: " + e.Message);
        }
    }

    // =============================
    //  Keyイベント（数字確定など）
    // =============================
    public void LogKeyEvent(
        string taskType,
        string condition,
        int trialIndex,
        int keyIndex,
        string target,
        string expected,
        string inputDigit,
        bool isCorrect,
        string currentInputAfter,
        float trialStartTime,
        int totalErrorCount
    )
    {
        if (!initialized)
        {
            InitLogFile();
            if (!initialized) return;
        }

        float t = Time.time;
        float rtFromTrialStart = t - trialStartTime;

        string line = string.Join(",",
            Escape(participantId),
            Escape(sessionId),
            "Key",                             // eventType
            Escape(taskType),
            Escape(condition),
            trialIndex,
            keyIndex,
            Escape(target),
            Escape(expected),
            Escape(inputDigit),
            isCorrect ? "1" : "0",
            Escape(currentInputAfter),
            "",                                // phaseFrom
            "",                                // phaseTo
            "",                                // phaseDuration
            t.ToString("F4"),
            rtFromTrialStart.ToString("F4"),
            totalErrorCount
        );

        AppendLine(line);
        Debug.Log("[LogKeyEvent] " + line);
    }

    // =============================
    //  Phaseイベント（Button→Area1 など）
    // =============================
    public void LogPhaseEvent(
        string taskType,
        string condition,
        int trialIndex,
        int keyIndex,
        string target,
        string currentInputAfter,
        string phaseFrom,
        string phaseTo,
        float phaseDuration,
        float trialStartTime
    )
    {
        if (!initialized)
        {
            InitLogFile();
            if (!initialized) return;
        }

        float t = Time.time;
        float rtFromTrialStart = t - trialStartTime;

        string line = string.Join(",",
            Escape(participantId),
            Escape(sessionId),
            "Phase",
            Escape(taskType),
            Escape(condition),
            trialIndex,
            keyIndex,
            Escape(target),
            "",                         // expected
            "",                         // inputDigit
            "",                         // isCorrect
            Escape(currentInputAfter),
            Escape(phaseFrom),
            Escape(phaseTo),
            phaseDuration.ToString("F4"),
            t.ToString("F4"),
            rtFromTrialStart.ToString("F4"),
            ""                          // totalErrorCount → Keyイベントのみで使う
        );

        AppendLine(line);
        Debug.Log("[LogPhaseEvent] " + line);
    }

    // =============================
    //  共通ユーティリティ
    // =============================
    private void AppendLine(string line)
    {
        try
        {
            File.AppendAllText(filePath, line + "\n");
        }
        catch (Exception e)
        {
            Debug.LogError("[ExperimentLogger] Failed to write log line: " + e.Message);
        }
    }

    private string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace(",", "_").Replace("\n", " ").Replace("\r", " ");
    }
}
