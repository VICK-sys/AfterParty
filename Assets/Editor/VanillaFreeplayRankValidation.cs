using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaFreeplayRankValidation
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static VanillaFreeplay freeplay;
    private static MenuV2 menu;
    private static int phase;
    private static int errors;
    private static int assertions;
    private static double started;
    private static double changed;
    private static bool finishing;
    private static bool observedReturn;
    private static bool observedDjReaction;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_FREEPLAY_TEST_PATH") ?? Path.GetFullPath("Validation/FreeplayRank");

    static VanillaFreeplayRankValidation()
    {
        if (!SessionState.GetBool("VanillaFreeplayRankValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run rank validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "FreeplayRankValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaFreeplayRankValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal)
            && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static T Field<T>(string name) => (T)typeof(VanillaFreeplay).GetField(name, Instance).GetValue(freeplay);
    private static void Advance(float time)
    {
        float target = time + 0.000001f;
        float remaining = target - freeplay.RankAnimationTime;
        var sprites = freeplay.GetComponentsInChildren<VanillaFreeplaySprite>(true);
        var animations = freeplay.GetComponentsInChildren<VanillaFreeplayAnimate>();
        while (remaining > 0)
        {
            float delta = Mathf.Min(remaining, 1f / 120);
            foreach (var sprite in sprites) if (sprite != null && sprite.gameObject.activeInHierarchy) sprite.Tick(delta);
            foreach (var animation in animations) if (animation != null) animation.Tick(delta);
            typeof(VanillaFreeplay).GetMethod("DrawRankAnimation", Instance).Invoke(freeplay, new object[] { delta });
            remaining = freeplay.RankAnimationTime < 0 ? 0 : target - freeplay.RankAnimationTime;
        }
        Canvas.ForceUpdateCanvases();
    }

    private static void Capture(string filename, int width = 1280, int height = 720, bool blank = false)
    {
        typeof(VanillaFreeplayValidation).GetField("freeplay", Static).SetValue(null, freeplay);
        typeof(VanillaFreeplayValidation).GetField("menu", Static).SetValue(null, menu);
        typeof(VanillaFreeplayValidation).GetMethod("Capture", Static).Invoke(null, new object[] { filename, width, height, blank, 0.1f });
    }

    private static PlayerStat Stats(int rank)
    {
        int sicks = new[] { 40, 60, 80, 90, 99, 100 }[rank];
        return new PlayerStat { totalNoteHits = 100, totalSicks = sicks, totalGoods = rank == 4 ? 1 : 0 };
    }

    private static void Reopen(VanillaFreeplayRankChange change, bool expectAnimation)
    {
        VanillaFreeplay.QueueRankReturn(change);
        Object.DestroyImmediate(freeplay.gameObject);
        freeplay = VanillaFreeplay.Open(menu, true, Path.Combine(Output, "EmptyBundles"));
        Require(freeplay.RankAnimationPlaying == expectAnimation, "Unexpected rank return trigger.");
        freeplay.enabled = false;
    }

    private static void CheckRanks()
    {
        var song = freeplay.SelectedSong;
        string key = "Freeplay.Rank." + song.ScoreKey(freeplay.Difficulty, 1);
        string[] prefixes = { "LOSS rank", "GOOD rank", "GREAT rank", "EXCELLENT rank", "PERFECT rank0", "PERFECT rank GOLD" };
        for (int rank = 0; rank < 6; rank++)
        {
            PlayerPrefs.SetInt(key, rank - 1);
            VanillaFreeplay.ArmRankReturn(song.meta, freeplay.Difficulty, 1);
            var change = VanillaFreeplayCatalog.SaveCompletion(song.meta, freeplay.Difficulty, 1, Stats(rank), true, 100);
            Require(change != null && change.oldRank == rank - 1 && change.newRank == rank, "Old/new rank capture failed: " + rank);
            Reopen(change, true);
            int selection = freeplay.SelectedIndex;
            string difficulty = freeplay.Difficulty;
            freeplay.MoveSelection(1);
            freeplay.ChangeDifficulty(1);
            freeplay.ChangeFilter(1);
            freeplay.SetMode(PlayModes.Autoplay);
            freeplay.ConfirmSelection();
            freeplay.Close();
            Require(freeplay.SelectedIndex == selection && freeplay.Difficulty == difficulty && freeplay.Mode == 1 && !Field<bool>("closing"), "Rank input lock failed.");
            Require(freeplay.PreviewSource.clip == null && !freeplay.PreviewSource.isPlaying, "Preview played during rank animation.");
            var badges = freeplay.GetComponentsInChildren<VanillaFreeplaySprite>(true);
            var badge = badges.Single(b => b.name == "Rank" && b.transform.IsChildOf(Field<RectTransform>("rankZoom")));
            Require(!badge.gameObject.activeSelf, "New badge appeared before reveal.");
            Require((Field<VanillaFreeplaySprite>("oldRankBadge") != null) == (rank > 0), "First clear showed an old rank.");
            Advance(0.5f);
            Require(badge.gameObject.activeSelf && Mathf.Abs(badge.drawScale - 20) < 0.001f && badge.CurrentFrameName.StartsWith(prefixes[rank]), "Rank reveal frame or scale failed.");
            Advance(0.55f);
            Require(Mathf.Abs(badge.drawScale - 10.45f) < 0.01f, "Rank pop is not the original 0.1 second linear tween.");
            if (rank == 3) Capture("rank-reveal.png");
            Advance(0.65f);
            Require(Field<bool>("rankHit") && Mathf.Abs(badge.drawScale - 0.9f) < 0.001f, "Rank impact failed.");
            Require(Field<AudioSource>("effects").isPlaying, "Rank impact sound did not play.");
            if (rank > 0) Require(Field<VanillaFreeplaySprite>("rankSparks").gameObject.activeSelf && !Field<VanillaFreeplaySprite>("oldRankBadge").gameObject.activeSelf, "Old rank sparks failed.");
            Capture("rank-" + rank + "-hit.png");
            Advance(1.15f);
            Require(Field<bool>("rankSound"), "Rank announcement did not fire.");
            Advance(1.6f);
            Require(Field<bool>("rankSlammed") && Mathf.Abs(Field<VanillaFreeplaySprite>("rankVignette").color.a - 1) < 0.001f, "Rank slam burst failed.");
            Require(Mathf.Abs(Field<RectTransform>("rankZoom").localScale.x - 0.8f) < 0.001f, "Rank slam camera did not recoil.");
            Advance(1.68f);
            Capture("rank-" + rank + "-slam.png");
            Require(freeplay.GetComponentsInChildren<VanillaFreeplaySprite>().Count(s => s.name.StartsWith("Rank Impact")) == 15, "Capsule impact trail missing.");
            Advance(2.21f);
            Require(!freeplay.Busy && !freeplay.RankAnimationPlaying, "Rank animation failed to release input at 2.2 seconds.");
            Require(badge.transform.IsChildOf(Field<RectTransform>("list")), "Ranked capsule did not return to normal menu.");
            Advance(2.61f);
            Require(Field<RectTransform>("menuContent").localScale == Vector3.one, "Menu zoom did not reset.");
            if (rank == 5)
            {
                Capture("rank-finished.png");
                Capture("rank-wide.png", 1920, 720);
                Capture("rank-blank.png", 1280, 720, true);
            }
            Reopen(null, false);
        }
        foreach (var check in new[] { "same", "worse", "aborted", "autoplay", "opponent", "empty", "wrong difficulty", "wrong song" })
        {
            VanillaFreeplay.ArmRankReturn(song.meta, "Normal", 1);
            int mode = check == "autoplay" ? PlayModes.Autoplay : check == "opponent" ? PlayModes.Opponent : 1;
            var change = VanillaFreeplayCatalog.SaveCompletion(song.meta, "Normal", mode, Stats(check == "worse" ? 0 : 5), check != "aborted", check == "empty" ? 0 : 100);
            if (check.StartsWith("wrong")) change = new VanillaFreeplayRankChange
            {
                songPath = check == "wrong song" ? Path.Combine(Output, "OtherSong") : song.meta.songPath,
                difficulty = check == "wrong difficulty" ? "Erect" : "Normal", mode = 1, oldRank = 0, newRank = 5
            };
            Reopen(change, false);
            Require(PlayerPrefs.GetInt(key) == 5, "Negative control changed the saved best rank: " + check);
        }
        foreach (string difficulty in new[] { "Erect", "Nightmare" })
        {
            SetDifficulty(difficulty);
            for (int i = 0; freeplay.SelectedSong?.meta.songName != "South" && i <= freeplay.VisibleSongCount; i++) freeplay.MoveSelection(1);
            var south = freeplay.SelectedSong;
            Require(south.meta.songName == "South", "South remix is missing from Freeplay.");
            string normalKey = "Freeplay.Rank." + south.ScoreKey("Normal", 1);
            int normalRank = PlayerPrefs.GetInt(normalKey, -1);
            PlayerPrefs.SetInt("Freeplay.Rank." + south.ScoreKey(difficulty, 1), 0);
            VanillaFreeplay.ArmRankReturn(south.meta, difficulty, 1);
            Reopen(VanillaFreeplayCatalog.SaveCompletion(south.meta, difficulty, 1, Stats(3), true, 100), true);
            Require(freeplay.Difficulty == difficulty && freeplay.SelectedSong.meta.songName == "South", "Rank return selected the wrong variation.");
            Advance(1.68f);
            Capture("rank-south-" + difficulty.ToLowerInvariant() + ".png");
            Advance(2.61f);
            Require(PlayerPrefs.GetInt(normalKey, -1) == normalRank, "Remix celebration overwrote the Normal rank.");
            Reopen(null, false);
        }
        SetDifficulty("Normal");
        for (int i = 0; freeplay.SelectedSong?.meta.songName != "Tutorial" && i <= freeplay.VisibleSongCount; i++) freeplay.MoveSelection(1);
        Debug.Log("FREEPLAY RANK CHECKS PASSED: six ranks, first clear, upgrades, exact timeline, input locks, preview mute, eight negative controls, consumed return.");
    }

    private static void SetDifficulty(string difficulty)
    {
        for (int i = 0; freeplay.Difficulty != difficulty && i < 6; i++)
        {
            freeplay.ChangeDifficulty(1);
            typeof(VanillaFreeplay).GetMethod("Draw", Instance).Invoke(freeplay, new object[] { .21f });
        }
        Require(freeplay.Difficulty == difficulty, "Rank fixture difficulty did not settle.");
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        double wait = EditorApplication.timeSinceStartup - changed;
        try
        {
            if (EditorApplication.timeSinceStartup - started >= 150) throw new InvalidOperationException("Rank probe timed out at phase " + phase);
            switch (phase)
            {
                case 0:
                    if (wait < 4) return;
                    menu = Object.FindFirstObjectByType<MenuV2>();
                    Require(menu?.vanillaMenu != null, "Main menu missing.");
                    PlayerPrefs.SetString("Freeplay.Character", "bf");
                    VanillaFreeplay.RememberDifficulty("Normal");
                    freeplay = VanillaFreeplay.Open(menu, true, Path.Combine(Output, "EmptyBundles"));
                    CheckRanks();
                    freeplay.enabled = true;
                    typeof(VanillaFreeplay).GetField("lastUpdateTime", Instance).SetValue(freeplay, Time.realtimeSinceStartupAsDouble);
                    PlayerPrefs.DeleteKey("Freeplay.Rank." + freeplay.SelectedSong.ScoreKey(freeplay.Difficulty, 1));
                    freeplay.ConfirmSelection();
                    Next();
                    break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "Game_Backup3" || Song.instance == null || !Song.instance.songStarted || wait < 3) return;
                    Song.instance.musicClip = AudioClip.Create("Freeplay rank end-of-track fixture", 441, 1, 44100, false);
                    foreach (AudioSource source in Song.instance.musicSources) source.Stop();
                    Song.instance.vocalSource.Stop();
                    Next();
                    break;
                case 2:
                    if (VanillaResultsScreen.Active != null) VanillaResultsScreen.Active.Accept();
                    if (SceneManager.GetActiveScene().name != "Title" || VanillaFreeplay.Active == null) return;
                    freeplay = VanillaFreeplay.Active;
                    menu = MenuV2.Instance;
                    observedReturn |= freeplay.RankAnimationPlaying;
                    int djFrame = Field<VanillaFreeplayAnimate>("dj").CurrentFrame;
                    observedDjReaction |= djFrame >= 105 && djFrame < 269;
                    if (freeplay.RankAnimationPlaying || freeplay.RankAnimationTime >= 0 || wait < 12) return;
                    Require(observedReturn && freeplay.SelectedSong.meta.songName == "Tutorial" && freeplay.Difficulty == "Normal", "Actual gameplay completion did not celebrate the selected song.");
                    if (!freeplay.PreviewSource.isPlaying) return;
                    Require(freeplay.PreviewSource.volume > 0 && !freeplay.Busy, "Preview or input failed to resume after gameplay rank return.");
                    Require(observedDjReaction && djFrame >= 17 && djFrame < 31, "DJ loss reaction did not play and return to Idle.");
                    Capture("rank-gameplay-return.png");
                    Finish(errors == 0);
                    break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Next() { phase++; changed = EditorApplication.timeSinceStartup; }

    private static void Finish(bool passed)
    {
        if (finishing) return;
        finishing = true;
        SessionState.SetBool("VanillaFreeplayRankValidation.Active", false);
        string result = "FREEPLAY RANK VALIDATION: passed=" + passed + ", errors=" + errors + ", assertions=" + assertions + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        Debug.Log(result);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
