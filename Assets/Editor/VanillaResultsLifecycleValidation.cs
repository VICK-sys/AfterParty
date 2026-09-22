using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class VanillaResultsLifecycleValidation
{
    private static int phase;
    private static int scenario = Environment.GetEnvironmentVariable("UNITY_PARTY_RESULTS_BLAZIN_ONLY") == "1" ? 6 : 0;
    private static int errors;
    private static int assertions;
    private static double changed;
    private static bool sawRank;
    private static VanillaFreeplaySong selected;
    private static List<VanillaFreeplaySong> songs;
    private static VanillaResultsData expected;
    private static int campaignNotes;
    private static int campaignScore;
    private static string scoreKey;
    private static readonly FieldInfo LightningTimer = typeof(VanillaCampaignStage).GetField("lightningTimer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo StageUpdate = typeof(VanillaCampaignStage).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_RESULTS_TEST_PATH") ?? Path.GetFullPath("Temp/ResultsLifecycle");

    static VanillaResultsLifecycleValidation()
    {
        if (!SessionState.GetBool("VanillaResultsLifecycleValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated batch editor.");
        Directory.CreateDirectory(Output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "ResultsLifecycleValidation";
        PlayerPrefs.SetInt("Funkin.Options.DiscordRPC", 0);
        PlayerPrefs.SetInt("Funkin.Options.AutoPause", 0);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaResultsLifecycleValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        assertions++;
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Next(int value)
    {
        phase = value;
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Launch()
    {
        songs = songs ?? VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"));
        string folder = scenario == 6 ? "04-Blazin" : scenario == 1 ? "01-Bopeebo-Pico" : "01-Bopeebo";
        selected = songs.Single(song => Path.GetFileName(song.meta.songPath) == folder);
        PlayerPrefs.SetString("Freeplay.Character", scenario == 1 ? "pico" : "bf");
        VanillaStoryCampaign.ReturnToMenu();
        VanillaFreeplay.ReturnToFreeplay = scenario != 2 && scenario != 5;
        Song.currentSongMeta = selected.meta;
        Song.difficulty = "Hard";
        Song.modeOfPlay = scenario == 4 ? PlayModes.Autoplay : PlayModes.Boyfriend;
        scoreKey = selected.ScoreKey("Hard", Song.modeOfPlay);
        PlayerPrefs.DeleteKey(scoreKey);
        PlayerPrefs.DeleteKey("Freeplay.Rank." + scoreKey);
        PlayerPrefs.DeleteKey("Freeplay.Clear." + scoreKey);
        if (scenario == 2)
        {
            PlayerPrefs.DeleteKey(VanillaStoryCampaign.ScoreKey("week1", "Hard"));
            VanillaStoryCampaign.Begin("week1", "Hard", new List<VanillaFreeplaySong> { selected, songs.Single(song => Path.GetFileName(song.meta.songPath) == "02-Fresh") });
        }
        else
        {
            VanillaFreeplay.ArmRankReturn(selected.meta, "Hard", Song.modeOfPlay);
            typeof(VanillaFreeplay).GetField("rememberedSong", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, selected.id);
            typeof(VanillaFreeplay).GetField("hasRememberedSelection", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
            VanillaFreeplay.RememberDifficulty("Hard");
        }
        SceneManager.LoadScene("Game_Backup3");
        Next(1);
    }

    private static int PlayerNotes()
    {
        var chart = JObject.Parse(File.ReadAllText(Path.Combine(Song.currentSongMeta.songPath, "Chart-hard.json")))["song"];
        return chart["notes"].Sum(section => section["sectionNotes"].Count(note => (int)note[1] > 3 ? !(bool)section["mustHitSection"] : (bool)section["mustHitSection"]));
    }

    private static void EndSong()
    {
        var song = Song.instance;
        int total = PlayerNotes();
        if (scenario == 1) Pause.instance.EnablePractice();
        if (scenario == 4)
        {
            Player.instance.Strumlines[0].HeadsHit = total;
            song.playerOneStats = new PlayerStat();
        }
        else song.playerOneStats = new PlayerStat { currentScore = 54321, totalSicks = total - 1, totalGoods = 1, totalNoteHits = total, hitNotes = total, highestCombo = total };
        expected = VanillaResultsData.Capture(song.playerOneStats, total);
        campaignNotes += total;
        campaignScore += expected.score;
        song.musicClip = AudioClip.Create("Results completion fixture", 441, 1, 44100, false);
        if (scenario == 3) song.FreeplayAborted = true;
        foreach (AudioSource source in song.musicSources) source.Stop();
        song.vocalSource.Stop();
        Next(2);
    }

    private static void CheckThunderStopped()
    {
        var stage = Song.instance.vanillaPlayback.CampaignStage;
        LightningTimer.SetValue(stage, -1f);
        StageUpdate.Invoke(stage, null);
        Require(!stage.GetComponent<AudioSource>().isPlaying, "Stage thunder continued into results.");
        Require((float)LightningTimer.GetValue(stage) == -1f, "Stage scheduled lightning during results.");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (changed == 0) changed = EditorApplication.timeSinceStartup;
        double age = EditorApplication.timeSinceStartup - changed;
        try
        {
            if (errors != 0 || age > 90) throw new InvalidOperationException("Lifecycle errors or timeout: " + scenario + "/" + phase);
            if (phase == 0)
            {
                if (SceneManager.GetActiveScene().name != "Title" || age < 5) return;
                Launch();
            }
            else if (phase == 1)
            {
                if (Song.instance == null || !Song.instance.songStarted || age < 3) return;
                Require(VanillaResultsScreen.Active == null, "Results remained active during gameplay.");
                if (scenario == 6)
                {
                    var stage = Song.instance.vanillaPlayback.CampaignStage;
                    LightningTimer.SetValue(stage, -1f);
                    StageUpdate.Invoke(stage, null);
                    Require((float)LightningTimer.GetValue(stage) > 0, "Gameplay lightning control did not trigger.");
                    Require(stage.GetComponent<AudioSource>().isPlaying, "Gameplay thunder control was silent.");
                }
                EndSong();
            }
            else if (phase == 2)
            {
                if (scenario == 6 && Song.instance.EnteringResults) CheckThunderStopped();
                if (scenario == 3)
                {
                    Require(VanillaResultsScreen.Active == null, "Aborted song showed results.");
                    if (SceneManager.GetActiveScene().name == "Title") Next(4);
                    return;
                }
                if (scenario == 2 && VanillaStoryCampaign.Running)
                {
                    Require(VanillaResultsScreen.Active == null, "Intermediate story song showed results.");
                    if (VanillaStoryCampaign.SongIndex == 1 && Song.instance != null && Song.instance.songStarted && age > 3)
                    {
                        Require(VanillaStoryCampaign.Score == 54321, "Campaign score did not accumulate.");
                        Require(!PlayerPrefs.HasKey(VanillaStoryCampaign.ScoreKey("week1", "Hard")), "Intermediate story score was saved.");
                        EndSong();
                    }
                    return;
                }
                var screen = VanillaResultsScreen.Active;
                if (screen == null) return;
                Require(!Song.instance.songStarted && !Song.instance.songSetupDone, "Gameplay remained active under results.");
                Require(screen.Data.Character == (scenario == 1 || scenario == 6 ? "pico" : "bf"), "Results used the wrong character.");
                Require(screen.Data.storyMode == (scenario == 2), "Story results flag.");
                Require(screen.Data.score == (scenario == 2 ? campaignScore : expected.score), "Results score was lost.");
                Require(screen.Data.totalNotes == (scenario == 2 ? campaignNotes : expected.totalNotes), "Results note totals were lost.");
                Require(screen.Data.totalNotesHit == screen.Data.totalNotes, "Results hit tally includes missed notes.");
                bool eligible = scenario == 0 || scenario == 2 || scenario == 5 || scenario == 6;
                Require(screen.Data.newHighscore == eligible && screen.Data.rankImproved == (scenario == 0 || scenario == 5 || scenario == 6), "Practice or autoplay earned a record.");
                if (!eligible) Require(!PlayerPrefs.HasKey(scoreKey) && !PlayerPrefs.HasKey("Freeplay.Rank." + scoreKey), "Ineligible completion saved a score.");
                if (scenario == 6 && age < 17) return;
                screen.Accept();
                screen.Accept();
                Next(3);
            }
            else if (phase == 3)
            {
                if (SceneManager.GetActiveScene().name != "Title") return;
                sawRank |= VanillaFreeplay.Active != null && VanillaFreeplay.Active.RankAnimationPlaying;
                if (VanillaPauseStickers.Active || age < 4) return;
                Require(VanillaResultsScreen.Active == null, "Results survived the scene change.");
                if (scenario == 0) Require(sawRank, "Improved rank did not play in Freeplay.");
                if (scenario == 1) Require(VanillaFreeplay.Active != null && VanillaFreeplay.Active.IsPico, "Pico results returned to BF Freeplay.");
                if (scenario == 2) Require(VanillaStoryMenu.Active != null && !VanillaStoryCampaign.Running, "Campaign results did not return to Story Mode.");
                if (scenario == 5) Require(VanillaFreeplay.Active != null && VanillaFreeplay.Active.SelectedSong?.id == selected.id, "Legacy song picker results lost Freeplay selection.");
                Next(4);
            }
            else if (phase == 4)
            {
                scenario++;
                campaignNotes = campaignScore = 0;
                if (scenario >= 7) Finish(true);
                else Next(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(false);
        }
    }

    private static void Finish(bool passed)
    {
        SessionState.SetBool("VanillaResultsLifecycleValidation.Active", false);
        string result = "RESULTS LIFECYCLE: passed=" + passed + ", assertions=" + assertions + ", errors=" + errors + ", scenario=" + scenario + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        Debug.Log(result);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
