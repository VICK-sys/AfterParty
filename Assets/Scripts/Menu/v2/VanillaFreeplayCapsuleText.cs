using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

public sealed class VanillaFreeplayCapsuleText : Text
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
    private static Color32[] pixels;
    private static int atlasWidth, atlasHeight;
    private Texture2D texture;
    private readonly Dictionary<string, (Texture2D texture, float width)> renders = new Dictionary<string, (Texture2D, float)>();
    private Material effect;
    private float width;
    private bool native;
    private const int Padding = 12;
    public override Texture mainTexture => native && texture != null ? texture : base.mainTexture;
    public override float preferredWidth => native ? width : base.preferredWidth;

    public override string text
    {
        get => base.text;
        set
        {
            if (base.text == value && texture != null) return;
            base.text = value;
            native = !string.IsNullOrEmpty(value);
            foreach (char character in value ?? "") native &= character >= 32 && character <= 126;
            texture = null;
            if (native)
            {
                if (renders.TryGetValue(value, out var cached)) { texture = cached.texture; width = cached.width; }
                else { BuildTexture(value); renders.Add(value, (texture, width)); }
            }
            material = native ? effect : null;
            SetAllDirty();
        }
    }

    public void Present(Color glow, bool selected, bool additive, bool whiteFlash, bool dimFlash = false)
    {
        if (!native || effect == null) return;
        effect.SetColor("_GlowColor", whiteFlash ? Color.white : additive || dimFlash ? new Color32(221, 221, 221, 255) : glow);
        effect.SetColor("_BlurColor", whiteFlash ? Color.white : glow);
        effect.SetFloat("_Selected", selected ? 1 : 0);
        effect.SetFloat("_Destination", additive ? 1 : 10);
        effect.SetFloat("_Additive", additive ? 1 : 0);
    }

    private void BuildTexture(string value)
    {
        if (data == null)
        {
            data = JsonConvert.DeserializeObject<FontData>(Resources.Load<TextAsset>("VanillaFreeplay/fonts/header/5by7-32").text);
            var atlas = Resources.Load<Texture2D>("VanillaFreeplay/fonts/header/5by7-32");
            atlasWidth = atlas.width;
            atlasHeight = atlas.height;
            pixels = atlas.GetPixels32();
        }
        width = 4;
        foreach (char character in value) width += data.glyphs[character - 32].advance;
        int w = Mathf.CeilToInt(width) + Padding * 2;
        int h = 27 + Padding * 2;
        var raw = new float[w * h];
        float advance = 0;
        foreach (char character in value)
        {
            Glyph glyph = data.glyphs[character - 32];
            int left = Padding + Mathf.FloorToInt(advance);
            for (int y = 0; y < glyph.height; y++)
                for (int x = 0; x < glyph.width; x++)
                {
                    float alpha = pixels[(atlasHeight - 1 - glyph.y - y) * atlasWidth + glyph.x + x].a / 255f;
                    int position = (h - 1 - Padding - y) * w + left + x;
                    raw[position] = alpha + raw[position] * (1 - alpha);
                }
            advance += glyph.advance;
        }
        var glow = new int[raw.Length];
        for (int index = 0; index < raw.Length; index++) glow[index] = Mathf.RoundToInt(raw[index] * 255);
        for (int pass = 0; pass < 4; pass++)
        {
            var next = new int[raw.Length];
            bool horizontal = pass % 2 == 0;
            int length = horizontal ? w : h, lines = horizontal ? h : w, stride = horizontal ? 1 : w;
            for (int line = 0; line < lines; line++)
            {
                int origin = horizontal ? line * w : line;
                int sum = 3 * glow[origin] + glow[origin + stride] + glow[origin + 2 * stride];
                for (int position = 0; position < length; position++)
                {
                    next[origin + position * stride] = (sum * 205) >> 10;
                    sum += glow[origin + Math.Min(position + 3, length - 1) * stride]
                        - glow[origin + Math.Max(position - 2, 0) * stride];
                }
            }
            glow = next;
        }
        var output = new Color32[raw.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int index = y * w + x;
                if (x < Padding || x >= w - Padding || y < Padding || y >= h - Padding)
                {
                    output[index] = new Color32(0, 0, 0, 255);
                    continue;
                }
                float blur = raw[index] * .19648255f;
                for (int tap = 0; tap < 3; tap++)
                {
                    float offset = 2 * (tap == 0 ? 1.4117647f : tap == 1 ? 3.2941176f : 5.1764706f);
                    float weight = (tap == 0 ? .29690696f : tap == 1 ? .0944704f : .01038136f) * .5f;
                    blur += weight * (Sample(raw, w, h, x + offset, y) + Sample(raw, w, h, x - offset, y)
                        + Sample(raw, w, h, x, y + offset) + Sample(raw, w, h, x, y - offset));
                }
                output[index] = new Color32((byte)Mathf.RoundToInt(raw[index] * 255), (byte)glow[index],
                    (byte)Mathf.RoundToInt(blur * 255), 255);
            }
        texture = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(output);
        texture.Apply(false, true);
        if (effect == null) effect = new Material(Resources.Load<Shader>("VanillaFreeplay/CapsuleText"));
    }

    private static float Sample(float[] input, int w, int h, float x, float y)
    {
        x = Mathf.Clamp(x, 0, w - 1);
        y = Mathf.Clamp(y, 0, h - 1);
        return input[Mathf.RoundToInt(y) * w + Mathf.RoundToInt(x)];
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        if (!native || texture == null) { base.OnPopulateMesh(mesh); return; }
        mesh.Clear();
        float left = -Padding, top = Padding;
        mesh.AddVert(new Vector3(left, top), color, new Vector2(0, 1));
        mesh.AddVert(new Vector3(left + texture.width, top), color, new Vector2(1, 1));
        mesh.AddVert(new Vector3(left + texture.width, top - texture.height), color, new Vector2(1, 0));
        mesh.AddVert(new Vector3(left, top - texture.height), color, new Vector2(0, 0));
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(0, 2, 3);
    }

    protected override void OnDestroy()
    {
        foreach (var cached in renders.Values) Release(cached.texture);
        renders.Clear();
        Release(effect);
        base.OnDestroy();
    }

    private static void Release(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
