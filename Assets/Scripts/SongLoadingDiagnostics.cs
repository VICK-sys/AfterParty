using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

public static class SongLoadingDiagnostics
{
    private static string logPath;
    private static int records;
    private static string pendingSong;

    [Conditional("UNITY_WSA")]
    public static void Begin(string song, string difficulty, bool beforeScene = false)
    {
        if (!beforeScene && pendingSong == song && logPath != null)
        {
            pendingSong = null;
            Record("song setup");
            return;
        }
        pendingSong = beforeScene ? song : null;
        try
        {
            logPath = Path.Combine(Application.persistentDataPath, "song-loading.log");
            records = 0;
            if (File.Exists(logPath)) File.Copy(logPath, logPath + ".previous", true);
            File.WriteAllText(logPath, "Song: " + song + "\nDifficulty: " + difficulty + "\nVersion: " + Application.version + "\n");
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            Record(beforeScene ? "scene load begin" : "song setup");
        }
        catch (Exception exception)
        {
            logPath = null;
            UnityEngine.Debug.LogWarning("Song loading diagnostics unavailable: " + exception.Message);
        }
    }

    [Conditional("UNITY_WSA")]
    public static void Record(string step)
    {
        if (logPath == null || records++ >= 1024) return;
        try
        {
            string memory = " unity=" + Profiler.GetTotalAllocatedMemoryLong() + " managed=" + GC.GetTotalMemory(false);
#if ENABLE_WINMD_SUPPORT && !UNITY_EDITOR
            memory += " usage=" + Windows.System.MemoryManager.AppMemoryUsage + " limit=" + Windows.System.MemoryManager.AppMemoryUsageLimit;
#endif
            File.AppendAllText(logPath, DateTime.UtcNow.ToString("O") + memory + " " + step.Replace('\r', ' ').Replace('\n', ' ') + "\n");
        }
        catch (Exception)
        {
            logPath = null;
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Record(type + ": " + message + " " + stack);
    }
}
