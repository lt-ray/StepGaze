using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 実験ログを CSV に書き出すロガー（シングルトン）
/// </summary>
public class ExperimentLogger : MonoBehaviour
{
    public static ExperimentLogger Instance;

    [Header("メタ情報")]
    public string participantId = "P001";   // 被験者ID
    public string fileNamePrefix = "log";   // ファイル名の接頭辞
    public string customFolderName = "Logs"; // 保存フォルダ名

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

        // ヘッダー定義
        string header = string.Join(",",
            "participantId",
            "sessionId",
            "eventType",        // "Key", "Phase", "Reset" など
            "taskType",         // "Number", "DotTask" など
            "condition",        // "Sequential_Areas2" など
            "trialIndex",
            "keyIndex",
            "target",
            "expected",
            "inputDigit",       // 入力された(またはリセットされた)ボタンの値
            "isCorrect",
            "currentInputAfter",
            "phaseFrom",        // Phaseイベント用 (Reset時は到達ステージを記録)
            "phaseTo",          // Phaseイベント用
            "phaseDuration",    // Phaseイベント用
            "unityTime",
            "rtFromTrialStart", // ターゲット提示からイベント発生までの時間
            "searchTime",       // ★追加: ボタンを最初に見るまでの時間
            "selectionTime",    // ★追加: 最初に見始めてから確定するまでの時間
            "resetCount",       // ★追加: 確定に至らずリセットされた回数
            "totalErrorCount"   // 累積エラー数
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
        int totalErrorCount,
        float searchTime,    // ★追加
        float selectionTime, // ★追加
        int resetCount       // ★追加
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
            "Key",                             
            Escape(taskType),
            Escape(condition),
            trialIndex,
            keyIndex,
            Escape(target),
            Escape(expected),
            Escape(inputDigit),
            isCorrect ? "1" : "0",
            Escape(currentInputAfter),
            "", // phaseFrom
            "", // phaseTo
            "", // phaseDuration
            t.ToString("F4"),
            rtFromTrialStart.ToString("F4"),
            searchTime.ToString("F4"),    // ★
            selectionTime.ToString("F4"), // ★
            resetCount,                   // ★
            totalErrorCount
        );

        AppendLine(line);
        Debug.Log("[LogKeyEvent] " + line);
    }

    // =============================
    //  Resetイベント（失敗・中断）★新規追加
    // =============================
    public void LogResetEvent(
        string taskType,
        string condition,
        int trialIndex,
        int keyIndex,
        string target,
        string buttonValue,  // リセットされたボタンの値
        float trialStartTime,
        int reachedStage     // どこまで進んでリセットされたか (nextExpectedIndex)
    )
    {
        if (!initialized)
        {
            InitLogFile();
            if (!initialized) return;
        }

        float t = Time.time;
        float rtFromTrialStart = t - trialStartTime;

        // Resetイベントとして記録
        // inputDigit にボタンの値を入れ、phaseFrom に到達ステージを入れる
        string line = string.Join(",",
            Escape(participantId),
            Escape(sessionId),
            "Reset",                           // eventType
            Escape(taskType),
            Escape(condition),
            trialIndex,
            keyIndex,
            Escape(target),
            "",                                // expected
            Escape(buttonValue),               // inputDigit (リセットされたボタン)
            "0",                               // isCorrect (失敗扱い)
            "",                                // currentInputAfter
            $"Stage{reachedStage}",            // phaseFrom (どこまで進んだか)
            "Reset",                           // phaseTo
            "",                                // phaseDuration
            t.ToString("F4"),
            rtFromTrialStart.ToString("F4"),
            "", // searchTime
            "", // selectionTime
            "", // resetCount
            ""  // totalErrorCount
        );

        AppendLine(line);
        Debug.Log("[LogResetEvent] " + line);
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
            "",                         
            "",                         
            "",                         
            Escape(currentInputAfter),
            Escape(phaseFrom),
            Escape(phaseTo),
            phaseDuration.ToString("F4"),
            t.ToString("F4"),
            rtFromTrialStart.ToString("F4"),
            "", // searchTime (空欄)
            "", // selectionTime (空欄)
            "", // resetCount (空欄)
            ""  // totalErrorCount (空欄)
        );

        AppendLine(line);
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