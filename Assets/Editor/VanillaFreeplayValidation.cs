using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaFreeplayValidation
{
    private static int phase;
    private static int errors;
    private static int editorWarnings;
    private static double started;
    private static double changed;
    private static bool finishing;
    private static VanillaFreeplay freeplay;
    private static MenuV2 menu;
    private static string songPath;
    private static string rankKey;
    private static int previousRank;
    private static string favoriteKey;
    private static int favoriteValue;
    private static bool sawEntry;
    private static bool sawEntryMotion;
    private static bool sawExitMotion;
    private static bool sawConfirmHold;
    private static VanillaFreeplaySprite confirmedIcon;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_FREEPLAY_TEST_PATH") ?? Path.GetFullPath("Validation/Freeplay");

    static VanillaFreeplayValidation()
    {
        if (!SessionState.GetBool("VanillaFreeplayValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run Freeplay validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        CheckCatalog();
        CheckScores();
        CheckIcons();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaFreeplayValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal)
            && stack.Contains("UnityEditor.Search.SearchDatabase") && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
        {
            editorWarnings++;
            return;
        }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckCatalog()
    {
        string source = Path.Combine(Application.streamingAssetsPath, "Bundles", "00-Tutorial", "01-Tutorial");
        string root = Path.Combine(Output, "Catalog");
        string bundle = Path.Combine(root, "Bundle");
        string song = Path.Combine(bundle, "Song");
        Directory.CreateDirectory(song);
        File.WriteAllText(Path.Combine(bundle, "bundle-meta.json"), JsonConvert.SerializeObject(new BundleMeta { bundleName = "Freeplay fixture", authorName = "Validation" }));
        File.WriteAllText(Path.Combine(song, "meta.json"), JsonConvert.SerializeObject(new SongMetaV2
        {
            songName = "A fixture", credits = new Dictionary<string, string>(),
            difficulties = new Dictionary<string, Color> { { "Normal", Color.white }, { "Missing", Color.white } }
        }));
        File.Copy(Path.Combine(source, "Inst.ogg"), Path.Combine(song, "Inst.ogg"), true);
        File.Copy(Path.Combine(source, "Chart-normal.json"), Path.Combine(song, "Chart-normal.json"), true);
        Directory.CreateDirectory(Path.Combine(bundle, "Broken"));
        File.WriteAllText(Path.Combine(bundle, "Broken", "meta.json"), "{}");
        var found = VanillaFreeplayCatalog.Discover(root, root);
        Require(found.Count == 1 && found[0].Difficulty("normal") == "Normal" && found[0].Difficulty("Missing") == null,
            "Catalog did not reject missing chart control or duplicate root.");
        Require(VanillaFreeplayCatalog.Matches(found[0], "A-B") && !VanillaFreeplayCatalog.Matches(found[0], "C-D"), "Letter filtering failed.");
        Debug.Log("FREEPLAY CATALOG PASSED: custom bundle, case matching, missing chart, invalid metadata, duplicate root, letter control.");
    }

    private static void CheckScores()
    {
        var meta = new SongMetaV2 { songName = "Freeplay validation " + Guid.NewGuid(), bundleMeta = new BundleMeta { bundleName = "Validation" } };
        string key = meta.songName + meta.bundleMeta.bundleName + "normal1";
        var stats = new PlayerStat { totalNoteHits = 100, totalSicks = 75, totalGoods = 15, missedHits = 5 };
        try
        {
            VanillaFreeplayCatalog.SaveCompletion(meta, "Normal", 1, stats, false, 100);
            Require(!PlayerPrefs.HasKey("Freeplay.Rank." + key), "Aborted run saved a rank.");
            VanillaFreeplayCatalog.SaveCompletion(meta, "Normal", 4, stats, true, 100);
            Require(!PlayerPrefs.HasKey("Freeplay.Rank." + key), "Autoplay saved a player rank.");
            VanillaFreeplayCatalog.SaveCompletion(meta, "Normal", 1, stats, true, 100);
            Require(Mathf.Abs(PlayerPrefs.GetFloat("Freeplay.Clear." + key) - 0.85f) < 0.0001f && PlayerPrefs.GetInt("Freeplay.Rank." + key) == 2,
                "Funkin clear formula or Great threshold failed.");
            PlayerPrefs.DeleteKey("Freeplay.Rank." + key);
            PlayerPrefs.DeleteKey("Freeplay.Clear." + key);
            stats.totalNoteHits = stats.totalSicks = 50;
            stats.totalGoods = stats.missedHits = 0;
            VanillaFreeplayCatalog.SaveCompletion(meta, "Normal", 1, stats, true, 100);
            Require(PlayerPrefs.GetInt("Freeplay.Rank." + key) == 0 && PlayerPrefs.GetFloat("Freeplay.Clear." + key) == 0, "Skipped heads produced a false clear.");
            stats.totalNoteHits = 100;
            stats.totalSicks = 100; stats.totalGoods = stats.missedHits = 0;
            VanillaFreeplayCatalog.SaveCompletion(meta, "Normal", 1, stats, true, 100);
            Require(PlayerPrefs.GetInt("Freeplay.Rank." + key) == 5, "All-Sick gold rank failed.");
            stats.totalSicks = 0;
            VanillaFreeplayCatalog.SaveCompletion(meta, "Normal", 1, stats, true, 100);
            Require(PlayerPrefs.GetInt("Freeplay.Rank." + key) == 5 && PlayerPrefs.GetFloat("Freeplay.Clear." + key) == 1, "Worse run replaced best rank.");
        }
        finally
        {
            PlayerPrefs.DeleteKey("Freeplay.Rank." + key);
            PlayerPrefs.DeleteKey("Freeplay.Clear." + key);
            PlayerPrefs.Save();
        }
        Debug.Log("FREEPLAY SCORE PASSED: clear formula, thresholds, best rank, aborted and autoplay controls.");
    }

    private static void Next() { phase++; changed = EditorApplication.timeSinceStartup; }

    private static void CheckIcons()
    {
        var probe = new GameObject("Icon Probe", typeof(RectTransform)).AddComponent<VanillaFreeplaySprite>();
        probe.fps = 10;
        try
        {
            string[] icons = { "bf", "dad", "darnell", "gf", "mom", "monster", "parents-christmas", "pico", "senpai", "spirit", "spooky", "sserafim-kazuha", "tankman" };
            foreach (string icon in icons)
            {
                probe.Load("freeplay/icons/" + icon + "pixel", "idle0");
                string idle = probe.CurrentFrameName;
                probe.Tick(1);
                Require(probe.CurrentFrameName == idle, "Idle icon changed without confirmation: " + icon);
                Require(probe.TryPlay("confirm0", false, "confirm-hold0") && !probe.loop, "Confirm animation missing: " + icon);
                string first = probe.CurrentFrameName;
                int count = probe.FrameCount;
                probe.Tick(0.11f);
                Require(probe.CurrentFrameName != first && probe.CurrentFrameName.StartsWith("confirm0", StringComparison.Ordinal), "Confirm icon did not advance: " + icon);
                probe.Tick(count / 10f);
                Require(probe.loop && probe.CurrentFrameName.StartsWith("confirm-hold0", StringComparison.Ordinal), "Confirm icon did not hold: " + icon);
                string hold = probe.CurrentFrameName;
                probe.Tick(2);
                Require(probe.CurrentFrameName == hold && !probe.TryPlay("missing-animation") && probe.CurrentFrameName == hold, "Missing animation changed icon: " + icon);
            }
            probe.Load("freeplay/pinkBack");
            Require(!probe.TryPlay("confirm0", false, "confirm-hold0"), "Static texture incorrectly started confirm.");
        }
        finally { Object.DestroyImmediate(probe.gameObject); }
        Debug.Log("FREEPLAY ICONS PASSED: 13 confirm sequences, hold poses, idle and missing-animation controls.");
    }

    private static float ReadTime(string name) => (float)typeof(VanillaFreeplay).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freeplay);

    private static RectTransform FindRect(string name) => freeplay.GetComponentsInChildren<RectTransform>(true).First(t => t.name == name);

    private static void CheckEntry()
    {
        freeplay = VanillaFreeplay.Active;
        if (freeplay == null) return;
        float age = ReadTime("age");
        if (!sawEntry)
        {
            Require(age < 17f / 24 && freeplay.Busy && menu.mainScreen.gameObject.activeSelf, "Entry hid its parent or unlocked input before the intro.");
            Require(freeplay.transform.Find("Letterbox").GetComponent<Image>() == null, "Opaque matte still hides the parent menu.");
            int selected = menu.vanillaMenu.SelectedIndex;
            menu.vanillaMenu.MoveSelection(1);
            menu.vanillaMenu.ConfirmSelection();
            freeplay.MoveSelection(1);
            freeplay.Close();
            Require(menu.vanillaMenu.SelectedIndex == selected && VanillaFreeplay.Active == freeplay && freeplay.SelectedIndex == 1, "Entry input lock failed.");
            sawEntry = true;
        }
        if (!sawEntryMotion && age >= 0.09f && age < 0.6f)
        {
            RectTransform card = FindRect("Card");
            RectTransform rows = FindRect("Capsules");
            var body = rows.GetChild(1).Find("Capsule").GetComponent<VanillaFreeplaySprite>();
            Require(card.anchoredPosition.x > -524 && card.anchoredPosition.x < 0 && body.rectTransform.parent.GetComponent<RectTransform>().anchoredPosition.x < 1280,
                "Entry tween did not advance: age=" + age + ", card=" + card.anchoredPosition + ", capsule=" + body.rectTransform.parent.GetComponent<RectTransform>().anchoredPosition);
            Debug.Log("FREEPLAY ENTRY SAMPLE: age=" + age + ", stretch=" + body.stretch);
            Require(rows.GetComponentsInParent<CanvasGroup>().All(g => g.alpha > 0), "Capsule entry remains hidden by chrome.");
            Capture("freeplay-enter.png", 1280, 720, false);
            sawEntryMotion = true;
        }
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        double wait = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 150, "Freeplay probe timed out at phase " + phase);
            switch (phase)
            {
                case 0:
                    if (wait < 4) return;
                    menu = Object.FindFirstObjectByType<MenuV2>();
                    Require(menu?.vanillaMenu != null, "Main menu missing.");
                    menu.vanillaMenu.MoveSelection(1 - menu.vanillaMenu.SelectedIndex);
                    menu.vanillaMenu.ConfirmSelection();
                    Next();
                    break;
                case 1:
                    CheckEntry();
                    if (wait < 4) return;
                    Require(sawEntry && sawEntryMotion, "Entry animation was not observed.");
                    freeplay = VanillaFreeplay.Active;
                    Require(freeplay != null && !freeplay.Busy && freeplay.SongCount >= 4 && !menu.mainScreen.gameObject.activeSelf && !menu.playScreen.gameObject.activeSelf,
                        "Freeplay entry did not replace bundle picker.");
                    Require(freeplay.Difficulty == "Normal" && freeplay.SelectedSong.meta.songName == "Tutorial", "Initial selection differs from vanilla.");
                    Button[] modes = freeplay.GetComponentsInChildren<Button>(true)
                        .Where(button => button.name.StartsWith("Select ", StringComparison.Ordinal)).ToArray();
                    Require(modes.Select(button => button.name).SequenceEqual(new[] { "Select BOYFRIEND", "Select OPPONENT", "Select AUTOPLAY" }),
                        "Freeplay must offer exactly the three single-player modes.");
                    modes[2].onClick.Invoke();
                    Require(freeplay.Mode == PlayModes.Autoplay, "Third mode must keep the Autoplay score ID.");
                    modes[1].onClick.Invoke();
                    Require(freeplay.Mode == PlayModes.Opponent, "Opponent selection failed.");
                    freeplay.SetMode(3);
                    Require(freeplay.Mode == PlayModes.Boyfriend, "Retired mode must fall back to Boyfriend.");
                    var rankProbe = new GameObject("Rank Probe", typeof(RectTransform)).AddComponent<VanillaFreeplaySprite>();
                    foreach (string prefix in new[] { "LOSS rank", "GOOD rank", "GREAT rank", "EXCELLENT rank", "PERFECT rank0", "PERFECT rank GOLD" })
                    {
                        rankProbe.Load("freeplay/rankbadges", prefix, false);
                        Require(rankProbe.FrameCount > 0, "Rank animation missing: " + prefix);
                    }
                    Object.DestroyImmediate(rankProbe.gameObject);
                    Button pointerButton = freeplay.GetComponentsInChildren<Button>().First(b => b.name == "Select");
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(pointerButton.gameObject);
                    pointerButton.onClick.Invoke();
                    Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null, "Mouse selection left a keyboard submit target.");
                    freeplay.MoveSelection(1 - freeplay.SelectedIndex);
                    Capture("freeplay-normal.png", 1280, 720, false);
                    Capture("freeplay-blank-control.png", 1280, 720, true);
                    Capture("freeplay-wide.png", 1600, 720, false);
                    freeplay.MoveSelection(-freeplay.SelectedIndex);
                    Require(freeplay.SelectedSong == null, "Random selection missing.");
                    freeplay.MoveSelection(-1);
                    Require(freeplay.SelectedIndex == freeplay.VisibleSongCount, "Selection did not wrap.");
                    freeplay.MoveSelection(2 - freeplay.SelectedIndex);
                    freeplay.MoveSelection(1);
                    freeplay.MoveSelection(-1);
                    freeplay.MoveSelection(1);
                    songPath = freeplay.SelectedSong.meta.songPath;
                    Next();
                    break;
                case 2:
                    if (wait < 3) return;
                    Require(freeplay.PreviewPath == Path.Combine(songPath, "Inst.ogg") && freeplay.PreviewSource.isPlaying, "Rapid selection loaded stale preview.");
                    Require(UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(freeplay.PreviewSource.clip)
                        < (long)freeplay.PreviewSource.clip.samples * freeplay.PreviewSource.clip.channels * 2, "Preview retained the complete decoded instrumental.");
                    freeplay.ChangeDifficulty(2);
                    Require(freeplay.Difficulty == "Erect" && freeplay.VisibleSongCount == 15 && freeplay.SelectedSong.meta.songPath == songPath, "Erect filtering lost selection or kept a song without a remix.");
                    Next();
                    break;
                case 3:
                    if (wait < 3) return;
                    Require(freeplay.PreviewPath == Path.Combine(songPath, "Inst-erect.ogg") && freeplay.PreviewSource.isPlaying, "Erect preview played original instrumental.");
                    Require(UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(freeplay.PreviewSource.clip)
                        < (long)freeplay.PreviewSource.clip.samples * freeplay.PreviewSource.clip.channels * 2, "Erect preview retained the complete decoded instrumental.");
                    Capture("freeplay-erect.png", 1280, 720, false);
                    favoriteKey = freeplay.SelectedSong.FavoriteKey;
                    favoriteValue = PlayerPrefs.GetInt(favoriteKey, 0);
                    if (!freeplay.SelectedSong.Favorite) freeplay.ToggleFavorite();
                    freeplay.ChangeFilter(-1);
                    Require(freeplay.SelectedSong != null && freeplay.SelectedSong.Favorite, "Favorites filter lost current favorite.");
                    freeplay.ToggleFavorite();
                    Require(freeplay.SelectedSong == null || freeplay.SelectedSong.Favorite, "Unfavorited song remained in favorites.");
                    PlayerPrefs.SetInt(favoriteKey, favoriteValue);
                    PlayerPrefs.Save();
                    favoriteKey = null;
                    freeplay.ChangeFilter(1);
                    freeplay.MoveSelection(2 - freeplay.SelectedIndex);
                    songPath = freeplay.SelectedSong.meta.songPath;
                    rankKey = "Freeplay.Rank." + freeplay.SelectedSong.ScoreKey(freeplay.Difficulty, freeplay.Mode);
                    previousRank = PlayerPrefs.GetInt(rankKey, -1);
                    freeplay.ConfirmSelection();
                    Require(freeplay.Busy, "Confirm did not lock input.");
                    confirmedIcon = FindRect("Capsules").Cast<Transform>().Where(t => t.gameObject.activeSelf).ElementAt(freeplay.SelectedIndex)
                        .Find("Details/Icon").GetComponent<VanillaFreeplaySprite>();
                    Require(confirmedIcon.CurrentFrameName.StartsWith("confirm0", StringComparison.Ordinal), "Song confirm did not animate its associated icon.");
                    foreach (var icon in freeplay.GetComponentsInChildren<VanillaFreeplaySprite>().Where(s => s.name == "Icon" && s != confirmedIcon))
                        Require(icon.CurrentFrameName.StartsWith("idle0", StringComparison.Ordinal), "Song confirm animated another capsule's icon.");
                    Next();
                    break;
                case 4:
                    if (!sawConfirmHold && confirmedIcon != null && confirmedIcon.CurrentFrameName.StartsWith("confirm-hold0", StringComparison.Ordinal))
                    {
                        Capture("freeplay-confirm.png", 1280, 720, false);
                        sawConfirmHold = true;
                    }
                    if (SceneManager.GetActiveScene().name != "Game_Backup3" || Song.instance == null || !Song.instance.songStarted || wait < 8) return;
                    Require(sawConfirmHold, "Song icon did not reach its hold pose before gameplay.");
                    Require(Song.currentSongMeta.songPath == songPath && Song.difficulty == "Erect" && Song.modeOfPlay == 1, "Launch selected wrong song, difficulty, or mode.");
                    Pause.instance.QuitSong();
                    Require(Song.instance.FreeplayAborted, "Pause quit did not mark aborted run.");
                    Next();
                    break;
                case 5:
                    if (wait < 5 || SceneManager.GetActiveScene().name != "Title" || VanillaFreeplay.Active == null) return;
                    freeplay = VanillaFreeplay.Active;
                    menu = MenuV2.Instance;
                    Require(freeplay.SelectedSong.meta.songPath == songPath && freeplay.Difficulty == "Erect" && !freeplay.Busy, "Return lost Freeplay selection.");
                    Require(PlayerPrefs.GetInt(rankKey, -1) == previousRank, "Early quit changed saved rank.");
                    Capture("freeplay-return.png", 1280, 720, false);
                    freeplay.Close();
                    Require(freeplay.GetComponent<Canvas>().enabled && freeplay.Busy && menu.mainScreen.gameObject.activeSelf && menu.vanillaMenu.Busy,
                        "Close skipped the visible exit transition or unlocked the parent early.");
                    Next();
                    break;
                case 6:
                    if (freeplay != null && freeplay.enabled && !sawExitMotion && ReadTime("exitAge") >= 0.16f)
                    {
                        Require(ReadTime("exitAge") < 0.5f && FindRect("Card").anchoredPosition.x < -1 && FindRect("Dad Backdrop").anchoredPosition.x > 387.76f,
                            "Exit elements did not tween before teardown.");
                        Require(((RectTransform)FindRect("Capsules").GetChild(1)).anchoredPosition.x > 1000, "Capsule jump-out did not advance.");
                        Capture("freeplay-exit.png", 1280, 720, false);
                        sawExitMotion = true;
                    }
                    if (wait < 1) return;
                    Require(sawExitMotion && VanillaFreeplay.Active == null && (freeplay == null || !freeplay.enabled), "Exit animation did not finish once.");
                    Require(menu.mainScreen.gameObject.activeSelf && menu.vanillaMenu.SelectedIndex == 1 && menu.musicSource.clip == menu.menuClip && menu.musicSource.isPlaying, "Cancel did not restore menu state and music.");
                    Require(menu.musicSource.time > 0.1f && !menu.vanillaMenu.Busy, "Exit restarted music or kept menu input locked.");
                    menu.vanillaMenu.MoveSelection(-1);
                    menu.vanillaMenu.ConfirmSelection();
                    Next();
                    break;
                case 7:
                    if (wait < 3) return;
                    Require(VanillaStoryMenu.Active != null && !menu.playScreen.gameObject.activeInHierarchy, "Story Mode did not open after Freeplay exit.");
                    Finish(errors == 0);
                    break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Capture(string filename, int width, int height, bool blank, float minimumLight = 0.3f)
    {
        Canvas parentCanvas = menu.mainScreen.gameObject.activeSelf && !blank ? menu.mainScreen.GetComponent<Canvas>() : null;
        var canvases = parentCanvas == null ? new[] { freeplay.GetComponent<Canvas>() } : new[] { parentCanvas, freeplay.GetComponent<Canvas>() };
        var transforms = canvases.SelectMany(c => c.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
        int[] layers = transforms.Select(t => t.gameObject.layer).ToArray();
        Canvas canvas = freeplay.GetComponent<Canvas>();
        CanvasScaler scaler = freeplay.GetComponent<CanvasScaler>();
        GameObject cameraObject = new GameObject("Freeplay Capture", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        RenderTexture target = new RenderTexture(width, height, 24);
        var features = AssetDatabase.FindAssets("t:UniversalRendererData").Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(f => f != null && f.isActive).Distinct().ToArray();
        try
        {
            foreach (Transform transform in transforms) transform.gameObject.layer = 31;
            foreach (var feature in features) feature.SetActive(false);
            camera.targetTexture = target;
            foreach (Canvas surface in canvases)
            {
                surface.renderMode = RenderMode.ScreenSpaceCamera;
                surface.worldCamera = camera;
                surface.planeDistance = 1000;
                surface.GetComponent<CanvasScaler>().enabled = false;
                surface.scaleFactor = Mathf.Min(width / 1280f, height / 720f);
            }
            freeplay.Viewport.gameObject.SetActive(!blank);
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            float light = image.GetPixels().Average(c => c.r + c.g + c.b);
            Require(blank ? light < 0.01f : light > minimumLight, "Freeplay render or blank control failed: " + light);
            File.WriteAllBytes(Path.Combine(Output, filename), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            freeplay.Viewport.gameObject.SetActive(true);
            foreach (Canvas surface in canvases)
            {
                surface.renderMode = RenderMode.ScreenSpaceOverlay;
                surface.worldCamera = null;
                surface.GetComponent<CanvasScaler>().enabled = true;
            }
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            foreach (var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed)
    {
        if (finishing) return;
        finishing = true;
        if (favoriteKey != null) { PlayerPrefs.SetInt(favoriteKey, favoriteValue); PlayerPrefs.Save(); }
        SessionState.SetBool("VanillaFreeplayValidation.Active", false);
        string result = "FREEPLAY VALIDATION: passed=" + passed + ", errors=" + errors + ", editorSearchWarnings=" + editorWarnings + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        Debug.Log(result);
        if (passed && !string.IsNullOrWhiteSpace(BuildAutomation.OutputOverride))
        {
            try { BuildAutomation.BuildWindows(); }
            catch (Exception exception) { Debug.LogException(exception); passed = false; }
        }
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
