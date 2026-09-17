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
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaMenuValidation
{
    private static double beginAt;
    private static double started;
    private static double changedAt;
    private static int phase;
    private static int errors;
    private static MenuV2 menu;
    private static VanillaMainMenu main;
    private static bool finishing;
    private static bool sawMagenta;
    private static bool sawHiddenSelection;
    private static bool sawVisibleSelection;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_MENU_TEST_PATH");

    static VanillaMenuValidation()
    {
        if (SessionState.GetBool("VanillaMenuValidation.Active", false))
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived += OnLog;
        }
    }

    public static void Begin()
    {
        Directory.CreateDirectory(Output);
        string bundle = Path.Combine(Output, "Bundles", "LocalBundle");
        string song = Path.Combine(bundle, "TestSong");
        Directory.CreateDirectory(song);
        Directory.CreateDirectory(Path.Combine(Output, "Bundles", "EmptyControl"));
        File.WriteAllText(Path.Combine(bundle, "bundle-meta.json"), JsonConvert.SerializeObject(new BundleMeta
        {
            bundleName = "Local bundle check",
            authorName = "Validation"
        }));
        File.WriteAllText(Path.Combine(song, "meta.json"), JsonConvert.SerializeObject(new SongMetaV2
        {
            songName = "Local song check",
            songDescription = "Local bundle validation.",
            credits = new Dictionary<string, string> { { "Composer", "Validation" } },
            difficulties = new Dictionary<string, Color> { { "Normal", Color.white } }
        }));
        File.Copy("Songs/Test/Inst.ogg", Path.Combine(song, "Inst.ogg"), true);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        beginAt = EditorApplication.timeSinceStartup + 5;
        EditorApplication.update += BeginWhenReady;
    }

    private static void BeginWhenReady()
    {
        if (EditorApplication.timeSinceStartup < beginAt)
            return;
        EditorApplication.update -= BeginWhenReady;
        SessionState.SetBool("VanillaMenuValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors++;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Next()
    {
        phase++;
        changedAt = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying)
            return;
        if (started == 0)
            started = changedAt = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - started;
        double sinceChange = EditorApplication.timeSinceStartup - changedAt;
        try
        {
            Require(elapsed < 90, "Menu validation timed out.");
            switch (phase)
            {
                case 0:
                    if (sinceChange < 7) return;
                    menu = Object.FindFirstObjectByType<MenuV2>();
                    main = menu.vanillaMenu;
                    Require(main != null && main.isActiveAndEnabled && main.items.Length == 5 && main.SelectedIndex == 0, "Five-entry menu did not initialize.");
                    Require(menu.musicSource.isPlaying && menu.menuClip.name == "freakyMenu", "Vanilla menu music did not play.");
                    Require(main.items.All(i => i.selected.Length == 3 && i.idle.Length == 9), "Atlas animation frames were lost.");
                    typeof(MenuV2).GetField("_songsFolder", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, Path.Combine(Output, "Bundles"));
                    Capture("menu-story.png", false, 1280, 720);
                    Capture("menu-control.png", true, 1280, 720);
                    Capture("menu-wide.png", false, 1600, 720);
                    main.MoveSelection(-1);
                    Require(main.SelectedIndex == 4, "Up did not wrap to Credits.");
                    Next();
                    break;
                case 1:
                    if (sinceChange < 3) return;
                    Require(Mathf.Abs(main.CameraScroll - 320.5f) < 1, "Camera did not settle on Credits.");
                    Capture("menu-credits-selected.png", false, 1280, 720);
                    main.MoveSelection(1);
                    Require(main.SelectedIndex == 0, "Down did not wrap to Story Mode.");
                    main.MoveSelection(3);
                    main.ConfirmSelection();
                    main.MoveSelection(1);
                    main.ConfirmSelection();
                    Require(main.Busy && main.SelectedIndex == 3, "Confirm did not lock navigation.");
                    Next();
                    break;
                case 2:
                    if (main.isActiveAndEnabled)
                    {
                        sawMagenta |= main.magenta.enabled;
                        sawHiddenSelection |= !main.items[3].image.enabled;
                        sawVisibleSelection |= main.items[3].image.enabled;
                    }
                    if (sinceChange < 2) return;
                    Require(sawMagenta && sawHiddenSelection && sawVisibleSelection, "Confirmation flicker did not render both phases.");
                    Require(menu.optionsScreen.gameObject.activeInHierarchy && !main.gameObject.activeSelf, "Options destination failed.");
                    menu.OptionsScreenTransition(false);
                    Require(main.SelectedIndex == 3 && !main.Busy && !main.magenta.enabled, "Returning from Options lost selection or input state.");
                    main.MoveSelection(1);
                    main.ConfirmSelection();
                    Next();
                    break;
                case 3:
                    if (sinceChange < 2) return;
                    Require(main.creditsPanel.activeInHierarchy && main.creditsScroll.content.rect.height > 510, "Credits content did not open.");
                    Capture("credits.png", false, 1280, 720);
                    main.CloseCredits();
                    Require(!main.creditsPanel.activeSelf && main.SelectedIndex == 4, "Credits did not close.");
                    main.MoveSelection(1);
                    main.ConfirmSelection();
                    Next();
                    break;
                case 4:
                    if (sinceChange < 2) return;
                    Require(VanillaStoryMenu.Active != null && !menu.playScreen.gameObject.activeInHierarchy, "Story Mode did not open the level menu.");
                    VanillaStoryMenu.Active.Close();
                    Require(main.SelectedIndex == 0, "Story Mode return lost selection.");
                    main.MoveSelection(1);
                    main.flashingLights = false;
                    main.ConfirmSelection();
                    Next();
                    break;
                case 5:
                    if (main.isActiveAndEnabled)
                        Require(!main.magenta.enabled && main.items[1].image.enabled, "Disabled flashing control still flickered.");
                    if (sinceChange < 3) return;
                    Require(VanillaFreeplay.Active != null && !VanillaFreeplay.Active.Busy, "Freeplay did not open.");
                    VanillaFreeplay.Active.Close();
                    Next();
                    break;
                case 6:
                    if (VanillaFreeplay.Active != null || sinceChange < 0.6f) return;
                    menu.ReloadSongList();
                    menu.OpenPlayScreenFromMenu();
                    CheckBundles();
                    var bundle = menu.songListRect.GetComponentsInChildren<BundleButtonV2>(true).Single(b => b.Name == "Local bundle check");
                    bundle.ToggleSongsVisibility();
                    bundle.SongButtons[0].GetComponent<Button>().onClick.Invoke();
                    Next();
                    break;
                case 7:
                    if (sinceChange < 5) return;
                    Require(menu.songInfoScreen.activeInHierarchy && menu.canChangeSongs && menu.musicSource.isPlaying && menu.musicSource.clip != menu.menuClip, "Song preview did not load after menu re-entry.");
                    menu.OpenMenuFromPlayScreen();
                    Require(main.SelectedIndex == 1 && !main.Busy && menu.musicSource.clip == menu.menuClip, "Freeplay return did not restore menu music and input.");
                    main.flashingLights = true;
                    Debug.Log("MENU CHECKS PASSED: atlas frames, wraparound, camera, confirm lock/flicker, Options, Credits, Story Mode/Freeplay bridges, repeated bundles, audio preview, and return state.");
                    Next();
                    menu.QuitGame();
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(false);
        }
    }

    private static void CheckBundles()
    {
        var bundles = menu.songListRect.GetComponentsInChildren<BundleButtonV2>(true);
        Require(bundles.Length == 3 && bundles.Count(b => b.Name == "Local bundle check" && b.SongButtons.Count == 1) == 1,
            "Bundle reload lost a built-in bundle, duplicated items, or included the empty-directory control.");
        var cache = (System.Collections.IDictionary)typeof(MenuV2).GetField("bundles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
        Require(cache.Count == bundles.Length, "Bundle reload kept stale cached buttons.");
    }

    private static void Capture(string filename, bool blankControl, int width, int height)
    {
        GameObject copy = Object.Instantiate(main.gameObject);
        Object.DestroyImmediate(copy.GetComponent<VanillaMainMenu>());
        foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = 31;
        for (int i = 0; i < main.items.Length; i++)
        {
            var image = copy.transform.Find("Viewport/" + main.items[i].name).GetComponent<RawImage>();
            image.uvRect = main.items[i].image.uvRect;
            image.rectTransform.sizeDelta = main.items[i].image.rectTransform.sizeDelta;
            image.rectTransform.anchoredPosition = main.items[i].image.rectTransform.anchoredPosition;
            image.color = main.items[i].image.color;
            image.enabled = main.items[i].image.enabled;
        }
        copy.transform.Find("Viewport/Credits").gameObject.SetActive(main.creditsPanel.activeSelf);
        copy.transform.Find("Viewport/Background").GetComponent<RectTransform>().anchoredPosition = main.background.anchoredPosition;
        copy.transform.Find("Viewport").gameObject.SetActive(!blankControl);
        GameObject cameraObject = new GameObject("Menu Capture", typeof(Camera));
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
        camera.targetTexture = target;
        Canvas canvas = copy.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1000;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(f => f != null && f.isActive).Distinct().ToArray();
        foreach (var feature in features) feature.SetActive(false);
        try
        {
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = Mathf.Min(width / 1280f, height / 720f);
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            Color[] pixels = image.GetPixels();
            float light = pixels.Average(c => c.r + c.g + c.b);
            Require(blankControl ? light < 0.01f : light > 0.25f, "Menu render or blank control failed: " + filename + " brightness=" + light);
            File.WriteAllBytes(Path.Combine(Output, filename), image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Debug.Log("MENU RENDER: " + filename + " brightness=" + light);
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += () => Finish(phase == 8 && errors == 0);
    }

    private static void Finish(bool succeeded)
    {
        if (finishing) return;
        finishing = true;
        SessionState.SetBool("VanillaMenuValidation.Active", false);
        Debug.Log("MENU VALIDATION FINISHED: passed=" + succeeded + ", errors=" + errors + ", phase=" + phase);
        if (succeeded)
        {
            try { BuildAutomation.BuildWindows(); }
            catch (Exception exception) { Debug.LogException(exception); succeeded = false; }
        }
        EditorApplication.Exit(succeeded ? 0 : 1);
    }
}
