using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaDebugText : MaskableGraphic
{
    [Serializable]
    private sealed class Glyph
    {
        public int x, y;
        public float advance;
    }

    [Serializable]
    private sealed class FontData
    {
        public Glyph[] glyphs;
        public Kerning[] kerning;
    }

    [Serializable]
    private sealed class Kerning
    {
        public int pair;
        public float offset;
    }

    private static FontData data;
    private static Texture2D atlas;
    private static readonly Dictionary<int, float> kerning = new Dictionary<int, float>();
    private string value = "";
    public override Texture mainTexture => atlas;

    public string Text
    {
        get => value;
        set
        {
            if (atlas == null)
            {
                atlas = Resources.Load<Texture2D>("VanillaDebug/font");
                data = JsonUtility.FromJson<FontData>(Resources.Load<TextAsset>("VanillaDebug/font").text);
                kerning.Clear();
                foreach (var pair in data.kerning) kerning[pair.pair] = pair.offset;
            }
            if (this.value == value) return;
            this.value = value ?? "";
            SetAllDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (atlas == null || data == null) return;
        float advance = 0, y = 0;
        int previous = 0;
        foreach (char character in value)
        {
            if (character == '\n')
            {
                advance = 0;
                y -= 15;
                previous = 0;
                continue;
            }
            Glyph glyph = data.glyphs[Mathf.Clamp(character - 32, 0, 94)];
            if (kerning.TryGetValue(previous * 128 + character, out float offset)) advance += offset;
            float x = Mathf.Floor(advance);
            float u0 = glyph.x / (float)atlas.width, u1 = (glyph.x + 32) / (float)atlas.width;
            float v0 = 1 - glyph.y / (float)atlas.height, v1 = 1 - (glyph.y + 32) / (float)atlas.height;
            int vertex = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, y), color, new Vector2(u0, v0));
            mesh.AddVert(new Vector3(x + 32, y), color, new Vector2(u1, v0));
            mesh.AddVert(new Vector3(x + 32, y - 32), color, new Vector2(u1, v1));
            mesh.AddVert(new Vector3(x, y - 32), color, new Vector2(u0, v1));
            mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
            mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
            advance += glyph.advance;
            previous = character;
        }
    }
}
