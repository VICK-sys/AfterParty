using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Pause : MonoBehaviour
{
    public GameObject pauseScreen;
    public bool editingVolume;
    public static Pause instance;
    public static bool PracticeMode { get; private set; }
    public static int DeathCount { get; private set; }
    public static bool PlayedCampaignIntro { get; set; }
    public static int GlobalOffset => PlayerPrefs.GetInt("Funkin.GlobalOffset", 0);
    public bool IsPaused => pauseScreen != null && pauseScreen.activeSelf;
    public bool Transitioning { get; private set; }
    public VanillaPauseMenu View { get; private set; }
    private static string sessionSong;
    private float previousTimeScale = 1;
    private bool clockPaused;
    private bool songClockRunning;
    private bool beatClockRunning;
    private readonly List<AudioSource> pausedAudio = new List<AudioSource>();

    private void Awake()
    {
        instance = this;
        string path = Song.currentSongMeta?.songPath;
        if (sessionSong != path) ResetSession();
        sessionSong = path;
        if (pauseScreen != null) pauseScreen.SetActive(false);
        pauseScreen = new GameObject("Funkin Pause", typeof(RectTransform));
        pauseScreen.SetActive(false);
        View = pauseScreen.AddComponent<VanillaPauseMenu>();
        View.Initialize(this);
    }

    private void Update()
    {
        Song song = Song.instance;
        if (song == null || Transitioning || IsPaused || editingVolume) return;
        if (song.vanillaPlayback?.Presentation != null && song.vanillaPlayback.Presentation.Busy) return;
        if (!Input.GetKeyDown(Player.pauseKey) && !Input.GetKeyDown(KeyCode.Escape)
            && !Input.GetKeyDown(KeyCode.JoystickButton7)) return;
        if (song.isDead || !song.songStarted && !song.IsCountingDown) return;
        PauseSong();
    }

    public void PauseSong()
    {
        Song song = Song.instance;
        if (song == null || IsPaused || Transitioning || song.isDead || !song.songStarted && !song.IsCountingDown) return;
        if (song.vanillaPlayback?.Presentation != null && song.vanillaPlayback.Presentation.Busy) return;
        song.modInstance?.Invoke("OnPause");
        song.subtitleDisplayer.paused = true;
        songClockRunning = song.stopwatch?.IsRunning ?? false;
        beatClockRunning = song.beatStopwatch?.IsRunning ?? false;
        song.stopwatch?.Stop();
        song.beatStopwatch?.Stop();
        song.SetCountdownPaused(true);
        Player.instance?.ClearInput();
        pausedAudio.Clear();
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (!source.isPlaying) continue;
            pausedAudio.Add(source);
            source.Pause();
        }
        previousTimeScale = Time.timeScale;
        clockPaused = true;
        Time.timeScale = 0;
        pauseScreen.SetActive(true);
    }

    public void ContinueSong()
    {
        if (Transitioning || (!IsPaused && !editingVolume)) return;
        pauseScreen.SetActive(false);
        RestoreClock();
        Song song = Song.instance;
        song.SetCountdownPaused(false);
        Player.instance?.ClearInput();
        if (songClockRunning) song.stopwatch.Start();
        if (beatClockRunning) song.beatStopwatch.Start();
        song.subtitleDisplayer.paused = false;
        foreach (AudioSource source in pausedAudio)
            if (source != null) source.UnPause();
        pausedAudio.Clear();
        song.modInstance?.Invoke("OnUnpause");
    }

    private void RestoreClock()
    {
        if (!clockPaused) return;
        Time.timeScale = previousTimeScale;
        clockPaused = false;
    }

    public void RestartSong()
    {
        if (Transitioning) return;
        Transitioning = true;
        Song.instance.respawning = true;
        View.StopMusic();
        RestoreClock();
        SceneManager.LoadScene("Game_Backup3");
    }

    public void QuitSong()
    {
        if (Transitioning) return;
        Transitioning = true;
        Song.instance.FreeplayAborted = true;
        Song.instance.respawning = true;
        if (!VanillaStoryCampaign.ReturnToStory) VanillaFreeplay.ReturnToFreeplay = true;
        Song.instance.subtitleDisplayer.StopSubtitles();
        foreach (AudioSource source in Song.instance.musicSources) source.Stop();
        Song.instance.vocalSource.Stop();
        VanillaPauseStickers.Begin(() =>
        {
            View.StopMusic();
            RestoreClock();
            ResetSession();
            SceneManager.LoadScene("Title");
            if (DiscordController.instance != null) DiscordController.instance.EnableGameStateLoop = false;
        });
    }

    public void EnablePractice()
    {
        PracticeMode = true;
        View.ShowStandard();
    }

    public static void RecordDeath() => DeathCount++;

    public static void ResetSession()
    {
        PracticeMode = false;
        DeathCount = 0;
        PlayedCampaignIntro = false;
        sessionSong = null;
    }

    public static void SetGlobalOffset(float value)
    {
        PlayerPrefs.SetInt("Funkin.GlobalOffset", (int)Mathf.Clamp(value, -1500, 1500));
    }

    public static string TitleCase(string value) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase((value ?? "").Replace('-', ' ').ToLowerInvariant());

    public string[] Difficulties()
    {
        SongMetaV2 meta = Song.currentSongMeta;
        if (meta?.difficulties == null) return Array.Empty<string>();
        string suffix = meta.GetVariation(Song.difficulty)?.assetSuffix ?? "";
        string[] order = { "easy", "normal", "hard", "erect", "nightmare" };
        return meta.difficulties.Keys.Where(value => (meta.GetVariation(value)?.assetSuffix ?? "") == suffix
                && File.Exists(Path.Combine(meta.songPath, "Chart-" + value.ToLowerInvariant() + ".json")))
            .OrderBy(value => Array.IndexOf(order, value.ToLowerInvariant()) < 0 ? order.Length : Array.IndexOf(order, value.ToLowerInvariant()))
            .ThenBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void ChangeDifficulty(string difficulty)
    {
        if (!Difficulties().Contains(difficulty)) return;
        if (!string.Equals(Song.difficulty, difficulty, StringComparison.OrdinalIgnoreCase))
        {
            VanillaStoryCampaign.ChangeDifficulty(difficulty);
            VanillaFreeplay.RememberDifficulty(difficulty);
            Song.difficulty = difficulty;
        }
        RestartSong();
    }

    public void EditVolume()
    {
        pauseScreen.SetActive(false);
        Menu.instance.menuCanvas.enabled = true;
        Song.instance.battleCanvas.enabled = false;
        Menu.instance.mainMenu.SetActive(false);
        editingVolume = true;
    }

    public void SaveVolume()
    {
        pauseScreen.SetActive(true);
        Song.instance.battleCanvas.enabled = true;
        Menu.instance.menuCanvas.enabled = false;
        Menu.instance.mainMenu.SetActive(true);
        editingVolume = false;
    }

    private void OnDestroy()
    {
        RestoreClock();
        if (instance == this) instance = null;
    }
}
