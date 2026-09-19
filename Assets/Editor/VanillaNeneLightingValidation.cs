using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaNeneLightingValidation
{
    private const string Active = "VanillaNeneLightingValidation.Active";
    private static readonly string[] Songs = { "ugh", "guns" };
    private static int index;
    private static int phase;
    private static int errors;
    private static double changed;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_NENE_LIGHTING_PATH") ?? Path.Combine(Path.GetTempPath(), "NeneLighting");

    static VanillaNeneLightingValidation()
    {
        if (!SessionState.GetBool(Active, false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run lighting validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        VanillaWeek7Validation.CheckRim();
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        if (changed == 0) changed = now;
        try
        {
            Require(errors == 0 && now - changed < 120, "Nene lighting validation failed or timed out at phase " + phase);
            if (phase == 0)
            {
                if (now - changed < 4 || Object.FindAnyObjectByType<MenuV2>() == null) return;
                if (index == Songs.Length)
                {
                    Debug.Log("WEEK 7 MIX LIGHTING PASSED: Ugh Pico and Guns Pico, all three characters, seven player and opponent poses, and separate-part controls.");
                    SessionState.SetBool(Active, false);
                    EditorApplication.Exit(0);
                    return;
                }
                OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
                VanillaStoryCampaign.ReturnToMenu();
                Pause.ResetSession();
                var selected = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles")).Single(entry =>
                {
                    var data = JObject.Parse(File.ReadAllText(Path.Combine(entry.meta.songPath, "Vanilla.json")));
                    return (string)data["song"] == Songs[index] && (string)data["variation"] == "pico";
                });
                Song.currentSongMeta = selected.meta;
                Song.difficulty = selected.Difficulty("Hard");
                Song.modeOfPlay = PlayModes.Autoplay;
                SceneManager.LoadScene("Game_Backup3");
                phase = 1;
                changed = now;
            }
            else
            {
                Song song = Song.instance;
                if (song == null || !song.songStarted || song.SongPosition < 6000) return;
                var stage = song.vanillaPlayback.CampaignStage;
                Require(song.musicSources[0].isPlaying && stage.CharacterId(2) == "nene-tankmen", "Pico mix did not load Nene or start audio.");
                CheckPlayerAndOpponent(song);
                var graphic = stage.CharacterGraphic(2);
                bool composite = (bool)typeof(VanillaWeek2Graphic).GetField("compositeRim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graphic);
                Require(composite && graphic.HasRim, "Nene uses separate-part lighting.");
                string after = Path.Combine(Output, Songs[index] + "-after.png");
                VanillaSongValidation.CaptureStage(song, after);
                string mask = Path.Combine(Application.streamingAssetsPath, "Bundles/Week7Assets/effects/neneTankmen_mask.png");
                graphic.SetRim(mask, 15, .1f, new Vector4(-38, -20, -46, -25), new Color32(223, 239, 60, 255), 90, .4f, false);
                graphic.Advance(0, song.mainCamera.transform.position, (float)song.SongPosition / 1000);
                string before = Path.Combine(Output, Songs[index] + "-separate-parts-control.png");
                VanillaSongValidation.CaptureStage(song, before);
                Require(!File.ReadAllBytes(after).SequenceEqual(File.ReadAllBytes(before)), "Separate-part lighting control did not change the rendered image.");
                Debug.Log("NENE LIGHTING CAPTURED: " + Songs[index]);
                Pause.instance.QuitSong();
                index++;
                phase = 0;
                changed = now;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(Active, false);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckPlayerAndOpponent(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        Require(stage.CharacterId(0) == "pico-playable" && stage.CharacterId(1) == "tankman", "Unexpected player or opponent.");
        Vector3 cameraPosition = song.mainCamera.transform.position;
        float cameraSize = song.mainCamera.orthographicSize;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(VanillaWeek2Graphic);
        var actors = (Array)typeof(VanillaCampaignStage).GetField("actors", fields).GetValue(stage);
        foreach (string pose in new[] { "idle", "singLEFT", "singDOWN", "singUP", "singRIGHT", "special", "alternate" })
        {
            for (int side = 0; side < 2; side++)
            {
                string animation = pose == "special" ? side == 0 ? "hey" : "ugh"
                    : pose == "alternate" ? side == 0 ? "singUP-censor" : "hehPrettyGood" : pose;
                object actor = actors.GetValue(side);
                actor.GetType().GetMethod("Play").Invoke(actor, new object[] { animation, false, false });
                var graphic = stage.CharacterGraphic(side);
                Require(graphic.Animation == animation, "Singing pose did not load.");
                Require(graphic.HasRim && (bool)type.GetField("compositeRim", fields).GetValue(graphic), "Actor uses separate-part lighting.");
                Require(Mathf.Approximately((float)type.GetField("rimAngle", fields).GetValue(graphic), side == 0 ? 90 : 25), "Actor rim angle differs from source.");
                Vector4 settings = (Vector4)type.GetField("rimSettings", fields).GetValue(graphic);
                Require(settings.x == 15 && Mathf.Approximately(settings.y, side == 0 ? .1f : .3f), "Actor rim distance or threshold differs from source.");
                Require((Vector4)type.GetField("rimAdjustment", fields).GetValue(graphic) == new Vector4(-38, -20, -46, -25), "Actor color adjustment differs from source.");
                Color expectedColor = new Color32(223, 239, 60, 255);
                Require((Color)type.GetField("rimColor", fields).GetValue(graphic) == expectedColor, "Actor rim color differs from source.");
                Require((Texture2D)type.GetField("rimMask", fields).GetValue(graphic) == null, "Unexpected alternate mask on player or opponent.");
                graphic.Advance(.1f, cameraPosition, (float)song.SongPosition / 1000);
            }
            Bounds bounds = stage.CharacterGraphic(0).GetComponent<MeshRenderer>().bounds;
            bounds.Encapsulate(stage.CharacterGraphic(1).GetComponent<MeshRenderer>().bounds);
            song.mainCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, cameraPosition.z);
            song.mainCamera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / song.mainCamera.aspect) * 1.15f;
            VanillaSongValidation.CaptureStage(song, Path.Combine(Output, Songs[index] + "-actors-" + pose + ".png"));
        }
        song.mainCamera.transform.position = cameraPosition;
        song.mainCamera.orthographicSize = cameraSize;
        Debug.Log("PLAYER AND OPPONENT LIGHTING PASSED: " + Songs[index] + ", source settings and seven rendered poses.");
    }
}
