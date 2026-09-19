using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaCutsceneBoundaryValidation
{
    private const string Active = "VanillaCutsceneBoundaryValidation.Active";
    private static readonly string[] Cases = (Environment.GetEnvironmentVariable("UNITY_PARTY_CUTSCENE_CASES") ??
        "winter-horrorland||0|intro;senpai||1|intro;roses||1|intro;thorns||1|intro;ugh||1|intro;guns||1|intro;stress||1|intro;darnell||1|intro;pico|pico|0|intro;philly-nice|pico|0|intro;blammed|pico|0|intro;senpai|pico|0|intro;roses|pico|0|intro;stress|pico|0|intro;spaghetti||0|intro;senpai||0|control;ugh||0|control;bopeebo||1|control;spaghetti||0|retry;winter-horrorland||0|retry;eggnog|erect|0|outro;stress|pico|0|outro;2hot||1|outro;blazin||1|outro;spaghetti||0|outro").Split(';');
    private static readonly List<string> completed = new List<string>();
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_CUTSCENE_PATH");
    private static string[] Current => Cases[index].Split('|');
    private static int index;
    private static int phase;
    private static int errors;
    private static bool finishing;
    private static bool covered;
    private static double changed;
    private static double advanced;
    private static Song song;

    static VanillaCutsceneBoundaryValidation()
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
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        PlayerPrefs.SetInt("Funkin.Options.DiscordRPC", 0);
        PlayerPrefs.SetInt("Funkin.Options.AutoPause", 0);
        PlayerPrefs.Save();
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(Cases[index] + ": " + message);
    }

    private static bool IsCover(Image image) => image.enabled && image.gameObject.activeInHierarchy
        && image.color.a >= .999f && image.rectTransform.rect.width >= 1280 && image.rectTransform.rect.height >= 720
        && image.canvasRenderer.GetMesh()?.vertexCount > 0;

    private static void Next(int next)
    {
        phase = next;
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (changed == 0) changed = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && elapsed < 100, "Runtime error or boundary timeout in phase " + phase);
            if (phase == 0)
            {
                if (elapsed < 3 || Object.FindAnyObjectByType<MenuV2>() == null) return;
                OptionsV2.DesperateMode = OptionsV2.LiteMode = false;
                Pause.ResetSession();
                VanillaStoryCampaign.ReturnToMenu();
                VanillaFreeplay.ReturnToFreeplay = false;
                string variation = Current[1] == "erect" ? "" : Current[1];
                var item = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
                    .Single(entry =>
                    {
                        var data = JObject.Parse(File.ReadAllText(Path.Combine(entry.meta.songPath, "Vanilla.json")));
                        return (string)data["song"] == Current[0] && ((string)data["variation"] ?? "") == variation;
                    });
                Song.currentSongMeta = item.meta;
                Song.difficulty = Current[1] == "erect" ? "Erect" : "Hard";
                if (Current[2] == "1") VanillaStoryCampaign.Begin("boundary-check", Song.difficulty, new List<VanillaFreeplaySong> { item });
                Song.modeOfPlay = PlayModes.Autoplay;
                if (Current[3] == "retry" || Current[3] == "outro")
                {
                    typeof(Pause).GetField("sessionSong", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, item.meta.songPath);
                    Pause.PlayedCampaignIntro = true;
                }
                UnityEngine.Random.InitState(42);
                covered = false;
                song = null;
                LoadingTransition.instance.LoadScene("Game_Backup3");
                Next(1);
                return;
            }
            if (phase == 1)
            {
                if (SceneManager.GetActiveScene().name != "Game_Backup3") return;
                song = Object.FindAnyObjectByType<Song>();
                var presentation = song?.vanillaPlayback?.Presentation;
                if (presentation == null) return;
                if (LoadingTransition.instance.toggled && LoadingTransition.instance.Progress == 1 && !covered)
                {
                    Canvas.ForceUpdateCanvases();
                    var cover = presentation.GetComponentsInChildren<Image>().FirstOrDefault(IsCover);
                    if (Current[3] == "intro")
                    {
                        Require(presentation.Busy && !song.uiCamera.enabled && !song.battleCanvas.enabled && cover != null,
                            "Gameplay is exposed while loading fades before the intro.");
                        Color color = cover.color;
                        cover.color = Color.clear;
                        bool rejected = !IsCover(cover);
                        cover.color = color;
                        Require(rejected, "Transparent cover control passed.");
                    }
                    else Require(!presentation.Busy && cover == null, "A suppressed or absent intro covered gameplay.");
                    covered = true;
                    Debug.Log("CUTSCENE LOADING BOUNDARY PASSED: " + Cases[index]);
                }
                if (EditorApplication.timeSinceStartup - advanced > .8 && !LoadingTransition.instance.toggled)
                {
                    if (presentation.VideoActive) presentation.SkipVideo();
                    else presentation.AdvanceDialogue();
                    advanced = EditorApplication.timeSinceStartup;
                }
                if (!song.songStarted) return;
                Require(covered && !presentation.Busy && song.uiCamera.enabled && song.battleCanvas.enabled,
                    "Intro did not restore gameplay after countdown.");
                if (Current[3] == "outro")
                {
                    foreach (var audio in song.musicSources) audio.Stop();
                    song.vocalSource.Stop();
                    song.respawning = true;
                    Require(!presentation.AllowEnd(song), "Outro failed to block completion.");
                    if (Current[0] != "spaghetti") Require(presentation.Busy, "Outro yielded before taking presentation ownership.");
                    Next(2);
                }
                else CompleteCase();
                return;
            }
            if (phase == 2)
            {
                var presentation = song.vanillaPlayback.Presentation;
                if (elapsed < .15) return;
                Require(presentation.OutroFinished || presentation.Busy, "Outro released ownership before completion.");
                if (Current[0] != "eggnog") Require(!song.uiCamera.enabled && !song.battleCanvas.enabled
                    || presentation.GetComponentsInChildren<Image>().Any(IsCover),
                    "Outro exposed gameplay HUD.");
                if (EditorApplication.timeSinceStartup - advanced > .8)
                {
                    if (presentation.VideoActive) presentation.SkipVideo();
                    else presentation.AdvanceDialogue();
                    advanced = EditorApplication.timeSinceStartup;
                }
                if (!presentation.OutroFinished) return;
                Require(presentation.AllowEnd(song), "Outro did not release completion.");
                CompleteCase();
                return;
            }
            if (phase == 3 && SceneManager.GetActiveScene().name == "Title")
            {
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

    private static void CompleteCase()
    {
        completed.Add(Cases[index]);
        Debug.Log("CUTSCENE BOUNDARY PASSED: " + Cases[index]);
        VanillaStoryCampaign.ReturnToMenu();
        Pause.ResetSession();
        SceneManager.LoadScene("Title");
        Next(3);
    }

    private static void Finish(bool passed)
    {
        finishing = true;
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonConvert.SerializeObject(new { passed, errors, completed, current = Cases[Math.Min(index, Cases.Length - 1)] }, Formatting.Indented));
        SessionState.SetBool(Active, false);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
