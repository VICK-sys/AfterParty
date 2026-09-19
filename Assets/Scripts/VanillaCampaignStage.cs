using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(100)]
public sealed partial class VanillaCampaignStage : MonoBehaviour, IVanillaCharacterStage
{
    private static readonly float[] MistSpeeds = { 1700, 2100, 900, 700, 100 };
    private static readonly float[] MistCenters = { 100, 0, -20, -180, -450 };
    private static readonly float[] MistWaves = { 200, 100, 200, 300, 150 };
    private static readonly float[] MistFrequencies = { 1, .8f, .5f, .4f, .2f };
    public Vector3[] CameraTargets { get; } = new Vector3[3];
    public float CameraZoom { get; private set; }
    public string StageId { get; private set; }
    public int Week { get; private set; }
    public int PropCount => props.Count;
    public bool CarDriving { get; private set; }
    public int CarCount { get; private set; }
    public int ShootingStarCount { get; private set; }
    public float Clock => clock;
    public string CharacterId(int index) => actors[index].id;
    public VanillaWeek2Graphic CharacterGraphic(int index) => actors[index].current;
    public VanillaWeek2Graphic PropGraphic(string name) => props[name];
    private readonly Dictionary<string, VanillaWeek2Graphic> props = new Dictionary<string, VanillaWeek2Graphic>();
    private readonly List<(SpriteRenderer renderer, Vector3 position, Vector2 scroll)> solids = new List<(SpriteRenderer, Vector3, Vector2)>();
    private readonly List<VanillaWeek2Graphic> mist = new List<VanillaWeek2Graphic>();
    private readonly List<VanillaWeek2Graphic> trails = new List<VanillaWeek2Graphic>();
    private readonly List<(Renderer renderer, int order)> drawOrder = new List<(Renderer, int)>();
    private readonly Queue<(string animation, int frame)> trailAnimations = new Queue<(string, int)>();
    private readonly Actor[] actors = new Actor[3];
    private readonly AudioClip[] carClips = new AudioClip[2];
    private Song song;
    private JObject chart;
    private JObject stageData;
    private string root;
    private float clock;
    private float carAge;
    private float carVelocity;
    private float trailAge;
    private int lastBeat;
    private int lastDanceStep = int.MinValue;
    private int starBeat;
    private int starOffset;
    private AudioSource sound;
    private VanillaWeek2Graphic death;
    private Vector3 deathTarget;
    private VanillaMixCompanion companion;
    private bool eggnogOutroActive;
    private VanillaWeek2Graphic outroSanta;
    private VanillaWeek2Graphic outroParents;

    private sealed class Actor
    {
        public string id;
        public JObject data;
        public VanillaWeek2Graphic graphic;
        public VanillaWeek2Graphic censor;
        public VanillaWeek2Graphic current;
        public float holdTimer;
        public bool alternate;
        public bool locked;
        public bool bloody;
        public bool hidden;
        public bool beautiful;
        public readonly List<VanillaWeek2Graphic> alternates = new List<VanillaWeek2Graphic>();

        public void Play(string name, bool protect = false, bool reverse = false)
        {
            if (beautiful && !name.EndsWith("-beautiful")) name += "-beautiful";
            if (bloody && (graphic.Has(name + "-bloody") || censor != null && censor.Has(name + "-bloody") || alternates.Any(item => item.Has(name + "-bloody")))) name += "-bloody";
            var target = graphic.Has(name) ? graphic : censor != null && censor.Has(name) ? censor : alternates.FirstOrDefault(item => item.Has(name));
            if (target == null) return;
            graphic.gameObject.SetActive(!hidden && target == graphic);
            if (censor != null) censor.gameObject.SetActive(!hidden && target == censor);
            foreach (var item in alternates) item.gameObject.SetActive(!hidden && item == target);
            current = target;
            current.Play(name, reverse);
            if (id == "tankman-bloody" && name == "redheadsAnim") bloody = true;
            locked = protect;
        }

        public void Dance(bool force = false)
        {
            string name = VanillaCharacterTiming.DanceAnimation(current, force, locked, ref alternate);
            if (name != null) Play(name);
        }

        public void UpdateSinging(float delta, float stepMilliseconds, bool held)
        {
            if (!VanillaCharacterTiming.AdvanceSinging(data, current.Animation, ref holdTimer, delta, stepMilliseconds, held)) return;
            string end = VanillaCharacterTiming.SingEndAnimation(current);
            if (end != null) Play(end);
            else Dance(true);
        }
    }

