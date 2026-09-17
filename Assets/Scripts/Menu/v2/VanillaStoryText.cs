using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaStoryText : MaskableGraphic
{
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
    private sealed class FontData
    {
        public float advance;
        public float lineHeight;
        public Glyph[] glyphs;
    }

    private static FontData data;
    private static Texture2D atlas;
    private string value = "";
    public bool centered;
    public override Texture mainTexture => atlas;
    public string Text
    {
        get => value;
        set
        {
            if (data == null)
            {
                data = JsonConvert.DeserializeObject<FontData>(Resources.Load<TextAsset>("VanillaStory/vcr32").text);
                atlas = Resources.Load<Texture2D>("VanillaStory/vcr32");
            }
            if (this.value == value) return;
            this.value = value ?? "";
            string[] lines = this.value.Split('\n');
            rectTransform.sizeDelta = new Vector2(Mathf.Ceil(lines.Max(line => line.Length) * data.advance + 4), Mathf.Ceil(lines.Length * data.lineHeight + 4));
            raycastTarget = false;
            SetAllDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (data == null || string.IsNullOrEmpty(value)) return;
        string[] lines = value.Split('\n');
        float width = rectTransform.sizeDelta.x;
        int vertex = 0;
        for (int row = 0; row < lines.Length; row++)
        {
            string line = lines[row];
            float start = centered ? (width - 4 - line.Length * data.advance) / 2 : 0;
            for (int column = 0; column < line.Length; column++)
            {
                int code = line[column];
                Glyph glyph = data.glyphs[Mathf.Clamp(code - 32, 0, data.glyphs.Length - 1)];
                float x = Mathf.Floor(start + 0.5f) + Mathf.Floor(column * data.advance);
                float y = -row * data.lineHeight;
                float u0 = glyph.x / (float)atlas.width;
                float u1 = (glyph.x + glyph.width) / (float)atlas.width;
                float v0 = 1 - glyph.y / (float)atlas.height;
                float v1 = 1 - (glyph.y + glyph.height) / (float)atlas.height;
                mesh.AddVert(new Vector3(x, y), color, new Vector2(u0, v0));
                mesh.AddVert(new Vector3(x + glyph.width, y), color, new Vector2(u1, v0));
                mesh.AddVert(new Vector3(x + glyph.width, y - glyph.height), color, new Vector2(u1, v1));
                mesh.AddVert(new Vector3(x, y - glyph.height), color, new Vector2(u0, v1));
                mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
                mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
                vertex += 4;
            }
        }
    }
}
