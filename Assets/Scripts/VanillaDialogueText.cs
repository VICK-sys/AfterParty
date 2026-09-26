using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

public sealed class VanillaDialogueText : Text
{
    [Serializable]
    private sealed class Glyph
    {
        public int code, x, y, width, height, offsetX;
        public float advance;
    }

    [Serializable]
    private sealed class FontData
    {
        public int lineHeight;
        public Glyph[] glyphs;
    }

    private static FontData data;
    private static Color32[] atlas;
    private static int atlasWidth, atlasHeight;
    private Texture2D bitmap;
    private Material bitmapMaterial;
    private Color32[] output;
    private byte[] coverage;
    private Color shadowColor;
    private int shadowWidth;
    private int preparedLines = 1;
    private bool native;
    private Shadow fallbackShadow;
    public override Texture mainTexture => native && bitmap != null ? bitmap : base.mainTexture;
    public override string text
    {
        get => base.text;
        set
        {
            if (base.text == value) return;
            SelectRenderer(value ?? "");
            base.text = value;
            SetMaterialDirty();
        }
    }

    public void Configure(Color foreground, Color shadow, float distance, int size)
    {
        color = foreground;
        shadowColor = shadow;
        shadowWidth = Mathf.Max(0, Mathf.RoundToInt(distance));
        fontSize = size;
        if (fallbackShadow == null) fallbackShadow = gameObject.AddComponent<Shadow>();
        fallbackShadow.effectColor = shadow;
        fallbackShadow.effectDistance = new Vector2(distance, -distance);
        SelectRenderer(text);
        SetAllDirty();
    }

    private void SelectRenderer(string value)
    {
        native = fontSize == 32 && Supported(value);
        if (fallbackShadow != null) fallbackShadow.enabled = !native;
        if (native && bitmapMaterial == null)
            bitmapMaterial = Resources.Load<Material>("VanillaText/Premultiplied");
        material = native ? bitmapMaterial : null;
    }

    public string Prepare(string value)
    {
        LoadFont();
        preparedLines = 1;
        if (fontSize != 32 || !Supported(value)) return value;
        float limit = rectTransform.rect.width - 4;
        var wrapped = new StringBuilder();
        bool firstParagraph = true;
        foreach (string paragraph in value.Replace("\r", "").Split('\n'))
        {
            if (!firstParagraph) wrapped.Append('\n');
            firstParagraph = false;
            float width = 0;
            int start = 0;
            while (start < paragraph.Length)
            {
                int end = paragraph.IndexOf(' ', start);
                if (end < 0) end = paragraph.Length;
                float wordWidth = 0;
                for (int index = start; index < end; index++) wordWidth += data.glyphs[paragraph[index] - 32].advance;
                if (width > 0 && width + wordWidth > limit)
                {
                    wrapped.Append('\n');
                    width = 0;
                }
                for (int index = start; index < end; index++)
                {
                    float advance = data.glyphs[paragraph[index] - 32].advance;
                    if (width > 0 && width + advance > limit) { wrapped.Append('\n'); width = 0; }
                    wrapped.Append(paragraph[index]);
                    width += advance;
                }
                if (end < paragraph.Length)
                {
                    wrapped.Append(' ');
                    width += data.glyphs[0].advance;
                }
                start = end + 1;
            }
        }
        string result = wrapped.ToString();
        preparedLines = result.Split('\n').Length;
        return result;
    }

    private static bool Supported(string value)
    {
        foreach (char character in value)
            if (character != '\n' && character != '\r' && (character < 32 || character > 126)) return false;
        return true;
    }

    private static void LoadFont()
    {
        if (data != null) return;
        data = JsonConvert.DeserializeObject<FontData>(Resources.Load<TextAsset>("VanillaText/dialogue").text);
        Texture2D texture = Resources.Load<Texture2D>("VanillaText/dialogue");
        atlasWidth = texture.width;
        atlasHeight = texture.height;
        atlas = texture.GetPixels32();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        if (!native) { base.OnPopulateMesh(mesh); return; }
        mesh.Clear();
        if (string.IsNullOrEmpty(text)) return;
        LoadFont();
        int width = Mathf.CeilToInt(rectTransform.rect.width) + shadowWidth;
        int coverageWidth = width + shadowWidth;
        int lines = Math.Max(preparedLines, text.Split('\n').Length);
        int height = lines * data.lineHeight + 4 + shadowWidth;
        if (bitmap == null || bitmap.width != width || bitmap.height != height || coverage.Length != coverageWidth * height)
        {
            Release(bitmap);
            bitmap = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            coverage = new byte[coverageWidth * height];
            output = new Color32[width * height];
        }
        Array.Clear(coverage, 0, coverage.Length);
        int left = 0, top = 0;
        foreach (char character in text)
        {
            if (character == '\r') continue;
            if (character == '\n') { left = 0; top += data.lineHeight; continue; }
            Glyph glyph = data.glyphs[character - 32];
            for (int y = 0; y < glyph.height && top + y < height; y++)
                for (int x = 0; x < glyph.width && left + glyph.offsetX + x < width; x++)
                {
                    if (left + glyph.offsetX + x < -Mathf.Min(1, shadowWidth)) continue;
                    byte alpha = atlas[(atlasHeight - 1 - glyph.y - y) * atlasWidth + glyph.x + x].a;
                    int index = (top + y) * coverageWidth + left + glyph.offsetX + x + shadowWidth;
                    coverage[index] = (byte)(alpha + (coverage[index] * (255 - alpha) + 127) / 255);
                }
            left += Mathf.RoundToInt(glyph.advance);
        }
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float front = coverage[y * coverageWidth + x + shadowWidth] / 255f * color.a;
                float shadow = 0;
                for (int step = shadowWidth; step > 0; step--)
                    if (y >= step)
                    {
                        float alpha = coverage[(y - step) * coverageWidth + x + shadowWidth - step] / 255f * shadowColor.a;
                        shadow = alpha + shadow * (1 - alpha);
                    }
                shadow *= 1 - front;
                output[(height - 1 - y) * width + x] = new Color(color.r * front + shadowColor.r * shadow,
                    color.g * front + shadowColor.g * shadow, color.b * front + shadowColor.b * shadow, front + shadow);
            }
        bitmap.SetPixels32(output);
        bitmap.Apply(false, false);
        mesh.AddVert(new Vector3(0, 0), Color.white, new Vector2(0, 1));
        mesh.AddVert(new Vector3(width, 0), Color.white, new Vector2(1, 1));
        mesh.AddVert(new Vector3(width, -height), Color.white, new Vector2(1, 0));
        mesh.AddVert(new Vector3(0, -height), Color.white, new Vector2(0, 0));
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(0, 2, 3);
    }

    protected override void OnDestroy()
    {
        Release(bitmap);
        base.OnDestroy();
    }

    private static void Release(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
