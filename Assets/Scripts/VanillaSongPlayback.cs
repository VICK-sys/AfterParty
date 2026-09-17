using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SimpleSpriteAnimator;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public sealed class VanillaSongPlayback : MonoBehaviour
{
    public bool FocusOnPlayer { get; private set; }
    public int EventsApplied { get; private set; }
    public string SongId { get; private set; }
    public bool IsErect { get; private set; }
    public VanillaErectStage Stage { get; private set; }
    public int FocusCharacter { get; private set; }
    public Vector2 FocusOffset { get; private set; }
    public float BopRate { get; private set; } = 4;
    public float BopIntensity { get; private set; } = 1;
    private Song song;
    private JToken[] events;
    private float stepMilliseconds;
    private float baseZoom;
    private float baseSpeed;
    private float zoom = 1.1f;
    private float scroll;
    private Transition zoomTransition;
    private Transition scrollTransition;
    private float bopOffset;
    private float gameBop = 1;
    private float hudBop = 1;
    private float hudSize;
    private int lastStep = -1;
    private Vector3 cameraFrom;
    private Vector3 cameraTo;
    private Transition cameraTransition;
    private bool classicCamera = true;

    private struct Transition
    {
        public float start;
        public float duration;
        public float from;
        public float to;
        public string ease;

        public float Value(float time)
        {
            float t = duration <= 0 ? 1 : Mathf.Clamp01((time - start) / duration);
            float eased = t;
            switch (ease)
            {
                case "expoIn":
                    eased = t == 0 ? 0 : Mathf.Pow(2, 10 * (t - 1));
                    break;
                case "expoOut":
                    eased = 1 - Mathf.Pow(2, -10 * t);
                    break;
                case "quadInOut":
                    eased = t <= 0.5f ? 2 * t * t : 1 - 2 * (t - 1) * (t - 1);
                    break;
                case "smoothStepInOut":
                    eased = t * t * (3 - 2 * t);
                    break;
                case "quintInOut":
                    eased = t < 0.5f ? 16 * Mathf.Pow(t, 5) : 1 - Mathf.Pow(-2 * t + 2, 5) / 2;
                    break;
                case "elasticInOut":
                    if (t > 0 && t < 1)
                    {
                        float wave = Mathf.Sin((20 * t - 11.125f) * (2 * Mathf.PI / 4.5f));
                        eased = t < 0.5f ? -Mathf.Pow(2, 20 * t - 10) * wave / 2 : Mathf.Pow(2, -20 * t + 10) * wave / 2 + 1;
                    }
                    break;
            }
            return Mathf.LerpUnclamped(from, to, eased);
        }
    }

    public static VanillaSongPlayback Attach(Song song, float speed)
    {
        string path = song.selectedVanillaPath ?? Path.Combine(song.selectedSongDir, "Vanilla.json");
        if (!File.Exists(path))
        {
            var previous = song.GetComponent<VanillaSongPlayback>();
            if (previous != null)
                previous.enabled = false;
            return null;
        }
        var playback = song.GetComponent<VanillaSongPlayback>() ?? song.gameObject.AddComponent<VanillaSongPlayback>();
        playback.Initialize(song, speed, JObject.Parse(File.ReadAllText(path)));
        return playback;
    }

    private void Initialize(Song owner, float speed, JObject data)
    {
        if (song == null)
        {
            baseZoom = owner.defaultGameZoom;
            hudSize = owner.uiCamera.orthographicSize;
        }
        song = owner;
        enabled = true;
        SongId = (string)data["song"];
        IsErect = (string)data["variation"] == "erect";
        BopRate = 4;
        BopIntensity = 1;
        bopOffset = 0;
        gameBop = hudBop = 1;
        lastStep = -1;
        classicCamera = true;
        events = data["events"].OrderBy(e => (double)e["t"]).ToArray();
        EventsApplied = 0;
        stepMilliseconds = 15000 / (float)data["bpm"];
        baseSpeed = scroll = speed;
        zoom = IsErect ? 0.85f : 1.1f;
        zoomTransition = new Transition { from = zoom, to = zoom };
        scrollTransition = new Transition { from = scroll, to = scroll };
        song.speedDifference = 0;
        var hey = Resources.Load<SpriteAnimation>("VanillaSongs/Boyfriend Hey");
        if (hey != null && song.boyfriendAnimator.spriteAnimations.All(a => a.Name != hey.Name))
            song.boyfriendAnimator.spriteAnimations.Add(hey);
        if ((string)data["opponent"] == "gf")
        {
            Character girlfriend = Resources.Load<Character>("VanillaSongs/Girlfriend");
            song.charactersDictionary["gf"] = girlfriend;
            song.opponentObject.transform.parent.position = song.girlfriendObject.transform.parent.position;
            song.opponentObject.transform.parent.localScale = song.girlfriendObject.transform.parent.localScale;
            var opponentRenderer = song.opponentObject.GetComponent<SpriteRenderer>();
            var playerRenderer = song.boyfriendObject.GetComponent<SpriteRenderer>();
            opponentRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            opponentRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
            song.girlfriendObject.SetActive(false);
        }
        if (!IsErect) ApplyUntil(0);
    }

    public void SetupStage()
    {
        if (!IsErect || OptionsV2.DesperateMode) return;
        if (Stage == null) Stage = VanillaErectStage.Create(song);
        cameraTo = Stage.CameraTargets[2];
        song.mainCamera.transform.position = cameraTo;
        song.mainCamera.orthographicSize = 3.6f / zoom;
        ApplyUntil(0);
    }

    public bool MoveCamera(Camera camera)
    {
        if (!IsErect || Stage == null) return false;
        if (OptionsV2.LiteMode || OptionsV2.Middlescroll)
        {
            camera.transform.position = Stage.CameraTargets[2];
            return true;
        }
        if (!song.songStarted) return true;
        if (!song.musicSources[0].isPlaying) return true;
        float time = song.stopwatch.ElapsedMilliseconds;
        camera.transform.position = classicCamera
            ? Vector3.Lerp(camera.transform.position, cameraTo, 1 - Mathf.Pow(0.96f, Time.deltaTime * 60))
            : Vector3.LerpUnclamped(cameraFrom, cameraTo, cameraTransition.Value(time));
        return true;
    }

    private void Update()
    {
        if (song == null || !song.songStarted || !song.musicSources[0].isPlaying)
            return;
        float time = song.stopwatch.ElapsedMilliseconds;
        ApplyUntil(time);
        zoom = zoomTransition.Value(time);
        scroll = scrollTransition.Value(time);
        song.defaultGameZoom = (IsErect ? 3.6f : baseZoom * 1.1f) / Mathf.Max(0.1f, zoom);
        song.speedDifference = (baseSpeed - scroll) / 100;
        if (IsErect)
        {
            int step = Mathf.FloorToInt(time / stepMilliseconds);
            while (lastStep < step)
            {
                lastStep++;
                if (BopRate > 0 && Mathf.Abs((lastStep + bopOffset * 4) % (BopRate * 4)) < 0.001f && hudBop < 1.35f)
                {
                    gameBop = 1 + 0.015f * BopIntensity;
                    hudBop += 0.03f * BopIntensity;
                }
            }
            float decay = Mathf.Pow(0.95f, Time.deltaTime * 60);
            gameBop = Mathf.Lerp(1, gameBop, decay);
            hudBop = Mathf.Lerp(1, hudBop, decay);
            song.mainCamera.orthographicSize = song.defaultGameZoom / gameBop;
            song.uiCamera.orthographicSize = hudSize / hudBop;
        }
    }

    private void ApplyUntil(float time)
    {
        while (EventsApplied < events.Length && (float)events[EventsApplied]["t"] <= time)
        {
            JToken entry = events[EventsApplied++];
            JToken value = entry["v"];
            float eventTime = (float)entry["t"];
            switch ((string)entry["e"])
            {
                case "FocusCamera":
                    FocusCharacter = value.Type == JTokenType.Object ? (int?)value["char"] ?? 0 : (int)value;
                    FocusOnPlayer = FocusCharacter == 0;
                    if (IsErect && Stage != null)
                    {
                        FocusOffset = value.Type == JTokenType.Object
                            ? new Vector2((float?)value["x"] ?? 0, (float?)value["y"] ?? 0) : Vector2.zero;
                        cameraFrom = song.mainCamera.transform.position;
                        cameraTo = (FocusCharacter < 0 ? new Vector3(0, 0, -10) : Stage.CameraTargets[FocusCharacter])
                            + new Vector3(FocusOffset.x / 100, -FocusOffset.y / 100, 0);
                        string ease = value.Type == JTokenType.Object ? (string)value["ease"] ?? "CLASSIC" : "CLASSIC";
                        classicCamera = ease == "CLASSIC";
                        if (!classicCamera) cameraTransition = MakeTransition(value, eventTime, 0, 1);
                    }
                    break;
                case "ZoomCamera":
                    zoom = zoomTransition.Value(eventTime);
                    float zoomTarget = (float)value["zoom"] * ((string)value["mode"] == "stage" ? (IsErect ? 0.85f : 1.1f) : 1);
                    zoomTransition = MakeTransition(value, eventTime, zoom, zoomTarget);
                    break;
                case "SetCameraBop":
                    BopRate = (float?)value["rate"] ?? 4;
                    BopIntensity = (float?)value["intensity"] ?? 1;
                    bopOffset = (float?)value["offset"] ?? 0;
                    break;
                case "ScrollSpeed":
                    scroll = scrollTransition.Value(eventTime);
                    float target = (float)value["scroll"] * ((bool?)value["absolute"] == true ? 1 : baseSpeed);
                    scrollTransition = MakeTransition(value, eventTime, scroll, target);
                    break;
                case "PlayAnimation":
                    string character = (string)value["target"];
                    bool player = character == "bf" || character == "boyfriend";
                    string animation = (string)value["anim"];
                    song.PlayChartAnimation(player, player && animation == "hey" ? "BF Hey" : animation == "cheer" ? "Cheer" : animation);
                    break;
            }
        }
    }

    private Transition MakeTransition(JToken value, float time, float from, float to)
    {
        string ease = (string)value["ease"] ?? "linear";
        if (ease != "linear" && ease != "INSTANT" && !ease.EndsWith("In") && !ease.EndsWith("Out"))
            ease += (string)value["easeDir"] ?? "In";
        return new Transition
        {
            start = time,
            duration = ease == "INSTANT" ? 0 : ((float?)value["duration"] ?? 4) * stepMilliseconds,
            from = from,
            to = to,
            ease = ease
        };
    }
}
