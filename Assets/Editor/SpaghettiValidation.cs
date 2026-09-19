using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class SpaghettiValidation
{
    private static int capture;
    private static JToken[] events;
    private static readonly int[] CaptureTimes = { 14000, 23000, 31000, 40000, 65000, 115000, 134000 };
    private static double pausedAt;
    private static float pausedClock;
    private static bool pauseChecked;
    private static Song introSong;
    private static int introCapture;
    private static float armedAt;
    private static bool skipRequested;
    private static int endingCard;
    private static bool introCoverChecked;

    public static void ObserveIntro(Song song)
    {
        if (introSong != song)
        {
            introSong = song;
            introCapture = 0;
            armedAt = 0;
            skipRequested = false;
            introCoverChecked = false;
        }
        var presentation = song.vanillaPlayback?.Presentation;
        if (presentation == null) return;
        if (!introCoverChecked && song.vanillaPlayback.CampaignStage != null
            && LoadingTransition.instance != null && LoadingTransition.instance.toggled && LoadingTransition.instance.Progress == 1)
        {
            Require(presentation.Busy && !song.uiCamera.enabled && !song.battleCanvas.enabled,
                "Spaghetti exposed gameplay while the loading screen cleared.");
            Canvas.ForceUpdateCanvases();
            var cover = presentation.GetComponentsInChildren<UnityEngine.UI.Image>().Single(item => item.name == "Overlay");
            bool OpaqueCover() => cover.gameObject.activeInHierarchy && cover.color == Color.black
                && cover.rectTransform.rect.width >= 1280 && cover.rectTransform.rect.height >= 720
                && cover.canvasRenderer.GetMesh()?.vertexCount > 0;
            Require(OpaqueCover(), "Spaghetti intro cover did not render before loading completed.");
            cover.color = Color.clear;
            bool transparentRejected = !OpaqueCover();
            cover.color = Color.black;
            Require(transparentRejected, "Transparent intro cover control passed.");
            introCoverChecked = true;
            Debug.Log("SPAGHETTI INTRO COVER PASSED: loading transition covered, HUD hidden, transparent control rejected.");
        }
        if (!presentation.Busy) return;
        float time = presentation.SpaghettiIntroTime;
        if (Song.difficulty == "Normal" && time > 3)
        {
            if (armedAt == 0)
            {
                presentation.AdvanceDialogue();
                armedAt = time;
            }
            else if (!skipRequested && time > armedAt + .8f)
            {
                Require(!song.IsCountingDown && !song.songStarted, "First advance skipped the intro without arming.");
                presentation.AdvanceDialogue();
                skipRequested = true;
            }
        }
        int[] times = { 3, 18, 29 };
        if (Song.difficulty == "Hard" && introCapture < times.Length && time >= times[introCapture])
        {
            string file = "intro-" + times[introCapture++] + ".png";
            VanillaSongValidation.CaptureStage(song, Path.Combine(Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_TEST_PATH"), file));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void CheckAssets()
    {
        VanillaSongValidation.CheckCharts();
        var level = VanillaStoryCatalog.Load().Single(item => item.id == "sserafim");
        foreach (string difficulty in new[] { "Easy", "Normal", "Hard" })
        {
            Require(VanillaStoryCatalog.TryPlaylist(level, difficulty, out var playlist, out _), "Spaghetti Story playlist is missing.");
            Require(playlist.Count == 1 && playlist[0].Album(difficulty) == "spaghetti", "Spaghetti album or playlist differs.");
            Require(playlist[0].Difficulty("Erect") == null, "Unavailable remix control failed.");
        }
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/SpaghettiAssets");
        foreach (string path in Directory.GetFiles(root, "graphic.json", SearchOption.AllDirectories))
        {
            var graphic = JObject.Parse(File.ReadAllText(path));
            foreach (JProperty clip in ((JObject)graphic["animations"]).Properties())
                Require(clip.Value["frames"].Values<int>().All(index => index >= 0 && index < graphic["frames"].Count()), "Animation crosses atlas bounds: " + path);
        }
        Color bright = VanillaCampaignStage.SpaghettiLighting(.5f, 1, 0, Color.white, false);
        Color dark = VanillaCampaignStage.SpaghettiLighting(.8f, 1, 0, Color.white, false);
        Require(Mathf.Abs(bright.r - .30745f) < .00001f && Mathf.Abs(dark.r - .17145f) < .00001f, "Spaghetti lighting threshold differs from the source.");
        Require(VanillaCampaignStage.SpaghettiLighting(.8f, 1, 0, Color.white, true).r > dark.r * 3, "Character lighting control failed.");
        Debug.Log("SPAGHETTI ASSETS PASSED: three playlists, 166 charts, atlas indices, lighting, and unavailable-remix control.");
    }

    public static void CheckStage(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        Require(stage != null && stage.Week == 9 && stage.StageId == "sserafim", "Diner stage did not load.");
        Require(song.defaultSceneObjects.All(item => !item.activeSelf), "Original stage overlaps diner.");
        Require(song.player2NoteSprites.All(item => !item.enabled), "Opponent receptors are visible.");
        float introTime = song.vanillaPlayback.Presentation.SpaghettiIntroTime;
        Require(introCoverChecked, "Spaghetti intro loading coverage was not observed.");
        Require(Song.difficulty == "Normal" ? skipRequested && introTime < 5 : introTime > 30,
            "Intro duration or armed skip differs from the source.");
        JObject chart = JObject.Parse(File.ReadAllText(song.selectedVanillaPath));
        events = chart["events"].OrderBy(item => (double)item["t"]).ToArray();
        for (int active = 0; active < 6; active++)
        {
            stage.ResetStage();
            stage.SpaghettiEvent("sserafimSing", new JObject { ["singing"] = new JArray(Enumerable.Range(0, 6).Select(index => index == active)) }, 0);
            stage.Sing(0, 2, false);
            for (int i = 0; i < 6; i++)
                Require((stage.SpaghettiCharacter(i).Animation == "singUP") == (i == active), "Performer routing or inactive-performer control failed.");
        }
        stage.SpaghettiEvent("sserafimSing", new JObject { ["singing"] = new JArray(false, false, false, false, true, false) }, 0);
        foreach (string kind in new[] { "sakura-joint", "sakura-bf1", "sakura-bf2" })
        {
            JToken note = chart["noteKinds"][Song.difficulty.ToLowerInvariant()].First(item => (string)item["k"] == kind);
            int direction = (int)note["d"] % 4;
            stage.Hit(0, direction, (double)note["t"]);
            Require(stage.SpaghettiCharacter(4).Animation.EndsWith("-" + kind.Substring(7)), "Sakura hit variant differs.");
            stage.Miss(0, direction, (double)note["t"]);
            Require(stage.SpaghettiCharacter(4).Animation.Contains("miss") && stage.SpaghettiCharacter(4).Animation.EndsWith(kind == "sakura-joint" ? "-joint" : "-bf2"), "Sakura miss variant differs.");
        }
        stage.SpaghettiEvent("sserafimSing", new JObject { ["singing"] = new JArray(false, false, false, false, false, true) }, 0);
        stage.SpaghettiEvent("sserafimBeautiful", new JObject { ["beautiful"] = true }, 0);
        stage.Sing(0, 0, false);
        Require(stage.SpaghettiCharacter(5).Animation == "singLEFT-beautiful", "Beautiful Girlfriend did not sing.");
        stage.SpaghettiEvent("sserafimKick", new JObject { ["final"] = true }, 0);
        Require(stage.SpaghettiIconVisible, "Final kick did not reveal the health icon.");
        stage.ResetStage();
        stage.StopSpaghettiSound();
        Require(!stage.SpaghettiIconVisible && !stage.PropGraphic("truckDoor").gameObject.activeSelf
            && Enumerable.Range(0, 6).All(index => !stage.SpaghettiSinging(index)) && stage.SpaghettiCharacter(0).Animation == "doorclosed", "Retry did not restore diner state.");
        song.FunkinHud.ShowRating(FunkinRules.Judgement.Sick);
        song.FunkinHud.ShowCombo(42);
        Require(song.FunkinHud.PopupCount == 0, "Spaghetti displays rating popups.");
        var unscored = chart["noteKinds"][Song.difficulty.ToLowerInvariant()].Single(note => (string)note["k"] == "non_scoreable");
        Require(!song.vanillaPlayback.IsScoreable(0, (int)unscored["d"], (double)unscored["t"])
            && song.vanillaPlayback.IsScoreable(0, (int)unscored["d"], (double)unscored["t"] - 1), "Non-scoreable note or adjacent-time control failed.");
        capture = 0;
        pausedAt = 0;
        pauseChecked = false;
        endingCard = 0;
        Debug.Log("SPAGHETTI STAGE PASSED: six performer routes, three hit and miss variants, beautiful GF, reset, hidden HUD elements, and non-scoreable control.");
    }

    public static void ObserveEnding(Song song)
    {
        var presentation = song.vanillaPlayback.Presentation;
        if (presentation.SpaghettiEndingCard != endingCard)
        {
            int previous = endingCard;
            endingCard = presentation.SpaghettiEndingCard;
            Require(endingCard == 1 && previous == 0 || endingCard == 2 && previous == 1 || endingCard == 0 && previous == 2,
                "Ending cards appeared out of order.");
            if (endingCard > 0)
            {
                Require(!song.uiCamera.enabled && !song.battleCanvas.enabled && !presentation.AllowEnd(song), "Ending did not hide gameplay or block early completion.");
                Canvas.ForceUpdateCanvases();
                var card = presentation.GetComponentsInChildren<UnityEngine.UI.RawImage>().Single(item => item.name == "end" + endingCard);
                Require(card.texture.width > 500 && card.canvasRenderer.GetMesh()?.vertexCount > 0, "Ending card did not render its source texture.");
                string output = Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_TEST_PATH")
                    ?? Environment.GetEnvironmentVariable("UNITY_PARTY_CAMPAIGN_LIFECYCLE_PATH");
                VanillaCampaignPresentationValidation.CaptureOverlay(Path.Combine(output,
                    "ending-" + Song.difficulty.ToLowerInvariant() + "-" + endingCard + ".png"), presentation, false);
            }
            else Debug.Log("SPAGHETTI ENDING PASSED: both cards rendered, HUD hidden, early completion blocked.");
        }
    }

    public static void CheckRegression()
    {
        CheckAssets();
        VanillaMixValidation.CheckAssets();
        VanillaWeek7Validation.CheckRim();
    }

    public static void Observe(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        ObserveEnding(song);
        if (!pauseChecked && song.SongPosition > 20000)
        {
            if (pausedAt == 0)
            {
                Pause.instance.PauseSong();
                pausedClock = stage.Clock;
                pausedAt = EditorApplication.timeSinceStartup;
            }
            else if (EditorApplication.timeSinceStartup - pausedAt > .5)
            {
                Require(Pause.instance.IsPaused && stage.Clock == pausedClock, "Diner animation clock advanced during pause.");
                Pause.instance.ContinueSong();
                pauseChecked = true;
                Debug.Log("SPAGHETTI PAUSE PASSED: stage clock froze and resumed.");
            }
        }
        if (capture < CaptureTimes.Length && song.SongPosition >= CaptureTimes[capture])
        {
            VanillaSongValidation.CaptureStage("spaghetti-" + Song.difficulty.ToLowerInvariant() + "-" + CaptureTimes[capture++] + ".png");
        }
        var expected = events.Take(song.vanillaPlayback.EventsApplied)
            .LastOrDefault(item => (string)item["e"] == "sserafimSing")?["v"]?["singing"];
        if (expected != null)
            for (int i = 0; i < 6; i++) Require(stage.SpaghettiSinging(i) == (bool)expected[i], "Live performer routing differs from the chart.");
    }
}
