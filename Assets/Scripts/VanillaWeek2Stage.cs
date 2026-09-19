using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(100)]
public sealed class VanillaWeek2Stage : MonoBehaviour, IVanillaCharacterStage
{
    public Vector3[] CameraTargets { get; } = new Vector3[3];
    public float CameraZoom { get; private set; }
    public bool Erect { get; private set; }
    public int LightningCount { get; private set; }
    public int NoAnimationHits { get; private set; }
    public int LastStrikeBeat { get; private set; }
    public int StrikeOffset { get; private set; } = 8;
    public int PropCount => props.Count;
    public float LightningAge { get; private set; } = 10;
    public bool AudioReady => thunder.All(clip => clip != null);
    public string CharacterId(int index) => actors[index].id;
    public VanillaWeek2Graphic CharacterGraphic(int index) => actors[index].graphic;
    public VanillaWeek2Graphic PropGraphic(string name) => props[name];
    private readonly Dictionary<string, VanillaWeek2Graphic> props = new Dictionary<string, VanillaWeek2Graphic>();
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private readonly Actor[] actors = new Actor[3];
    private readonly AudioClip[] thunder = new AudioClip[2];
    private Song song;
    private AudioSource sound;
    private float clock;
    private int lastBeat;
    private int lastDanceStep = int.MinValue;
    private JObject chart;
    private string root;
    private VanillaWeek2Graphic death;
    private Vector3 deathTarget;
    private VanillaMixCompanion companion;
    private VanillaWeek2Graphic deathKnife;

    private sealed class Actor
    {
        public string id;
        public VanillaWeek2Graphic graphic;
        public VanillaWeek2Graphic light;
        public readonly List<VanillaWeek2Graphic> lightGraphics = new List<VanillaWeek2Graphic>();
        public JObject data;
        public float holdTimer;
        public bool alternate;
        public bool locked;

        public void Play(string name, bool protect = false, bool interrupt = false)
        {
            if (!interrupt && locked && !graphic.Finished && !(graphic.Animation == "scared" && name.StartsWith("sing"))) return;
            if (!graphic.Play(name)) return;
            var lit = lightGraphics.FirstOrDefault(item => item.Has(name));
            if (lit != null)
            {
                lit.Alpha = light == null ? 0 : light.Alpha;
                foreach (var item in lightGraphics) item.gameObject.SetActive(item == lit);
                light = lit;
                light.Play(name);
            }
            locked = protect;
        }

        public void Dance(bool force = false)
        {
            string name = VanillaCharacterTiming.DanceAnimation(graphic, force, locked, ref alternate);
            if (name != null) Play(name);
        }

        public void UpdateSinging(float delta, float stepMilliseconds, bool held)
        {
            if (!VanillaCharacterTiming.AdvanceSinging(data, graphic.Animation, ref holdTimer, delta, stepMilliseconds, held)) return;
            string end = VanillaCharacterTiming.SingEndAnimation(graphic);
            if (end != null) Play(end);
            else Dance(true);
        }
    }

    public static VanillaWeek2Stage Create(Song owner, JObject data)
    {
        var stage = new GameObject("Week 2 Stage").AddComponent<VanillaWeek2Stage>();
        stage.song = owner;
        stage.chart = data;
        stage.Load();
        return stage;
    }

    private VanillaWeek2Graphic Graphic(string directory, string objectName, int order)
    {
        var item = new GameObject(objectName).AddComponent<VanillaWeek2Graphic>();
        item.transform.SetParent(transform, false);
        item.Load(directory, order);
        return item;
    }

