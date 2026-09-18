using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class VanillaWeek2Validation
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void CheckAssets()
    {
        VanillaSongValidation.CheckCharts();
        Require(VanillaStoryCatalog.Load().Select(level => level.id).SequenceEqual(new[] { "tutorial", "week1", "week2", "week3", "week4", "week5", "week6" }), "Installed story weeks changed.");
        VanillaStoryLevel level = VanillaStoryCatalog.Load().Single(item => item.id == "week2");
        foreach (string difficulty in new[] { "easy", "normal", "hard" })
        {
            Require(VanillaStoryCatalog.TryPlaylist(level, difficulty, out var songs, out _), "Week 2 playlist is incomplete.");
            Require(songs.Select(song => song.meta.songName).SequenceEqual(new[] { "Spookeez", "South", "Monster" }), "Week 2 song order changed.");
        }
        Require(!VanillaStoryCatalog.TryPlaylist(level, "erect", out var incomplete, out _) && incomplete.Count == 0, "Monster Erect negative control was accepted.");
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week2Assets/characters");
        foreach (string id in new[] { "bf", "bf-dark", "gf", "gf-dark", "spooky", "spooky-dark", "monster" })
        {
            var obj = new GameObject(id);
            try
            {
                var graphic = obj.AddComponent<VanillaWeek2Graphic>();
                graphic.Load(Path.Combine(root, id), 0);
                Require(graphic.Size.x > 100 && graphic.Size.y > 100, "Character bounds are empty: " + id);
                Require(graphic.Has(id.StartsWith("gf") || id.StartsWith("spooky") ? "danceLeft" : "idle"), "Idle animation is missing: " + id);
                Require(!graphic.Play("missing-control"), "Missing animation control was accepted.");
                graphic.Advance(0.1f, Vector3.zero, 0.1f);
                Require(obj.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "Character mesh is empty: " + id);
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
        Debug.Log("WEEK 2 ASSETS PASSED: 46 parsed charts, story playlists, seven source characters, and missing-content controls.");
    }

    public static void CheckStage(Song song)
    {
        VanillaWeek2Stage stage = song.vanillaPlayback.Week2Stage;
        Require(stage != null, "Week 2 stage did not load.");
        Require(song.OpponentVocals != null && song.OpponentVocals.isPlaying && song.SplitPlayerVocalsPath != null, "Original split vocal stems did not start.");
        Require(Math.Abs(song.OpponentVocals.time - song.vocalSource.time) < 0.1f, "Split vocal stems lost synchronization.");
        Require(song.defaultSceneObjects.All(item => !item.activeSelf), "Default stage overlaps Week 2.");
        Require(stage.Erect == song.vanillaPlayback.IsErect, "Wrong Week 2 stage variation.");
        Require(stage.PropCount == (stage.Erect ? 5 : 1), "Week 2 props are missing.");
        Require(stage.CharacterId(0) == (stage.Erect ? "bf-dark" : "bf"), "Wrong player atlas.");
        Require(stage.CharacterId(1) == (song.vanillaPlayback.SongId == "monster" ? "monster" : stage.Erect ? "spooky-dark" : "spooky"), "Wrong opponent atlas.");
        Require(stage.CharacterId(2) == (stage.Erect ? "gf-dark" : "gf"), "Wrong Girlfriend atlas.");
        Require(stage.CameraTargets.All(point => point.z == -10) && Vector3.Distance(stage.CameraTargets[2], stage.CameraTargets[1]) > 1, "Camera targets overlap.");
        JObject data = JObject.Parse(File.ReadAllText(song.selectedVanillaPath));
        foreach (JToken tempo in data["timeChanges"])
        {
            float time = (float)tempo["t"];
            float expected = (float)tempo["b"];
            Require(Math.Abs(song.vanillaPlayback.BeatAt(time) - expected) < 0.002, "Source tempo marker changed.");
        }
        if (song.vanillaPlayback.SongId == "monster")
            Require(Math.Abs(song.vanillaPlayback.BeatAt(140000) - 140000 * 95f / 60000) > 10, "Constant-tempo Monster control passed.");
        foreach (JToken note in data["noteKinds"][Song.difficulty.ToLowerInvariant()])
        {
            int lane = (int)note["d"];
            double time = (double)note["t"];
            Require(stage.SuppressAnimation(lane / 4, lane % 4, time), "Source noanim note was lost.");
            Require(!stage.SuppressAnimation(1 - lane / 4, lane % 4, time), "Swapped-side noanim control passed.");
        }
        CheckSingingPriority(stage);
        if (stage.Erect)
        {
            foreach (var sample in new[] { ("danceLeft", 0, 3.81f, 5.49f), ("danceLeft", 2, 3.79f, 5.41f), ("singRIGHT", 2, 4.37f, 5.33f) })
            {
                stage.ResetStage();
                stage.PlayAnimation("dad", sample.Item1);
                typeof(VanillaWeek2Stage).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(stage, new object[] { (sample.Item2 + 0.01f) / 24 });
                Bounds bounds = stage.CharacterGraphic(1).GetComponent<MeshFilter>().sharedMesh.bounds;
                Require(Math.Abs(bounds.size.x - sample.Item3) < 0.0001f && Math.Abs(bounds.size.y - sample.Item4) < 0.0001f,
                    "Opponent frame has incorrect proportions: " + sample.Item1 + " " + sample.Item2);
                VanillaSongValidation.CaptureStage(song.vanillaPlayback.SongId + "-opponent-" + sample.Item1 + "-" + sample.Item2 + ".png");
            }
            stage.ResetStage();
        }
        stage.Strike(false, 4);
        Require(stage.LightningCount == 1 && stage.LastStrikeBeat == 4 && stage.StrikeOffset >= 8 && stage.StrikeOffset <= 24, "Lightning timing changed.");
        Require(stage.CharacterGraphic(0).Animation == "scared" && stage.CharacterGraphic(2).Animation == "scared", "Lightning fear animations did not play.");
        if (stage.Erect)
            Require(stage.PropGraphic("bgLight").Alpha == 1 && stage.CharacterGraphic(0).Alpha == 0, "Erect lightning did not swap lighting.");
        else Require(stage.PropGraphic("halloweenBG").Animation == "lightning", "Original lightning animation did not play.");
        typeof(VanillaWeek2Stage).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, new object[] { 0f });
        VanillaSongValidation.CaptureStage(song.vanillaPlayback.SongId + (stage.Erect ? "-erect" : "") + "-lightning.png");
        if (stage.Erect)
        {
            typeof(VanillaWeek2Stage).GetProperty("LightningAge").SetValue(stage, 0.87f);
            typeof(VanillaWeek2Stage).GetMethod("UpdateLighting", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, null);
            typeof(VanillaWeek2Stage).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, new object[] { 0f });
            VanillaSongValidation.CaptureStage(song.vanillaPlayback.SongId + "-erect-fade.png");
        }
        float age = stage.LightningAge;
        int frame = stage.CharacterGraphic(0).Frame;
        song.PauseSong();
        typeof(VanillaWeek2Stage).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, null);
        Require(stage.LightningAge == age && stage.CharacterGraphic(0).Frame == frame && !song.OpponentVocals.isPlaying, "Pause advanced lightning, characters, or vocals.");
        song.ContinueSong();
        var mute = typeof(Song).GetMethod("SetFunkinVocalMuted", BindingFlags.Instance | BindingFlags.NonPublic);
        mute.Invoke(song, new object[] { 0, true });
        Require(song.vocalSource.mute && !song.OpponentVocals.mute, "Player miss muted opponent vocals.");
        mute.Invoke(song, new object[] { 0, false });
        mute.Invoke(song, new object[] { 1, true });
        Require(!song.vocalSource.mute && song.OpponentVocals.mute, "Opponent miss muted player vocals.");
        mute.Invoke(song, new object[] { 1, false });
        stage.ResetStage();
        Require(stage.LightningCount == 0 && stage.LastStrikeBeat == 0 && stage.StrikeOffset == 8, "Retry retained lightning state.");
        if (stage.Erect)
            Require(stage.PropGraphic("bgLight").Alpha == 0 && stage.CharacterGraphic(0).Alpha == 1, "Retry retained flash alpha.");
        Debug.Log("WEEK 2 STAGE PASSED: " + song.vanillaPlayback.SongId + " " + Song.difficulty + ", source actors, tempos, noanim, lightning, and retry controls.");
    }

    private static void CheckSingingPriority(VanillaWeek2Stage stage)
    {
        VanillaWeek2Graphic boyfriend = stage.CharacterGraphic(0);
        for (int direction = 0; direction < 4; direction++)
        foreach (bool miss in new[] { false, true })
        {
            stage.ResetStage();
            stage.Sing(0, direction, miss);
            boyfriend.Advance(0.08f, Vector3.zero, 0);
            string singing = boyfriend.Animation;
            int frame = boyfriend.Frame;
            Require(singing == "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction] + (miss ? "miss" : ""), "Singing control did not start.");
            stage.Strike(false, 4);
            Require(boyfriend.Animation == singing && boyfriend.Frame == frame, "Lightning interrupted or restarted Boyfriend singing.");
            Require(stage.CharacterGraphic(2).Animation == "scared", "Girlfriend fear control did not play.");
            if (!miss)
            {
                stage.Hold(0);
                stage.Strike(false, 5);
                Require(boyfriend.Animation == singing && boyfriend.Frame == frame, "Lightning interrupted a sustained note.");
            }
            stage.ResetStage();
            stage.Strike(false, 4);
            Require(boyfriend.Animation == "scared", "Idle Boyfriend fear control did not play.");
            stage.Sing(0, direction, miss);
            Require(boyfriend.Animation == singing, "Scared animation blocked a new hit or miss.");
            if (stage.Erect)
                Require(stage.GetComponentsInChildren<VanillaWeek2Graphic>().Single(graphic => graphic.name == "bf lightning").Animation == singing,
                    "Boyfriend lightning counterpart did not follow singing.");
        }
        stage.ResetStage();
        stage.Strike(false, 4);
        stage.PlayAnimation("bf", "idle");
        Require(boyfriend.Animation == "scared", "Idle control canceled the scared animation.");
        stage.ResetStage();
        Debug.Log("WEEK 2 ANIMATION PRIORITY PASSED: four directions, hits, misses, sustains, idle fear, Girlfriend, and lightning counterparts.");
    }
}
