using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaGameplayPerformanceValidation
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] SongIds = { "bopeebo", "spookeez", "senpai", "2hot" };
    private static readonly string[] Difficulties = { "Hard", "Erect", "Hard", "Hard" };
    private static readonly List<double> frameTimes = new List<double>(20000);
    private static readonly JArray playSamples = new JArray();
    private static Song song;
    private static int phase;
    private static int songIndex;
    private static int lastFrame = -1;
    private static int meshesAtStart;
    private static int collectionsAtStart;
    private static int errors;
    private static double changed;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_GAMEPLAY_PERFORMANCE_PATH");

    static VanillaGameplayPerformanceValidation()
    {
        if (!SessionState.GetBool("VanillaGameplayPerformanceValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                if (!stack.Contains("UnityEditor.Search.SearchDatabase")) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run gameplay performance validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        VanillaCharacterValidation.CheckIdlePoseFrames();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaGameplayPerformanceValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    public static void RunAndBegin()
    {
        Run();
        UnityEngine.Profiling.Profiler.enabled = false;
        Begin();
    }

    private static int MeshCount()
    {
        int total = 0;
        foreach (var graphic in Object.FindObjectsByType<VanillaWeek2Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            total += ((Dictionary<int, Mesh>)typeof(VanillaWeek2Graphic).GetField("meshes", Private).GetValue(graphic)).Count;
        return total;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        double now = Time.realtimeSinceStartupAsDouble;
        if (changed == 0) changed = now;
        try
        {
            Require(errors == 0 && now - changed < 120, "Gameplay probe failed or timed out at phase " + phase);
            if (phase == 0)
            {
                if (now - changed < 4 || SceneManager.GetActiveScene().name != "Title" || Object.FindAnyObjectByType<MenuV2>() == null) return;
                if (songIndex == SongIds.Length)
                {
                    File.WriteAllText(Path.Combine(Output, "gameplay.json"), playSamples.ToString());
                    SessionState.SetBool("VanillaGameplayPerformanceValidation.Active", false);
                    EditorApplication.Exit(0);
                    return;
                }
                OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
                VanillaStoryCampaign.ReturnToMenu();
                VanillaFreeplay.ReturnToFreeplay = false;
                Pause.ResetSession();
                var selected = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles")).Single(entry =>
                {
                    var data = JObject.Parse(File.ReadAllText(Path.Combine(entry.meta.songPath, "Vanilla.json")));
                    return (string)data["song"] == SongIds[songIndex] && string.IsNullOrEmpty((string)data["variation"]);
                });
                Song.currentSongMeta = selected.meta;
                Song.difficulty = selected.Difficulty(Difficulties[songIndex]);
                Song.modeOfPlay = PlayModes.Autoplay;
                SceneManager.LoadScene("Game_Backup3");
                phase = 1;
                changed = now;
            }
            else if (phase == 1)
            {
                song = Song.instance;
                if (song == null || !song.songStarted) return;
                Require(song.musicSources[0].isPlaying, "Song audio did not start.");
                meshesAtStart = MeshCount();
                frameTimes.Clear();
                collectionsAtStart = GC.CollectionCount(0);
                changed = now;
                phase = 2;
            }
            else if (phase == 2)
            {
                frameTimes.Add(Time.unscaledDeltaTime * 1000);
                if (now - changed < 30) return;
                Require(Player.instance.Strumlines[0].HeadsHit > 0, "Autoplay control hit no notes.");
                Require(MeshCount() == meshesAtStart, "Gameplay generated animation meshes after loading.");
                frameTimes.Sort();
                var sample = new JObject { ["song"] = SongIds[songIndex], ["difficulty"] = Difficulties[songIndex],
                    ["frames"] = frameTimes.Count, ["medianMs"] = frameTimes[frameTimes.Count / 2],
                    ["p99Ms"] = frameTimes[(int)(frameTimes.Count * .99)], ["maxMs"] = frameTimes[frameTimes.Count - 1],
                    ["collections"] = GC.CollectionCount(0) - collectionsAtStart, ["meshes"] = meshesAtStart,
                    ["headsHit"] = Player.instance.Strumlines[0].HeadsHit };
                playSamples.Add(sample);
                UnityEngine.Debug.Log("GAMEPLAY PERFORMANCE PLAYED: " + sample.ToString(Newtonsoft.Json.Formatting.None));
                VanillaSongValidation.CaptureStage(song, Path.Combine(Output, SongIds[songIndex] + ".png"));
                Pause.instance.QuitSong();
                songIndex++;
                changed = now;
                phase = 0;
            }
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            SessionState.SetBool("VanillaGameplayPerformanceValidation.Active", false);
            EditorApplication.Exit(1);
        }
    }

    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("UNITY_PARTY_GAMEPLAY_PERFORMANCE_PATH");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set UNITY_PARTY_GAMEPLAY_PERFORMANCE_PATH.");
        Directory.CreateDirectory(output);
        UnityEngine.Profiling.Profiler.enabled = true;
        UnityEngine.Profiling.Profiler.enableAllocationCallstacks = false;
        using var allocations = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 100000, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        BeginAllocationSample(allocations);
        var control = new byte[4096];
        long controlAllocations = EndAllocationSample(allocations);
        GC.KeepAlive(control);
        Require(controlAllocations >= 1, "Allocation control failed: " + controlAllocations + " allocations, " + allocations.Count + " samples.");
        BeginAllocationSample(allocations);
        long empty = EndAllocationSample(allocations);
        Require(empty == 0, "Empty allocation control failed.");
        var samples = new JArray();
        foreach (string path in new[] { "Week1Assets/characters/bf", "Week1Assets/characters/dad", "Week2Assets/characters/spooky",
            "Week3Assets/characters/pico", "Week6Assets/characters/senpai", "Week8Assets/characters/pico-playable" })
        {
            string directory = Path.Combine(Application.streamingAssetsPath, "Bundles", path);
            var data = JObject.Parse(File.ReadAllText(Path.Combine(directory, "graphic.json")));
            string[] names = ((JObject)data["animations"]).Properties().Select(p => p.Name).Where(n => n == "idle" || n.StartsWith("sing")).ToArray();
            var obj = new GameObject("Performance " + path);
            try
            {
                var graphic = obj.AddComponent<VanillaWeek2Graphic>();
                graphic.Load(directory, 0);
                graphic.Advance(0, Vector3.zero, 0);
                var cold = Measure(graphic, names, data, 1, allocations);
                graphic.WarmFrames();
                var warm = Measure(graphic, names, data, 5, allocations);
                graphic.ColorAdjustment = new Vector4(-10, -20, -30, 5);
                graphic.Advance(0, Vector3.zero, 0);
                var composite = Measure(graphic, names, data, 5, allocations);
                samples.Add(new JObject { ["asset"] = path, ["cold"] = cold, ["warm"] = warm, ["composite"] = composite });
            }
            finally { Object.DestroyImmediate(obj); }
        }
        for (int i = 0; i < 4; i++) FunkinNoteSkin.Receptor(i, FunkinStrumline.Animation.Static, 0);
        BeginAllocationSample(allocations);
        for (int i = 0; i < 10000; i++) FunkinNoteSkin.Receptor(i % 4, FunkinStrumline.Animation.Static, 0);
        long noteAllocations = EndAllocationSample(allocations);
        var result = new JObject { ["graphics"] = samples, ["receptorAllocationsPerCall"] = noteAllocations / 10000.0, ["controlAllocations"] = controlAllocations };
        File.WriteAllText(Path.Combine(output, "rendering.json"), result.ToString());
        UnityEngine.Debug.Log("GAMEPLAY PERFORMANCE: " + result.ToString(Newtonsoft.Json.Formatting.None));
    }

    private static JObject Measure(VanillaWeek2Graphic graphic, string[] names, JObject data, int repeats, ProfilerRecorder allocations)
    {
        var meshes = (Dictionary<int, Mesh>)typeof(VanillaWeek2Graphic).GetField("meshes", Private).GetValue(graphic);
        int meshesBefore = meshes.Count;
        int calls = 0;
        long allocationCount = 0;
        long ticks = 0;
        long maxTicks = 0;
        int signature = 17;
        foreach (int repeat in Enumerable.Range(0, repeats))
        foreach (string name in names)
        {
            JToken clip = data["animations"][name];
            int[] frames = clip["frames"].Values<int>().ToArray();
            float fps = (float)clip["fps"];
            bool loop = (bool)clip["loop"];
            bool reverse = repeat % 2 == 1;
            graphic.Play(name, reverse);
            for (int index = 0; index < frames.Length + 2; index++)
            {
                BeginAllocationSample(allocations);
                long start = Stopwatch.GetTimestamp();
                graphic.Advance(index == 0 ? 0 : 1 / fps, new Vector3(6.4f, -3.6f, -10), index / fps);
                long elapsed = Stopwatch.GetTimestamp() - start;
                allocationCount += EndAllocationSample(allocations);
                ticks += elapsed;
                maxTicks = Math.Max(maxTicks, elapsed);
                calls++;
                int ageIndex = Mathf.FloorToInt((float)typeof(VanillaWeek2Graphic).GetField("age", Private).GetValue(graphic) * fps);
                int frame = loop ? ageIndex % frames.Length : Math.Min(ageIndex, frames.Length - 1);
                if (reverse) frame = frames.Length - 1 - frame;
                Require(graphic.Frame == frames[frame], "Animation frame changed: " + name);
                Require(graphic.Finished == (!loop && ageIndex >= frames.Length), "Animation completion changed: " + name);
                signature = unchecked(signature * 31 + graphic.Frame);
            }
            graphic.SetAnimationFrame(0);
            graphic.Advance(1, Vector3.zero, 0);
            Require(graphic.Frame == frames[0], "Frozen-frame control failed: " + name);
        }
        return new JObject { ["calls"] = calls, ["allocationsPerAdvance"] = allocationCount / (double)calls,
            ["meanMicroseconds"] = ticks * 1000000.0 / Stopwatch.Frequency / calls,
            ["maxMicroseconds"] = maxTicks * 1000000.0 / Stopwatch.Frequency,
            ["newMeshes"] = meshes.Count - meshesBefore, ["frameSignature"] = signature };
    }

    private static void BeginAllocationSample(ProfilerRecorder recorder)
    {
        recorder.Reset();
        recorder.Start();
    }

    private static long EndAllocationSample(ProfilerRecorder recorder)
    {
        recorder.Stop();
        Require(recorder.Valid && recorder.Count < recorder.Capacity, "Allocation recorder unavailable or full.");
        return recorder.Count;
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
