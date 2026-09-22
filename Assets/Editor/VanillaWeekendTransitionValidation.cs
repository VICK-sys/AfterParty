using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class VanillaWeekendTransitionValidation
{
    private const string Active = "VanillaWeekendTransitionValidation.Active";
    private static readonly string[] Cases = (Environment.GetEnvironmentVariable("UNITY_PARTY_WEEKEND_TRANSITION_CASES") ?? "natural;skip;prepare-error;playback-error;desperate-mode").Split(';');
    private static readonly List<string> completed = new List<string>();
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEKEND_TRANSITION_PATH") ?? Path.GetFullPath("Temp/WeekendTransition");
    private static int index;
    private static int phase;
    private static int errors;
    private static bool finishing;
    private static bool injected;
    private static bool sawVideo;
    private static double changed;
    private static Song song;

    static VanillaWeekendTransitionValidation()
    {
        if (!SessionState.GetBool(Active, false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
        Directory.CreateDirectory(Output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "WeekendTransitionValidation";
        PlayerPrefs.SetInt("Funkin.Options.DiscordRPC", 0);
        PlayerPrefs.SetInt("Funkin.Options.AutoPause", 0);
        PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(Cases[index] + ": " + message);
    }

    private static void Next(int value)
    {
        phase = value;
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (changed == 0) changed = EditorApplication.timeSinceStartup;
        double age = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && age < 180, "Runtime error or timeout in phase " + phase);
            if (phase == 0)
            {
                if (age < 4 || UnityEngine.Object.FindAnyObjectByType<MenuV2>() == null) return;
                OptionsV2.DesperateMode = OptionsV2.LiteMode = false;
                OptionsV2.DesperateMode = Cases[index] == "desperate-mode";
                Pause.ResetSession();
                var songs = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"));
                VanillaStoryCampaign.Begin("weekend1", "Hard", new List<VanillaFreeplaySong>
                {
                    songs.Single(item => item.meta.songName == "2hot"),
                    songs.Single(item => item.meta.songName == "Blazin'")
                });
                injected = sawVideo = false;
                SceneManager.LoadScene("Game_Backup3");
                Next(1);
                return;
            }
            if (phase == 1)
            {
                song = Song.instance;
                if (song == null || !song.songStarted || age < 2) return;
                Require(song.enabled && Song.modeOfPlay == PlayModes.Boyfriend, "Normal story updates are disabled.");
                if (Cases[index] == "desperate-mode") Require(song.vanillaPlayback.CampaignStage == null, "Desperate Mode control loaded the stage.");
                song.playerOneStats.currentScore = 12345;
                song.musicClip = AudioClip.Create("Weekend completion fixture", 441, 1, 44100, false);
                foreach (AudioSource source in song.musicSources) source.Stop();
                song.vocalSource.Stop();
                Next(2);
                return;
            }
            if (phase == 2)
            {
                var presentation = song != null ? song.vanillaPlayback.Presentation : null;
                if (presentation != null && presentation.Video != null)
                {
                    var video = presentation.Video;
                    sawVideo |= video.isPrepared && video.frame > 0;
                    bool prepareError = Cases[index] == "prepare-error";
                    bool playbackError = Cases[index] == "playback-error" && presentation.VideoActive && presentation.VideoTime > 7;
                    if (!injected && (prepareError || playbackError))
                    {
                        typeof(VanillaCampaignPresentation).GetField("videoError", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(presentation, "Injected video failure");
                        injected = true;
                    }
                    if ((Cases[index] == "skip" || Cases[index] == "desperate-mode") && presentation.VideoActive && presentation.VideoTime > 7) presentation.SkipVideo();
                }
                Require(VanillaResultsScreen.Active == null, "Intermediate song opened results.");
                if (VanillaStoryCampaign.SongIndex != 1) return;
                Require(VanillaStoryCampaign.Running && VanillaStoryCampaign.Score == 12345, "Campaign progress or score was lost.");
                Require(Song.currentSongMeta.songName == "Blazin'", "Campaign selected the wrong next song.");
                if (Cases[index].EndsWith("error")) Require(injected, "Failure control was not exercised.");
                if (Cases[index] != "prepare-error") Require(sawVideo, "Video did not decode before completion.");
                Next(3);
                return;
            }
            if (phase == 3)
            {
                var nextSong = Song.instance;
                if (nextSong == null || nextSong == song || !nextSong.songStarted) return;
                Require(nextSong.vanillaPlayback.SongId == "blazin" && nextSong.uiCamera.enabled && nextSong.battleCanvas.enabled,
                    "Blazin did not start with its HUD.");
                Require(!nextSong.vanillaPlayback.Presentation.Busy && nextSong.stopwatch.IsRunning, "Blazin remained blocked.");
                completed.Add(Cases[index]);
                Debug.Log("WEEKEND TRANSITION PASSED: " + Cases[index]);
                VanillaStoryCampaign.ReturnToMenu();
                Pause.ResetSession();
                SceneManager.LoadScene("Title");
                if (++index == Cases.Length) Finish(true);
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
        finishing = true;
        SessionState.SetBool(Active, false);
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonConvert.SerializeObject(new { passed, errors, completed, phase }, Formatting.Indented));
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
