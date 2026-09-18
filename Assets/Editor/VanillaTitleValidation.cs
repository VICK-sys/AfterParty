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
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaTitleValidation
{
    private static int phase;
    private static int errors;
    private static int assertions;
    private static double started;
    private static double changed;
    private static MenuV2 menu;
    private static VanillaTitleScreen title;
    private static bool finishing;
    private static readonly HashSet<int> Beats = new HashSet<int>();
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_TITLE_TEST_PATH") ?? Path.GetFullPath("Builds/TitleValidation");

    static VanillaTitleValidation()
    {
        if (!SessionState.GetBool("VanillaTitleValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run title validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        CheckDiamondShader();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaTitleValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static void Set(string name, object value) => typeof(VanillaTitleScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(title, value);
    private static void Call(string name, params object[] values) => typeof(VanillaTitleScreen).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(title, values);
    private static void Next() { phase++; changed = EditorApplication.timeSinceStartup; }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || finishing) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        double wait = EditorApplication.timeSinceStartup - changed;
        try
        {
            if (EditorApplication.timeSinceStartup - started >= 110) throw new InvalidOperationException("Title validation timed out.");
            switch (phase)
            {
                case 0:
                    title = VanillaTitleScreen.Active;
                    if (title == null) return;
                    menu = MenuV2.Instance;
                    Require(!menu.mainScreen.gameObject.activeSelf && !menu.playScreen.gameObject.activeSelf, "Title left another menu active.");
                    title.Accept();
                    Require(!title.IntroSkipped && !title.Transitioning && !menu.musicSource.isPlaying, "Input or music bypassed the one-second startup delay.");
                    Next();
                    break;
                case 1:
                    int beat = title.CurrentBeat;
                    if (!Beats.Add(beat)) return;
                    if (beat == 1) Require(title.CreditText == "THE\nFUNKIN CREW INC", "Beat 1 credits differ.");
                    if (beat == 3) Require(title.CreditText == "THE\nFUNKIN CREW INC\nPRESENTS", "Beat 3 credits differ.");
                    if (beat == 4 || beat == 8 || beat == 12) Require(title.CreditText == "", "Credits did not clear on the scheduled beat.");
                    if (beat == 5) Require(title.CreditText == "IN ASSOCIATION\nWITH", "Beat 5 credits differ.");
                    if (beat == 7) Require(title.CreditText.EndsWith("NEWGROUNDS") && title.GetComponentsInChildren<RawImage>().Any(image => image.name == "Newgrounds" && image.enabled), "Newgrounds did not appear on beat 7.");
                    if (beat == 9) Require(title.CreditText.Length > 0 && !title.CreditText.Contains('\n'), "Random intro first line did not appear.");
                    if (beat == 11) Require(title.CreditText.Split('\n').Length == 2, "Random intro second line did not appear.");
                    if (beat == 13) Require(title.CreditText == "FRIDAY", "Friday cue changed.");
                    if (beat == 15) Require(title.CreditText.StartsWith("FRIDAY\n") && title.CreditText.EndsWith("\nFUNKIN"), "Title credits changed.");
                    if (beat < 16) return;
                    Require(title.IntroSkipped && !title.Transitioning && menu.musicSource.isPlaying, "Intro did not reveal the title at beat 16.");
                    Require(Beats.Contains(1) && Beats.Contains(7) && Beats.Contains(15), "Live beat observation missed required controls.");
                    Require(Mathf.Abs(menu.musicSource.volume - OptionsV2.menuVolume) < 0.02f, "Four-second music fade did not reach its target.");
                    Next();
                    break;
                case 2:
                    if (wait < 1.2) return;
                    title.enabled = false;
                    title.Logo.Load("logoBumpin", "logo bumpin", null, "VanillaTitle");
                    title.Girlfriend.Load("gfDanceTitle", "gfDance", Enumerable.Range(15, 15).ToArray(), "VanillaTitle");
                    title.Prompt.SetFrame(0);
                    Capture("title.png", 1280, 720, false);
                    Capture("title-wide.png", 1920, 800, false);
                    Capture("title-tall.png", 720, 960, false);
                    Capture("blank-control.png", 1280, 720, true);
                    title.ShiftHue(0.25f);
                    Capture("title-hue.png", 1280, 720, false);
                    title.ShiftHue(-0.25f);
                    Call("ClearText");
                    Call("AddText", "The");
                    Call("AddText", "Funkin Crew Inc");
                    title.Viewport.Find("Intro Credits").gameObject.SetActive(true);
                    Capture("credits.png", 1280, 720, false);
                    title.Viewport.Find("Intro Credits").gameObject.SetActive(false);
                    title.Accept();
                    Require(title.Transitioning && title.Prompt.CurrentLabel == "Confirm" && !menu.mainScreen.gameObject.activeSelf, "First title confirmation skipped its transition.");
                    title.Viewport.Find("White Flash").GetComponent<Image>().color = Color.clear;
                    Capture("confirm.png", 1280, 720, false);
                    title.enabled = true;
                    Next();
                    break;
                case 3:
                    if (wait < 1) { Require(!menu.mainScreen.gameObject.activeSelf, "Main menu opened before the two-second confirmation."); return; }
                    if (VanillaTitleScreen.Active != null || VanillaTitleTransition.BlocksInput) return;
                    Require(menu.mainScreen.gameObject.activeSelf && VanillaTitleScreen.EnteredMainMenu, "Timed title confirmation did not open the main menu.");
                    Require(menu.musicSource.isPlaying && menu.musicSource.time > 10, "Title wipe restarted menu music.");
                    CheckReturnWipe();
                    Next();
                    break;
                case 4:
                    if (VanillaTitleTransition.BlocksInput) return;
                    title = VanillaTitleScreen.Active;
                    Require(title.IntroSkipped && !title.Transitioning, "Returning to title replayed the credits.");
                    int[] invalid = { 0, 1, 2, 0, 1, 2, 3 };
                    foreach (int direction in invalid) title.CodePress(direction);
                    Require(!title.CheatActive && menu.musicSource.clip == menu.menuClip, "Invalid cheat activated the ringtone.");
                    title.CodePress(3);
                    foreach (int direction in new[] { 0, 1, 0, 1, 2, 3, 2, 3 }) title.CodePress(direction);
                    Require(title.CheatActive && menu.musicSource.clip.name == "girlfriendsRingtone" && menu.musicSource.isPlaying, "Ringtone cheat failed.");
                    Next();
                    break;
                case 5:
                    if (wait < 1.2) return;
                    Require(title.Hue >= 0.125f, "Ringtone beats did not cycle hue.");
                    Set("age", 40f);
                    title.Tick(0);
                    Require(!title.AttractPlaying, "Ringtone failed to suppress the attract timer.");
                    title.Accept();
                    title.Accept();
                    Require(VanillaTitleTransition.IsRunning && !menu.mainScreen.gameObject.activeSelf, "Repeated Enter bypassed the diamond wipe.");
                    VanillaTitleTransition current = VanillaTitleTransition.Active;
                    title.Accept();
                    title.MoveToMainMenu();
                    Require(VanillaTitleTransition.Active == current, "Repeated Enter restarted the wipe.");
                    Next();
                    break;
                case 6:
                    if (VanillaTitleTransition.BlocksInput) return;
                    Require(menu.mainScreen.gameObject.activeSelf && menu.musicSource.clip == menu.menuClip, "Repeated Enter or ringtone exit failed.");
                    menu.vanillaMenu.ReturnToTitle();
                    Next();
                    break;
                case 7:
                    if (VanillaTitleTransition.BlocksInput) return;
                    title = VanillaTitleScreen.Active;
                    Set("age", 37.4f);
                    title.Tick(0);
                    Require(!title.AttractPlaying, "Attract mode began before 37.5 seconds.");
                    Set("age", 37.5f);
                    title.Tick(1);
                    Require(!title.AttractPlaying, "Attract mode skipped its two-second fade.");
                    title.Tick(1.01f);
                    Require(title.AttractPlaying && !menu.musicSource.isPlaying, "Attract mode did not start or left menu music playing.");
                    Next();
                    break;
                case 8:
                    VideoPlayer video = title.GetComponentInChildren<VideoPlayer>();
                    if (video == null || !video.isPlaying || video.frame < 2) return;
                    Require(video.url.EndsWith("riftCollabTrailer.mp4"), "Attract video order changed.");
                    Capture("attract.png", 1280, 720, false);
                    title.EndAttract();
                    Require(!title.AttractPlaying && title.IntroSkipped && menu.musicSource.isPlaying && !title.Transitioning, "Attract return did not restore title and music.");
                    title.Accept();
                    title.Accept();
                    Next();
                    break;
                case 9:
                    if (VanillaTitleTransition.BlocksInput) return;
                    menu.OpenStoryMode();
                    Require(VanillaStoryMenu.Active != null && !menu.mainScreen.gameObject.activeSelf, "Title integration broke Story Mode access.");
                    VanillaStoryMenu.Active.Close();
                    Next();
                    break;
                case 10:
                    if (wait < 0.5) return;
                    menu.OpenFreeplay(true);
                    Require(VanillaFreeplay.Active != null && !VanillaFreeplay.Active.Busy, "Title integration broke Freeplay access.");
                    Finish(errors == 0);
                    break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void CheckReturnWipe()
    {
        int selection = menu.vanillaMenu.SelectedIndex;
        float position = menu.musicSource.time;
        menu.vanillaMenu.ReturnToTitle();
        VanillaTitleTransition wipe = VanillaTitleTransition.Active;
        Require(wipe != null && !wipe.Revealing && wipe.Progress == 0, "Back did not start a fresh cover pass.");
        wipe.enabled = false;
        menu.vanillaMenu.MoveSelection(1);
        menu.vanillaMenu.ConfirmSelection();
        menu.vanillaMenu.ReturnToTitle();
        Require(menu.vanillaMenu.SelectedIndex == selection && !menu.vanillaMenu.Busy
            && VanillaTitleTransition.Active == wipe, "Menu input leaked through the wipe.");
        wipe.Tick(0.25f);
        Require(!wipe.Revealing && VanillaTitleScreen.Active == null && menu.mainScreen.gameObject.activeSelf,
            "Back switched screens before full coverage.");
        Capture("wipe-menu-cover.png", 1280, 720, false, wipe);
        wipe.Tick(0.25f);
        title = VanillaTitleScreen.Active;
        Require(wipe.Revealing && wipe.Progress == 0 && title != null && !menu.mainScreen.gameObject.activeSelf,
            "Back did not switch at full coverage.");
        Require(title.Viewport.Find("White Flash").GetComponent<Image>().color.a == 0, "Return flash obscured the diamond reveal.");
        title.Accept();
        Require(!title.Transitioning, "Incoming title accepted input during its reveal.");
        wipe.Tick(0.25f);
        Require(VanillaTitleTransition.IsRunning && wipe.Revealing, "Reveal ended before its half-second duration.");
        Capture("wipe-title-reveal.png", 1280, 720, false, wipe);
        wipe.Tick(0.25f);
        Require(!VanillaTitleTransition.IsRunning && VanillaTitleTransition.BlocksInput, "Wipe cleanup or final-frame input lock failed.");
        Require(menu.musicSource.isPlaying && menu.musicSource.time >= position, "Back wipe restarted music.");
    }

    private static void CheckDiamondShader()
    {
        Shader shader = Resources.Load<Shader>("VanillaTitle/DiamondWipe");
        Require(shader != null && shader.isSupported, "Diamond shader is missing or unsupported.");
        var material = new Material(shader);
        RenderTexture previous = RenderTexture.active;
        try
        {
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 800) })
            {
                var target = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32);
                var image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                try
                {
                    material.SetVector("_Resolution", new Vector4(size.x, size.y, 0, 0));
                    foreach (bool reveal in new[] { false, true })
                    {
                        material.SetFloat("_Revealing", reveal ? 1 : 0);
                        foreach (float progress in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                        {
                            RenderTexture.active = target;
                            GL.Clear(false, true, Color.white);
                            material.SetFloat("_Progress", progress);
                            Graphics.Blit(Texture2D.whiteTexture, target, material);
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                            image.Apply();
                            RequireDiamondPixels(image, progress, reveal);
                            if (progress == 0.5f)
                                File.WriteAllBytes(Path.Combine(Output, "diamonds-" + size.x + (reveal ? "-reveal.png" : "-cover.png")), image.EncodeToPNG());
                        }
                    }
                }
                finally { Object.DestroyImmediate(image); Object.DestroyImmediate(target); }
            }
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(material); }
        Debug.Log("DIAMOND SHADER PASSED: source formula, 10-pixel cells, cover/reveal direction, complete endpoints, and widescreen output.");
    }

    private static void RequireDiamondPixels(Texture2D image, float progress, bool reveal)
    {
        Color32[] pixels = image.GetPixels32();
        int mismatches = 0;
        for (int y = 0; y < image.height; y += 3)
        for (int x = 0; x < image.width; x += 3)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float threshold = Mathf.Abs(Mathf.Repeat(px / 10, 1) - 0.5f) + Mathf.Abs(Mathf.Repeat(py / 10, 1) - 0.5f)
                + px / image.width + py / image.height;
            if (Mathf.Abs(threshold - progress * 3) < 0.0001f) continue;
            bool covered = progress >= 1 || progress > 0 && threshold <= progress * 3;
            bool black = reveal ? !covered : covered;
            Color32 pixel = pixels[y * image.width + x];
            if (black ? pixel.r > 1 || pixel.g > 1 || pixel.b > 1 : pixel.r < 254 || pixel.g < 254 || pixel.b < 254) mismatches++;
        }
        Require(mismatches == 0, "Diamond shader differs from the supplied formula: " + image.width + "x" + image.height
            + ", progress=" + progress + ", reveal=" + reveal + ", mismatches=" + mismatches);
    }

    private static void Capture(string filename, int width, int height, bool blank, VanillaTitleTransition wipe = null)
    {
        Canvas canvas = wipe == null ? title.GetComponent<Canvas>() : wipe.GetComponent<Canvas>();
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
        int[] layers = transforms.Select(t => t.gameObject.layer).ToArray();
        var host = new GameObject("Title Capture", typeof(Camera));
        Camera camera = host.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = wipe == null ? Color.black : Color.white;
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
            if (scaler != null) scaler.enabled = false;
            canvas.scaleFactor = wipe == null ? Mathf.Min(width / 1280f, height / 720f) : 1;
            if (wipe == null) title.Viewport.gameObject.SetActive(!blank);
            Canvas.ForceUpdateCanvases();
            if (wipe == null) title.ApplyLayout(width / canvas.scaleFactor);
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            if (wipe != null) RequireDiamondPixels(image, wipe.Progress, wipe.Revealing);
            else
            {
                float light = image.GetPixels().Average(color => color.r + color.g + color.b);
                Require(blank ? light < 0.001f : light > 0.005f, "Title render or blank control failed: " + light);
            }
            File.WriteAllBytes(Path.Combine(Output, filename), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            if (wipe == null) title.Viewport.gameObject.SetActive(true);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            if (scaler != null) scaler.enabled = true;
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
        SessionState.SetBool("VanillaTitleValidation.Active", false);
        string result = "TITLE VALIDATION: passed=" + passed + ", errors=" + errors + ", assertions=" + assertions + ", phase=" + phase;
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
