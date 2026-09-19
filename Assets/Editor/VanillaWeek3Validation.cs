using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

public static class VanillaWeek3Validation
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void CheckAssets()
    {
        CheckLightShader();
        var levels = VanillaStoryCatalog.Load();
        Require(levels.Select(level => level.id).SequenceEqual(new[] { "tutorial", "week1", "week2", "week3", "week4", "week5", "week6", "week7", "weekend1" }), "Installed story weeks changed.");
        var level = levels.Single(item => item.id == "week3");
        foreach (string difficulty in new[] { "easy", "normal", "hard", "erect", "nightmare" })
        {
            Require(VanillaStoryCatalog.TryPlaylist(level, difficulty, out var songs, out _), "Week 3 playlist is incomplete: " + difficulty);
            Require(songs.Select(song => song.meta.songName).SequenceEqual(new[] { "Pico", "Philly Nice", "Blammed" }), "Week 3 order changed.");
            Require(songs.All(song => song.week == "Week 3" && song.Icon(difficulty) == "pico"), "Week 3 label or opponent changed.");
        }
        Require(!VanillaStoryCatalog.TryPlaylist(level, "pico", out var missing, out _) && missing.Count == 0, "Excluded Pico mix control was accepted.");
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week3Assets/characters");
        foreach (string id in new[] { "bf", "gf", "pico" })
        {
            var obj = new GameObject(id);
            try
            {
                var graphic = obj.AddComponent<VanillaWeek2Graphic>();
                graphic.Load(Path.Combine(root, id), 0);
                Require(!graphic.Play("missing-control"), "Missing animation control passed.");
                foreach (string direction in new[] { "LEFT", "DOWN", "UP", "RIGHT" })
                {
                    Require(graphic.Play("sing" + direction), "Missing singing animation: " + id + direction);
                    graphic.Advance(0.1f, Vector3.zero, 0);
                    Require(obj.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "Empty character mesh: " + id);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
        Debug.Log("WEEK 3 ASSETS PASSED: five difficulties, source order, three source actors, excluded Pico mixes, and missing-animation controls.");
    }

    private static void CheckLightShader()
    {
        var material = new Material(Resources.Load<Shader>("VanillaSongs/Week2Graphic"));
        var target = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var image = new Texture2D(32, 32, TextureFormat.RGBA32, false, true);
        var mesh = new Mesh
        {
            vertices = new[] { Vector3.zero, Vector3.right, new Vector3(1, 1), Vector3.up },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
            colors = new[] { Color.white, Color.white, Color.white, Color.white },
            triangles = new[] { 0, 1, 2, 2, 3, 0 }
        };
        mesh.SetUVs(1, new[] { Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero });
        RenderTexture previous = RenderTexture.active;
        try
        {
            material.mainTexture = Texture2D.whiteTexture;
            material.SetVector("_Tint", new Vector4(0.5f, 0.75f, 1, 1));
            material.SetFloat("_BuildingFade", 0.25f);
            target.Create();
            using (var commands = new CommandBuffer())
            {
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(false, true, Color.black);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, 1, 0, 1, -1, 1), true));
                commands.DrawMesh(mesh, Matrix4x4.identity, material);
                Graphics.ExecuteCommandBuffer(commands);
            }
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            image.Apply();
            Color pixel = image.GetPixel(16, 16);
            Require(Mathf.Abs(pixel.r - 0.25f) < 0.01f && Mathf.Abs(pixel.g - 0.5f) < 0.01f && Mathf.Abs(pixel.b - 0.75f) < 0.01f,
                "Building shader changed premultiplied source fading: " + pixel);
            Require(Mathf.Abs(pixel.b - 0.5625f) > 0.1f, "Double-opacity light control passed.");
            Debug.Log("WEEK 3 LIGHT SHADER PASSED: source subtraction, premultiplied blending, and double-opacity control.");
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    public static void CheckStage(Song song)
    {
        var stage = song.vanillaPlayback.Week3Stage;
        Require(stage != null && stage.PropCount == 6 && stage.AudioReady, "Philly stage or train sound did not load.");
        Require(stage.Erect == song.vanillaPlayback.IsErect && Mathf.Abs(stage.CameraZoom - 1.1f) < 0.0001f, "Philly variation or camera zoom changed.");
        Require(song.defaultSceneObjects.All(item => !item.activeSelf), "Default stage overlaps Philly.");
        Require(Enumerable.Range(0, 3).Select(stage.CharacterId).SequenceEqual(new[] { "bf", "pico", "gf" }), "Philly character selection changed.");
        Require(stage.CharacterGraphic(1).FlipX && !stage.CharacterGraphic(0).FlipX, "Source character facing changed.");
        Require(stage.CharacterGraphic(1).PhillyColor == stage.Erect && stage.PropGraphic("train").PhillyColor == stage.Erect, "Philly Erect color filter is missing.");
        Require(song.OpponentVocals != null && song.OpponentVocals.isPlaying && song.SplitPlayerVocalsPath != null, "Source vocal stems did not start.");
        Require(Math.Abs(song.OpponentVocals.time - song.vocalSource.time) < 0.1, "Source vocal stems lost sync.");
        Require(stage.CameraTargets.All(point => point.z == -10) && Vector3.Distance(stage.CameraTargets[1], stage.CameraTargets[2]) > 1, "Philly camera targets overlap.");
        stage.ResetStage();
        Vector3 initial = stage.PropGraphic("train").Position;
        stage.AdvanceTrain(4700);
        Require(stage.PropGraphic("train").Position == initial, "Stopped-train control moved.");
        stage.StartTrain();
        stage.AdvanceTrain(4699);
        Require(stage.TrainMoving && !stage.TrainStarted && stage.PropGraphic("train").Position == initial, "Train moved before the 4700 ms cue.");
        stage.AdvanceTrain(4700);
        Require(stage.TrainStarted && stage.PropGraphic("train").Position.x == initial.x - 4
            && stage.CharacterGraphic(2).Animation == "hairBlow", "Train cue, 400-pixel movement, or hair animation changed.");
        var girlfriend = stage.CharacterGraphic(2);
        int firstHairFrame = girlfriend.Frame;
        girlfriend.Advance(1.1f / 24, Vector3.zero, 0);
        int nextHairFrame = girlfriend.Frame;
        Require(nextHairFrame != firstHairFrame && !girlfriend.Finished, "Hair animation did not advance past its first frame.");
        stage.AdvanceTrain(4700);
        Require(girlfriend.Frame == nextHairFrame, "Train tick restarted unfinished hair animation.");
        for (int cycle = 0; cycle < 3; cycle++)
        {
            girlfriend.Advance(4f / 24, Vector3.zero, 0);
            Require(girlfriend.Finished, "Hair completion control failed.");
            stage.AdvanceTrain(4700);
            Require(girlfriend.Animation == "hairBlow" && !girlfriend.Finished && girlfriend.Frame == firstHairFrame,
                "Train did not restart completed hair animation.");
            bool alternate = false;
            Require(VanillaCharacterTiming.DanceAnimation(girlfriend, false, true, ref alternate) == null,
                "Dance interrupted restarted hair animation.");
        }
        for (int i = 0; i < 9; i++) stage.AdvanceTrain(4700);
        typeof(VanillaWeek3Stage).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, new object[] { 0.1f });
        VanillaSongValidation.CaptureStage(song.vanillaPlayback.SongId + (stage.Erect ? "-erect" : "") + "-train.png");
        for (int i = 0; i < 100 && stage.TrainMoving; i++) stage.AdvanceTrain(4700);
        Require(!stage.TrainMoving && stage.TrainCars == 8 && Mathf.Abs(stage.PropGraphic("train").Position.x - 14.8f) < 0.0001f
            && stage.CharacterGraphic(2).Animation == "hairFall", "Train did not finish eight cars and restore Girlfriend.");
        bool hairAlternate = false;
        Require(VanillaCharacterTiming.DanceAnimation(girlfriend, false, true, ref hairAlternate) == null,
            "Dance interrupted hair landing.");
        girlfriend.Advance(12f / 24, Vector3.zero, 0);
        Require(VanillaCharacterTiming.DanceAnimation(girlfriend, false, true, ref hairAlternate) == "danceLeft",
            "Girlfriend did not resume dancing after hair landing.");
        stage.ResetStage();
        stage.StartTrain();
        stage.AdvanceTrain(4700);
        Vector3 trainPosition = stage.PropGraphic("train").Position;
        int frame = stage.CharacterGraphic(2).Frame;
        float fade = stage.LightFade;
        song.PauseSong();
        typeof(VanillaWeek3Stage).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, null);
        Require(stage.PropGraphic("train").Position == trainPosition && stage.CharacterGraphic(2).Frame == frame
            && stage.LightFade == fade && !song.OpponentVocals.isPlaying, "Pause advanced train, hair, lights, or vocals.");
        song.ContinueSong();
        var mute = typeof(Song).GetMethod("SetFunkinVocalMuted", BindingFlags.Instance | BindingFlags.NonPublic);
        mute.Invoke(song, new object[] { 0, true });
        Require(song.vocalSource.mute && !song.OpponentVocals.mute, "Player miss muted opponent vocals.");
        mute.Invoke(song, new object[] { 0, false });
        mute.Invoke(song, new object[] { 1, true });
        Require(!song.vocalSource.mute && song.OpponentVocals.mute, "Opponent miss muted player vocals.");
        mute.Invoke(song, new object[] { 1, false });
        foreach (int side in new[] { 0, 1 })
        foreach (int direction in new[] { 0, 1, 2, 3 })
        {
            stage.Sing(side, direction, false);
            Require(stage.CharacterGraphic(side).Animation == "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction], "Singing direction changed.");
        }
        stage.PlayAnimation("boyfriend", "hey");
        Require(stage.CharacterGraphic(0).Animation == "hey", "Philly Nice greeting event did not play.");
        stage.ResetStage();
        Require(!stage.TrainMoving && stage.TrainCount == 0 && stage.TrainCars == 8 && stage.TrainCooldown == 0
            && stage.LightFade == 1 && stage.CharacterGraphic(2).Animation == "danceRight", "Retry retained train, lights, or actor state.");
        Debug.Log("WEEK 3 STAGE PASSED: " + song.vanillaPlayback.SongId + " " + Song.difficulty + ", source actors, train cue, eight cars, pause, split vocals, and reset.");
    }
}
