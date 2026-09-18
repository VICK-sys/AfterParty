using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaFreeplayAnimate : MaskableGraphic
{
    private struct Clip { public int start, length; }
    private struct Quad
    {
        public Vector2 a, b, c, d;
        public Rect uv;
        public Color tint;
        public bool rotated;
    }

    private readonly Dictionary<string, JToken> symbols = new Dictionary<string, JToken>();
    private readonly Dictionary<string, JToken> sprites = new Dictionary<string, JToken>();
    private readonly Dictionary<string, int> lengths = new Dictionary<string, int>();
    private readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, List<Quad>> cache = new Dictionary<int, List<Quad>>();
    private static readonly Dictionary<string, Rect> BoundsCache = new Dictionary<string, Rect>();
    private JToken root;
    private JToken timeline;
    private string assetPath;
    private Texture2D atlas;
    private Texture2D replacement;
    private Matrix4x4 stage = Matrix4x4.identity;
    private int totalFrames, firstFrame, clipLength, frame;
    private float frameRate = 24, clock;
    private double lastUpdateTime;
    private bool playing, looping;
    public bool Finished { get; private set; }
    public int CurrentFrame => frame;
    public string CurrentLabel { get; private set; }
    public int LabelFrame => CurrentLabel != null ? frame - clips[CurrentLabel].start : frame;
    public int CompletedLoops { get; private set; }
    public Vector2 BoundsSize { get; private set; }
    public override Texture mainTexture => replacement != null ? replacement : atlas != null ? atlas : Texture2D.whiteTexture;

    public void Initialize(string path, bool useStagePosition = true, string resourceRoot = "VanillaFreeplay")
    {
        string prefix = resourceRoot + "/" + path.Trim('/');
        assetPath = prefix;
        TextAsset animation = Resources.Load<TextAsset>(prefix + "/Animation");
        TextAsset spriteData = Resources.Load<TextAsset>(prefix + "/spritemap1");
        atlas = Resources.Load<Texture2D>(prefix + "/spritemap1");
        if (animation == null || spriteData == null || atlas == null) throw new InvalidOperationException("Missing Animate assets: " + prefix);
        JObject data = JObject.Parse(animation.text.TrimStart('\uFEFF'));
        JObject map = JObject.Parse(spriteData.text.TrimStart('\uFEFF'));
        root = data["AN"];
        if (root == null) throw new InvalidOperationException("Expected compressed Animate JSON: " + path);
        symbols.Clear();
        sprites.Clear();
        lengths.Clear();
        clips.Clear();
        cache.Clear();
        replacement = null;
        foreach (JToken symbol in data["SD"]["S"])
        {
            string name = (string)symbol["SN"];
            symbols[name] = symbol["TL"];
            lengths[name] = TimelineLength(symbol["TL"]);
        }
        foreach (JToken entry in map["ATLAS"]["SPRITES"])
        {
            JToken sprite = entry["SPRITE"];
            sprites[(string)sprite["name"]] = sprite;
        }
        foreach (JToken layer in root["TL"]["L"])
            foreach (JToken key in layer["FR"])
                if (key["N"] != null) clips[(string)key["N"]] = new Clip { start = Integer(key["I"]), length = Math.Max(1, Integer(key["DU"], 1)) };
        totalFrames = TimelineLength(root["TL"]);
        timeline = root["TL"];
        frameRate = Number(data["MD"]?["FRT"], 24);
        stage = useStagePosition ? Matrix(root["STI"]?["SI"]) : Matrix4x4.identity;
        if (!useStagePosition) NormalizeBounds(assetPath);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.sizeDelta = new Vector2(Number(data["MD"]?["W"], 1280), Number(data["MD"]?["H"], 720));
        raycastTarget = false;
        firstFrame = 0;
        clipLength = totalFrames;
        SetFrame(0);
        SetMaterialDirty();
    }

    public void Play(string label, bool loop)
    {
        if (!clips.TryGetValue(label, out Clip clip)) throw new ArgumentException("Unknown Animate label: " + label);
        PlayFrames(clip.start, clip.length, loop);
        CurrentLabel = label;
    }

    public void SetFrame(int value)
    {
        frame = Mathf.Clamp(value, 0, Math.Max(0, totalFrames - 1));
        playing = false;
        Finished = false;
        CurrentLabel = null;
        CompletedLoops = 0;
        clock = 0;
        lastUpdateTime = Time.realtimeSinceStartupAsDouble;
        SetVerticesDirty();
    }

    public void PlayRange(string label, int start, int length, bool loop)
    {
        if (!clips.TryGetValue(label, out Clip clip)) throw new ArgumentException("Unknown Animate label: " + label);
        PlayFrames(clip.start + start, length < 0 ? clip.length - start : length, loop);
        CurrentLabel = label;
    }

    public void PlayFrames(int start, int length, bool loop)
    {
        firstFrame = start;
        clipLength = length;
        looping = loop;
        playing = true;
        Finished = false;
        CurrentLabel = null;
        CompletedLoops = 0;
        clock = 0;
        lastUpdateTime = Time.realtimeSinceStartupAsDouble;
        frame = firstFrame;
        SetVerticesDirty();
    }

    public int GetSymbolFrameLabel(string symbol, string label)
    {
        foreach (JToken layer in symbols[symbol]["L"])
            foreach (JToken key in layer["FR"])
                if ((string)key["N"] == label) return Integer(key["I"]);
        throw new ArgumentException("Unknown Animate symbol label: " + symbol + "/" + label);
    }

    public void PlaySymbol(string name, bool loop)
    {
        timeline = symbols[name];
        totalFrames = lengths[name];
        cache.Clear();
        NormalizeBounds(assetPath + ":" + name);
        PlayFrames(0, totalFrames, loop);
        if (!loop) SetFrame(0);
    }

    private void NormalizeBounds(string key)
    {
        if (!BoundsCache.TryGetValue(key, out Rect bounds))
        {
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var quads = new List<Quad>();
            for (int i = 0; i < totalFrames; i++)
            {
                quads.Clear();
                Flatten(timeline, i, Matrix4x4.identity, Color.white, quads, 0);
                foreach (Quad quad in quads)
                {
                    minimum = Vector2.Min(minimum, Vector2.Min(Vector2.Min(quad.a, quad.b), Vector2.Min(quad.c, quad.d)));
                    maximum = Vector2.Max(maximum, Vector2.Max(Vector2.Max(quad.a, quad.b), Vector2.Max(quad.c, quad.d)));
                }
            }
            bounds = float.IsInfinity(minimum.x) ? new Rect() : new Rect(minimum, maximum - minimum);
            BoundsCache[key] = bounds;
        }
        BoundsSize = bounds.size;
        stage = Matrix4x4.Translate(new Vector3(-bounds.xMin, bounds.yMax, 0));
    }

    public void SetSymbolTexture(string symbol, Texture2D texture)
    {
        if (sprites.Count != 1 || !symbols.ContainsKey(symbol)) throw new InvalidOperationException("Texture replacement requires a single-sprite atlas.");
        replacement = texture;
        SetMaterialDirty();
        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        lastUpdateTime = Time.realtimeSinceStartupAsDouble;
    }

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        float delta = (float)(now - lastUpdateTime);
        lastUpdateTime = now;
        Tick(delta);
    }

    public void Tick(float delta)
    {
        if (!playing || root == null || clipLength <= 0) return;
        clock += delta * frameRate;
        int next = Mathf.FloorToInt(clock);
        if (looping) { CompletedLoops = next / clipLength; next %= clipLength; }
        else if (next >= clipLength) { next = clipLength - 1; playing = false; Finished = true; }
        next += firstFrame;
        if (next == frame) return;
        frame = next;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        if (root == null || atlas == null) return;
        if (!cache.TryGetValue(frame, out List<Quad> quads))
        {
            quads = new List<Quad>(48);
            Flatten(timeline, frame, stage, Color.white, quads, 0);
            cache[frame] = quads;
        }
        foreach (Quad quad in quads)
        {
            Rect uv = replacement != null ? new Rect(0, 0, 1, 1) : quad.uv;
            Vector2 tl = new Vector2(uv.xMin, uv.yMax), tr = new Vector2(uv.xMax, uv.yMax);
            Vector2 br = new Vector2(uv.xMax, uv.yMin), bl = new Vector2(uv.xMin, uv.yMin);
            Color tint = quad.tint * color;
            int index = vertices.currentVertCount;
            Add(vertices, quad.a, quad.rotated ? tr : tl, tint);
            Add(vertices, quad.b, quad.rotated ? br : tr, tint);
            Add(vertices, quad.c, quad.rotated ? bl : br, tint);
            Add(vertices, quad.d, quad.rotated ? tl : bl, tint);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index + 2, index + 3, index);
        }
    }

    private void Flatten(JToken timeline, int time, Matrix4x4 transform, Color tint, List<Quad> output, int depth)
    {
        if (depth > 64) return;
        JArray layers = timeline?["L"] as JArray;
        if (layers == null) return;
        for (int l = layers.Count - 1; l >= 0; l--)
        {
            JToken key = FindFrame(layers[l]["FR"] as JArray, time);
            if (key == null || key["E"] == null) continue;
            int elapsed = time - Integer(key["I"]);
            foreach (JToken element in key["E"])
            {
                JToken instance = element["SI"];
                if (instance != null)
                {
                    string name = (string)instance["SN"];
                    if (name == null || !symbols.TryGetValue(name, out JToken child)) continue;
                    int length = lengths[name];
                    int first = Integer(instance["FF"]);
                    string mode = (string)instance["LP"] ?? "LP";
                    int childFrame = first + (mode == "SF" ? 0 : elapsed);
                    childFrame = mode == "LP" ? ((childFrame % length) + length) % length : Mathf.Clamp(childFrame, 0, length - 1);
                    Flatten(child, childFrame, transform * Matrix(instance), tint * Shade(instance["C"]), output, depth + 1);
                    continue;
                }
                instance = element["ASI"];
                if (instance == null) continue;
                string spriteName = (string)instance["N"];
                if (spriteName == null || !sprites.TryGetValue(spriteName, out JToken sprite)) continue;
                Matrix4x4 matrix = transform * Matrix(instance);
                bool rotated = (bool?)sprite["rotated"] ?? false;
                float width = Number(sprite["w"]), height = Number(sprite["h"]);
                float localWidth = rotated ? height : width, localHeight = rotated ? width : height;
                output.Add(new Quad
                {
                    a = Point(matrix, 0, 0), b = Point(matrix, localWidth, 0), c = Point(matrix, localWidth, localHeight), d = Point(matrix, 0, localHeight),
                    uv = new Rect(Number(sprite["x"]) / atlas.width, 1 - (Number(sprite["y"]) + height) / atlas.height, width / atlas.width, height / atlas.height),
                    tint = tint * Shade(instance["C"]), rotated = rotated
                });
            }
        }
    }

    private static JToken FindFrame(JArray frames, int time)
    {
        if (frames == null) return null;
        for (int i = frames.Count - 1; i >= 0; i--)
        {
            int start = Integer(frames[i]["I"]);
            if (time >= start && time < start + Integer(frames[i]["DU"], 1)) return frames[i];
        }
        return null;
    }

    private static int TimelineLength(JToken timeline)
    {
        int length = 1;
        if (timeline?["L"] == null) return length;
        foreach (JToken layer in timeline["L"])
            foreach (JToken key in layer["FR"]) length = Math.Max(length, Integer(key["I"]) + Integer(key["DU"], 1));
        return length;
    }

    private static Matrix4x4 Matrix(JToken instance)
    {
        Matrix4x4 result = Matrix4x4.identity;
        JArray values = instance?["MX"] as JArray;
        if (values != null && values.Count >= 6)
        {
            result.m00 = Number(values[0]); result.m10 = Number(values[1]);
            result.m01 = Number(values[2]); result.m11 = Number(values[3]);
            result.m03 = Number(values[4]); result.m13 = Number(values[5]);
        }
        else if (instance?["M3D"] is JArray matrix && matrix.Count >= 16)
            for (int i = 0; i < 16; i++) result[i % 4, i / 4] = Number(matrix[i]);
        return result;
    }

    private static Color Shade(JToken value)
    {
        if (value == null) return Color.white;
        if ((string)value["M"] == "CBRT")
        {
            float brightness = 1 + Mathf.Min(0, Number(value["BRT"]));
            return new Color(brightness, brightness, brightness, 1);
        }
        return new Color(Number(value["RM"], 1), Number(value["GM"], 1), Number(value["BM"], 1), Number(value["AM"], 1));
    }

    private static Vector2 Point(Matrix4x4 matrix, float x, float y)
    {
        Vector3 point = matrix.MultiplyPoint3x4(new Vector3(x, y));
        return new Vector2(point.x, -point.y);
    }

    private static void Add(VertexHelper vertices, Vector2 point, Vector2 uv, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = point; vertex.color = tint; vertex.uv0 = uv;
        vertices.AddVert(vertex);
    }

    private static int Integer(JToken value, int fallback = 0) => value == null || value.Type == JTokenType.Null ? fallback : (int)value;
    private static float Number(JToken value, float fallback = 0) => value == null || value.Type == JTokenType.Null ? fallback : (float)value;
}
