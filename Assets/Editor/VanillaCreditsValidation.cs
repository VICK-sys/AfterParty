using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaCreditsValidation
{
    private static int phase;
    private static int assertions;
    private static int errors;
    private static double started;
    private static double changed;
    private static bool finishing;
    private static bool sawEntryCover;
    private static MenuV2 menu;
    private static VanillaCreditsScreen credits;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_CREDITS_TEST_PATH") ?? Path.GetFullPath("Builds/CreditsValidation");
    private static string Reference => Environment.GetEnvironmentVariable("UNITY_PARTY_CREDITS_REFERENCE_PATH") ?? Path.GetFullPath("Builds/CreditsReference");

    static VanillaCreditsValidation()
    {
        if (!SessionState.GetBool("VanillaCreditsValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run credits validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaCreditsValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Next() { phase++; changed = EditorApplication.timeSinceStartup; }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || finishing) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        double wait = EditorApplication.timeSinceStartup - changed;
        try
        {
            if (EditorApplication.timeSinceStartup - started >= 100) throw new InvalidOperationException("Credits validation timed out.");
            switch (phase)
            {
                case 0:
                    if (wait < 2 || VanillaTitleScreen.Active == null) return;
                    menu = MenuV2.Instance;
                    VanillaTitleScreen.Active.Accept();
                    VanillaTitleScreen.Active.Accept();
                    VanillaTitleScreen.Active.Accept();
                    Next();
                    break;
                case 1:
                    if (VanillaTitleScreen.Active != null || VanillaTitleTransition.BlocksInput) return;
                    Require(menu.mainScreen.gameObject.activeInHierarchy, "Main menu did not open.");
                    menu.vanillaMenu.MoveSelection(4 - menu.vanillaMenu.SelectedIndex);
                    menu.vanillaMenu.ConfirmSelection();
                    Next();
                    break;
                case 2:
                    sawEntryCover |= VanillaCreditsTransition.Active != null && !VanillaCreditsTransition.Active.Revealing;
                    credits = VanillaCreditsScreen.Active;
                    if (credits == null) return;
                    Require(!menu.mainScreen.gameObject.activeSelf && !menu.vanillaMenu.creditsPanel.activeSelf, "Credits left a previous menu visible.");
                    Require(menu.musicSource.isPlaying && menu.musicSource.clip.name == "freeplayRandom" && menu.musicSource.loop, "Credits music did not start.");
                    Require(credits.GroupY <= 720 && credits.GroupY > 650, "Credits did not enter from below the screen: " + credits.GroupY);
                    Require(sawEntryCover && VanillaCreditsTransition.IsRunning && VanillaCreditsTransition.Active.Revealing,
                        "Credits entry did not cover the main menu before revealing credits.");
                    Next();
                    break;
                case 3:
                    if (credits.MusicAge < 3) return;
                    Require(Mathf.Abs(menu.musicSource.volume - OptionsV2.menuVolume * 0.8f * credits.MusicAge / 6) < 0.005f,
                        "Credits music did not fade linearly over six seconds.");
                    Require(menu.musicSource.time > 2, "Credits audio playback did not advance.");
                    Require(Math.Abs(credits.GroupY - (720 - credits.ScrollAge * 100)) < 0.1, "Live credits scroll speed differs from 100 pixels per second.");
                    Require(Math.Abs(credits.MusicAge - credits.ScrollAge - 1) < 0.1, "Credits did not wait for the one-second incoming fade.");
                    Next();
                    break;
                case 4:
                    if (credits.MusicAge < 6.1f) return;
                    Require(Mathf.Abs(menu.musicSource.volume - OptionsV2.menuVolume * 0.8f) < 0.001f, "Credits music missed its target volume.");
                    CheckFrames();
                    CheckFade();
                    CheckControls();
                    Next();
                    break;
                case 5:
                    if (wait < 0.2) return;
                    Require(menu.mainScreen.gameObject.activeInHierarchy && !menu.vanillaMenu.Busy && menu.vanillaMenu.SelectedIndex == 4,
                        "Returning from credits lost menu selection or input state.");
                    Require(menu.musicSource.clip == menu.menuClip && menu.musicSource.isPlaying && menu.musicSource.loop,
                        "Returning from credits did not restore menu music.");
                    menu.vanillaMenu.MoveSelection(1);
                    Require(menu.vanillaMenu.SelectedIndex == 0, "Credits return left navigation locked.");
                    Finish(errors == 0);
                    break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Restart(int width)
    {
        if (VanillaCreditsTransition.IsRunning) CompleteFade();
        if (VanillaCreditsScreen.Active != null) VanillaCreditsScreen.Active.Close(false);
        credits = VanillaCreditsScreen.Open(menu);
        credits.enabled = false;
        credits.ApplyLayout(width);
    }

    private static void Step(int frames, bool fast = false, bool pause = false)
    {
        for (int i = 0; i < frames; i++) credits.Tick(1.0 / 60, fast, pause);
    }

    private static void CheckFrames()
    {
        Restart(1280);
        Require(credits.TotalLineCount == 207, "Credit lines were lost.");
        Capture("blank-control", 1280, true);
        Step(300);
        Compare("opening-1280", 1280);
        Step(1);
        Compare("fractional-1280", 1280);
        Step(899);
        Compare("middle-1280", 1280);
        Step(2400);
        Compare("later-1280", 1280);
        Restart(1600);
        Step(1200);
        Compare("middle-1600", 1600);
        Step(2400);
        Compare("later-1600", 1600);
        Step(2040);
        Compare("party-1600", 1600);
        Restart(1440);
        Step(1200);
        Compare("middle-1440", 1440);
        Step(2400);
        Compare("later-1440", 1440);
        Restart(1280);
        Step(5640);
        Compare("party-1280", 1280);
        Require(credits.VisibleLines.Any(line => line.data.text == "Rei the Goat"), "Unity Party developer credit is missing.");
        Require(credits.VisibleLines.Any(line => line.data.text == "UniBrine"), "Unity Party designer credit is missing.");
        Require(credits.VisibleLines.Any(line => line.data.text == "St4bility aka Thesnakerox"), "Unity Party composer and advisor credit is missing.");
        Step(1000);
        Require(VanillaCreditsTransition.IsRunning && VanillaCreditsScreen.Active == credits, "Finished credits did not start the outgoing fade.");
        CompleteFade();
        Require(VanillaCreditsScreen.Active == null && menu.mainScreen.gameObject.activeSelf, "Finished credits did not return automatically.");
        Require(credits.BuiltLineCount == credits.TotalLineCount, "Credits returned before the final line.");
    }

    private static void Compare(string name, int width)
    {
        JObject expected = JObject.Parse(File.ReadAllText(Path.Combine(Reference, name + ".json")));
        Require(Math.Abs(credits.GroupY - (double)expected["groupY"]) < 0.001, "Credits scroll differs from source: " + name);
        Require(Math.Abs(credits.NextY - (double)expected["nextY"]) < 0.001, "Credits spacing differs from source: " + name);
        JArray visible = (JArray)expected["visible"];
        Require(credits.VisibleLines.Count == visible.Count, "Credits recycling differs from source: " + name);
        foreach (JObject line in visible)
        {
            var match = credits.VisibleLines.FirstOrDefault(item => item.data.text == (string)line["text"] && Math.Abs(item.y - (double)line["y"]) < 0.001);
            Require(match != null, "Visible source line was lost: " + name + ": " + line["text"]);
            Require(match.variant.numLines == (int)line["numLines"] && match.variant.fullHeight == (float)line["height"],
                "Source text wrapping differs: " + name + ": " + line["text"]);
        }
        Capture(name, width, false);
    }

    private static void CheckControls()
    {
        Restart(1280);
        Step(1, false, true);
        Require(credits.GroupY == 720 && credits.BuiltLineCount == 0, "Pause built or moved credits before entry.");
        Step(60);
        Require(Math.Abs(credits.GroupY - 620) < 0.001, "Normal scroll speed differs.");
        Step(60, true, true);
        Require(Math.Abs(credits.GroupY - 220) < 0.001, "Fast scroll must use 400 pixels per second and take precedence over pause.");
        Step(60, false, true);
        Require(Math.Abs(credits.GroupY - 220) < 0.001, "Pause did not hold position.");
        Step(60);
        Require(Math.Abs(credits.GroupY - 120) < 0.001, "Scroll did not resume after pause.");
        credits.ApplyLayout(1600);
        Require(credits.Viewport.rect.width == 1600 && credits.GroupY > 119, "Widescreen resize reset the credits scroll.");
        credits.Tick(0, false, false, true);
        Require(VanillaCreditsTransition.IsRunning && VanillaCreditsScreen.Active == credits, "Back did not start the credits fade.");
        CompleteFade();
        Require(VanillaCreditsScreen.Active == null, "Back did not close credits after covering the screen.");
        Restart(1280);
        Require(credits.GroupY == 720 && credits.BuiltLineCount == 0 && credits.MusicAge == 0, "Reopening credits retained stale state.");
        credits.Close(false);
    }

    private static void CompleteFade()
    {
        var fade = VanillaCreditsTransition.Active;
        fade.enabled = false;
        if (!fade.Revealing) fade.Tick(VanillaCreditsTransition.CoverDuration);
        fade.Tick(VanillaCreditsTransition.RevealDuration);
    }

    private static void CheckFade()
    {
        Restart(1280);
        int swaps = 0;
        Require(VanillaCreditsTransition.Begin(() => swaps++), "Credits fade could not start.");
        var fade = VanillaCreditsTransition.Active;
        fade.enabled = false;
        Require(!VanillaCreditsTransition.Begin(() => swaps++), "Credits fade accepted a duplicate transition.");
        credits.Tick(0.3);
        Require(credits.GroupY == 720, "Credits scrolled during the outgoing fade.");
        fade.Tick(0.35f);
        Require(swaps == 0 && !fade.Revealing && fade.Progress == 0.5f, "Credits screen changed before the cover completed.");
        Capture("fade-cover-1280", 1280, false, fade);
        fade.Tick(0.35f);
        Require(swaps == 1 && fade.Revealing && fade.Progress == 0, "Credits did not swap under the black frame.");
        credits.Tick(0.5);
        Require(credits.GroupY == 720, "Credits scrolled during the incoming fade.");
        fade.Tick(0.5f);
        Capture("fade-reveal-1280", 1280, false, fade);
        fade.Tick(0.5f);
        Require(!VanillaCreditsTransition.IsRunning && VanillaCreditsTransition.BlocksInput && swaps == 1, "Credits fade did not finish safely.");
    }

    private static void Capture(string name, int width, bool blank, VanillaCreditsTransition fade = null)
    {
        Canvas canvas = fade == null ? credits.GetComponent<Canvas>() : fade.GetComponent<Canvas>();
        RectTransform viewport = fade == null ? credits.Viewport : fade.Viewport;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
        int[] layers = transforms.Select(t => t.gameObject.layer).ToArray();
        var host = new GameObject("Credits Capture", typeof(Camera));
        Camera camera = host.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = fade == null ? Color.black : Color.white;
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(width, 720, 24);
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
            canvas.scaleFactor = 1;
            viewport.gameObject.SetActive(!blank);
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, 720, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, 720), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            float light = image.GetPixels().Average(color => color.r + color.g + color.b);
            Require(blank ? light < 0.001 : light > 0.002, "Credits render or blank control failed: " + name);
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            viewport.gameObject.SetActive(true);
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
        SessionState.SetBool("VanillaCreditsValidation.Active", false);
        string result = "CREDITS VALIDATION: passed=" + passed + ", errors=" + errors + ", assertions=" + assertions + ", phase=" + phase;
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
