using System;
using System.Collections.Generic;
using UnityEngine;

public static class VanillaStoryCampaign
{
    public static bool ReturnToStory { get; private set; }
    public static bool Running { get; private set; }
    public static string LevelId { get; private set; }
    public static string Difficulty { get; private set; }
    public static int Score { get; private set; }
    public static int SongIndex { get; private set; }
    private static List<VanillaFreeplaySong> playlist;
    private static bool scoreEligible;

    public static string ScoreKey(string level, string difficulty) => "Story.Score." + level + "." + difficulty.ToLowerInvariant();
    public static int HighScore(string level, string difficulty) => PlayerPrefs.GetInt(ScoreKey(level, difficulty), 0);
    public static bool HasBeaten(string level) => HighScore(level, "easy") > 0 || HighScore(level, "normal") > 0 || HighScore(level, "hard") > 0;

    public static void Begin(string level, string difficulty, List<VanillaFreeplaySong> songs)
    {
        if (songs == null || songs.Count == 0 || songs.Exists(song => song.Difficulty(difficulty) == null))
            throw new ArgumentException("Story Mode requires a complete playlist for the selected difficulty.");
        LevelId = level;
        Difficulty = difficulty;
        playlist = new List<VanillaFreeplaySong>(songs);
        Score = 0;
        scoreEligible = true;
        SongIndex = 0;
        ReturnToStory = Running = true;
        VanillaFreeplay.ReturnToFreeplay = false;
        SelectSong();
    }

    private static void SelectSong()
    {
        Song.currentSongMeta = playlist[SongIndex].meta;
        Song.difficulty = playlist[SongIndex].Difficulty(Difficulty);
        Song.modeOfPlay = 1;
    }

    public static void ChangeDifficulty(string difficulty)
    {
        if (!Running) return;
        Score = 0;
        Difficulty = difficulty;
    }

    public static bool CompleteSong(SongMetaV2 meta, string difficulty, int mode, int score, bool completed, bool saveScore = true)
    {
        if (!Running) return false;
        if (!completed || mode != 1 || !string.Equals(difficulty, Difficulty, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(meta.songPath, playlist[SongIndex].meta.songPath, StringComparison.OrdinalIgnoreCase))
        {
            Running = false;
            return false;
        }
        Score += score;
        scoreEligible &= saveScore;
        SongIndex++;
        if (SongIndex < playlist.Count)
        {
            SelectSong();
            return true;
        }
        string key = ScoreKey(LevelId, Difficulty);
        if (scoreEligible && (!PlayerPrefs.HasKey(key) || Score > PlayerPrefs.GetInt(key))) PlayerPrefs.SetInt(key, Score);
        PlayerPrefs.Save();
        Running = false;
        return false;
    }

    public static void ReturnToMenu()
    {
        Running = ReturnToStory = false;
        playlist = null;
    }
}
