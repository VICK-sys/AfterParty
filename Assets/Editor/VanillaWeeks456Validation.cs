using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class VanillaWeeks456Validation
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void CheckAssets()
    {
        var levels = VanillaStoryCatalog.Load();
        Require(levels.Count == 9, "Expected Tutorial, seven weeks, and Weekend 1.");
        foreach (int week in new[] { 4, 5, 6 })
        {
            var level = levels.Single(item => item.id == "week" + week);
            foreach (string difficulty in new[] { "easy", "normal", "hard" })
                Require(VanillaStoryCatalog.TryPlaylist(level, difficulty, out var songs, out _) && songs.Count == 3, "Incomplete campaign: " + level.id);
            Require(!VanillaStoryCatalog.TryPlaylist(level, "pico", out _, out _), "Excluded Pico control passed.");
            Require(VanillaStoryCatalog.TryPlaylist(level, "erect", out _, out _) == (week == 6), "Unavailable remix control failed.");
            string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week" + week + "Assets");
            foreach (string path in Directory.GetFiles(root, "graphic.json", SearchOption.AllDirectories))
            {
                var obj = new GameObject("Campaign mesh probe");
                try
                {
                    var graphic = obj.AddComponent<VanillaWeek2Graphic>();
                    graphic.Load(Path.GetDirectoryName(path), 0);
                    Require(!graphic.Play("missing-control"), "Missing animation control passed.");
                    JObject data = JObject.Parse(File.ReadAllText(path));
                    foreach (var animation in ((JObject)data["animations"]).Properties())
                    {
                        Require(graphic.Play(animation.Name), "Source animation missing: " + path + "/" + animation.Name);
                        graphic.Advance(.1f, Vector3.zero, 0);
                        Require(obj.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "Empty character or prop mesh: " + path);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(obj); }
            }
        }
        Debug.Log("WEEKS 4-6 ASSETS PASSED: campaigns, source animations, mesh generation, missing remix and animation controls.");
    }

    public static void CheckStage(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        Require(stage != null, "Campaign stage did not load.");
        Require(song.defaultSceneObjects.All(item => !item.activeSelf), "Default stage overlaps source stage.");
        Require(song.OpponentVocals != null && song.OpponentVocals.isPlaying, "Source opponent vocal stem did not play.");
        JObject source = JObject.Parse(File.ReadAllText(song.selectedVanillaPath));
        Require(stage.CharacterId(0) == (string)source["characters"]["player"] && stage.CharacterId(1) == (string)source["opponent"], "Wrong source actors.");
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week" + stage.Week + "Assets/stages", stage.StageId);
        var data = JObject.Parse(File.ReadAllText(Path.Combine(root, "stage.json")));
        Require(stage.PropCount == data["props"].Count(prop => !((string)prop["assetPath"]).StartsWith("#")), "Stage prop count differs from source.");
        foreach (int side in new[] { 0, 1 })
            foreach (int direction in new[] { 0, 1, 2, 3 })
            {
                stage.Sing(side, direction, false);
                Require(stage.CharacterGraphic(side).Animation == "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction], "Source singing direction failed.");
            }
        foreach (JToken note in source["noteKinds"][Song.difficulty.ToLowerInvariant()].GroupBy(n => (string)n["k"]).Select(group => group.First()))
        {
            int lane = (int)note["d"];
            string before = stage.CharacterGraphic(lane / 4).Animation;
            stage.Hit(lane / 4, lane % 4, (double)note["t"]);
            string kind = (string)note["k"];
            Require(kind == "noanim" ? stage.CharacterGraphic(lane / 4).Animation == before
                : stage.CharacterGraphic(lane / 4).Animation.EndsWith(kind == "mom" ? "-alt" : "-censor"), "Special note kind failed: " + kind);
        }
        if (stage.Week == 4)
        {
            stage.DriveCar(1f / 60);
            Require(stage.CarDriving && stage.CarCount > 0, "Fast car did not start.");
            if (stage.StageId.EndsWith("Erect")) { stage.ShootStar(16); Require(stage.ShootingStarCount > 0, "Shooting star did not start."); }
        }
        if (stage.Week == 6)
        {
            Require(song.vanillaPlayback.IsPixel && FunkinNoteSkin.Head(0).texture.name.Contains("arrows-pixels"), "Pixel note style did not load.");
            if (song.vanillaPlayback.SongId == "roses") Require(stage.PropGraphic("freaks").Animation.EndsWith("-scared"), "Roses crowd is not scared.");
            if (song.vanillaPlayback.SongId == "thorns") Require(stage.PropGraphic("school").Wiggle.z > 0, "Thorns distortion missing.");
        }
        float clock = stage.Clock;
        foreach (AudioSource audio in song.musicSources) audio.Pause();
        typeof(VanillaCampaignStage).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(stage, null);
        Require(stage.Clock == clock, "Pause advanced campaign effects.");
        foreach (AudioSource audio in song.musicSources) audio.UnPause();
        stage.ResetStage();
        Require(stage.CarCount == 0 && !stage.CarDriving && stage.Clock == 0 && stage.ShootingStarCount == 0, "Retry failed to reset stage effects.");
        VanillaSongValidation.CaptureStage(song.vanillaPlayback.SongId + "-" + Song.difficulty.ToLowerInvariant() + "-stage.png");
        Debug.Log("CAMPAIGN STAGE PASSED: " + song.vanillaPlayback.SongId + "/" + Song.difficulty + ", source actors, note kinds, effects, pause, retry.");
    }
}
