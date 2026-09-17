using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaStoryValidation
{
    private static int phase;
    private static int errors;
    private static int page;
    private static double started;
    private static double changed;
    private static MenuV2 menu;
    private static VanillaStoryMenu story;
    private static bool finishing;
    private static bool capturedConfirm;
    private static bool sawDance;
    private static string firstFrame;
    private static int previousScore;
    private static int previousSongScore;
    private static int previousRank;
    private static float previousClear;
    private static string songScoreKey;
    private static string weekScoreKey;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_STORY_TEST_PATH") ?? Path.GetFullPath("Builds/StoryValidation");

    static VanillaStoryValidation()
    {
        if (!SessionState.GetBool("VanillaStoryValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run Story Mode validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        CheckPlaylist();
        CheckCampaign();
        CheckSprite();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaStoryValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckPlaylist()
    {
        List<VanillaStoryLevel> levels = VanillaStoryCatalog.Load();
        Require(levels.Select(level => level.id).SequenceEqual(new[] { "tutorial", "week1", "week2", "week3", "week4", "week5", "week6", "week7", "weekend1", "sserafim" }), "Story registry order changed.");
        Require(levels[8].songs.Length == 4 && levels[9].Tracks.Length == 3, "Scripted level track list changed.");
        Require(VanillaStoryCatalog.TryPlaylist(levels[1], "hard", out var songs, out _), "Week 1 Hard is not playable.");
        Require(songs.Select(song => song.meta.songName).SequenceEqual(new[] { "Bopeebo", "Fresh", "DadBattle" }), "Playlist did not preserve source song order.");
        var missing = new VanillaStoryLevel { songs = new[] { "bopeebo", "not-installed" }, songNames = new[] { "Bopeebo", "Missing control" } };
        Require(!VanillaStoryCatalog.TryPlaylist(missing, "normal", out var partial, out string names) && partial.Count == 0 && names == "Missing control", "Partial week was accepted.");
        Require(!VanillaStoryCatalog.TryPlaylist(levels[0], "nightmare", out _, out _), "Missing difficulty control was accepted.");
        Debug.Log("STORY CATALOG PASSED: registry order, scripted tracks, complete playlists, missing-song and missing-difficulty controls.");
    }

    private static void CheckCampaign()
    {
        string id = "story-validation-" + Guid.NewGuid();
        string key = VanillaStoryCampaign.ScoreKey(id, "normal");
        VanillaStoryCatalog.TryPlaylist(VanillaStoryCatalog.Load()[1], "normal", out var songs, out _);
        SongMetaV2 oldSong = Song.currentSongMeta;
        string oldDifficulty = Song.difficulty;
        int oldMode = Song.modeOfPlay;
        try
        {
            VanillaStoryCampaign.Begin(id, "normal", songs);
            Require(Song.currentSongMeta == songs[0].meta && Song.modeOfPlay == 1, "Campaign did not choose its first song.");
            Require(VanillaStoryCampaign.CompleteSong(songs[0].meta, "normal", 1, 100, true) && Song.currentSongMeta == songs[1].meta, "Campaign did not advance to Fresh.");
            Require(!VanillaStoryCampaign.CompleteSong(songs[1].meta, "normal", 1, 200, false) && !PlayerPrefs.HasKey(key), "Aborted campaign saved a level score.");
            VanillaStoryCampaign.Begin(id, "normal", songs);
            Require(!VanillaStoryCampaign.CompleteSong(songs[0].meta, "normal", 4, 90000, true) && !PlayerPrefs.HasKey(key), "Autoplay campaign saved a level score.");
            VanillaStoryCampaign.Begin(id, "normal", songs);
            Require(!VanillaStoryCampaign.CompleteSong(songs[1].meta, "normal", 1, 90000, true) && !PlayerPrefs.HasKey(key), "Unexpected song saved a level score.");
            VanillaStoryCampaign.Begin(id, "normal", songs);
            for (int i = 0; i < songs.Count; i++)
                Require(VanillaStoryCampaign.CompleteSong(songs[i].meta, "normal", 1, 100 * (i + 1), true) == (i < songs.Count - 1), "Campaign completion advanced incorrectly.");
            Require(VanillaStoryCampaign.HighScore(id, "normal") == 600 && VanillaStoryCampaign.HasBeaten(id) && VanillaStoryCampaign.ReturnToStory, "Completed campaign total or return flag failed.");
            VanillaStoryCampaign.Begin(id, "normal", songs);
            foreach (var song in songs) VanillaStoryCampaign.CompleteSong(song.meta, "normal", 1, 1, true);
            Require(VanillaStoryCampaign.HighScore(id, "normal") == 600 && VanillaStoryCampaign.HighScore(id, "hard") == 0, "Worse score or other difficulty overwrote the best score.");
        }
        finally
        {
            VanillaStoryCampaign.ReturnToMenu();
            Song.currentSongMeta = oldSong;
            Song.difficulty = oldDifficulty;
            Song.modeOfPlay = oldMode;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        Debug.Log("STORY CAMPAIGN PASSED: ordered advancement, total, best score, difficulty isolation, abort, autoplay, and unexpected-song controls.");
    }

    private static void CheckSprite()
    {
        var host = new GameObject("Story Sprite Probe", typeof(RectTransform));
        try
        {
            var sprite = host.AddComponent<VanillaStorySprite>();
            sprite.Load("storymenu/props/bf", "confirm0");
            Require(sprite.FrameSize == new Vector2(327, 331) && sprite.FrameName == "confirm0001", "Trimmed confirmation frame changed.");
            using (var mesh = new VertexHelper())
            {
                typeof(VanillaStorySprite).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(sprite, new object[] { mesh });
                var vertices = new List<UIVertex>();
                mesh.GetUIVertexStream(vertices);
                Require(vertices[0].position == Vector3.zero && vertices[1].position.x == 311 && vertices[2].position.y == -330, "Rotated frame dimensions were not restored.");
                Require(vertices[0].uv0.x == vertices[1].uv0.x && vertices[0].uv0.y > vertices[1].uv0.y, "Rotated frame UV orientation failed.");
            }
            sprite.Load("storymenu/props/gf", "idle0", new[] { 30, 0, 1 });
            Require(sprite.FrameCount == 2 && sprite.FrameName == "idle0030", "Indexed dance frames did not match suffixes or skip missing frame zero.");
            string first = sprite.FrameName;
            sprite.Tick(1f / 24 + 0.001f);
            Require(sprite.FrameName != first, "Indexed animation did not advance.");
            sprite.Tick(5);
            string final = sprite.FrameName;
            sprite.Tick(5);
            Require(sprite.FrameName == final && sprite.Finished, "Non-looping animation did not hold its final frame.");
        }
        finally { Object.DestroyImmediate(host); }
        Debug.Log("STORY SPRITE PASSED: rotated atlas geometry and UVs, indexed dance, animation advance, and terminal frame hold.");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static void Next() { phase++; changed = EditorApplication.timeSinceStartup; }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || finishing) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - started > 180) { Finish(false); return; }
        double wait = EditorApplication.timeSinceStartup - changed;
        try
        {
            switch (phase)
            {
                case 0:
                    menu = MenuV2.Instance;
                    if (menu == null || wait < 3) return;
                    menu.vanillaMenu.MoveSelection(-menu.vanillaMenu.SelectedIndex);
                    menu.vanillaMenu.ConfirmSelection();
                    Next();
                    break;
                case 1:
                    story = VanillaStoryMenu.Active;
                    if (story == null || wait < 4) return;
                    Require(!menu.mainScreen.gameObject.activeSelf && !menu.playScreen.gameObject.activeSelf, "Story Mode left an old menu active.");
                    Require(story.SelectedLevel.id == "tutorial" && story.Difficulty == "normal" && story.DisplayedScore == VanillaStoryCampaign.HighScore("tutorial", "normal"), "Initial selection, difficulty, or score interpolation failed.");
                    Require(story.TrackText == "TRACKS\n\nTutorial" && story.Props.Count(prop => prop.sprite.gameObject.activeSelf) == 2, "Tutorial props or track list changed.");
                    Capture("story-tutorial.png", 1280, 720, false);
                    Capture("story-wide.png", 1920, 1080, false);
                    Capture("story-ultrawide.png", 1920, 800, false);
                    Capture("story-blank-control.png", 1280, 720, true);
                    firstFrame = story.Props[0].sprite.FrameName;
                    Next();
                    break;
                case 2:
                    sawDance |= firstFrame != story.Props[0].sprite.FrameName;
                    if (wait < 2) return;
                    Require(sawDance, "Beat-driven dance never advanced.");
                    story.ChangeDifficulty(1);
                    Require(story.Difficulty == "hard", "Right did not choose Hard.");
                    story.ChangeDifficulty(1);
                    Require(story.Difficulty == "easy", "Right did not wrap difficulty.");
                    story.ChangeDifficulty(-1);
                    Require(story.Difficulty == "hard", "Left did not wrap difficulty.");
                    story.ChangeLevel(-1);
                    Require(story.SelectedLevel.id == "sserafim", "Up did not wrap to final level.");
                    story.ChangeLevel(1);
                    Require(story.SelectedLevel.id == "tutorial", "Down did not wrap to Tutorial.");
                    story.ChangeLevel(1);
                    story.ChangeDifficulty(-1);
                    page = 1;
                    Next();
                    break;
                case 3:
                    if (wait < 0.8f) return;
                    Capture("story-" + story.SelectedLevel.id + ".png", 1280, 720, false);
                    if (story.SelectedLevel.id == "week1")
                    {
                        for (int i = 0; i < story.Props.Count; i++)
                        {
                            typeof(VanillaStoryProp).GetMethod("Play", BindingFlags.Instance | BindingFlags.NonPublic)
                                .Invoke(story.Props[i], new object[] { i == 2 ? "danceLeft" : "idle" });
                            story.Props[i].sprite.paused = true;
                        }
                        Capture("story-reference-week1.png", 1280, 720, false);
                        foreach (var prop in story.Props) prop.sprite.paused = false;
                    }
                    if (++page < story.Levels.Count)
                    {
                        story.ChangeLevel(1);
                        story.ChangeDifficulty(0);
                        changed = EditorApplication.timeSinceStartup;
                        return;
                    }
                    story.ChangeLevel(-7);
                    Require(story.SelectedLevel.id == "week2", "Missing-week control selected the wrong level.");
                    story.Confirm();
                    Require(!story.Busy && story.Status.StartsWith("Songs not installed:"), "Missing songs launched an incomplete week.");
                    story.ChangeLevel(-1);
                    story.ChangeDifficulty(1);
                    Require(story.SelectedLevel.id == "week1" && story.Difficulty == "hard", "Week 1 selection failed.");
                    story.Close();
                    Next();
                    break;
                case 4:
                    if (wait < 0.5) return;
                    Require(VanillaStoryMenu.Active == null && menu.mainScreen.gameObject.activeSelf && menu.vanillaMenu.SelectedIndex == 0, "Cancel did not restore main menu selection.");
                    menu.OpenStoryMode();
                    story = VanillaStoryMenu.Active;
                    Require(story.SelectedLevel.id == "week1" && story.Difficulty == "hard", "Reentry lost level or difficulty.");
                    weekScoreKey = VanillaStoryCampaign.ScoreKey("week1", "hard");
                    previousScore = PlayerPrefs.GetInt(weekScoreKey, int.MinValue);
                    story.Confirm();
                    story.ChangeLevel(1);
                    story.ChangeDifficulty(1);
                    story.Confirm();
                    story.Close();
                    Require(story.Busy && story.SelectedLevel.id == "week1" && story.Difficulty == "hard", "Confirmation input lock failed.");
                    Require(story.Props[1].Animation == "confirm", "Player confirmation animation did not start.");
                    Next();
                    break;
                case 5:
                    if (!capturedConfirm && wait > 0.15 && wait < 0.95 && story != null)
                    {
                        Capture("story-confirm.png", 1280, 720, false);
                        capturedConfirm = true;
                    }
                    if (SceneManager.GetActiveScene().name != "Game_Backup3" || Song.instance == null || !Song.instance.songStarted) return;
                    Require(capturedConfirm && VanillaStoryCampaign.Running && Song.currentSongMeta.songName == "Bopeebo" && Song.difficulty == "Hard" && Song.modeOfPlay == 1, "Story launch lost the playlist, song, difficulty, or mode.");
                    songScoreKey = Song.currentSongMeta.songName + Song.currentSongMeta.bundleMeta.bundleName + "hard1";
                    previousSongScore = PlayerPrefs.GetInt(songScoreKey, int.MinValue);
                    previousRank = PlayerPrefs.GetInt("Freeplay.Rank." + songScoreKey, int.MinValue);
                    previousClear = PlayerPrefs.GetFloat("Freeplay.Clear." + songScoreKey, float.MinValue);
                    Require(Song.instance.musicClip != null, "Gameplay did not load audio.");
                    Next();
                    break;
                case 6:
                    if (wait < 1) return;
                    Song.instance.playerOneStats.currentScore = 135;
                    Song.instance.musicClip = AudioClip.Create("Story end-of-track fixture", 441, 1, 44100, false);
                    foreach (AudioSource source in Song.instance.musicSources) source.Stop();
                    Song.instance.vocalSource.Stop();
                    Next();
                    break;
                case 7:
                    if (SceneManager.GetActiveScene().name != "Game_Backup3" || Song.currentSongMeta.songName != "Fresh"
                        || Song.instance == null || !Song.instance.songStarted || wait < 3) return;
                    Require(VanillaStoryCampaign.Running && VanillaStoryCampaign.SongIndex == 1 && VanillaStoryCampaign.Score == 135,
                        "Gameplay completion did not advance the real campaign to Fresh.");
                    Require(PlayerPrefs.GetInt(weekScoreKey, int.MinValue) == previousScore, "Incomplete week saved a level score.");
                    Pause.instance.QuitSong();
                    Next();
                    break;
                case 8:
                    if (SceneManager.GetActiveScene().name != "Title" || VanillaStoryMenu.Active == null || wait < 4) return;
                    story = VanillaStoryMenu.Active;
                    menu = MenuV2.Instance;
                    Require(story.SelectedLevel.id == "week1" && story.Difficulty == "hard" && !VanillaStoryCampaign.Running, "Gameplay return lost Story Mode selection.");
                    Require(PlayerPrefs.GetInt(weekScoreKey, int.MinValue) == previousScore, "Early quit saved campaign progress.");
                    Capture("story-return.png", 1280, 720, false);
                    story.Close();
                    Next();
                    break;
                case 9:
                    if (wait < 0.5) return;
                    menu.OpenFreeplay(true);
                    Require(VanillaFreeplay.Active != null && VanillaFreeplay.Active.SongCount >= 4 && !VanillaFreeplay.Active.Busy, "Story Mode broke Freeplay access.");
                    Finish(errors == 0);
                    break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Capture(string filename, int width, int height, bool blank)
    {
        Canvas canvas = story.GetComponent<Canvas>();
        CanvasScaler scaler = story.GetComponent<CanvasScaler>();
        Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
        int[] layers = transforms.Select(t => t.gameObject.layer).ToArray();
        var host = new GameObject("Story Capture", typeof(Camera));
        Camera camera = host.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(width, height, 24);
        var features = AssetDatabase.FindAssets("t:UniversalRendererData").Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        try
        {
            foreach (Transform child in transforms) child.gameObject.layer = 31;
            foreach (var feature in features) feature.SetActive(false);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1000;
            scaler.enabled = false;
            canvas.scaleFactor = Mathf.Min(width / 1280f, height / 720f);
            story.Viewport.gameObject.SetActive(!blank);
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            float light = image.GetPixels().Average(color => color.r + color.g + color.b);
            Require(blank ? light < 0.01f : light > 0.3f, "Story render or blank control failed: " + light);
            if (!blank)
            {
                float scale = Mathf.Min(width / 1280f, height / 720f);
                Color background = image.GetPixel((int)((width - 1280 * scale) / 2 + 20 * scale), (int)((height + 720 * scale) / 2 - 100 * scale));
                Require(background.maxColorComponent > 0.1f, "Level background was not rendered.");
            }
            File.WriteAllBytes(Path.Combine(Output, filename), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            story.Viewport.gameObject.SetActive(true);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            scaler.enabled = true;
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            foreach (var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed)
    {
        if (finishing) return;
        finishing = true;
        if (songScoreKey != null)
        {
            if (previousSongScore == int.MinValue) PlayerPrefs.DeleteKey(songScoreKey);
            else PlayerPrefs.SetInt(songScoreKey, previousSongScore);
            if (previousRank == int.MinValue) PlayerPrefs.DeleteKey("Freeplay.Rank." + songScoreKey);
            else PlayerPrefs.SetInt("Freeplay.Rank." + songScoreKey, previousRank);
            if (previousClear == float.MinValue) PlayerPrefs.DeleteKey("Freeplay.Clear." + songScoreKey);
            else PlayerPrefs.SetFloat("Freeplay.Clear." + songScoreKey, previousClear);
            PlayerPrefs.Save();
        }
        SessionState.SetBool("VanillaStoryValidation.Active", false);
        string result = "STORY VALIDATION: passed=" + passed + ", errors=" + errors + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        Debug.Log(result);
        if (passed && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNITY_PARTY_BUILD_PATH")))
        {
            try { BuildAutomation.BuildWindows(); }
            catch (Exception exception) { Debug.LogException(exception); passed = false; }
        }
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
