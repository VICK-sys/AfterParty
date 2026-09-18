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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaWeek2LifecycleValidation
{
    private static bool Week3 => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEK3_TEST") == "1";
    private static string[] CampaignSongs => (Environment.GetEnvironmentVariable("UNITY_PARTY_CAMPAIGN_LIFECYCLE_SONG") ?? "").Split(',');
    private static int campaignIndex;
    private static string CampaignSong => CampaignSongs[campaignIndex];
    private static bool Campaign => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UNITY_PARTY_CAMPAIGN_LIFECYCLE_SONG"));
    private static int phase;
    private static int errors;
    private static int editorWarnings;
    private static double started;
    private static double changed;
    private static Song song;
    private static bool finishing;
    private static string Output => Environment.GetEnvironmentVariable(Campaign ? "UNITY_PARTY_CAMPAIGN_LIFECYCLE_PATH" : Week3 ? "UNITY_PARTY_WEEK3_LIFECYCLE_PATH" : "UNITY_PARTY_WEEK2_LIFECYCLE_PATH")
        ?? Path.GetFullPath(Week3 ? "Builds/Week3Lifecycle" : "Builds/Week2Lifecycle");

    static VanillaWeek2LifecycleValidation()
    {
        if (!SessionState.GetBool("VanillaWeek2LifecycleValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal)
                && stack.Contains("UnityEditor.Search.SearchDatabase") && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
            {
                editorWarnings++;
                return;
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run lifecycle validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaWeek2LifecycleValidation.Active", true);
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

    private static VanillaWeek2Graphic Death => (VanillaWeek2Graphic)song.vanillaPlayback.CharacterStage.GetType()
        .GetField("death", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(song.vanillaPlayback.CharacterStage);

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && EditorApplication.timeSinceStartup - started < 100 * CampaignSongs.Length, "Lifecycle validation failed or timed out.");
            switch (phase)
            {
                case 0:
                    if (elapsed < 7 || Object.FindAnyObjectByType<MenuV2>() == null) return;
                    OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
                    VanillaStoryCampaign.ReturnToMenu();
                    VanillaFreeplay.ReturnToFreeplay = false;
                    Song.currentSongMeta = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
                        .Single(item => item.meta.songName == (Campaign ? CampaignSong : Week3 ? "Pico" : "Spookeez")).meta;
                    Song.difficulty = "Erect";
                    Song.modeOfPlay = PlayModes.Boyfriend;
                    SceneManager.LoadScene("Game_Backup3");
                    Next();
                    break;
                case 1:
                    song = Object.FindAnyObjectByType<Song>();
                    if (song == null || !song.songStarted) return;
                    Require(song.vanillaPlayback.CharacterStage != null && song.OpponentVocals.isPlaying, "Erect gameplay did not initialize.");
                    song.health = 0;
                    Next();
                    break;
                case 2:
                    if (elapsed < 0.8) return;
                    Require(song.isDead && song.deadCamera.enabled && song.deadCamera.orthographic, "Source death camera did not activate.");
                    Require(Death != null && Death.Animation == "firstDeath" && Death.Frame > 0, "Source death intro did not advance.");
                    Require(song.deadBoyfriend.GetComponentsInChildren<SpriteRenderer>(true).All(renderer => !renderer.enabled), "Legacy death sprite overlaps source atlas.");
                    Require(!song.OpponentVocals.isPlaying && !song.vocalSource.isPlaying, "Vocal stems continued after death.");
                    Capture("death-intro.png");
                    Next();
                    break;
                case 3:
                    if (elapsed < 2) return;
                    Require(Death.Animation == "deathLoop" && song.musicSources[0].isPlaying, "Death loop or game-over music did not start.");
                    Capture("death-loop.png");
                    song.vanillaPlayback.CharacterStage.PlayDeath("deathConfirm");
                    Next();
                    break;
                case 4:
                    if (elapsed < 0.3) return;
                    Require(Death.Animation == "deathConfirm" && Death.Frame > 0, "Death confirm did not advance.");
                    Capture("death-confirm.png");
                    SceneManager.LoadScene("Game_Backup3");
                    Next();
                    break;
                case 5:
                    song = Object.FindAnyObjectByType<Song>();
                    if (song == null || !song.songStarted) return;
                    Require(!song.isDead && (Campaign ? Death == null : Week3 ? song.vanillaPlayback.Week3Stage.TrainCount == 0 : song.vanillaPlayback.Week2Stage.LightningCount == 0), "Retry retained death or stage effects.");
                    Require(song.OpponentVocals.isPlaying && song.vocalSource.isPlaying && song.musicSources.Count(source => source == song.OpponentVocals) == 1,
                        "Retry duplicated or lost vocal sources.");
                    Require((Campaign ? song.vanillaPlayback.CampaignStage.CharacterGraphic(0) : Week3 ? song.vanillaPlayback.Week3Stage.CharacterGraphic(0) : song.vanillaPlayback.Week2Stage.CharacterGraphic(0)).Alpha == 1, "Retry retained faded character.");
                    Pause.instance.QuitSong();
                    Next();
                    break;
                case 6:
                    if (SceneManager.GetActiveScene().name != "Title") return;
                    if (Campaign && ++campaignIndex < CampaignSongs.Length)
                    {
                        phase = 0;
                        changed = EditorApplication.timeSinceStartup;
                        break;
                    }
                    if (Campaign) campaignIndex--;
                    Finish(true);
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(false);
        }
    }

    private static void Capture(string name)
    {
        if (Campaign) name = CampaignSong.Replace(' ', '-') + "-" + name;
        var host = new GameObject("Death validation camera");
        Camera camera = host.AddComponent<Camera>();
        camera.CopyFrom(song.deadCamera);
        camera.transform.SetPositionAndRotation(song.deadCamera.transform.position, song.deadCamera.transform.rotation);
        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var target = new RenderTexture(1280, 720, 24);
        RenderTexture previous = RenderTexture.active;
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
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                int visible = image.GetPixels32().Count(pixel => pixel.r > 40 || pixel.g > 40 || pixel.b > 40);
                Require(pass == 0 ? visible > 1000 : visible == 0, "Death render or blank control failed.");
                if (pass == 0) File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = previous;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed)
    {
        finishing = true;
        SessionState.SetBool("VanillaWeek2LifecycleValidation.Active", false);
        string result = (Campaign ? CampaignSong : Week3 ? "WEEK 3" : "WEEK 2") + " LIFECYCLE: passed=" + passed + ", errors=" + errors + ", editorWarnings=" + editorWarnings + ", phase=" + phase;
        Debug.Log(result);
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
