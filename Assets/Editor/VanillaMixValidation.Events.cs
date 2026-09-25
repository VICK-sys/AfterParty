using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class VanillaMixValidation
{
    private const BindingFlags ProbeFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static object ProbeGet(object owner, string name) => owner.GetType().GetField(name, ProbeFlags).GetValue(owner);
    private static void ProbeSet(object owner, string name, object value) => owner.GetType().GetField(name, ProbeFlags).SetValue(owner, value);
    private static void ProbeInvoke(object owner, string name, params object[] values) => owner.GetType().GetMethod(name, ProbeFlags).Invoke(owner, values);

    private static void ProbeEvents(VanillaSongPlayback playback, IEnumerable<JToken> source)
    {
        ProbeSet(playback, "events", source.OrderBy(entry => (double)entry["t"]).ToArray());
        ProbeSet(playback, "<EventsApplied>k__BackingField", 0);
    }

    private static void ProbeUntil(VanillaSongPlayback playback, double time) => ProbeInvoke(playback, "ApplyUntil", (float)time);

    public static void CheckRuntimeEvents(Song song)
    {
        var playback = song.vanillaPlayback;
        Require(playback != null && IsMix(Read(song.selectedVanillaPath)), "Runtime mix probe requires an active mix.");
        Require(Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_STAGE_ONLY") == "1", "Run mix event probes only after the stage screenshot and before quitting.");
        object stage = (object)playback.CampaignStage ?? (object)playback.Week3Stage ?? playback.Week2Stage;
        var fields = playback.GetType().GetFields(ProbeFlags | BindingFlags.DeclaredOnly).Where(field => !field.IsInitOnly).ToArray();
        var saved = fields.ToDictionary(field => field, field => field.GetValue(playback));
        Sprite[][] icons = Field<Sprite[][]>(song.FunkinHud, "iconFrames");
        Sprite[] playerIcon = icons[0];
        Sprite[] opponentIcon = icons[1];
        bool muted = song.vocalSource.mute;
        float health = song.health;
        float speedDifference = song.speedDifference;
        var random = UnityEngine.Random.state;
        JObject source = Read(song.selectedVanillaPath);
        try
        {
            if (playback.SongId == "stress" && playback.Variation == "pico") ProbeStressEvents(song, source);
            if (playback.SongId == "south" && playback.Variation == "pico") ProbeSouthScroll(song, source);
            ProbeBurpVocals(song, source);
            if (playback.Week2Stage != null && playback.PlayerId == "pico-dark") ProbePicoLightning(song);
            if (playback.Week3Stage != null && playback.SongId == "pico" && playback.Variation == "pico") ProbePicoCensor(song);
            if (playback.CampaignStage != null && playback.Variation == "bf") ProbeBfCompanionControl(song);
        }
        finally
        {
            ProbeInvoke(stage, "ResetStage");
            foreach (var entry in saved) entry.Key.SetValue(playback, entry.Value);
            icons[0] = playerIcon;
            icons[1] = opponentIcon;
            song.vocalSource.mute = muted;
            song.health = health;
            song.speedDifference = speedDifference;
            UnityEngine.Random.state = random;
        }
        Debug.Log("MIX RUNTIME EVENTS PASSED: " + playback.SongId + "/" + playback.Variation + ". Source event queue, vocal mute, health, icons and random state restored.");
    }

    private static void ProbeStressEvents(Song song, JObject source)
    {
        var playback = song.vanillaPlayback;
        var stage = playback.CampaignStage;
        JToken redheads = source["events"].Single(entry => (string)entry["e"] == "PlayAnimation" && (string)entry["v"]["anim"] == "redheadsAnim");
        JToken mask = source["events"].Single(entry => (string)entry["e"] == "EnableMask");
        JToken knife = source["events"].Single(entry => (string)entry["e"] == "PlayAnimation" && (string)entry["v"]["anim"] == "knifeToss");
        JToken icon = source["events"].Single(entry => (string)entry["e"] == "SetHealthIcon");
        var bloody = stage.GetComponentsInChildren<VanillaWeek2Graphic>(true).Single(graphic => graphic.Has("idle-bloody"));
        var preparedMask = Field<Texture2D>(bloody, "preparedRimMask");
        Require(preparedMask != null, "Stress did not prepare its blood mask before playback.");
        var meshes = Field<Dictionary<int, Mesh>>(bloody, "meshes");
        int preparedMeshes = meshes.Count;
        Require(preparedMeshes > 1, "Stress did not prepare its bloody animation frames.");
        foreach (string prop in new[] { "sniper", "guy" })
            Require(Mathf.Abs(stage.PropGraphic(prop).transform.localScale.x - 1.15f) < .0001f, "Stress background tankman scale differs from stage data.");
        Require(!Cursor.visible, "Gameplay left the mouse cursor visible.");
        bool autoplay = Player.demoMode;
        try
        {
            Player.demoMode = false;
            song.health = 200;
            for (int frame = 0; frame < 120; frame++) song.FunkinHud.Advance(1d / 60, song.SongPosition);
            song.FunkinHud.Render();
            var green = Field<SpriteRenderer>(song.FunkinHud, "green");
            var red = Field<SpriteRenderer>(song.FunkinHud, "red");
            Require(song.FunkinHud.DisplayHealth == 200 && Vector3.Distance(green.bounds.min, red.bounds.min) < .0001f
                && Vector3.Distance(green.bounds.max, red.bounds.max) < .0001f, "Full health leaves opponent fill visible.");
            song.health = 199.9f;
            for (int frame = 0; frame < 120; frame++) song.FunkinHud.Advance(1d / 60, song.SongPosition);
            song.FunkinHud.Render();
            Require(green.bounds.size.x < red.bounds.size.x, "Below-maximum health passed the full-fill control.");
        }
        finally { Player.demoMode = autoplay; }
        stage.ResetStage();
        song.FunkinHud.Initialize(song, playback.OpponentId);
        Require(Field<Texture2D>(bloody, "rimMask") == null, "Stress starts with the blood mask enabled.");
        stage.Sing(1, 0, false);
        Require(stage.CharacterGraphic(1).Animation == "singLEFT", "Stress starts with bloody singing.");
        ProbeEvents(playback, new[] { redheads, mask, knife, icon });
        ProbeUntil(playback, (float)redheads["t"] - 1);
        Require(playback.EventsApplied == 0 && stage.CharacterGraphic(1).Animation == "singLEFT", "Stress redheads animation ran early.");
        ProbeUntil(playback, (float)redheads["t"]);
        Require(stage.CharacterGraphic(1) == bloody && bloody.Animation == "redheadsAnim", "Stress source redheads event selected the wrong atlas.");
        Require(Field<Texture2D>(bloody, "rimMask") == null, "Redheads event enabled the mask before its separate source event.");
        for (int frame = 0; frame < Mathf.CeilToInt(bloody.Duration * 60); frame++)
            bloody.Advance(1f / 60, song.mainCamera.transform.position, stage.Clock);
        Require(meshes.Count == preparedMeshes, "Redheads created animation meshes during playback.");
        var maskTimer = System.Diagnostics.Stopwatch.StartNew();
        ProbeUntil(playback, (float)mask["t"]);
        maskTimer.Stop();
        Texture2D texture = Field<Texture2D>(bloody, "rimMask");
        Require(texture == preparedMask && Field<Material>(bloody, "material").GetTexture("_RimMask") == texture, "Stress mask event did not reuse the prepared bloody mask.");
        stage.Sing(1, 0, false);
        Require(stage.CharacterGraphic(1) == bloody && bloody.Animation == "singLEFT-bloody", "Stress singing did not stay bloody after redheads.");
        ProbeUntil(playback, (float)knife["t"]);
        Require(stage.CharacterGraphic(0).Animation == "knifeToss", "Stress knife event did not reach Pico.");
        Sprite[][] frames = Field<Sprite[][]>(song.FunkinHud, "iconFrames");
        Require(frames[1][0] == FunkinHudAssets.Icon("tankman")[0], "Stress icon changed before its source event.");
        ProbeUntil(playback, (float)icon["t"]);
        Require(frames[1][0] == FunkinHudAssets.Icon("tankman-bloody")[0] && frames[1][0] != FunkinHudAssets.Icon("tankman")[0],
            "Stress icon event used the original icon or wrong side.");
        Require(frames[0][0] == FunkinHudAssets.Icon("pico")[0], "Stress opponent event replaced the player icon.");
        Require(playback.EventsApplied == 4, "Stress event probe skipped or duplicated an event.");
        stage.ResetStage();
        song.FunkinHud.Initialize(song, playback.OpponentId);
        stage.Sing(1, 0, false);
        Require(stage.CharacterGraphic(1).Animation == "singLEFT" && Field<Texture2D>(bloody, "rimMask") == null,
            "Stress reset retained bloody singing or the blood mask.");
        Require(Field<Sprite[][]>(song.FunkinHud, "iconFrames")[1][0] == FunkinHudAssets.Icon("tankman")[0], "Stress HUD reset retained the bloody icon.");
        Require(Field<Texture2D>(bloody, "preparedRimMask") == preparedMask, "Stress reset discarded its prepared mask.");
        stage.EnableTankmanMask();
        Require(Field<Texture2D>(bloody, "rimMask") == preparedMask, "Stress retry loaded another mask texture.");
        stage.ResetStage();
        Debug.Log("STRESS PREPARATION PASSED: " + preparedMeshes + " cached frames, mask activation " + maskTimer.Elapsed.TotalMilliseconds + " ms, texture reused after reset.");
        stage.PlayAnimation("gf", "shoot1");
        stage.PlayAnimation("dad", "stressPicoEnding");
        stage.PlayAnimation("bf", "laughEnd");
        var otis = stage.CharacterGraphic(2);
        var companion = Field<VanillaMixCompanion>(stage, "companion");
        stage.StressPicoOutroBeat(true);
        Require(otis.Animation == "idle", "Stress outro retained Otis's final shooting pose.");
        int firstFrame = otis.AnimationFrame;
        for (int frame = 0; frame < 8; frame++) otis.Advance(1f / 24, song.mainCamera.transform.position, stage.Clock);
        Require(otis.AnimationFrame != firstFrame, "Otis did not animate during the outro.");
        otis.Advance(otis.Duration + 1, song.mainCamera.transform.position, stage.Clock);
        companion.Body.Advance(companion.Body.Duration + 1, song.mainCamera.transform.position, stage.Clock);
        Require(otis.Finished && companion.Body.Finished, "Outro stopped-clock control did not reach the frozen final frames.");
        stage.StressPicoOutroBeat(false);
        Require(!otis.Finished && !companion.Body.Finished, "Outro beat did not restart Otis and A-Bot.");
        Require(stage.CharacterGraphic(1).Animation == "stressPicoEnding" && stage.CharacterGraphic(0).Animation == "laughEnd",
            "Companion outro beat interrupted Tankman or Pico.");
        stage.ResetStage();
        Debug.Log("STRESS OUTRO COMPANION PASSED: idle frames advance, Otis and A-Bot restart, stopped-clock control freezes, ending actors remain unchanged.");
        Debug.Log("STRESS PICO EVENTS PASSED: timed redheads, separate mask, bloody singing, knife, icon side and retry controls.");
    }

    private static void ProbeSouthScroll(Song song, JObject source)
    {
        var playback = song.vanillaPlayback;
        JToken[] changes = source["events"].Where(entry => (string)entry["e"] == "ScrollSpeed").ToArray();
        Require(changes.Length == 15, "South source scroll events are incomplete.");
        ProbeEvents(playback, changes);
        float expectedFrom = Field<float>(playback, "baseSpeed");
        for (int index = 0; index < changes.Length; index++)
        {
            JToken entry = changes[index];
            float time = (float)entry["t"];
            ProbeUntil(playback, time - 1);
            Require(playback.EventsApplied == index, "South scroll transition ran before its source timestamp.");
            ProbeUntil(playback, time);
            object transition = ProbeGet(playback, "scrollTransition");
            float target = (float)entry["v"]["scroll"];
            float duration = (float)entry["v"]["duration"] * Field<float>(playback, "stepMilliseconds");
            string ease = (string)entry["v"]["ease"];
            Require(Math.Abs((float)ProbeGet(transition, "from") - expectedFrom) < .0001f && Math.Abs((float)ProbeGet(transition, "to") - target) < .0001f,
                "South scroll event multiplied an absolute speed or used the wrong preceding speed.");
            Require(Math.Abs((float)ProbeGet(transition, "start") - time) < .001f && Math.Abs((float)ProbeGet(transition, "duration") - duration) < .001f,
                "South scroll transition changed its source timestamp or four-step duration.");
            Require((string)ProbeGet(transition, "ease") == ease, "South scroll transition changed its source easing.");
            float eased = ease == "cubeInOut" ? .0625f : .03125f;
            float sampleTime = time + duration * .25f;
            float actual = (float)transition.GetType().GetMethod("Value").Invoke(transition, new object[] { sampleTime });
            float expected = expectedFrom + (target - expectedFrom) * eased;
            float linear = expectedFrom + (target - expectedFrom) * .25f;
            Require(Math.Abs(actual - expected) < .0001f && Math.Abs(actual - linear) > .01f, "South source easing or linear negative control failed.");
            float end = (float)transition.GetType().GetMethod("Value").Invoke(transition, new object[] { time + duration });
            Require(Math.Abs(end - target) < .0001f, "South transition did not reach its exact source speed.");
            expectedFrom = target;
        }
        Debug.Log("SOUTH PICO SCROLL PASSED: all 15 source timestamps, absolute targets, four-step durations, cubic/quartic samples and rejected linear controls.");
    }

    private static void ProbeBurpVocals(Song song, JObject source)
    {
        var playback = song.vanillaPlayback;
        var events = source["events"].Where(entry => (string)entry["e"] == "PlayAnimation"
            && new[] { "burpShit", "burpSmile", "burpSmileLong" }.Contains((string)entry["v"]["anim"])).ToArray();
        foreach (JToken entry in events)
        {
            JObject control = (JObject)entry.DeepClone();
            control["v"]["anim"] = "idle";
            song.vocalSource.mute = true;
            ProbeEvents(playback, new[] { control });
            ProbeUntil(playback, (float)control["t"]);
            Require(song.vocalSource.mute, "Ordinary animation incorrectly unmuted Pico vocals.");
            control = (JObject)entry.DeepClone();
            control["v"]["target"] = "dad";
            ProbeEvents(playback, new[] { control });
            ProbeUntil(playback, (float)control["t"]);
            Require(song.vocalSource.mute, "Opponent animation incorrectly unmuted Pico vocals.");
            ProbeEvents(playback, new[] { entry });
            ProbeUntil(playback, (float)entry["t"] - 1);
            Require(song.vocalSource.mute, "Pico burp unmuted vocals early.");
            ProbeUntil(playback, (float)entry["t"]);
            Require(!song.vocalSource.mute, "Source Pico burp did not unmute player vocals.");
        }
        if (events.Length > 0) Debug.Log("PICO BURP VOCALS PASSED: " + events.Length + " source events, ordinary-animation and opponent-side controls.");
    }

    private static void ProbePicoLightning(Song song)
    {
        var stage = song.vanillaPlayback.Week2Stage;
        object actor = ((Array)ProbeGet(stage, "actors")).GetValue(0);
        foreach (int direction in new[] { 0, 1, 2, 3 })
        {
            stage.ResetStage();
            stage.Sing(0, direction, true);
            ProbeInvoke(stage, "UpdateLighting");
            string animation = "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction] + "miss";
            var light = (VanillaWeek2Graphic)ProbeGet(actor, "light");
            var alternatives = (IEnumerable<VanillaWeek2Graphic>)ProbeGet(actor, "lightGraphics");
            Require(stage.CharacterGraphic(0).Animation == animation && light.Animation == animation, "Pico lightning overlay lost its miss animation.");
            Require(Field<string>(light, "assetKey").Contains("pico-playable") && Field<string>(light, "assetKey").Contains("alternate"),
                "Pico lightning loaded opponent Pico instead of the playable miss atlas.");
            Require(alternatives.Count(item => item.gameObject.activeSelf) == 1 && light.Alpha == 0 && stage.CharacterGraphic(0).Alpha == 1,
                "Pico lightning miss overlay is visible before the strike or overlaps another atlas.");
            stage.Strike(false, 20);
            Require(stage.CharacterGraphic(0).Alpha == 0 && light.Alpha == 1 && light.Animation == animation, "Lightning did not reveal the matching Pico miss pose.");
            stage.Sing(0, direction, false);
            var normal = (VanillaWeek2Graphic)ProbeGet(actor, "light");
            Require(normal != light && normal.Animation == animation.Replace("miss", "") && alternatives.Count(item => item.gameObject.activeSelf) == 1,
                "Normal singing did not leave the lightning miss atlas.");
        }
        stage.ResetStage();
        Require(stage.LightningCount == 0 && stage.CharacterGraphic(0).Alpha == 1 && ((VanillaWeek2Graphic)ProbeGet(actor, "light")).Alpha == 0,
            "Pico lightning reset retained the flash.");
        Debug.Log("PICO LIGHTNING PASSED: four playable miss overlays, flash visibility, normal-singing atlas and retry controls.");
    }

    private static void ProbePicoCensor(Song song)
    {
        var stage = song.vanillaPlayback.Week3Stage;
        JObject chart = Read(Path.Combine(song.selectedSongDir, "Source/chart.json"));
        JToken[] notes = chart["notes"][Song.difficulty.ToLowerInvariant()].ToArray();
        JToken[] censored = notes.Where(note => (string)note["k"] == "censor").ToArray();
        Require(censored.Length > 0, "Pico source chart has no censor notes to probe.");
        foreach (JToken note in censored)
        {
            int side = (int)note["d"] / 4;
            int direction = (int)note["d"] % 4;
            string animation = "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction];
            stage.Hit(side, direction, (double)note["t"]);
            Require(stage.CharacterGraphic(side).Animation == animation + "-censor", "Pico censor note did not select its source animation.");
            JToken normal = notes.Where(other => (int)other["d"] == (int)note["d"] && string.IsNullOrEmpty((string)other["k"]))
                .OrderBy(other => Math.Abs((double)other["t"] - (double)note["t"])).First();
            stage.Hit(side, direction, (double)normal["t"]);
            Require(stage.CharacterGraphic(side).Animation == animation, "Nearby ordinary Pico note retained the censor animation.");
            stage.Hit(1 - side, direction, (double)note["t"]);
            Require(!stage.CharacterGraphic(1 - side).Animation.EndsWith("-censor"), "Pico censor note leaked to the other strumline.");
        }
        Debug.Log("PICO CENSOR PASSED: " + censored.Length + " source notes, nearby normal notes and other-side controls.");
    }

    private static void ProbeBfCompanionControl(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        stage.ResetStage();
        object girlfriend = ((Array)ProbeGet(stage, "actors")).GetValue(2);
        string original = (string)ProbeGet(girlfriend, "id");
        Require(original == "gf" && Field<VanillaWeek2Graphic>(stage, "abot") == null, "BF companion control requires GF without A-Bot.");
        foreach (float health in new[] { 50f, 49f, 1f })
        {
            song.health = health;
            stage.AdvanceWeekend1(0);
            stage.WeekendBeat(1);
            Require((int)ProbeGet(stage, "neneState") == 0 && !stage.CharacterGraphic(2).Animation.Contains("Knife"),
                "BF mix entered Nene's low-health state.");
        }
        try
        {
            ProbeSet(girlfriend, "id", "nene");
            ProbeSet(stage, "neneState", 0);
            song.health = 49;
            stage.AdvanceWeekend1(0);
            Require((int)ProbeGet(stage, "neneState") > 0, "Nene identity control did not exercise the low-health branch.");
        }
        finally
        {
            ProbeSet(girlfriend, "id", original);
            stage.ResetStage();
        }
        Require((int)ProbeGet(stage, "neneState") == 0 && stage.CharacterId(2) == "gf", "BF companion probe failed to restore GF.");
        Debug.Log("BF MIX LOW HEALTH PASSED: threshold/below-threshold GF controls and a positive Nene identity control.");
    }
}
