using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(100)]
public sealed class VanillaWeek3Stage : MonoBehaviour, IVanillaCharacterStage
{
    public Vector3[] CameraTargets { get; } = new Vector3[3];
    public float CameraZoom { get; private set; }
    public bool Erect { get; private set; }
    public int PropCount => props.Count;
    public bool TrainMoving { get; private set; }
    public bool TrainStarted { get; private set; }
    public bool TrainFinishing { get; private set; }
    public int TrainCars { get; private set; } = 8;
    public int TrainCooldown { get; private set; }
    public int TrainCount { get; private set; }
    public bool AudioReady => trainSound.clip != null;
    public float LightFade => props["lights"].BuildingFade;
    public string CharacterId(int index) => actors[index].id;
    public VanillaWeek2Graphic CharacterGraphic(int index) => actors[index].graphic;
    public VanillaWeek2Graphic PropGraphic(string name) => props[name];
    private readonly Dictionary<string, VanillaWeek2Graphic> props = new Dictionary<string, VanillaWeek2Graphic>();
    private readonly Actor[] actors = new Actor[3];
    private readonly string[] normalColors = { "#31A2FD", "#31FD8C", "#FB33F5", "#FBA633", "#FD4531" };
    private readonly string[] erectColors = { "#B66F43", "#329A6D", "#932C28", "#2663AC", "#502D64" };
    private Song song;
    private JObject chart;
    private string root;
    private AudioSource trainSound;
    private AudioClip trainClip;
    private float trainFrameTiming;
    private float clock;
    private int lastBeat;
    private int lastDanceStep = int.MinValue;
    private VanillaWeek2Graphic death;
    private Vector3 deathTarget;

    private sealed class Actor
    {
        public string id;
        public JObject data;
        public VanillaWeek2Graphic graphic;
        public float singing;
        public bool alternate;
        public bool locked;

        public void Play(string name, bool protect = false)
        {
            if (!graphic.Play(name)) return;
            locked = protect;
        }

        public void Dance()
        {
            string name = VanillaCharacterTiming.DanceAnimation(graphic, singing, locked, ref alternate);
            if (name != null) Play(name);
        }
    }

    public static VanillaWeek3Stage Create(Song owner, JObject data)
    {
        var stage = new GameObject("Week 3 Stage").AddComponent<VanillaWeek3Stage>();
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
        root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week3Assets");
        string stageId = (string)chart["stage"];
        Erect = stageId == "phillyTrainErect";
        string directory = Path.Combine(root, "stages", stageId);
        JObject data = JObject.Parse(File.ReadAllText(Path.Combine(directory, "stage.json")));
        CameraZoom = (float)data["cameraZoom"];
        foreach (GameObject item in song.defaultSceneObjects) item.SetActive(false);
        foreach (JToken prop in data["props"])
        {
            string name = (string)prop["name"];
            var graphic = Graphic(Path.Combine(directory, name), name, (int)prop["zIndex"]);
            graphic.Position = Point(prop["position"]);
            graphic.Scroll = new Vector2((float)prop["scroll"][0], (float)prop["scroll"][1]);
            graphic.transform.localScale = new Vector3((float)prop["scale"][0], (float)prop["scale"][1], 1);
            graphic.PhillyColor = Erect && name == "train";
            props.Add(name, graphic);
        }
        string[] roles = { "player", "opponent", "girlfriend" };
        string[] stageRoles = { "bf", "dad", "gf" };
        for (int index = 0; index < actors.Length; index++)
        {
            string id = (string)chart["characters"][roles[index]];
            JObject character = JObject.Parse(File.ReadAllText(Path.Combine(root, "characters", id, "character.json")));
            JToken placement = data["characters"][stageRoles[index]];
            var graphic = Graphic(Path.Combine(root, "characters", id), id, (int)placement["zIndex"]);
            graphic.Position = Point(placement["position"]) + new Vector3(-graphic.Size.x / 200, graphic.Size.y / 100, 0)
                + Point(character["offsets"]);
            graphic.FlipX = index == 0 ? !((bool?)character["flipX"] ?? false) : (bool?)character["flipX"] ?? false;
            graphic.PhillyColor = Erect;
            CameraTargets[index] = Point(placement["position"]) + new Vector3(0, graphic.Size.y / 200, -10)
                + Point(character["offsets"]) + Point(character["cameraOffsets"]) + Point(placement["cameraOffsets"]);
            actors[index] = new Actor { id = id, data = character, graphic = graphic };
        }
        foreach (GameObject character in new[] { song.boyfriendObject, song.opponentObject, song.girlfriendObject })
            character.GetComponent<SpriteRenderer>().enabled = false;
        song.boyfriendAnimator.enabled = song.opponentAnimator.enabled = song.girlfriendAnimator.enabled = false;
        trainSound = gameObject.AddComponent<AudioSource>();
        trainSound.outputAudioMixerGroup = song.oopsSource.outputAudioMixerGroup;
        StartCoroutine(LoadTrain());
        ResetStage();
    }