    private void Load()
    {
        root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week2Assets");
        string stageId = (string)chart["stage"];
        Erect = stageId == "spookyMansionErect";
        string directory = Path.Combine(root, "stages", stageId);
        JObject data = JObject.Parse(File.ReadAllText(Path.Combine(directory, "stage.json")));
        CameraZoom = (float)data["cameraZoom"];
        foreach (GameObject item in song.defaultSceneObjects) item.SetActive(false);
        foreach (JToken prop in data["props"])
        {
            string name = (string)prop["name"];
            string asset = (string)prop["assetPath"];
            if (asset.StartsWith("#"))
            {
                var solid = new GameObject(name).AddComponent<SpriteRenderer>();
                solid.transform.SetParent(transform, false);
                solid.sprite = FunkinHudAssets.Solid;
                solid.sharedMaterial = FunkinNoteSkin.NoteMaterial;
                ColorUtility.TryParseHtmlString(asset, out Color color);
                solid.color = color;
                solid.sortingOrder = (int)prop["zIndex"];
                solid.transform.localPosition = Point(prop["position"]);
                solid.transform.localScale = new Vector3((float)prop["scale"][0], (float)prop["scale"][1], 1);
                continue;
            }
            var graphic = Graphic(Path.Combine(directory, name), name, (int)prop["zIndex"]);
            graphic.Position = Point(prop["position"]);
            graphic.Scroll = new Vector2((float)prop["scroll"][0], (float)prop["scroll"][1]);
            graphic.transform.localScale = new Vector3((float)prop["scale"][0], (float)prop["scale"][1], 1);
            graphic.Play((string)prop["startingAnimation"] ?? "idle");
            graphic.Alpha = name == "bgLight" || name == "stairsLight" ? 0 : 1;
            graphic.Rain = name == "bgTrees" ? 1 : 0;
            props.Add(name, graphic);
        }
        string[] roles = { "player", "opponent", "girlfriend" };
        string[] stageRoles = { "bf", "dad", "gf" };
        for (int index = 0; index < 3; index++)
        {
            string id = (string)chart["characters"][roles[index]];
            JObject character = JObject.Parse(File.ReadAllText(Path.Combine(root, "characters", id, "character.json")));
            JToken placement = data["characters"][stageRoles[index]];
            var graphic = Graphic(Path.Combine(root, "characters", id), id, (int)placement["zIndex"]);
            graphic.FlipX = index == 0 ? !((bool?)character["flipX"] ?? false) : (bool?)character["flipX"] ?? false;
            graphic.Play((string)character["startingAnimation"] ?? "idle");
            graphic.CompositeAlpha = (bool?)character["atlasSettings"]?["useRenderTexture"] ?? false;
            bool mix = (string)chart["variation"] == "pico";
            Vector2 hitbox = mix ? new Vector2((int)graphic.HitboxSize.x, (int)graphic.HitboxSize.y) : graphic.Size;
            Vector3 corner = Point(placement["position"]) + new Vector3(-hitbox.x / 200, hitbox.y / 100, 0);
            graphic.Position = corner + Point(character["offsets"]);
            if (mix) graphic.GlobalOffset = Point(character["offsets"]);
            if (!Erect && id == "spooky") graphic.Position += Vector3.up * 0.24f;
            CameraTargets[index] = Point(placement["position"]) + new Vector3(0, hitbox.y / 200, -10)
                + Point(character["offsets"]) + Point(character["cameraOffsets"]) + Point(placement["cameraOffsets"]);
            actors[index] = new Actor { id = id, graphic = graphic, data = character };
            if (id.EndsWith("-dark"))
            {
                string normal = id == "pico-dark" ? "pico-playable" : id.Substring(0, id.Length - 5);
                var light = Graphic(Path.Combine(root, "characters", normal), normal + " lightning", (int)placement["zIndex"] - 1);
                JObject normalData = JObject.Parse(File.ReadAllText(Path.Combine(root, "characters", normal, "character.json")));
                light.Position = corner + Point(normalData["offsets"])
                    + (id == "bf-dark" ? new Vector3(-0.01f, 0.11f, 0) : id == "gf-dark" ? new Vector3(-0.455f, 0, 0) : Vector3.zero);
                light.Play((string)character["startingAnimation"] ?? "idle");
                if (mix)
                {
                    light.Position = graphic.Position;
                    light.GlobalOffset = Point(normalData["offsets"]);
                }
                light.FlipX = graphic.FlipX;
                light.Alpha = 0;
                actors[index].light = light;
                actors[index].lightGraphics.Add(light);
                foreach (string alternate in Directory.GetDirectories(Path.Combine(root, "characters", normal), "alternate*"))
                {
                    var extra = Graphic(alternate, normal + " lightning " + Path.GetFileName(alternate), (int)placement["zIndex"] - 1);
                    extra.Position = light.Position;
                    extra.GlobalOffset = light.GlobalOffset;
                    extra.FlipX = light.FlipX;
                    extra.Alpha = 0;
                    extra.gameObject.SetActive(false);
                    actors[index].lightGraphics.Add(extra);
                }
            }
        }
        foreach (GameObject character in new[] { song.boyfriendObject, song.opponentObject, song.girlfriendObject })
            character.GetComponent<SpriteRenderer>().enabled = false;
        song.boyfriendAnimator.enabled = song.opponentAnimator.enabled = song.girlfriendAnimator.enabled = false;
        sound = gameObject.AddComponent<AudioSource>();
        sound.outputAudioMixerGroup = song.oopsSource.outputAudioMixerGroup;
        StartCoroutine(LoadThunder());
        if (actors[2].id.StartsWith("nene"))
        {
            companion = new VanillaMixCompanion();
            companion.Initialize(song, actors[2].id, (int)data["characters"]["gf"]["zIndex"],
                (path, order) => Graphic(Path.Combine(root, path), Path.GetFileName(path), order),
                animation => actors[2].Play(animation, true, true), () => actors[2].graphic);
            companion.ApplyLighting(root, 2);
        }
        ResetStage();
    }

