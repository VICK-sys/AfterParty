using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaCampaignFixValidation
{
    private static readonly string[] Songs = { "Winter Horrorland", "Thorns", "Thorns", "Senpai", "Roses", "Senpai" };
    private static readonly string[] Difficulties = { "Hard", "Hard", "Erect", "Erect", "Erect", "Hard" };
    private static int index;
    private static int phase;
    private static int errors;
    private static bool finishing;
    private static bool sawLights;
    private static double changed;
    private static Song song;
    private static Vector3 pausedPosition;
    private static float pausedSize;
    private static float pausedAlpha;
    private static double pausedClock;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_CAMPAIGN_FIX_TEST_PATH");

    static VanillaCampaignFixValidation()
    {
        if (!SessionState.GetBool("VanillaCampaignFixValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run campaign validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaCampaignFixValidation.Active", true);
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
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && elapsed < 75, "Campaign validation failed or timed out at phase " + phase);
            if (phase == 0)
            {
                if (elapsed < 7 || Object.FindAnyObjectByType<MenuV2>() == null) return;
                if (index == 0 && int.TryParse(Environment.GetEnvironmentVariable("UNITY_PARTY_CAMPAIGN_FIX_TEST_START"), out int start)) index = start;
                OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
                Pause.ResetSession();
                VanillaStoryCampaign.ReturnToMenu();
                var item = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
                    .Single(entry => entry.meta.songName == Songs[index]);
                Song.currentSongMeta = item.meta;
                Song.difficulty = item.Difficulty(Difficulties[index]);
                Song.modeOfPlay = PlayModes.Autoplay;
                SceneManager.LoadScene("Game_Backup3");
                Next(1);
            }
            else if (phase == 1)
            {
                song = Object.FindAnyObjectByType<Song>();
                if (song?.vanillaPlayback?.CampaignStage == null) return;
                if (index != 0)
                {
                    if (!song.songStarted) return;
                    CheckSchool();
                    ExitSong();
                    return;
                }
                var presentation = song.vanillaPlayback.Presentation;
                if (presentation.Busy && Vector3.Distance(song.mainCamera.transform.position, new Vector3(4, 20.5f, -10)) < .001f)
                {
                    sawLights = true;
                    Require(!song.uiCamera.enabled && !song.battleCanvas.enabled, "Winter lights revealed the HUD early.");
                }
                if (!song.IsCountingDown || presentation.HudAlpha < .2f) return;
                Require(sawLights && presentation.OwnsCamera && presentation.HudAlpha < .8f, "Winter skipped the camera or HUD transition.");
                Require(song.mainCamera.orthographicSize > 1.5f && song.mainCamera.orthographicSize < 3.6f / song.vanillaPlayback.CampaignStage.CameraZoom,
                    "Winter zoom did not interpolate.");
                var block = new MaterialPropertyBlock();
                song.player1NoteSprites[0].GetPropertyBlock(block);
                Require(Mathf.Abs(block.GetFloat("_HudOpacity") - presentation.HudAlpha) < .001f, "Winter strums did not fade with the HUD.");
                Pause.instance.PauseSong();
                pausedClock = song.SongPosition;
                Next(2);
            }
            else if (phase == 2)
            {
                if (elapsed < .15) return;
                pausedPosition = song.mainCamera.transform.position;
                pausedSize = song.mainCamera.orthographicSize;
                pausedAlpha = song.vanillaPlayback.Presentation.HudAlpha;
                Next(3);
            }
            else if (phase == 3)
            {
                if (elapsed < .4) return;
                Require(Pause.instance.IsPaused && Math.Abs(song.SongPosition - pausedClock) < .001, "Paused countdown clock advanced.");
                Require(Vector3.Distance(pausedPosition, song.mainCamera.transform.position) < .0001f
                    && Mathf.Abs(pausedSize - song.mainCamera.orthographicSize) < .0001f
                    && Mathf.Abs(pausedAlpha - song.vanillaPlayback.Presentation.HudAlpha) < .0001f, "Winter transition advanced while paused.");
                Pause.instance.ContinueSong();
                Next(4);
            }
            else if (phase == 4)
            {
                if (!song.songStarted || song.vanillaPlayback.Presentation.OwnsCamera) return;
                var stage = song.vanillaPlayback.CampaignStage;
                Require(song.vanillaPlayback.Presentation.HudAlpha == 1 && song.musicSources[0].isPlaying, "Winter did not finish its handoff.");
                Require(Vector3.Distance(song.mainCamera.transform.position, song.vanillaPlayback.CameraFocusTarget) < .05f,
                    "Winter returned to the wrong focus target.");
                Require(Mathf.Abs(song.mainCamera.orthographicSize - song.vanillaPlayback.CameraSize) < .03f, "Winter zoom snapped at countdown completion.");
                Require(stage.PropGraphic("evilSnow").GetComponent<MeshRenderer>().sortingOrder > stage.PropGraphic("evilTree").GetComponent<MeshRenderer>().sortingOrder,
                    "Winter snow renders behind the tree.");
                VanillaSongValidation.CaptureStage(song, Path.Combine(Output, "winter-layering.png"));
                Pause.instance.RestartSong();
                Next(5);
            }
            else if (phase == 5)
            {
                var restarted = Object.FindAnyObjectByType<Song>();
                if (restarted == null || restarted == song || restarted.vanillaPlayback?.Presentation == null) return;
                Require(!restarted.vanillaPlayback.Presentation.Busy && !restarted.vanillaPlayback.Presentation.OwnsCamera,
                    "Retry replayed the Winter intro.");
                if (!restarted.songStarted) return;
                song = restarted;
                Require(song.vanillaPlayback.Presentation.HudAlpha == 1, "Retry retained the faded HUD.");
                Debug.Log("CAMPAIGN FIX PASSED: Winter layering, camera handoff, HUD fade, pause, audio start, retry.");
                ExitSong();
            }
            else if (phase == 6)
            {
                if (SceneManager.GetActiveScene().name != "Title") return;
                if (++index == Songs.Length) Finish(true);
                else Next(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void CheckSchool()
    {
        var stage = song.vanillaPlayback.CampaignStage;
        Require(FunkinHudAssets.Icon(stage.CharacterId(1))[0].texture.filterMode == FilterMode.Point, "Week 6 opponent icon uses interpolation.");
        Require(FunkinHudAssets.Icon("bf")[0].texture.filterMode == FilterMode.Bilinear, "Normal icon filtering control failed.");
        Pause.instance.PauseSong();
        stage.ResetStage();
        if (Songs[index] == "Thorns")
        {
            var spirit = stage.CharacterGraphic(1);
            spirit.Play("idle");
            spirit.Advance(0, song.mainCamera.transform.position, 0);
            Require(Vector3.Distance(spirit.transform.localPosition - spirit.Position, new Vector3(1.86f, -2.4f, 0)) < .0001f,
                "Spirit render position omits the scaled source global offset.");
            Require(Vector3.Distance(spirit.transform.localPosition, new Vector3(-1.67f, -1.6f, 0)) < .0001f,
                "Spirit does not match the source idle position.");
            Vector3 idle = spirit.transform.localPosition;
            spirit.Play("singDOWN");
            spirit.Advance(0, song.mainCamera.transform.position, 0);
            Require(Vector3.Distance(spirit.transform.localPosition - idle, new Vector3(-3.9f, 3.84f, 0)) < .0001f,
                "Spirit singing offsets do not use pixel scale.");
            spirit.Play("idle");
            song.mainCamera.transform.position = stage.CameraTargets[1];
            foreach (var graphic in stage.GetComponentsInChildren<VanillaWeek2Graphic>()) graphic.Advance(0, song.mainCamera.transform.position, 0);
        }
        else
        {
            bool erect = Difficulties[index] == "Erect";
            Require(stage.StageId == (erect ? "schoolErect" : "school"), "Wrong school stage variation.");
            Require(stage.PropGraphic("freaks").Alpha == (erect ? 0 : 1), "School crowd visibility differs from source.");
            var crowdMaterial = stage.PropGraphic("freaks").GetComponent<MeshRenderer>().sharedMaterial;
            Require(crowdMaterial.GetFloat("_Opacity") == (erect ? 0 : 1), "School crowd alpha did not reach the shader.");
            if (Songs[index] == "Senpai") Require(stage.CharacterGraphic(1).Size == new Vector2(124, 163), "Senpai placement uses the wrong initial frame size.");
            if (erect)
            {
                JObject data = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Bundles/Week6Assets/stages/schoolErect/stage.json")));
                foreach (var prop in data["props"])
                {
                    var graphic = stage.PropGraphic((string)prop["name"]);
                    Require(Vector3.Distance(graphic.Position, new Vector3((float)prop["position"][0] / 100, -(float)prop["position"][1] / 100, 0)) < .001f,
                        "School Erect prop position differs from source.");
                    Require(((Texture2D)graphic.GetComponent<MeshRenderer>().sharedMaterial.mainTexture).filterMode == FilterMode.Point,
                        "School Erect prop filtering changed.");
                }
                for (int side = 0; side < 3; side++)
                {
                    var material = stage.CharacterGraphic(side).GetComponent<MeshRenderer>().sharedMaterial;
                    Require(material.GetTexture("_RimMask") != null && material.GetVector("_Rim").x == (side == 2 ? 3 : 5),
                        "School Erect character rim lighting missing: " + side + ", " + material.shader.name + ", " + material.GetVector("_Rim")
                        + ", mask=" + material.GetTexture("_RimMask"));
                }
            }
        }
        string capture = Path.Combine(Output, Songs[index].ToLowerInvariant() + "-" + Difficulties[index].ToLowerInvariant() + ".png");
        VanillaSongValidation.CaptureStage(song, capture);
        if (index == 3)
        {
            var materials = Enumerable.Range(0, 3).Select(side => stage.CharacterGraphic(side).GetComponent<MeshRenderer>().sharedMaterial).ToArray();
            var settings = materials.Select(material => material.GetVector("_Rim")).ToArray();
            foreach (var material in materials) material.SetVector("_Rim", Vector4.zero);
            string control = Path.Combine(Output, "senpai-unlit-control.png");
            VanillaSongValidation.CaptureStage(song, control);
            for (int side = 0; side < 3; side++) materials[side].SetVector("_Rim", settings[side]);
            var lit = new Texture2D(2, 2);
            var unlit = new Texture2D(2, 2);
            lit.LoadImage(File.ReadAllBytes(capture));
            unlit.LoadImage(File.ReadAllBytes(control));
            int changedPixels = lit.GetPixels32().Zip(unlit.GetPixels32(), (a, b) => a.r != b.r || a.g != b.g || a.b != b.b).Count(value => value);
            Object.DestroyImmediate(lit);
            Object.DestroyImmediate(unlit);
            Require(changedPixels > 10000, "School Erect lighting does not affect the rendered characters.");
        }
        Pause.instance.ContinueSong();
        Debug.Log("CAMPAIGN FIX PASSED: " + Songs[index] + "/" + Difficulties[index] + ", source placement, stage presentation, icon filtering.");
    }

    private static void ExitSong()
    {
        Pause.instance.QuitSong();
        Next(6);
    }

    private static void Finish(bool passed)
    {
        finishing = true;
        SessionState.SetBool("VanillaCampaignFixValidation.Active", false);
        string result = "CAMPAIGN FIX VALIDATION: passed=" + passed + ", errors=" + errors + ", song=" + index + ", phase=" + phase;
        Debug.Log(result);
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
