using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaFreeplayAnimate : MaskableGraphic
{
    private sealed class AssetData
    {
        public JObject animation, sprites;
        public Texture2D texture;
    }
    private static readonly Dictionary<string, AssetData> Assets = new Dictionary<string, AssetData>();
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
    private readonly Dictionary<string, int[]> layerFrames = new Dictionary<string, int[]>();
    private readonly Dictionary<JToken, Matrix4x4> elementMatrices = new Dictionary<JToken, Matrix4x4>();
    private Matrix4x4 stage = Matrix4x4.identity;
    private int totalFrames, firstFrame, clipLength, frame;
    private float frameRate = 24, clock;
    private double lastUpdateTime;
    private bool playing, looping;
    private bool sourceMovieClips;
    public bool Finished { get; private set; }
    public int CurrentFrame => frame;
    public int TotalFrames => totalFrames;
    public float FrameRate => frameRate;
    public string CurrentLabel { get; private set; }
    public int LabelFrame => CurrentLabel != null ? frame - clips[CurrentLabel].start : frame;
    public int CompletedLoops { get; private set; }
    public Vector2 BoundsSize { get; private set; }
    public Rect CurrentBounds
    {
        get
        {
            var quads = new List<Quad>();
            Flatten(timeline, frame, stage, Color.white, quads, 0);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var quad in quads)
            {
                minimum = Vector2.Min(minimum, Vector2.Min(Vector2.Min(quad.a, quad.b), Vector2.Min(quad.c, quad.d)));
                maximum = Vector2.Max(maximum, Vector2.Max(Vector2.Max(quad.a, quad.b), Vector2.Max(quad.c, quad.d)));
            }
            return quads.Count == 0 ? new Rect() : new Rect(minimum, maximum - minimum);
        }
    }
    public bool HasLabel(string name) => clips.ContainsKey(name);
    public int LabelStart(string name) => clips[name].start;
    public float GetLabelDuration(string name) => clips[name].length / frameRate;
    public void PlayAll(bool loop) => PlayFrames(0, totalFrames, loop);
    public override Texture mainTexture => replacement != null ? replacement : atlas != null ? atlas : Texture2D.whiteTexture;

    public static IEnumerator ReleaseCachedAssets()
    {
        Assets.Clear();
        BoundsCache.Clear();
        yield return null;
        yield return Resources.UnloadUnusedAssets();
        GC.Collect();
    }

    public static IEnumerator Preload(string path)
    {
        string prefix = "VanillaFreeplay/" + path.Trim('/');
        if (Assets.ContainsKey(prefix)) yield break;
        var animation = Resources.LoadAsync<TextAsset>(prefix + "/Animation");
        var sprites = Resources.LoadAsync<TextAsset>(prefix + "/spritemap1");
        var texture = Resources.LoadAsync<Texture2D>(prefix + "/spritemap1");
        yield return animation;
        yield return sprites;
        yield return texture;
        if (animation.asset == null || sprites.asset == null || texture.asset == null) throw new InvalidOperationException("Missing Animate assets: " + prefix);
        string animationText = ((TextAsset)animation.asset).text;
        string spriteText = ((TextAsset)sprites.asset).text;
        var parsed = Task.Run(() => new AssetData { animation = JObject.Parse(animationText.TrimStart('\uFEFF')), sprites = JObject.Parse(spriteText.TrimStart('\uFEFF')) });
        while (!parsed.IsCompleted) yield return null;
        var data = parsed.GetAwaiter().GetResult();
        data.texture = (Texture2D)texture.asset;
        if (!Assets.ContainsKey(prefix)) Assets.Add(prefix, data);
    }

    public void Initialize(string path, bool useStagePosition = true, string resourceRoot = "VanillaFreeplay")
    {
        string prefix = resourceRoot + "/" + path.Trim('/');
        assetPath = prefix;
        if (!Assets.TryGetValue(prefix, out var loaded))
        {
            TextAsset animation = Resources.Load<TextAsset>(prefix + "/Animation");
            TextAsset spriteData = Resources.Load<TextAsset>(prefix + "/spritemap1");
            var texture = Resources.Load<Texture2D>(prefix + "/spritemap1");
            if (animation == null || spriteData == null || texture == null) throw new InvalidOperationException("Missing Animate assets: " + prefix);
            loaded = new AssetData { animation = JObject.Parse(animation.text.TrimStart('\uFEFF')), sprites = JObject.Parse(spriteData.text.TrimStart('\uFEFF')), texture = texture };
            Assets.Add(prefix, loaded);
        }
        atlas = loaded.texture;
        JObject data = loaded.animation;
        JObject map = loaded.sprites;
        root = data["AN"];
        if (root == null) throw new InvalidOperationException("Expected compressed Animate JSON: " + path);
        symbols.Clear();
        sprites.Clear();
        lengths.Clear();
        clips.Clear();
        cache.Clear();
        layerFrames.Clear();
        elementMatrices.Clear();
        replacement = null;
        sourceMovieClips = false;
        foreach (JToken symbol in data["SD"]?["S"] ?? new JArray())
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

    public void SetLayerFrames(string layer, int[] frames)
    {
        layerFrames[layer] = frames;
        cache.Clear();
        SetVerticesDirty();
    }

    public void ScaleSymbolElement(string symbol, float scale, float positionOffset)
    {
        if (!symbols.TryGetValue(symbol, out JToken child)) throw new ArgumentException("Unknown Animate symbol: " + symbol);
        JToken element = null;
        foreach (JToken layer in child["L"])
        {
            JToken first = FindFrame(layer["FR"] as JArray, 0)?["E"]?.First;
            if (first == null) continue;
            element = first["SI"] ?? first["ASI"];
            if (element != null) break;
        }
        if (element == null) throw new ArgumentException("Empty Animate symbol: " + symbol);
        Matrix4x4 matrix = Matrix(element);
        matrix.m00 += scale;
        matrix.m11 += scale;
        matrix.m03 -= positionOffset;
        matrix.m13 -= positionOffset;
        elementMatrices[element] = matrix;
        cache.Clear();
        SetVerticesDirty();
    }

    private Matrix4x4 ElementMatrix(JToken instance) => elementMatrices.TryGetValue(instance, out Matrix4x4 matrix) ? matrix : Matrix(instance);

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

    public void UseTimelineBounds()
    {
        sourceMovieClips = true;
        string key = assetPath + ":timeline-bounds";
        if (!BoundsCache.TryGetValue(key, out Rect bounds))
        {
            var frames = new Dictionary<(JToken, int), Rect>();
            bool found = false;
            bounds = new Rect();
            for (int i = 0; i < totalFrames; i++)
            {
                Rect frameBounds = TimelineBounds(timeline, i, frames, 0);
                if (frameBounds.width <= 0 || frameBounds.height <= 0) continue;
                bounds = found ? Union(bounds, frameBounds) : frameBounds;
                found = true;
            }
            BoundsCache[key] = bounds;
        }
        if (root["RB"]?["bounds"] is JArray bakedBounds)
            bounds = new Rect(Number(bakedBounds[0]), Number(bakedBounds[1]), Number(bakedBounds[2]), Number(bakedBounds[3]));
        BoundsSize = new Vector2(Mathf.Floor(bounds.width), Mathf.Floor(bounds.height));
        stage = Matrix4x4.Translate(new Vector3(-bounds.xMin, -bounds.yMin, 0));
        cache.Clear();
        SetVerticesDirty();
    }

    private Rect TimelineBounds(JToken source, int time, Dictionary<(JToken, int), Rect> frames, int depth)
    {
        if (depth > 64) return new Rect();
        if (frames.TryGetValue((source, time), out Rect cached)) return cached;
        bool found = false;
        Rect bounds = new Rect();
        foreach (JToken layer in source["L"])
        {
            JToken key = FindFrame(layer["FR"] as JArray, time);
            if (key?["E"] == null) continue;
            int elapsed = time - Integer(key["I"]);
            foreach (JToken element in key["E"])
            {
                JToken instance = element["SI"];
                Rect childBounds;
                if (instance != null)
                {
                    if (instance["RF"]?["bounds"] is JArray filterBounds)
                    {
                        var filtered = new Rect(Number(filterBounds[0]), Number(filterBounds[1]), Number(filterBounds[2]), Number(filterBounds[3]));
                        bounds = found ? Union(bounds, filtered) : filtered;
                        found = true;
                        continue;
                    }
                    string name = (string)instance["SN"];
                    if (name == null || !symbols.TryGetValue(name, out JToken child)) continue;
                    int length = lengths[name];
                    string mode = (string)instance["LP"] ?? "LP";
                    int childFrame = Integer(instance["FF"]) + (mode == "SF" ? 0 : elapsed);
                    childFrame = mode == "LP" ? (childFrame % length + length) % length : Mathf.Clamp(childFrame, 0, length - 1);
                    if ((string)instance["ST"] == "MC") childFrame = 0;
                    childBounds = TimelineBounds(child, childFrame, frames, depth + 1);
                }
                else
                {
                    instance = element["ASI"];
                    if (instance == null || !sprites.TryGetValue((string)instance["N"], out JToken sprite)) continue;
                    bool rotated = (bool?)sprite["rotated"] ?? false;
                    childBounds = new Rect(0, 0, Number(sprite[rotated ? "h" : "w"]), Number(sprite[rotated ? "w" : "h"]));
                }
                if (childBounds.width <= 0 || childBounds.height <= 0) continue;
                Matrix4x4 matrix = Matrix(instance);
                Vector2 a = matrix.MultiplyPoint3x4(new Vector3(childBounds.xMin, childBounds.yMin));
                Vector2 b = matrix.MultiplyPoint3x4(new Vector3(childBounds.xMax, childBounds.yMin));
                Vector2 c = matrix.MultiplyPoint3x4(new Vector3(childBounds.xMax, childBounds.yMax));
                Vector2 d = matrix.MultiplyPoint3x4(new Vector3(childBounds.xMin, childBounds.yMax));
                Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
                Vector2 max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
                var transformed = new Rect(min, max - min);
                bounds = found ? Union(bounds, transformed) : transformed;
                found = true;
            }
        }
        frames[(source, time)] = bounds;
        return bounds;
    }

    private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

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
        float delta = VanillaMenuTiming.Clamp((float)(now - lastUpdateTime));
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
        if (depth == 0 && timeline == root["TL"] && root["RB"] is JObject baked)
        {
            JArray bounds = (JArray)baked["bounds"];
            JToken offset = baked["offsets"]?[time];
            float scale = 1 / Number(baked["renderScale"], 1);
            AddSpriteQuad((string)baked["frames"][time], transform * Matrix4x4.Translate(new Vector3(Number(bounds[0]) + Number(offset?[0]), Number(bounds[1]) + Number(offset?[1]), 0))
                * Matrix4x4.Scale(new Vector3(scale, scale, 1)), tint, output);
            return;
        }
        JArray layers = timeline?["L"] as JArray;
        if (layers == null) return;
        for (int l = layers.Count - 1; l >= 0; l--)
        {
            if (layers[l]["RB"] is JObject renderedLayer)
            {
                JToken offset = renderedLayer["offsets"][time];
                float scale = 1 / Number(renderedLayer["renderScale"], 1);
                AddSpriteQuad((string)renderedLayer["frames"][time], transform * Matrix4x4.Translate(new Vector3(Number(offset[0]), Number(offset[1]), 0))
                    * Matrix4x4.Scale(new Vector3(scale, scale, 1)), tint, output);
                continue;
            }
            JToken key = FindFrame(layers[l]["FR"] as JArray, time);
            if (key == null || key["E"] == null) continue;
            int elapsed = time - Integer(key["I"]);
            int elementIndex = -1;
            foreach (JToken element in key["E"])
            {
                elementIndex++;
                JToken instance = element["SI"];
                if (instance != null)
                {
                    if (instance["RF"] is JObject filtered)
                    {
                        AddSpriteQuad((string)filtered["N"], transform * Matrix(filtered), tint * Shade(instance["C"]), output);
                        continue;
                    }
                    string name = (string)instance["SN"];
                    if (name == null || !symbols.TryGetValue(name, out JToken child)) continue;
                    int length = lengths[name];
                    int first = Integer(instance["FF"]);
                    string mode = (string)instance["LP"] ?? "LP";
                    int childFrame = first + (mode == "SF" ? 0 : elapsed);
                    childFrame = mode == "LP" ? ((childFrame % length) + length) % length : Mathf.Clamp(childFrame, 0, length - 1);
                    if (sourceMovieClips && (string)instance["ST"] == "MC") childFrame = 0;
                    if (layerFrames.TryGetValue((string)layers[l]["LN"] ?? "", out int[] overrides) && elementIndex < overrides.Length)
                        childFrame = Mathf.Clamp(overrides[elementIndex], 0, length - 1);
                    Matrix4x4 local = ElementMatrix(instance);
                    Flatten(child, childFrame, transform * local, tint * Shade(instance["C"]), output, depth + 1);
                    continue;
                }
                instance = element["ASI"];
                if (instance == null) continue;
                AddSpriteQuad((string)instance["N"], transform * ElementMatrix(instance), tint * Shade(instance["C"]), output);
            }
        }
    }

    private void AddSpriteQuad(string name, Matrix4x4 matrix, Color tint, List<Quad> output)
    {
        if (name == null || !sprites.TryGetValue(name, out JToken sprite)) return;
        bool rotated = (bool?)sprite["rotated"] ?? false;
        float width = Number(sprite["w"]), height = Number(sprite["h"]);
        float localWidth = rotated ? height : width, localHeight = rotated ? width : height;
        output.Add(new Quad
        {
            a = Point(matrix, 0, 0), b = Point(matrix, localWidth, 0), c = Point(matrix, localWidth, localHeight), d = Point(matrix, 0, localHeight),
            uv = new Rect(Number(sprite["x"]) / atlas.width, 1 - (Number(sprite["y"]) + height) / atlas.height, width / atlas.width, height / atlas.height),
            tint = tint, rotated = rotated
        });
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
