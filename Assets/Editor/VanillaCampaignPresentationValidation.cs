using System;
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
public static class VanillaCampaignPresentationValidation
{
    private static readonly string[] Songs = { "Senpai", "Roses", "Thorns", "Eggnog" };
    private static int index = int.TryParse(Environment.GetEnvironmentVariable("UNITY_PARTY_PRESENTATION_START"), out int start) ? start : 0;
    private static int phase;
    private static int errors;
    private static bool captured;
    private static bool finishing;
    private static double changed;
    private static double lastAdvance;
    private static int exitAttempts;
    private static bool skipArmed;
    private static double skipStarted;
    private static bool sawSkipFade;
    private static bool SkipTest => Environment.GetEnvironmentVariable("UNITY_PARTY_PRESENTATION_SKIP_TEST") == "1";
    private static Song song;
    private static bool InputTest => Environment.GetEnvironmentVariable("UNITY_PARTY_PRESENTATION_INPUT_TEST") == "1";
    private static readonly MethodInfo manualStartExit = typeof(Song).GetMethod("HandleManualStartExit", BindingFlags.Instance | BindingFlags.NonPublic);
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_PRESENTATION_TEST_PATH");

    static VanillaCampaignPresentationValidation()
    {
        if (!SessionState.GetBool("VanillaCampaignPresentationValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run presentation validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaCampaignPresentationValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Next(int next)
    {
        phase = next;
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (changed == 0) changed = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup-changed;
        try
        {
            Require(errors == 0 && elapsed < 90, "Presentation failed or timed out at phase " + phase);
            if (phase == 0)
            {
                if (elapsed < 7 || Object.FindAnyObjectByType<MenuV2>() == null) return;
                OptionsV2.DesperateMode = OptionsV2.LiteMode = false;
                Pause.ResetSession();
                var item = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath,"Bundles"))
                    .Single(entry => entry.meta.songName == Songs[index]);
                VanillaStoryCampaign.Begin("presentation-validation", index == 3 ? "Erect" : "Hard", new System.Collections.Generic.List<VanillaFreeplaySong> { item });
                Song.modeOfPlay = PlayModes.Autoplay;
                if (InputTest) Player.pauseKey = Player.keybinds.pauseKeyCode = KeyCode.Return;
                captured = false;
                exitAttempts = 0;
                SceneManager.LoadScene("Game_Backup3");
                Next(1);
            }
            else if (phase == 1)
            {
                song = Object.FindAnyObjectByType<Song>();
                if (song == null || song.vanillaPlayback?.Presentation == null) return;
                var presentation = song.vanillaPlayback.Presentation;
                if (index == 3)
                {
                    if (!song.songStarted) return;
                    foreach (AudioSource source in song.musicSources) source.Stop();
                    song.respawning = true;
                    Require(!presentation.OwnsCamera, "Camera ownership control failed before the outro.");
                    Require(!presentation.AllowEnd(song), "Eggnog outro did not delay completion.");
                    Next(3);
                    return;
                }
                Text dialogue = presentation.GetComponentsInChildren<Text>().FirstOrDefault(text => text.name == "Dialogue");
                if (dialogue == null || dialogue.text.Length < 12) return;
                Require(presentation.Busy && !song.songStarted && !song.IsCountingDown, "Dialogue overlaps gameplay or countdown.");
                Require(dialogue.font != null, "Dialogue font did not load.");
                Capture(Songs[index]+"-dialogue.png", presentation);
                captured = true;
                Next(2);
            }
            else if (phase == 2)
            {
                if (InputTest)
                    Require(SceneManager.GetActiveScene().name == "Game_Backup3" && VanillaStoryCampaign.Running
                        && !LoadingTransition.instance.toggled && !Pause.instance.Transitioning && !Pause.instance.IsPaused,
                        "Dialogue Enter escaped or paused the campaign.");
                if (EditorApplication.timeSinceStartup-lastAdvance > .5)
                {
                    if (InputTest)
                    {
                        manualStartExit.Invoke(song, new object[] { true });
                        Require(!LoadingTransition.instance.toggled, "Cutscene input triggered the manual-start exit handler.");
                        exitAttempts++;
                    }
                    song.vanillaPlayback.Presentation.AdvanceDialogue();
                    lastAdvance = EditorApplication.timeSinceStartup;
                }
                if (!song.songStarted) return;
                Require(captured && !song.vanillaPlayback.Presentation.Busy && song.musicSources[0].isPlaying, "Dialogue did not start gameplay.");
                Require(Pause.PlayedCampaignIntro, "Retry did not retain intro state.");
                if (InputTest) Require(exitAttempts > 1, "Dialogue input regression did not exercise the exit handler.");
                Debug.Log("PRESENTATION PASSED: " + Songs[index] + ", dialogue render, advance, countdown, gameplay.");
                if (InputTest) Debug.Log("DIALOGUE INPUT PASSED: " + Songs[index] + ", exit attempts=" + exitAttempts + ", campaign retained.");
                if (InputTest && index == 2)
                {
                    song.vanillaPlayback = null;
                    song.songStarted = false;
                    foreach (AudioSource source in song.musicSources) source.Stop();
                    manualStartExit.Invoke(song, new object[] { false });
                    Require(!LoadingTransition.instance.toggled, "Manual-start control exited without input.");
                    manualStartExit.Invoke(song, new object[] { true });
                    Require(LoadingTransition.instance.toggled, "Manual-start exit control did not reach the transition.");
                    Debug.Log("DIALOGUE INPUT CONTROL PASSED: manual-start exit still works, no-input control stays in gameplay.");
                    Finish(true);
                    return;
                }
                ExitSong();
            }
            else if (phase == 3)
            {
                var presentation = song.vanillaPlayback.Presentation;
                Require(presentation.OwnsCamera, "Eggnog did not claim the cutscene camera.");
                Vector3 position = song.mainCamera.transform.position;
                OptionsV2.LiteMode = true;
                song.vanillaPlayback.MoveCamera(song.mainCamera);
                OptionsV2.LiteMode = false;
                Require(song.mainCamera.transform.position == position, "Gameplay overwrote the outro camera.");
                var santa = GameObject.Find("Santa Outro");
                if (santa == null && !presentation.OutroFinished)
                {
                    Require(elapsed < 5, "Eggnog cutscene preparation did not finish.");
                    return;
                }
                Require(!song.vanillaPlayback.CampaignStage.CharacterGraphic(1).gameObject.activeSelf,
                    "The normal parents reappeared during the Eggnog outro.");
                Require(!song.vanillaPlayback.CampaignStage.PropGraphic("santa").gameObject.activeSelf,
                    "The normal Santa overlaps the Eggnog outro.");
                if (santa != null)
                {
                    var graphic = santa.GetComponent<VanillaWeek2Graphic>();
                    var standing = song.vanillaPlayback.CampaignStage.PropGraphic("santa");
                    float floor = standing.Position.y - standing.FrameSize.y / 100;
                    Require(Mathf.Abs(graphic.Position.y - 7.57457456f - floor) < .0001f,
                        "Santa cutscene feet do not align with the stage floor.");
                    Require(Mathf.Abs(-1 - 7.57457456f - floor) > .4f,
                        "Original Santa position negative control did not expose the vertical gap.");
                }
                Require(song.vanillaPlayback.CampaignStage.CharacterGraphic(0).gameObject.activeSelf,
                    "Eggnog outro visibility control hid Boyfriend.");
                if (!captured && elapsed > 4)
                {
                    Require(presentation.Busy && GameObject.Find("Santa Outro") != null && GameObject.Find("Parents Outro") != null,
                        "Eggnog cutscene actors did not load.");
                    VanillaSongValidation.CaptureStage(song,Path.Combine(Output,"Eggnog-outro.png"));
                    captured = true;
                }
                if (SkipTest && elapsed > 4 && !skipArmed)
                {
                    presentation.AdvanceDialogue();
                    skipArmed = true;
                }
                else if (SkipTest && elapsed > 4.8 && skipStarted == 0)
                {
                    presentation.AdvanceDialogue();
                    skipStarted = EditorApplication.timeSinceStartup;
                }
                if (skipStarted > 0)
                {
                    double skipAge = EditorApplication.timeSinceStartup - skipStarted;
                    if (skipAge > .15 && skipAge < .4)
                    {
                        Require(!presentation.OutroFinished, "Skip ended before its fade completed.");
                        Require(presentation.GetComponentsInChildren<Image>().Any(image => image.color == Color.black
                            || image.color.r == 0 && image.color.a > 0 && image.color.a < 1), "Skip fade did not render.");
                        sawSkipFade = true;
                    }
                }
                if (!presentation.OutroFinished) return;
                if (SkipTest) Require(sawSkipFade && elapsed < 8, "Skip fade or completion timing failed.");
                Require(captured && presentation.AllowEnd(song), "Eggnog did not release song completion.");
                Debug.Log("PRESENTATION PASSED: Eggnog Erect, cutscene actors, timed outro, completion.");
                ExitSong();
            }
            else if (phase == 4)
            {
                if (SceneManager.GetActiveScene().name != "Title") return;
                if (++index == Songs.Length) Finish(true);
                else Next(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void ExitSong()
    {
        song.respawning = false;
        VanillaStoryCampaign.ReturnToMenu();
        Pause.instance.QuitSong();
        Next(4);
    }

    private static void Capture(string name, VanillaCampaignPresentation presentation)
    {
        CaptureOverlay(Path.Combine(Output, name), presentation, index < 3);
    }

    public static void CaptureOverlay(string path, VanillaCampaignPresentation presentation, bool dialogue)
    {
        Canvas canvas = presentation.GetComponentInChildren<Canvas>();
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        var transforms = canvas.GetComponentsInChildren<Transform>(true);
        int[] layers = transforms.Select(item => item.gameObject.layer).ToArray();
        var host = new GameObject("Presentation Capture");
        Camera camera = host.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.gray;
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(0,0,-1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(1280,720,24);
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
            canvas.scaleFactor = 1;
            Canvas.ForceUpdateCanvases();
            if (dialogue)
                foreach (var graphic in canvas.GetComponentsInChildren<VanillaDialogueGraphic>())
                    Require(graphic.canvasRenderer != null && graphic.canvasRenderer.GetMesh()?.vertexCount > 0,
                        "Dialogue artwork did not create a canvas mesh: " + graphic.name);
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) camera.cullingMask = 0;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);
                image.Apply();
                int colored = image.GetPixels32().Count(pixel => dialogue
                    ? Math.Abs(pixel.r-pixel.g) > 45 || Math.Abs(pixel.g-pixel.b) > 45 : pixel.r > 200 || pixel.g > 200 || pixel.b > 200);
                if (pass == 0) File.WriteAllBytes(path,image.EncodeToPNG());
                Require(pass == 0 ? colored > (dialogue ? 3000 : 1000) : colored == 0,"Presentation artwork render or blank control failed: " + colored);
                Object.DestroyImmediate(image);
            }
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            scaler.enabled = true;
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = previous;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed)
    {
        finishing = true;
        SessionState.SetBool("VanillaCampaignPresentationValidation.Active", false);
        string result = "PRESENTATION VALIDATION: passed="+passed+", errors="+errors+", song="+index+", phase="+phase;
        Debug.Log(result);
        File.WriteAllText(Path.Combine(Output,"result.txt"),result+"\n");
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
