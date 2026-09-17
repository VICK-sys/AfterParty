using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaStorySprite : MaskableGraphic
{
    private sealed class Frame
    {
        public string name;
        public Rect uv;
        public Vector2 size;
        public Vector2 trim;
        public Vector2 full;
        public bool rotated;
    }

    private static readonly Dictionary<string, Frame[]> Atlases = new Dictionary<string, Frame[]>();
    private Texture2D atlas;
    private Frame[] frames;
    private float clock;
    private int index;
    public float fps = 24;
    public bool loop;
    public bool paused;
    public bool flipX;
    public bool flipY;
    public float scale = 1;
    public Vector2 animationOffset;
    public string AssetPath { get; private set; }
    public Vector2 FrameSize => frames == null ? Vector2.zero : frames[0].full;
    public int FrameCount => frames?.Length ?? 0;
    public string FrameName => frames == null ? null : frames[index].name;
    public bool Finished => !loop && clock * fps >= FrameCount;
    public override Texture mainTexture => atlas;

    public void Load(string path, string prefix = "", int[] indices = null)
    {
        AssetPath = path;
        atlas = Resources.Load<Texture2D>("VanillaStory/" + path);
        if (atlas == null) throw new InvalidOperationException("Missing Story Mode texture: " + path);
        if (!Atlases.TryGetValue(path, out Frame[] all))
        {
            TextAsset xml = Resources.Load<TextAsset>("VanillaStory/" + path);
            all = xml == null
                ? new[] { new Frame { name = path, uv = new Rect(0, 0, 1, 1), size = new Vector2(atlas.width, atlas.height), full = new Vector2(atlas.width, atlas.height) } }
                : XDocument.Parse(xml.text).Root.Elements("SubTexture").OrderBy(e => (string)e.Attribute("name"), StringComparer.Ordinal).Select(e =>
                {
                    float w = (float)e.Attribute("width"), h = (float)e.Attribute("height");
                    bool rotated = (bool?)e.Attribute("rotated") ?? false;
                    return new Frame
                    {
                        name = (string)e.Attribute("name"),
                        uv = new Rect((float)e.Attribute("x") / atlas.width, 1 - ((float)e.Attribute("y") + h) / atlas.height, w / atlas.width, h / atlas.height),
                        size = rotated ? new Vector2(h, w) : new Vector2(w, h),
                        full = new Vector2((float?)e.Attribute("frameWidth") ?? (rotated ? h : w), (float?)e.Attribute("frameHeight") ?? (rotated ? w : h)),
                        trim = new Vector2(-((float?)e.Attribute("frameX") ?? 0), -((float?)e.Attribute("frameY") ?? 0)),
                        rotated = rotated
                    };
                }).ToArray();
            Atlases[path] = all;
        }
        frames = all.Where(f => f.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (indices != null && indices.Length > 0)
            frames = indices.Select(i => frames.FirstOrDefault(frame => int.TryParse(frame.name.Substring(prefix.Length), out int number) && number == i))
                .Where(frame => frame != null).ToArray();
        if (frames.Length == 0) throw new InvalidOperationException("Missing Story Mode animation: " + path + ":" + prefix);
        clock = 0;
        index = 0;
        paused = false;
        raycastTarget = false;
        rectTransform.sizeDelta = FrameSize;
        SetAllDirty();
    }

    public void Tick(float delta)
    {
        if (paused || FrameCount == 0) return;
        clock += delta;
        int next = loop ? (int)(clock * fps) % FrameCount : Mathf.Min((int)(clock * fps), FrameCount - 1);
        if (next == index) return;
        index = next;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (FrameCount == 0) return;
        Frame frame = frames[index];
        Vector2 trim = frame.trim;
        if (flipX) trim.x = frame.full.x - trim.x - frame.size.x;
        if (flipY) trim.y = frame.full.y - trim.y - frame.size.y;
        Vector2 origin = FrameSize * (1 - scale) * 0.5f + (trim - animationOffset) * scale;
        Vector2 size = frame.size * scale;
        for (int corner = 0; corner < 4; corner++)
        {
            bool right = corner == 1 || corner == 2;
            bool bottom = corner >= 2;
            float u = right ^ flipX ? 1 : 0;
            float v = bottom ^ flipY ? 1 : 0;
            Vector2 uv = frame.rotated
                ? new Vector2(Mathf.Lerp(frame.uv.xMax, frame.uv.xMin, v), Mathf.Lerp(frame.uv.yMax, frame.uv.yMin, u))
                : new Vector2(Mathf.Lerp(frame.uv.xMin, frame.uv.xMax, u), Mathf.Lerp(frame.uv.yMax, frame.uv.yMin, v));
            mesh.AddVert(new Vector3(origin.x + (right ? size.x : 0), -origin.y - (bottom ? size.y : 0)), color, uv);
        }
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(0, 2, 3);
    }
}
