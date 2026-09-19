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
    private static bool Mix => Environment.GetEnvironmentVariable("UNITY_PARTY_MIX_TEST") == "1";
    private static readonly string[] MixIds = { "bopeebo", "spookeez", "pico", "cocoa", "senpai", "ugh", "stress", "darnell" };
    private static readonly string[] MixTitles = { "Bopeebo (Pico Mix)", "Spookeez (Pico Mix)", "Pico (Pico Mix)", "Cocoa (Pico Mix)", "Senpai (Pico Mix)", "Ugh (Pico Mix)", "Stress (Pico Mix)", "Darnell (BF Mix)" };
    private static string MixVariation => MixIds[campaignIndex] == "darnell" ? "bf" : "pico";
    private static bool Week3 => Environment.GetEnvironmentVariable("UNITY_PARTY_WEEK3_TEST") == "1";
    private static string[] CampaignSongs => Mix ? MixTitles : (Environment.GetEnvironmentVariable("UNITY_PARTY_CAMPAIGN_LIFECYCLE_SONG") ?? "").Split(',');
    private static int campaignIndex;
    private static string CampaignSong => CampaignSongs[campaignIndex];
    private static bool Blazin => song?.vanillaPlayback?.SongId == "blazin";
    private static bool Explosion => song?.vanillaPlayback?.SongId == "2hot";
    private static bool Campaign => Mix || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UNITY_PARTY_CAMPAIGN_LIFECYCLE_SONG"));
    private static int phase;
    private static int errors;
    private static int editorWarnings;
    private static double started;
    private static double changed;
    private static Song song;
    private static bool finishing;
    private static bool mixQuoteObserved;
    private static bool mixQuoteDucked;
    private static string Output => Environment.GetEnvironmentVariable(Campaign ? "UNITY_PARTY_CAMPAIGN_LIFECYCLE_PATH" : Week3 ? "UNITY_PARTY_WEEK3_LIFECYCLE_PATH" : "UNITY_PARTY_WEEK2_LIFECYCLE_PATH")
        ?? Path.GetFullPath(Mix ? "Builds/MixLifecycle" : Week3 ? "Builds/Week3Lifecycle" : "Builds/Week2Lifecycle");

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

    private static VanillaWeek2Graphic PlayerGraphic => (VanillaWeek2Graphic)song.vanillaPlayback.CharacterStage.GetType()
        .GetMethod("CharacterGraphic", BindingFlags.Instance | BindingFlags.Public).Invoke(song.vanillaPlayback.CharacterStage, new object[] { 0 });

    private static void SuppressMixIntro()
    {
        if (!Mix) return;
        typeof(Pause).GetField("sessionSong", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Song.currentSongMeta.songPath);
        Pause.PlayedCampaignIntro = true;
    }

    private static void CheckMixAudio()
    {
        Require(song.deadNoise != null && song.deadTheme != null && song.deadConfirm != null, "Mix game-over audio did not load.");
        string player = song.vanillaPlayback.PlayerId;
        if (player.StartsWith("pico", StringComparison.Ordinal))
        {
            string loss = player == "pico-pixel" ? "pixel-pico" : player == "pico-holding-nene" ? "pico-and-nene" : "pico";
            string music = player == "pico-pixel" ? "pixel-pico" : "pico";
            Require(song.deadNoise == Resources.Load<AudioClip>("FunkinHud/Pico/loss-" + loss)
                && song.deadTheme == Resources.Load<AudioClip>("FunkinHud/Pico/gameOver-" + music)
                && song.deadConfirm == Resources.Load<AudioClip>("FunkinHud/Pico/gameOverEnd-" + music), "Mix selected incorrect game-over audio.");
        }
        else
        {
            Require(song.deadNoise == AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("4e579e7ceec5a9d4eac42afa794d0db7"))
                && song.deadTheme == AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("3d41b1ea33a7a574da3b237d80aa505b"))
                && song.deadConfirm == AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("aac163f43a3770b47892e7de2c215ac8")), "BF mix retained Pico game-over audio.");
        }
    }

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
                        .Single(item => Mix
                            ? (string)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(item.meta.songPath, "Vanilla.json")))["song"] == MixIds[campaignIndex]
                                && (string)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(item.meta.songPath, "Vanilla.json")))["variation"] == MixVariation
                            : item.meta.songName == (Campaign ? CampaignSong : Week3 ? "Pico" : "Spookeez")).meta;
                    if (Mix) Require(Song.currentSongMeta.songName == CampaignSong, "Mix source title changed.");
                    Song.difficulty = !Mix && Song.currentSongMeta.difficulties.ContainsKey("Erect") ? "Erect" : "Hard";
                    Song.modeOfPlay = PlayModes.Boyfriend;
                    SuppressMixIntro();
                    SceneManager.LoadScene("Game_Backup3");
                    Next();
                    break;
                case 1:
                    song = Object.FindAnyObjectByType<Song>();
                    if (song == null || !song.songStarted) return;
                    Require(song.vanillaPlayback.CharacterStage != null && (Blazin || song.OpponentVocals.isPlaying), "Source gameplay did not initialize.");
                    if (Mix)
                    {
                        Require(song.vanillaPlayback.SongId == MixIds[campaignIndex] && song.vanillaPlayback.Variation == MixVariation, "Lifecycle selected the wrong mix.");
                        Require(song.vanillaPlayback.Presentation == null || !song.vanillaPlayback.Presentation.Busy, "Lifecycle mix intro was not suppressed.");
                        CheckMixAudio();
                        mixQuoteObserved = mixQuoteDucked = false;
                    }
                    if (Mix)
                    {
                        VanillaMixValidation.CheckStage(song);
                        VanillaSongValidation.CaptureStage(song, Path.Combine(Output, CampaignSong.Replace(' ', '-') + "-stage.png"));
                    }
                    if (Explosion)
                    {
                        var kinds = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(song.selectedVanillaPath))["noteKinds"][Song.difficulty.ToLowerInvariant()];
                        var fire = kinds.First(note => (string)note["k"] == "weekend-1-firegun");
                        song.health = 40;
                        song.vanillaPlayback.CampaignStage.Miss(0,(int)fire["d"]%4,(double)fire["t"]);
                    }
                    else song.health = 0;
                    Next();
                    break;
                case 2:
                    if (elapsed < (Mix ? .2 : .8)) return;
                    Require(song.isDead && song.deadCamera.enabled && song.deadCamera.orthographic, "Source death camera did not activate.");
                    Require(Death != null && Death.Animation == (Explosion ? "firstDeath-explosion" : "firstDeath") && Death.Frame > 0, "Source death intro did not advance.");
                    if (Mix)
                    {
                        Require(Vector3.Distance(Death.Position, PlayerGraphic.Position) < .00005f, "Mix death lost the source character corner.");
                        Require(Vector3.Distance(Death.GlobalOffset, PlayerGraphic.GlobalOffset) < .00005f, "Mix death lost the source drawing offset.");
                    }
                    Require(song.deadBoyfriend.GetComponentsInChildren<SpriteRenderer>(true).All(renderer => !renderer.enabled), "Legacy death sprite overlaps source atlas.");
                    Require((song.OpponentVocals == null || !song.OpponentVocals.isPlaying) && !song.vocalSource.isPlaying, "Vocal stems continued after death.");
                    if (Mix) Require(song.musicSources[0].isPlaying && song.musicSources[0].clip != song.deadTheme, "Mix death intro sound did not play.");
                    Capture("death-intro.png");
                    Next();
                    break;
                case 3:
                    if (Mix && song.vanillaPlayback.CampaignStage?.Week == 7)
                    {
                        var stage = song.vanillaPlayback.CampaignStage;
                        var quote = (AudioSource)typeof(VanillaCampaignStage).GetField("sound", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
                        if (quote.clip != null && quote.isPlaying)
                        {
                            mixQuoteObserved = true;
                            mixQuoteDucked |= song.musicSources[0].volume <= OptionsV2.instVolume * .21f;
                        }
                    }
                    if (elapsed < (song.vanillaPlayback.CampaignStage?.Week == 8 ? 3.5 : 2)) return;
                    Require(Death.Animation == (Explosion ? "deathLoop-explosion" : "deathLoop") && song.musicSources[0].isPlaying, "Death loop or game-over music did not start.");
                    if (song.vanillaPlayback.CampaignStage?.Week == 7)
                    {
                        var stage = song.vanillaPlayback.CampaignStage;
                        var quote = (AudioSource)typeof(VanillaCampaignStage).GetField("sound", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
                        Require(Mix ? mixQuoteObserved : quote.clip != null && quote.isPlaying, "Tankman death quote did not play.");
                        Require(Mix ? mixQuoteDucked : song.musicSources[0].volume <= OptionsV2.instVolume * .21f, "Death quote did not lower the game-over music.");
                    }
                    if (Mix) Require(song.musicSources[0].clip == song.deadTheme && song.musicSources[0].loop, "Mix game-over theme did not loop.");
                    Capture("death-loop.png");
                    song.vanillaPlayback.CharacterStage.PlayDeath("deathConfirm");
                    if (Mix)
                    {
                        song.musicSources[0].Stop();
                        song.musicSources[0].PlayOneShot(song.deadConfirm);
                    }
                    Next();
                    break;
                case 4:
                    if (elapsed < 0.3) return;
                    Require(Death.Animation == (Explosion ? "deathConfirm-explosion" : "deathConfirm") && Death.Frame > 0, "Death confirm did not advance.");
                    if (song.vanillaPlayback.CampaignStage?.Week == 7)
                    {
                        var quote = (AudioSource)typeof(VanillaCampaignStage).GetField("sound", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(song.vanillaPlayback.CampaignStage);
                        Require(!quote.isPlaying, "Confirm left the death quote playing.");
                    }
                    if (Mix) Require(song.musicSources[0].isPlaying, "Mix game-over confirm sound did not play.");
                    Capture("death-confirm.png");
                    SuppressMixIntro();
                    SceneManager.LoadScene("Game_Backup3");
                    Next();
                    break;
                case 5:
                    song = Object.FindAnyObjectByType<Song>();
                    if (song == null || !song.songStarted) return;
                    Require(!song.isDead && (Campaign ? Death == null : Week3 ? song.vanillaPlayback.Week3Stage.TrainCount == 0 : song.vanillaPlayback.Week2Stage.LightningCount == 0), "Retry retained death or stage effects.");
                    Require(Blazin ? !song.hasVoiceLoaded : song.OpponentVocals.isPlaying && song.vocalSource.isPlaying && song.musicSources.Count(source => source == song.OpponentVocals) == 1,
                        "Retry duplicated or lost vocal sources.");
                    Require(PlayerGraphic.Alpha == 1, "Retry retained faded character.");
                    if (Mix)
                    {
                        Require(PlayerGraphic.gameObject.activeInHierarchy && !song.deadCamera.enabled && song.mainCamera.enabled && Pause.DeathCount == 1 && Pause.PlayedCampaignIntro, "Mix retry lost visible player, camera or session state.");
                        Require(!PlayerGraphic.Animation.StartsWith("death", StringComparison.Ordinal) && PlayerGraphic.Animation != "firstDeath", "Mix retry retained a death animation.");
                        CheckMixAudio();
                    }
                    if (song.vanillaPlayback.CampaignStage?.Week == 8) VanillaWeekend1Validation.CheckRain(song, Output);
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
                var pixels = image.GetPixels32();
                int visible = pixels.Count(pixel => pixel.r > 40 || pixel.g > 40 || pixel.b > 40);
                Require(pass == 0 ? visible > 1000 : visible == 0, "Death render or blank control failed.");
                if (pass == 0 && song.vanillaPlayback.CampaignStage?.Week == 8 && name.EndsWith("death-loop.png"))
                {
                    double x = 0, y = 0;
                    for (int i = 0; i < pixels.Length; i++)
                        if (pixels[i].r > 40 || pixels[i].g > 40 || pixels[i].b > 40) { x += i % 1280; y += i / 1280; }
                    x /= visible * 1280.0;
                    y /= visible * 720.0;
                    Require(x > .2 && x < .8 && y > .2 && y < .8, "Death camera left Pico outside the central viewport: " + x + ", " + y);
                }
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
        string result = (Mix ? "MIX " + (campaignIndex + 1) + "/" + MixTitles.Length + " " + CampaignSong : Campaign ? CampaignSong : Week3 ? "WEEK 3" : "WEEK 2") + " LIFECYCLE: passed=" + passed + ", errors=" + errors + ", editorWarnings=" + editorWarnings + ", phase=" + phase;
        Debug.Log(result);
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
