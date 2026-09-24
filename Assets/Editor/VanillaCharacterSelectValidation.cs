using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaCharacterSelectValidation
{
    private static int phase;
    private static int errors;
    private static double changed;
    private static VanillaCharacterSelect screen;
    private static VanillaFreeplayAnimate transitionDJ;
    private static AudioSource transitionSound;
    private static bool sawWipe;
    private static Vector2 transitionStart;
    private static float previewVolume;
    private static bool sawIntro;
    private static double previousFrameTime;
    private static int previousFrame;
    private static double picoEntryGap;
    private static VanillaFreeplay entryScreen;
    private static double finishedIntroTime;
    private static bool entryCompleted;
    private static bool idleAdvanced;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_CHARACTER_TEST_PATH") ?? Path.GetFullPath("Temp/CharacterSelect");
    static VanillaCharacterSelectValidation()
    {
        if (!SessionState.GetBool("VanillaCharacterSelectValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }
    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaCharacterSelectValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Next() { phase++; changed = EditorApplication.timeSinceStartup; }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (previousFrame != Time.frameCount)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if ((phase == 7 || phase == 8) && previousFrameTime > 0) picoEntryGap = Math.Max(picoEntryGap, (now - previousFrameTime) * 1000);
            previousFrameTime = now;
            previousFrame = Time.frameCount;
        }
        if (changed == 0) changed = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && elapsed < 45, "Character select errors or timeout at phase " + phase);
            if (Environment.GetEnvironmentVariable("UNITY_PARTY_CHARACTER_PARITY_TEST") == "1")
            {
                CheckParity(elapsed);
                return;
            }
            if (Environment.GetEnvironmentVariable("UNITY_PARTY_PICO_CONFIRM_TEST") == "1" || Environment.GetEnvironmentVariable("UNITY_PARTY_PICO_EXIT_TEST") == "1" || Environment.GetEnvironmentVariable("UNITY_PARTY_PICO_GUN_TEST") == "1")
            {
                CheckPicoConfirmation(elapsed);
                return;
            }
            if (phase == 1 || phase == 8 || phase == 12) CheckFreeplayEntry();
            switch (phase)
            {
                case 0:
                    if (elapsed < 7) return;
                    foreach (string difficulty in new[] { "easy", "normal", "hard" }) PlayerPrefs.DeleteKey("Story.Score.weekend1." + difficulty);
                    PlayerPrefs.SetString("Freeplay.Character", "bf");
                    PlayerPrefs.SetInt("CharacterSelect.SeenPico", 0);
                    PlayerPrefs.SetInt("CharacterSelect.SeenIntro", 0);
                    VanillaFreeplay.Open(Object.FindAnyObjectByType<MenuV2>(), false, Path.Combine(Output, "Empty"));
                    Next();
                    break;
                case 1:
                    if (elapsed < 3 || VanillaFreeplay.Active.Busy) return;
                    CheckBFCatalog();
                    BeginTransition();
                    Next();
                    break;
                case 2:
                case 9:
                    if (VanillaCharacterSelect.Active == null)
                    {
                        Require(VanillaFreeplay.Active.gameObject.activeSelf, "Freeplay was disabled before the transition completed.");
                        var wipe = Object.FindAnyObjectByType<VanillaFreeplayTransition>();
                        if (wipe != null && !sawWipe && wipe.Progress >= .7f && wipe.Progress < 1)
                        {
                            Require(transitionDJ.rectTransform.anchoredPosition.y > transitionStart.y + 5, "Character transition did not move the DJ upward.");
                            var gradient = (RectTransform)wipe.Overlay.transform.Find("Viewport/Gradient Wipe");
                            Require(gradient.anchoredPosition.y > -720 && gradient.anchoredPosition.y < 0, "Gradient is not sweeping into the screen.");
                            Require(wipe.Texture.IsCreated(), "Blue fade has no captured Freeplay image.");
                            Require(VanillaFreeplay.Active.PreviewSource.isPlaying && VanillaFreeplay.Active.PreviewSource.volume < previewVolume,
                                "Character transition stopped the music instead of fading it.");
                            Capture(wipe.Overlay, phase == 2 ? "bf-wipe.png" : "pico-wipe.png");
                            sawWipe = true;
                        }
                        return;
                    }
                    Require(sawWipe, "Character select skipped its gradient transition.");
                    Require(transitionDJ.Finished && !transitionSound.isPlaying, "Character select cut off the DJ animation or confirmation sound.");
                    screen = VanillaCharacterSelect.Active;
                    Next();
                    break;
                case 3:
                    var video = screen.GetComponent<UnityEngine.Video.VideoPlayer>();
                    if (video != null && video.isPlaying && video.frame > 0) sawIntro = true;
                    if (screen.Busy) return;
                    Require(sawIntro && PlayerPrefs.GetInt("CharacterSelect.SeenIntro") == 1, "Character select introduction did not decode.");
                    Require(screen.Character == "bf" && VanillaCharacterSelect.PicoUnlocked && !VanillaStoryCampaign.HasBeaten("weekend1"), "Pico is not available on a fresh save.");
                    CheckGrid();
                    Capture(screen.GetComponent<Canvas>(), "bf.png");
                    screen.SelectSlot(0);
                    Require(screen.Character == "locked", "Unavailable character control failed.");
                    Next();
                    break;
                case 4:
                    if (elapsed < 2) return;
                    var lockedPlayer = (VanillaFreeplayAnimate)typeof(VanillaCharacterSelect).GetField("player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(screen);
                    Require(lockedPlayer.CurrentLabel == "idle" && lockedPlayer.CompletedLoops > 0 && !lockedPlayer.Finished,
                        "Locked silhouette idle stopped or restarted on a beat instead of looping after entry.");
                    screen.Confirm();
                    Require(!screen.Confirming, "Locked confirmation control failed.");
                    Capture(screen.GetComponent<Canvas>(), "locked.png");
                    CheckGrid();
                    screen.SelectSlot(3);
                    Require(screen.Character == "pico" && !screen.Busy, "Pico requires an unlock animation or campaign completion.");
                    Next();
                    break;
                case 5:
                    if (elapsed < .75) return;
                    CheckGrid();
                    Capture(screen.GetComponent<Canvas>(), "pico.png");
                    screen.Confirm();
                    Require(screen.Confirming, "Pico confirmation did not start.");
                    Next();
                    break;
                case 6:
                    if (elapsed < .5) return;
                    screen.Back();
                    Require(!screen.Confirming, "Confirmation cancel failed.");
                    screen.SelectSlot(4);
                    Require(screen.Character == "bf", "Return to BF failed.");
                    screen.SelectSlot(3);
                    screen.Confirm();
                    Next();
                    break;
                case 7:
                    if (VanillaCharacterSelect.Active != null) return;
                    Require(VanillaCharacterSelect.SelectedCharacter == "pico", "Pico selection was not saved.");
                    Next();
                    break;
                case 8:
                    if (elapsed < 4 || VanillaFreeplay.Active.Busy) return;
                    Require(entryCompleted, "Pico entry did not switch from Intro to Idle.");
                    Require(idleAdvanced, "Pico stayed on one pose after the menu opened.");
                    Debug.Log("PICO ENTRY FRAME GAP MS: " + picoEntryGap);
                    var freeplay = VanillaFreeplay.Active;
                    Capture(freeplay.GetComponent<Canvas>(), "pico-freeplay.png");
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var songs = (System.Collections.Generic.List<VanillaFreeplaySong>)typeof(VanillaFreeplay).GetField("filtered", flags).GetValue(freeplay);
                    Require(songs.Count == 19 && songs.All(song => ((string)song.Details("Hard")["playData"]["characters"]["player"]).StartsWith("pico")) && !VanillaStoryCampaign.HasBeaten("weekend1"), "Fresh-save Pico Freeplay must include four Weekend songs and 15 mixes.");
                    BeginTransition();
                    Next();
                    break;
                case 10:
                    if (screen.Busy) return;
                    Require(screen.Character == "pico", "Pico return selected the wrong character.");
                    CheckGrid();
                    screen.SelectSlot(4);
                    screen.Confirm();
                    Next();
                    break;
                case 11:
                    if (VanillaCharacterSelect.Active != null) return;
                    Require(VanillaCharacterSelect.SelectedCharacter == "bf", "BF selection was not saved.");
                    Next();
                    break;
                case 12:
                    if (elapsed < 4 || VanillaFreeplay.Active.Busy) return;
                    CheckBFCatalog();
                    Finish(true);
                    break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }
    private static void CheckBFCatalog()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        foreach (string field in new[] { "songs", "filtered" })
        {
            var songs = (System.Collections.Generic.List<VanillaFreeplaySong>)typeof(VanillaFreeplay).GetField(field, flags).GetValue(VanillaFreeplay.Active);
            Require(songs.Any(song => song.week == "Week 1"), "BF catalog lost its Week 1 control songs.");
            Require(songs.Count == 25 && songs.Count(song => ((string)song.Details("Hard")["playData"]["characters"]["player"]).StartsWith("bf")) == 24
                && songs.Count(song => Path.GetFileName(song.meta.songPath) == "01-Spaghetti" && ((string)song.Details("Hard")["playData"]["characters"]["player"]).StartsWith("sserafim")) == 1,
                "BF catalog must include 22 originals, two BF mixes, and Spaghetti.");
        }
    }

    private static void CheckFreeplayEntry()
    {
        var freeplay = VanillaFreeplay.Active;
        if (entryScreen != freeplay)
        {
            entryScreen = freeplay;
            finishedIntroTime = 0;
            entryCompleted = false;
            idleAdvanced = false;
        }
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var dj = (VanillaFreeplayAnimate)typeof(VanillaFreeplay).GetField("dj", flags).GetValue(freeplay);
        double now = Time.realtimeSinceStartupAsDouble;
        if (dj.CurrentLabel == "Intro")
        {
            Require(freeplay.Busy, "Freeplay accepted input before its DJ intro completed.");
            if (!dj.Finished) return;
            if (finishedIntroTime == 0) finishedIntroTime = now;
            Require(now - finishedIntroTime < .1, "Freeplay holds the finished DJ intro instead of revealing the menu.");
        }
        else if (dj.CurrentLabel == "Idle")
        {
            Require(!freeplay.Busy, "The DJ idles while Freeplay remains locked.");
            if (freeplay.IsPico)
            {
                var blueBar = (VanillaFreeplaySprite)typeof(VanillaFreeplay).GetField("picoBlue", flags).GetValue(freeplay);
                Require(blueBar.gameObject.activeSelf, "Pico's backing card stayed hidden after the intro finished.");
            }
            idleAdvanced |= dj.LabelFrame > 0;
            if (!entryCompleted) Debug.Log("FREEPLAY INTRO HOLD MS: character=" + (freeplay.IsPico ? "pico" : "bf") + ", hold=" + (finishedIntroTime == 0 ? 0 : (now - finishedIntroTime) * 1000));
            entryCompleted = true;
        }
    }

    private static void BeginTransition()
    {
        var freeplay = VanillaFreeplay.Active;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        transitionDJ = (VanillaFreeplayAnimate)typeof(VanillaFreeplay).GetField("dj", flags).GetValue(freeplay);
        transitionSound = (AudioSource)typeof(VanillaFreeplay).GetField("effects", flags).GetValue(freeplay);
        transitionStart = transitionDJ.rectTransform.anchoredPosition;
        sawWipe = false;
        previewVolume = freeplay.PreviewSource.volume;
        freeplay.OpenCharacterSelect();
        Require(transitionDJ.CurrentLabel == "To Character Select" && !transitionDJ.Finished, "Tab did not start the DJ animation.");
        Require(transitionSound.isPlaying && VanillaCharacterSelect.Active == null, "Tab did not play its sound before changing screens.");
    }

    private static void CheckGrid()
    {
        var root = screen.transform.Find("Viewport/Content/Icons");
        var cursors = screen.transform.Find("Viewport/Content/Cursors").GetComponentsInChildren<VanillaFreeplaySprite>();
        var cursor = cursors.Where(item => item.name == "charSelector").Last();
        Vector2 cursorCenter = cursor.rectTransform.anchoredPosition + new Vector2(cursor.FrameSize.x / 2, -cursor.FrameSize.y / 2);
        Vector2 expected = new Vector2(screen.SelectedSlot % 3 * 110 + 64, -(screen.SelectedSlot / 3 * 110 + 64));
        Require(Vector2.Distance(cursorCenter, expected) < 2, "Hover cursor does not align with the selected cell.");
        foreach (var icon in root.GetComponentsInChildren<VanillaFreeplaySprite>().Where(item => item.name.StartsWith("Character Icon")))
        {
            int slot = int.Parse(icon.name.Split(' ').Last());
            Vector2 center = icon.rectTransform.anchoredPosition + new Vector2(icon.FrameSize.x * icon.drawScale / 2, -icon.FrameSize.y * icon.drawScale / 2);
            Require(Vector2.Distance(center, new Vector2(slot % 3 * 107 + 64, -(slot / 3 * 127 + 64))) < .01f, "Character icon is outside its cell.");
            Require(icon.GetComponent<Outline>().enabled == (slot == screen.SelectedSlot), "Selected icon outline is missing or applied to another icon.");
        }
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(instance);

    public static void RunRendering()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated batch editor.");
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderReferenceFrames();
        EditorApplication.Exit(0);
    }

    private static void RenderReferenceFrames()
    {
        var host = new GameObject("Character Reference", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(host.transform, false);
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
        background.color = new Color32(128,128,128,255);
        var graphic = new GameObject("Character", typeof(RectTransform)).AddComponent<VanillaFreeplayAnimate>();
        graphic.transform.SetParent(host.transform, false);
        graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = new Vector2(0,1);
        graphic.rectTransform.anchoredPosition = Vector2.zero;
        foreach (string name in new[] { "lockedChill", "bfChill", "gfChill", "picoChill", "neneChill" })
        {
            graphic.Initialize("charSelect/" + name, true);
            foreach (int frame in name == "lockedChill" ? new[] { 0,10,27,29,34,109 } : name == "gfChill" ? new[] { 0,15,30,54,55,56,60,80,105 } : name == "neneChill" ? new[] { 0,7,15,29,30,38,46,47,51 } : new[] { 0,15,17,22,28,30,35 })
            {
                graphic.SetFrame(frame);
                Capture(canvas, name + "-" + frame + ".png");
            }
        }
        graphic.SetFrame(38);
        graphic.SetLayerFrames("VIZ_bars", new[] { 12,12,12,12,12,12,12 });
        Capture(canvas, "neneChill-visualizer-control.png");
        graphic.SetLayerFrames("VIZ_bars", new[] { 0,0,0,0,0,0,0 });
        var root = Field<Newtonsoft.Json.Linq.JToken>(graphic, "root");
        var layer = root["TL"]["L"].First(item => (string)item["LN"] == "Nene");
        var rendered = layer["RB"];
        ((Newtonsoft.Json.Linq.JObject)layer).Remove("RB");
        graphic.SetLayerFrames("VIZ_bars", new[] { 0,0,0,0,0,0,0 });
        Capture(canvas, "neneChill-unfiltered-control.png");
        layer["RB"] = rendered;
        foreach (string name in new[] { "bfChill", "gfChill", "picoChill", "neneChill" })
        {
            graphic.Initialize("charSelect/" + name, true);
            int frame = name == "gfChill" ? 56 : name == "neneChill" ? 47 : 30;
            graphic.SetFrame(frame);
            var animation = Field<Newtonsoft.Json.Linq.JToken>(graphic, "root");
            var owner = name == "neneChill" ? animation["TL"]["L"].First(item => (string)item["LN"] == "Nene") : animation;
            var baked = owner["RB"];
            ((Newtonsoft.Json.Linq.JObject)owner).Remove("RB");
            graphic.SetLayerFrames("VIZ_bars", new[] { 0,0,0,0,0,0,0 });
            Capture(canvas, name + "-overlay-control.png");
            owner["RB"] = baked;
        }
        Object.DestroyImmediate(host);
    }

    private static void CheckParity(double elapsed)
    {
        if (phase == 0)
        {
            if (elapsed < 7) return;
            RenderReferenceFrames();
            PlayerPrefs.SetString("Freeplay.Character", "bf");
            screen = VanillaCharacterSelect.Open(_ => { }, true);
            Next();
            return;
        }
        if (phase == 1)
        {
            if (screen.Busy) return;
            CheckGrid();
            screen.enabled = false;
            var cursors = Field<VanillaFreeplaySprite[]>(screen, "cursors");
            Vector2 start = cursors[2].rectTransform.anchoredPosition;
            screen.Move(-1, 0);
            for (int frame = 0; frame < 6; frame++) screen.Tick(1f / 60);
            Require(Mathf.Abs(cursors[2].rectTransform.anchoredPosition.x - (start.x - 110)) < 1.2f, "Main cursor does not settle within the source 0.1-second interval.");
            Require(Mathf.Abs(cursors[0].rectTransform.anchoredPosition.x - (start.x - 110)) > 20, "Afterimage negative control lost its distinct delay.");
            for (int frame = 6; frame < 25; frame++) screen.Tick(1f / 60);
            Require(Mathf.Abs(cursors[0].rectTransform.anchoredPosition.x - (start.x - 110)) < 1.1f, "Dark cursor does not settle within 0.404 seconds.");
            screen.SelectSlot(0);
            screen.Confirm();
            Require(!screen.Confirming, "Locked character confirmation was accepted.");
            Field<VanillaFreeplayAnimate>(screen, "player").SetFrame(0);
            Field<VanillaFreeplayAnimate>(screen, "outgoing").gameObject.SetActive(false);
            screen.Tick(.5f);
            Capture(screen.GetComponent<Canvas>(), "locked-screen.png");
            screen.SelectSlot(4);
            screen.Tick(0);
            screen.Confirm();
            screen.Tick(.5f);
            var music = Field<AudioSource>(screen, "music");
            Require(Mathf.Abs(music.pitch - .55f) < .001f, "Confirmation pitch differs from quadInOut.");
            Require(Mathf.Abs(music.volume - OptionsV2.menuVolume * 7 / 9) < .001f, "Confirmation volume differs from quadInOut.");
            screen.Back();
            var icon = Field<System.Collections.Generic.Dictionary<int, VanillaFreeplaySprite>>(screen, "slotIcons")[4];
            Require(icon.FrameIndex == icon.FrameCount - 1 && icon.CurrentFrameName.StartsWith("confirm0"), "Cancellation did not reverse the icon.");
            int lastFrame = icon.FrameIndex;
            icon.Tick(.1f);
            Require(icon.FrameIndex < lastFrame, "Reverse confirmation is not advancing backwards.");
            screen.Tick(.5f);
            Require(Mathf.Abs(music.pitch - .775f) < .001f, "Cancellation pitch did not recover with quartInOut.");
            screen.Tick(.5f);
            icon.Tick(2);
            Require(!screen.Confirming && !screen.Busy && icon.CurrentFrameName.StartsWith("idle"), "Cancellation did not restore selection.");
            Require(Field<VanillaFreeplayAnimate>(screen, "player").CurrentLabel == "idle", "Cancellation did not restore the player idle after one second.");
            Capture(screen.GetComponent<Canvas>(), "bf-screen.png");
            screen.SelectSlot(3);
            screen.Tick(.5f);
            screen.Back();
            screen.Tick(.4f);
            var transition = Field<VanillaFreeplayTransition>(screen, "transition");
            Require(transition != null && transition.Texture.IsCreated(), "Exit has no captured scene.");
            Require(Mathf.Abs(Field<Material>(transition, "blue").GetFloat("_Fade") - .75f) < .001f, "Exit does not use the source blue fade curve.");
            Require(Field<CanvasGroup>(screen, "cursorAlpha").alpha < .04f, "Exit cursor did not fade independently.");
            Next();
            return;
        }
        if (phase == 2)
        {
            if (elapsed < .1) return;
            var transition = Field<VanillaFreeplayTransition>(screen, "transition");
            Capture(transition.Overlay, "exit-half.png");
            screen.Tick(.4f);
            Require(VanillaCharacterSelect.SelectedCharacter == "bf", "Back did not restore the remembered character.");
            Next();
            return;
        }
        if (phase == 3)
        {
            if (VanillaCharacterSelect.Active != null)
            {
                screen.Tick(.01f);
                return;
            }
            Require(Object.FindObjectsByType<VanillaFreeplayTransition>(FindObjectsSortMode.None).Length == 0, "Exit leaked its capture camera.");
            Debug.Log("CHARACTER PARITY PASSED: 29 reference frames, cursor timing and delay control, locked denial, confirm/cancel music, reverse icon, blue fade, cursor exit and remembered character.");
            Finish(true);
        }
    }

    private static void CheckPicoConfirmation(double elapsed)
    {
        if (phase == 0)
        {
            if (elapsed < 7) return;
            PlayerPrefs.SetString("Freeplay.Character", "pico");
            VanillaFreeplay.Open(Object.FindAnyObjectByType<MenuV2>(), false, Path.Combine(Output, "Empty"));
            Next();
            return;
        }
        var freeplay = Object.FindAnyObjectByType<VanillaFreeplay>();
        if (freeplay == null || freeplay.Busy) return;
        Require(freeplay.IsPico, "Pico confirmation probe opened the wrong character.");
        if (Environment.GetEnvironmentVariable("UNITY_PARTY_PICO_GUN_TEST") == "1")
        {
            CheckPicoGunAtlas(freeplay);
            Finish(true);
            return;
        }
        if (Environment.GetEnvironmentVariable("UNITY_PARTY_PICO_EXIT_TEST") == "1")
        {
            var decorations = freeplay.GetComponentsInChildren<VanillaFreeplaySprite>(true).Where(sprite => sprite.name.StartsWith("Pico ")).ToArray();
            Require(decorations.Length > 10 && decorations.All(sprite => sprite.gameObject.activeSelf), "Pico exit control did not start with visible gun graphics.");
            freeplay.Close();
            Require(decorations.All(sprite => !sprite.gameObject.activeSelf), "Pico gun graphics remained visible after closing Freeplay.");
            var exit = typeof(VanillaFreeplay).GetMethod("DrawExit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            exit.Invoke(freeplay, new object[] { .2f });
            Require(decorations.All(sprite => !sprite.gameObject.activeSelf), "Pico graphics reappeared during the exit tween.");
            Capture(freeplay.GetComponent<Canvas>(), "pico-exit.png");
            exit.Invoke(freeplay, new object[] { .31f });
            Require(VanillaFreeplay.Active == null && !freeplay.GetComponent<Canvas>().enabled, "Freeplay overlay remained after exiting.");
            Require(Object.FindAnyObjectByType<MenuV2>().mainScreen.gameObject.activeSelf, "Main menu was not restored.");
            Debug.Log("PICO EXIT PASSED: visible entry control, immediate decoration removal, tween visibility and main menu restoration.");
            Finish(true);
            return;
        }
        if (freeplay.SelectedSong == null) freeplay.MoveSelection(1);
        freeplay.ConfirmSelection();
        freeplay.StopAllCoroutines();
        freeplay.enabled = false;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var draw = typeof(VanillaFreeplay).GetMethod("Draw", flags);
        var age = typeof(VanillaFreeplay).GetField("confirmAge", flags);
        var confirm = (VanillaFreeplayAnimate)typeof(VanillaFreeplay).GetField("picoConfirm", flags).GetValue(freeplay);
        var controlObject = new GameObject("Unscaled confirmation control", typeof(RectTransform));
        var control = controlObject.AddComponent<VanillaFreeplayAnimate>();
        control.Initialize("freeplay/backingCards/pico/pico-confirm", true);
        controlObject.SetActive(false);
        for (int frame = 0; frame < 31; frame++)
        {
            age.SetValue(freeplay, frame / 24f);
            draw.Invoke(freeplay, new object[] { 0f });
            confirm.SetFrame(frame);
            control.SetFrame(frame);
            Rect bounds = confirm.CurrentBounds;
            Rect unscaled = control.CurrentBounds;
            Require(bounds.width > unscaled.width + 400 && bounds.height > unscaled.height + 400, "Pico confirmation did not expand its backdrop at frame " + frame);
            if (new[] { 0, 9, 10, 14, 18, 21, 24, 30 }.Contains(frame)) Capture(freeplay.GetComponent<Canvas>(), "confirm-" + frame + ".png");
        }
        Require(control.CurrentBounds.width < confirm.CurrentBounds.width - 400, "Confirmation transforms leaked into a shared atlas instance.");
        Object.DestroyImmediate(controlObject);
        Debug.Log("PICO CONFIRMATION PASSED: 31 frames, unscaled control, isolated atlas transforms and eight rendered captures.");
        Finish(true);
    }

    private static void CheckPicoGunAtlas(VanillaFreeplay freeplay)
    {
        const string path = "Assets/Resources/VanillaFreeplay/freeplay/backingCards/pico/topLoop.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        var sprites = freeplay.GetComponentsInChildren<VanillaFreeplaySprite>().Where(sprite => sprite.name.StartsWith("Pico topLoop")).ToArray();
        Require(sprites.Length == 4, "Gun strip tiles are missing.");
        Require(sprites[0].mainTexture.width == width && sprites[0].mainTexture.height == height, "Gun atlas was resized during import.");
        var frames = System.Xml.Linq.XDocument.Load(Path.ChangeExtension(path, ".xml")).Root.Elements("SubTexture").ToArray();
        Require(frames.Any(frame => (float)frame.Attribute("x") + (float)frame.Attribute("width") > 4096), "Downscaled atlas negative control does not exercise the clipped area.");
        var populate = typeof(VanillaFreeplaySprite).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
        int checkedFrames = 0;
        foreach (string label in new[] { "base", "uzi info", "sniper info", "rifle info", "rocket launcher info" })
        {
            var expected = frames.Where(frame => ((string)frame.Attribute("name")).StartsWith(label)).OrderBy(frame => (string)frame.Attribute("name"), StringComparer.Ordinal).ToArray();
            foreach (var sprite in sprites) sprite.TryPlay(label, false);
            for (int i = 0; i < expected.Length; i++)
            {
                foreach (var sprite in sprites) sprite.FreezeFrame(i);
                using (var vertices = new VertexHelper())
                {
                    populate.Invoke(sprites[0], new object[] { vertices });
                    Require(vertices.currentVertCount == 4, "Gun frame has no quad.");
                    UIVertex vertex = default;
                    vertices.PopulateUIVertex(ref vertex, 2);
                    float u = ((float)expected[i].Attribute("x") + (float)expected[i].Attribute("width")) / width;
                    float v = 1 - ((float)expected[i].Attribute("y") + (float)expected[i].Attribute("height")) / height;
                    Require(Mathf.Abs(vertex.uv0.x - u) < .00001f && Mathf.Abs(vertex.uv0.y - v) < .00001f, "Gun frame samples the wrong atlas area: " + (string)expected[i].Attribute("name"));
                }
                checkedFrames++;
            }
        }
        foreach (var sprite in sprites)
        {
            sprite.TryPlay("rifle info", false);
            sprite.FreezeFrame(sprite.FrameCount - 1);
        }
        Capture(freeplay.GetComponent<Canvas>(), "guns.png");
        Debug.Log("PICO GUN ATLAS PASSED: " + checkedFrames + " frames, original texture dimensions and downscale negative control.");
    }

    public static void Capture(Canvas canvas, string filename)
    {
        var transforms = canvas.GetComponentsInChildren<Transform>(true);
        var layers = transforms.Select(item => item.gameObject.layer).ToArray();
        var host = new GameObject("Menu Probe Camera", typeof(Camera));
        var camera = host.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(0,0,-1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(1280,720,24);
        var features = AssetDatabase.FindAssets("t:UniversalRendererData").Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id))).SelectMany(data => data.rendererFeatures).Where(item => item != null && item.isActive).Distinct().ToArray();
        var previous = RenderTexture.active;
        try
        {
            foreach (var item in transforms) item.gameObject.layer = 31;
            foreach (var feature in features) feature.SetActive(false);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1000;
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = 1;
            Canvas.ForceUpdateCanvases();
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) camera.cullingMask = 0;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(1280,720,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);
                image.Apply();
                float light = image.GetPixels().Average(pixel => pixel.r+pixel.g+pixel.b);
                Require(pass == 0 ? light > .1f : light < .01f, "Menu capture or blank control failed.");
                if (pass == 0) File.WriteAllBytes(Path.Combine(Output,filename),image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.GetComponent<CanvasScaler>().enabled = true;
            for (int i=0;i<transforms.Length;i++) transforms[i].gameObject.layer = layers[i];
            foreach (var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }
    private static void Finish(bool passed)
    {
        SessionState.SetBool("VanillaCharacterSelectValidation.Active",false);
        EditorApplication.update -= Tick;
        string message = "CHARACTER SELECT: passed="+passed+", errors="+errors+", phase="+phase;
        File.WriteAllText(Path.Combine(Output,"result.txt"),message);
        Debug.Log(message);
        EditorApplication.Exit(passed?0:1);
    }
}
