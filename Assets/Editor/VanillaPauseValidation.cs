using System;
using System.IO;
using System.Linq;
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
    private static Vector3 cameraPosition;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_PAUSE_TEST_PATH") ?? Path.GetFullPath("Builds/PauseValidation");

    static VanillaPauseValidation()
    {
        if (!SessionState.GetBool("VanillaPauseValidation.Active", false)) return;
        EditorApplication.update += Tick;
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
                    VanillaStoryCampaign.ReturnToMenu();
                    VanillaFreeplay.ReturnToFreeplay = true;
                    Song.currentSongMeta = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
                        .Single(item => item.meta.songName == "Bopeebo").meta;
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
                            Pause.instance.PauseSong();
                            pausedPosition = song.SongPosition;
                            changed = EditorApplication.timeSinceStartup;
                            countdownPaused = true;
                        }
                        else if (elapsed >= 0.3)
                        {
                            Require(song.SongPosition == pausedPosition && !song.soundSource.isPlaying, "Countdown advanced while paused.");
                            Pause.instance.ContinueSong();
                            Require(song.soundSource.isPlaying, "Countdown audio did not resume.");
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
                    Pause.instance.RestartSong();
                    Next();
                    break;
                case 3:
                    song = Song.instance;
                    if (song == null || ReferenceEquals(song, songInstance) || !song.songStarted) return;
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
                    if (song == null || ReferenceEquals(song, songInstance) || !song.songStarted) return;
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
                    Pause.instance.RestartSong();
                    Next();
                    break;
                case 7:
                    song = Song.instance;
                    if (song == null || ReferenceEquals(song, songInstance) || !song.songStarted) return;
                    Require(Pause.PracticeMode && Song.difficulty == "Hard", "Restart discarded practice mode or difficulty.");
                    Pause.instance.PauseSong();
                    Pause.instance.QuitSong();
                    Next();
                    break;
                case 8:
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

