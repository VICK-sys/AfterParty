using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(100)]
public sealed class VanillaCampaignStage : MonoBehaviour, IVanillaCharacterStage
{
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

    private sealed class Actor
    {
        public string id;
        public JObject data;
        public VanillaWeek2Graphic graphic;
        public VanillaWeek2Graphic censor;
        public VanillaWeek2Graphic current;
        public float singing;
        public bool alternate;
        public bool locked;

        public void Play(string name, bool protect = false)
        {
            var target = graphic.Has(name) ? graphic : censor != null && censor.Has(name) ? censor : null;
            if (target == null) return;
            graphic.gameObject.SetActive(target == graphic);
            if (censor != null) censor.gameObject.SetActive(target == censor);
            current = target;
            current.Play(name);
            locked = protect;
        }

        public void Dance()
        {
            string name = VanillaCharacterTiming.DanceAnimation(current, singing, locked, ref alternate);
            if (name != null) Play(name);
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
        Week = StageId.StartsWith("mainStage") ? 1 : StageId.StartsWith("limo") ? 4 : StageId.StartsWith("mall") ? 5 : 6;
        root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week" + Week + "Assets");
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
            graphic.Scroll = Scale(prop["scroll"]);
            graphic.Alpha = (float?)prop["alpha"] ?? 1;
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
            float scale = ((float?)character["scale"] ?? 1) * ((float?)placement["scale"] ?? 1);
            graphic.transform.localScale = new Vector3(scale, scale, 1);
            graphic.Position = Point(placement["position"]) + new Vector3(-graphic.Size.x * scale / 200, graphic.Size.y * scale / 100, 0)
                + Point(character["offsets"]);
            graphic.GlobalOffset = Point(character["offsets"]);
            graphic.FlipX = index == 0 ? !((bool?)character["flipX"] ?? false) : (bool?)character["flipX"] ?? false;
            if (StageId == "limoRideErect") graphic.ColorAdjustment = new Vector4(-30, -20, -30, 0);
            if (StageId == "mallXmasErect") graphic.ColorAdjustment = new Vector4(5, 20, 0, 0);
            if (StageId == "mainStageErect") graphic.ColorAdjustment = index == 0 ? new Vector4(12, 0, -23, 7)
                : index == 1 ? new Vector4(-32, 0, -33, -23) : new Vector4(-9, 0, -30, -4);
            if (StageId == "schoolErect")
            {
                string mask = index == 0 ? "bfPixel_mask" : index == 1 ? "senpai_mask" : "gfPixel_mask";
                graphic.SetRim(Path.Combine(root, "effects", mask + ".png"), index == 2 ? 3 : 5, index == 2 ? .3f : .1f,
                    index == 2 ? new Vector4(-10, -25, -42, 5) : new Vector4(-10, -23, -66, 24));
            }
            CameraTargets[index] = Point(placement["position"]) + new Vector3(0, graphic.Size.y * scale / 200, -10)
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
            actor.singing = 0;
            actor.locked = actor.alternate = false;
            actor.Play((string)actor.data["startingAnimation"] ?? "idle");
        }
        if (chart["characters"]["girlfriend"] == null) actors[2].current.gameObject.SetActive(false);
        if (Week == 4) ResetCar();
        if (props.ContainsKey("freaks")) DanceProp("freaks", 0);
        trailAnimations.Clear();
        foreach (var trail in trails) { trail.Play("idle"); trail.gameObject.SetActive(false); }
        Render(0);
    }

    public void Sing(int side, int direction, bool miss)
    {
        SingKind(side, direction, miss, "");
    }

    private void SingKind(int side, int direction, bool miss, string kind)
    {
        if (kind == "noanim") return;
        Actor actor = actors[side];
        string name = "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction] + (miss ? "miss" : "")
            + (kind == "mom" ? "-alt" : kind == "censor" ? "-censor" : "");
        if (!actor.graphic.Has(name) && (actor.censor == null || !actor.censor.Has(name))) name = name.Replace("miss", "");
        actor.Play(name);
        actor.singing = ((float?)actor.data["singTime"] ?? 8) * song.stepCrochet / 1000 * (miss ? 2 : 1);
    }

    public void Hit(int side, int direction, double time)
    {
        var notes = chart["noteKinds"]?[Song.difficulty.ToLowerInvariant()];
        string kind = (string)notes?.FirstOrDefault(n => (int)n["d"] == direction + side * 4 && Math.Abs((double)n["t"] - time) < .02)?["k"];
        SingKind(side, direction, false, kind);
    }

    public void Hold(int side)
    {
        if (actors[side].current.Animation.StartsWith("sing"))
            actors[side].singing = ((float?)actors[side].data["singTime"] ?? 8) * song.stepCrochet / 1000;
    }

    public void Combo(int count, bool dropped)
    {
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
        if (Week == 4 && UnityEngine.Random.value < .1f) DriveCar(Time.deltaTime);
        if (StageId == "limoRideErect" && beat > starBeat + starOffset && UnityEngine.Random.value < .1f) ShootStar(beat);
    }

