using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class VanillaWeekend1Validation
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void CheckAssets()
    {
        var level = VanillaStoryCatalog.Load().Single(item => item.id == "weekend1");
        foreach (string difficulty in new[] { "easy", "normal", "hard" })
        {
            Require(VanillaStoryCatalog.TryPlaylist(level, difficulty, out var songs, out _), "Weekend playlist unavailable.");
            Require(songs.Count == 4, "Weekend playlist must include Blazin.");
            Require(songs.Select(item => (string)item.Details(difficulty)["playData"]["characters"]["player"]).All(id => id.StartsWith("pico")), "Weekend player differs from source.");
        }
        Require(!VanillaStoryCatalog.TryPlaylist(level, "bf", out _, out _), "BF mix control passed.");
        Require(!VanillaStoryCatalog.TryPlaylist(level, "pico", out _, out _), "Pico mix control passed.");
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week8Assets");
        foreach (string path in Directory.GetFiles(root,"graphic.json",SearchOption.AllDirectories))
        {
            var obj = new GameObject("Weekend Mesh Probe");
            try
            {
                var graphic = obj.AddComponent<VanillaWeek2Graphic>();
                graphic.Load(Path.GetDirectoryName(path),0);
                Require(!graphic.Play("missing-control"), "Missing animation control passed.");
                foreach (var animation in ((JObject)JObject.Parse(File.ReadAllText(path))["animations"]).Properties())
                {
                    graphic.Play(animation.Name);
                    graphic.Advance(0,Vector3.zero,0);
                    Require(obj.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0,"Empty mesh: "+path+"/"+animation.Name);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
        foreach (string name in new[] {"bfChill","picoChill","lockedChill","gfChill","neneChill","crowd","charSelectStage","charSelectSpeakers","barThing","lock"})
        {
            var obj = new GameObject("Character Select Probe",typeof(RectTransform));
            try
            {
                var graphic = obj.AddComponent<VanillaFreeplayAnimate>();
                graphic.Initialize("charSelect/"+name,true);
                graphic.PlayAll(true);
                if (name.EndsWith("Chill")) Require(graphic.HasLabel("idle"), "Character select idle unavailable.");
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
        Debug.Log("WEEKEND 1 ASSETS PASSED: playlists, players, animation meshes, character select atlases, unavailable mix controls.");
    }

    public static void CheckStage(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        Require(stage != null && stage.Week == 8,"Weekend stage unavailable.");
        Require(stage.CharacterId(0).StartsWith("pico"),"Wrong player.");
        Require(song.defaultSceneObjects.All(item => !item.activeSelf),"Default stage overlaps Weekend.");
        if (Environment.GetEnvironmentVariable("UNITY_PARTY_VISUALIZER_TEST") == "1")
        {
            CheckVisualizer(song);
            return;
        }
        JObject data = JObject.Parse(File.ReadAllText(song.selectedVanillaPath));
        var notes = data["noteKinds"][Song.difficulty.ToLowerInvariant()];
        if (song.vanillaPlayback.SongId == "2hot")
        {
            for (int i = 0; i < 3; i++)
            {
                int textureLoads = VanillaWeek2Graphic.TextureLoads;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                stage.SpawnCan(false);
                double spawn = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                stage.ShootCan();
                Debug.Log("SPRAY CAN COST MS: spawn=" + spawn + ", shot=" + watch.Elapsed.TotalMilliseconds);
                Require(VanillaWeek2Graphic.TextureLoads == textureLoads, "Spray can shot decoded a texture during gameplay.");
                stage.ResetStage();
            }
            var cock = notes.First(note => (string)note["k"] == "weekend-1-cockgun");
            stage.Hit(0, (int)cock["d"] % 4, (double)cock["t"]);
            var ghost = stage.GetComponentsInChildren<VanillaWeek2Graphic>().Single(item => item.name == "Pico Gun Afterimage");
            Require(!ghost.ApplyAnimationOffsets && ghost.GlobalOffset == Vector3.zero && ghost.CompositeAlpha, "Afterimage retained character offsets or separate limb opacity.");
            Vector3 center = ghost.Position + new Vector3(ghost.Size.x, -ghost.Size.y) / 200;
            Vector3 anchor = stage.CharacterGraphic(0).Position + new Vector3(-1, 2);
            Require(Vector3.Distance(ghost.Position, anchor) < .001f, "Afterimage does not use the original captured-frame anchor.");
            stage.AdvanceWeekend1(.2f);
            Vector3 scaledCenter = ghost.Position + new Vector3(ghost.Size.x, -ghost.Size.y) * (ghost.transform.localScale.x / 200);
            Require(Vector3.Distance(center, scaledCenter) < .001f && Mathf.Abs(ghost.transform.localScale.x - 1.15f) < .001f, "Afterimage expansion moved its center.");
            typeof(VanillaCampaignStage).GetMethod("Render", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(stage, new object[] { 0f });
            Vector3 cameraPosition = song.mainCamera.transform.position;
            song.mainCamera.transform.position = stage.CameraTargets[0];
            VanillaSongValidation.CaptureStage("2hot-afterimage.png");
            song.mainCamera.transform.position = cameraPosition;
            stage.ResetStage();
            JToken fire = notes.First(note => (string)note["k"] == "weekend-1-firegun");
            Require(!stage.CanHitWeekendNote(0,(int)fire["d"]%4,(double)fire["t"]),"Unprepared gun control passed.");
            foreach (var note in notes.TakeWhile(note => (double)note["t"] <= (double)fire["t"]))
                stage.Hit((int)note["d"]/4,(int)note["d"]%4,(double)note["t"]);
            Require(stage.CansShot == 1,"Prepared gun did not shoot the can.");
            stage.ResetStage();
            float health = song.health;
            stage.Miss(0,(int)fire["d"]%4,(double)fire["t"]);
            Require(stage.CansMissed == 1 && song.health == health - 42,"Can damage differs from the source after normal miss compensation.");
            song.health = health;
        }
        if (song.vanillaPlayback.SongId == "blazin")
        {
            CheckBlazinPlacement(song);
            var note = notes.First(item => (string)item["k"] == "weekend-1-punchlow");
            stage.Hit(0,(int)note["d"],(double)note["t"]);
            Require(stage.CharacterGraphic(0).Animation.StartsWith("punchLow") && stage.CharacterGraphic(1).Animation == "hitLow","Combat hit failed.");
            stage.Miss(0,(int)note["d"],(double)note["t"]);
            Require(stage.CharacterGraphic(0).Animation == "hitLow" && stage.CharacterGraphic(1).Animation.StartsWith("punchLow"),"Combat miss control failed.");
            var prep = notes.First(item => (string)item["k"] == "weekend-1-picouppercutprep");
            var uppercut = notes.First(item => (string)item["k"] == "weekend-1-picouppercut");
            stage.Miss(0,(int)prep["d"],(double)prep["t"]);
            stage.Hit(0,(int)uppercut["d"],(double)uppercut["t"]);
            Require(stage.CharacterGraphic(0).Animation == "block" && stage.CharacterGraphic(1).Animation == "uppercutHit","Missed uppercut preparation changed Darnell's independent response.");
            stage.Hit(0,(int)prep["d"],(double)prep["t"]);
            stage.Hit(0,(int)uppercut["d"],(double)uppercut["t"]);
            Require(stage.CharacterGraphic(0).Animation == "uppercut" && stage.CharacterGraphic(1).Animation == "uppercutHit","Prepared uppercut control failed.");
            Require(song.player2NoteSprites.All(item => !item.enabled),"Blazin opponent strumline visible.");
        }
        stage.ResetStage();
        CheckNeneReactions(song);
        CheckVisualizer(song);
        if (song.vanillaPlayback.SongId == "blazin") CheckBlazinPlacement(song);
        Require(stage.CansShot == 0 && stage.CansMissed == 0 && stage.CombatNotes == 0 && !stage.GunCocked,"Retry retained Weekend state.");
        if (song.vanillaPlayback.SongId == "blazin") song.mainCamera.transform.position = song.vanillaPlayback.CameraFocusTarget;
        VanillaSongValidation.CaptureStage(song.vanillaPlayback.SongId+"-"+Song.difficulty+"-stage.png");
        if (song.vanillaPlayback.SongId == "2hot" && Environment.GetEnvironmentVariable("UNITY_PARTY_MISS_TEST") == "1") CheckIdleMisses(song, notes);
    }

    private static void CheckVisualizer(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var bars = (VanillaWeek2Graphic[])typeof(VanillaCampaignStage).GetField("vizBars", flags).GetValue(stage);
        var abot = (VanillaWeek2Graphic)typeof(VanillaCampaignStage).GetField("abot", flags).GetValue(stage);
        var setLevels = typeof(VanillaCampaignStage).GetMethod("SetWeekendVisualizerLevels", flags);
        JObject data = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Bundles/Week8Assets/effects/visualizer/graphic.json")));
        float[] x = { 207, 266, 322, 388, 442, 494, 545 };
        float[] y = { 84, 76, 72.5f, 72.1f, 72.6f, 77.3f, 84.3f };
        for (int i = 0; i < bars.Length; i++)
            Require(Vector3.Distance(bars[i].Position, abot.Position + new Vector3(x[i], -y[i]) / 100) < .00001f, "A-Bot visualizer source position differs: " + i);
        for (int height = 0; height <= 6; height++)
        {
            setLevels.Invoke(stage, new object[] { Enumerable.Repeat(height / 6f, 7).ToArray() });
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i].Advance(0, song.mainCamera.transform.position, 0);
                Require(bars[i].Alpha == (height == 0 ? 0 : 1), "A-Bot silence visibility differs from source.");
                int frame = height == 0 ? 5 : 6 - height;
                Require(bars[i].Frame == (int)data["animations"][(i + 1).ToString()]["frames"][frame], "A-Bot six-level frame mapping differs from source.");
            }
            if (height == 1 || height == 6) VanillaSongValidation.CaptureStage(song.vanillaPlayback.SongId + "-visualizer-" + height + ".png");
        }
        stage.AdvanceWeekend1(0);
        Debug.Log("A-BOT VISUALIZER PASSED: seven source positions, six frame heights and hidden silence controls.");
    }

    private static void CheckNeneReactions(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        var nene = stage.CharacterGraphic(2);
        float health = song.health;
        try
        {
            foreach (int combo in new[] { 50, 200 })
            {
                stage.Combo(combo, false);
                Require(nene.Animation == "combo" + combo, "Nene combo reaction is missing.");
            }
            stage.ResetStage();
            stage.Combo(69, true);
            Require(!nene.Animation.StartsWith("drop"), "Nene reacted below the drop threshold.");
            stage.Combo(70, true);
            Require(nene.Animation == "drop70", "Nene combo drop reaction is missing.");
            stage.ResetStage();
            song.health = 51;
            stage.AdvanceWeekend1(0);
            Require(nene.Animation != "raiseKnife", "Nene raised her knife above 25 percent health.");
            song.health = 50;
            int beat = 0;
            for (int frame = 0; frame < 180; frame++)
            {
                if (frame % 20 == 0) stage.WeekendBeat(beat++);
                nene.Advance(1f / 60, Vector3.zero, 0);
                stage.AdvanceWeekend1(1f / 60);
            }
            if (song.vanillaPlayback.SongId == "blazin")
                Require(nene.Animation != "raiseKnife" && nene.Animation != "idleKnife", "Blazin incorrectly enabled Nene's knife threat.");
            else
            {
                Require(nene.Animation == "idleKnife", "Nene never reached her low-health knife idle at 180 BPM.");
                song.health = 51;
                stage.AdvanceWeekend1(0);
                Require(nene.Animation != "lowerKnife", "Nene lowered her knife before the recovery beat.");
                stage.WeekendBeat(beat++);
                Require(nene.Animation == "lowerKnife", "Nene did not lower her knife on the recovery beat.");
                nene.Advance(nene.Duration + .01f, Vector3.zero, 0);
                stage.AdvanceWeekend1(0);
                Require(nene.Animation.StartsWith("dance"), "Nene did not resume dancing after recovery.");
            }
            stage.ResetStage();
            Debug.Log("NENE REACTIONS PASSED: combo50, combo200, drop70 threshold, low-health threat, recovery and Blazin exclusion.");
        }
        finally { song.health = health; }
    }

    private static void CheckIdleMisses(Song song, JToken notes)
    {
        var player = Player.instance;
        var line = player.Strumlines[0];
        var savedNotes = line.Notes.ToArray();
        bool bot = line.BotPlay;
        bool controlled = line.Controlled;
        bool downscroll = OptionsV2.Downscroll;
        float offset = Player.visualOffset;
        float speed = song.speedDifference;
        var render = typeof(NoteObject).GetMethod("Render", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        line.Notes.Clear();
        line.ClearInput();
        line.Controlled = true;
        line.BotPlay = false;
        try
        {
            foreach (bool down in new[] { false, true })
            foreach (double length in new[] { 0d, 500d })
            {
                OptionsV2.Downscroll = down;
                song.RefreshFunkinStrums();
                song.speedDifference = (song.ChartScrollSpeed - 4) / 100;
                NoteObject note = CreateMissProbe(song, 1000000, 0, length);
                Player.visualOffset = (float)(song.SongPosition - note.State.Time - 150);
                render.Invoke(note, null);
                var sprite = note.GetComponentInChildren<SpriteRenderer>();
                Require(down ? sprite.bounds.max.y < song.uiCamera.transform.position.y - song.uiCamera.orthographicSize
                    : sprite.bounds.min.y > song.uiCamera.transform.position.y + song.uiCamera.orthographicSize, "Early offscreen control did not leave the viewport.");
                Require(line.Notes.Contains(note.State), "Unjudged offscreen note was recycled before its miss window closed.");
                song.health = 100;
                int misses = song.playerOneStats.missedHits;
                line.Advance(note.State.Time + 159, .016);
                Require(song.health == 100 && song.playerOneStats.missedHits == misses, "Note missed inside the hit window.");
                line.Advance(note.State.Time + 161, .016);
                Require(song.health == 92 && song.playerOneStats.missedHits == misses + 1, "Idle note did not apply one normal miss.");
                line.Advance(note.State.Time + 180, .016);
                Require(song.health == 92, "Idle note applied duplicate damage.");
                song.ReleaseFunkinNote(note);
            }
            var stage = song.vanillaPlayback.CampaignStage;
            JToken fire = notes.First(note => (string)note["k"] == "weekend-1-firegun");
            foreach (float health in new[] { 100f, 50f })
            {
                stage.ResetStage();
                stage.SpawnCan(false);
                song.health = health;
                NoteObject note = CreateMissProbe(song, (double)fire["t"], (int)fire["d"] % 4, 0);
                line.Advance(note.State.Time + 161, .016);
                Require(song.health == health - 50 && stage.CansMissed == 1, "Idle can miss did not remove 25 percent of maximum health.");
                Require(stage.CharacterGraphic(0).Animation == "shootMISS", "Can miss did not hit Pico.");
                song.ReleaseFunkinNote(note);
                if (health > 50)
                {
                    stage.BeginDeath();
                    Require(stage.GetComponentsInChildren<VanillaWeek2Graphic>().Any(graphic => graphic.name == "Boyfriend Death" && graphic.Animation == "firstDeath"), "Surviving can incorrectly selected explosion death.");
                }
            }
            bool demo = Player.demoMode;
            Player.demoMode = false;
            try
            {
                typeof(Song).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(song, null);
                Require(song.isDead && song.deadCamera.enabled && !song.uiCamera.enabled, "Lethal idle can did not enter game over.");
            }
            finally { Player.demoMode = demo; }
            var death = stage.GetComponentsInChildren<VanillaWeek2Graphic>().Single(graphic => graphic.name == "Boyfriend Death" && graphic.Animation == "firstDeath-explosion");
            Require(song.deadNoise.name == "loss-pico-explode", "Lethal can did not select explosion audio.");
            foreach (string animation in new[] { "deathLoop", "deathConfirm" })
            {
                stage.PlayDeath(animation);
                Require(death.Animation == animation + "-explosion", "Explosion death transition failed: " + animation);
                death.Advance(0, Vector3.zero, 0);
                Require(death.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "Explosion death mesh is empty.");
            }
            stage.ResetStage();
            song.health = 100;
            Debug.Log("IDLE MISS VALIDATION PASSED: tap and sustain heads, upscroll and downscroll, early offscreen control, single damage, surviving and lethal cans, explosion intro, loop and confirm.");
        }
        finally
        {
            line.Notes.Clear();
            line.Notes.AddRange(savedNotes);
            line.BotPlay = bot;
            line.Controlled = controlled;
            OptionsV2.Downscroll = downscroll;
            Player.visualOffset = offset;
            song.speedDifference = speed;
            song.RefreshFunkinStrums();
        }
    }

    private static NoteObject CreateMissProbe(Song song, double time, int direction, double length)
    {
        Player.visualOffset = (float)(song.SongPosition - time + 1000);
        var pool = direction == 0 ? song.leftNotesPool : direction == 1 ? song.downNotesPool : direction == 2 ? song.upNotesPool : song.rightNotesPool;
        var note = pool.GetObject().GetComponent<NoteObject>();
        note.Initialize(song, time, direction, true, length, song.ChartScrollSpeed, 1);
        Player.instance.Strumlines[0].Add(note.State);
        return note;
    }

    private static void CheckBlazinPlacement(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week8Assets/characters");
        Vector2[] positions = { new Vector2(-740.75f, -905.25f), new Vector2(-355.375f, -525.75f), new Vector2(1110.5f, 470) };
        Vector2[] origins = { new Vector2(-534.7f, -556.05f), new Vector2(-451.25f, -532.95f), new Vector2(-329.75f, -423.6f) };
        Vector2[] hitboxes = { new Vector2(1274, 947), new Vector2(733, 645), new Vector2(493, 566) };
        int frames = 0;
        for (int side = 0; side < 3; side++)
        {
            var graphic = stage.CharacterGraphic(side);
            Vector3 expectedPosition = new Vector3(positions[side].x, -positions[side].y) / 100;
            Require(Vector3.Distance(graphic.Position, expectedPosition) < .00001f, "Blazin source position differs: " + graphic.name);
            Require(graphic.HitboxSize == hitboxes[side], "Blazin source hitbox differs: " + graphic.name);
            Require(!graphic.FlipX, "Blazin source facing differs: " + graphic.name);
            JObject data = JObject.Parse(File.ReadAllText(Path.Combine(root, stage.CharacterId(side), "graphic.json")));
            JObject character = JObject.Parse(File.ReadAllText(Path.Combine(root, stage.CharacterId(side), "character.json")));
            string previous = graphic.Animation;
            float scale = side == 2 ? 1 : 1.75f;
            Require(Mathf.Abs(graphic.transform.localScale.x - scale) < .00001f, "Blazin source scale differs.");
            try
            {
                foreach (JProperty animation in ((JObject)data["animations"]).Properties())
                {
                    if (animation.Name.Contains("Death") || animation.Name.StartsWith("death")) continue;
                    graphic.Play(animation.Name);
                    for (int frame = 0; frame < animation.Value["frames"].Count(); frame++)
                    {
                        graphic.SetAnimationFrame(frame);
                        graphic.Advance(0, Vector3.zero, 0);
                        var vertices = graphic.GetComponent<MeshFilter>().sharedMesh.vertices;
                        var quads = data["frames"][(int)animation.Value["frames"][frame]];
                        for (int corner = 0; corner < vertices.Length; corner++)
                        {
                            JToken xy = quads[corner / 4]["xy"];
                            float x = positions[side].x + ((float)character["offsets"][0] - (float)animation.Value["offset"][0]
                                + (float)xy[corner % 4 * 2] - origins[side].x) * scale;
                            float y = positions[side].y + ((float)character["offsets"][1] - (float)animation.Value["offset"][1]
                                + (float)xy[corner % 4 * 2 + 1] - origins[side].y) * scale;
                            Vector3 expected = new Vector3(x, -y) / 100;
                            Vector3 actual = graphic.transform.TransformPoint(vertices[corner]);
                            Require(Vector3.Distance(actual, expected) < .00002f, "Blazin source vertex differs: " + graphic.name + "/" + animation.Name + "/" + frame);
                            Require(Vector3.Distance(actual + Vector3.right * .01f, expected) > .005f, "Blazin shifted vertex control passed.");
                        }
                        frames++;
                    }
                }
            }
            finally
            {
                graphic.FrozenFrame = -1;
                graphic.Play(previous);
                graphic.Advance(0, Vector3.zero, 0);
            }
        }
        Require(Vector3.Distance(stage.CameraTargets[0], new Vector3(14.03f, -7.17f, -10)) < .00001f, "Blazin reference camera differs.");
        Require(stage.CameraTargets[0] == stage.CameraTargets[1] && stage.CameraTargets[0] == stage.CameraTargets[2]
            && Mathf.Abs(stage.CameraZoom - .75f) < .00001f, "Blazin camera focus or zoom differs.");
        Require(Vector3.Distance(song.vanillaPlayback.CameraFocusTarget, stage.CameraTargets[0]) < .00001f, "Blazin runtime camera retained the girlfriend focus.");
        Require(Vector3.Distance(song.mainCamera.transform.position, stage.CameraTargets[0]) < .1f, "Blazin rendered camera differs from the reference beyond combat shake: " + song.mainCamera.transform.position);
        Debug.Log("BLAZIN PLACEMENT PASSED: reference positions, hitboxes, camera, " + frames + " animation frames, shifted vertex control.");
    }

    public static void CheckRain(Song song, string output)
    {
        var host = new GameObject("Rain Probe");
        var camera = host.AddComponent<Camera>();
        camera.CopyFrom(song.mainCamera);
        camera.transform.SetPositionAndRotation(song.mainCamera.transform.position, song.mainCamera.transform.rotation);
        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var rain = host.AddComponent<VanillaWeekendRain>();
        var target = new RenderTexture(1280,720,24);
        camera.targetTexture = target;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData").Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        var previous = RenderTexture.active;
        Color32[] dry = null;
        try
        {
            foreach (var feature in features) feature.SetActive(feature is VanillaWeekendRainFeature);
            for (int pass = 0; pass < 2; pass++)
            {
                rain.Configure(4, pass == 0 ? 0 : .5f);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);
                image.Apply();
                if (pass == 0) dry = image.GetPixels32();
                else
                {
                    Require(image.GetPixels32().Zip(dry,(wet,control) => Math.Abs(wet.r-control.r)+Math.Abs(wet.g-control.g)+Math.Abs(wet.b-control.b)).Count(difference => difference > 6) > 128,
                        "Rain output matches the dry negative control.");
                    File.WriteAllBytes(Path.Combine(output,song.vanillaPlayback.SongId+"-rain.png"),image.EncodeToPNG());
                }
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
