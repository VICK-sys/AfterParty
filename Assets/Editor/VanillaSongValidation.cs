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
    private static readonly string[] MixIds = (Environment.GetEnvironmentVariable("UNITY_PARTY_MIX_IDS") ?? "bopeebo,fresh,dadbattle,spookeez,south,pico,philly-nice,blammed,cocoa,eggnog,senpai,roses,ugh,guns,stress,darnell,lit-up").Split(',');
    private static bool SpaghettiOnly => Environment.GetEnvironmentVariable("UNITY_PARTY_SPAGHETTI_TEST") == "1";
    private static bool MixOnly => Environment.GetEnvironmentVariable("UNITY_PARTY_MIX_TEST") == "1";
    private static bool WeekendOnly => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEKEND_TEST") == "1";
    private static bool Week7Only => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEK7_TEST") == "1";
    private static bool Week2Only => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEK2_TEST") == "1";
    private static bool Week3Only => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEK3_TEST") == "1";
    private static bool Weeks456Only => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEKS456_TEST") == "1";
    private static bool StageOnly => Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_STAGE_ONLY") == "1";
    private static int RunLimit => int.TryParse(Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_TEST_LIMIT"), out int limit) && limit > 0 ? limit : int.MaxValue;
    private static int RunStart => int.TryParse(Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_TEST_START"), out int start) ? start : 0;
    private static readonly string[] Ids = (SpaghettiOnly ? new[] { "spaghetti", "spaghetti", "spaghetti" } : MixOnly ? MixIds
        : WeekendOnly ? new[] { "darnell", "lit-up", "2hot", "blazin", "darnell", "darnell" }
        : Week7Only ? new[] { "ugh", "guns", "stress", "ugh", "ugh" }
        : Weeks456Only ? new[] { "satin-panties", "high", "milf", "cocoa", "eggnog", "winter-horrorland", "senpai", "roses", "thorns", "satin-panties", "high", "cocoa", "eggnog", "senpai", "roses", "thorns", "satin-panties", "high", "cocoa", "eggnog", "senpai", "roses", "thorns" }
        : Week3Only ? new[] { "pico", "philly-nice", "blammed", "pico", "philly-nice", "blammed", "pico", "philly-nice", "blammed" }
        : Week2Only ? new[] { "spookeez", "south", "monster", "spookeez", "south", "spookeez", "south" }
        : new[] { "bopeebo", "fresh", "dadbattle", "bopeebo", "fresh", "dadbattle", "tutorial", "bopeebo", "fresh", "dadbattle" }).Skip(RunStart).Take(RunLimit).ToArray();
    private static readonly string[] Difficulties = (SpaghettiOnly ? new[] { "Hard", "Normal", "Easy" } : MixOnly ? Enumerable.Repeat("Hard", MixIds.Length).ToArray()
        : WeekendOnly ? new[] { "Hard", "Hard", "Hard", "Hard", "Erect", "Nightmare" }
        : Week7Only ? new[] { "Hard", "Hard", "Hard", "Erect", "Nightmare" }
        : Weeks456Only ? Enumerable.Repeat("Hard", 9).Concat(Enumerable.Repeat("Erect", 7)).Concat(Enumerable.Repeat("Nightmare", 7)).ToArray()
        : Week3Only ? new[] { "Erect", "Erect", "Erect", "Hard", "Hard", "Hard", "Nightmare", "Nightmare", "Nightmare" }
        : Week2Only ? new[] { "Erect", "Erect", "Hard", "Hard", "Hard", "Nightmare", "Nightmare" }
        : new[] { "Erect", "Erect", "Erect", "Nightmare", "Nightmare", "Nightmare", "Hard", "Hard", "Hard", "Hard" }).Skip(RunStart).Take(RunLimit).ToArray();
    private static double beginAt;
    private static double changedAt;
    private static double started;
    private static int phase;
    private static int songIndex;
    private static int errors;
    private static int editorWarnings;
    private static bool finishing;
    private static Song activeSong;
    private static int headsPlayer;
    private static int headsOpponent;
    private static bool checkedEnd;
    private static bool captured;
    private static Song countdownSong;
    private static double countdownPosition;
    private static double countdownRealtime;
    private static bool countdownObserved;

    private static void CheckCountdownClock()
    {
        if (Environment.GetEnvironmentVariable("UNITY_PARTY_COUNTDOWN_TEST") != "1") return;
        Song song = Song.instance;
        if (song == null) return;
        double realtime = Time.realtimeSinceStartupAsDouble * 1000;
        double position = song.SongPosition;
        if (song != countdownSong)
        {
            countdownSong = song;
            countdownObserved = false;
        }
        if (countdownObserved && song.songStarted)
        {
            double jump = position - countdownPosition - (realtime - countdownRealtime);
            Debug.Log("COUNTDOWN CLOCK TRANSITION: jump=" + jump + "ms, previous=" + countdownPosition + ", current=" + position);
            Require(Math.Abs(jump) < 35, "Countdown clock jumped when the music started: " + jump + "ms.");
            countdownObserved = false;
        }
        if (song.IsCountingDown)
        {
            countdownObserved = true;
            countdownPosition = position;
            countdownRealtime = realtime;
        }
    }
    private static int weekendCombatBeforeProbe;
    private static JToken[] sourceEvents;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_TEST_PATH");

    static VanillaSongValidation()
    {
        if (SessionState.GetBool("VanillaSongValidation.Pending", false))
        {
            beginAt = EditorApplication.timeSinceStartup + 5;
            EditorApplication.update += BeginWhenReady;
        }
        if (SessionState.GetBool("VanillaSongValidation.Active", false))
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived += OnLog;
        }
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run song validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        PlayerPrefs.SetInt("Funkin.Options.DiscordRPC", 0);
        PlayerPrefs.SetInt("Funkin.Options.AutoPause", 0);
        PlayerPrefs.Save();
        Directory.CreateDirectory(Output);
        try
        {
            CheckCharts();
            CheckEasing();
            if (SpaghettiOnly && StageOnly) SpaghettiValidation.CheckRegression();
            if (MixOnly) VanillaMixValidation.CheckAssets();
            if (WeekendOnly) VanillaWeekend1Validation.CheckAssets();
            if (Week7Only) VanillaWeek7Validation.CheckAssets();
            if (Week3Only) VanillaWeek3Validation.CheckAssets();
            if (Weeks456Only) VanillaWeeks456Validation.CheckAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            SessionState.SetBool("VanillaSongValidation.Pending", true);
            beginAt = EditorApplication.timeSinceStartup + 5;
            EditorApplication.update += BeginWhenReady;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void CheckCharts()
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
            foreach (string difficulty in JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourcePath), Path.GetFileName(sourcePath).Replace("chart", "metadata"))))["playData"]["difficulties"].Values<string>())
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
        Require(chartCount == 166 && control && speedControl, "Expected 166 charts and rejected side and integer-speed controls.");
        Debug.Log("SONG CHARTS PASSED: all 166 charts preserve timing, note sides, directions, sustains, and speeds. Swapped-side control rejected.");
    }

    private static void CheckEasing()
    {
        Type type = typeof(VanillaSongPlayback).GetNestedType("Transition", BindingFlags.NonPublic);
        foreach (var sample in new[] { ("expoOut", 0.5f, 0.96875f), ("quadInOut", 0.25f, 0.125f), ("smoothStepInOut", 0.25f, 0.15625f),
            ("quartOut", 0.5f, 0.9375f), ("sineInOut", 0.25f, 0.1464466f), ("quadOut", .5f, .75f), ("sineOut", .5f, .70710678f) })
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
        if (EditorApplication.timeSinceStartup < beginAt || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= BeginWhenReady;
        SessionState.SetBool("VanillaSongValidation.Pending", false);
        SessionState.SetBool("VanillaSongValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal)
            && stack.Contains("UnityEditor.Search.SearchDatabase") && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
        {
            editorWarnings++;
            return;
        }
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
            Require(EditorApplication.timeSinceStartup - started < Math.Max(1800, Ids.Length * 210), "Song validation timed out.");
            CheckCountdownClock();
            if (VanillaInitialCameraValidation.Tick()) return;
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
                Require(bundles.Length == 10 && bundles.Sum(b => b.SongButtons.Count) == 44, "Built-in songs and mixes did not appear in the song picker.");
                SongButtonV2 button = bundles.SelectMany(b => b.SongButtons).Single(b =>
                    (string)JObject.Parse(File.ReadAllText(Path.Combine(b.Meta.songPath, "Vanilla.json")))["song"] == Ids[songIndex]
                    && (string)JObject.Parse(File.ReadAllText(Path.Combine(b.Meta.songPath, "Vanilla.json")))["variation"] == (MixOnly ? Ids[songIndex] == "darnell" || Ids[songIndex] == "lit-up" ? "bf" : "pico" : ""));
                button.GetComponent<Button>().onClick.Invoke();
                Next(2);
            }
            else if (phase == 2)
            {
                if (elapsed < 3) return;
                MenuV2 menu = Object.FindFirstObjectByType<MenuV2>();
                Require(menu.songInfoScreen.activeInHierarchy && menu.canChangeSongs && menu.musicSource.isPlaying, "Bundled song preview did not load.");
                string[] options = MixOnly || new[] { "tutorial", "monster", "milf", "winter-horrorland", "guns", "stress", "lit-up", "2hot", "blazin", "spaghetti" }.Contains(Ids[songIndex]) ? new[] { "Easy", "Normal", "Hard" }
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
                bool erect = Difficulties[songIndex] == "Erect" || Difficulties[songIndex] == "Nightmare";
                Require(menu.songNameText.text.EndsWith(" Erect") == erect, "Variation title did not update.");
                Require(menu.songModeDropdown.options.Select(option => option.text).SequenceEqual(new[] { "as Protagonist", "as Opponent", "AutoPlay" }),
                    "Bundle picker must offer exactly the three single-player modes.");
                menu.songModeDropdown.value = 2;
                menu.PlaySong();
                if (MixOnly)
                {
                    typeof(Pause).GetField("sessionSong", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Song.currentSongMeta.songPath);
                    Pause.PlayedCampaignIntro = true;
                }
                Require(Song.modeOfPlay == PlayModes.Autoplay, "Bundle picker launched the wrong mode.");
                checkedEnd = false;
                captured = false;
                Next(3);
            }
            else if (phase == 3)
            {
                Require(elapsed < (SpaghettiOnly ? 70 : 30), "Gameplay did not start.");
                if (SceneManager.GetActiveScene().name != "Game_Backup3") return;
                activeSong = Object.FindFirstObjectByType<Song>();
                if (SpaghettiOnly && activeSong != null) SpaghettiValidation.ObserveIntro(activeSong);
                if (activeSong == null || !activeSong.songStarted) return;
                Require(activeSong.musicSources[0].isPlaying && (Ids[songIndex] == "blazin" ? !activeSong.hasVoiceLoaded : activeSong.hasVoiceLoaded && activeSong.vocalSource.isPlaying), "Gameplay audio did not start.");
                if (!MixOnly && !SpaghettiOnly && Ids[songIndex] != "blazin") Require(Math.Abs(activeSong.musicClip.length - activeSong.vocalClip.length) < 0.05, "Instrumental and mixed vocals have different durations.");
                Require(activeSong.vanillaPlayback != null && activeSong.vanillaPlayback.SongId == Ids[songIndex], "Vanilla chart events did not attach.");
                bool erect = Difficulties[songIndex] == "Erect" || Difficulties[songIndex] == "Nightmare";
                Require(activeSong.selectedInstrumentalPath.EndsWith("Inst-erect.ogg") == erect, "Wrong instrumental variation loaded.");
                Require(activeSong.selectedVocalsPath.EndsWith("Voices-erect.ogg") == erect, "Wrong vocal variation loaded.");
                Require(activeSong.vanillaPlayback.IsErect == erect, "Wrong chart events loaded.");
                if (erect && !Week2Only && !Week3Only && !Weeks456Only && !Week7Only && !WeekendOnly)
                {
                    Require(activeSong.vanillaPlayback.CampaignStage != null && activeSong.vanillaPlayback.CampaignStage.PropCount == 9, "Erect stage did not load.");
                    Require(activeSong.defaultSceneObjects.All(item => !item.activeSelf), "Original stage overlaps Erect stage.");
                    Vector3 position = activeSong.mainCamera.transform.position;
                    OptionsV2.Middlescroll = true;
                    activeSong.vanillaPlayback.MoveCamera(activeSong.mainCamera);
                    Vector3 target = activeSong.vanillaPlayback.CampaignStage.CameraTargets[2];
                    Require(Vector3.Distance(activeSong.mainCamera.transform.position, target) < 0.001f, "Middlescroll camera left the Erect stage.");
                    Require(Vector3.Distance(target, activeSong.vanillaPlayback.CampaignStage.CameraTargets[1]) > 1, "Girlfriend focus incorrectly uses Dad.");
                    OptionsV2.Middlescroll = false;
                    activeSong.mainCamera.transform.position = position;
                }
                string sourceName = erect ? "chart-erect.json" : "chart.json";
                sourceEvents = JObject.Parse(File.ReadAllText(Path.Combine(activeSong.selectedSongDir, "Source", sourceName)))["events"]
                    .OrderBy(entry => (double)entry["t"]).ToArray();
                Require(MixOnly || SpaghettiOnly || activeSong.enemy.characterName == (Weeks456Only || Week7Only || WeekendOnly ? activeSong.vanillaPlayback.OpponentId : Week3Only ? "Pico" : Week2Only ? Ids[songIndex] == "monster" ? "Monster" : "Spooky Kids"
                    : Ids[songIndex] == "tutorial" ? "Girlfriend" : "Dad"), "Wrong opponent loaded.");
                if (WeekendOnly)
                {
                    weekendCombatBeforeProbe = activeSong.vanillaPlayback.CampaignStage.CombatNotes;
                    VanillaWeekend1Validation.CheckStage(activeSong);
                }
                if (SpaghettiOnly) SpaghettiValidation.CheckStage(activeSong);
                if (Week7Only) VanillaWeek7Validation.CheckStage(activeSong);
                if (Week2Only) VanillaWeek2Validation.CheckStage(activeSong);
                if (Week3Only) VanillaWeek3Validation.CheckStage(activeSong);
                if (Weeks456Only) VanillaWeeks456Validation.CheckStage(activeSong);
                if (MixOnly) VanillaMixValidation.CheckStage(activeSong);
                else CheckGirlfriendReactions(activeSong);
                VanillaPhillyBackgroundValidation.CheckStage(activeSong);
                if (Ids[songIndex] == "tutorial")
                    Require(!activeSong.girlfriendObject.activeSelf, "Tutorial displays a duplicate Girlfriend.");
                var chart = (FNFSong)typeof(Song).GetField("_song", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(activeSong);
                var notes = Notes(chart);
                headsPlayer = notes.Count(n => n.Item2 < 4) - activeSong.vanillaPlayback.UnscoredNotes(0);
                headsOpponent = notes.Count(n => n.Item2 >= 4) - activeSong.vanillaPlayback.UnscoredNotes(1);
                var behaviors = (List<NoteBehaviour>)typeof(Song).GetField("_noteBehaviours", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(activeSong);
                Require(behaviors.Count == notes.Length, "Note scheduler contains missing or duplicate heads.");
                Debug.Log("SONG RUNNING: " + Ids[songIndex] + " " + Difficulties[songIndex] + ", " + notes.Length + " heads, opponent=" + activeSong.enemy.characterName + ", duration=" + activeSong.musicClip.length);
                if (StageOnly)
                {
                    CaptureStage(Ids[songIndex] + "-" + Difficulties[songIndex].ToLowerInvariant() + ".png");
                    if (MixOnly) VanillaMixValidation.CheckRuntimeEvents(activeSong);
                    Debug.Log("SONG STAGE COMPLETED: " + Ids[songIndex] + " " + Difficulties[songIndex]);
                    Pause.instance.QuitSong();
                    Next(7);
                }
                else Next(4);
            }
            else if (phase == 4)
            {
                if (SpaghettiOnly && activeSong != null) SpaghettiValidation.Observe(activeSong);
                if (activeSong != null && (activeSong.vanillaPlayback.IsErect || MixOnly || SpaghettiOnly))
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
                if (!checkedEnd && activeSong != null && activeSong.stopwatch.ElapsedMilliseconds >= (SpaghettiOnly ? 168000 : (activeSong.musicClip.length - 0.2f) * 1000))
                {
                    Require(Player.instance.Strumlines[0].HeadsHit == headsPlayer && Player.instance.Strumlines[1].HeadsHit == headsOpponent,
                        "Autoplay did not consume all chart heads: " + Player.instance.Strumlines[0].HeadsHit + "/" + headsPlayer + ", " + Player.instance.Strumlines[1].HeadsHit + "/" + headsOpponent
                        + ", missed=" + string.Join(",", Player.instance.Strumlines.SelectMany(line => line.Notes).Where(note => note.Missed).Select(note => note.Time)));
                    Require(activeSong.playerOneStats.currentScore == 0 && activeSong.playerTwoStats.currentScore == 0,
                        "Autoplay must not grant player score.");
                    Require(activeSong.playerOneStats.missedHits == 0 && activeSong.playerTwoStats.missedHits == 0, "Autoplay missed notes.");
                    int events = JObject.Parse(File.ReadAllText(activeSong.selectedVanillaPath))["events"].Count();
                    Require(activeSong.vanillaPlayback.EventsApplied == events, "Song ended before all chart events were consumed.");
                    if (WeekendOnly)
                    {
                        var stage = activeSong.vanillaPlayback.CampaignStage;
                        var kinds = JObject.Parse(File.ReadAllText(activeSong.selectedVanillaPath))["noteKinds"][Difficulties[songIndex].ToLowerInvariant()];
                        Require(stage.CansShot == kinds.Count(n => (string)n["k"] == "weekend-1-firegun") && stage.CansMissed == 0, "Can notes were skipped or duplicated.");
                        if (Ids[songIndex] == "blazin") Require(stage.CombatNotes + weekendCombatBeforeProbe == kinds.Count(),
                            "Combat notes were skipped or duplicated: " + stage.CombatNotes + " after probe + " + weekendCombatBeforeProbe + " before probe, expected " + kinds.Count());
                    }
                    if (Week7Only)
                    {
                        var stage = activeSong.vanillaPlayback.CampaignStage;
                        int expectedSpecial = JObject.Parse(File.ReadAllText(activeSong.selectedVanillaPath))["noteKinds"][Difficulties[songIndex].ToLowerInvariant()].Count();
                        Require(stage.SpecialNoteHits == expectedSpecial, "Week 7 special notes were skipped or duplicated.");
                        if (Ids[songIndex] == "stress") Require(stage.SpeakerShots == 546, "Stress did not consume all speaker cues.");
                    }
                    if (MixOnly && Ids[songIndex] == "stress") Require(activeSong.vanillaPlayback.CampaignStage.SpeakerShots == 584, "Stress Pico did not consume all Otis cues.");
                    if (Week2Only)
                    {
                        int noAnimation = JObject.Parse(File.ReadAllText(activeSong.selectedVanillaPath))["noteKinds"][Difficulties[songIndex].ToLowerInvariant()].Count();
                        Require(activeSong.vanillaPlayback.Week2Stage.NoAnimationHits == noAnimation, "Noanim hits did not preserve chart animation events.");
                    }
                    checkedEnd = true;
                    Debug.Log("SONG COMPLETED: " + Ids[songIndex] + " " + Difficulties[songIndex] + ", all heads hit, all events consumed, zero misses.");
                }
                if (VanillaResultsScreen.Active != null)
                {
                    Require(checkedEnd && VanillaResultsScreen.Active.Data.score == 0 && !VanillaResultsScreen.Active.Data.rankImproved,
                        "Autoplay results changed score eligibility.");
                    VanillaResultsScreen.Active.Accept();
                }
                if (SceneManager.GetActiveScene().name != "Title") return;
                Require(checkedEnd, "Song returned to menu before its completion checks.");
                songIndex++;
                Next(0);
            }
            else if (phase == 7)
            {
                Require(elapsed < 15, "Stage probe did not return to the menu.");
                if (SceneManager.GetActiveScene().name != "Title") return;
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

    private static void CheckGirlfriendReactions(Song song)
    {
        var stage = song.vanillaPlayback.CharacterStage;
        var graphic = song.vanillaPlayback.CampaignStage != null ? song.vanillaPlayback.CampaignStage.CharacterGraphic(2)
            : song.vanillaPlayback.Week2Stage != null ? song.vanillaPlayback.Week2Stage.CharacterGraphic(2)
            : song.vanillaPlayback.Week3Stage?.CharacterGraphic(2);
        if (graphic == null || graphic.name.IndexOf("nene", StringComparison.OrdinalIgnoreCase) >= 0) return;
        string previous = graphic.Animation;
        stage.PlayAnimation("gf", "danceLeft");
        string dance = graphic.Animation;
        stage.Combo(49, false);
        Require(graphic.Animation == dance, "GF reacted below her combo milestone.");
        stage.Combo(50, false);
        Require(graphic.Animation == (graphic.Has("combo50") ? "combo50" : dance), "GF combo50 reaction differs from available source animations.");
        stage.PlayAnimation("gf", "danceLeft");
        stage.Combo(69, true);
        Require(graphic.Animation == dance, "GF reacted below her combo-drop threshold.");
        stage.Combo(70, true);
        Require(graphic.Animation == (graphic.Has("drop70") ? "drop70" : dance), "GF drop70 reaction differs from available source animations.");
        if (graphic.Has("combo50") && graphic.Has("drop70"))
        {
            stage.Combo(50, false);
            Require(graphic.Animation == "combo50", "GF's previous reaction blocked her combo reaction.");
            stage.Combo(70, true);
            Require(graphic.Animation == "drop70", "GF's combo reaction blocked her drop reaction.");
        }
        stage.PlayAnimation("gf", previous);
        Debug.Log("GF REACTIONS PASSED: combo50 and drop70 with below-threshold controls.");
    }

    public static void CaptureStage(string name)
    {
        bool playing = activeSong.songStarted && !activeSong.isDead && !Pause.instance.IsPaused;
        if (playing) Pause.instance.PauseSong();
        try { CaptureStage(activeSong, Path.Combine(Output, name)); }
        finally { if (playing) Pause.instance.ContinueSong(); }
    }

    public static void CaptureStage(Song song, string path)
    {
        var cameraObject = new GameObject("Stage validation camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(song.mainCamera);
        camera.transform.SetPositionAndRotation(song.mainCamera.transform.position, song.mainCamera.transform.rotation);
        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
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
                if (pass == 0) File.WriteAllBytes(path, image.EncodeToPNG());
                Require(pass == 0 ? visible > 10000 : visible == 0, "Stage render or blank control failed: " + path + ", pass=" + pass + ", visible=" + visible);
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
        File.WriteAllText(Path.Combine(Output, "result.json"), new JObject { ["passed"] = success, ["errors"] = errors, ["completed"] = songIndex,
            ["editorWarnings"] = editorWarnings,
            ["mode"] = StageOnly ? "stage" : "full" }.ToString());
        if (success && Environment.GetEnvironmentVariable("UNITY_PARTY_SKIP_BUILD") != "1")
        {
            try { BuildAutomation.BuildWindows(); }
            catch (Exception exception) { Debug.LogException(exception); success = false; }
        }
        EditorApplication.Exit(success ? 0 : 1);
    }
}
