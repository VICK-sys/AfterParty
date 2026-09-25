using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FridayNightFunkin;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class VanillaMixValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Difficulties = { "Easy", "Normal", "Hard" };
    private static readonly string[] Roles = { "player", "opponent", "girlfriend" };
    private static readonly string[] EventKinds = { "EnableMask", "FocusCamera", "PlayAnimation", "ScrollSpeed", "SetCameraBop", "SetHealthIcon", "ZoomCamera" };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static JObject Read(string path) => JObject.Parse(File.ReadAllText(path));
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static int Week(string path) => int.Parse(Path.GetFileName(Path.GetDirectoryName(path)).Substring(0, 2));
    private static bool IsMix(JToken data) => (string)data["variation"] == "pico" || (string)data["variation"] == "bf";

    public static void CheckAssets()
    {
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles");
        var catalog = VanillaFreeplayCatalog.Discover(root);
        Require(catalog.Count == 44, "Expected 44 distinct Freeplay songs after importing Spaghetti.");
        Require(catalog.Select(song => song.id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 44, "Freeplay contains duplicate paths.");
        Require(catalog.Count(song => ((string)song.Details("Hard")["playData"]["characters"]["player"]).StartsWith("pico")) == 19,
            "Pico Freeplay catalog must contain 15 mixes and four Weekend songs.");
        Require(catalog.Count(song => ((string)song.Details("Hard")["playData"]["characters"]["player"]).StartsWith("bf")) == 24,
            "BF Freeplay catalog must contain 22 originals and two BF mixes.");
        var manifest = Read(Path.Combine(root, "vanilla-import.json"))["songs"].ToArray();
        var mixes = manifest.Where(IsMix).ToArray();
        Require(mixes.Length == 17 && mixes.Count(entry => (string)entry["variation"] == "pico") == 15, "Mix manifest is incomplete.");
        Require(manifest.Select(entry => (string)entry["id"] + "/" + (string)entry["variation"]).Distinct().Count() == manifest.Length,
            "Manifest contains duplicate song variations.");
        var graphics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var events = new List<JToken>();
        int heads = 0;
        int charts = 0;
        foreach (JToken entry in mixes)
        {
            string path = Path.GetFullPath(Path.Combine(root, (string)entry["path"]));
            var row = catalog.Single(song => string.Equals(song.id, path, StringComparison.OrdinalIgnoreCase));
            JObject sidecar = Read(Path.Combine(path, "Vanilla.json"));
            JObject source = Read(Path.Combine(path, "Source/chart.json"));
            JObject metadata = Read(Path.Combine(path, "Source/metadata.json"));
            Require((string)sidecar["song"] == (string)entry["id"] && (string)sidecar["variation"] == (string)entry["variation"], "Mix identity changed: " + path);
            Require(row.meta.difficulties.Keys.SequenceEqual(Difficulties), "Mix exposes incorrect difficulties: " + path);
            Require(row.Title("Hard") == (string)metadata["songName"], "Freeplay displays the original song title for a mix.");
            Require(JToken.DeepEquals(sidecar["characters"], metadata["playData"]["characters"]), "Mix character metadata changed.");
            Require(JToken.DeepEquals(sidecar["events"], source["events"]), "Mix events changed during import.");
            events.AddRange(sidecar["events"]);
            var originalEntry = manifest.Single(item => (string)item["id"] == (string)entry["id"] && string.IsNullOrEmpty((string)item["variation"]));
            string originalPath = Path.GetFullPath(Path.Combine(root, (string)originalEntry["path"]));
            Require(originalPath != path && catalog.Any(song => string.Equals(song.id, originalPath, StringComparison.OrdinalIgnoreCase)), "Mix replaced the original Freeplay row.");
            Require((string)Read(Path.Combine(originalPath, "Vanilla.json"))["characters"]["player"] != (string)sidecar["characters"]["player"],
                "Original-player negative control did not differ from its mix.");
            foreach (string difficulty in Difficulties)
            {
                Require(JToken.DeepEquals(row.Details(difficulty), metadata), "Freeplay selected mismatched mix metadata.");
                var chart = new FNFSong(Path.Combine(path, "Chart-" + difficulty.ToLowerInvariant() + ".json"));
                int count = chart.Sections.Sum(section => section.Notes.Count());
                Require(count == source["notes"][difficulty.ToLowerInvariant()].Count() && count > 0, "Parsed mix chart has the wrong note count.");
                heads += count;
                charts++;
            }
            foreach (string role in Roles)
            {
                string folder = Path.Combine(root, "Week" + Week(path) + "Assets/characters", (string)sidecar["characters"][role]);
                Require(File.Exists(Path.Combine(folder, "character.json")), "Missing mix character: " + folder);
                var character = Read(Path.Combine(folder, "character.json"));
                var actorGraphics = Directory.GetFiles(folder, "graphic.json", SearchOption.AllDirectories);
                var animations = new HashSet<string>(actorGraphics.SelectMany(file => ((JObject)Read(file)["animations"]).Properties().Select(property => property.Name)));
                foreach (JToken animation in character["animations"].Where(animation => (string)animation["name"] != "fakeoutDeath"))
                    Require(animations.Contains((string)animation["name"]), "Missing alternate character animation: " + folder + "/" + (string)animation["name"]);
                if (role == "player")
                    Require(new[] { "firstDeath", "deathLoop", "deathConfirm" }.All(animations.Contains), "Mix player lacks game-over animations: " + folder);
                foreach (string graphic in actorGraphics) graphics.Add(graphic);
            }
        }
        Require(charts == 51 && heads == 29804 && events.Count == 1329, "Mix chart or event totals changed.");
        Require(events.Select(entry => (string)entry["e"]).Distinct().OrderBy(kind => kind, StringComparer.Ordinal).SequenceEqual(EventKinds), "Unsupported or missing mix event kind.");
        Require(events.Count(entry => (string)entry["e"] == "ScrollSpeed") == 15 && events.Count(entry => (string)entry["e"] == "EnableMask") == 1
            && events.Count(entry => (string)entry["e"] == "SetHealthIcon") == 1, "South scroll or Stress mask/icon events changed.");
        foreach (var level in VanillaStoryCatalog.Load())
        {
            Require(VanillaStoryCatalog.TryPlaylist(level, "hard", root, out var playlist, out string missing), "Story playlist unavailable: " + missing);
            Require(playlist.Count == level.songs.Length, "Mix import changed the Story song count.");
            foreach (var song in playlist)
                Require(string.IsNullOrEmpty((string)Read(Path.Combine(song.meta.songPath, "Vanilla.json"))["variation"]), "Story selected a Freeplay mix.");
            Require(!VanillaStoryCatalog.TryPlaylist(level, "pico", root, out _, out _) && !VanillaStoryCatalog.TryPlaylist(level, "bf", root, out _, out _),
                "Variation-as-difficulty negative control passed.");
        }
        foreach (string path in graphics) CheckGraphic(path);
        Require(FunkinHudAssets.Icon("missing-mix-control")[0] == FunkinHudAssets.Icon("face")[0], "Missing-icon fallback control failed.");
        foreach (string icon in new[] { "pico", "pico-pixel", "tankman-bloody", "darnell" })
            Require(FunkinHudAssets.Icon(icon)[0] != FunkinHudAssets.Icon("face")[0], "Mix icon silently falls back: " + icon);
        Debug.Log("MIX ASSETS PASSED: 44 catalog rows, 19 Pico and 24 BF players, Spaghetti, 51 parsed mix charts, 29804 heads, 1329 events, Story originals, meshes and negative controls.");
    }

    private static void CheckGraphic(string path)
    {
        var probe = new GameObject("Mix Graphic Probe");
        try
        {
            var graphic = probe.AddComponent<VanillaWeek2Graphic>();
            graphic.Load(Path.GetDirectoryName(path), 0);
            Require(!graphic.Play("missing-mix-control"), "Missing animation control passed.");
            foreach (var animation in ((JObject)Read(path)["animations"]).Properties())
            {
                Require(graphic.Play(animation.Name), "Mix animation failed to load: " + path + "/" + animation.Name);
                graphic.Advance(0, Vector3.zero, 0);
                Require(probe.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "Mix animation produced an empty mesh: " + path + "/" + animation.Name);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(probe); }
    }

    public static void CheckStage(Song song)
    {
        var playback = song.vanillaPlayback;
        Require(playback != null && playback.CharacterStage != null, "Mix playback or character stage is unavailable.");
        JObject sidecar = Read(song.selectedVanillaPath);
        bool mix = IsMix(sidecar);
        Require(playback.Variation == (string)sidecar["variation"] && playback.IsErect == ((string)sidecar["variation"] == "erect"), "Mix variation was inferred from its Erect stage.");
        Require(playback.IsPixel == ((string)sidecar["noteStyle"] == "pixel") && playback.UsesSourceCamera, "Mix note style or source camera is incorrect.");
        object stage = (object)playback.CampaignStage ?? (object)playback.Week3Stage ?? playback.Week2Stage;
        var component = (MonoBehaviour)stage;
        for (int index = 0; index < Roles.Length; index++)
        {
            string expected = (string)sidecar["characters"][Roles[index]];
            string actual = (string)stage.GetType().GetMethod("CharacterId").Invoke(stage, new object[] { index });
            Require(actual == expected, "Wrong mix actor in role " + Roles[index] + ": " + actual);
            var graphic = (VanillaWeek2Graphic)stage.GetType().GetMethod("CharacterGraphic").Invoke(stage, new object[] { index });
            Require(graphic != null && graphic.gameObject.activeInHierarchy && graphic.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0,
                "Mix actor is hidden or empty: " + expected);
            Require(!graphic.Has("missing-mix-control"), "Missing live animation control passed.");
            if (playback.IsPixel) Require(Field<Texture2D>(graphic, "texture").filterMode == FilterMode.Point, "Pixel mix actor uses bilinear filtering.");
            if ((string)sidecar["stage"] == "schoolErect" || (string)sidecar["stage"] == "tankmanBattlefieldErect")
                Require(graphic.HasRim, "Mix actor has no source rim lighting: " + expected);
        }
        Require(song.defaultSceneObjects.All(item => !item.activeSelf), "Original stage overlaps the mix stage.");
        if (mix) CheckPositions(song, component, sidecar);
        if (playback.SongId == "stress" && playback.Variation == "pico")
        {
            CheckRunnerLoading(song);
            var campaign = playback.CampaignStage;
            JObject runnerChart = Field<JObject>(campaign, "chart");
            JToken variation = runnerChart["variation"].DeepClone();
            try
            {
                foreach (string mode in new[] { "pico", "" })
                {
                    runnerChart["variation"] = mode;
                    foreach (bool right in new[] { false, true })
                    {
                        campaign.SpawnRunner(1000000, right);
                        foreach (var runner in component.GetComponentsInChildren<VanillaWeek2Graphic>().Where(graphic => graphic.name.StartsWith("Running Tankman")))
                        {
                            float y = -runner.Position.y * 100;
                            Require(mode == "pico" ? Mathf.Abs(y - 350) < .001f : y >= 250 && y <= 300,
                                "Stress runner height differs from the source: " + mode + "/" + y);
                            runner.gameObject.SetActive(false);
                        }
                    }
                }
            }
            finally
            {
                runnerChart["variation"] = variation;
                campaign.ResetStage();
            }
            Debug.Log("STRESS RUNNER POSITIONS PASSED: both directions at Pico Y=350 and original Y=250..300.");
            CheckRunnerRendering(song);
        }
        if (playback.IsWeek3 && playback.Variation == "pico")
        {
            var week3 = playback.Week3Stage;
            Require(Enumerable.Range(0, 3).All(index => week3.CharacterGraphic(index).PhillyColor == week3.Erect)
                && week3.PropGraphic("train").PhillyColor == week3.Erect, "Week 3 Pico mix color filter changed.");
            Vector3[] corners = { new Vector3(6.6f, -3.44f), new Vector3(.655f, -3.44f), new Vector3(5.09f, -1.32f) };
            Vector3[] offsets = { new Vector3(.19f, -.01f), new Vector3(-.23f, -.01f), new Vector3(.04f, .89f) };
            for (int index = 0; index < 3; index++)
            {
                var actor = week3.CharacterGraphic(index);
                Require(Vector3.Distance(actor.Position, corners[index]) < .001f, "Week 3 mix character corner differs from source feet placement.");
                Require(Vector3.Distance(actor.GlobalOffset, offsets[index]) < .001f, "Week 3 mix omitted its source drawing offset.");
                Require(Vector3.Distance(actor.GlobalOffset, Vector3.zero) > .01f, "Missing-offset negative control passed.");
            }
            var bot = Field<VanillaMixCompanion>(week3, "companion");
            Require(Vector3.Distance(bot.Body.Position, new Vector3(4.18f, -4.27f)) < .001f, "Week 3 A-Bot differs from its source position.");
            var render = typeof(VanillaWeek3Stage).GetMethod("Render", Private);
            var nene = week3.CharacterGraphic(2);
            float health = song.health;
            foreach (bool knife in new[] { false, true })
            {
                week3.ResetStage();
                song.health = knife ? 25 : 100;
                if (knife)
                {
                    bot.Advance(0, song.mainCamera.transform.position, 0);
                    bot.Beat();
                    nene.Advance(14f / 24, song.mainCamera.transform.position, 0);
                    bot.Advance(0, song.mainCamera.transform.position, 0);
                }
                bot.Train(true);
                for (int cycle = 0; cycle < 4; cycle++)
                {
                    render.Invoke(week3, new object[] { .4f });
                    Require(nene.Animation == (knife ? "hairBlowKnife" : "hairBlowNormal"), "Nene train pose changed.");
                    Require(nene.GetComponent<MeshFilter>().sharedMesh == Field<Mesh>(nene, "compositeMesh"),
                        "Nene hair restart replaced the composited mesh after rendering.");
                }
                bot.Advance(0, song.mainCamera.transform.position, 0);
                Require(nene.GetComponent<MeshFilter>().sharedMesh != Field<Mesh>(nene, "compositeMesh"),
                    "Late hair restart negative control did not reproduce the mesh mismatch.");
                bot.Train(false);
                render.Invoke(week3, new object[] { 0f });
                Require(nene.Animation == (knife ? "hairFallKnife" : "hairFallNormal")
                    && nene.GetComponent<MeshFilter>().sharedMesh == Field<Mesh>(nene, "compositeMesh"),
                    "Nene hair landing did not render with its composite mesh.");
            }
            song.health = health;
            week3.ResetStage();
            Debug.Log("NENE TRAIN RENDER PASSED: normal and knife hair loops, landing, and late-restart negative control.");
        }
        Require(song.FunkinHud != null && song.player1NoteSprites.All(item => item != null && item.enabled)
            && song.player2NoteSprites.All(item => item != null && item.enabled), "Mix HUD or strumline is unavailable.");
        Require(song.musicClip.loadType == AudioClipLoadType.DecompressOnLoad, "Mix instrumental cannot supply source PCM to A-Bot.");
        Require(song.OpponentVocals != null && song.OpponentVocals.clip != null && song.SplitPlayerVocalsPath != null,
            "Mix did not load independent vocal stems.");
        Require(song.SplitPlayerVocalsPath == Path.Combine(song.selectedSongDir, "Voices-player" + (playback.IsErect ? "-erect" : "") + ".ogg"),
            "Mix loaded the wrong player vocal stem.");
        if (mix)
        {
            Require(Path.GetFileName(song.selectedInstrumentalPath) == "Inst.ogg" && Path.GetFileName(song.selectedVocalsPath) == "Voices.ogg",
                "Independent mix row selected an unrelated asset suffix.");
            Require(!playback.IsErect, "Pico or BF mix incorrectly reports the Erect variation.");
        }
        Sprite[][] frames = Field<Sprite[][]>(song.FunkinHud, "iconFrames");
        Require(frames[0][0] == FunkinHudAssets.Icon(playback.PlayerId)[0], "HUD kept the previous player icon.");
        Require(frames[0][0] != FunkinHudAssets.Icon("face")[0], "Player icon used the fallback face.");
        var companion = Field<VanillaMixCompanion>(stage, "companion");
        string girlfriend = (string)sidecar["characters"]["girlfriend"];
        if (mix && (girlfriend.StartsWith("nene") || girlfriend == "otis-speaker"))
        {
            Require(companion != null && companion.Body != null && companion.Body.gameObject.activeInHierarchy, "Pico mix is missing A-Bot.");
            Require(companion.Graphics.Count() >= 10, "Pico mix is missing A-Bot parts or visualizer bars.");
            var bars = Field<VanillaWeek2Graphic[]>(companion, "bars");
            Require(bars.Length == 7 && bars.All(bar => bar != null && bar.gameObject.activeInHierarchy), "Pico mix visualizer has missing bars.");
            if (playback.IsPixel)
            {
                var actor = playback.CampaignStage.CharacterGraphic(2);
                Vector3 origin = actor.Position + Vector3.Scale(actor.GlobalOffset, new Vector3(6, 6, 1));
                Require(Vector3.Distance(companion.Body.Position, origin + new Vector3(.36f, -3)) < .001f,
                    "Pixel A-Bot omitted the source centered scaling origin.");
            }
            if (playback.IsPixel)
                Require(companion.Graphics.All(graphic => Field<Texture2D>(graphic, "texture").filterMode == FilterMode.Point), "Pixel A-Bot uses bilinear filtering.");
            Require(frames[0][0] != FunkinHudAssets.Icon("bf")[0], "Pico mix retained the BF icon.");
            Require(song.deadNoise != null && song.deadTheme != null && song.deadConfirm != null, "Pico game-over audio did not load.");
            string loss = playback.PlayerId == "pico-pixel" ? "pixel-pico" : playback.PlayerId == "pico-holding-nene" ? "pico-and-nene" : "pico";
            Require(song.deadNoise == Resources.Load<AudioClip>("FunkinHud/Pico/loss-" + loss), "Pico mix selected the wrong death sound.");
        }
        else if ((string)sidecar["variation"] == "bf")
        {
            Require(companion == null && playback.PlayerId == "bf" && girlfriend == "gf", "BF mix retained Pico companions.");
            Require(Field<VanillaWeek2Graphic>(stage, "abot") == null && !component.GetComponentsInChildren<VanillaWeek2Graphic>(true).Any(graphic => graphic.name == "abotSystem"),
                "BF mix contains Weekend A-Bot graphics.");
            Require(frames[0][0] == FunkinHudAssets.Icon("bf")[0], "BF mix retained the Pico icon.");
        }
        if (playback.IsCampaign || playback.IsWeek3 && (string)sidecar["variation"] == "pico")
            Require(playback.Presentation != null, "Mix presentation controller did not attach.");
        Require(Field<FNFSong>(song, "_song").Sections.Sum(section => section.Notes.Count()) > 0, "Mix started without chart notes.");
        Debug.Log("MIX STAGE PASSED: " + playback.SongId + "/" + playback.Variation + ", actors, meshes, split vocals, HUD, companions, filters, rims and variation controls.");
    }

    private static void CheckPositions(Song song, MonoBehaviour stage, JObject sidecar)
    {
        var sizes = new Dictionary<string, Vector2>
        {
            ["pico-playable"] = new Vector2(759, 542), ["pico"] = new Vector2(759, 542),
            ["pico-dark"] = new Vector2(759, 572), ["spooky-dark"] = new Vector2(564, 531),
            ["dad"] = new Vector2(566, 841), ["nene"] = new Vector2(493, 566),
            ["nene-dark"] = new Vector2(493, 566), ["nene-tankmen"] = new Vector2(493, 566),
            ["pico-christmas"] = new Vector2(1063, 805), ["parents-christmas"] = new Vector2(977, 819),
            ["nene-christmas"] = new Vector2(493, 576), ["pico-pixel"] = new Vector2(136, 85),
            ["nene-pixel"] = new Vector2(75, 74), ["senpai"] = new Vector2(124, 163),
            ["senpai-angry"] = new Vector2(125, 164), ["tankman"] = new Vector2(634, 649),
            ["tankman-bloody"] = new Vector2(634, 649), ["pico-holding-nene"] = new Vector2(849, 588),
            ["otis-speaker"] = new Vector2(1147, 506), ["bf"] = new Vector2(496, 513),
            ["darnell"] = new Vector2(738, 736), ["gf"] = new Vector2(748, 674)
        };
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week" + Week(song.selectedSongDir) + "Assets");
        JObject sourceStage = Read(Path.Combine(root, "stages", (string)sidecar["stage"], "stage.json"));
        var graphics = stage.GetComponentsInChildren<VanillaWeek2Graphic>(true);
        var cameras = song.vanillaPlayback.CampaignStage?.CameraTargets ?? song.vanillaPlayback.Week2Stage?.CameraTargets ?? song.vanillaPlayback.Week3Stage.CameraTargets;
        string[] stageRoles = { "bf", "dad", "gf" };
        for (int index = 0; index < Roles.Length; index++)
        {
            string id = (string)sidecar["characters"][Roles[index]];
            JObject character = Read(Path.Combine(root, "characters", id, "character.json"));
            JToken placement = sourceStage["characters"][stageRoles[index]];
            float scale = ((float?)character["scale"] ?? 1) * ((float?)placement["scale"] ?? 1);
            Vector2 size = sizes[id] * scale;
            Vector3 offset = SourcePoint(character["offsets"]);
            Vector3 corner = SourcePoint(placement["position"]) + new Vector3(-size.x / 200, size.y / 100) + offset;
            Vector3 camera = corner + new Vector3(size.x / 200, -size.y / 200, -10)
                + SourcePoint(character["cameraOffsets"]) + SourcePoint(placement["cameraOffsets"]);
            Require(Vector3.Distance(cameras[index], camera) < .00005f, "Mix camera differs from source hitbox: " + id);
            foreach (var graphic in graphics.Where(item => item.name == id || item.name.StartsWith(id + " alternate") || item.name == id + " censor"))
            {
                Require(Vector3.Distance(graphic.Position, corner) < .00005f, "Mix position differs from source hitbox: " + graphic.name);
                Require(Vector3.Distance(graphic.GlobalOffset, offset) < .00005f, "Mix drawing offset differs from source: " + graphic.name);
            }
            if (id.EndsWith("-dark"))
            {
                string normal = id == "pico-dark" ? "pico-playable" : id.Substring(0, id.Length - 5);
                Vector3 lightOffset = SourcePoint(Read(Path.Combine(root, "characters", normal, "character.json"))["offsets"]);
                foreach (var light in graphics.Where(item => item.name.StartsWith(normal + " lightning")))
                    Require(Vector3.Distance(light.Position, corner) < .00005f && Vector3.Distance(light.GlobalOffset, lightOffset) < .00005f,
                        "Lightning overlay differs from source character position: " + light.name);
            }
            if (index == 2 && (id.StartsWith("nene") || id == "otis-speaker"))
            {
                var companion = Field<VanillaMixCompanion>(stage, "companion");
                Vector3 delta = id == "nene-pixel" ? new Vector3(.36f, -3) : id == "otis-speaker" ? new Vector3(1.7f, -4.55f) : new Vector3(-.95f, -3.84f);
                Require(Vector3.Distance(companion.Body.Position, corner + offset * scale + delta) < .00005f, "Mix companion position differs from source: " + id);
            }
        }
        Debug.Log("MIX POSITIONS PASSED: " + song.vanillaPlayback.SongId + ", source hitboxes, global offsets, alternate atlases, cameras and companions.");
    }

    private static Vector3 SourcePoint(JToken value) => value == null ? Vector3.zero : new Vector3((float)value[0] / 100, -(float)value[1] / 100);
}
