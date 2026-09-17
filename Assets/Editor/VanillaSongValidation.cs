using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FridayNightFunkin;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaSongValidation
{
    private static readonly string[] Ids = { "bopeebo", "fresh", "dadbattle", "bopeebo", "fresh", "dadbattle", "tutorial", "bopeebo", "fresh", "dadbattle" };
    private static readonly string[] Difficulties = { "Erect", "Erect", "Erect", "Nightmare", "Nightmare", "Nightmare", "Hard", "Hard", "Hard", "Hard" };
    private static double beginAt;
    private static double changedAt;
    private static double started;
    private static int phase;
    private static int songIndex;
    private static int errors;
    private static bool finishing;
    private static Song activeSong;
    private static int headsPlayer;
    private static int headsOpponent;
    private static bool checkedEnd;
    private static bool captured;
    private static JToken[] sourceEvents;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_TEST_PATH");

    static VanillaSongValidation()
    {
        if (SessionState.GetBool("VanillaSongValidation.Active", false))
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived += OnLog;
        }
    }

    public static void Begin()
    {
        Directory.CreateDirectory(Output);
        try
        {
            CheckCharts();
            CheckEasing();
            EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            beginAt = EditorApplication.timeSinceStartup + 5;
            EditorApplication.update += BeginWhenReady;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckCharts()
    {
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles");
        int chartCount = 0;
        bool control = false;
        bool speedControl = false;
        foreach (string sourcePath in Directory.GetFiles(root, "chart*.json", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == "Source"))
        {
            JObject source = JObject.Parse(File.ReadAllText(sourcePath));
            string directory = Directory.GetParent(Path.GetDirectoryName(sourcePath)).FullName;
            foreach (string difficulty in ((JObject)source["notes"]).Properties().Select(property => property.Name))
            {
                var chart = new FNFSong(Path.Combine(directory, "Chart-" + difficulty + ".json"));
                var expected = source["notes"][difficulty].Select(n =>
                    ((double)n["t"], (int)n["d"], (double?)n["l"] ?? 0)).OrderBy(n => n.Item1).ThenBy(n => n.Item2).ThenBy(n => n.Item3).ToArray();
                var actual = Notes(chart);
                Require(SameNotes(actual, expected), "Parsed chart changed note timing, side, direction, or hold: " + directory + "/" + difficulty);
                Require(Math.Abs(Song.ReadChartScrollSpeed(Path.Combine(directory, "Chart-" + difficulty + ".json"))
                    - (float)source["scrollSpeed"][difficulty]) < 0.0001, "Scroll speed changed: " + difficulty);
                speedControl |= Math.Abs(chart.Speed - (float)source["scrollSpeed"][difficulty]) > 0.01;
                if (!control)
                {
                    var wrong = expected.ToArray();
                    wrong[0] = (wrong[0].Item1, (wrong[0].Item2 + 4) % 8, wrong[0].Item3);
                    Require(!SameNotes(actual, wrong), "Swapped-side negative control did not fail.");
                    control = true;
                }
                chartCount++;
            }
        }
        Require(chartCount == 18 && control && speedControl, "Expected eighteen charts and rejected side and integer-speed controls.");
        Debug.Log("SONG CHARTS PASSED: all 18 charts preserve timing, note sides, directions, sustains, and speeds. Swapped-side control rejected.");
    }

    private static void CheckEasing()
    {
        Type type = typeof(VanillaSongPlayback).GetNestedType("Transition", BindingFlags.NonPublic);
        foreach (var sample in new[] { ("expoOut", 0.5f, 0.96875f), ("quadInOut", 0.25f, 0.125f), ("smoothStepInOut", 0.25f, 0.15625f) })
        {
            object transition = Activator.CreateInstance(type);
            type.GetField("duration").SetValue(transition, 1f);
            type.GetField("to").SetValue(transition, 1f);
            type.GetField("ease").SetValue(transition, sample.Item1);
            float actual = (float)type.GetMethod("Value").Invoke(transition, new object[] { sample.Item2 });
            Require(Math.Abs(actual - sample.Item3) < 0.00001f, "Camera easing changed: " + sample.Item1);
            Require(Math.Abs(actual - sample.Item2) > 0.01f, "Linear easing negative control passed.");
        }
        Debug.Log("SONG EASING PASSED: expoOut, quadInOut, smoothStepInOut. Linear controls rejected.");
    }

    private static (double, int, double)[] Notes(FNFSong chart)
    {
        var result = new List<(double, int, double)>();
        foreach (var section in chart.Sections)
        foreach (var entry in section.Notes)
        {
            var note = entry.ConvertToNote();
            int lane = (int)note[1];
            result.Add(((double)note[0], section.MustHitSection ? lane : (lane + 4) % 8, (double)note[2]));
        }
        return result.OrderBy(n => n.Item1).ThenBy(n => n.Item2).ThenBy(n => n.Item3).ToArray();
    }

    private static bool SameNotes((double, int, double)[] actual, (double, int, double)[] expected)
    {
        return actual.Length == expected.Length && actual.Zip(expected, (a, b) =>
            Math.Abs(a.Item1 - b.Item1) < 0.01 && a.Item2 == b.Item2 && Math.Abs(a.Item3 - b.Item3) < 0.01).All(equal => equal);
    }

    private static void BeginWhenReady()
    {
        if (EditorApplication.timeSinceStartup < beginAt) return;
        EditorApplication.update -= BeginWhenReady;
        SessionState.SetBool("VanillaSongValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors++;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Next(int next)
    {
        phase = next;
        changedAt = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (started == 0) started = changedAt = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - changedAt;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 1800, "Song validation timed out.");
            Require(errors == 0, "Runtime reported errors during song validation.");
            if (phase == 0)
            {
                if (elapsed < 7 || SceneManager.GetActiveScene().name != "Title") return;
                MenuV2 menu = Object.FindFirstObjectByType<MenuV2>();
                if (songIndex == Ids.Length)
                {
                    Next(5);
                    menu.QuitGame();
                    return;
                }
                OptionsV2.DesperateMode = false;
                OptionsV2.LiteMode = false;
                string empty = Path.Combine(Output, "EmptyCustomBundles");
                Directory.CreateDirectory(empty);
                typeof(MenuV2).GetField("_songsFolder", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, empty);
                menu.ReloadSongList();
                menu.OpenPlayScreenFromMenu();
                Next(1);
            }
            else if (phase == 1)
            {
                if (elapsed < 1) return;
                MenuV2 menu = Object.FindFirstObjectByType<MenuV2>();
                var bundles = menu.songListRect.GetComponentsInChildren<BundleButtonV2>(true);
                Require(bundles.Length == 2 && bundles.Sum(b => b.SongButtons.Count) == 4, "Built-in Tutorial and Week 1 did not appear in the song picker.");
                SongButtonV2 button = bundles.SelectMany(b => b.SongButtons).Single(b =>
                    (string)JObject.Parse(File.ReadAllText(Path.Combine(b.Meta.songPath, "Vanilla.json")))["song"] == Ids[songIndex]);
                button.GetComponent<Button>().onClick.Invoke();
                Next(2);
            }
            else if (phase == 2)
            {
                if (elapsed < 3) return;
                MenuV2 menu = Object.FindFirstObjectByType<MenuV2>();
                Require(menu.songInfoScreen.activeInHierarchy && menu.canChangeSongs && menu.musicSource.isPlaying, "Bundled song preview did not load.");
                string[] options = Ids[songIndex] == "tutorial" ? new[] { "Easy", "Normal", "Hard" }
                    : new[] { "Easy", "Normal", "Hard", "Erect", "Nightmare" };
                Require(menu.songDifficultiesDropdown.options.Select(o => o.text).SequenceEqual(options), "Difficulty order is incorrect.");
                menu.songDifficultiesDropdown.value = Array.IndexOf(options, Difficulties[songIndex]);
                Next(6);
            }
            else if (phase == 6)
            {
                if (elapsed < 3) return;
                MenuV2 menu = Object.FindFirstObjectByType<MenuV2>();
                Require(menu.canChangeSongs && menu.musicSource.isPlaying, "Variation preview did not finish loading.");
                bool erect = Difficulties[songIndex] != "Hard";
                Require(menu.songNameText.text.EndsWith(" Erect") == erect, "Variation title did not update.");
                menu.songModeDropdown.value = 3;
                menu.PlaySong();
                checkedEnd = false;
                captured = false;
                Next(3);
            }
            else if (phase == 3)
            {
                Require(elapsed < 30, "Gameplay did not start.");
                if (SceneManager.GetActiveScene().name != "Game_Backup3") return;
                activeSong = Object.FindFirstObjectByType<Song>();
                if (activeSong == null || !activeSong.songStarted) return;
                Require(activeSong.musicSources[0].isPlaying && activeSong.hasVoiceLoaded && activeSong.vocalSource.isPlaying, "Gameplay audio did not start.");
                Require(Math.Abs(activeSong.musicClip.length - activeSong.vocalClip.length) < 0.05, "Instrumental and mixed vocals have different durations.");
                Require(activeSong.vanillaPlayback != null && activeSong.vanillaPlayback.SongId == Ids[songIndex], "Vanilla chart events did not attach.");
                bool erect = Difficulties[songIndex] != "Hard";
                Require(activeSong.selectedInstrumentalPath.EndsWith("Inst-erect.ogg") == erect, "Wrong instrumental variation loaded.");
                Require(activeSong.selectedVocalsPath.EndsWith("Voices-erect.ogg") == erect, "Wrong vocal variation loaded.");
                Require(activeSong.vanillaPlayback.IsErect == erect, "Wrong chart events loaded.");
                if (erect)
                {
                    Require(activeSong.vanillaPlayback.Stage != null && activeSong.vanillaPlayback.Stage.PropCount == 10, "Erect stage did not load.");
                    Require(activeSong.defaultSceneObjects.All(item => !item.activeSelf), "Original stage overlaps Erect stage.");
                    Vector3 position = activeSong.mainCamera.transform.position;
                    OptionsV2.Middlescroll = true;
                    activeSong.vanillaPlayback.MoveCamera(activeSong.mainCamera);
                    Vector3 target = activeSong.vanillaPlayback.Stage.CameraTargets[2];
                    Require(Vector3.Distance(activeSong.mainCamera.transform.position, target) < 0.001f, "Middlescroll camera left the Erect stage.");
                    Require(Vector3.Distance(target, activeSong.vanillaPlayback.Stage.CameraTargets[1]) > 1, "Girlfriend focus incorrectly uses Dad.");
                    OptionsV2.Middlescroll = false;
                    activeSong.mainCamera.transform.position = position;
                }
                string sourceName = erect ? "chart-erect.json" : "chart.json";
                sourceEvents = JObject.Parse(File.ReadAllText(Path.Combine(activeSong.selectedSongDir, "Source", sourceName)))["events"]
                    .OrderBy(entry => (double)entry["t"]).ToArray();
                Require(activeSong.enemy.characterName == (Ids[songIndex] == "tutorial" ? "Girlfriend" : "Dad"), "Wrong opponent loaded.");
                if (Ids[songIndex] == "tutorial")
                    Require(!activeSong.girlfriendObject.activeSelf, "Tutorial displays a duplicate Girlfriend.");
                var chart = (FNFSong)typeof(Song).GetField("_song", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(activeSong);
                var notes = Notes(chart);
                headsPlayer = notes.Count(n => n.Item2 < 4);
                headsOpponent = notes.Length - headsPlayer;
                var behaviors = (List<NoteBehaviour>)typeof(Song).GetField("_noteBehaviours", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(activeSong);
                Require(behaviors.Count == notes.Length, "Note scheduler contains missing or duplicate heads.");
                Debug.Log("SONG RUNNING: " + Ids[songIndex] + " " + Difficulties[songIndex] + ", " + notes.Length + " heads, opponent=" + activeSong.enemy.characterName + ", duration=" + activeSong.musicClip.length);
                Next(4);
            }
            else if (phase == 4)
            {
                if (activeSong != null && activeSong.vanillaPlayback.IsErect)
                {
                    var applied = sourceEvents.Take(activeSong.vanillaPlayback.EventsApplied).ToArray();
                    JToken focus = applied.LastOrDefault(entry => (string)entry["e"] == "FocusCamera")?["v"];
                    if (focus != null)
                    {
                        Require(activeSong.vanillaPlayback.FocusCharacter == ((int?)focus["char"] ?? 0), "Camera focus differs from source event.");
                        Vector2 expected = new Vector2((float?)focus["x"] ?? 0, (float?)focus["y"] ?? 0);
                        Require(Vector2.Distance(activeSong.vanillaPlayback.FocusOffset, expected) < 0.001f, "Camera offset differs from source event.");
                    }
                    JToken bop = applied.LastOrDefault(entry => (string)entry["e"] == "SetCameraBop")?["v"];
                    if (bop != null)
                        Require(Math.Abs(activeSong.vanillaPlayback.BopRate - ((float?)bop["rate"] ?? 4)) < 0.001f
                            && Math.Abs(activeSong.vanillaPlayback.BopIntensity - ((float?)bop["intensity"] ?? 1)) < 0.001f,
                            "Camera bop differs from source event.");
                }
                if (!captured && elapsed > 8)
                {
                    CaptureStage(Ids[songIndex] + "-" + Difficulties[songIndex].ToLowerInvariant() + ".png");
                    captured = true;
                }
                if (!checkedEnd && activeSong != null && activeSong.stopwatch.ElapsedMilliseconds >= (activeSong.musicClip.length - 0.2f) * 1000)
                {
                    Require(Player.instance.Strumlines[0].HeadsHit == headsPlayer && Player.instance.Strumlines[1].HeadsHit == headsOpponent,
                        "Autoplay did not consume all chart heads: " + Player.instance.Strumlines[0].HeadsHit + "/" + headsPlayer + ", " + Player.instance.Strumlines[1].HeadsHit + "/" + headsOpponent);
                    Require(activeSong.playerOneStats.currentScore == 0 && activeSong.playerTwoStats.currentScore == 0,
                        "Autoplay must not grant player score.");
                    Require(activeSong.playerOneStats.missedHits == 0 && activeSong.playerTwoStats.missedHits == 0, "Autoplay missed notes.");
                    int events = JObject.Parse(File.ReadAllText(activeSong.selectedVanillaPath))["events"].Count();
                    Require(activeSong.vanillaPlayback.EventsApplied == events, "Song ended before all chart events were consumed.");
                    checkedEnd = true;
                    Debug.Log("SONG COMPLETED: " + Ids[songIndex] + " " + Difficulties[songIndex] + ", all heads hit, all events consumed, zero misses.");
                }
                if (SceneManager.GetActiveScene().name != "Title") return;
                Require(checkedEnd, "Song returned to menu before its completion checks.");
                songIndex++;
                Next(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(false);
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += () => Finish(phase == 5 && songIndex == Ids.Length && errors == 0);
    }

    private static void CaptureStage(string name)
    {
        var cameraObject = new GameObject("Stage validation camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(activeSong.mainCamera);
        camera.transform.SetPositionAndRotation(activeSong.mainCamera.transform.position, activeSong.mainCamera.transform.rotation);
        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var target = new RenderTexture(1280, 720, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        foreach (var feature in features) feature.SetActive(false);
        try
        {
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) camera.cullingMask = 0;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                int visible = image.GetPixels32().Count(pixel => pixel.r > 40 || pixel.g > 40 || pixel.b > 40);
                Require(pass == 0 ? visible > 10000 : visible == 0, "Stage render or blank control failed: " + name);
                if (pass == 0) File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = previous;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool success)
    {
        if (finishing) return;
        finishing = true;
        SessionState.SetBool("VanillaSongValidation.Active", false);
        Debug.Log("SONG VALIDATION FINISHED: passed=" + success + ", errors=" + errors + ", completed=" + songIndex);
        File.WriteAllText(Path.Combine(Output, "result.json"), new JObject { ["passed"] = success, ["errors"] = errors, ["completed"] = songIndex }.ToString());
        if (success)
        {
            try { BuildAutomation.BuildWindows(); }
            catch (Exception exception) { Debug.LogException(exception); success = false; }
        }
        EditorApplication.Exit(success ? 0 : 1);
    }
}
