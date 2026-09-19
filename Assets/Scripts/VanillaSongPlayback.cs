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
    public string Variation { get; private set; }
    public VanillaErectStage Stage { get; private set; }
    public VanillaWeek2Stage Week2Stage { get; private set; }
    public VanillaWeek3Stage Week3Stage { get; private set; }
    public VanillaCampaignStage CampaignStage { get; private set; }
    public VanillaCampaignPresentation Presentation { get; private set; }
    public IVanillaCharacterStage CharacterStage => (IVanillaCharacterStage)CampaignStage ?? (IVanillaCharacterStage)Week3Stage ?? Week2Stage;
    public bool IsCampaign { get; private set; }
    public bool IsMainStage { get; private set; }
    public bool IsPixel { get; private set; }
    public bool IsSpaghetti => SongId == "spaghetti";
    public int UnscoredNotes(int side) => sourceData?["noteKinds"]?[Song.difficulty.ToLowerInvariant()]?.Count(note => (string)note["k"] == "non_scoreable" && (int)note["d"] / 4 == side) ?? 0;
    public bool IsScoreable(int side, int direction, double time)
    {
        foreach (var note in unscoredNotes)
            if (note.lane == side * 4 + direction && System.Math.Abs(note.time - time) < .02) return false;
        return true;
    }
    private (int lane, double time)[] unscoredNotes = System.Array.Empty<(int, double)>();
    public string PlayerId => (string)sourceData?["characters"]?["player"] ?? "bf";
    public string OpponentId => (string)sourceData?["opponent"];
    public bool IsWeek2 { get; private set; }
    public bool IsWeek3 { get; private set; }
    public bool UsesSourceCamera => IsErect || IsWeek2 || IsWeek3 || IsCampaign || IsMainStage;
    public Vector3 CameraFocusTarget => OptionsV2.LiteMode || OptionsV2.Middlescroll ? CameraTargets[2] : cameraTo;
    public float CameraSize => 3.6f / Mathf.Max(.1f, zoom) / gameBop;
    private Vector3[] CameraTargets => CampaignStage != null ? CampaignStage.CameraTargets : Week3Stage != null ? Week3Stage.CameraTargets : Week2Stage != null ? Week2Stage.CameraTargets : Stage.CameraTargets;
    private float stageZoom;
    private JObject sourceData;
    private JToken[] timeChanges;
    private Character week2Opponent;
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
                case "cubeInOut":
                    eased = t < .5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                    break;
                case "quartInOut":
                    eased = t < .5f ? 8 * Mathf.Pow(t, 4) : 1 - Mathf.Pow(-2 * t + 2, 4) / 2;
                    break;
                case "quadOut":
                    eased = 1 - (1-t)*(1-t);
                    break;
                case "sineOut":
                    eased = Mathf.Sin(t * Mathf.PI / 2);
                    break;
                case "quartOut":
                    eased = 1 - Mathf.Pow(1 - t, 4);
                    break;
                case "circOut":
                    eased = Mathf.Sqrt(1 - (t - 1) * (t - 1));
                    break;
                case "circIn":
                    eased = 1 - Mathf.Sqrt(1 - t * t);
                    break;
                case "circInOut":
                    eased = t < .5f ? (1 - Mathf.Sqrt(1 - 4 * t * t)) / 2 : (Mathf.Sqrt(1 - Mathf.Pow(-2 * t + 2, 2)) + 1) / 2;
                    break;
                case "quartIn":
                    eased = Mathf.Pow(t, 4);
                    break;
                case "sineInOut":
                    eased = (1 - Mathf.Cos(Mathf.PI * t)) / 2;
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
        Variation = (string)data["variation"] ?? "";
        IsErect = Variation == "erect";
        IsWeek2 = ((string)data["stage"])?.StartsWith("spookyMansion") == true;
        IsWeek3 = ((string)data["stage"])?.StartsWith("phillyTrain") == true;
        string stageId = (string)data["stage"] ?? "";
        IsMainStage = stageId.StartsWith("mainStage");
        IsCampaign = stageId.StartsWith("limo") || stageId.StartsWith("mall") || stageId.StartsWith("school") || stageId.StartsWith("tankmanBattlefield") || stageId.StartsWith("phillyStreets") || stageId == "phillyBlazin" || IsSpaghetti;
        IsPixel = (string)data["noteStyle"] == "pixel";
        sourceData = data;
        unscoredNotes = data["noteKinds"]?[Song.difficulty.ToLowerInvariant()]?.Where(note => (string)note["k"] == "non_scoreable")
            .Select(note => ((int)note["d"], (double)note["t"])).ToArray() ?? System.Array.Empty<(int, double)>();
        if (Presentation == null) Presentation = song.gameObject.AddComponent<VanillaCampaignPresentation>();
        if (IsPixel)
        {
            song.deadNoise = Resources.Load<AudioClip>("FunkinHud/Pixel/loss");
            song.deadTheme = Resources.Load<AudioClip>("FunkinHud/Pixel/gameOver");
            song.deadConfirm = Resources.Load<AudioClip>("FunkinHud/Pixel/gameOverEnd");
        }
        if (PlayerId.StartsWith("pico"))
        {
            string loss = PlayerId == "pico-blazin" ? "pico-gutpunch" : PlayerId == "pico-pixel" ? "pixel-pico" : PlayerId == "pico-holding-nene" ? "pico-and-nene" : "pico";
            string music = PlayerId == "pico-pixel" ? "pixel-pico" : "pico";
            song.deadNoise = Resources.Load<AudioClip>("FunkinHud/Pico/loss-" + loss);
            song.deadTheme = Resources.Load<AudioClip>("FunkinHud/Pico/gameOver-" + music);
            song.deadConfirm = Resources.Load<AudioClip>("FunkinHud/Pico/gameOverEnd-" + music);
        }
        timeChanges = data["timeChanges"]?.ToArray();
        stageZoom = IsWeek3 ? 1.1f : IsWeek2 ? 1 : IsErect ? 0.85f : 1.1f;
        if (IsCampaign || IsMainStage)
        {
            int week = IsMainStage ? 1 : stageId.StartsWith("limo") ? 4 : stageId.StartsWith("mall") ? 5 : stageId.StartsWith("school") ? 6 : stageId.StartsWith("tankman") ? 7 : 8;
            string path = Path.Combine(Application.streamingAssetsPath, IsSpaghetti ? "Bundles/SpaghettiAssets/stages" : "Bundles/Week" + week + "Assets/stages", stageId, "stage.json");
            stageZoom = (float)JObject.Parse(File.ReadAllText(path))["cameraZoom"];
        }
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
        zoom = stageZoom;
        zoomTransition = new Transition { from = zoom, to = zoom };
        scrollTransition = new Transition { from = scroll, to = scroll };
        song.speedDifference = 0;
        if ((IsWeek2 || IsWeek3 || IsCampaign || IsMainStage) && week2Opponent == null)
        {
            string id = (string)data["opponent"];
            week2Opponent = song.enemy != null ? Instantiate(song.enemy) : ScriptableObject.CreateInstance<Character>();
            week2Opponent.animations ??= new System.Collections.Generic.List<SpriteAnimation>();
            week2Opponent.characterName = IsCampaign || IsMainStage ? id : IsWeek3 ? "Pico" : id == "monster" ? "Monster" : "Spooky Kids";
            week2Opponent.portrait = FunkinHudAssets.Icon(id == "spooky-dark" ? "spooky" : id)[0];
            week2Opponent.portraitDead = week2Opponent.portrait;
            song.charactersDictionary[id] = week2Opponent;
            Cache.cachedOpponents[id] = week2Opponent;
        }
        var hey = Resources.Load<SpriteAnimation>("VanillaSongs/Boyfriend Hey");
        if (hey != null && song.boyfriendAnimator.spriteAnimations.All(a => a == null || a.Name != hey.Name))
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
        if (!UsesSourceCamera) ApplyUntil(0);
    }

    public void SetupStage()
    {
        if (!UsesSourceCamera || OptionsV2.DesperateMode) return;
        if (IsCampaign || IsMainStage)
        {
            if (CampaignStage == null) CampaignStage = VanillaCampaignStage.Create(song, sourceData);
            else CampaignStage.ResetStage();
        }
        else if (IsWeek3)
        {
            if (Week3Stage == null) Week3Stage = VanillaWeek3Stage.Create(song, sourceData);
            else Week3Stage.ResetStage();
        }
        else if (IsWeek2)
        {
            if (Week2Stage == null) Week2Stage = VanillaWeek2Stage.Create(song, sourceData);
            else Week2Stage.ResetStage();
        }
        else if (Stage == null) Stage = VanillaErectStage.Create(song);
        Transform graphicRoot = CampaignStage != null ? CampaignStage.transform : Week3Stage != null ? Week3Stage.transform : Week2Stage != null ? Week2Stage.transform : Stage.transform;
        foreach (var graphic in graphicRoot.GetComponentsInChildren<VanillaWeek2Graphic>(true)) graphic.WarmFrames();
        cameraTo = CameraTargets[2];
        song.mainCamera.transform.position = cameraTo;
        song.mainCamera.orthographicSize = 3.6f / zoom;
        ApplyUntil(0);
        if (IsSpaghetti)
        {
            cameraTo = new Vector3(10.7f, -4.7f, -10);
            zoom = .55f;
            zoomTransition = new Transition { from = zoom, to = zoom };
            song.mainCamera.transform.position = cameraTo;
            song.mainCamera.orthographicSize = 3.6f / zoom;
        }
    }

    public bool MoveCamera(Camera camera)
    {
        if (!UsesSourceCamera || Stage == null && CharacterStage == null) return false;
        if (Presentation != null && Presentation.OwnsCamera) return true;
        if (OptionsV2.LiteMode || OptionsV2.Middlescroll)
        {
            camera.transform.position = CameraTargets[2];
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
        if (IsWeek2 || IsWeek3 || IsCampaign || IsMainStage)
        {
            JToken tempo = TempoAt(time);
            stepMilliseconds = tempo == null ? stepMilliseconds : 15000 / (float)tempo["bpm"];
            song.stepCrochet = stepMilliseconds;
            song.beatsPerSecond = stepMilliseconds * 4 / 1000;
        }
        ApplyUntil(time);
        zoom = zoomTransition.Value(time);
        scroll = scrollTransition.Value(time);
        song.defaultGameZoom = (UsesSourceCamera ? 3.6f : baseZoom * 1.1f) / Mathf.Max(0.1f, zoom);
        song.speedDifference = (baseSpeed - scroll) / 100;
        if (UsesSourceCamera)
        {
            int step = Mathf.FloorToInt(BeatAt(time) * 4);
            while (lastStep < step)
            {
                lastStep++;
                if (VanillaPreferences.CameraZooms && BopRate > 0 && Mathf.Abs((lastStep + bopOffset * 4) % (BopRate * 4)) < 0.001f && hudBop < 1.35f)
                {
                    gameBop = 1 + 0.015f * BopIntensity;
                    hudBop += 0.03f * BopIntensity;
                }
            }
            float decay = Mathf.Pow(0.95f, Time.deltaTime * 60);
            gameBop = Mathf.Lerp(1, gameBop, decay);
            hudBop = Mathf.Lerp(1, hudBop, decay);
            if (Presentation == null || !Presentation.OwnsCamera) song.mainCamera.orthographicSize = song.defaultGameZoom / gameBop;
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
            if (IsSpaghetti) CampaignStage?.SpaghettiEvent((string)entry["e"], value, eventTime);
            switch ((string)entry["e"])
            {
                case "FocusCamera":
                    FocusCharacter = value.Type == JTokenType.Object ? (int?)value["char"] ?? 0 : (int)value;
                    FocusOnPlayer = FocusCharacter == 0;
                    if (UsesSourceCamera && (Stage != null || CharacterStage != null))
                    {
                        FocusOffset = value.Type == JTokenType.Object
                            ? new Vector2((float?)value["x"] ?? 0, (float?)value["y"] ?? 0) : Vector2.zero;
                        cameraFrom = song.mainCamera.transform.position;
                        cameraTo = (FocusCharacter < 0 ? new Vector3(0, 0, -10) : CameraTargets[FocusCharacter])
                            + new Vector3(FocusOffset.x / 100, -FocusOffset.y / 100, 0);
                        string ease = value.Type == JTokenType.Object ? (string)value["ease"] ?? "CLASSIC" : "CLASSIC";
                        classicCamera = ease == "CLASSIC";
                        if (!classicCamera) cameraTransition = MakeTransition(value, eventTime, 0, 1);
                    }
                    break;
                case "ZoomCamera":
                    zoom = zoomTransition.Value(eventTime);
                    float zoomTarget = (float)value["zoom"] * ((string)value["mode"] == "stage" ? stageZoom : 1);
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
                    if (PlayerId.StartsWith("pico") && ((string)value["target"] == "bf" || (string)value["target"] == "boyfriend")
                        && new[] { "burpShit", "burpSmile", "burpSmileLong" }.Contains((string)value["anim"])) song.vocalSource.mute = false;
                    if (CharacterStage != null)
                    {
                        CharacterStage.PlayAnimation((string)value["target"], (string)value["anim"]);
                        break;
                    }
                    string character = (string)value["target"];
                    bool player = character == "bf" || character == "boyfriend";
                    string animation = (string)value["anim"];
                    song.PlayChartAnimation(player, player && animation == "hey" ? "BF Hey" : animation == "cheer" ? "Cheer" : animation);
                    break;
                case "EnableMask":
                    CampaignStage?.EnableTankmanMask();
                    break;
                case "SetHealthIcon":
                    song.FunkinHud?.SetIcon((int?)value["char"] ?? 0, (string)value["id"]);
                    break;
            }
        }
    }

    private JToken TempoAt(double time)
    {
        if (timeChanges == null || timeChanges.Length == 0) return null;
        for (int index = timeChanges.Length - 1; index >= 0; index--)
            if ((double)timeChanges[index]["t"] <= time) return timeChanges[index];
        return timeChanges[0];
    }

    public float BeatAt(double time)
    {
        JToken change = TempoAt(time);
        return change == null ? (float)(time / (stepMilliseconds * 4))
            : (float)((double?)change["b"] ?? 0) + (float)((time - (double)change["t"]) * (double)change["bpm"] / 60000);
    }

    private void OnDestroy()
    {
        if (Week2Stage != null) Destroy(Week2Stage.gameObject);
        if (Week3Stage != null) Destroy(Week3Stage.gameObject);
        if (CampaignStage != null) Destroy(CampaignStage.gameObject);
        if (week2Opponent != null)
        {
            string id = (string)sourceData["opponent"];
            if (Cache.cachedOpponents.TryGetValue(id, out Character cached) && cached == week2Opponent) Cache.cachedOpponents.Remove(id);
            Destroy(week2Opponent);
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
