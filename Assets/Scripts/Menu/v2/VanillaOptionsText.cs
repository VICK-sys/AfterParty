using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaOptionsText : MaskableGraphic
{
    private sealed class Frame
    {
        public Rect uv;
        public Vector2 size, trim, full;
    }

    private sealed class FontData
    {
        public Texture2D texture;
        public Dictionary<string, Frame[]> glyphs;
        public float height;
    }

    private static readonly Dictionary<string, FontData> Fonts = new Dictionary<string, FontData>();
    private static readonly Dictionary<char, string> Symbols = new Dictionary<char, string>
    {
        {'&', "-andpersand-"}, {'\'', "-apostraphie-"}, {'\\', "-back slash-"},
        {',', "-comma-"}, {'-', "-dash-"}, {'!', "-exclamation point-"},
        {'/', "-forward slash-"}, {'>', "-greater than-"}, {'<', "-less than-"},
        {'*', "-multiply x-"}, {'.', "-period-"}, {'?', "-question mark-"}
    };
    private FontData font;
    private bool bold;
    private string value = "";
    private float age;
    public override Texture mainTexture => font?.texture;
    public float TextWidth { get; private set; }
    public float TextHeight { get; private set; }
    public string Text => value;

    public void Initialize(string text, bool isBold = true)
    {
        bold = isBold;
        string id = bold ? "bold" : "default";
        if (!Fonts.TryGetValue(id, out font))
        {
            var texture = Resources.Load<Texture2D>("VanillaOptions/" + id);
            string xml = Resources.Load<TextAsset>("VanillaOptions/" + id).text.TrimStart('\uFEFF');
            xml = Regex.Replace(xml, "name=\"([^\"]*)\"", match => "name=\"" + System.Security.SecurityElement.Escape(match.Groups[1].Value) + "\"");
            var entries = XDocument.Parse(xml).Root.Elements("SubTexture").ToArray();
            font = new FontData
            {
                texture = texture,
                height = entries.Max(e => (float)e.Attribute("height")),
                glyphs = entries.OrderBy(e => (string)e.Attribute("name"), StringComparer.Ordinal)
                    .GroupBy(e => Regex.Replace((string)e.Attribute("name"), "[0-9]{4}$", ""))
                    .ToDictionary(g => g.Key, g => g.Select(e => new Frame
                    {
                        uv = new Rect((float)e.Attribute("x") / texture.width, 1-((float)e.Attribute("y")+(float)e.Attribute("height"))/texture.height,
                            (float)e.Attribute("width")/texture.width, (float)e.Attribute("height")/texture.height),
                        size = new Vector2((float)e.Attribute("width"), (float)e.Attribute("height")),
                        trim = new Vector2(-(float?)e.Attribute("frameX") ?? 0, -(float?)e.Attribute("frameY") ?? 0),
                        full = new Vector2((float?)e.Attribute("frameWidth") ?? (float)e.Attribute("width"), (float?)e.Attribute("frameHeight") ?? (float)e.Attribute("height"))
                    }).ToArray())
            };
            Fonts.Add(id, font);
        }
        raycastTarget = false;
        SetText(text);
    }

    private Frame[] Glyph(char c)
    {
        string key = Symbols.TryGetValue(c, out string symbol) ? symbol : c.ToString();
        return font.glyphs.TryGetValue(key, out Frame[] frames) ? frames : null;
    }

    public void SetText(string text)
    {
        string next = bold ? (text ?? "").ToUpperInvariant() : text ?? "";
        if (value == next && TextHeight != 0) return;
        value = next;
        TextWidth = 0;
        float width = 0;
        TextHeight = font.height;
        foreach (char c in value)
        {
            if (c == '\n') { TextWidth = Mathf.Max(width, TextWidth); width = 0; TextHeight += font.height; }
            else width += c == ' ' ? 40 : Glyph(c)?[0].full.x ?? 0;
        }
        TextWidth = Mathf.Max(width, TextWidth);
        rectTransform.sizeDelta = new Vector2(TextWidth, TextHeight);
        SetAllDirty();
    }

    private void Update()
    {
        int previous = (int)(age * 24);
        age += VanillaMenuTiming.Delta;
        if ((int)(age * 24) != previous) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (font == null) return;
        float x = 0, y = 0;
        foreach (char c in value)
        {
            if (c == ' ') { x += 40; continue; }
            if (c == '\n') { x = 0; y += font.height; continue; }
            Frame[] frames = Glyph(c);
            if (frames == null) continue;
            Frame f = frames[(int)(age * 24) % frames.Length];
            float left = x + f.trim.x, top = y + font.height - frames[0].full.y + f.trim.y;
            int i = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, -top), color, new Vector2(f.uv.xMin, f.uv.yMax));
            mesh.AddVert(new Vector3(left+f.size.x, -top), color, new Vector2(f.uv.xMax, f.uv.yMax));
            mesh.AddVert(new Vector3(left+f.size.x, -top-f.size.y), color, new Vector2(f.uv.xMax, f.uv.yMin));
            mesh.AddVert(new Vector3(left, -top-f.size.y), color, new Vector2(f.uv.xMin, f.uv.yMin));
            mesh.AddTriangle(i, i+1, i+2);
            mesh.AddTriangle(i, i+2, i+3);
            x += frames[0].full.x;
        }
    }
}