    private IEnumerator LoadThunder()
    {
        for (int index = 0; index < thunder.Length; index++)
        {
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(root, "thunder_" + (index + 1) + ".ogg")).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
                thunder[index] = DownloadHandlerAudioClip.GetContent(request);
                owned.Add(thunder[index]);
            }
        }
    }

    public void ResetStage()
    {
        if (death != null) Destroy(death.gameObject);
        if (deathKnife != null) Destroy(deathKnife.gameObject);
        companion?.Reset();
        lastBeat = -1;
        lastDanceStep = int.MinValue;
        LastStrikeBeat = 0;
        StrikeOffset = 8;
        LightningCount = 0;
        NoAnimationHits = 0;
        LightningAge = 10;
        clock = 0;
        foreach (Actor actor in actors)
        {
            actor.holdTimer = 0;
            actor.locked = false;
            actor.alternate = false;
            actor.Play((string)actor.data["startingAnimation"] ?? "idle", false, true);
        }
        if (!Erect) props["halloweenBG"].Play("idle");
        if (sound != null) sound.Stop();
        UpdateLighting();
        Render(0);
    }

    public bool SuppressAnimation(int side, int direction, double time)
    {
        JToken notes = chart["noteKinds"]?[Song.difficulty.ToLowerInvariant()];
        return notes != null && notes.Any(note => (string)note["k"] == "noanim"
            && (int)note["d"] == side * 4 + direction && Math.Abs((double)note["t"] - time) < 0.01);
    }

    public void Sing(int side, int direction, bool miss)
    {
        string name = "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction] + (miss ? "miss" : "");
        Actor actor = actors[side];
        actor.Play(actor.graphic.Has(name) ? name : name.Replace("miss", ""));
        if (!miss) actor.holdTimer = 0;
    }

    public void Hit(int side, int direction, double time)
    {
        if (SuppressAnimation(side, direction, time)) NoAnimationHits++;
        else Sing(side, direction, false);
    }

    public void Combo(int count, bool dropped)
    {
        if (dropped)
        {
            if (count >= 70) actors[2].Play("drop70", true, true);
        }
        else actors[2].Play("combo" + count, true, true);
    }

    public void Press(int side)
    {
        if (VanillaCharacterTiming.IsManualSide(side)) actors[side].holdTimer = 0;
    }

    public void Hold(int side)
    {
        if (!VanillaCharacterTiming.IsManualSide(side) && VanillaCharacterTiming.IsSinging(actors[side].graphic.Animation))
            actors[side].holdTimer = 0;
    }

    public void PlayAnimation(string target, string animation)
    {
        int index = target == "gf" || target == "girlfriend" ? 2 : target == "bf" || target == "boyfriend" ? 0 : 1;
        actors[index].Play(animation, true, index == 2);
    }

    public void BeginDeath()
    {
        death = Graphic(Path.Combine(root, actors[0].id.StartsWith("pico") ? "characters/" + actors[0].id + "/death" : "characters/bf-death"), "Player Death", 0);
        death.gameObject.layer = song.deadBoyfriend.layer;
        death.Position = actors[0].graphic.Position;
        death.GlobalOffset = actors[0].graphic.GlobalOffset;
        death.Play("firstDeath");
        deathKnife = companion?.DeathKnife();
        foreach (SpriteRenderer renderer in song.deadBoyfriend.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
        song.deadBoyfriendAnimator.enabled = false;
        song.deadCamera.orthographic = true;
        deathTarget = actors[0].graphic.Position + new Vector3(actors[0].graphic.Size.x / 200, -actors[0].graphic.Size.y / 200, -10)
            + Point(actors[0].data["death"]?["cameraOffsets"]);
        LeanTween.cancel(song.deadCamera.gameObject);
        sound.Stop();
    }

    public void PlayDeath(string animation)
    {
        if (death != null) death.Play(animation);
    }

    public void Strike(bool playSound, int beat)
    {
        if (playSound && AudioReady) sound.PlayOneShot(thunder[UnityEngine.Random.Range(0, thunder.Length)], 1);
        LastStrikeBeat = beat;
        StrikeOffset = UnityEngine.Random.Range(8, 25);
        LightningCount++;
        LightningAge = 0;
        if (!Erect) props["halloweenBG"].Play("lightning");
        if (actors[0].graphic.Animation != "cheer" && !actors[0].graphic.Animation.StartsWith("sing")) actors[0].Play("scared", true);
        actors[2].Play("scared", true, true);
        UpdateLighting();
    }

    private void UpdateLighting()
    {
        if (!Erect) return;
        float amount = LightningAge < 0.06f ? 1 : LightningAge < 0.12f ? 0 : 1 - Mathf.Clamp01((LightningAge - 0.12f) / 1.5f);
        props["bgLight"].Alpha = props["stairsLight"].Alpha = amount;
        foreach (Actor actor in actors)
        {
            actor.graphic.Alpha = 1 - amount;
            if (actor.light != null) actor.light.Alpha = amount > 0 ? 1 : 0;
        }
    }

    private void LateUpdate()
    {
        if (song == null) return;
        bool paused = song.songStarted && !song.musicSources[0].isPlaying || Pause.instance != null && Pause.instance.pauseScreen.activeSelf;
        if (song.isDead)
        {
            LightningAge = 10;
            UpdateLighting();
            Render(0);
            if (death != null)
            {
                float amount = 1 - Mathf.Pow(0.98f, Time.deltaTime * 60);
                song.deadCamera.transform.position = Vector3.Lerp(song.deadCamera.transform.position, deathTarget, amount);
                song.deadCamera.orthographicSize = Mathf.Lerp(song.deadCamera.orthographicSize, 3.6f / CameraZoom, amount);
                death.Advance(Time.deltaTime, song.deadCamera.transform.position, clock);
                if (deathKnife != null)
                {
                    deathKnife.Advance(Time.deltaTime, song.deadCamera.transform.position, clock);
                    if (deathKnife.Finished) deathKnife.gameObject.SetActive(false);
                }
            }
            return;
        }
        if (paused) { sound.Pause(); return; }
        sound.UnPause();
        float delta = Time.deltaTime;
        clock += delta;
        LightningAge += delta;
        for (int index = 0; index < actors.Length; index++)
        {
            Actor actor = actors[index];
            if (actor.graphic.Finished && actor.graphic.Has(actor.graphic.Animation + "-hold")) actor.Play(actor.graphic.Animation + "-hold");
            actor.UpdateSinging(delta, song.stepCrochet, VanillaCharacterTiming.IsHoldingInput(index));
        }
        VanillaCharacterTiming.Advance(song, ref lastDanceStep, step =>
        {
            foreach (Actor actor in actors)
                if ((actor != actors[2] || companion?.BlocksDance != true) && actor.graphic.gameObject.activeSelf && VanillaCharacterTiming.IsDanceStep(actor.data, step)) actor.Dance();
            if (step % 4 == 0) companion?.Beat();
        });
        if (song.songStarted)
        {
            int beat = Mathf.FloorToInt(song.vanillaPlayback.BeatAt(song.SongPosition));
            while (lastBeat < beat)
            {
                lastBeat++;
                if (lastBeat == 4 && song.vanillaPlayback.SongId == "spookeez") Strike(false, lastBeat);
                if (UnityEngine.Random.value < 0.1f && lastBeat > LastStrikeBeat + StrikeOffset) Strike(true, lastBeat);
            }
        }
        UpdateLighting();
        Render(delta);
    }

    private void Render(float delta)
    {
        Vector3 camera = song.mainCamera.transform.position;
        foreach (VanillaWeek2Graphic prop in props.Values) prop.Advance(delta, camera, clock);
        foreach (Actor actor in actors)
        {
            actor.graphic.Advance(delta, camera, clock);
            actor.light?.Advance(delta, camera, clock);
        }
        companion?.Advance(delta, camera, clock);
    }

    private static Vector3 Point(JToken value) => value == null ? Vector3.zero
        : new Vector3((float)value[0] / 100, -(float)value[1] / 100, 0);

    private void OnDestroy()
    {
        foreach (UnityEngine.Object item in owned) if (item != null) Destroy(item);
    }
}
