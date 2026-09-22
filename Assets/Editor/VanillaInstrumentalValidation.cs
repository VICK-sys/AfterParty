using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class VanillaInstrumentalValidation
{
    private static int phase;
    private static double started;
    private static double changed;
    private static VanillaFreeplay freeplay;
    private static string expected;
    private static string vocals;
    private static string chart;
    private static bool countdownObserved;
    private static bool clockChecked;
    private static double countdownPosition;
    private static double countdownTime;
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    static VanillaInstrumentalValidation()
    {
        if (SessionState.GetBool("VanillaInstrumentalValidation.Active", false)) EditorApplication.update += Tick;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
        var songs = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"));
        int picoSongs = 0;
        foreach (var song in songs)
        {
            foreach (string difficulty in song.meta.difficulties.Keys)
            {
                string[] choices = song.Instrumentals(difficulty);
                Require(choices.Length > 0, "Missing base instrumental.");
                foreach (string choice in choices)
                    Require(File.Exists(song.InstrumentalPath(difficulty, choice)), "Missing instrumental: " + song.meta.songName + "/" + difficulty + "/" + choice);
            }
            if (song.Instrumentals("Normal").Skip(1).Contains("pico")) picoSongs++;
        }
        Require(picoSongs == 15, "Expected all 15 original songs with Pico alternatives.");
        var bopeebo = songs.Single(s => s.meta.songName == "Bopeebo");
        Require(bopeebo.Instrumentals("Normal").SequenceEqual(new[] { "", "pico" }), "Original choice order changed.");
        Require(bopeebo.Instrumentals("Erect").SequenceEqual(new[] { "erect" }), "Erect negative control exposed Pico.");
        Require(songs.Single(s => s.meta.songName == "Bopeebo (Pico Mix)").Instrumentals("Normal").SequenceEqual(new[] { "pico" }), "Pico chart negative control exposed alternatives.");
        Require(songs.Single(s => s.meta.songName == "Roses").InstrumentalStart("Normal", "pico") == 4, "Roses offset missing.");
        Debug.Log("INSTRUMENTAL CATALOG PASSED: all 15 Pico alternatives, every difficulty, Erect and Pico-chart controls, Roses offset.");
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaInstrumentalValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        double wait = EditorApplication.timeSinceStartup - changed;
        try
        {
            CheckOpeningClock();
            Require(EditorApplication.timeSinceStartup - started < 150, "Instrumental validation timed out at " + phase);
            switch (phase)
            {
                case 0:
                    if (wait < 4) return;
                    var menu = UnityEngine.Object.FindFirstObjectByType<MenuV2>();
                    Require(menu != null, "Menu missing.");
                    SessionState.SetString("VanillaInstrumentalValidation.Character", PlayerPrefs.GetString("Freeplay.Character", "bf"));
                    PlayerPrefs.SetString("Freeplay.Character", "bf");
                    VanillaFreeplay.RememberDifficulty("Normal");
                    freeplay = VanillaFreeplay.Open(menu, true);
                    Next();
                    break;
                case 1:
                    if (wait < 2 || freeplay.Busy) return;
                    CheckTopBar();
                    SelectSong("Bopeebo");
                    freeplay.ConfirmSelection();
                    Require(freeplay.InstrumentalMenuOpen && freeplay.SelectedInstrumental == "", "Confirm skipped Default popup.");
                    int selection = freeplay.SelectedIndex;
                    string difficulty = freeplay.Difficulty;
                    freeplay.MoveSelection(1);
                    freeplay.ChangeDifficulty(1);
                    Require(freeplay.SelectedIndex == selection && freeplay.Difficulty == difficulty, "Popup did not lock parent input.");
                    Next();
                    break;
                case 2:
                    if (wait < .4) return;
                    Require(freeplay.GetComponentsInChildren<VanillaFreeplaySprite>().Single(s => s.name == "Box").CurrentFrameName.StartsWith("idle0"), "Popup did not finish opening.");
                    typeof(VanillaFreeplayValidation).GetField("freeplay", Private).SetValue(null, freeplay);
                    typeof(VanillaFreeplayValidation).GetField("menu", Private).SetValue(null, UnityEngine.Object.FindFirstObjectByType<MenuV2>());
                    typeof(VanillaFreeplayValidation).GetMethod("Capture", Private).Invoke(null, new object[] { "instrumental-default.png", 1280, 720, false, .1f });
                    freeplay.ChangeInstrumental(1);
                    Require(freeplay.SelectedInstrumental == "pico", "Left did not select Pico.");
                    freeplay.ChangeInstrumental(-1);
                    Require(freeplay.SelectedInstrumental == "", "Right did not wrap to Default.");
                    for (int i = 0; i < 100; i++)
                    {
                        freeplay.ChangeInstrumental(1);
                        freeplay.ChangeInstrumental(-1);
                    }
                    Require(freeplay.SelectedInstrumental == "", "Rapid input changed the final selection.");
                    RequireArrowPulse(-35, .3f);
                    phase = 8;
                    changed = EditorApplication.timeSinceStartup;
                    break;
                case 8:
                    if (wait < .2) return;
                    RequireArrowPulse(-30, .6f);
                    phase = 2;
                    freeplay.CancelInstrumental();
                    Require(freeplay.Busy, "Reverse close unlocked early.");
                    Next();
                    break;
                case 3:
                    if (wait < .5) return;
                    Require(!freeplay.Busy && !freeplay.InstrumentalMenuOpen, "Cancel did not restore Freeplay.");
                    freeplay.MoveSelection(-freeplay.SelectedIndex);
                    freeplay.ConfirmSelection();
                    Require(freeplay.SelectedInstrumental == "default", "Random capsule has no default choice.");
                    freeplay.ChangeInstrumental(1);
                    Require(freeplay.SelectedInstrumental == "random", "Random choice missing.");
                    freeplay.CancelInstrumental();
                    Next();
                    break;
                case 4:
                    if (wait < .5) return;
                    string target = Environment.GetEnvironmentVariable("UNITY_PARTY_INSTRUMENTAL_SONG") ?? "Bopeebo";
                    SelectSong(target);
                    freeplay.ConfirmSelection();
                    freeplay.ChangeInstrumental(1);
                    expected = freeplay.SelectedSong.InstrumentalPath(freeplay.Difficulty, "pico");
                    vocals = freeplay.SelectedSong.meta.AssetPath("Voices.ogg", freeplay.Difficulty);
                    chart = freeplay.SelectedSong.meta.songPath;
                    Next();
                    break;
                case 5:
                    if (wait < .2) return;
                    freeplay.AcceptInstrumental();
                    Next();
                    break;
                case 6:
                    var song = UnityEngine.Object.FindFirstObjectByType<Song>();
                    if (song == null || !song.songStarted) return;
                    Require(song.selectedInstrumentalPath == expected, "Gameplay ignored Pico instrumental.");
                    Require(song.selectedVocalsPath == vocals && song.selectedSongDir == chart, "Instrumental selection changed chart or vocals.");
                    Require(song.musicSources[0].isPlaying, "Instrumental did not play.");
                    if (Song.currentSongMeta.freeplayInstrumentalStart > 0)
                    {
                        Require(song.SongPosition - Pause.GlobalOffset < 0 && !song.vocalSource.isPlaying, "Roses did not delay chart and vocals.");
                        Next();
                        return;
                    }
                    Finish();
                    break;
                case 7:
                    if (wait < 4.5) return;
                    var offsetSong = UnityEngine.Object.FindFirstObjectByType<Song>();
                    Require(offsetSong.vocalSource.isPlaying, "Roses vocals did not start after the offset.");
                    double position = (offsetSong.SongPosition - Pause.GlobalOffset) / 1000;
                    Require(Math.Abs(offsetSong.musicSources[0].time - position - 4) < .2, "Roses instrumental and chart lost their offset.");
                    Require(Math.Abs(offsetSong.vocalSource.time - position) < .2, "Roses vocals and chart lost synchronization.");
                    Debug.Log("ROSES OFFSET PASSED: full instrumental intro, delayed chart and vocals, synchronized playback.");
                    Finish();
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            PlayerPrefs.SetString("Freeplay.Character", SessionState.GetString("VanillaInstrumentalValidation.Character", "bf"));
            SessionState.SetBool("VanillaInstrumentalValidation.Active", false);
            EditorApplication.Exit(1);
        }
    }

    private static void Finish()
    {
        Require(clockChecked, "Countdown-to-audio clock transition was not observed.");
        Debug.Log("INSTRUMENTAL FLOW PASSED: open, idle, wrap, parent lock, reverse cancel, random popup, selected audio playback, original chart and vocals.");
        PlayerPrefs.SetString("Freeplay.Character", SessionState.GetString("VanillaInstrumentalValidation.Character", "bf"));
        SessionState.SetBool("VanillaInstrumentalValidation.Active", false);
        EditorApplication.Exit(0);
    }

    private static void CheckOpeningClock()
    {
        var song = Song.instance;
        if (song == null || clockChecked) return;
        double now = Time.realtimeSinceStartupAsDouble * 1000;
        if (song.IsCountingDown)
        {
            countdownObserved = true;
            countdownPosition = song.SongPosition;
            countdownTime = now;
            if (Song.currentSongMeta.freeplayInstrumentalStart > 0)
                Require(song.SongPosition - Pause.GlobalOffset < -3900, "Roses notes advanced before the instrumental lead-in.");
        }
        else if (song.songStarted && countdownObserved)
        {
            double jump = song.SongPosition - countdownPosition - (now - countdownTime);
            Require(Math.Abs(jump) < 100, "Chart clock jumped at the instrumental start: " + jump);
            clockChecked = true;
            Debug.Log("INSTRUMENTAL CLOCK PASSED: countdown-to-audio jump " + jump + " ms.");
        }
    }

    private static void CheckTopBar()
    {
        var bar = freeplay.GetComponentsInChildren<UnityEngine.RectTransform>().Single(item => item.name == "Top Bar");
        foreach (float width in new[] { 1280f, 1600f, 1280f })
        {
            freeplay.ApplyLayout(width);
            float bottom = bar.anchoredPosition.y + bar.rect.yMin;
            Require(Mathf.Abs(bottom + 64) < .01f, "Freeplay top bar moved below its original bottom edge.");
            Require(bar.anchoredPosition.y + bar.rect.yMax >= 656, "Freeplay top bar did not extend upward.");
            Require(bar.rect.width == width, "Freeplay top bar did not cover the viewport width.");
        }
        Debug.Log("FREEPLAY TOP BAR PASSED: upward coverage and unchanged bottom edge at standard and wide sizes.");
    }

    private static void RequireArrowPulse(float y, float scale)
    {
        var arrows = freeplay.GetComponentsInChildren<VanillaFreeplaySprite>()
            .Where(s => s.name == "Previous Instrumental" || s.name == "Next Instrumental").ToArray();
        Require(arrows.Length == 2, "Instrumental arrows missing.");
        foreach (var arrow in arrows)
        {
            Require(Mathf.Approximately(arrow.rectTransform.anchoredPosition.y, y), "Instrumental arrow drifted vertically.");
            Require(Mathf.Approximately(arrow.drawScale, scale), "Instrumental arrow pulse scale is incorrect.");
        }
    }

    private static void SelectSong(string name)
    {
        for (int i = 0; i <= freeplay.VisibleSongCount && freeplay.SelectedSong?.meta.songName != name; i++) freeplay.MoveSelection(1);
        Require(freeplay.SelectedSong?.meta.songName == name, "Song unavailable: " + name);
    }

    private static void Next()
    {
        phase++;
        changed = EditorApplication.timeSinceStartup;
    }
}


