using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SongAudioLoadingValidation
{
    private static IEnumerator routine;
    private static SongAudioLoader.Result result;
    private static string directory;
    private static string validPath;
    private static int phase;
    private static double deadline;
    private static float previousTimeScale;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run audio validation in a batch editor.");
        directory = Path.Combine(Path.GetTempPath(), "AfterPartyAudio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        validPath = Path.Combine(directory, "Audio # 100% escaped.ogg");
        string source = Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "Bundles"), "Inst.ogg", SearchOption.AllDirectories)[0];
        File.Copy(source, validPath);
        File.WriteAllText(Path.Combine(directory, "corrupt.ogg"), "Invalid audio control");
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0;
        phase = 0;
        StartCase(validPath);
        EditorApplication.update += Tick;
    }

    private static void StartCase(string path)
    {
        result = new SongAudioLoader.Result();
        routine = SongAudioLoader.Load(path, result, 10);
        deadline = EditorApplication.timeSinceStartup + 15;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Audio validation exceeded its deadline.");
            if (routine.MoveNext()) return;
            ((IDisposable)routine).Dispose();
            routine = null;
            if (phase == 0)
            {
                if (result.Error != null || result.Clip == null || result.Clip.loadState != AudioDataLoadState.Loaded
                    || result.Clip.samples <= 0 || result.Clip.channels <= 0)
                    throw new Exception("Valid Ogg failed: " + result.Error);
                float[] samples = new float[Math.Min(result.Clip.samples, result.Clip.frequency * 5) * result.Clip.channels];
                if (!result.Clip.GetData(samples, 0) || !Array.Exists(samples, value => Math.Abs(value) > .001f))
                    throw new Exception("Decoded audio contains no signal.");
                UnityEngine.Object.DestroyImmediate(result.Clip);
                result.Clip = null;
                phase++;
                StartCase(Path.Combine(directory, "missing.ogg"));
            }
            else
            {
                if (string.IsNullOrEmpty(result.Error) || result.Clip != null)
                    throw new Exception("Invalid audio control was accepted: " + phase);
                if (phase++ == 1) StartCase(Path.Combine(directory, "corrupt.ogg"));
                else Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void Finish(int exitCode)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose();
        if (result?.Clip != null) UnityEngine.Object.DestroyImmediate(result.Clip);
        Time.timeScale = previousTimeScale;
        foreach (string path in Directory.GetFiles(directory)) File.Delete(path);
        Directory.Delete(directory);
        if (exitCode == 0) Debug.Log("SONG AUDIO VALIDATION PASSED: escaped file URI, Ogg sample data, zero time scale, missing file, corrupt file.");
        EditorApplication.Exit(exitCode);
    }
}
