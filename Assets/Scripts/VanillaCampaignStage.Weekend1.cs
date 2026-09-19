using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public sealed partial class VanillaCampaignStage
{
    private static readonly float[] WeekendMistY = { 660, 500, 540, 230, 170, -80 };
    private static readonly float[] WeekendMistFrequency = { .35f, .3f, .4f, .3f, .35f, .08f };
    private static readonly float[] WeekendMistAmplitude = { 70, 80, 60, 70, 50, 100 };
    private static readonly float[] WeekendMistSpeed = { 172, 150, -80, -50, 40, 20 };
    public FunkinRules.Judgement WeekendJudgement { get; set; }
    public int CansShot { get; private set; }
    public int CansMissed { get; private set; }
    public int CombatNotes { get; private set; }
    public bool GunCocked => gunUntil > clock;
    public float RainIntensity { get; private set; }
    public bool LightsRed => lightsRed;
    private readonly List<VanillaWeek2Graphic> weekendGraphics = new List<VanillaWeek2Graphic>();
    private readonly List<VanillaWeek2Graphic> cans = new List<VanillaWeek2Graphic>();
    private readonly List<VanillaWeek2Graphic> particles = new List<VanillaWeek2Graphic>();
    private readonly Dictionary<string, List<VanillaWeek2Graphic>> effectPools = new Dictionary<string, List<VanillaWeek2Graphic>>();
    private readonly Dictionary<string, AudioClip> weekendSounds = new Dictionary<string, AudioClip>();
    private readonly VanillaWeek2Graphic[] skyTiles = new VanillaWeek2Graphic[4];
    private readonly VanillaWeek2Graphic[] vizBars = new VanillaWeek2Graphic[7];
    private readonly TrafficCar[] trafficCars = { new TrafficCar(), new TrafficCar() };
    private readonly bool[] punchAlternate = new bool[2];
    private readonly bool[] cantUppercut = new bool[2];
    private readonly VanillaABotAnalyzer weekendAnalyzer = new VanillaABotAnalyzer();
    private readonly float[] visualizerLevels = new float[7];
    private readonly List<(VanillaWeek2Graphic graphic, int layer, int tile)> weekendMist = new List<(VanillaWeek2Graphic, int, int)>();
    private VanillaWeek2Graphic abot;
    private VanillaWeek2Graphic pupils;
    private SpriteRenderer eyeWhites;
    private VanillaWeekendRain rain;
    private float rainTimeScale = 1;
    private float rainClock;
    private float gunUntil;
    private float darkenAge = 10;
    private float lightningTimer = 3;
    private float lightningAge = 10;
    private int neneState;
    private int blinkCountdown = 3;
    private int lastPupil = -1;
    private int pupilDirection;
    private float pupilAge;
    private int lightBeat;
    private int lightInterval = 8;
    private bool lightsRed;
    private bool explosionDeath;
    private float shakeAge;
    private float shakeStrength;
    private VanillaWeek2Graphic deathKnife;
    private VanillaWeek2Graphic gunGhost;
    private Vector3 gunGhostOrigin;
    private float ghostAge = 1;
    private float casingDelay = -1;
    private float flickerAge = 2;
    private bool flickerPending;
    private readonly List<Casing> casings = new List<Casing>();

    private sealed class Casing
    {
        public VanillaWeek2Graphic graphic;
        public float age;
        public float velocity;
        public float drag;
        public float angularVelocity = 100;
        public float angularDrag;
        public bool rolling;
    }

    private sealed class TrafficCar
    {
        public bool active;
        public bool waiting;
        public int path;
        public float age;
        public float duration;
        public float delay;
        public Vector2 offset;
    }

    private VanillaWeek2Graphic WeekendGraphic(string name, int order)
    {
        var graphic = Graphic(Path.Combine(root, "effects", name), name, order);
        weekendGraphics.Add(graphic);
        return graphic;
    }

    private void LoadWeekend1()
    {
        bool blazin = StageId == "phillyBlazin";
        int gfOrder = (int)stageData["characters"]["gf"]["zIndex"];
        VanillaWeek2Graphic stereo = null;
        if (actors[2].id.StartsWith("nene"))
        {
            Vector3 abotPosition = actors[2].graphic.Position + actors[2].graphic.GlobalOffset + new Vector3(-.95f, -3.84f);
            abot = WeekendGraphic("abotSystem", gfOrder - 10);
            abot.Position = abotPosition;
            stereo = WeekendGraphic("stereoBG", gfOrder - 18);
            stereo.Position = abotPosition + new Vector3(1.5f, -.3f);
            pupils = WeekendGraphic("systemEyes", gfOrder - 15);
            pupils.Position = abotPosition + new Vector3(.5f, -2.38f);
            var eyes = new GameObject("A-Bot Eye Whites");
            eyes.transform.SetParent(transform, false);
            eyeWhites = eyes.AddComponent<SpriteRenderer>();
            eyeWhites.sprite = FunkinHudAssets.Solid;
            eyeWhites.sharedMaterial = FunkinNoteSkin.NoteMaterial;
            eyes.transform.localScale = new Vector3(160, 60, 1);
            eyes.transform.localPosition = abotPosition + new Vector3(.4f, -2.5f);
            SortRenderer(eyeWhites, gfOrder - 20);
            float[] vizX = { 0, 59, 115, 181, 235, 287, 338 };
            float[] vizY = { 0, -8, -11.5f, -11.9f, -11.4f, -6.7f, .3f };
            for (int i = 0; i < vizBars.Length; i++)
            {
                vizBars[i] = WeekendGraphic("visualizer", gfOrder - 11);
                vizBars[i].Position = abotPosition + new Vector3((207 + vizX[i]) / 100, -(84 + vizY[i]) / 100);
                vizBars[i].Play((i + 1).ToString());
                if (vizBars[i] == null) continue;
            vizBars[i].Alpha = 0;
            }
        }
        for (int i = 0; i < skyTiles.Length; i++)
        {
            skyTiles[i] = WeekendGraphic(blazin ? "skyBlazin" : StageId == "phillyStreetsErect" ? "phillySkybox" : "sky", 10);
            skyTiles[i].Scroll = Vector2.one * (blazin ? 0 : .1f);
            skyTiles[i].transform.localScale = Vector3.one * (blazin ? 1 : .65f);
        }
        if (blazin)
        {
            props["skyAdditive"].Additive = true;
            props["foregroundMultiply"].Multiply = true;
            CameraTargets[2] += new Vector3(.5f, .9f, 0);
            CameraTargets[0] = CameraTargets[1] = CameraTargets[2];
        }
        else
        {
            foreach (string name in new[] { "phillyHighwayLights_lightmap", "phillyTraffic_lightmap" })
                if (props.TryGetValue(name, out var light))
                {
                    light.Additive = true;
                    light.Alpha = .6f;
                }
            props["phillyCars2"].FlipX = true;
        }
        if (StageId == "phillyStreetsErect")
        {
            foreach (var graphic in new[] { abot, stereo }.Concat(vizBars).Where(item => item != null)) graphic.ColorAdjustment = new Vector4(-5, -40, -20, -25);
            foreach (var actor in actors)
                foreach (var graphic in new[] { actor.graphic, actor.censor }.Concat(actor.alternates).Where(item => item != null))
                    graphic.ColorAdjustment = new Vector4(-5, -40, -20, -25);
            int[] orders = { 1000, 1000, 1001, 99, 88, 39 };
            float[] scroll = { 1.2f, 1.1f, 1.2f, .95f, .8f, .5f };
            float[] scale = { 1, 1, 1, .8f, .7f, 1.1f };
            float[] alpha = { .6f, .6f, .8f, .5f, 1, 1 };
            for (int layer = 0; layer < 6; layer++)
                for (int tile = -1; tile < 3; tile++)
                {
                    var graphic = WeekendGraphic(layer == 2 || layer == 4 ? "mistBack" : "mistMid", orders[layer]);
                    graphic.Additive = true;
                    graphic.Tint = new Color(92 / 255f, 92 / 255f, 92 / 255f, 1);
                    graphic.Scroll = Vector2.one * scroll[layer];
                    graphic.Alpha = alpha[layer];
                    graphic.transform.localScale = Vector3.one * scale[layer];
                    weekendMist.Add((graphic, layer, tile));
                }
        }
        rain = song.mainCamera.gameObject.AddComponent<VanillaWeekendRain>();
        if (!blazin)
        {
            WarmEffect("can", 689, 2);
            WarmEffect("SpraypaintExplosion", 700, 2);
            WarmEffect("spraypaintExplosionEZ", 700, 2);
            int casingsNeeded = chart["noteKinds"]?[Song.difficulty.ToLowerInvariant()]?.Count(note => (string)note["k"] == "weekend-1-cockgun") ?? 0;
            WarmEffect("casing", 1900, Mathf.Max(1, casingsNeeded));
            gunGhost = Graphic(Path.Combine(root, "characters/pico-playable/alternate1"), "Pico Gun Afterimage", (int)stageData["characters"]["bf"]["zIndex"] - 3);
            gunGhost.ApplyAnimationOffsets = false;
            gunGhost.CompositeAlpha = true;
            gunGhost.Alpha = .3f;
            gunGhost.Play("cock");
            gunGhost.SetAnimationFrame(0);
            gunGhost.Advance(0, Vector3.zero, 0);
            gunGhost.gameObject.SetActive(false);
            foreach (var actor in actors)
                foreach (var graphic in new[] { actor.graphic }.Concat(actor.alternates)) graphic.WarmFrames();
        }
        StartCoroutine(LoadWeekendSounds());
    }

    private void WarmEffect(string name, int order, int count)
    {
        var pool = new List<VanillaWeek2Graphic>();
        effectPools.Add(name, pool);
        for (int i = 0; i < count; i++)
        {
            var graphic = Graphic(Path.Combine(root, "effects", name), name, order);
            graphic.WarmFrames();
            graphic.gameObject.SetActive(false);
            pool.Add(graphic);
        }
    }

    private VanillaWeek2Graphic TakeEffect(string name, int order)
    {
        var pool = effectPools[name];
        var graphic = pool.FirstOrDefault(item => !item.gameObject.activeSelf);
        if (graphic == null)
        {
            graphic = Graphic(Path.Combine(root, "effects", name), name, order);
            pool.Add(graphic);
        }
        graphic.Angle = 0;
        graphic.gameObject.SetActive(true);
        return graphic;
    }

    private IEnumerator LoadWeekendSounds()
    {
        foreach (string path in Directory.GetFiles(Path.Combine(root, "audio"), "*.ogg", SearchOption.AllDirectories))
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                    weekendSounds[Path.GetFileNameWithoutExtension(path)] = DownloadHandlerAudioClip.GetContent(request);
            }
        }
    }

    public void WeekendSound(string name, float volume = 1)
    {
        if (weekendSounds.TryGetValue(name, out var clip)) sound.PlayOneShot(clip, volume);
    }

    private void ResetWeekend1()
    {
        weekendAnalyzer.Reset();
        CansShot = CansMissed = CombatNotes = 0;
        casingDelay = -1;
        ghostAge = 1;
        flickerAge = 2;
        flickerPending = false;
        sound.loop = false;
        sound.clip = null;
        foreach (var casing in casings) casing.graphic.gameObject.SetActive(false);
        casings.Clear();
        if (deathKnife != null) Destroy(deathKnife.gameObject);
        if (gunGhost != null) gunGhost.gameObject.SetActive(false);
        gunUntil = 0;
        darkenAge = lightningAge = 10;
        lightningTimer = 3;
        rainTimeScale = 1;
        rainClock = 0;
        neneState = 0;
        blinkCountdown = 3;
        lightBeat = 0;
        lightInterval = 8;
        lightsRed = explosionDeath = false;
        lastPupil = -1;
        pupilAge = 1;
        foreach (var graphic in cans.Concat(particles)) if (graphic != null) graphic.gameObject.SetActive(false);
        cans.Clear();
        particles.Clear();
        for (int i = 0; i < 2; i++)
        {
            punchAlternate[i] = cantUppercut[i] = false;
            trafficCars[i].active = trafficCars[i].waiting = false;
        }
        if (StageId != "phillyBlazin")
        {
            props["phillyTraffic"].Play("togreen");
            props["phillyCars"].Position = props["phillyCars2"].Position = new Vector3(12, -8.18f);
        }
        else
        {
            foreach (string name in new[] { "skyAdditive", "foregroundMultiply", "lightning" }) props[name].Alpha = 0;
            foreach (var solid in solids) if (solid.renderer.name == "additionalLighten") solid.renderer.color = Color.clear;
        }
        foreach (var actor in actors)
        {
            actor.graphic.Tint = Color.white;
            foreach (var alternate in actor.alternates) alternate.Tint = Color.white;
        }
        if (rain != null) rain.enabled = true;
    }

    public void Miss(int side, int direction, double time)
    {
        string kind = NoteKind(side, direction, time);
        if (Week == 9) { SingSpaghetti(side, direction, true, kind); return; }
        if (!WeekendNote(side, kind, true)) SingKind(side, direction, true, "");
    }

    private string NoteKind(int side, int direction, double time)
    {
        return (string)chart["noteKinds"]?[Song.difficulty.ToLowerInvariant()]?
            .FirstOrDefault(n => (int)n["d"] == direction + side * 4 && Math.Abs((double)n["t"] - time) < .02)?["k"] ?? "";
    }

    public bool CanHitWeekendNote(int side, int direction, double time)
    {
        return NoteKind(side, direction, time) != "weekend-1-firegun" || GunCocked;
    }

    private bool WeekendNote(int side, string kind, bool miss)
    {
        kind = kind ?? "";
        if (StageId == "phillyBlazin")
        {
            if (!kind.StartsWith("weekend-1-"))
            {
                if (miss) FightPair(song.health <= 0 ? "hitLow" : "punchHigh", song.health <= 0 ? "punchLow" : UnityEngine.Random.value < .5f ? "block" : "dodge");
                return true;
            }
            Combat(kind.Substring(10), miss);
            return true;
        }
        if (!kind.StartsWith("weekend-1-")) return false;
        actors[side].holdTimer = 0;
        switch (kind.Substring(10))
        {
            case "lightcan": actors[1].Play("lightCan", true); LookWeekend(1); break;
            case "kickcan":
                actors[1].Play("kickCan", true);
                SpawnCan(false);
                break;
            case "kneecan": actors[1].Play("kneeCan", true); break;
            case "cockgun":
                LookWeekend(0);
                if (!miss)
                {
                    gunUntil = clock + 1;
                    actors[0].Play("cock", true);
                    WeekendSound("Gun_Prep");
                    casingDelay = 3f / 24;
                    gunGhost.gameObject.SetActive(true);
                    gunGhost.Play("cock");
                    gunGhost.SetAnimationFrame(0);
                    gunGhostOrigin = actors[0].graphic.Position + new Vector3(-1, 2);
                    gunGhost.Position = gunGhostOrigin;
                    gunGhost.GlobalOffset = Vector3.zero;
                    gunGhost.ApplyAnimationOffsets = false;
                    gunGhost.CompositeAlpha = true;
                    gunGhost.FlipX = actors[0].current.FlipX;
                    gunGhost.transform.localScale = Vector3.one;
                    ghostAge = 0;
                }
                else song.health += 8;
                break;
            case "firegun":
                if (miss || !GunCocked)
                {
                    gunUntil = 0;
                    CansMissed++;
                    if (miss) song.health += 8;
                    song.health -= 200f * .25f;
                    explosionDeath = song.health <= 0;
                    actors[0].Play("shootMISS", true);
                    flickerPending = !explosionDeath;
                    var can = cans.FirstOrDefault(item => item.Animation == "Can Start");
                    if (can != null) can.Play("Hit Pico");
                    WeekendSound("Pico_Bonk");
                    if (explosionDeath) song.deadNoise = Resources.Load<AudioClip>("FunkinHud/Pico/loss-pico-explode");
                }
                else
                {
                    actors[0].Play("shoot", true);
                    ShootCan();
                    WeekendSound("shot" + UnityEngine.Random.Range(1, 5));
                }
                break;
        }
        return true;
    }

    public VanillaWeek2Graphic CreateIntroCan()
    {
        var can = Graphic(Path.Combine(root, "effects/cutsceneCan"), "Intro Can", 689);
        can.Position = props["spraycanPile"].Position + new Vector3(.3f, 3.2f);
        return can;
    }

    public void SpawnCan(bool shot)
    {
        var can = TakeEffect("can", 689);
        can.Position = props["spraycanPile"].Position + new Vector3(-.1f, 5.5f);
        can.Play(shot ? "Can Shot" : "Can Start");
        cans.Add(can);
    }

    public void ShootCan()
    {
        var can = cans.FirstOrDefault(item => item.Animation == "Can Start");
        if (can == null) return;
        can.Play("Can Shot");
        CansShot++;
        darkenAge = -1f / 24;
        SpawnParticle("SpraypaintExplosion", can.Position + new Vector3(1.5f, 2.5f));
    }

    private void SpawnParticle(string name, Vector3 position)
    {
        var graphic = TakeEffect(name, 700);
        graphic.Position = position;
        graphic.Play("idle");
        particles.Add(graphic);
    }

    private void FightPair(string pico, string darnell)
    {
        FightAnimation(1, darnell);
        FightAnimation(0, pico);
    }

    private void FightAnimation(int side, string animation)
    {
        if (animation == null) return;
        if (animation == "punchHigh" || animation == "punchLow")
        {
            punchAlternate[side] = !punchAlternate[side];
            animation += punchAlternate[side] ? "1" : "2";
        }
        actors[side].Play(animation, true, side == 0 && animation == "hitSpin");
        actors[side].holdTimer = 0;
        int order = animation.StartsWith("punch") || animation == "uppercut" || animation == "uppercutPrep" ? 3000 : 2000;
        SortRenderer(actors[side].graphic.GetComponent<MeshRenderer>(), order);
        if (animation.StartsWith("hit") || animation == "block" || animation == "uppercut" || animation == "uppercutHit")
        {
            shakeAge = animation.StartsWith("uppercut") ? .25f : animation == "block" ? .1f : .15f;
            shakeStrength = animation.StartsWith("uppercut") ? .005f : animation == "block" ? .002f : .0025f;
        }
    }

    private void Combat(string kind, bool miss)
    {
        CombatNotes++;
        if (!miss && song.health <= 200f * .3f && (WeekendJudgement == FunkinRules.Judgement.Bad || WeekendJudgement == FunkinRules.Judgement.Shit)
            && UnityEngine.Random.value < .3f)
        {
            FightPair("punchHigh", "uppercutPrep");
            return;
        }
        bool preparedUppercut = actors[1].current.Animation == "uppercutPrep";
        bool blockedUppercut = cantUppercut[0];
        bool low = kind.Contains("low");
        string punch = low ? "punchLow" : "punchHigh";
        string hit = kind.EndsWith("spin") ? "hitSpin" : low ? "hitLow" : "hitHigh";
        string pico = "idle", darnell = "idle";
        if (kind.StartsWith("punch"))
        {
            string response = kind.EndsWith("blocked") ? "block" : kind.EndsWith("dodged") ? "dodge" : hit;
            pico = miss ? hit : punch;
            darnell = miss ? punch : response;
        }
        else if (kind.StartsWith("block") || kind.StartsWith("dodge") || kind.StartsWith("hit"))
        {
            pico = miss || kind.StartsWith("hit") ? hit : kind.StartsWith("block") ? "block" : "dodge";
            darnell = punch;
        }
        else switch (kind)
        {
            case "picouppercutprep":
                pico = miss ? "punchHigh" : "uppercutPrep";
                darnell = miss ? "hitHigh" : null;
                if (miss && !blockedUppercut) cantUppercut[0] = true;
                break;
            case "picouppercut": pico = "uppercut"; darnell = miss ? "dodge" : "uppercutHit"; break;
            case "darnelluppercutprep": darnell = "uppercutPrep"; break;
            case "darnelluppercut": pico = "uppercutHit"; darnell = "uppercut"; break;
            case "fakeout": pico = miss ? "hitHigh" : "fakeout"; darnell = "cringe"; break;
            case "taunt":
                pico = actors[0].current.Animation == "fakeout" ? "taunt" : "idle";
                darnell = actors[1].current.Animation == "cringe" ? "pissed" : "idle";
                break;
            case "tauntforce": pico = "taunt"; darnell = "pissed"; break;
            case "reversefakeout": darnell = "fakeout"; break;
        }
        if (miss && preparedUppercut) darnell = "uppercut";
        else if (miss && song.health <= 0) darnell = "punchLow";
        if (miss && darnell == "uppercut") pico = "uppercutHit";
        else if (miss && song.health <= 0) pico = "hitLow";
        else if (blockedUppercut)
        {
            pico = miss ? "hitHigh" : "block";
            if (!miss) cantUppercut[0] = false;
        }
        FightPair(pico, darnell);
    }

    public void WeekendBeat(int beat)
    {
        if (abot != null) abot.Play(abot.Animation);
        if (StageId != "phillyBlazin")
        {
            if (beat != lightBeat + lightInterval)
                for (int side = 0; side < 2; side++)
                    if (!trafficCars[side].active && !trafficCars[side].waiting && (side == 0 || !lightsRed) && UnityEngine.Random.value < .1f)
                        StartTraffic(side, side == 1 ? 1 : lightsRed ? 2 : 0);
            if (beat == lightBeat + lightInterval)
            {
                lightBeat = beat;
                lightsRed = !lightsRed;
                lightInterval = lightsRed ? 20 : 30;
                props["phillyTraffic"].Play(lightsRed ? "tored" : "togreen");
                if (!lightsRed && trafficCars[0].waiting) StartTraffic(0, 3);
            }
        }
        if (actors[2].id.StartsWith("nene") && neneState == 1 && (actors[2].current.Animation != "danceLeft" || actors[2].current.Finished)) actors[2].Play("danceLeft");
        if (actors[2].id.StartsWith("nene") && neneState == 3 && blinkCountdown-- == 0)
        {
            actors[2].Play("idleKnife", true);
            blinkCountdown = UnityEngine.Random.Range(3, 8);
        }
        if (actors[2].id.StartsWith("nene") && neneState == 4 && actors[2].current.Animation != "lowerKnife") actors[2].Play("lowerKnife", true);
    }

    private void SetWeekendVisualizerLevels(float[] levels)
    {
        for (int i = 0; i < vizBars.Length; i++)
        {
            int height = Mathf.FloorToInt(Mathf.Clamp01(levels[i]) * 6 + .5f);
            if (vizBars[i] == null) continue;
            vizBars[i].Alpha = height > 0 ? 1 : 0;
            vizBars[i].SetAnimationFrame(5 - Mathf.Clamp(height - 1, 0, 5));
        }
    }

    private void StartTraffic(int side, int path)
    {
        var car = trafficCars[side];
        var graphic = props[side == 0 ? "phillyCars" : "phillyCars2"];
        int variant = UnityEngine.Random.Range(1, 5);
        if (path != 3) graphic.Play("car" + variant);
        car.active = true;
        car.waiting = false;
        car.path = path;
        car.age = 0;
        car.delay = path == 3 ? UnityEngine.Random.Range(.2f, 1.2f) : 0;
        car.duration = path == 3 ? UnityEngine.Random.Range(1.8f, 3) : variant == 1 ? UnityEngine.Random.Range(1f, 1.7f)
            : variant == 2 ? path == 2 ? UnityEngine.Random.Range(.9f, 1.5f) : UnityEngine.Random.Range(.6f, 1.2f) : UnityEngine.Random.Range(1.5f, 2.5f);
        if (path != 3) car.offset = variant == 1 ? Vector2.zero : variant == 2 ? new Vector2(20, -15) : variant == 3 ? new Vector2(30, 50) : new Vector2(10, 60);
    }

    private void AdvanceTraffic(float delta)
    {
        for (int side = 0; side < 2; side++)
        {
            var car = trafficCars[side];
            if (!car.active) continue;
            car.age += delta;
            float t = Mathf.Clamp01((car.age - car.delay) / car.duration);
            float eased = car.path == 2 ? 1 - Mathf.Pow(1 - t, 3) : car.path == 3 ? 1 - Mathf.Cos(t * Mathf.PI / 2) : t;
            Vector2 a, b, c;
            float from, to;
            switch (car.path)
            {
                case 1: a = new Vector2(3102, 1187); b = new Vector2(2400, 950); c = new Vector2(1570, 1039); from = 18; to = -8; break;
                case 2: a = new Vector2(1480, 1029); b = new Vector2(1690, 1004); c = new Vector2(1870, 995); from = -7; to = -5; break;
                case 3: a = new Vector2(1870, 995); b = new Vector2(2400, 930); c = new Vector2(3102, 1227); from = -5; to = 18; break;
                default: a = new Vector2(1570, 1019); b = new Vector2(2400, 930); c = new Vector2(3102, 1227); from = -8; to = 18; break;
            }
            Vector2 position = (1 - eased) * (1 - eased) * a + 2 * (1 - eased) * eased * b + eased * eased * c - new Vector2(306.6f, 168.3f) - car.offset;
            var graphic = props[side == 0 ? "phillyCars" : "phillyCars2"];
            graphic.Position = new Vector3(position.x / 100, -position.y / 100);
            graphic.Angle = Mathf.Lerp(from, to, eased);
            if (t >= 1)
            {
                car.active = false;
                car.waiting = car.path == 2;
                if (car.waiting && !lightsRed) StartTraffic(0, 3);
            }
        }
    }

    public void AdvanceWeekend1(float delta)
    {
        if (casingDelay >= 0)
        {
            casingDelay -= delta;
            if (casingDelay < 0)
            {
                var graphic = TakeEffect("casing", 1900);
                graphic.Play("pop");
                graphic.Position = actors[0].graphic.Position + new Vector3(2.5f, -1);
                float factor = UnityEngine.Random.Range(1f, 2f);
                float drag = UnityEngine.Random.Range(3f, 10f) * factor;
                casings.Add(new Casing { graphic = graphic, velocity = 20 * factor, drag = drag, angularDrag = drag / (20 * factor) * 100 });
            }
        }
        foreach (var casing in casings)
        {
            casing.age += delta;
            if (!casing.rolling && casing.age >= 40f / 24)
            {
                casing.rolling = true;
                casing.graphic.Play("idle");
                casing.graphic.Position += new Vector3(3.22f, -2.96f);
                casing.graphic.Angle = 125.1f;
            }
            if (!casing.rolling) continue;
            casing.graphic.Position += Vector3.right * (casing.velocity * delta / 100);
            casing.graphic.Angle += casing.angularVelocity * delta;
            casing.velocity = Mathf.Max(0, casing.velocity - casing.drag * delta);
            casing.angularVelocity = Mathf.Max(0, casing.angularVelocity - casing.angularDrag * delta);
        }
        ghostAge += delta;
        if (gunGhost != null)
        {
            gunGhost.Alpha = .3f * Mathf.Clamp01(1 - ghostAge / .4f);
            float scale = Mathf.Lerp(1, 1.3f, ghostAge / .4f);
            gunGhost.transform.localScale = Vector3.one * scale;
            gunGhost.Position = gunGhostOrigin + new Vector3(-gunGhost.Size.x, gunGhost.Size.y) * ((scale - 1) / 200);
            gunGhost.gameObject.SetActive(ghostAge < .4f);
        }
        if (flickerPending && actors[0].current.Animation != "shootMISS") flickerPending = false;
        if (flickerPending && actors[0].current.Finished)
        {
            flickerPending = false;
            flickerAge = 0;
        }
        flickerAge += delta;
        actors[0].current.Alpha = flickerAge < 1.5f ? Mathf.FloorToInt(flickerAge * (flickerAge < 1 ? 30 : 60)) % 2 : 1;
        bool blazin = StageId == "phillyBlazin";
        float progress = song.musicSources[0].clip == null ? 0 : Mathf.Clamp01((float)(song.SongPosition / 1000) / song.musicSources[0].clip.length);
        RainIntensity = blazin ? .5f : song.vanillaPlayback.SongId == "darnell" ? progress * .1f : song.vanillaPlayback.SongId == "lit-up" ? .1f + progress * .1f : .2f + progress * .2f;
        if (StageId == "phillyStreetsErect") RainIntensity *= .1f;
        rainClock += delta * (blazin ? rainTimeScale : 1);
        rainTimeScale = Mathf.Lerp(rainTimeScale, .02f, 1 - Mathf.Exp(-delta / 1.535f));
        if (rain != null) rain.Configure(rainClock, RainIntensity);
        if (rain != null && StageId == "phillyStreetsErect") rain.Material.SetColor("_RainColor", new Color32(168, 173, 181, 255));
        if (!blazin) AdvanceTraffic(delta);
        if (blazin && !song.vanillaPlayback.Presentation.Busy)
        {
            lightningTimer -= delta;
            if (lightningTimer <= 0)
            {
                lightningTimer = UnityEngine.Random.Range(7f, 15f);
                lightningAge = 0;
                props["lightning"].Play("strike");
                props["lightning"].Position = new Vector3((UnityEngine.Random.value < .65f ? UnityEngine.Random.Range(-250f, 280f) : UnityEngine.Random.Range(780f, 900f)) / 100, 3);
                WeekendSound("Lightning" + UnityEngine.Random.Range(1, 4));
            }
        }
        lightningAge += delta;
        darkenAge += delta;
        if (blazin)
        {
            props["skyAdditive"].Alpha = .7f * Mathf.Clamp01(1 - lightningAge / 1.5f);
            props["foregroundMultiply"].Alpha = .64f * Mathf.Clamp01(1 - lightningAge / 1.5f);
            props["lightning"].Alpha = lightningAge < 1.5f ? 1 : 0;
            foreach (var solid in solids) solid.renderer.color = new Color(1, 1, 1, .3f * Mathf.Clamp01(1 - lightningAge / .3f));
            for (int i = 0; i < 3; i++)
            {
                float tint = Mathf.Lerp(96 / 255f, i == 2 ? 136 / 255f : 222 / 255f, Mathf.Clamp01(lightningAge / .3f));
                actors[i].current.Tint = new Color(tint, tint, tint, 1);
            }
            if (abot != null) abot.Tint = actors[2].current.Tint;
        }
        else
        {
            float tint = darkenAge < 0 ? 1 : darkenAge < 1f / 24 ? 17 / 255f : Mathf.Lerp(34 / 255f, 1, (darkenAge - 1f / 24) / 1.4f);
            foreach (var graphic in props.Values) graphic.Tint = new Color(tint, tint, tint, 1);
        }
        if (!blazin && actors[2].id.StartsWith("nene"))
        {
            var nene = actors[2];
            if (neneState == 0 && song.health <= 200f * .25f) neneState = 1;
            if (neneState == 1)
            {
                if (song.health > 200f * .25f) neneState = 0;
                else if (nene.current.Animation == "danceLeft" && nene.current.AnimationFrame >= 13)
                { neneState = 2; nene.Play("raiseKnife", true); }
            }
            if (neneState == 2 && nene.current.Finished) neneState = 3;
            if (neneState == 3 && song.health > 200f * .25f) neneState = 4;
            if (neneState == 4 && nene.current.Animation == "lowerKnife" && nene.current.Finished) { neneState = 0; nene.Dance(true); }
        }
        int focus = song.vanillaPlayback.FocusCharacter;
        if (focus != lastPupil && focus < 2)
        {
            lastPupil = focus;
            LookWeekend(focus);
        }
        pupilAge += delta;
        pupils?.SetAnimationFrame(pupilDirection == 0 ? 17 + Mathf.Min(13, (int)(pupilAge * 24)) : Mathf.Min(16, (int)(pupilAge * 24)));
        for (int index = 0; index < cans.Count;)
        {
            var can = cans[index];
            if (!can.Finished) { index++; continue; }
            if (can.Animation == "Can Start") { can.Play("Hit Pico"); index++; }
            else
            {
                if (can.Animation == "Hit Pico") SpawnParticle("spraypaintExplosionEZ", can.Position + new Vector3(7.5f, 1));
                cans.RemoveAt(index);
                can.gameObject.SetActive(false);
            }
        }
        for (int index = particles.Count - 1; index >= 0; index--)
        {
            var particle = particles[index];
            if (!particle.Finished) continue;
            particles.RemoveAt(index);
            particle.gameObject.SetActive(false);
        }
    }

    private void RenderWeekend1(float delta, Vector3 camera)
    {
        bool blazin = StageId == "phillyBlazin";
        foreach (var item in weekendMist)
        {
            float width = item.graphic.Size.x * item.graphic.transform.localScale.x;
            item.graphic.Position = new Vector3((-650 + item.tile * width + clock * WeekendMistSpeed[item.layer] % width) / 100,
                -(WeekendMistY[item.layer] + Mathf.Sin(clock * WeekendMistFrequency[item.layer]) * WeekendMistAmplitude[item.layer]) / 100);
        }
        for (int i = 0; i < skyTiles.Length; i++)
        {
            float width = skyTiles[i].Size.x * Mathf.Abs(skyTiles[i].transform.localScale.x);
            skyTiles[i].Position = new Vector3(((blazin ? -700 : -650) + i * width - clock * (blazin ? 35 : 22) % width) / 100, blazin ? 1.2f : 3.75f);
        }
        bool audible = song.songStarted && song.musicSources[0].isPlaying && !song.musicSources[0].mute && song.musicSources[0].volume > 0 && AudioListener.volume > 0;
        bool available = audible && weekendAnalyzer.Read(song.musicSources[0]);
        for (int i = 0; i < vizBars.Length; i++)
        {
            visualizerLevels[i] = available ? weekendAnalyzer.Levels[i] : 0;
        }
        SetWeekendVisualizerLevels(visualizerLevels);
        foreach (var graphic in weekendGraphics) graphic.Advance(delta, camera, clock);
        foreach (var graphic in cans) graphic.Advance(delta, camera, clock);
        foreach (var graphic in particles) graphic.Advance(delta, camera, clock);
        foreach (var casing in casings) casing.graphic.Advance(delta, camera, clock);
        if (gunGhost != null && gunGhost.gameObject.activeSelf) gunGhost.Advance(delta, camera, clock);
        if (shakeAge > 0)
        {
            shakeAge -= delta;
            song.mainCamera.transform.position += new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * shakeStrength * 12.8f;
        }
    }

    private string WeekendDeathPath()
    {
        if (rain != null) rain.enabled = false;
        if (actors[0].id == "pico-blazin") return Path.Combine(root, "characters/pico-blazin");
        return Path.Combine(root, "characters", actors[0].id, explosionDeath ? "explosion" : "death");
    }

    private void BeginWeekendDeath()
    {
        if (actors[0].id != "pico-playable") return;
        if (explosionDeath)
        {
            deathTarget += new Vector3(-.1f, 1.4f);
            return;
        }
        deathKnife = Graphic(Path.Combine(root, "effects/knife"), "Nene Death Knife", -5);
        deathKnife.gameObject.layer = song.deadBoyfriend.layer;
        deathKnife.Position = actors[2].graphic.Position + new Vector3(1.2f, 0);
        deathKnife.Play("throw");
    }

    private void LookWeekend(int direction)
    {
        pupilDirection = direction;
        pupilAge = 0;
    }

    private void WeekendDeathAnimation(string animation)
    {
        if (!explosionDeath) return;
        if (animation == "deathLoop" && weekendSounds.TryGetValue("singed_loop", out var clip))
        {
            sound.clip = clip;
            sound.loop = true;
            sound.Play();
        }
        else if (animation == "deathConfirm") { sound.Stop(); sound.loop = false; }
    }
}
