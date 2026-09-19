using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaResultsValidation
{
    private static int assertions;
    private static int errors;
    private static int phase;
    private static double started;
    private static VanillaResultsScreen screen;
    private static bool callback;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_RESULTS_TEST_PATH") ?? Path.GetFullPath("Temp/Results");

    static VanillaResultsValidation()
    {
        if (!SessionState.GetBool("VanillaResultsValidation.Active", false)) return;
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
        PlayerSettings.productName = "ResultsValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("VanillaResultsValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static VanillaResultsData Data(int rank, string character)
    {
        int sick = new[] { 40, 60, 80, 90, 99, 100 }[rank];
        return new VanillaResultsData
        {
            title = "Bopeebo by Kawai Sprite", difficulty = "hard", characterId = character,
            sick = sick, good = rank == 4 ? 1 : 0, bad = rank < 4 ? 100 - sick : 0,
            totalNotes = 100, totalNotesHit = 100, maxCombo = 100, score = 123456,
            newHighscore = true, rankImproved = true
        };
    }

    public static void CheckData()
    {
        for (int rank = 0; rank < 6; rank++) Require(Data(rank, "bf").Rank == rank, "Rank boundary " + rank);
        foreach (var boundary in new[] { (59, 0), (60, 1), (79, 1), (80, 2), (89, 2), (90, 3), (99, 3) })
            Require(new VanillaResultsData { sick = boundary.Item1, totalNotes = 100 }.Rank == boundary.Item2, "Threshold control " + boundary.Item1);
        var zero = new VanillaResultsData();
        Require(zero.Rank == 0 && zero.ClearPercent == 0, "Empty chart control.");
        var missed = VanillaResultsData.Capture(new PlayerStat { totalNoteHits = 90, hitNotes = 80, totalSicks = 80, missedHits = 10, highestCombo = 70 }, 100);
        Require(missed.missed == 20 && missed.totalNotesHit == 80 && missed.ClearPercent == 60, "Miss and hit tallies.");
        var sum = Data(2, "bf");
        sum.Add(missed);
        Require(sum.totalNotes == 200 && sum.totalNotesHit == 180 && sum.maxCombo == 100 && sum.missed == 20, "Campaign accumulation.");
        Require(Data(1, "pico-pixel").Character == "pico" && Data(1, "bf-car").Character == "bf", "Character ownership.");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        try
        {
            if (errors != 0) throw new InvalidOperationException("Results emitted runtime errors.");
            if (EditorApplication.timeSinceStartup - started >= 240) throw new InvalidOperationException("Results validation timed out at " + phase);
            if (phase == 0)
            {
                new GameObject("Results audio listener", typeof(AudioListener));
                CheckData();
                OptionsV2.menuVolume = OptionsV2.miscVolume = 0.5f;
                CheckScreens();
                phase = 1;
                started = EditorApplication.timeSinceStartup;
                screen = VanillaResultsScreen.Open(Data(3, "pico"), () => callback = true);
            }
            else if (phase == 1 && EditorApplication.timeSinceStartup - started > 0.3)
            {
                var music = screen.GetComponents<AudioSource>();
                Require(music[0].clip != null && music[0].isPlaying, "Pico excellent intro did not start.");
                Require(music[1].timeSamples == 0, "Loop advanced before the intro ended.");
                phase = 2;
            }
            else if (phase == 2 && EditorApplication.timeSinceStartup - started > screen.GetComponents<AudioSource>()[0].clip.length + 0.5)
            {
                var music = screen.GetComponents<AudioSource>();
                Require(!music[0].isPlaying && music[1].isPlaying && music[1].timeSamples > 0, "Results intro did not hand off to its loop.");
                screen.Accept();
                screen.Accept();
                phase = 3;
                started = EditorApplication.timeSinceStartup;
            }
            else if (phase == 3 && callback && VanillaResultsScreen.Active == null)
            {
                Require(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length == 0, "Audio leaked after results closed.");
                Finish(true);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(false);
        }
    }

    private static void CheckScreens()
    {
        foreach (string character in new[] { "bf", "pico" })
        {
            for (int rank = 0; rank < 6; rank++)
            {
                var data = Data(rank, character);
                screen = VanillaResultsScreen.Open(data, null, "intro");
                screen.ManualClock = true;
                Require(screen.MusicPath.EndsWith(character == "pico" ? "-pico" : rank == 0 ? "SHIT" : rank >= 4 ? "PERFECT" : rank == 3 ? "EXCELLENT" : "NORMAL", StringComparison.Ordinal), "Character music.");
                screen.RenderAt(0);
                Require(!screen.transform.Find("Viewport/Results").gameObject.activeSelf, "Results appeared before the pop-in.");
                var characterObjects = screen.GetComponentsInChildren<VanillaFreeplayAnimate>(true);
                Require(characterObjects.Length > 0 && characterObjects.All(a => !a.gameObject.activeSelf), "Character delay control.");
                screen.RenderAt(data.CharacterDelay + 0.1f);
                Require(characterObjects.Any(a => a.gameObject.activeSelf), "Character did not appear.");
                screen.RenderAt(12);
                Require(screen.DisplayedClear == data.ClearPercent, "Final clear percent.");
                Require(screen.transform.Find("Viewport/New Highscore").gameObject.activeSelf, "Highscore marker missing.");
                foreach (var atlas in characterObjects)
                {
                    using (var mesh = new VertexHelper())
                    {
                        typeof(VanillaFreeplayAnimate).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(atlas, new object[] { mesh });
                        Require(mesh.currentVertCount > 0, "Empty character mesh.");
                    }
                    Require(atlas.CurrentFrame < atlas.TotalFrames, "Loop frame outside timeline.");
                }
                Capture(character + "-" + rank + "-12.png");
                File.WriteAllText(Path.Combine(Output, character + "-" + rank + "-metrics.json"), Newtonsoft.Json.JsonConvert.SerializeObject(characterObjects.Select(a => new
                {
                    a.name, frame = a.CurrentFrame, bounds = new[] { a.BoundsSize.x, a.BoundsSize.y },
                    position = new[] { a.rectTransform.anchoredPosition.x, -a.rectTransform.anchoredPosition.y }
                }), Newtonsoft.Json.Formatting.Indented));
                CaptureCharacters(character + "-" + rank + "-characters.png");
                if (rank == 3)
                {
                    screen.RenderAt(2);
                    Capture(character + "-tally.png");
                    screen.RenderAt(5);
                    Capture(character + "-reveal.png");
                }
                Object.DestroyImmediate(screen.gameObject);
            }
        }
        foreach (string variant in new[] { "intro fat gf", "intro cass" })
        {
            screen = VanillaResultsScreen.Open(Data(1, "pico"), null, variant);
            screen.ManualClock = true;
            screen.RenderAt(9);
            Capture("pico-" + variant.Replace(' ', '-') + ".png");
            CaptureCharacters(variant == "intro cass" ? "pico-cass-characters.png" : "pico-fat-characters.png");
            screen.RenderAt(60);
            Require(screen.GetComponentInChildren<VanillaFreeplayAnimate>().CurrentFrame >= 41, "Pico variant did not reach its loop.");
            Object.DestroyImmediate(screen.gameObject);
        }
        var safe = Data(5, "bf");
        safe.naughty = false;
        safe.newHighscore = false;
        screen = VanillaResultsScreen.Open(safe, null);
        screen.ManualClock = true;
        screen.RenderAt(12);
        Require(screen.GetComponentsInChildren<VanillaFreeplayAnimate>().Any(a => a.name.Contains("tickleFight")), "Safe perfect animation missing.");
        Require(screen.transform.Find("Viewport/New Highscore") == null, "False highscore control.");
        Capture("bf-safe-perfect.png");
        CaptureCharacters("bf-safe-characters.png");
        Object.DestroyImmediate(screen.gameObject);
        foreach (int rank in new[] { 1, 2 })
        {
            screen = VanillaResultsScreen.Open(Data(rank, "pico"), null, "intro");
            screen.ManualClock = true;
            screen.RenderAt(5.8f);
            CaptureCharacters(rank == 1 ? "pico-flash-characters.png" : "pico-great-flash-characters.png");
            Object.DestroyImmediate(screen.gameObject);
        }
    }

    private static void CaptureCharacters(string name)
    {
        var viewport = screen.transform.Find("Viewport");
        var children = viewport.Cast<Transform>().ToArray();
        var enabled = children.Select(child => child.gameObject.activeSelf).ToArray();
        for (int i = 0; i < children.Length; i++) children[i].gameObject.SetActive(enabled[i] && children[i].name.StartsWith("Character ", StringComparison.Ordinal));
        Capture(name);
        for (int i = 0; i < children.Length; i++) children[i].gameObject.SetActive(enabled[i]);
    }

    private static void Capture(string filename)
    {
        var canvas = screen.GetComponent<Canvas>();
        var scaler = screen.GetComponent<CanvasScaler>();
        var cameraObject = new GameObject("Results Capture", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(254, 204, 92, 255);
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(1280, 720, 24);
        var features = AssetDatabase.FindAssets("t:UniversalRendererData").Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(f => f != null && f.isActive).Distinct().ToArray();
        try
        {
            foreach (var transform in screen.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
            foreach (var feature in features) feature.SetActive(false);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1000;
            scaler.enabled = false;
            canvas.scaleFactor = 1;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            Require(image.GetPixels().Average(c => c.r + c.g + c.b) > 0.5, "Blank results render.");
            File.WriteAllBytes(Path.Combine(Output, filename), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            scaler.enabled = true;
            foreach (var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed)
    {
        SessionState.SetBool("VanillaResultsValidation.Active", false);
        string result = "RESULTS VALIDATION: passed=" + passed + ", assertions=" + assertions + ", errors=" + errors + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        Debug.Log(result);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
