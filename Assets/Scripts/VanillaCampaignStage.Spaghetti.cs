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
    private static readonly string[] SpaghettiDirections = { "LEFT", "DOWN", "UP", "RIGHT" };
    private static readonly float[] SpaghettiDustSpeeds = { 350, -300, -200, -150 };
    private static readonly float[] SpaghettiDustHeights = { -400, -450, -600, -1500 };
    private static readonly float[] SpaghettiDustDurations = { 20, 16, 24, 16 };
    private static readonly float[] SpaghettiDustRises = { 100, 200, 150, 100 };
    private readonly Actor[] spaghettiActors = new Actor[6];
    private readonly bool[] spaghettiSinging = new bool[6];
    private readonly List<VanillaWeek2Graphic> spaghettiDust = new List<VanillaWeek2Graphic>();
    private readonly List<VanillaWeek2Graphic> spaghettiEffects = new List<VanillaWeek2Graphic>();
    private readonly Dictionary<string, AudioClip> spaghettiSounds = new Dictionary<string, AudioClip>();
    private readonly List<LipSync> spaghettiLips = new List<LipSync>();
    private VanillaWeek2Graphic spaghettiFloor;
    private VanillaWeek2Graphic spaghettiIntroFloor;
    private VanillaWeek2Graphic spaghettiCutscene;
    private VanillaWeek2Graphic spaghettiBf;
    private VanillaWeek2Graphic spaghettiGf;
    private SpriteRenderer spaghettiCover;
    private float spaghettiClearAt = -1;
    private float spaghettiDustAt;
    private float spaghettiDarkFrom;
    private float spaghettiDarkTo;
    private float spaghettiDarkAt;
    private float spaghettiDarkDuration;
    private float spaghettiTruckAt = -100;
    private float spaghettiTruckAmount;
    private float spaghettiTruckDuration;
    private float spaghettiPulseAt = -100;
    private float spaghettiPulseAmount;
    private float spaghettiPulseDuration;
    private Color spaghettiPulseColor = Color.white;
    private bool spaghettiPulseEnabled;
    private Color[] spaghettiColors = { Color.white };
    private float[] spaghettiDurations = { .5f };
    private float[] spaghettiIntensities = { 1 };
    private bool spaghettiDoorReleased;
    public bool SpaghettiIconVisible { get; private set; }
    public bool SpaghettiAudioReady { get; private set; }
    public float SpaghettiDarkness { get; private set; }
    public Vector4 SpaghettiAdjustment { get; set; }
    public VanillaWeek2Graphic SpaghettiCharacter(int index) => spaghettiActors[index].current;
    public bool SpaghettiSinging(int index) => spaghettiSinging[index];

    private sealed class LipSync
    {
        public Actor actor;
        public int index;
        public VanillaWeek2Graphic graphic;
        public VanillaWeek2Graphic foreground;
        public JObject poses;
        public bool flip;
        public Vector2 offset;
        public float angle;
    }

    private VanillaWeek2Graphic SpaghettiEffect(string path, Vector2 position, float scroll, int order)
    {
        var graphic = Graphic(Path.Combine(root, "effects", path), path, order);
        graphic.Position = new Vector3(position.x / 100, -position.y / 100);
        graphic.Scroll = Vector2.one * scroll;
        spaghettiEffects.Add(graphic);
        return graphic;
    }

    private void LoadSpaghetti()
    {
        spaghettiActors[1] = actors[1];
        spaghettiActors[4] = actors[0];
        spaghettiActors[5] = actors[2];
        string[] names = { "yunjin", "chaewon", "eunchae" };
        int[] indices = { 0, 2, 3 };
        Vector2[] positions = { new Vector2(-621, 154), new Vector2(687, 98), new Vector2(770, 675) };
        for (int i = 0; i < names.Length; i++)
        {
            string id = "sserafim-" + names[i];
            string folder = Path.Combine(root, "characters", id);
            var data = JObject.Parse(File.ReadAllText(Path.Combine(folder, "character.json")));
            var graphic = Graphic(folder, id, 1000);
            graphic.Position = new Vector3((positions[i].x - Mathf.Floor(graphic.HitboxSize.x) / 2) / 100,
                -(positions[i].y - Mathf.Floor(graphic.HitboxSize.y)) / 100);
            graphic.GlobalOffset = Point(data["offsets"]);
            graphic.Scroll = Vector2.one * (i == 2 ? .97f : .95f);
            spaghettiActors[indices[i]] = new Actor { id = id, data = data, graphic = graphic, current = graphic };
        }
        spaghettiFloor = SpaghettiEffect("floor", Vector2.zero, 1, 11);
        spaghettiIntroFloor = SpaghettiEffect("cutscene/floor-cutscene", Vector2.zero, 1, 11);
        spaghettiCutscene = SpaghettiEffect("cutscene/cutsceneMain", new Vector2(-395, 10), .94f, 25);
        spaghettiBf = SpaghettiEffect("cutscene/bfGetUp", new Vector2(1220, 531), .99f, 305);
        spaghettiGf = SpaghettiEffect("cutscene/gfGetUp", new Vector2(655, -104), .95f, 25);
        string[] dustColors = { "#98847d", "#8b6c63", "#6e645c", "#886a60" };
        float[] scales = { 1.5f, 1.5f, 2, 3.5f };
        for (int layer = 0; layer < 4; layer++)
            for (int tile = -2; tile <= 2; tile++)
            {
                var dust = Graphic(Path.Combine(root, "effects/dust", layer == 0 || layer == 2 ? "dustMid" : "dustBack"), "Dust " + layer + " " + tile, 2000);
                dust.transform.localScale = Vector3.one * scales[layer];
                dust.Scroll = Vector2.one * (1.1f + layer * .05f);
                ColorUtility.TryParseHtmlString(dustColors[layer], out Color color);
                dust.Tint = color;
                spaghettiDust.Add(dust);
            }
        for (int i = 0; i < 5; i++)
        {
            Actor actor = spaghettiActors[i];
            var data = JObject.Parse(File.ReadAllText(Path.Combine(root, "characters", actor.id, "lipsync.json")));
            var lip = Graphic(Path.Combine(root, "effects", i == 0 ? "sserafim-lipsync-yunjin" : "sserafim-lipsync"), actor.id + " Lip Sync", i == 1 ? 201 : i == 4 ? 301 : 1001);
            lip.transform.SetParent(actor.graphic.transform, false);
            VanillaWeek2Graphic foreground = null;
            if (i == 0)
            {
                foreground = Graphic(Path.Combine(root, "characters", actor.id, "foreground"), actor.id + " Foreground", 1002);
                foreground.transform.SetParent(actor.graphic.transform, false);
                foreground.ApplyAnimationOffsets = false;
            }
            spaghettiLips.Add(new LipSync { actor = actor, index = i, graphic = lip, foreground = foreground, poses = (JObject)data["poses"], flip = (bool)data["flipX"] });
        }
        spaghettiCover = solids.First(item => item.renderer.name == "solidCover").renderer;
        props["truckLight1"].Additive = true;
        props["backLightColor"].Additive = true;
        StartCoroutine(LoadSpaghettiAudio());
    }

    private IEnumerator LoadSpaghettiAudio()
    {
        foreach (string path in Directory.GetFiles(Path.Combine(root, "audio"), "*.ogg", SearchOption.AllDirectories))
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
                spaghettiSounds[Path.GetFileNameWithoutExtension(path)] = DownloadHandlerAudioClip.GetContent(request);
            }
        SpaghettiAudioReady = true;
    }

    public void PlaySpaghettiSound(string name)
    {
        if (spaghettiSounds.TryGetValue(name, out var clip)) sound.PlayOneShot(clip);
    }

    public void FadeSpaghettiSound(float volume) => sound.volume = Mathf.Clamp01(volume);

    public void StopSpaghettiSound()
    {
        sound.Stop();
        sound.volume = 1;
    }

    private void ResetSpaghetti()
    {
        SpaghettiIconVisible = false;
        spaghettiClearAt = -1;
        spaghettiDoorReleased = false;
        spaghettiDarkFrom = spaghettiDarkTo = SpaghettiDarkness = 0;
        spaghettiTruckAt = spaghettiPulseAt = -100;
        spaghettiPulseEnabled = false;
        SpaghettiAdjustment = new Vector4(6, -74, -24, -26);
        for (int i = 0; i < spaghettiActors.Length; i++)
        {
            spaghettiSinging[i] = false;
            spaghettiActors[i].beautiful = false;
            spaghettiActors[i].locked = false;
            spaghettiActors[i].holdTimer = 0;
            spaghettiActors[i].Play(i == 0 ? "doorclosed" : i == 5 ? "danceLeft" : "idle");
        }
        SetSpaghettiCutscene(false);
        SetSpaghettiCover(0);
        SpaghettiGetUp(true);
        foreach (string name in new[] { "truckLight1", "truckLight2", "backLightColor", "backLightWhite" }) props[name].Alpha = 0;
    }

    public void SetSpaghettiCover(float alpha) => spaghettiCover.color = new Color(0, 0, 0, alpha);

    private void SetSpaghettiVisible(bool[] visible)
    {
        for (int i = 0; i < Math.Min(5, visible.Length); i++)
        {
            spaghettiActors[i].hidden = !visible[i];
            spaghettiActors[i].current.gameObject.SetActive(visible[i]);
        }
    }

    public void SetSpaghettiCutscene(bool active)
    {
        SetSpaghettiVisible(new[] { !active, false, false, false, false });
        foreach (string name in new[] { "truck", "backTables", "backStools", "frontStool" }) props[name].gameObject.SetActive(!active);
        props["truckDoor"].gameObject.SetActive(false);
        props["backTablesCutscene"].gameObject.SetActive(active);
        props["burgerCutscene"].gameObject.SetActive(active);
        spaghettiFloor.gameObject.SetActive(!active);
        spaghettiIntroFloor.gameObject.SetActive(active);
        spaghettiCutscene.gameObject.SetActive(active);
        spaghettiActors[5].hidden = true;
        spaghettiActors[5].current.gameObject.SetActive(false);
        spaghettiBf.gameObject.SetActive(false);
        spaghettiGf.gameObject.SetActive(false);
        foreach (var dust in spaghettiDust) dust.gameObject.SetActive(!active);
        if (active)
        {
            SpaghettiAdjustment = Vector4.zero;
            spaghettiCutscene.Play("idle");
        }
    }

    public void SpaghettiGetUp(bool restart = false, bool still = false)
    {
        if (restart || still) spaghettiDustAt = clock;
        foreach (var graphic in new[] { spaghettiGf, spaghettiBf })
        {
            graphic.gameObject.SetActive(true);
            graphic.Play(still ? "static" : "getup");
            if (restart) graphic.SeekAnimationFrame(23);
        }
    }

    public void DarkenSpaghetti(float amount, float duration)
    {
        spaghettiDarkFrom = SpaghettiDarkness;
        spaghettiDarkTo = amount;
        spaghettiDarkAt = clock;
        spaghettiDarkDuration = duration;
    }

    public void SpaghettiEvent(string kind, JToken value, float time)
    {
        switch (kind)
        {
            case "sserafimGuitarVibration": break;
            case "sserafimShow": SetSpaghettiVisible(value["visible"].Values<bool>().ToArray()); break;
            case "sserafimSing":
                for (int i = 0; i < 6; i++) spaghettiSinging[i] = (bool)value["singing"][i];
                break;
            case "sserafimBeautiful": spaghettiActors[5].beautiful = (bool)value["beautiful"]; break;
            case "sserafimDark": DarkenSpaghetti((float)value["amount"], (float)value["duration"]); break;
            case "sserafimCover": SetSpaghettiCover((bool)value["visible"] ? 1 : 0); break;
            case "sserafimLights":
                spaghettiTruckAt = clock;
                spaghettiTruckAmount = (float)value["amount"];
                spaghettiTruckDuration = (float)value["duration"];
                break;
            case "sserafimPulseLights":
                spaghettiPulseEnabled = (bool)value["enabled"];
                if (value["colors"] != null)
                {
                    spaghettiColors = value["colors"].Values<string>().Select(text =>
                    {
                        ColorUtility.TryParseHtmlString("#" + text.Substring(text.Length - 6), out Color color);
                        return color;
                    }).ToArray();
                    spaghettiDurations = value["durations"].Values<float>().ToArray();
                    spaghettiIntensities = value["intensities"].Values<float>().ToArray();
                }
                break;
            case "sserafimKick":
                bool final = (bool)value["final"];
                spaghettiActors[0].Play(final ? "kick2" : "kick1", true);
                PlaySpaghettiSound(final ? "doorKick2" : "doorKick1");
                if (final)
                {
                    spaghettiClearAt = clock;
                    SpaghettiIconVisible = true;
                    spaghettiActors[5].hidden = false;
                    spaghettiActors[5].current.gameObject.SetActive(true);
                    spaghettiBf.gameObject.SetActive(false);
                    spaghettiGf.gameObject.SetActive(false);
                }
                break;
            case "sserafimFlash": song.vanillaPlayback.Presentation.FlashSpaghetti((float)value["duration"]); break;
            case "sserafimEnd": song.vanillaPlayback.Presentation.BeginSpaghettiEnding(song); break;
        }
    }

    private void SingSpaghetti(int side, int direction, bool miss, string kind)
    {
        if (kind == "non_scoreable") return;
        for (int i = 0; i < spaghettiActors.Length; i++)
        {
            if (spaghettiSinging[i] != (side == 0)) continue;
            Actor actor = spaghettiActors[i];
            string suffix = i == 4 && kind != null && kind.StartsWith("sakura-") ? "-" + kind.Substring(7) : "";
            if (miss && suffix == "-bf1") suffix = "-bf2";
            actor.Play("sing" + SpaghettiDirections[direction] + (miss ? "miss" : "") + suffix);
            if (!miss) actor.holdTimer = 0;
        }
    }

    private void HoldSpaghetti(int side, bool pressed)
    {
        for (int i = 0; i < spaghettiActors.Length; i++)
            if (spaghettiSinging[i] == (side == 0) && (pressed ? VanillaCharacterTiming.IsManualSide(side)
                : !VanillaCharacterTiming.IsManualSide(side) && VanillaCharacterTiming.IsSinging(spaghettiActors[i].current.Animation)))
                spaghettiActors[i].holdTimer = 0;
    }

    private static float SineOut(float value) => Mathf.Sin(Mathf.Clamp01(value) * Mathf.PI / 2);
    private static float CubeFade(float age, float duration)
    {
        float t = Mathf.Clamp01(age / Mathf.Max(.001f, duration));
        return 1 - (t < .5f ? 4*t*t*t : 1 - Mathf.Pow(-2*t+2, 3) / 2);
    }

    public void AdvanceSpaghetti(float delta)
    {
        float t = Mathf.Clamp01((clock - spaghettiDarkAt) / Mathf.Max(.001f, spaghettiDarkDuration));
        SpaghettiDarkness = Mathf.Lerp(spaghettiDarkFrom, spaghettiDarkTo, (1 - Mathf.Cos(Mathf.PI * t)) / 2);
        if (spaghettiClearAt >= 0) SpaghettiAdjustment = new Vector4(6, -74, -24, -26) * (1 - SineOut((clock - spaghettiClearAt) / 24));
        for (int i = 0; i < 6; i++)
        {
            if (i == 0 && spaghettiClearAt < 0) continue;
            spaghettiActors[i].UpdateSinging(delta, song.stepCrochet, VanillaCharacterTiming.IsHoldingInput(spaghettiSinging[i] ? 0 : 1));
        }
        VanillaCharacterTiming.Advance(song, ref lastDanceStep, step =>
        {
            for (int i = 0; i < 6; i++)
                if ((i != 0 || spaghettiClearAt >= 0) && !spaghettiActors[i].hidden && VanillaCharacterTiming.IsDanceStep(spaghettiActors[i].data, step)) spaghettiActors[i].Dance();
            if (step % 4 == 0 && spaghettiPulseEnabled)
            {
                int beat = Math.Max(0, step / 4);
                spaghettiPulseAt = clock;
                spaghettiPulseAmount = spaghettiIntensities[beat % spaghettiIntensities.Length];
                spaghettiPulseDuration = spaghettiDurations[beat % spaghettiDurations.Length];
                spaghettiPulseColor = spaghettiColors[beat % spaghettiColors.Length];
            }
        });
    }

    public static Color SpaghettiLighting(float dark, float truck, float pulse, Color color, bool character)
    {
        float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        float l = (max + min) / 2;
        float saturation = max == min ? 0 : (max - min) / (1 - Mathf.Abs(2 * l - 1));
        Color.RGBToHSV(color, out float hue, out _, out _);
        float luminance = l * pulse;
        float chroma = (1 - Mathf.Abs(2 * luminance - 1)) * saturation;
        Color rgb = Color.HSVToRGB(hue, 1, 1) * chroma;
        rgb += new Color(luminance - chroma / 2, luminance - chroma / 2, luminance - chroma / 2);
        float factor = dark + (truck + pulse) * (dark > .65f ? -.07f : .07f);
        Color result = Color.LerpUnclamped(Color.white * (1 - factor / (character ? 5 : 1)), rgb, factor / (character ? 3 : 2));
        result.a = 1;
        return result;
    }

    private void RenderSpaghetti(float delta, Vector3 camera)
    {
        float truck = spaghettiTruckAmount * CubeFade(clock - spaghettiTruckAt, spaghettiTruckDuration);
        float pulse = spaghettiPulseAmount * CubeFade(clock - spaghettiPulseAt, spaghettiPulseDuration);
        props["truckLight1"].Alpha = props["truckLight2"].Alpha = truck;
        props["backLightColor"].Alpha = pulse * .8f;
        props["backLightColor"].Tint = spaghettiPulseColor;
        props["backLightWhite"].Alpha = pulse * .7f;
        Color stageColor = SpaghettiLighting(SpaghettiDarkness, truck, pulse * .8f, spaghettiPulseColor, false);
        Color actorColor = SpaghettiLighting(SpaghettiDarkness, truck, pulse * .8f, spaghettiPulseColor, true);
        foreach (var pair in props)
        {
            if (pair.Key.Contains("Light")) continue;
            pair.Value.ColorAdjustment = SpaghettiAdjustment;
            pair.Value.Lighting = stageColor;
            pair.Value.Advance(0, camera, clock);
        }
        foreach (var actor in spaghettiActors)
        {
            actor.current.ColorAdjustment = SpaghettiAdjustment;
            actor.current.Lighting = actorColor;
            actor.current.Advance(actor == actors[0] || actor == actors[1] || actor == actors[2] ? 0 : delta, camera, clock);
        }
        if (!spaghettiDoorReleased && spaghettiActors[0].current.Animation == "kick2" && spaghettiActors[0].current.AnimationFrame >= 23)
        {
            spaghettiDoorReleased = true;
            props["truckDoor"].gameObject.SetActive(true);
        }
        foreach (var graphic in spaghettiEffects)
        {
            graphic.ColorAdjustment = SpaghettiAdjustment;
            graphic.Lighting = graphic == spaghettiFloor || graphic == spaghettiIntroFloor ? stageColor : actorColor;
            graphic.Advance(delta, camera, clock);
        }
        for (int i = 0; i < 2; i++)
        {
            var floor = i == 0 ? spaghettiFloor : spaghettiIntroFloor;
            Vector3 scroll = camera - new Vector3(6.4f, -3.6f, camera.z);
            Vector2 top = new Vector2(7.9f + scroll.x * .07f, -6.25f + scroll.y * .07f);
            Vector2 bottom = new Vector2(7.6f - scroll.x * .05f, -13.75f - scroll.y * .05f);
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.m01 = (top.x - bottom.x) / (floor.Size.y / 100);
            matrix.m11 = (top.y - bottom.y) / (floor.Size.y / 100);
            matrix.m03 = top.x - floor.Size.x / 200;
            matrix.m13 = top.y;
            floor.VertexTransform = matrix;
            floor.Advance(0, camera, clock);
        }
        for (int i = 0; i < spaghettiDust.Count; i++)
        {
            int layer = i / 5;
            var dust = spaghettiDust[i];
            float age = spaghettiClearAt < 0 ? 0 : Mathf.Max(0, clock - spaghettiClearAt);
            float duration = SpaghettiDustDurations[layer];
            float progress = SineOut(age / duration);
            float integrated = spaghettiClearAt < 0 ? clock - spaghettiDustAt : spaghettiClearAt - spaghettiDustAt + Mathf.Min(age, duration)
                + (Mathf.Cos(Mathf.Min(age, duration) / duration * Mathf.PI / 2) - 1) * duration * 2 / Mathf.PI;
            float width = dust.Size.x * Mathf.Abs(dust.transform.localScale.x);
            Vector2 origin = dust.Size * (dust.transform.localScale.x - 1) / 2;
            dust.Position = new Vector3((-650 + SpaghettiDustSpeeds[layer] * integrated % width + (i % 5 - 2) * width - origin.x) / 100,
                -(SpaghettiDustHeights[layer] + SpaghettiDustRises[layer] * progress - origin.y) / 100);
            dust.Alpha = 1 - progress;
            dust.Advance(0, camera, clock);
        }
        foreach (LipSync lip in spaghettiLips) RenderSpaghettiLip(lip, camera, actorColor);
    }

    private void RenderSpaghettiLip(LipSync lip, Vector3 camera, Color light)
    {
        var parent = lip.actor.current;
        if (lip.foreground != null)
        {
            lip.foreground.gameObject.SetActive(!lip.actor.hidden);
            lip.foreground.FrozenFrame = parent.Frame;
            lip.foreground.ColorAdjustment = SpaghettiAdjustment;
            lip.foreground.Lighting = light;
            lip.foreground.Advance(0, Vector3.zero, clock);
        }
        JToken attachment = parent.Attachments?.First;
        lip.graphic.gameObject.SetActive(!lip.actor.hidden && attachment != null);
        if (attachment == null) return;
        if (lip.poses[parent.Animation] is JToken pose)
        {
            lip.offset = new Vector2((float)pose["offset"][0], (float)pose["offset"][1]);
            lip.angle = (float)pose["angle"];
        }
        int frame = spaghettiSinging[lip.index] ? Mathf.Max(0, Mathf.FloorToInt((float)(song.SongPosition - Pause.GlobalOffset) * .024f) - 1) : 0;
        lip.graphic.SetAnimationFrame(frame);
        lip.graphic.VertexTransform = SpaghettiLipTransform(parent, lip.graphic, attachment, lip.offset, lip.angle, lip.flip);
        lip.graphic.ColorAdjustment = SpaghettiAdjustment;
        lip.graphic.Lighting = light;
        lip.graphic.Advance(0, Vector3.zero, clock);
    }

    public static Matrix4x4 SpaghettiLipTransform(VanillaWeek2Graphic parent, VanillaWeek2Graphic graphic,
        JToken attachment, Vector2 lipOffset, float angle, bool flip)
    {
        JToken m = attachment["matrix"];
        Vector3 anchor = new Vector3(((float)m[4] - parent.BoundsOrigin.x) / 100,
            -((float)m[5] - parent.BoundsOrigin.y) / 100);
        float rotation = Mathf.Atan2((float)m[1], (float)m[0]) * Mathf.Rad2Deg;
        Vector3 center = new Vector3(Mathf.Floor(graphic.Size.x) / 200, -Mathf.Floor(graphic.Size.y) / 200);
        Vector3 offset = new Vector3(-lipOffset.x / 100, lipOffset.y / 100);
        return Matrix4x4.Translate(anchor + offset + center)
            * Matrix4x4.Rotate(Quaternion.Euler(0, 0, -angle - rotation))
            * Matrix4x4.Translate(flip ? new Vector3((graphic.Size.x - Mathf.Floor(graphic.Size.x)) / 100, 0) : Vector3.zero)
            * Matrix4x4.Scale(new Vector3(flip ? -1 : 1, 1, 1)) * Matrix4x4.Translate(-center);
    }
}