    private IEnumerator LoadTrain()
    {
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(root, "train_passes.ogg")).AbsoluteUri, AudioType.OGGVORBIS))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
            trainClip = DownloadHandlerAudioClip.GetContent(request);
            trainSound.clip = trainClip;
        }
    }

    public void ResetStage()
    {
        if (death != null) Destroy(death.gameObject);
        lastBeat = -1;
        lastDanceStep = int.MinValue;
        TrainMoving = TrainStarted = TrainFinishing = false;
        TrainCars = 8;
        TrainCooldown = TrainCount = 0;
        trainFrameTiming = clock = 0;
        props["train"].Position = new Vector3(20, -3.6f, 0);
        props["lights"].BuildingFade = 1;
        props["lights"].Tint = Color.white;
        trainSound.Stop();
        foreach (Actor actor in actors)
        {
            actor.singing = 0;
            actor.locked = actor.alternate = false;
            actor.graphic.Play((string)actor.data["startingAnimation"] ?? "idle");
        }
        Render(0);
    }

    public void Sing(int side, int direction, bool miss)
    {
        string name = "sing" + new[] { "LEFT", "DOWN", "UP", "RIGHT" }[direction] + (miss ? "miss" : "");
        Actor actor = actors[side];
        actor.Play(actor.graphic.Has(name) ? name : name.Replace("miss", ""));
        actor.singing = ((float?)actor.data["singTime"] ?? 8) * song.stepCrochet / 1000 * (miss ? 2 : 1);
    }

    public void Hit(int side, int direction, double time) => Sing(side, direction, false);

    public void Hold(int side)
    {
        if (actors[side].graphic.Animation.StartsWith("sing"))
            actors[side].singing = ((float?)actors[side].data["singTime"] ?? 8) * song.stepCrochet / 1000;
    }

    public void Combo(int count, bool dropped)
    {
        if (dropped)
        {
            if (count >= 70) actors[2].Play("drop70", true);
        }
        else actors[2].Play("combo" + count, true);
    }

    public void PlayAnimation(string target, string animation)
    {
        int index = target == "gf" || target == "girlfriend" ? 2 : target == "bf" || target == "boyfriend" ? 0 : 1;
        actors[index].Play(animation, true);
    }

    public void StartTrain()
    {
        if (TrainMoving || !AudioReady) return;
        TrainMoving = true;
        TrainCount++;
        trainSound.Play();
    }

    public void AdvanceTrain(float soundMilliseconds)
    {
        if (!TrainMoving) return;
        if (soundMilliseconds >= 4700)
        {
            TrainStarted = true;
            if (actors[2].graphic.Animation != "hairBlow") actors[2].Play("hairBlow", true);
        }
        if (!TrainStarted) return;
        var train = props["train"];
        train.Position -= Vector3.right * 4;
        if (train.Position.x < -20 && !TrainFinishing)
        {
            train.Position = new Vector3(-11.5f, train.Position.y, 0);
            TrainCars--;
            if (TrainCars <= 0) TrainFinishing = true;
        }
        if (train.Position.x < -40 && TrainFinishing)
        {
            actors[2].Play("hairFall", true);
            train.Position = new Vector3(14.8f, train.Position.y, 0);
            TrainMoving = TrainStarted = TrainFinishing = false;
            TrainCars = 8;
        }
    }

    private void Beat(int beat)
    {
        if (!TrainMoving) TrainCooldown++;
        if (beat % 8 == 4 && UnityEngine.Random.value < 0.3f && !TrainMoving && TrainCooldown > 8)
        {
            TrainCooldown = UnityEngine.Random.Range(-4, 1);
            StartTrain();
        }
        if (beat % 4 == 0)
        {
            props["lights"].BuildingFade = 0;
            ColorUtility.TryParseHtmlString((Erect ? erectColors : normalColors)[UnityEngine.Random.Range(0, 5)], out Color color);
            props["lights"].Tint = color;
        }
    }

    public void BeginDeath()
    {
        death = Graphic(Path.Combine(root, "characters/bf-death"), "Boyfriend Death", 0);
        death.gameObject.layer = song.deadBoyfriend.layer;
        death.Position = actors[0].graphic.Position;
        death.Play("firstDeath");
        foreach (SpriteRenderer renderer in song.deadBoyfriend.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
        song.deadBoyfriendAnimator.enabled = false;
        song.deadCamera.orthographic = true;
        deathTarget = actors[0].graphic.Position + new Vector3(actors[0].graphic.Size.x / 200, -actors[0].graphic.Size.y / 200, -10)
            + Point(actors[0].data["death"]?["cameraOffsets"]);
        LeanTween.cancel(song.deadCamera.gameObject);
        trainSound.Stop();
    }

    public void PlayDeath(string animation)
    {
        if (death != null) death.Play(animation);
    }

    private void LateUpdate()
    {
        if (song == null) return;
        if (song.isDead)
        {
            Render(0);
            if (death != null)
            {
                float amount = 1 - Mathf.Pow(0.98f, Time.deltaTime * 60);
                song.deadCamera.transform.position = Vector3.Lerp(song.deadCamera.transform.position, deathTarget, amount);
                song.deadCamera.orthographicSize = Mathf.Lerp(song.deadCamera.orthographicSize, 3.6f / CameraZoom, amount);
                death.Advance(Time.deltaTime, song.deadCamera.transform.position, clock);
            }
            return;
        }
        bool paused = song.songStarted && !song.musicSources[0].isPlaying || Pause.instance != null && Pause.instance.pauseScreen.activeSelf;
        if (paused) { trainSound.Pause(); return; }
        trainSound.UnPause();
        float delta = Time.deltaTime;
        clock += delta;
        props["lights"].BuildingFade += song.stepCrochet * 4 / 1000 * delta * 1.5f;
        if (TrainMoving)
        {
            trainFrameTiming += delta;
            if (trainFrameTiming >= 1f / 24)
            {
                AdvanceTrain(trainSound.time * 1000);
                trainFrameTiming = 0;
            }
        }
        foreach (Actor actor in actors)
        {
            bool singing = actor.singing > 0;
            actor.singing -= delta;
            if (actor.graphic.Finished && actor.graphic.Has(actor.graphic.Animation + "-hold")) actor.Play(actor.graphic.Animation + "-hold");
            if (singing && actor.singing <= 0) actor.Dance();
        }
        VanillaCharacterTiming.Advance(song, ref lastDanceStep, step =>
        {
            foreach (Actor actor in actors)
                if (actor.graphic.gameObject.activeSelf && VanillaCharacterTiming.IsDanceStep(actor.data, step)) actor.Dance();
        });
        if (song.songStarted)
        {
            int beat = Mathf.FloorToInt(song.vanillaPlayback.BeatAt(song.SongPosition));
            while (lastBeat < beat) Beat(++lastBeat);
        }
        Render(delta);
    }

    private void Render(float delta)
    {
        Vector3 camera = song.mainCamera.transform.position;
        foreach (VanillaWeek2Graphic prop in props.Values) prop.Advance(delta, camera, clock);
        foreach (Actor actor in actors) actor.graphic.Advance(delta, camera, clock);
    }

    private static Vector3 Point(JToken value) => value == null ? Vector3.zero
        : new Vector3((float)value[0] / 100, -(float)value[1] / 100, 0);

    private void OnDestroy()
    {
        if (trainClip != null) Destroy(trainClip);
    }
}
