using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FridayNightFunkin;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaPauseValidation
{
    private static int phase;
    private static int errors;
    private static double started;
    private static double changed;
    private static Song song;
    private static double pausedPosition;
    private static int[] audioSamples;
    private static Song songInstance;
    private static bool finishing;
    private static bool countdownChecked;
    private static bool countdownPaused;
    private static AudioSource countdownAudio;
    private static RawImage countdownGraphic;
    private static float countdownAlpha;
    private static Vector3 cameraPosition;
    private static NoteObject restartNote;
    private static Vector3 restartOrigin;
    private static Vector3 restartReceptor;
    private static bool outgoingObserved;
    private static bool incomingObserved;
    private static bool lateNoteChecked;
    private static int coveredRestarts;
    private static AudioClip restartClip;
    private static IVanillaCharacterStage restartStage;
    private static double retryStarted;
    private static NoteObject incomingControl;
    private static MeshRenderer restartHold;
    private static Vector3 restartHoldOrigin;
    private static float restartDelay;
    private static bool observedDeathFade;
    private static bool observedRetryReveal;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_PAUSE_TEST_PATH") ?? Path.GetFullPath("Builds/PauseValidation");

    static VanillaPauseValidation()
    {
        if (!SessionState.GetBool("VanillaPauseValidation.Active", false)) return;
        EditorApplication.update += Tick;
        SceneManager.sceneLoaded += CheckRestartCover;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (stack.Contains("DG.DOTweenEditor.UtilityWindowProcessor")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run pause validation in an isolated batch editor.");
        CheckRestartCurves();
        CheckPixelFade();
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "PauseValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaPauseValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckRestartCover(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Game_Backup3" && phase >= 3 && phase <= 10) coveredRestarts++;
    }

    private static void Next()
    {
        phase++;
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && EditorApplication.timeSinceStartup - started < 180, "Pause validation failed or timed out.");
            switch (phase)
            {
                case 0:
                    if (elapsed < 7 || Object.FindAnyObjectByType<MenuV2>() == null) return;
                    Pause.ResetSession();
                    Pause.SetGlobalOffset(0);
                    OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
                    OptionsV2.Downscroll = Environment.GetEnvironmentVariable("UNITY_PARTY_RESTART_DOWNSCROLL") == "1";
                    OptionsV2.menuVolume = 0.8f;
                    OptionsV2.miscVolume = 0.6f;
                    InGameVolume.menuVolume = 0;
                    VanillaStoryCampaign.ReturnToMenu();
                    VanillaFreeplay.ReturnToFreeplay = true;
                    Song.currentSongMeta = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
                        .Single(item => item.meta.songName == (Environment.GetEnvironmentVariable("UNITY_PARTY_RESTART_SONG") ?? "Bopeebo")).meta;
                    Song.difficulty = "Normal";
                    Song.modeOfPlay = PlayModes.Boyfriend;
                    SceneManager.LoadScene("Game_Backup3");
                    Next();
                    break;
                case 1:
                    song = Song.instance;
                    if (song != null && song.IsCountingDown && !countdownChecked)
                    {
                        if (!countdownPaused)
                        {
                            var presentation = song.vanillaPlayback.Presentation;
                            countdownGraphic = presentation.GetComponentsInChildren<RawImage>()
                                .FirstOrDefault(image => image.name == "Countdown" && image.texture.name == "ready");
                            if (countdownGraphic == null) return;
                            countdownAudio = (AudioSource)typeof(VanillaCampaignPresentation).GetField("sound",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(presentation);
                            Require(countdownAudio.isPlaying && countdownGraphic.color.a > 0, "Countdown control has no visible graphic or playing audio.");
                            Require(!LoadingTransition.instance.toggled, "Loading screen covered the countdown.");
                            countdownAlpha = countdownGraphic.color.a;
                            Pause.instance.PauseSong();
                            pausedPosition = song.SongPosition;
                            changed = EditorApplication.timeSinceStartup;
                            countdownPaused = true;
                        }
                        else if (elapsed >= 0.3)
                        {
                            Require(song.SongPosition == pausedPosition && !countdownAudio.isPlaying, "Countdown advanced while paused.");
                            Require(countdownGraphic != null && countdownGraphic.color.a == countdownAlpha, "Countdown graphic faded while paused.");
                            Pause.instance.ContinueSong();
                            Require(countdownAudio.isPlaying, "Countdown audio did not resume.");
                            countdownChecked = true;
                        }
                        return;
                    }
                    if (song == null || !song.songStarted || song.SongPosition < 500) return;
                    Require(countdownChecked, "Countdown pause check did not run.");
                    Require(song.musicSources[0].isPlaying && song.stopwatch.IsRunning, "Running control did not advance audio and clock.");
                    song.health = 0;
                    Next();
                    break;
                case 2:
                    if (!song.isDead) return;
                    Require(Pause.DeathCount == 1, "Real death did not increment Blue Balls.");
                    songInstance = song;
                    restartClip = song.musicClip;
                    restartStage = song.vanillaPlayback.CharacterStage;
                    retryStarted = Time.realtimeSinceStartupAsDouble;
                    song.ConfirmRetry();
                    Require(song.isDead && song.respawning, "Death confirmation did not retain the game-over state.");
                    Next();
                    break;
                case 3:
                    song = Song.instance;
                    var retryCover = GameObject.Find("Retry Cover")?.GetComponent<Image>();
                    if (retryCover != null && retryCover.color.a >= .3f && retryCover.color.a <= .7f)
                    {
                        Require(retryCover.canvas.worldCamera == (song.isDead ? song.deadCamera : song.mainCamera),
                            "Retry fade covered the HUD instead of the gameplay camera.");
                        if (song.isDead && !observedDeathFade || !song.isDead && !observedRetryReveal)
                        {
                            if (song.isDead) observedDeathFade = true;
                            else observedRetryReveal = true;
                        }
                    }
                    if (song == null || !song.songStarted || song.isDead) return;
                    Require(observedDeathFade && observedRetryReveal, "Game-over retry did not render both camera fades.");
                    Require(ReferenceEquals(song, songInstance) && song.musicClip == restartClip && ReferenceEquals(restartStage, song.vanillaPlayback.CharacterStage),
                        "Death retry replaced the song, stage, or instrumental.");
                    Require(Time.realtimeSinceStartupAsDouble - retryStarted >= song.deadConfirm.length / 7 + 2 + .5 + song.beatsPerSecond * 5 - .1,
                        "Death retry skipped confirmation, fade, delay, or countdown.");
                    Require(Pause.DeathCount == 1, "Restart discarded death count.");
                    Pause.instance.PauseSong();
                    pausedPosition = song.SongPosition;
                    cameraPosition = song.mainCamera.transform.position;
                    audioSamples = song.musicSources.Select(source => source.timeSamples).ToArray();
                    Require(Pause.instance.IsPaused && Time.timeScale == 0 && !song.stopwatch.IsRunning, "Pause did not freeze gameplay.");
                    Require(Pause.instance.View.Labels.SequenceEqual(new[] { "Resume", "Restart Song", "Change Difficulty", "Enable Practice Mode", "Exit to Menu" }), "Standard entries differ.");
                    Next();
                    break;
                case 4:
                    if (elapsed < 2.6) return;
                    Require(song.SongPosition == pausedPosition && song.musicSources.All(source => !source.isPlaying), "Paused clock or audio advanced.");
                    Require(song.mainCamera.transform.position == cameraPosition, "Paused camera moved.");
                    Require(song.musicSources.Select(source => source.timeSamples).SequenceEqual(audioSamples), "Paused audio samples advanced.");
                    Require(Pause.instance.View.Music.isPlaying && Pause.instance.View.Music.time > 0, "Breakfast did not play while gameplay paused.");
                    Require(Pause.instance.View.Music.volume > 0, "Breakfast used the inactive volume setting.");
                    Pause.instance.View.Render(5);
                    Require(Mathf.Approximately(Pause.instance.View.Music.volume, 0.6f), "Breakfast did not use the music volume setting.");
                    OptionsV2.menuVolume = 0;
                    Pause.instance.View.Render(0);
                    Require(Pause.instance.View.Music.volume == 0, "Muted music control remained audible.");
                    OptionsV2.menuVolume = 0.8f;
                    Pause.instance.View.Render(0);
                    Capture("standard.png", 1280, 720, false);
                    Capture("blank.png", 1280, 720, true);
                    Pause.instance.View.ChangeSelection(-1);
                    Require(Pause.instance.View.SelectedIndex == 4, "Selection did not wrap upward.");
                    Pause.instance.View.Render(0.4f);
                    Capture("exit-selected.png", 1280, 720, false);
                    Pause.instance.View.ChangeSelection(1);
                    Require(Pause.instance.View.SelectedIndex == 0, "Selection did not wrap downward.");
                    Pause.instance.View.ShowDifficulties();
                    Require(Pause.instance.View.Labels.SequenceEqual(new[] { "Easy", "Normal", "Hard", "Back" }), "Difficulty entries crossed song variations.");
                    Pause.instance.View.Render(2);
                    Capture("difficulties.png", 1280, 720, false);
                    Capture("wide.png", 1600, 720, false);
                    Capture("tall.png", 960, 720, false);
                    Pause.instance.View.ChangeSelection(3);
                    Pause.instance.View.Accept();
                    Require(!Pause.instance.View.DifficultyMenu && Pause.instance.IsPaused, "Back did not return to standard menu.");
                    Pause.SetGlobalOffset(1501);
                    Require(Pause.GlobalOffset == 1500 && song.SongPosition == pausedPosition + 1500, "Positive offset did not shift gameplay or clamp.");
                    NoteObject frozenNote = song.player1NotesObjects.Concat(song.player2NotesObjects).SelectMany(notes => notes).FirstOrDefault();
                    Require(frozenNote != null, "Paused note control was missing.");
                    Vector3 notePosition = frozenNote.transform.position;
                    frozenNote.SendMessage("LateUpdate");
                    Require(frozenNote.transform.position == notePosition, "Offset adjustment moved a paused note.");
                    Pause.SetGlobalOffset(-1501);
                    Require(Pause.GlobalOffset == -1500, "Negative offset did not clamp.");
                    Pause.SetGlobalOffset(0);
                    songInstance = song;
                    Pause.instance.ChangeDifficulty("Hard");
                    Next();
                    break;
                case 5:
                    song = Song.instance;
                    if (song == null || !song.songStarted) return;
                    Require(ReferenceEquals(song, songInstance) && song.musicClip == restartClip, "Difficulty change reloaded the song or instrumental.");
                    Require(Song.difficulty == "Hard" && song.jsonDir.EndsWith("Chart-hard.json"), "Difficulty did not reload the selected chart.");
                    Require(Pause.DeathCount == 1 && Time.timeScale == 1 && song.musicSources[0].isPlaying, "Difficulty reload left gameplay paused or lost deaths.");
                    Pause.instance.PauseSong();
                    Pause.instance.EnablePractice();
                    Require(Pause.PracticeMode && !Pause.instance.View.Labels.Contains("Enable Practice Mode"), "Practice option did not hide.");
                    Pause.instance.View.Render(20);
                    Capture("practice-charter.png", 1280, 720, false);
                    Require(Pause.instance.View.GetComponentsInChildren<VanillaPauseText>().Any(text => text.Text.StartsWith("Charter: ") && text.color.a > 0.99f), "Artist did not change to charter.");
                    Pause.instance.ContinueSong();
                    Require(!Pause.instance.IsPaused && Time.timeScale == 1 && !Pause.instance.View.Music.isPlaying, "Resume left pause state active.");
                    song.health = 0;
                    pausedPosition = song.SongPosition;
                    Next();
                    break;
                case 6:
                    if (elapsed < 0.4) return;
                    Require(!song.isDead && song.SongPosition > pausedPosition + 100 && song.musicSources[0].isPlaying, "Practice died or resume did not advance.");
                    Pause.instance.PauseSong();
                    songInstance = song;
                    var chart = (FNFSong)typeof(Song).GetField("_song", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(song);
                    song.GenNote(chart.Sections.First(), new List<decimal> { (decimal)song.SongPosition + 500, 0, 1200 });
                    restartNote = song.lastNote;
                    restartHold = restartNote.GetComponentInChildren<MeshRenderer>();
                    Require(restartHold != null && restartHold.enabled, "Outgoing sustain control was not visible.");
                    restartHoldOrigin = restartHold.bounds.center;
                    restartOrigin = restartNote.transform.position;
                    restartReceptor = song.player1NoteSprites[0].transform.position;
                    song.playerOneStats.currentScore = 4321;
                    song.playerOneStats.missedHits = 9;
                    Pause.instance.RestartSong();
                    Require(!Pause.instance.IsPaused && !Pause.instance.Transitioning && song.RestartDelayRemaining > 0, "Restart did not reveal the outgoing notes.");
                    Require(song.health == 100 && song.playerOneStats.currentScore == 0 && song.playerOneStats.missedHits == 0,
                        "Restart did not reset health and score immediately.");
                    song.GenNote(chart.Sections.First(), new List<decimal> { 0, 0, 1000 });
                    incomingControl = song.lastNote;
                    Next();
                    break;
                case 7:
                    song = Song.instance;
                    if (ReferenceEquals(song, songInstance) && restartNote != null && restartNote.RestartOutgoing && restartNote.gameObject.activeInHierarchy)
                    {
                        float displacement = restartNote.transform.position.y - restartOrigin.y;
                        if (Mathf.Abs(displacement) > .01f)
                        {
                            Require(OptionsV2.Downscroll ? displacement > 0 : displacement < 0, "Restart notes moved in the wrong direction.");
                            Require(Mathf.Abs(restartHold.bounds.center.y - restartHoldOrigin.y - displacement) < .001f,
                                "Outgoing sustain did not move with its head.");
                            Require(song.player1NoteSprites[0].transform.position == restartReceptor, "Restart moved a stationary receptor.");
                            Require(!song.stopwatch.IsRunning && !song.musicSources[0].isPlaying, "Restart resumed gameplay during the outgoing effect.");
                            outgoingObserved = true;
                        }
                    }
                    if (song != null && song.IsCountingDown && Mathf.Abs(song.RestartNoteOffset) > .01f)
                    {
                        var note = incomingControl;
                        if (note != null)
                        {
                            note.SendMessage("LateUpdate");
                            var receptor = (note.mustHit ? song.player1NoteSprites : song.player2NoteSprites)[note.type];
                            double distance = FunkinRules.NoteDistance(note.State.Time, song.SongPosition - Player.visualOffset,
                                song.FunkinScrollSpeed, OptionsV2.Downscroll);
                            float actual = (note.transform.position.y - receptor.transform.position.y) / song.FunkinWorldPixelSize;
                            Require(Math.Abs(actual - distance - song.RestartNoteOffset) < 2, "Incoming notes did not use the retry offset.");
                            if (!lateNoteChecked && Mathf.Abs(song.RestartNoteOffset) > 20)
                            {
                                var initialChart = (FNFSong)typeof(Song).GetField("_song", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(song);
                                song.GenNote(initialChart.Sections.First(), new List<decimal> { 0, 1, 0 });
                                NoteObject late = song.lastNote;
                                late.SendMessage("LateUpdate");
                                var lateReceptor = (late.mustHit ? song.player1NoteSprites : song.player2NoteSprites)[late.type];
                                double normalDistance = FunkinRules.NoteDistance(late.State.Time, song.SongPosition - Player.visualOffset,
                                    song.FunkinScrollSpeed, OptionsV2.Downscroll);
                                float lateDistance = (late.transform.position.y - lateReceptor.transform.position.y) / song.FunkinWorldPixelSize;
                                Require(Math.Abs(lateDistance - normalDistance) < 2, "Late note control inherited the incoming animation.");
                                lateNoteChecked = true;
                            }
                            incomingObserved = true;
                        }
                    }
                    if (song == null || !song.songStarted) return;
                    Require(ReferenceEquals(song, songInstance) && song.musicClip == restartClip && ReferenceEquals(restartStage, song.vanillaPlayback.CharacterStage),
                        "Pause restart replaced the song, stage, or instrumental.");
                    Require(outgoingObserved && incomingObserved && lateNoteChecked, "Restart note animation or late note control was not observed.");
                    Require(coveredRestarts == 0, "Restart loaded a new gameplay scene.");
                    Require(!LoadingTransition.instance.HoldingStoryFrame && !LoadingTransition.instance.toggled,
                        "Restart cover survived into gameplay.");
                    Require(song.RestartNoteOffset == 0, "Restart offset survived the countdown.");
                    Debug.Log("RESTART NOTES PASSED: outgoing motion, stationary receptors, frozen gameplay, incoming offset and completed countdown.");
                    Require(Pause.PracticeMode && Song.difficulty == "Hard", "Restart discarded practice mode or difficulty.");
                    Pause.instance.PauseSong();
                    Pause.instance.RestartSong();
                    Pause.instance.PauseSong();
                    restartDelay = song.RestartDelayRemaining;
                    Next();
                    break;
                case 8:
                    if (elapsed < .3) return;
                    Require(Pause.instance.IsPaused && song.RestartDelayRemaining == restartDelay, "Pause did not freeze the restart delay.");
                    Pause.instance.ContinueSong();
                    Next();
                    break;
                case 9:
                    countdownGraphic = song.vanillaPlayback.Presentation.GetComponentsInChildren<RawImage>()
                        .FirstOrDefault(image => image.name == "Countdown" && image.texture.name == "ready");
                    if (countdownGraphic == null) return;
                    Pause.instance.PauseSong();
                    int before = song.RestartCount;
                    Pause.instance.RestartSong();
                    Require(song.RestartCount == before + 1 && !song.songStarted && song.RestartDelayRemaining == .5f,
                        "Countdown restart did not reset the existing run.");
                    Next();
                    break;
                case 10:
                    if (elapsed < .1) return;
                    Require(countdownGraphic == null, "Countdown restart left the old graphic alive.");
                    if (!song.songStarted) return;
                    Require(song.RestartCount == 5 && song.musicSources[0].isPlaying && song.stopwatch.IsRunning,
                        "Repeated restart failed to start one new run.");
                    Debug.Log("REPEATED RESTART PASSED: paused delay, countdown cancellation, retained song and fresh audio start.");
                    Pause.instance.PauseSong();
                    Pause.instance.QuitSong();
                    Next();
                    break;
                case 11:
                    if (SceneManager.GetActiveScene().name != "Title" || elapsed < 6) return;
                    Require(!Pause.PracticeMode && Pause.DeathCount == 0 && Time.timeScale == 1, "Exit leaked pause session state.");
                    Require(VanillaFreeplay.Active != null && !VanillaPauseStickers.Active, "Sticker exit did not return to Freeplay.");
                    CheckCampaign();
                    Finish(true);
                    break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void CheckRestartCurves()
    {
        foreach (bool downscroll in new[] { false, true })
        {
            float sign = downscroll ? 1 : -1;
            Require(Mathf.Abs(NoteObject.RestartOffset(.25f, false, downscroll) - sign * 22.5f) < .0001f,
                "Outgoing restart curve differs from expoIn.");
            Require(NoteObject.RestartOffset(.5f, false, downscroll) == sign * 720,
                "Outgoing restart distance differs from the viewport height.");
            Require(NoteObject.RestartOffset(0, true, downscroll) == sign * 200,
                "Incoming restart offset differs from source.");
            Require(Mathf.Abs(NoteObject.RestartOffset(.25f, true, downscroll) - sign * 6.25f) < .0001f,
                "Incoming restart curve differs from expoOut.");
            Require(NoteObject.RestartOffset(.5f, true, downscroll) == 0,
                "Incoming restart did not finish after half a second.");
        }
        Debug.Log("RESTART CURVES PASSED: expoIn, expoOut, distances, duration and downscroll control.");
    }

    private static void CheckPixelFade()
    {
        var material = new Material(Resources.Load<Shader>("FunkinHud/RetryFade"));
        var target = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            RenderTexture.active = target;
            foreach (float amount in new[] { 0f, .5f, 1f })
            {
                GL.Clear(true, true, new Color(.75f, .25f, 1, 1));
                GL.PushMatrix();
                GL.LoadOrtho();
                material.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.Color(new Color(0, 0, 0, amount));
                GL.Vertex3(0, 0, 0);
                GL.Vertex3(0, 1, 0);
                GL.Vertex3(1, 1, 0);
                GL.Vertex3(1, 0, 0);
                GL.End();
                GL.PopMatrix();
                texture.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                texture.Apply();
                Color pixel = texture.GetPixel(2, 2);
                Require(Mathf.Abs(pixel.r - Mathf.Max(0, .75f - amount)) < .01f
                    && Mathf.Abs(pixel.g - Mathf.Max(0, .25f - amount)) < .01f
                    && Mathf.Abs(pixel.b - (1 - amount)) < .01f, "Pixel retry fade did not subtract source color levels.");
            }
            Debug.Log("PIXEL RETRY FADE PASSED: subtraction, clipping, unchanged-color and black controls.");
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }
    }

    private static void CheckCampaign()
    {
        var songs = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
            .Where(item => item.meta.songName == "Bopeebo" || item.meta.songName == "Fresh").OrderBy(item => item.meta.songName).ToList();
        string level = "pause-validation-" + Guid.NewGuid();
        VanillaStoryCampaign.Begin(level, "Normal", songs);
        Require(VanillaStoryCampaign.CompleteSong(songs[0].meta, "Normal", PlayModes.Boyfriend, 500, true), "Campaign control did not advance.");
        VanillaStoryCampaign.ChangeDifficulty("Hard");
        Require(VanillaStoryCampaign.Score == 0 && VanillaStoryCampaign.Difficulty == "Hard" && VanillaStoryCampaign.SongIndex == 1, "Difficulty reset campaign position or retained score.");
        VanillaStoryCampaign.CompleteSong(songs[1].meta, "Hard", PlayModes.Boyfriend, 900, true, false);
        Require(!PlayerPrefs.HasKey(VanillaStoryCampaign.ScoreKey(level, "Hard")), "Practice campaign saved a score.");
        VanillaStoryCampaign.Begin(level, "Normal", songs);
        VanillaStoryCampaign.CompleteSong(songs[0].meta, "Normal", PlayModes.Boyfriend, 500, true);
        VanillaStoryCampaign.CompleteSong(songs[1].meta, "Normal", PlayModes.Boyfriend, 900, true);
        Require(VanillaStoryCampaign.HighScore(level, "Normal") == 1400, "Eligible campaign score control failed.");
        PlayerPrefs.DeleteKey(VanillaStoryCampaign.ScoreKey(level, "Normal"));
        VanillaStoryCampaign.ReturnToMenu();
    }

    private static void Capture(string name, int width, int height, bool blank)
    {
        Canvas canvas = Pause.instance.View.GetComponent<Canvas>();
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        var transforms = canvas.GetComponentsInChildren<Transform>(true);
        int[] layers = transforms.Select(item => item.gameObject.layer).ToArray();
        var host = new GameObject("Pause Capture");
        Camera camera = host.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.gray;
        camera.cullingMask = blank ? 0 : 1 << 31;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(width, height, 24);
        RenderTexture previous = RenderTexture.active;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        try
        {
            foreach (var item in transforms) item.gameObject.layer = 31;
            foreach (var feature in features) feature.SetActive(false);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1000;
            scaler.enabled = false;
            canvas.scaleFactor = Mathf.Min(width / 1280f, height / 720f);
            Canvas.ForceUpdateCanvases();
            File.WriteAllLines(Path.Combine(Output, "render-state.txt"), canvas.GetComponentsInChildren<VanillaPauseText>().Select(text =>
            {
                var mesh = text.canvasRenderer.GetMesh();
                string state = text.Text + " texture=" + text.mainTexture + " rect=" + text.rectTransform.rect + " position="
                    + text.rectTransform.position + " color=" + text.color + " vertices=" + (mesh == null ? 0 : mesh.vertexCount) + " culled=" + text.canvasRenderer.cull;
                return state;
            }));
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            int white = image.GetPixels32().Count(pixel => pixel.r > 220 && pixel.g > 220 && pixel.b > 220);
            File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
            Require(blank ? white == 0 : white > 2500, "Pause render or blank control failed: " + white);
            Object.DestroyImmediate(image);
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            scaler.enabled = true;
            for (int index = 0; index < transforms.Length; index++) transforms[index].gameObject.layer = layers[index];
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = previous;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed)
    {
        finishing = true;
        Time.timeScale = 1;
        SessionState.SetBool("VanillaPauseValidation.Active", false);
        string result = "PAUSE VALIDATION: passed=" + passed + ", errors=" + errors + ", phase=" + phase;
        Debug.Log(result);
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        EditorApplication.Exit(passed ? 0 : 1);
    }
}