    public static VanillaCampaignStage Create(Song owner, JObject data)
    {
        var stage = new GameObject("Campaign Stage").AddComponent<VanillaCampaignStage>();
        stage.song = owner;
        stage.chart = data;
        stage.Load();
        return stage;
    }

    private VanillaWeek2Graphic Graphic(string path, string title, int order)
    {
        var item = new GameObject(title).AddComponent<VanillaWeek2Graphic>();
        item.transform.SetParent(transform, false);
        item.Load(path, 0);
        SortRenderer(item.GetComponent<MeshRenderer>(), order);
        return item;
    }

    private void SortRenderer(Renderer renderer, int order)
    {
        drawOrder.RemoveAll(item => item.renderer == null);
        drawOrder.Add((renderer, order));
        int index = 0;
        foreach (var item in drawOrder.OrderBy(item => item.order)) item.renderer.sortingOrder = index++;
    }

    private void Load()
    {
        StageId = (string)chart["stage"];
        Week = StageId == "sserafim" ? 9 : StageId.StartsWith("mainStage") ? 1 : StageId.StartsWith("limo") ? 4 : StageId.StartsWith("mall") ? 5 : StageId.StartsWith("school") ? 6 : StageId.StartsWith("tankman") ? 7 : 8;
        root = Path.Combine(Application.streamingAssetsPath, Week == 9 ? "Bundles/SpaghettiAssets" : "Bundles/Week" + Week + "Assets");
        string directory = Path.Combine(root, "stages", StageId);
        stageData = JObject.Parse(File.ReadAllText(Path.Combine(directory, "stage.json")));
        CameraZoom = (float)stageData["cameraZoom"];
        foreach (GameObject item in song.defaultSceneObjects) item.SetActive(false);
        foreach (JToken prop in stageData["props"])
        {
            string name = (string)prop["name"];
            string asset = (string)prop["assetPath"];
            Vector2 scale = Scale(prop["scale"]);
            if (asset.StartsWith("#"))
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(transform, false);
                var solid = obj.AddComponent<SpriteRenderer>();
                solid.sprite = FunkinHudAssets.Solid;
                solid.sharedMaterial = FunkinNoteSkin.NoteMaterial;
                SortRenderer(solid, (int?)prop["zIndex"] ?? 0);
                ColorUtility.TryParseHtmlString(asset, out Color color);
                color.a *= (float?)prop["alpha"] ?? 1;
                solid.color = color;
                solid.transform.localPosition = Point(prop["position"]);
                solid.transform.localScale = new Vector3(scale.x, scale.y, 1);
                solids.Add((solid, solid.transform.localPosition, Scale(prop["scroll"])));
                continue;
            }
            var graphic = Graphic(Path.Combine(directory, name), name, (int?)prop["zIndex"] ?? 0);
            graphic.Position = Point(prop["position"]);
            graphic.Angle = (float?)prop["angle"] ?? 0;
            graphic.Scroll = Scale(prop["scroll"]);
            graphic.Alpha = (float?)prop["alpha"] ?? 1;
            graphic.FlipX = (bool?)prop["flipX"] ?? false;
            graphic.transform.localScale = new Vector3(scale.x, scale.y, 1);
            props.Add(name, graphic);
            graphic.Additive = StageId == "limoRideErect" && name == "shootingStar";
            if (StageId == "mainStageErect")
                graphic.Additive = new[] { "brightLightSmall", "orangeLight", "lightgreen", "lightred", "lightAbove" }.Contains(name);
            if (StageId.StartsWith("schoolEvil"))
            {
                if (name == "backTrees" || name == "backspikes") graphic.Wiggle = new Vector3(1.6f, 1.6f, .011f);
                if (name == "school") graphic.Wiggle = new Vector3(2, 4, .017f);
                if (name == "trees" || name == "street" || name == "evilstreet") graphic.Wiggle = new Vector3(2, 4, .007f);
                if (name == "backspike") graphic.Wiggle = new Vector3(2, 4, .01f);
            }
            if (StageId == "limoRideErect" && (name.StartsWith("limoDancer") || name == "fastCar"))
                graphic.ColorAdjustment = new Vector4(-30, -20, -30, 0);
            if (StageId == "mallXmasErect")
            {
                if (name == "santa") graphic.ColorAdjustment = new Vector4(5, 20, 0, 0);
                if (name == "bottomBoppers") graphic.ColorAdjustment = new Vector4(15, 0, 20, 0);
            }
        }
        string[] roles = { "player", "opponent", "girlfriend" };
        string[] stageRoles = { "bf", "dad", "gf" };
        for (int index = 0; index < actors.Length; index++)
        {
            string id = (string)chart["characters"][roles[index]] ?? "gf";
            string folder = Path.Combine(root, "characters", id);
            JObject character = JObject.Parse(File.ReadAllText(Path.Combine(folder, "character.json")));
            JToken placement = stageData["characters"][stageRoles[index]];
            if (index == 1 && id == "gf") placement = stageData["characters"]["gf"];
            var graphic = Graphic(folder, id, (int)placement["zIndex"]);
            Vector2 hitbox = graphic.HitboxSize;
            if ((string)chart["variation"] == "pico" || (string)chart["variation"] == "bf" || Week == 9)
                hitbox = new Vector2((int)hitbox.x, (int)hitbox.y);
            float scale = ((float?)character["scale"] ?? 1) * ((float?)placement["scale"] ?? 1);
            graphic.transform.localScale = new Vector3(scale, scale, 1);
            graphic.Position = Point(placement["position"]) + new Vector3(-hitbox.x * scale / 200, hitbox.y * scale / 100, 0)
                + Point(character["offsets"]);
            graphic.GlobalOffset = Point(character["offsets"]);
            if (Week == 9) graphic.Scroll = Scale(placement["scroll"]);
            graphic.FlipX = index == 0 ? !((bool?)character["flipX"] ?? false) : (bool?)character["flipX"] ?? false;
            if (StageId == "limoRideErect") graphic.ColorAdjustment = new Vector4(-30, -20, -30, 0);
            if (StageId == "mallXmasErect") graphic.ColorAdjustment = new Vector4(5, 20, 0, 0);
            if (StageId == "mainStageErect") graphic.ColorAdjustment = index == 0 ? new Vector4(12, 0, -23, 7)
                : index == 1 ? new Vector4(-32, 0, -33, -23) : new Vector4(-9, 0, -30, -4);
            if (StageId == "schoolErect")
            {
                string mask = id == "pico-pixel" ? "picoPixel_mask" : id == "nene-pixel" ? "nenePixel_mask" : index == 0 ? "bfPixel_mask" : index == 1 ? "senpai_mask" : "gfPixel_mask";
                graphic.SetRim(Path.Combine(root, "effects", mask + ".png"), index == 2 ? 3 : 5, index == 2 ? .3f : .1f,
                    index == 2 ? new Vector4(-10, -25, -42, 5) : new Vector4(-10, -23, -66, 24));
            }
            CameraTargets[index] = Point(placement["position"]) + new Vector3(0, hitbox.y * scale / 200, -10)
                + Point(character["offsets"]) + Point(character["cameraOffsets"]) + Point(placement["cameraOffsets"]);
            var actor = new Actor { id = id, data = character, graphic = graphic, current = graphic };
            if (Directory.Exists(Path.Combine(folder, "censor")))
            {
                actor.censor = Graphic(Path.Combine(folder, "censor"), id + " censor", (int)placement["zIndex"]);
                actor.censor.Position = graphic.Position;
                actor.censor.GlobalOffset = graphic.GlobalOffset;
                actor.censor.transform.localScale = graphic.transform.localScale;
                actor.censor.FlipX = graphic.FlipX;
                actor.censor.ColorAdjustment = graphic.ColorAdjustment;
                actor.censor.gameObject.SetActive(false);
            }
            if (Directory.Exists(folder))
                foreach (string extra in Directory.GetDirectories(folder).Where(path => Path.GetFileName(path).StartsWith("alternate")))
                {
                    var alternate = Graphic(extra, id + " alternate", (int)placement["zIndex"]);
                    alternate.Position = graphic.Position;
                    alternate.GlobalOffset = graphic.GlobalOffset;
                    alternate.transform.localScale = graphic.transform.localScale;
                    alternate.FlipX = graphic.FlipX;
                    alternate.ColorAdjustment = graphic.ColorAdjustment;
                    alternate.gameObject.SetActive(false);
                    actor.alternates.Add(alternate);
                }
            actors[index] = actor;
        }
        foreach (GameObject character in new[] { song.boyfriendObject, song.opponentObject, song.girlfriendObject })
            character.GetComponent<SpriteRenderer>().enabled = false;
        song.boyfriendAnimator.enabled = song.opponentAnimator.enabled = song.girlfriendAnimator.enabled = false;
        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.outputAudioMixerGroup = song.oopsSource.outputAudioMixerGroup;
        if (Week == 4) StartCoroutine(LoadCarAudio());
        if (StageId == "limoRideErect") LoadMist();
        if (actors[1].id == "spirit")
            for (int index = 0; index < 4; index++)
            {
                var trail = Graphic(Path.Combine(root, "characters/spirit"), "Spirit Trail " + index, 190 - index);
                trail.Position = actors[1].graphic.Position;
                trail.GlobalOffset = actors[1].graphic.GlobalOffset;
                trail.transform.localScale = actors[1].graphic.transform.localScale;
                trail.Alpha = .3f - index * .069f;
                trails.Add(trail);
            }
        if (Week == 7) LoadWeek7();
        if (Week == 8) LoadWeekend1();
        if (Week == 9) LoadSpaghetti();
        if (Week != 8 && (actors[2].id.StartsWith("nene") || actors[2].id == "otis-speaker"))
        {
            companion = new VanillaMixCompanion();
            companion.Initialize(song, actors[2].id, (int)stageData["characters"]["gf"]["zIndex"],
                (path, order) => Graphic(Path.Combine(root, path), Path.GetFileName(path), order),
                animation => actors[2].Play(animation, true), () => actors[2].current, SortRenderer);
            companion.ApplyLighting(root, Week);
        }
        if ((string)chart["song"] == "eggnog" && StageId == "mallXmasErect") PrepareEggnogGraphics();
        ResetStage();
    }

    private void LoadMist()
    {
        string[] colors = { "#c6bfde", "#6a4da1", "#a7d9be", "#9c77c7", "#E7A480" };
        float[] scales = { 1.3f, 1, 1.5f, 1.5f, 1.5f };
        float[] scrolls = { 1.1f, 1.2f, .8f, .6f, .2f };
        int[] orders = { 400, 401, 99, 98, 15 };
        for (int index = 0; index < 5; index++)
            for (int tile = -2; tile <= 2; tile++)
            {
                var graphic = Graphic(Path.Combine(root, "effects", index == 1 || index == 3 ? "mistBack" : "mistMid"), "Mist " + index + " " + tile, orders[index]);
                graphic.transform.localScale = new Vector3(scales[index], scales[index], 1);
                graphic.Scroll = Vector2.one * scrolls[index];
                ColorUtility.TryParseHtmlString(colors[index], out Color color);
                graphic.Tint = color;
                graphic.Additive = true;
                graphic.Alpha = index == 0 ? .4f : index == 2 ? .5f : 1;
                mist.Add(graphic);
            }
    }

    private IEnumerator LoadCarAudio()
    {
        for (int index = 0; index < carClips.Length; index++)
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(root, "audio/carPass" + index + ".ogg")).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
                carClips[index] = DownloadHandlerAudioClip.GetContent(request);
            }
    }

    public void ResetStage()
    {
        if (eggnogOutroActive) props["santa"].gameObject.SetActive(true);
        eggnogOutroActive = false;
        if (death != null) Destroy(death.gameObject);
        lastBeat = -1;
        lastDanceStep = int.MinValue;
        clock = trailAge = 0;
        starBeat = 0;
        starOffset = 2;
        CarCount = ShootingStarCount = 0;
        sound.Stop();
        foreach (JToken prop in stageData["props"])
            if (props.TryGetValue((string)prop["name"], out var graphic))
            {
                graphic.Position = Point(prop["position"]);
                graphic.Play((string)prop["startingAnimation"] ?? "idle");
            }
        foreach (Actor actor in actors)
        {
            actor.hidden = false;
            actor.holdTimer = 0;
            actor.bloody = false;
            actor.locked = actor.alternate = false;
            actor.Play((string)actor.data["startingAnimation"] ?? "idle");
        }
        if (chart["characters"]["girlfriend"] == null) actors[2].current.gameObject.SetActive(false);
        if (Week == 4) ResetCar();
        if (props.ContainsKey("freaks")) DanceProp("freaks", 0);
        trailAnimations.Clear();
        foreach (var trail in trails) { trail.Play("idle"); trail.gameObject.SetActive(false); }
        if (Week == 7) ResetWeek7();
        if (Week == 8) ResetWeekend1();
        if (Week == 9) ResetSpaghetti();
        companion?.Reset();
        Render(0);
    }

    public void Sing(int side, int direction, bool miss)
    {
        SingKind(side, direction, miss, "");
    }

    private void SingKind(int side, int direction, bool miss, string kind)
    {
        if (kind == "noanim") return;
        if (Week == 9) { SingSpaghetti(side, direction, miss, kind); return; }
        if (Week == 8 && WeekendNote(side, kind, miss)) return;
        Actor actor = actors[side];
        if (side == 1 && Week == 7 && (kind == "ugh" || kind == "hehPrettyGood"))
        {
            SpecialNoteHits++;
            actor.Play(kind, true);
            actor.holdTimer = 0;
            return;
        }
        string name = "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction] + (miss ? "miss" : "")
            + (kind == "mom" ? "-alt" : kind == "censor" && !VanillaPreferences.Naughtyness ? "-censor" : "");
        if (!actor.graphic.Has(name) && (actor.censor == null || !actor.censor.Has(name)) && !actor.alternates.Any(item => item.Has(name))) name = name.Replace("miss", "");
        actor.Play(name);
        if (!miss) actor.holdTimer = 0;
    }

    public void Hit(int side, int direction, double time)
    {
        var notes = chart["noteKinds"]?[Song.difficulty.ToLowerInvariant()];
        string kind = (string)notes?.FirstOrDefault(n => (int)n["d"] == direction + side * 4 && Math.Abs((double)n["t"] - time) < .02)?["k"];
        SingKind(side, direction, false, kind);
        if (Week == 8 && StageId == "phillyBlazin") rainTimeScale += .7f;
    }

    public void Press(int side)
    {
        if (Week == 9) { HoldSpaghetti(side, true); return; }
        if (VanillaCharacterTiming.IsManualSide(side)) actors[side].holdTimer = 0;
    }

    public void Hold(int side)
    {
        if (Week == 9) { HoldSpaghetti(side, false); return; }
        if (!VanillaCharacterTiming.IsManualSide(side) && VanillaCharacterTiming.IsSinging(actors[side].current.Animation))
            actors[side].holdTimer = 0;
    }

    public void Combo(int count, bool dropped)
    {
        if (Week == 9) return;
        if (chart["characters"]["girlfriend"] == null) return;
        if (dropped) { if (count >= 70) actors[2].Play("drop70", true); }
        else actors[2].Play("combo" + count, true);
    }

    public void PlayAnimation(string target, string animation)
    {
        int index = target == "gf" || target == "girlfriend" ? 2 : target == "bf" || target == "boyfriend" ? 0 : 1;
        actors[index].Play(animation, true);
    }

    public void DriveCar(float delta)
    {
        if (Week != 4 || CarDriving) return;
        CarDriving = true;
        CarCount++;
        carAge = 0;
        carVelocity = UnityEngine.Random.Range(170, 221) / Mathf.Max(delta, .001f) * 3 / 100;
        AudioClip clip = carClips[UnityEngine.Random.Range(0, 2)];
        if (clip != null) sound.PlayOneShot(clip, .7f);
    }

    private void ResetCar()
    {
        CarDriving = false;
        carAge = carVelocity = 0;
        props["fastCar"].Position = new Vector3(-126, -UnityEngine.Random.Range(350, 381) / 100f, 0);
    }

    public void ShootStar(int beat)
    {
        if (!props.TryGetValue("shootingStar", out var star)) return;
        star.Position = new Vector3(UnityEngine.Random.Range(50, 901) / 100f, -UnityEngine.Random.Range(-10, 21) / 100f, 0);
        star.FlipX = UnityEngine.Random.value < .5f;
        star.Play("shooting star");
        starBeat = beat;
        starOffset = UnityEngine.Random.Range(4, 9);
        ShootingStarCount++;
    }

    private void DanceProp(string name, int beat)
    {
        var prop = props[name];
        if (Week == 7 && name == "sniper" && prop.Animation == "sip" && !prop.Finished) return;
        string suffix = name == "freaks" && (string)chart["song"] == "roses" ? "-scared" : "";
        prop.Play(prop.Has("danceLeft" + suffix) ? (beat % 2 == 0 ? "danceLeft" : "danceRight") + suffix : "idle");
    }

    private void Beat(int beat)
    {
        foreach (JToken prop in stageData["props"])
        {
            float every = (float?)prop["danceEvery"] ?? 0;
            if (every > 0 && Mathf.Abs(beat % every) < .001f) DanceProp((string)prop["name"], beat);
        }
        if (Week == 8) WeekendBeat(beat);
        if (Week == 4 && UnityEngine.Random.value < .1f) DriveCar(Time.deltaTime);
        if (StageId == "limoRideErect" && beat > starBeat + starOffset && UnityEngine.Random.value < .1f) ShootStar(beat);
        if (StageId == "tankmanBattlefieldErect" && UnityEngine.Random.value < .02f) props["sniper"].Play("sip");
    }

    public void BeginDeath()
    {
        string folder = Path.Combine(root, "characters", actors[0].id);
        death = Graphic(Week == 8 ? WeekendDeathPath() : Directory.Exists(Path.Combine(folder, "death")) ? Path.Combine(folder, "death") : folder, "Player Death", 0);
        death.gameObject.layer = song.deadBoyfriend.layer;
        death.transform.localScale = actors[0].graphic.transform.localScale;
        death.Position = actors[0].graphic.Position;
        death.GlobalOffset = actors[0].graphic.GlobalOffset;
        death.Play(Week == 8 && explosionDeath ? "firstDeath-explosion" : "firstDeath");
        foreach (SpriteRenderer renderer in song.deadBoyfriend.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
        song.deadBoyfriendAnimator.enabled = false;
        song.deadCamera.orthographic = true;
        float scale = Week == 8 ? Mathf.Abs(actors[0].graphic.transform.localScale.x) : (float?)actors[0].data["scale"] ?? 1;
        Vector2 size = Week == 8 ? actors[0].graphic.Size : death.Size;
        deathTarget = death.Position + new Vector3(size.x * scale / 200, -size.y * scale / 200, -10)
            + Point(actors[0].data["death"]?["cameraOffsets"]);
        LeanTween.cancel(song.deadCamera.gameObject);
        sound.Stop();
        if (Week == 8) BeginWeekendDeath();
        if (companion != null)
        {
            companion.Advance(0, song.mainCamera.transform.position, clock);
            deathKnife = companion.DeathKnife();
        }
        if (Week == 7) StartCoroutine(LoadDeathQuote());
    }

    public void PlayDeath(string animation)
    {
        if (death != null) death.Play(Week == 8 && explosionDeath ? animation + "-explosion" : animation);
        if (Week == 7) Week7Death(animation);
        if (Week == 8) WeekendDeathAnimation(animation);
    }

    private void LateUpdate()
    {
        if (song == null) return;
        if (eggnogOutroActive) return;
        if (song.isDead)
        {
            if (death != null)
            {
                float amount = 1 - Mathf.Pow(.98f, Time.deltaTime * 60);
                song.deadCamera.transform.position = Vector3.Lerp(song.deadCamera.transform.position, deathTarget, amount);
                float deathZoom = Week == 8 || Week == 9 ? (float?)actors[0].data["death"]?["cameraZoom"] ?? 1 : 1;
                song.deadCamera.orthographicSize = Mathf.Lerp(song.deadCamera.orthographicSize, 3.6f / (CameraZoom * deathZoom), amount);
                death.Advance(Time.deltaTime, song.deadCamera.transform.position, clock);
                if (Week == 7) AdvanceDeathQuote(Time.deltaTime);
                if (deathKnife != null)
                {
                    deathKnife.Advance(Time.deltaTime, song.deadCamera.transform.position, clock);
                    if (deathKnife.Finished) deathKnife.gameObject.SetActive(false);
                }
            }
            return;
        }
        bool paused = song.songStarted && !song.musicSources[0].isPlaying && song.vanillaPlayback.Presentation?.Busy != true || Pause.instance != null && Pause.instance.pauseScreen.activeSelf;
        if (paused) { sound.Pause(); return; }
        sound.UnPause();
        float delta = Time.deltaTime;
        clock += delta;
        if (Week == 7) AdvanceWeek7(delta, song.SongPosition - Pause.GlobalOffset);
        if (Week == 8) AdvanceWeekend1(delta);
        if (Week == 9)
        {
            AdvanceSpaghetti(delta);
            Render(delta);
            return;
        }
        if (CarDriving)
        {
            props["fastCar"].Position += Vector3.right * (carVelocity * delta);
            carAge += delta;
            if (carAge >= 2) ResetCar();
        }
        for (int index = 0; index < actors.Length; index++)
        {
            Actor actor = actors[index];
            if (actor.current.Finished)
            {
                string hold = actor.current.Animation + "-hold";
                if (actor.current.Has(hold)) actor.Play(hold);
                else if (actor.current.Has(actor.current.Animation + "-loop")) actor.Play(actor.current.Animation + "-loop");
                else if (actor.id == "gf-car" && actor.current.Has("idle-hold")) actor.Play("idle-hold");
            }
            actor.UpdateSinging(delta, song.stepCrochet, VanillaCharacterTiming.IsHoldingInput(index));
        }
        VanillaCharacterTiming.Advance(song, ref lastDanceStep, step =>
        {
            if (Week == 7 && step % 4 == 0) Beat(step / 4);
            foreach (Actor actor in actors.Where(actor => actor.id != "pico-speaker" && !actor.id.EndsWith("-blazin") && (actor != actors[2] || (companion == null ? actor.id != "nene" || neneState == 0 : !companion.BlocksDance))))
                if (actor.current.gameObject.activeSelf && VanillaCharacterTiming.IsDanceStep(actor.data, step)) actor.Dance();
            if (step % 4 == 0) companion?.Beat();
        });
        if (song.songStarted && Week != 7)
        {
            int beat = Mathf.FloorToInt(song.vanillaPlayback.BeatAt(song.SongPosition));
            while (lastBeat < beat) Beat(++lastBeat);
        }
        trailAge += delta;
        if (trails.Count > 0 && trailAge >= .4f)
        {
            trailAge = 0;
            trailAnimations.Enqueue((actors[1].current.Animation, actors[1].current.Frame));
            while (trailAnimations.Count > 4) trailAnimations.Dequeue();
            var history = trailAnimations.Reverse().ToArray();
            for (int index = 0; index < history.Length; index++)
            {
                trails[index].gameObject.SetActive(true);
                trails[index].Play(history[index].animation);
                trails[index].FrozenFrame = history[index].frame;
            }
        }
        Render(delta);
    }

    private void Render(float delta)
    {
        Vector3 camera = song.mainCamera.transform.position;
        Vector3 scrollOrigin = camera - new Vector3(6.4f,-3.6f,camera.z);
        foreach (var solid in solids)
            solid.renderer.transform.localPosition = solid.position + new Vector3(scrollOrigin.x*(1-solid.scroll.x),scrollOrigin.y*(1-solid.scroll.y));
        foreach (var prop in props.Values) prop.Advance(delta, camera, clock);
        foreach (Actor actor in actors) actor.current.Advance(delta, camera, clock);
        companion?.Advance(delta, camera, clock);
        foreach (var trail in trails) trail.Advance(0, camera, clock);
        if (Week == 7) RenderWeek7(delta, camera);
        if (Week == 8) RenderWeekend1(delta, camera);
        if (Week == 9) RenderSpaghetti(delta, camera);
        for (int i = 0; i < mist.Count; i++)
        {
            int layer = i / 5;
            var graphic = mist[i];
            float width = graphic.Size.x * graphic.transform.localScale.x;
            graphic.Position = new Vector3((-650 + (clock * MistSpeeds[layer] % width) + (i % 5 - 2) * width) / 100,
                -(MistCenters[layer] + Mathf.Sin(clock * MistFrequencies[layer]) * MistWaves[layer]) / 100, 0);
            graphic.Advance(delta, camera, clock);
        }
    }

    private void PrepareEggnogGraphics()
    {
        if (outroSanta != null) return;
        outroSanta = Graphic(Path.Combine(root,"cutscene/santa_speaks_assets"),"Santa Outro",209);
        outroParents = Graphic(Path.Combine(root,"cutscene/parents_shoot_assets"),"Parents Outro",208);
        outroSanta.Position = new Vector3(-13,-1,0);
        outroParents.Position = new Vector3(-6.02f,.035f,0);
        foreach (var graphic in new[] { outroSanta, outroParents })
        {
            graphic.gameObject.SetActive(false);
            graphic.ColorAdjustment = new Vector4(5,20,0,0);
            graphic.WarmFrames();
            graphic.Advance(0, song.mainCamera.transform.position, clock);
        }
    }

    public IEnumerator EggnogOutro()
    {
        eggnogOutroActive = true;
        PrepareEggnogGraphics();
        var santa = outroSanta;
        var parents = outroParents;
        AudioClip emotion = null;
        AudioClip shot = null;
        foreach (string name in new[] { "santa_emotion", "santa_shot_n_falls" })
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(root,"audio",name+".ogg")).AbsoluteUri,AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
                if (name == "santa_emotion") emotion = DownloadHandlerAudioClip.GetContent(request);
                else shot = DownloadHandlerAudioClip.GetContent(request);
            }
        props["santa"].gameObject.SetActive(false);
        actors[1].hidden = true;
        actors[1].current.gameObject.SetActive(false);
        santa.Play("cutscene");
        parents.Play("cutscene");
        santa.gameObject.SetActive(true);
        parents.gameObject.SetActive(true);
        Vector3 from = song.mainCamera.transform.position;
        float fromZoom = song.mainCamera.orthographicSize;
        bool fired = false;
        float skipAt = -1;
        float skipFadeAt = -1;
        var skipText = song.vanillaPlayback.Presentation.CreateSkipText();
        var fade = song.vanillaPlayback.Presentation.CreateFade();
        sound.clip = emotion;
        sound.Play();
        double started = AudioSettings.dspTime;
        float previousTime = 0;
        while (true)
        {
            float time = (float)(AudioSettings.dspTime - started);
            float delta = time - previousTime;
            previousTime = time;
            if (time >= 16 || skipFadeAt >= 0 && time - skipFadeAt >= .5f) break;
            bool advance = song.vanillaPlayback.Presentation.TakeAdvanceInput();
            if (advance && skipFadeAt < 0)
            {
                if (skipAt < 0) skipAt = time;
                else if (time - skipAt >= .5f)
                {
                    skipFadeAt = time;
                    sound.Stop();
                }
            }
            float skipFade = skipFadeAt < 0 ? 0 : Mathf.Clamp01((time-skipFadeAt)/.5f);
            if (skipAt >= 0) skipText.color = new Color(1,1,1,Mathf.Clamp01((time-skipAt)/.5f)*(1-skipFade));
            fade.color = new Color(0,0,0,Mathf.Max(Mathf.Clamp01(time-15),skipFade));
            if (!fired && time >= 11.375f && skipFadeAt < 0) { sound.PlayOneShot(shot); fired = true; }
            Vector3 target = new Vector3(-1,-4,-10);
            if (time < 2.8f) song.mainCamera.transform.position = Vector3.Lerp(from,target,1-Mathf.Pow(2,-10*time/2.8f));
            else if (time < 12.83f)
            {
                float t = Mathf.Clamp01((time-2.8f)/9);
                float ease = t < .5f ? 8*t*t*t*t : 1-Mathf.Pow(-2*t+2,4)/2;
                song.mainCamera.transform.position = Vector3.Lerp(target,new Vector3(-2.5f,-4,-10),ease);
            }
            else song.mainCamera.transform.position = Vector3.Lerp(new Vector3(-2.5f,-4,-10),new Vector3(-2.4f,-4.8f,-10),1-Mathf.Pow(2,-10*(time-12.83f)/5));
            if (time >= 12.83f && time < 13.03f)
                song.mainCamera.transform.position += new Vector3(UnityEngine.Random.Range(-.064f,.064f),UnityEngine.Random.Range(-.036f,.036f),0);
            float zoomT = Mathf.Clamp01(time < 2.8f ? time / 2 : (time-2.8f)/9);
            float zoomEase = zoomT < .5f ? 2*zoomT*zoomT : 1-Mathf.Pow(-2*zoomT+2,2)/2;
            float zoom = time < 2.8f ? Mathf.Lerp(3.6f/fromZoom,.73f,zoomEase) : Mathf.Lerp(.73f,.79f,zoomEase);
            song.mainCamera.orthographicSize = 3.6f/zoom;
            clock += delta;
            Render(delta);
            santa.Advance(delta,song.mainCamera.transform.position,clock);
            parents.Advance(delta,song.mainCamera.transform.position,clock);
            yield return null;
        }
        sound.Stop();
        sound.clip = null;
        fade.color = Color.black;
        Destroy(skipText.gameObject);
        Destroy(emotion);
        Destroy(shot);
        Destroy(santa.gameObject);
        Destroy(parents.gameObject);
    }

    private static Vector2 Scale(JToken value) => value == null ? Vector2.one : value.Type == JTokenType.Array
        ? new Vector2((float)value[0], (float)value[1]) : Vector2.one * (float)value;
    private static Vector3 Point(JToken value) => value == null ? Vector3.zero : new Vector3((float)value[0] / 100, -(float)value[1] / 100, 0);

    private void OnDestroy()
    {
        foreach (var clip in spaghettiSounds.Values) if (clip != null) Destroy(clip);
        foreach (AudioClip clip in carClips) if (clip != null) Destroy(clip);
        if (deathQuote != null) Destroy(deathQuote);
        foreach (var clip in weekendSounds.Values) if (clip != null) Destroy(clip);
        if (rain != null) Destroy(rain);
    }
}
