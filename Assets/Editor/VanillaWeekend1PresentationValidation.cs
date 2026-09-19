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
public static class VanillaWeekend1PresentationValidation
{
    private static readonly string[] Songs = { "Darnell", "2hot", "Blazin'" };
    private static int index = int.TryParse(Environment.GetEnvironmentVariable("UNITY_PARTY_WEEKEND_VIDEO_START"), out int start) ? Mathf.Clamp(start, 0, 2) : 0;
    private static int phase;
    private static int errors;
    private static double changed;
    private static float pausedTime;
    private static bool finishing;
    private static bool outroStarted;
    private static bool sawDarnellVideo;
    private static bool sawDarnellReveal;
    private static Song song;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEKEND_VIDEO_PATH");

    static VanillaWeekend1PresentationValidation()
    {
        if (!SessionState.GetBool("VanillaWeekend1PresentationValidation.Active", false)) return;
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
        SessionState.SetBool("VanillaWeekend1PresentationValidation.Active", true);
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
            Require(errors == 0 && elapsed < (phase == 5 ? 150 : 65), "Video failed or timed out at phase " + phase);
            if (index == 0 && phase < 6 && song != null && song.vanillaPlayback?.Presentation != null)
            {
                var presentation = song.vanillaPlayback.Presentation;
                sawDarnellVideo |= presentation.VideoActive;
                if (sawDarnellVideo && !sawDarnellReveal && !presentation.VideoActive && !song.IsCountingDown && !song.songStarted)
                {
                    var covers = presentation.GetComponentsInChildren<Image>().Where(image => image.enabled && image.color.r == 0 && image.color.g == 0 && image.color.b == 0).ToArray();
                    bool covered = covers.Any(image => image.color.a >= .999f);
                    Require(presentation.Busy, "Darnell handoff released presentation ownership.");
                    Require(covered || presentation.OwnsCamera, "Video revealed idle characters before the in-game cutscene took control.");
                    if (!covered && !sawDarnellReveal)
                    {
                        Require(song.vanillaPlayback.CampaignStage.CharacterGraphic(0).Animation == "intro1", "Darnell reveal used an idle Pico pose.");
                        Require(!song.uiCamera.enabled && !song.battleCanvas.enabled, "Darnell reveal exposed the gameplay HUD.");
                        sawDarnellReveal = true;
                    }
                }
            }
            if (phase == 0)
            {
                if (elapsed < 7 || Object.FindAnyObjectByType<MenuV2>() == null) return;
                OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
                Pause.ResetSession();
                var item = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
                    .Single(entry => entry.meta.songName == Songs[index]);
                VanillaStoryCampaign.Begin("weekend1", "Hard", new List<VanillaFreeplaySong> { item });
                Song.modeOfPlay = PlayModes.Autoplay;
                SceneManager.LoadScene("Game_Backup3");
                outroStarted = false;
                Next(1);
            }
            else if (phase == 1)
            {
                song = Object.FindAnyObjectByType<Song>();
                var presentation = song?.vanillaPlayback?.Presentation;
                if (index > 0 && song != null && song.songStarted && !outroStarted)
                {
                    outroStarted = true;
                    foreach (var audio in song.musicSources) audio.Stop();
                    Require(!presentation.AllowEnd(song), "Outro did not delay song completion.");
                    Require(!song.stopwatch.IsRunning && !song.beatStopwatch.IsRunning, "Outro left the song clock running.");
                    song.enabled = false;
                }
                if (presentation == null || !presentation.VideoActive || presentation.VideoTime < (index == 1 ? 7 : 1.5f)) return;
                if (index == 0) Require(!song.songStarted && !song.IsCountingDown, "Cutscene overlaps gameplay.");
                Require(presentation.Video.isPlaying && presentation.Video.frame > 0, "Video is not decoding.");
                if (index == 1)
                {
                    Require(song.vanillaPlayback.CampaignStage.CharacterGraphic(1).Animation == "pissed", "2hot in-engine outro pose failed.");
                }
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
                if (index == 0 ? !song.songStarted : !song.vanillaPlayback.Presentation.OutroFinished) return;
                Require(!song.vanillaPlayback.Presentation.Busy && !song.vanillaPlayback.Presentation.VideoActive && song.uiCamera.enabled && song.battleCanvas.enabled,
                    "Cutscene did not restore gameplay and HUD.");
                bool covered = song.vanillaPlayback.Presentation.GetComponentsInChildren<Image>()
                    .Any(image => image.name == "Overlay" && image.isActiveAndEnabled && image.color == Color.black);
                Require(covered == (index > 0), "Video handoff must retain the outro cover and release the intro cover.");
                if (index == 0) { Require(sawDarnellReveal, "Darnell handoff reveal was not observed."); Pause.instance.RestartSong(); Next(6); }
                else { VanillaStoryCampaign.ReturnToMenu(); Pause.instance.QuitSong(); Next(7); }
            }
            else if (phase == 6)
            {
                song = Object.FindAnyObjectByType<Song>();
                if (song == null) return;
                Require(song.vanillaPlayback?.Presentation?.VideoActive != true, "Retry replayed the cutscene.");
                if (!song.songStarted) return;
                Debug.Log("WEEKEND 1 PRESENTATION PASSED: " + Songs[index] + ", decoded frames, pause, resume, restart, completion, retry.");
                VanillaStoryCampaign.ReturnToMenu();
                Pause.instance.QuitSong();
                Next(7);
            }
            else if (phase == 7)
            {
                if (SceneManager.GetActiveScene().name != "Title") return;
                if (++index == Songs.Length || Environment.GetEnvironmentVariable("UNITY_PARTY_DARNELL_HANDOFF_TEST") == "1") Finish(true);
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
        SessionState.SetBool("VanillaWeekend1PresentationValidation.Active", false);
        string result = "WEEKEND 1 PRESENTATION VALIDATION: passed=" + passed + ", errors=" + errors + ", song=" + index + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        Debug.Log(result);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
