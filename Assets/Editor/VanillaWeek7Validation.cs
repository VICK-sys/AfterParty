using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class VanillaWeek7Validation
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void CheckAssets()
    {
        var levels = VanillaStoryCatalog.Load();
        Require(levels.Count == 10, "Expected Tutorial, seven weeks, Weekend 1, and LE SSERAFIM.");
        var level = levels.Single(item => item.id == "week7");
        foreach (string difficulty in new[] { "easy", "normal", "hard" })
        {
            Require(VanillaStoryCatalog.TryPlaylist(level, difficulty, out var songs, out _), "Week 7 playlist unavailable.");
            Require(songs.Select(song => song.meta.songName).SequenceEqual(new[] { "Ugh", "Guns", "Stress" }), "Week 7 playlist order changed.");
        }
        foreach (string difficulty in new[] { "pico", "erect", "nightmare" })
            Require(!VanillaStoryCatalog.TryPlaylist(level, difficulty, out _, out _), "Unavailable campaign control passed: " + difficulty);
        CheckRim();
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week7Assets");
        foreach (string path in Directory.GetFiles(root, "graphic.json", SearchOption.AllDirectories))
        {
            var obj = new GameObject("Week 7 Mesh Probe");
            try
            {
                var graphic = obj.AddComponent<VanillaWeek2Graphic>();
                graphic.Load(Path.GetDirectoryName(path), 0);
                Require(!graphic.Play("missing-control"), "Missing animation control passed.");
                foreach (var animation in ((JObject)JObject.Parse(File.ReadAllText(path))["animations"]).Properties())
                {
                    Require(graphic.Play(animation.Name), "Animation unavailable: " + path + "/" + animation.Name);
                    graphic.Advance(.1f, Vector3.zero, 0);
                    Require(obj.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "Empty mesh: " + path + "/" + animation.Name);
                }
                if (Path.GetFileName(Path.GetDirectoryName(path)) == "runner")
                {
                    graphic.Play("run");
                    graphic.FlipX = true;
                    graphic.Advance(0, Vector3.zero, 0);
                    Require(Math.Abs(graphic.transform.localPosition.x - 5.05f) < .0001f, "Running frame used the shot width when flipped.");
                    graphic.Play("shot2");
                    graphic.Advance(0, Vector3.zero, 0);
                    Require(Math.Abs(graphic.transform.localPosition.x - 9.21f) < .0001f, "Shot frame did not retain its width and offset.");
                    graphic.FlipX = false;
                    graphic.Play("run");
                    graphic.Advance(0, Vector3.zero, 0);
                    Require(graphic.transform.localScale.x > 0 && graphic.transform.localPosition.x == 0, "Recycled runner retained its previous flip.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
        Debug.Log("WEEK 7 ASSETS PASSED: playlists, animations, meshes, unavailable mix and missing animation controls.");
    }

    public static void CheckRim()
    {
        string directory = Path.Combine(Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_TEST_PATH") ?? Path.GetTempPath(), "Week7RimProbe");
        Directory.CreateDirectory(directory);
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.SetPixels(Enumerable.Repeat(new Color(.25f, .25f, .25f, 1), 4096).ToArray());
        texture.Apply();
        File.WriteAllBytes(Path.Combine(directory, "probe.png"), texture.EncodeToPNG());
        File.WriteAllText(Path.Combine(directory, "graphic.json"), "{\"bounds\":[0,0,64,64],\"frames\":[[{\"xy\":[0,0,32,0,32,64,0,64],\"rect\":[0,0,32,64],\"image\":\"probe.png\",\"rotated\":false},{\"xy\":[32,0,64,0,64,64,32,64],\"rect\":[0,0,32,64],\"image\":\"probe.png\",\"rotated\":false}]],\"animations\":{\"idle\":{\"frames\":[0],\"fps\":24,\"loop\":true,\"offset\":[0,0]}}}");
        var obj = new GameObject("Composite Rim Probe");
        obj.layer = 31;
        var graphic = obj.AddComponent<VanillaWeek2Graphic>();
        var cameraObject = new GameObject("Composite Rim Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        camera.orthographic = true;
        camera.orthographicSize = .32f;
        camera.transform.position = new Vector3(.32f, -.32f, -10);
        camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var target = new RenderTexture(128, 128, 24);
        camera.targetTexture = target;
        var result = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            graphic.Load(directory, 0);
            foreach (bool composite in new[] { true, false })
            {
                graphic.SetRim(null, 8, .1f, Vector4.zero, new Color(.5f, 0, 0), 0, 1, composite);
                graphic.Advance(0, Vector3.zero, 0);
                camera.Render();
                RenderTexture.active = target;
                result.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                result.Apply();
                Color seam = result.GetPixel(56, 64);
                Color edge = result.GetPixel(120, 64);
                Require(edge.r - edge.g > .2f, "Rim exterior control is unlit.");
                Require(composite ? Math.Abs(seam.r - seam.g) < .05f : seam.r - seam.g > .2f,
                    composite ? "Composite rim lit an internal atlas seam." : "Separate-part rim control failed to expose the seam.");
            }
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++) texture.SetPixel(x, y, y >= 32 ? Color.blue : Color.black);
            texture.Apply();
            string mask = Path.Combine(directory, "mask.png");
            File.WriteAllBytes(mask, texture.EncodeToPNG());
            graphic.SetRim(mask, 8, .1f, Vector4.zero, new Color(.5f, 0, 0), 0, .4f, true);
            graphic.Advance(0, Vector3.zero, 0);
            camera.Render();
            RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            result.Apply();
            Color masked = result.GetPixel(120, 96);
            Color unmasked = result.GetPixel(120, 32);
            Require(Math.Abs(masked.r - masked.g) < .05f, "Composite rim ignored or inverted the alternate mask.");
            Require(unmasked.r - unmasked.g > .2f, "Alternate mask disabled the unmasked rim control.");
            graphic.ClearRimMask();
            graphic.Advance(0, Vector3.zero, 0);
            camera.Render();
            RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            result.Apply();
            Color cleared = result.GetPixel(120, 96);
            Require(cleared.r - cleared.g > .2f, "Composite rim retained a cleared mask.");
            foreach (float angle in new[] { 90f, 25f })
            {
                graphic.SetRim(null, 8, .1f, Vector4.zero, new Color(.5f, 0, 0), angle, 1, true);
                graphic.Advance(0, Vector3.zero, 0);
                camera.Render();
                RenderTexture.active = target;
                result.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                result.Apply();
                Color top = result.GetPixel(64, 124);
                Color bottom = result.GetPixel(64, 4);
                Color right = result.GetPixel(120, 64);
                Color left = result.GetPixel(8, 64);
                Require(top.r - top.g > .2f && Math.Abs(bottom.r - bottom.g) < .05f, "Rim vertical direction changed at " + angle);
                Require(Math.Abs(left.r - left.g) < .05f, "Rim lights the opposite horizontal edge at " + angle);
                Require(angle == 90 ? Math.Abs(right.r - right.g) < .05f : right.r - right.g > .2f,
                    "Player and opponent rim angles do not produce distinct horizontal coverage.");
            }
            graphic.SetRim(null, 8, .3f, Vector4.zero, new Color(.5f, 0, 0), 25, 1, true);
            graphic.Advance(0, Vector3.zero, 0);
            camera.Render();
            RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            result.Apply();
            Color rejected = result.GetPixel(120, 64);
            Require(Math.Abs(rejected.r - rejected.g) < .05f, "Opponent threshold did not reject the gray surface lit by the player threshold.");
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(obj);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(result);
        }
        Debug.Log("WEEK 7 RIM PASSED: silhouette, seam control, alternate mask, cleared mask, player and opponent directions and thresholds.");
    }

    public static void CheckStage(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        Require(stage != null && stage.Week == 7, "Week 7 stage unavailable.");
        Require(song.defaultSceneObjects.All(item => !item.activeSelf), "Default stage overlaps Week 7.");
        Require(song.OpponentVocals != null && song.OpponentVocals.isPlaying, "Opponent vocal stem is silent.");
        song.stopwatch.Stop();
        song.beatStopwatch.Stop();
        foreach (AudioSource audio in song.musicSources) audio.Pause();
        JObject source = JObject.Parse(File.ReadAllText(song.selectedVanillaPath));
        Require(stage.CharacterId(0) == (string)source["characters"]["player"] && stage.CharacterId(2) == (string)source["characters"]["girlfriend"], "Week 7 characters changed.");
        foreach (int side in new[] { 0, 1 })
            foreach (int direction in new[] { 0, 1, 2, 3 })
            {
                stage.Sing(side, direction, false);
                Require(stage.CharacterGraphic(side).Animation == "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction], "Singing animation changed.");
            }
        foreach (JToken note in source["noteKinds"][Song.difficulty.ToLowerInvariant()])
        {
            stage.Hit(1, (int)note["d"] % 4, (double)note["t"]);
            Require(stage.CharacterGraphic(1).Animation == (string)note["k"], "Tankman special note failed.");
        }
        if (stage.StageId == "tankmanBattlefield")
        {
            Vector3 before = stage.PropGraphic("tankRolling").Position;
            stage.AdvanceTank(1);
            Require(Vector3.Distance(before, stage.PropGraphic("tankRolling").Position) > .1f, "Tank does not move.");
            float angle = (stage.TankAngle + 180) * Mathf.Deg2Rad;
            Vector3 expected = new Vector3(4 + Mathf.Cos(angle) * 15, -(13 + Mathf.Sin(angle) * 11), 0);
            Require(Vector3.Distance(expected, stage.PropGraphic("tankRolling").Position) < .0001f, "Tank orbit differs from source.");
        }
        else
        {
            Require(stage.CharacterGraphic(0).HasRim && stage.CharacterGraphic(1).HasRim && stage.CharacterGraphic(2).HasRim, "Erect rim lighting missing.");
            Require(stage.PropGraphic("tankBricks").FlipX && stage.PropGraphic("tankBricks").Position == new Vector3(4.45f, -7.74f, 0), "Erect bricks changed.");
        }
        if (song.vanillaPlayback.SongId == "stress")
        {
            VanillaMixValidation.CheckRunnerLoading(song);
            stage.ResetStage();
            var notes = JArray.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Bundles/Week7Assets/speaker-chart.json")));
            double first = (double)notes[0]["t"];
            stage.AdvanceWeek7(0, first - .01);
            Require(stage.SpeakerShots == 0, "Premature speaker cue control failed.");
            stage.AdvanceWeek7(0, first);
            Require(stage.SpeakerShots == notes.Count(note => (double)note["t"] <= first) && stage.CharacterGraphic(2).Animation.StartsWith("shoot"), "Speaker timing failed.");
            stage.SpawnRunner(first + 100, true);
            Require(stage.ActiveRunners > 0, "Runner failed to spawn.");
            double end = (double)notes.Last["t"] + 1;
            stage.AdvanceWeek7(0, end);
            stage.AdvanceWeek7(2, end);
            Require(stage.SpeakerShots == 546, "Speaker cue count changed.");
            Require(stage.ActiveRunners == 0, "Shot runner did not finish flickering.");
        }
        float clock = stage.Clock;
        typeof(VanillaCampaignStage).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, null);
        Require(stage.Clock == clock, "Pause advanced Week 7.");
        stage.ResetStage();
        Require(stage.SpeakerShots == 0 && stage.ActiveRunners == 0 && stage.RunnerSpawns == 0, "Retry retained Week 7 effects.");
        VanillaSongValidation.CaptureStage(song.vanillaPlayback.SongId + "-" + Song.difficulty.ToLowerInvariant() + "-stage.png");
        foreach (AudioSource audio in song.musicSources) audio.UnPause();
        song.beatStopwatch.Start();
        song.stopwatch.Start();
        Debug.Log("WEEK 7 STAGE PASSED: " + song.vanillaPlayback.SongId + "/" + Song.difficulty + ", characters, note kinds, tank, lighting, speaker cues, runners, pause, retry.");
    }
}
