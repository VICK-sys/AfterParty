using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FridayNightFunkin;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FunkinGameplayValidation
{
    private static int phase;
    private static double started;
    private static double changed;
    private static int errors;
    private static Song song;
    private static Keyboard keyboard;
    private static NoteObject futureNote;
    private static bool countdownChecked;
    private static SpriteRenderer pausePopup;
    private static Vector3 popupPosition;
    private static string Output => Path.Combine(Directory.GetCurrentDirectory(), "Validation");

    static FunkinGameplayValidation()
    {
        if (!SessionState.GetBool("FunkinValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    public static void Begin()
    {
        BeginValidation(false);
    }

    public static void BeginTutorialLayers()
    {
        BeginValidation(true);
    }

    private static void BeginValidation(bool tutorialLayers)
    {
        Directory.CreateDirectory(Output);
        SessionState.SetBool("FunkinValidation.TutorialLayers", tutorialLayers);
        SessionState.SetBool("FunkinValidation.Active", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (phase == 0 && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Next(int value)
    {
        phase = value;
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0)
        {
            EditorApplication.LockReloadAssemblies();
            started = changed = EditorApplication.timeSinceStartup;
        }
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 180, "Funkin gameplay validation timed out.");
            Require(errors == 0, "Runtime errors occurred during validation.");
            if (phase == 0)
            {
                if (elapsed < 4) return;
                var menu = Object.FindFirstObjectByType<MenuV2>();
                if (menu == null) return;
                menu.ReloadSongList();
                menu.OpenPlayScreenFromMenu();
                Next(1);
            }
            else if (phase == 1)
            {
                if (elapsed < 1) return;
                var menu = Object.FindFirstObjectByType<MenuV2>();
                var button = menu.songListRect.GetComponentsInChildren<SongButtonV2>(true).First(entry =>
                    File.Exists(Path.Combine(entry.Meta.songPath, "Vanilla.json")) &&
                    (string)JObject.Parse(File.ReadAllText(Path.Combine(entry.Meta.songPath, "Vanilla.json")))["song"] ==
                    (SessionState.GetBool("FunkinValidation.TutorialLayers", false) ? "tutorial" : "bopeebo"));
                button.GetComponent<Button>().onClick.Invoke();
                Next(2);
            }
            else if (phase == 2)
            {
                if (elapsed < 3) return;
                var menu = Object.FindFirstObjectByType<MenuV2>();
                menu.songDifficultiesDropdown.value = 2;
                menu.songModeDropdown.value = 0;
                menu.PlaySong();
                Next(3);
            }
            else if (phase == 3)
            {
                song = Object.FindFirstObjectByType<Song>();
                if (song != null && song.IsCountingDown && !countdownChecked)
                {
                    Require(song.SongPosition < 0, "Countdown must use negative song time.");
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    Require(Player.TryConvertKey(Player.primaryKeyCodes[0], out Key firstKey), "Countdown binding missing.");
                    double time = Time.realtimeSinceStartupAsDouble;
                    Send(time, firstKey);
                    Player.instance.AdvanceFrame(song.SongPosition, time * 1000, 0);
                    Require(Player.instance.Strumlines[0].IsHeld(0), "Countdown did not accept receptor input.");
                    InputSystem.RemoveDevice(keyboard);
                    countdownChecked = true;
                }
                if (song == null || !song.songStarted) return;
                Require(countdownChecked, "Countdown input check did not run.");
                if (SessionState.GetBool("FunkinValidation.TutorialLayers", false))
                {
                    CheckTutorialLayers();
                    File.WriteAllText(Path.Combine(Output, "tutorial-layers-result.txt"), "PASS: Tutorial loads one Girlfriend. Boyfriend renders in front of Girlfriend. Restoring the old order reverses the overlap pixels.\n");
                    Debug.Log("TUTORIAL LAYER VALIDATION PASSED");
                    phase = 9;
                    SessionState.SetBool("FunkinValidation.Active", false);
                    EditorApplication.Exit(0);
                    return;
                }
                CheckGameplay();
                SetupScreenshot(false);
                Next(4);
            }
            else if (phase == 4)
            {
                if (elapsed < 1) return;
                Capture("upscroll.png");
                SetupScreenshot(true);
                Next(5);
            }
            else if (phase == 5)
            {
                if (elapsed < 1) return;
                Capture("downscroll.png");
                CaptureHealthStates();
                Require(errors == 0, "Render checks reported errors.");
                song.FunkinHud.ClearPopups();
                song.FunkinHud.ShowRating(FunkinRules.Judgement.Sick, 0);
                song.FunkinHud.Render();
                pausePopup = song.FunkinHud.GetComponentsInChildren<SpriteRenderer>().First(sprite => sprite.enabled && sprite.sortingOrder >= 900);
                popupPosition = pausePopup.transform.position;
                Player.instance.enabled = false;
                song.stopwatch.Start();
                Next(6);
            }
            else if (phase == 6)
            {
                if (elapsed < 0.05) return;
                Require(Vector3.Distance(pausePopup.transform.position, popupPosition) > 0.001f, "Popup did not move before pause control.");
                Pause.instance.pauseScreen.SetActive(true);
                song.FunkinHud.Icons[0].Reset();
                song.FunkinHud.Icons[0].Bop(song.stepCrochet);
                popupPosition = pausePopup.transform.position;
                Next(7);
            }
            else if (phase == 7)
            {
                if (elapsed < 0.2) return;
                Require(Vector3.Distance(pausePopup.transform.position, popupPosition) < 0.00001f && song.FunkinHud.Icons[0].Width == 180,
                    "Paused HUD continued popup motion or icon bounce.");
                Pause.instance.pauseScreen.SetActive(false);
                Next(8);
            }
            else if (phase == 8)
            {
                if (elapsed < 0.05) return;
                Require(Vector3.Distance(pausePopup.transform.position, popupPosition) > 0.001f && song.FunkinHud.Icons[0].Width < 180,
                    "HUD did not resume after pause.");
                Require(errors == 0, "HUD pause checks reported errors.");
                File.WriteAllText(Path.Combine(Output, "result.txt"), "PASS: countdown input, timestamped keyboard and controller events, delayed input control, holds, alternate bindings, pooling, splash limits, bot play, pause clearing, incoming-note retention, HUD health and combo rules, camera zoom, HUD pause/resume controls, and four renders with blank controls.\n");
                Debug.Log("FUNKIN GAMEPLAY VALIDATION PASSED");
                phase = 9;
                SessionState.SetBool("FunkinValidation.Active", false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText(Path.Combine(Output, "result.txt"), "FAIL: " + exception);
            SessionState.SetBool("FunkinValidation.Active", false);
            EditorApplication.Exit(1);
        }
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool("FunkinValidation.Active", false);
        EditorApplication.Exit(phase == 9 && errors == 0 ? 0 : 1);
    }

    private static void ClearCase()
    {
        var scheduler = (List<NoteBehaviour>)typeof(Song).GetField("_noteBehaviours", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(song);
        scheduler.Clear();
        foreach (var line in Player.instance.Strumlines)
            foreach (var note in line.Notes.ToArray()) song.ReleaseFunkinNote((NoteObject)note.View);
        Player.instance.ResetSong();
        song.playerOneStats = new PlayerStat();
        song.playerTwoStats = new PlayerStat();
        song.ResetFunkinScore();
        song.health = 100;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        Player.instance.ClearInput();
        song.InitializeFunkinStrums();
        song.FunkinHud.Initialize(song, "dad");
    }

    private static void CheckTutorialLayers()
    {
        Require(song.vanillaPlayback.SongId == "tutorial" && song.enemy.characterName == "Girlfriend" &&
            !song.girlfriendObject.activeSelf, "Tutorial did not load exactly one Girlfriend.");
        SpriteRenderer boyfriend = song.boyfriendObject.GetComponent<SpriteRenderer>();
        SpriteRenderer girlfriend = song.opponentObject.GetComponent<SpriteRenderer>();
        int boyfriendLayer = boyfriend.gameObject.layer;
        int girlfriendLayer = girlfriend.gameObject.layer;
        int girlfriendOrder = girlfriend.sortingOrder;
        Vector3 boyfriendPosition = boyfriend.transform.position;
        var cameraObject = new GameObject("Tutorial layer validation");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(song.mainCamera);
        Bounds framing = girlfriend.bounds;
        framing.Encapsulate(boyfriend.bounds);
        camera.transform.SetPositionAndRotation(new Vector3(framing.center.x, framing.center.y, song.mainCamera.transform.position.z), song.mainCamera.transform.rotation);
        camera.orthographicSize = Mathf.Max(framing.extents.y, framing.extents.x / (1280f / 720)) * 1.15f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(1280, 720, 24);
        var active = RenderTexture.active;
        camera.targetTexture = target;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        foreach (var feature in features) feature.SetActive(false);
        Color32[] Render(string file = null)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            if (file != null) File.WriteAllBytes(Path.Combine(Output, file), texture.EncodeToPNG());
            Color32[] pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return pixels;
        }
        int Difference(Color32 first, Color32 second) => Math.Abs(first.r - second.r) + Math.Abs(first.g - second.g) + Math.Abs(first.b - second.b);
        try
        {
            Render("tutorial-stage.png");
            boyfriend.gameObject.layer = girlfriend.gameObject.layer = 31;
            camera.cullingMask = 1 << 31;
            boyfriend.transform.position += girlfriend.bounds.center - boyfriend.bounds.center;
            girlfriend.enabled = false;
            Color32[] playerOnly = Render();
            girlfriend.enabled = true;
            boyfriend.enabled = false;
            Color32[] opponentOnly = Render();
            boyfriend.enabled = true;
            Color32[] fixedOrder = Render("tutorial-overlap-fixed.png");
            girlfriend.sortingOrder = 7;
            Color32[] oldOrder = Render("tutorial-overlap-old-order.png");
            int playerInFront = 0;
            int oldOpponentInFront = 0;
            for (int index = 0; index < fixedOrder.Length; index++)
            {
                Color32 player = playerOnly[index];
                Color32 opponent = opponentOnly[index];
                if (player.r + player.g + player.b < 100 || opponent.r + opponent.g + opponent.b < 100 || Difference(player, opponent) < 100) continue;
                if (Difference(fixedOrder[index], player) < 6 && Difference(fixedOrder[index], oldOrder[index]) > 100) playerInFront++;
                if (Difference(oldOrder[index], opponent) < 6 && Difference(fixedOrder[index], oldOrder[index]) > 100) oldOpponentInFront++;
            }
            Require(playerInFront > 1000 && oldOpponentInFront > 1000,
                "Layer overlap or old-order negative control failed: BF=" + playerInFront + ", old GF=" + oldOpponentInFront);
            Debug.Log("TUTORIAL LAYER PIXELS PASSED: BF front=" + playerInFront + ", old-order GF front=" + oldOpponentInFront);
        }
        finally
        {
            boyfriend.gameObject.layer = boyfriendLayer;
            girlfriend.gameObject.layer = girlfriendLayer;
            girlfriend.sortingOrder = girlfriendOrder;
            boyfriend.transform.position = boyfriendPosition;
            boyfriend.enabled = girlfriend.enabled = true;
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = active;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }
    }

    private static NoteObject AddNote(double time, int direction, double length = 0, bool player = true)
    {
        var chart = (FNFSong)typeof(Song).GetField("_song", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(song);
        var section = chart.Sections.First();
        int lane = direction + (section.MustHitSection == player ? 0 : 4);
        song.GenNote(section, new List<decimal> { (decimal)time, lane, (decimal)length });
        return song.lastNote;
    }

    private static void Send(double realtime, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys), realtime);
        InputSystem.Update();
    }

    private static void CheckGameplay()
    {
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>();
        Player.inputOffset = Player.visualOffset = 0;
        Player.primaryKeyCodes = new List<KeyCode> { KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.UpArrow, KeyCode.RightArrow };
        Player.secondaryKeyCodes = new List<KeyCode> { KeyCode.A, KeyCode.S, KeyCode.W, KeyCode.D };
        OptionsV2.GhostTapping = false;
        foreach (double latency in new[] { 5d, 25, 60, 100 })
        {
            ClearCase();
            double now = song.SongPosition;
            double realtime = Time.realtimeSinceStartupAsDouble;
            NoteObject note = AddNote(now - latency, 0);
            Player.instance.Strumlines[0].Advance(now, 0);
            Send(realtime - latency / 1000, Key.LeftArrow);
            Require(Player.instance.Strumlines[0].Presses.Count == 1, "Timestamped keyboard press was not captured.");
            Player.instance.AdvanceFrame(now, realtime * 1000, 0);
            Require(note.State.Hit && song.playerOneStats.totalSicks == 1 && song.playerOneStats.currentScore == 500,
                "Input latency changed judgement at " + latency + " ms.");
        }
        ClearCase();
        double position = song.SongPosition;
        double clock = Time.realtimeSinceStartupAsDouble;
        NoteObject control = AddNote(position - 100, 0);
        Player.instance.Strumlines[0].Advance(position, 0);
        Send(clock, Key.LeftArrow);
        Player.instance.AdvanceFrame(position, clock * 1000, 0);
        Require(control.State.Hit && song.playerOneStats.totalBads == 1 && song.playerOneStats.currentScore < 500,
            "Frame timestamp negative control did not reject a perfect hit.");
        ClearCase();
        position = song.SongPosition;
        clock = Time.realtimeSinceStartupAsDouble;
        NoteObject hold = AddNote(position, 0, 1000);
        var line = Player.instance.Strumlines[0];
        line.Advance(position, 0);
        Send(clock, Key.LeftArrow);
        Player.instance.AdvanceFrame(position, clock * 1000, 0);
        Send(clock, Key.LeftArrow, Key.A);
        Player.instance.AdvanceFrame(position, clock * 1000, 0);
        Send(clock, Key.A);
        Player.instance.AdvanceFrame(position + 100, clock * 1000, 0.1);
        line.Advance(position + 200, 0.1);
        Require(!hold.State.HoldDropped && line.IsHeld(0), "Alternate binding failed to preserve a sustain.");
        Send(clock);
        Player.instance.AdvanceFrame(position + 200, clock * 1000, 0);
        line.Advance(position + 210, 0.01);
        Require(hold.State.HoldDropped && hold.State.HandledDrop, "Final release failed to drop sustain.");
        song.ReleaseFunkinNote(hold);
        NoteObject reused = AddNote(position + 500, 0);
        Require(!reused.State.Hit && !reused.State.HoldDropped && reused.State.Length == 0, "Pool retained hold state.");
        Player.instance.ClearInput();
        Require(!line.IsHeld(0) && line.Presses.Count == 0, "Pause left stale input.");
        ClearCase();
        Player.demoMode = true;
        Player.instance.ConfigureBindings();
        position = song.SongPosition;
        NoteObject bot = AddNote(position, 1, 500);
        line.Advance(position, 0);
        line.Advance(position + 200, 0.2);
        Require(bot.State.Hit && !bot.State.HoldDropped && song.playerOneStats.currentScore == 0, "Bot hold or score mismatch.");
        Player.demoMode = false;
        ClearCase();
        Player.twoPlayers = true;
        Player.instance.ConfigureBindings();
        position = song.SongPosition;
        clock = Time.realtimeSinceStartupAsDouble;
        NoteObject p1 = AddNote(position, 0);
        NoteObject p2 = AddNote(position, 0, 0, false);
        foreach (var strum in Player.instance.Strumlines) strum.Advance(position, 0);
        Send(clock, Key.LeftArrow, Key.A);
        Player.instance.AdvanceFrame(position, clock * 1000, 0);
        Require(p1.State.Hit && p2.State.Hit && song.playerOneStats.currentScore == 500 && song.playerTwoStats.currentScore == 500,
            "Local two-player routing changed.");
        Player.twoPlayers = false;
        ClearCase();
        var gamepad = InputSystem.AddDevice<Gamepad>();
        InputSystem.EnableDevice(gamepad);
        position = song.SongPosition;
        clock = Time.realtimeSinceStartupAsDouble;
        NoteObject padNote = AddNote(position, 1);
        line.Advance(position, 0);
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South), clock);
        InputSystem.Update();
        string padEvents = string.Join(",", line.Presses.Select(entry => entry.Direction + ":" + entry.Time));
        Player.instance.AdvanceFrame(position, clock * 1000, 0);
        Require(padNote.State.Hit && song.playerOneStats.currentScore == 500,
            "Controller button mapping or timestamp failed: hit=" + padNote.State.Hit + ", score=" + song.playerOneStats.currentScore + ", events=" + padEvents);
        InputSystem.RemoveDevice(gamepad);
        ClearCase();
        CheckSplashPool();
        CheckHud();
        Debug.Log("FUNKIN INPUT CHECKS PASSED: four latency cases, negative control, dual-bind hold, release, pooling, pause, bot, and local two-player.");
    }

    private static void CheckSplashPool()
    {
        for (int index = 0; index < 8; index++)
            song.player1NoteSprites[index % 4].GetComponent<FunkinStrumEffect>().Splash();
        int CountSplashes(IEnumerable<SpriteRenderer> strums) => strums.Sum(strum =>
            strum.GetComponentsInChildren<SpriteRenderer>().Count(sprite => sprite.enabled && sprite.sortingOrder == 50));
        Require(CountSplashes(song.player1NoteSprites) == 6, "Splash cap must apply across all four lanes.");
        Require(CountSplashes(song.player2NoteSprites) == 0, "Player splashes crossed strumlines.");
        for (int index = 0; index < 8; index++)
            song.player2NoteSprites[index % 4].GetComponent<FunkinStrumEffect>().Splash();
        Require(CountSplashes(song.player1NoteSprites) == 6 && CountSplashes(song.player2NoteSprites) == 6,
            "Each strumline must have an independent splash pool.");
        song.InitializeFunkinStrums();
        Require(CountSplashes(song.player1NoteSprites) == 0 && CountSplashes(song.player2NoteSprites) == 0,
            "Reset left active splashes.");
    }

    private static void SetupScreenshot(bool downscroll)
    {
        ClearCase();
        OptionsV2.Downscroll = downscroll;
        song.stopwatch.Stop();
        song.enabled = false;
        song.uiCamera.orthographicSize = song.HudReferenceOrthographicSize;
        song.RefreshFunkinStrums();
        double position = song.SongPosition;
        for (int lane = 0; lane < 4; lane++)
        {
            AddNote(position + 250 + lane * 150, lane, lane == 0 ? 220 : lane == 2 ? 80 : 0);
            AddNote(position + 250 + lane * 150, lane, lane == 0 ? 220 : lane == 2 ? 80 : 0, false);
        }
        NoteObject clipped = AddNote(position - 200, 3, 600);
        Player.instance.Strumlines[0].Hit(clipped.State, 0, false, position);
        NoteObject bad = AddNote(position, 1, 0, false);
        Player.instance.Strumlines[1].Hit(bad.State, 100, false, position);
        futureNote = AddNote(position + 1400, 3);
        song.FunkinHud.ClearPopups();
        song.FunkinHud.ShowRating(FunkinRules.Judgement.Sick, 0);
        song.FunkinHud.ShowCombo(123, 0);
        song.FunkinHud.Advance(0.15, position);
        song.FunkinHud.Render();
        Require(Object.FindObjectsByType<FunkinHoldMesh>(FindObjectsSortMode.None).Length >= 4, "Continuous hold meshes did not spawn.");
    }

    private static void CheckHud()
    {
        ClearCase();
        FunkinHud hud = song.FunkinHud;
        Require(hud != null && !song.liteRatingObjectP1.activeSelf && !song.playerOneComboText.enabled &&
            !song.playerOneScoringObject.activeSelf, "Legacy judgement or score UI remains visible.");
        Require(song.healthBar.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.enabled), "Legacy health graphics remain visible.");
        song.health = 20;
        hud.Advance(0, song.SongPosition);
        Require(Math.Abs(hud.DisplayHealth - 88) < 0.001, "HUD does not smooth health by 0.15 per frame.");
        Require(hud.Icons[0].Animation == FunkinHealthIconState.Face.Losing, "HUD icon used smoothed health instead of raw health.");
        Player.demoMode = true;
        hud.Advance(0, song.SongPosition);
        hud.Render();
        Require(hud.DisplayHealth == 200 && song.health == 20 && hud.ScoreText == "Bot Play Enabled", "Bot HUD display changed gameplay health.");
        Player.demoMode = false;
        ClearCase();
        song.playerOneStats.currentScore = 12345;
        hud.Render();
        Require(hud.ScoreText == "Score: 12,345", "Score bitmap label format differs.");
        SpriteRenderer border = hud.GetComponentsInChildren<SpriteRenderer>().First(sprite => sprite.name == "Health Bar Border");
        Vector3 borderPosition = border.transform.position;
        float originalZoom = song.uiCamera.orthographicSize;
        Vector3 originalViewport = song.uiCamera.WorldToViewportPoint(borderPosition);
        song.uiCamera.orthographicSize *= 0.8f;
        hud.Render();
        Require(Vector3.Distance(borderPosition, border.transform.position) < 0.00001f &&
            Vector3.Distance(originalViewport, song.uiCamera.WorldToViewportPoint(border.transform.position)) > 0.01f,
            "HUD layout cancelled the camera zoom.");
        song.uiCamera.orthographicSize = originalZoom;
        song.playerOneStats.currentCombo = 8;
        double position = song.SongPosition;
        var line = Player.instance.Strumlines[0];
        line.Hit(AddNote(position, 0).State, 0, false, position);
        Require(hud.PopupCount == 1, "Nine combo must show only a judgement.");
        line.Hit(AddNote(position, 1).State, 0, false, position);
        Require(hud.PopupCount == 5, "Ten combo must add a judgement and three digits.");
        line.Hit(AddNote(position, 2).State, 100, false, position);
        Require(song.playerOneStats.currentCombo == 0 && hud.PopupCount == 9, "Bad hit must add 000 and a judgement when breaking ten combo.");
        hud.ClearPopups();
        song.playerOneStats.currentCombo = 10;
        song.ApplyFunkinGhost(0, 0);
        Require(hud.PopupCount == 0 && song.playerOneStats.currentCombo == 10, "Ghost miss must not display combo break.");
        song.ApplyFunkinMiss(AddNote(position, 3));
        Require(hud.PopupCount == 3 && song.playerOneStats.currentCombo == 0, "Missed note must display only 000 for a broken combo.");
        hud.ClearPopups();
        foreach (var rating in new[] { FunkinRules.Judgement.Sick, FunkinRules.Judgement.Good, FunkinRules.Judgement.Bad, FunkinRules.Judgement.Shit })
            hud.ShowRating(rating, 0);
        Require(hud.PopupCount == 4, "Judgements must overlap instead of replacing each other.");
        hud.ShowRating(FunkinRules.Judgement.Miss, 0);
        Require(hud.PopupCount == 4, "Miss must not create a judgement sprite.");
        hud.Advance(song.beatsPerSecond + 0.201, position);
        Require(hud.PopupCount == 0, "Judgements did not expire after one beat and 0.2 seconds.");
        hud.ShowCombo(1000, 0);
        Require(hud.PopupCount == 4, "Four-digit combo was truncated.");
        hud.Advance(song.beatsPerSecond + 0.201, position);
        Require(hud.PopupCount == 4, "Combo digits must remain longer than judgements.");
        hud.Advance(song.beatsPerSecond, position);
        Require(hud.PopupCount == 0, "Combo digits did not expire after two beats and 0.2 seconds.");
        ClearCase();
        Debug.Log("FUNKIN HUD CHECKS PASSED: health smoothing, raw-health faces, bot display, score font, combo thresholds, breaks, overlap, and popup lifetime.");
    }

    private static void CaptureHealthStates()
    {
        FunkinHud hud = song.FunkinHud;
        foreach (int health in new[] { 20, 190 })
        {
            song.health = health;
            for (int frame = 0; frame < 100; frame++) hud.Advance(0, song.SongPosition);
            hud.ClearPopups();
            hud.ShowRating(health == 20 ? FunkinRules.Judgement.Shit : FunkinRules.Judgement.Good, 0);
            hud.ShowCombo(health == 20 ? 0 : 456, 0);
            hud.Render();
            Capture(health == 20 ? "hud-low-health.png" : "hud-high-health.png");
        }
        song.health = 100;
    }

    private static void Capture(string name)
    {
        Require(futureNote.gameObject.activeSelf && Player.instance.Strumlines[0].Notes.Contains(futureNote.State),
            "An incoming note was recycled before reaching the screen.");
        var cameraObject = new GameObject("Note render validation");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(song.uiCamera);
        camera.transform.SetPositionAndRotation(song.uiCamera.transform.position, song.uiCamera.transform.rotation);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(1280, 720, 24);
        var active = RenderTexture.active;
        camera.targetTexture = target;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        foreach (var feature in features) feature.SetActive(false);
        try
        {
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) camera.cullingMask = 0;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                int colored = texture.GetPixels32().Count(pixel => Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) - Math.Min(pixel.r, Math.Min(pixel.g, pixel.b)) > 30);
                Require(pass == 0 ? colored > 1000 : colored == 0, "Note render or blank control failed: " + name + " colors=" + colored);
                if (pass == 0 && (name == "upscroll.png" || name == "downscroll.png"))
                {
                    int barY = (int)FunkinHudRules.BarY(OptionsV2.Downscroll) + 8;
                    Color red = texture.GetPixel(400, 719 - barY);
                    Color green = texture.GetPixel(850, 719 - barY);
                    Require(red.r > 0.99f && red.g < 0.01f && green.g > 0.99f &&
                        Math.Abs(green.r - 102f / 255) < 0.01f && Math.Abs(green.b - 51f / 255) < 0.01f,
                        "HUD bar position or colors differ: " + red + " / " + green);
                }
                if (pass == 0) File.WriteAllBytes(Path.Combine(Output, name), texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = active;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }
    }
}