    public void BeginDeath()
    {
        death = Graphic(Path.Combine(root, "characters", actors[0].id, "death"), "Boyfriend Death", 0);
        death.gameObject.layer = song.deadBoyfriend.layer;
        death.transform.localScale = actors[0].graphic.transform.localScale;
        death.Position = actors[0].graphic.Position;
        death.GlobalOffset = actors[0].graphic.GlobalOffset;
        death.Play("firstDeath");
        foreach (SpriteRenderer renderer in song.deadBoyfriend.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
        song.deadBoyfriendAnimator.enabled = false;
        song.deadCamera.orthographic = true;
        float scale = (float?)actors[0].data["scale"] ?? 1;
        deathTarget = death.Position + new Vector3(death.Size.x * scale / 200, -death.Size.y * scale / 200, -10)
            + Point(actors[0].data["death"]?["cameraOffsets"]);
        LeanTween.cancel(song.deadCamera.gameObject);
        sound.Stop();
    }

    public void PlayDeath(string animation) { if (death != null) death.Play(animation); }

    private void LateUpdate()
    {
        if (song == null) return;
        if (song.isDead)
        {
            if (death != null)
            {
                float amount = 1 - Mathf.Pow(.98f, Time.deltaTime * 60);
                song.deadCamera.transform.position = Vector3.Lerp(song.deadCamera.transform.position, deathTarget, amount);
                song.deadCamera.orthographicSize = Mathf.Lerp(song.deadCamera.orthographicSize, 3.6f / CameraZoom, amount);
                death.Advance(Time.deltaTime, song.deadCamera.transform.position, clock);
            }
            return;
        }
        bool paused = song.songStarted && !song.musicSources[0].isPlaying || Pause.instance != null && Pause.instance.pauseScreen.activeSelf;
        if (paused) { sound.Pause(); return; }
        sound.UnPause();
        float delta = Time.deltaTime;
        clock += delta;
        if (CarDriving)
        {
            props["fastCar"].Position += Vector3.right * (carVelocity * delta);
            carAge += delta;
            if (carAge >= 2) ResetCar();
        }
        foreach (Actor actor in actors)
        {
            bool singing = actor.singing > 0;
            actor.singing -= delta;
            if (actor.current.Finished)
            {
                string hold = actor.current.Animation + "-hold";
                if (actor.current.Has(hold)) actor.Play(hold);
                else if (actor.id == "gf-car" && actor.current.Has("idle-hold")) actor.Play("idle-hold");
            }
            if (singing && actor.singing <= 0) actor.Dance();
        }
        VanillaCharacterTiming.Advance(song, ref lastDanceStep, step =>
        {
            foreach (Actor actor in actors)
                if (actor.current.gameObject.activeSelf && VanillaCharacterTiming.IsDanceStep(actor.data, step)) actor.Dance();
        });
        if (song.songStarted)
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
        foreach (var trail in trails) trail.Advance(0, camera, clock);
        float[] speeds = { 1700, 2100, 900, 700, 100 };
        float[] centers = { 100, 0, -20, -180, -450 };
        float[] waves = { 200, 100, 200, 300, 150 };
        float[] frequencies = { 1, .8f, .5f, .4f, .2f };
        for (int i = 0; i < mist.Count; i++)
        {
            int layer = i / 5;
            var graphic = mist[i];
            float width = graphic.Size.x * graphic.transform.localScale.x;
            graphic.Position = new Vector3((-650 + (clock * speeds[layer] % width) + (i % 5 - 2) * width) / 100,
                -(centers[layer] + Mathf.Sin(clock * frequencies[layer]) * waves[layer]) / 100, 0);
            graphic.Advance(delta, camera, clock);
        }
    }

    public IEnumerator EggnogOutro()
    {
        props["santa"].gameObject.SetActive(false);
        actors[1].current.gameObject.SetActive(false);
        var santa = Graphic(Path.Combine(root,"cutscene/santa_speaks_assets"),"Santa Outro",209);
        var parents = Graphic(Path.Combine(root,"cutscene/parents_shoot_assets"),"Parents Outro",208);
        santa.Position = new Vector3(-13,-1,0);
        parents.Position = new Vector3(-6.02f,.035f,0);
        santa.ColorAdjustment = parents.ColorAdjustment = new Vector4(5,20,0,0);
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
        sound.clip = emotion;
        sound.Play();
        santa.Play("cutscene");
        parents.Play("cutscene");
        Vector3 from = song.mainCamera.transform.position;
        float fromZoom = song.mainCamera.orthographicSize;
        bool fired = false;
        float skipAt = -1;
        var skipText = song.vanillaPlayback.Presentation.CreateSkipText();
        var fade = song.vanillaPlayback.Presentation.CreateFade();
        for (float time = 0; time < 16; time += Time.deltaTime)
        {
            bool advance = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape);
            if (advance)
            {
                if (skipAt >= 0 && time - skipAt >= .5f) break;
                skipAt = time;
            }
            if (skipAt >= 0) skipText.color = new Color(1,1,1,Mathf.Clamp01((time-skipAt)/.5f));
            fade.color = new Color(0,0,0,Mathf.Clamp01(time-15));
            if (!fired && time >= 11.375f) { sound.PlayOneShot(shot); fired = true; }
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
            float zoomT = Mathf.Clamp01(time / 2);
            float zoomEase = zoomT < .5f ? 2*zoomT*zoomT : 1-Mathf.Pow(-2*zoomT+2,2)/2;
            song.mainCamera.orthographicSize = time < 2.8f ? Mathf.Lerp(fromZoom,3.6f/.73f,zoomEase)
                : Mathf.Lerp(3.6f/.73f,3.6f/.79f,Mathf.Clamp01((time-2.8f)/9));
            Render(Time.deltaTime);
            santa.Advance(Time.deltaTime,song.mainCamera.transform.position,clock);
            parents.Advance(Time.deltaTime,song.mainCamera.transform.position,clock);
            yield return null;
        }
        sound.Stop();
        sound.clip = null;
        Destroy(skipText.gameObject);
        Destroy(fade.gameObject);
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
        foreach (AudioClip clip in carClips) if (clip != null) Destroy(clip);
    }
}
