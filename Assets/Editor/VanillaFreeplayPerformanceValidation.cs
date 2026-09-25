using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaFreeplayPerformanceValidation
{
    private static readonly int[] Selections = { 2, 10, 13, 16, 19, 2, 10, 13, 16, 19, 1 };
    private static readonly JArray samples = new JArray();
    private static VanillaFreeplay freeplay;
    private static double changed;
    private static double lastFrame;
    private static double previewReady;
    private static double maximumGap;
    private static int frame = -1;
    private static int phase;
    private static int sample;
    private static int rapidMoves;
    private static bool finishing;
    private static JObject current;
    private static string expectedPath;
    private static float playbackTime;
    private static float loopStart;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_FREEPLAY_PERFORMANCE_PATH");
    private static bool DifficultyChanges => Environment.GetEnvironmentVariable("UNITY_PARTY_FREEPLAY_DIFFICULTY_TEST") == "1";

    static VanillaFreeplayPerformanceValidation()
    {
        if (!SessionState.GetBool("VanillaFreeplayPerformanceValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message);
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run performance validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "FreeplayPerformanceValidation";
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaFreeplayPerformanceValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        double now = Time.realtimeSinceStartupAsDouble;
        if (changed == 0) changed = now;
        if (lastFrame > 0 && phase == 2) maximumGap = Math.Max(maximumGap, (now - lastFrame) * 1000);
        lastFrame = now;
        try
        {
            if (now - changed > 30) throw new InvalidOperationException("Performance probe timed out at phase " + phase);
            if (phase == 0)
            {
                var menu = Object.FindAnyObjectByType<MenuV2>();
                if (menu?.vanillaMenu == null || now - changed < 3) return;
                PlayerPrefs.SetString("Freeplay.Character", "bf");
                VanillaFreeplay.RememberDifficulty("Normal");
                freeplay = VanillaFreeplay.Open(menu, true, Path.Combine(Output, "EmptyBundles"));
                if (DifficultyChanges) freeplay.MoveSelection(2 - freeplay.SelectedIndex);
                if (DifficultyChanges) VanillaFreeplayDifficultyValidation.Capture(freeplay);
                phase = 1;
                changed = now;
            }
            else if (phase == 1)
            {
                if (now - changed < 2) return;
                Select();
            }
            else if (phase == 2)
            {
                if (freeplay.PreviewPath != expectedPath || !freeplay.PreviewSource.isPlaying) return;
                if (previewReady == 0)
                {
                    previewReady = now;
                    current["preview_ms"] = (now - changed) * 1000;
                    current["clip_bytes"] = Profiler.GetRuntimeMemorySizeLong(freeplay.PreviewSource.clip);
                    current["pcm_bytes"] = (long)freeplay.PreviewSource.clip.samples * freeplay.PreviewSource.clip.channels * 2;
                    current["load_type"] = freeplay.PreviewSource.clip.loadType.ToString();
                }
                if (now - previewReady < .5) return;
                current["frame_gap_max_ms"] = maximumGap;
                samples.Add(current);
                Debug.Log("FREEPLAY PERFORMANCE SAMPLE: " + current.ToString(Newtonsoft.Json.Formatting.None));
                if (++sample < Selections.Length) Select();
                else { phase = 3; changed = now; }
            }
            else if (phase == 3)
            {
                if (now - changed < .07) return;
                if (DifficultyChanges) freeplay.ChangeDifficulty(1);
                else freeplay.MoveSelection(1);
                changed = now;
                if (++rapidMoves < 30) return;
                expectedPath = freeplay.SelectedSong?.meta.AssetPath("Inst.ogg", freeplay.Difficulty) ?? "freeplayRandom";
                phase = 4;
            }
            else if (phase == 4)
            {
                if (now - changed < 2) return;
                if (freeplay.PreviewPath != expectedPath || !freeplay.PreviewSource.isPlaying)
                    throw new InvalidOperationException("Rapid selection played a stale preview.");
                playbackTime = freeplay.PreviewSource.time;
                changed = now;
                phase = 5;
            }
            else if (phase == 5)
            {
                if (now - changed < .2) return;
                if (freeplay.PreviewSource.time < playbackTime + .05f)
                    throw new InvalidOperationException("Streamed preview did not advance after seeking.");
                var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                loopStart = (float)typeof(VanillaFreeplay).GetField("previewStart", fields).GetValue(freeplay);
                float loopEnd = (float)typeof(VanillaFreeplay).GetField("previewEnd", fields).GetValue(freeplay);
                freeplay.PreviewSource.time = loopEnd;
                changed = now;
                phase = 6;
            }
            else if (phase == 6)
            {
                if (now - changed < .2) return;
                if (!freeplay.PreviewSource.isPlaying || freeplay.PreviewSource.time < loopStart || freeplay.PreviewSource.time > loopStart + 1)
                    throw new InvalidOperationException("Streamed preview failed to loop to its source start.");
                foreach (JObject result in samples)
                    if ((long)result["clip_bytes"] >= (long)result["pcm_bytes"])
                        throw new InvalidOperationException("Preview retained a complete decoded instrumental: " + result["song"]);
                if (DifficultyChanges) VanillaFreeplayDifficultyValidation.Check(freeplay);
                Finish(true, "Selection, streamed preview playback, seeking, looping, and rapid cancellation passed.");
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Select()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (DifficultyChanges) freeplay.ChangeDifficulty(1);
        else freeplay.MoveSelection(Selections[sample] - freeplay.SelectedIndex);
        double selection = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        Canvas.ForceUpdateCanvases();
        current = new JObject
        {
            ["song"] = freeplay.SelectedSong.meta.songName,
            ["difficulty"] = freeplay.Difficulty,
            ["selection_ms"] = selection,
            ["canvas_ms"] = watch.Elapsed.TotalMilliseconds
        };
        expectedPath = freeplay.SelectedSong.meta.AssetPath("Inst.ogg", freeplay.Difficulty);
        changed = Time.realtimeSinceStartupAsDouble;
        previewReady = maximumGap = 0;
        phase = 2;
    }

    private static void Finish(bool passed, string message)
    {
        if (finishing) return;
        finishing = true;
        SessionState.SetBool("VanillaFreeplayPerformanceValidation.Active", false);
        File.WriteAllText(Path.Combine(Output, "result.json"), new JObject
        {
            ["passed"] = passed, ["message"] = message, ["samples"] = samples
        }.ToString());
        Debug.Log("FREEPLAY PERFORMANCE: passed=" + passed + ", " + message);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
