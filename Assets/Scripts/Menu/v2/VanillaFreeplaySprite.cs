using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaFreeplaySprite : MaskableGraphic
{
    public sealed class Frame
    {
        public string name;
        public Rect uv;
        public Vector2 size;
        public Vector2 trim;
        public Vector2 full;
        public bool rotated;
    }

    private static readonly Dictionary<string, Frame[]> Cache = new Dictionary<string, Frame[]>();
    private Texture2D atlas;
    private string assetPath;
    private string resourceRoot = "VanillaFreeplay";
    private string nextPrefix;
    private Frame[] frames;
    private float clock;
    private double lastUpdateTime;
    private int index;
    private bool frozen;
    private bool reversed;
    public float fps = 24;
    public bool loop = true;
    public float drawScale = 1;
    public bool flipX;
    public bool centerScale = true;
    public float leftSlant;
    public float angle;
    public Vector2 stretch = Vector2.one;
    public override Texture mainTexture => atlas;
    public Vector2 FrameSize => frames == null || frames.Length == 0 ? Vector2.zero : frames[0].full;
    public int FrameCount => frames?.Length ?? 0;
    public int FrameIndex => index;
    public string CurrentFrameName => frames == null || frames.Length == 0 ? null : frames[index].name;

    public void Load(string path, string prefix = "", bool repeat = true, string root = "VanillaFreeplay")
    {
        assetPath = path;
        resourceRoot = root;
        nextPrefix = null;
        frozen = false;
        reversed = false;
        atlas = Resources.Load<Texture2D>(resourceRoot + "/" + path);
        if (atlas == null) throw new InvalidOperationException("Missing Freeplay texture: " + path);
        string key = resourceRoot + "/" + path + ":" + prefix;
        if (!Cache.TryGetValue(key, out frames))
        {
            TextAsset xml = Resources.Load<TextAsset>(resourceRoot + "/" + path);
            frames = xml == null
                ? new[] { new Frame { name = path, uv = new Rect(0, 0, 1, 1), size = new Vector2(atlas.width, atlas.height), full = new Vector2(atlas.width, atlas.height) } }
                : XDocument.Parse(xml.text).Root.Elements("SubTexture")
                    .Where(e => ((string)e.Attribute("name")).StartsWith(prefix, StringComparison.Ordinal))
                    .OrderBy(e => (string)e.Attribute("name"), StringComparer.Ordinal).Select(e =>
                    {
                        float w = (float)e.Attribute("width"), h = (float)e.Attribute("height");
                        bool rotated = (bool?)e.Attribute("rotated") ?? false;
                        return new Frame
                        {
                            name = (string)e.Attribute("name"),
                            uv = new Rect((float)e.Attribute("x") / atlas.width, 1 - ((float)e.Attribute("y") + h) / atlas.height, w / atlas.width, h / atlas.height),
                            rotated = rotated,
                            size = rotated ? new Vector2(h, w) : new Vector2(w, h),
                            full = new Vector2((float?)e.Attribute("frameWidth") ?? (rotated ? h : w), (float?)e.Attribute("frameHeight") ?? (rotated ? w : h)),
                            trim = new Vector2(-((float?)e.Attribute("frameX") ?? 0), -((float?)e.Attribute("frameY") ?? 0))
                        };
                    }).ToArray();
            if (frames.Length == 0) throw new InvalidOperationException("Missing Freeplay animation: " + key);
            Cache[key] = frames;
        }
        clock = 0;
        lastUpdateTime = Time.realtimeSinceStartupAsDouble;
        index = 0;
        loop = repeat;
        raycastTarget = false;
        rectTransform.sizeDelta = FrameSize;
        SetAllDirty();
    }

    public bool TryPlay(string prefix, bool repeat = true, string then = null)
    {
        TextAsset xml = Resources.Load<TextAsset>(resourceRoot + "/" + assetPath);
        if (xml == null || !XDocument.Parse(xml.text).Root.Elements("SubTexture")
            .Any(e => ((string)e.Attribute("name")).StartsWith(prefix, StringComparison.Ordinal))) return false;
        Load(assetPath, prefix, repeat, resourceRoot);
        nextPrefix = then;
        return true;
    }

    public void Tick(float delta)
    {
        if (frozen || frames == null || frames.Length == 0) return;
        clock += delta;
        if (!loop && nextPrefix != null && clock * fps >= frames.Length)
        {
            string nextAnimation = nextPrefix;
            nextPrefix = null;
            float remainder = clock - frames.Length / fps;
            if (TryPlay(nextAnimation)) { Tick(remainder); return; }
        }
        int next = loop ? (int)(clock * fps) % frames.Length : Mathf.Min((int)(clock * fps), frames.Length - 1);
        if (reversed) next = frames.Length - 1 - next;
        if (index == next) return;
        index = next;
        SetVerticesDirty();
    }

    public void PlayReverse(string prefix, string then)
    {
        if (!TryPlay(prefix, false, then)) return;
        reversed = true;
        index = frames.Length - 1;
        SetVerticesDirty();
    }

    public void FreezeFrame(int frame)
    {
        index = Mathf.Clamp(frame, 0, frames.Length - 1);
        frozen = true;
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
        Tick(VanillaMenuTiming.Clamp((float)(now - lastUpdateTime)));
        lastUpdateTime = now;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (frames == null || frames.Length == 0) return;
        Frame frame = frames[index];
        Vector2 scale = stretch * drawScale;
        Vector2 offset = centerScale ? Vector2.Scale(frame.full, Vector2.one - scale) * 0.5f : Vector2.zero;
        float x = offset.x + frame.trim.x * scale.x;
        float y = -offset.y - frame.trim.y * scale.y;
        float width = frame.size.x * scale.x, height = frame.size.y * scale.y;
        Vector2 Uv(float horizontal, float vertical)
        {
            if (flipX) horizontal = 1 - horizontal;
            return new Vector2(Mathf.Lerp(frame.uv.xMin, frame.uv.xMax, frame.rotated ? vertical : horizontal),
                Mathf.Lerp(frame.uv.yMin, frame.uv.yMax, frame.rotated ? 1 - horizontal : vertical));
        }
        vh.AddVert(new Vector3(x, y), color, Uv(0, 1));
        vh.AddVert(new Vector3(x + width, y), color, Uv(1, 1));
        vh.AddVert(new Vector3(x + width, y - height), color, Uv(1, 0));
        vh.AddVert(new Vector3(x + leftSlant, y - height), color, Uv(leftSlant / width, 0));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
        if (angle != 0)
        {
            Vector3 center = new Vector3(frame.full.x / 2, -frame.full.y / 2);
            Quaternion rotation = Quaternion.Euler(0, 0, -angle);
            UIVertex vertex = default;
            for (int i = 0; i < 4; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.position = center + rotation * (vertex.position - center);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
