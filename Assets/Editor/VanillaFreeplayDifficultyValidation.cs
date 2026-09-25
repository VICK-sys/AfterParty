using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class VanillaFreeplayDifficultyValidation
{
    private static readonly string[] Numbers = { "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE" };
    private static readonly string[] Ranks = { "LOSS rank", "GOOD rank", "GREAT rank", "EXCELLENT rank", "PERFECT rank0", "PERFECT rank GOLD" };
    private static Dictionary<string, Transform> rows;
    private static Transform[] filterObjects;
    private static T Field<T>(VanillaFreeplay freeplay, string name) => (T)typeof(VanillaFreeplay)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freeplay);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Capture(VanillaFreeplay freeplay)
    {
        rows = Field<RectTransform>(freeplay, "list").Cast<Transform>().ToDictionary(row => row.name, row => row);
        filterObjects = Field<RectTransform>(freeplay, "filters").Cast<Transform>().ToArray();
    }

    public static void Check(VanillaFreeplay freeplay)
    {
        var song = Field<List<VanillaFreeplaySong>>(freeplay, "songs").Single(item => item.meta.songName == "Bopeebo");
        string originalDifficulty = freeplay.Difficulty;
        int originalMode = freeplay.Mode;
        var saved = new Dictionary<string, (bool existed, int value)>();
        void Set(string key, int value)
        {
            if (!saved.ContainsKey(key)) saved[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key));
            if (value < 0) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetInt(key, value);
        }
        void Difficulty(string name)
        {
            int moves = 0;
            while (freeplay.Difficulty != name && moves++ < 6)
            {
                freeplay.ChangeDifficulty(1);
                Settle(freeplay);
            }
            Require(freeplay.Difficulty == name, "Difficulty selection did not reach " + name);
        }
        void Select(string name)
        {
            int index = Field<List<VanillaFreeplaySong>>(freeplay, "filtered").FindIndex(item => item.meta.songName == name) + 1;
            Require(index > 0, "Expected song is missing: " + name);
            freeplay.MoveSelection(index - freeplay.SelectedIndex);
        }
        try
        {
            Set(song.FavoriteKey, 0);
            Set("Freeplay.Rank." + song.ScoreKey("Normal", PlayModes.Boyfriend), 2);
            Set("Freeplay.Rank." + song.ScoreKey("Hard", PlayModes.Boyfriend), 5);
            Set("Freeplay.Rank." + song.ScoreKey("Erect", PlayModes.Boyfriend), -1);
            Set("Freeplay.Rank." + song.ScoreKey("Nightmare", PlayModes.Boyfriend), 1);
            Set("Freeplay.Rank." + song.ScoreKey("Easy", PlayModes.Boyfriend), 4);
            Set("Freeplay.Rank." + song.ScoreKey("Normal", PlayModes.Opponent), 0);
            freeplay.SetMode(PlayModes.Boyfriend);
            Select("Bopeebo");
            foreach (string difficulty in new[] { "Normal", "Hard", "Erect", "Nightmare", "Easy", "Normal" })
            {
                Difficulty(difficulty);
                Require(freeplay.SelectedSong.id == song.id, "Difficulty change lost the selected song.");
                CheckRows(freeplay);
                CheckReuse(freeplay);
                var selected = Field<RectTransform>(freeplay, "list").GetChild(freeplay.SelectedIndex) as RectTransform;
                Require(selected.anchoredPosition.x < 1000, "Difficulty change replayed the list entrance.");
                Require(filterObjects.SequenceEqual(Field<RectTransform>(freeplay, "filters").Cast<Transform>()),
                    "Difficulty change rebuilt unchanged filters.");
            }
            freeplay.ToggleFavorite();
            Settle(freeplay);
            CheckRows(freeplay);
            freeplay.ToggleFavorite();
            Settle(freeplay);
            freeplay.SetMode(PlayModes.Opponent);
            CheckRows(freeplay);
            freeplay.SetMode(PlayModes.Boyfriend);
            Difficulty("Erect");
            var filtered = Field<List<VanillaFreeplaySong>>(freeplay, "filtered");
            int pico = filtered.FindIndex(item => item.meta.songName == "Pico") + 1;
            Field<RectTransform>(freeplay, "list").GetChild(pico).Find("Select").GetComponent<Button>().onClick.Invoke();
            Require(freeplay.SelectedSong.meta.songName == "Pico" && !freeplay.Busy, "Reused row clicked its old list index.");
            Difficulty("Normal");
            Select("Monster");
            Difficulty("Erect");
            Require(freeplay.SelectedSong != null && freeplay.SelectedSong.Difficulty("Erect") != null,
                "Unavailable remix left a hidden row selected.");
            freeplay.ChangeFilter(-2);
            Require(freeplay.VisibleSongCount == 0 && freeplay.SelectedSong == null, "Empty filter retained a song row.");
            CheckRows(freeplay);
            freeplay.ChangeFilter(2);
            Difficulty("Normal");
            CheckRows(freeplay);
            CheckReuse(freeplay);
            var title = Field<RectTransform>(freeplay, "list").GetChild(1).Find("Details/Title Clip/Title").GetComponent<Text>();
            string original = title.text;
            bool rejected = false;
            title.text = "Stale title control";
            try { CheckRows(freeplay); }
            catch (InvalidOperationException) { rejected = true; }
            finally { title.text = original; }
            Require(rejected, "Stale row control incorrectly passed.");
            Debug.Log("FREEPLAY DIFFICULTY PASSED: row reuse, stable filters, titles, icons, BPM, ratings, ranks, sparkle visibility, favorites, modes, remapped clicks, unavailable remixes, and empty filters. Stale title control rejected.");
        }
        finally
        {
            foreach (var pair in saved)
                if (pair.Value.existed) PlayerPrefs.SetInt(pair.Key, pair.Value.value);
                else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save();
            Difficulty(originalDifficulty);
            freeplay.SetMode(originalMode);
        }
    }

    private static void CheckReuse(VanillaFreeplay freeplay)
    {
        var list = Field<RectTransform>(freeplay, "list");
        Require(list.childCount == rows.Count, "Difficulty changes increased the row count.");
        foreach (Transform row in list)
            Require(rows.TryGetValue(row.name, out Transform original) && row == original, "Difficulty change replaced row " + row.name);
    }

    private static void Settle(VanillaFreeplay freeplay)
    {
        typeof(VanillaFreeplay).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(freeplay, new object[] { .21f });
    }

    private static void CheckRows(VanillaFreeplay freeplay)
    {
        var songs = Field<List<VanillaFreeplaySong>>(freeplay, "filtered");
        var active = Field<RectTransform>(freeplay, "list").Cast<Transform>().Where(row => row.gameObject.activeSelf).ToArray();
        Require(active.Length == songs.Count + 1 && active[0].name == "Random", "Visible rows do not match the filtered songs.");
        for (int i = 0; i < songs.Count; i++)
        {
            var song = songs[i];
            Transform row = active[i + 1];
            Transform detail = row.Find("Details");
            Require(row.name == song.meta.songName && detail.Find("Title Clip/Title").GetComponent<Text>().text == song.Title(freeplay.Difficulty), "Stale row title.");
            CheckNumber(detail, "BPM Digits", Mathf.RoundToInt(song.Bpm(freeplay.Difficulty)), 3);
            CheckNumber(detail, "Rating", song.Rating(freeplay.Difficulty), 2);
            int rank = PlayerPrefs.GetInt("Freeplay.Rank." + song.ScoreKey(freeplay.Difficulty, freeplay.Mode), -1);
            foreach (string name in new[] { "Rank", "Rank Glow" })
            {
                Transform badge = detail.Find(name);
                Require((badge != null && badge.gameObject.activeSelf) == (rank >= 0), "Stale rank visibility.");
                if (rank >= 0) Require(badge.GetComponent<VanillaFreeplaySprite>().CurrentFrameName.StartsWith(Ranks[Mathf.Clamp(rank, 0, 5)], StringComparison.Ordinal), "Stale rank badge.");
            }
            Transform sparkle = detail.Find("Rank Sparkle");
            Require((sparkle != null && sparkle.gameObject.activeSelf) == (rank == 5), "Gold sparkle leaked across difficulties.");
            Transform favorite = detail.Find("Favorite");
            Require((favorite != null && favorite.gameObject.activeSelf) == song.Favorite, "Stale favorite marker.");
            string character = song.Icon(freeplay.Difficulty);
            string path = "VanillaFreeplay/freeplay/icons/" + character + "pixel";
            while (Resources.Load<Texture2D>(path) == null && character.Contains("-"))
            {
                character = character.Substring(0, character.LastIndexOf('-'));
                path = "VanillaFreeplay/freeplay/icons/" + character + "pixel";
            }
            Texture2D expected = Resources.Load<Texture2D>(path);
            Transform icon = detail.Find("Icon");
            Require((icon != null && icon.gameObject.activeSelf) == (expected != null), "Stale icon visibility.");
            if (expected != null) Require(icon.GetComponent<VanillaFreeplaySprite>().mainTexture == expected, "Stale difficulty icon.");
        }
    }

    private static void CheckNumber(Transform detail, string name, int value, int digits)
    {
        string text = Mathf.Clamp(value, 0, (int)Mathf.Pow(10, digits) - 1).ToString("D" + digits);
        for (int digit = 0; digit < digits; digit++)
            Require(detail.Find(name + digit).GetComponent<VanillaFreeplaySprite>().CurrentFrameName.StartsWith(Numbers[text[digit] - '0'], StringComparison.Ordinal), "Stale " + name);
    }
}
