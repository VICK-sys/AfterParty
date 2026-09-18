using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaPauseText : MaskableGraphic
{
    private sealed class Frame
    {
        public Rect pixels;
        public Vector2 trim;
        public Vector2 full;
    }

    [Serializable]
    private sealed class Glyph
    {
        public int code;
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [Serializable]
    private sealed class BitmapFont
    {
        public float advance;
        public float lineHeight;
        public Glyph[] glyphs;
    }

    private static readonly Dictionary<string, string> Symbols = new Dictionary<string, string>
    {
        { "&", "-andpersand-" }, { "'", "-apostraphie-" }, { "\\", "-back slash-" },
        { ",", "-comma-" }, { "-", "-dash-" }, { "!", "-exclamation point-" },
        { "/", "-forward slash-" }, { ">", "-greater than-" }, { "<", "-less than-" },
        { "*", "-multiply x-" }, { ".", "-period-" }, { "?", "-question mark-" }
    };
    private static Dictionary<string, Frame[]> boldFrames;
    private static float maxHeight;
    private Texture2D texture;
    private BitmapFont font;
    private string value = "";
    private bool bold;
    private float age;
    private int frameIndex;
    public bool rightAligned;
    public override Texture mainTexture => texture;
    public string Text => value;

    public void Initialize(bool animated, int size = 32)
    {
        bold = animated;
        raycastTarget = false;
        texture = Resources.Load<Texture2D>("FunkinPause/" + (bold ? "bold" : "vcr" + size));
        if (texture == null) throw new InvalidOperationException("Missing pause texture: " + (bold ? "bold" : "vcr" + size));
        if (!bold)
            font = JsonConvert.DeserializeObject<BitmapFont>(Resources.Load<TextAsset>("FunkinPause/vcr" + size).text);
        else if (boldFrames == null)
        {
            var entries = XDocument.Parse(Resources.Load<TextAsset>("FunkinPause/bold").text).Root.Elements("SubTexture");
            maxHeight = entries.Max(entry => (float)entry.Attribute("height"));
            boldFrames = entries.OrderBy(entry => (string)entry.Attribute("name"), StringComparer.Ordinal)
                .GroupBy(entry => ((string)entry.Attribute("name")).Substring(0, ((string)entry.Attribute("name")).Length - 4))
                .ToDictionary(group => group.Key, group => group.Select(entry => new Frame
                {
                    pixels = new Rect((float)entry.Attribute("x"), (float)entry.Attribute("y"),
                        (float)entry.Attribute("width"), (float)entry.Attribute("height")),
                    trim = new Vector2(-((float?)entry.Attribute("frameX") ?? 0), -((float?)entry.Attribute("frameY") ?? 0)),
                    full = new Vector2((float?)entry.Attribute("frameWidth") ?? (float)entry.Attribute("width"),
                        (float?)entry.Attribute("frameHeight") ?? (float)entry.Attribute("height"))
                }).ToArray());
        }
        SetAllDirty();
    }

    public void SetText(string text)
    {
        string next = bold ? (text ?? "").ToUpperInvariant() : text ?? "";
        if (value == next) return;
        value = next;
        SetVerticesDirty();
    }

    public void Tick(float delta)
    {
        if (!bold) return;
        age += delta;
        int next = (int)(age * 24);
        if (next == frameIndex) return;
        frameIndex = next;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (texture == null) return;
        float x = 0;
        float y = 0;
        if (bold)
        {
            foreach (char character in value)
            {
                if (character == ' ') { x += 40; continue; }
                if (character == '\n') { x = 0; y += maxHeight; continue; }
                string prefix = character.ToString();
                if (Symbols.TryGetValue(prefix, out string symbol)) prefix = symbol;
                if (!boldFrames.TryGetValue(prefix, out Frame[] frames)) continue;
                Frame frame = frames[frameIndex % frames.Length];
                Quad(mesh, x + frame.trim.x, y + maxHeight - frames[0].full.y + frame.trim.y, frame.pixels);
                x += frames[0].full.x;
            }
            return;
        }
        foreach (string line in value.Split('\n'))
        {
            x = rightAligned ? Mathf.Floor(rectTransform.sizeDelta.x - line.Length * font.advance - 4 + 0.5f) : 0;
            for (int index = 0; index < line.Length; index++)
            {
                Glyph glyph = font.glyphs[Mathf.Clamp(line[index] - 32, 0, font.glyphs.Length - 1)];
                Quad(mesh, x + Mathf.Floor(index * font.advance), y, new Rect(glyph.x, glyph.y, glyph.width, glyph.height));
            }
            y += font.lineHeight;
        }
    }

    private void Quad(VertexHelper mesh, float x, float y, Rect pixels)
    {
        int first = mesh.currentVertCount;
        float u0 = pixels.x / texture.width;
        float u1 = pixels.xMax / texture.width;
        float v0 = 1 - pixels.y / texture.height;
        float v1 = 1 - pixels.yMax / texture.height;
        mesh.AddVert(new Vector3(x, -y), color, new Vector2(u0, v0));
        mesh.AddVert(new Vector3(x + pixels.width, -y), color, new Vector2(u1, v0));
        mesh.AddVert(new Vector3(x + pixels.width, -y - pixels.height), color, new Vector2(u1, v1));
        mesh.AddVert(new Vector3(x, -y - pixels.height), color, new Vector2(u0, v1));
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first, first + 2, first + 3);
    }
}
