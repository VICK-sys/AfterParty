using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaWeek7VideoValidation
{
    private static readonly string[] Songs = { "Ugh", "Guns", "Stress" };
    private static int index;
    private static int phase;
    private static int errors;
    private static double changed;
    private static float pausedTime;
    private static bool finishing;
    private static Song song;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEK7_VIDEO_PATH");

    static VanillaWeek7VideoValidation()
    {
        if (!SessionState.GetBool("VanillaWeek7VideoValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run video validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaWeek7VideoValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
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
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && elapsed < 65, "Video failed or timed out at phase " + phase);
            if (phase == 0)
            {
                if (elapsed < 7 || Object.FindAnyObjectByType<MenuV2>() == null) return;
                OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
                Pause.ResetSession();
                var item = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
                    .Single(entry => entry.meta.songName == Songs[index]);
                VanillaStoryCampaign.Begin("week7", "Hard", new List<VanillaFreeplaySong> { item });
                Song.modeOfPlay = PlayModes.Autoplay;
                SceneManager.LoadScene("Game_Backup3");
                Next(1);
            }
            else if (phase == 1)
            {
                song = Object.FindAnyObjectByType<Song>();
                var presentation = song?.vanillaPlayback?.Presentation;
                if (presentation == null || !presentation.VideoActive || presentation.VideoTime < 1.5f) return;
                Require(!song.songStarted && !song.IsCountingDown, "Cutscene overlaps gameplay.");
                Require(presentation.Video.isPlaying && presentation.Video.frame > 0, "Video is not decoding.");
                Require(presentation.GetComponentsInChildren<Text>().Any(text => text.name == "Cutscene Subtitles" && text.text.Length > 0), "Cutscene subtitles are missing.");
                Capture(presentation);
                Pause.instance.PauseSong();
                Require(Pause.instance.IsPaused && Pause.instance.View.Labels.SequenceEqual(new[] { "Resume", "Skip Cutscene", "Restart Cutscene", "Exit to Menu" }), "Cutscene pause menu changed.");
                pausedTime = presentation.VideoTime;
                Next(2);
            }
            else if (phase == 2)
            {
                if (elapsed < .7) return;
                var presentation = song.vanillaPlayback.Presentation;
                Require(Math.Abs(presentation.VideoTime - pausedTime) < .03 && !presentation.Video.isPlaying, "Paused cutscene advanced.");
                Pause.instance.ContinueSong();
                Next(3);
            }
            else if (phase == 3)
            {
                if (elapsed < .7) return;
                var presentation = song.vanillaPlayback.Presentation;
                Require(presentation.VideoTime > pausedTime + .3 && presentation.Video.isPlaying, "Cutscene did not resume.");
                Pause.instance.PauseSong();
                presentation.RestartVideo();
                Pause.instance.ContinueSong();
                Next(4);
            }
            else if (phase == 4)
            {
                if (elapsed < .7) return;
                var presentation = song.vanillaPlayback.Presentation;
                Require(presentation.VideoTime < 1.5f && presentation.VideoTime > .1f, "Cutscene did not restart.");
                if (index == 1)
                {
                    Pause.instance.PauseSong();
                    presentation.SkipVideo();
                    Pause.instance.ContinueSong();
                }
                Next(5);
            }
            else if (phase == 5)
            {
                if (!song.songStarted) return;
                Require(!song.vanillaPlayback.Presentation.Busy && !song.vanillaPlayback.Presentation.VideoActive && song.uiCamera.enabled && song.battleCanvas.enabled,
                    "Cutscene did not restore gameplay and HUD.");
                Pause.instance.RestartSong();
                Next(6);
            }
            else if (phase == 6)
            {
                song = Object.FindAnyObjectByType<Song>();
                if (song == null) return;
                Require(song.vanillaPlayback?.Presentation?.VideoActive != true, "Retry replayed the cutscene.");
                if (!song.songStarted) return;
                Debug.Log("WEEK 7 VIDEO PASSED: " + Songs[index] + ", decoded frames, subtitles, pause, resume, restart, completion, retry.");
                VanillaStoryCampaign.ReturnToMenu();
                Pause.instance.QuitSong();
                Next(7);
            }
            else if (phase == 7)
            {
                if (SceneManager.GetActiveScene().name != "Title") return;
                if (++index == Songs.Length) Finish(true);
                else Next(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Capture(VanillaCampaignPresentation presentation)
    {
        RenderTexture previous = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = presentation.Video.targetTexture;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            Require(texture.GetPixels32().Count(pixel => pixel.r > 40 || pixel.g > 40 || pixel.b > 40) > 10000, "Blank cutscene control failed.");
            File.WriteAllBytes(Path.Combine(Output, Songs[index] + "-video.png"), texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(texture);
        }
    }

    private static void Finish(bool passed)
    {
        finishing = true;
        SessionState.SetBool("VanillaWeek7VideoValidation.Active", false);
        string result = "WEEK 7 VIDEO VALIDATION: passed=" + passed + ", errors=" + errors + ", song=" + index + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        Debug.Log(result);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
