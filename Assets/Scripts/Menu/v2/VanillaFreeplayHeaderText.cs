using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaFreeplayHeaderText : MaskableGraphic
{
    [Serializable]
    private sealed class Glyph
    {
        public int code, x, y, width, height;
        public float advance;
    }

    [Serializable]
    private sealed class FontData
    {
        public Glyph[] glyphs;
    }

    private static FontData data;
    private static Texture2D atlas;
    private string value = "";
    public override Texture mainTexture => atlas;
    public string Text
    {
        get => value;
        set
        {
            if (data == null)
            {
                data = JsonConvert.DeserializeObject<FontData>(Resources.Load<TextAsset>("VanillaFreeplay/fonts/header/5by7-32").text);
                atlas = Resources.Load<Texture2D>("VanillaFreeplay/fonts/header/5by7-32");
            }
            if (this.value == value) return;
            this.value = value ?? "";
            raycastTarget = false;
            SetAllDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (data == null) return;
        float width = 0;
        foreach (char character in value) width += data.glyphs[Mathf.Clamp(character - 32, 0, 94)].advance;
        float start = Mathf.Floor((rectTransform.rect.width - 4 - width) / 2 + .5f);
        float advance = 0;
        foreach (char character in value)
        {
            Glyph glyph = data.glyphs[Mathf.Clamp(character - 32, 0, 94)];
            float x = start + Mathf.Floor(advance);
            float u0 = glyph.x / (float)atlas.width;
            float u1 = (glyph.x + glyph.width) / (float)atlas.width;
            float v0 = 1 - glyph.y / (float)atlas.height;
            float v1 = 1 - (glyph.y + glyph.height) / (float)atlas.height;
            int vertex = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, 0), color, new Vector2(u0, v0));
            mesh.AddVert(new Vector3(x + glyph.width, 0), color, new Vector2(u1, v0));
            mesh.AddVert(new Vector3(x + glyph.width, -glyph.height), color, new Vector2(u1, v1));
            mesh.AddVert(new Vector3(x, -glyph.height), color, new Vector2(u0, v1));
            mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
            mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
            advance += glyph.advance;
        }
    }
}
