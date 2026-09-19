using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public static class FunkinHudAssets
{
    public sealed class Glyph
    {
        public Sprite Sprite;
        public int X;
        public int Y;
        public int Advance;
    }

    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite[]> icons = new Dictionary<string, Sprite[]>();
    private static Dictionary<char, Glyph> glyphs;
    private static int fontOriginX;
    private static Sprite solid;

    public static int FontOriginX
    {
        get
        {
            FontGlyph(' ');
            return fontOriginX;
        }
    }

    public static Sprite Solid => solid != null ? solid : solid = Sprite.Create(Texture2D.whiteTexture,
        new Rect(0, 0, 1, 1), new Vector2(0, 1), 100, 0, SpriteMeshType.FullRect);

    public static Sprite Image(string name)
    {
        if (sprites.TryGetValue(name, out Sprite sprite)) return sprite;
        Texture2D texture = Resources.Load<Texture2D>("FunkinHud/" + name);
        if (texture == null) throw new InvalidOperationException("Missing Funkin HUD image: " + name);
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 1), 100, 0, SpriteMeshType.FullRect);
        sprites.Add(name, sprite);
        return sprite;
    }

    public static Sprite[] Icon(string character)
    {
        if (character == "sserafim-sakura") character = "bf";
        if (character.StartsWith("sserafim-")) character = character.Substring(9);
        if (character.StartsWith("pico-") && character != "pico-pixel") character = "pico";
        if (character == "darnell-blazin") character = "darnell";
        if (character == "spooky-dark") character = "spooky";
        if (character == "bf-dark" || character == "bf-car" || character == "bf-christmas" || character == "bf-holding-gf") character = "bf";
        if (character == "mom-car") character = "mom";
        if (character == "parents-christmas") character = "parents";
        if (character == "monster-christmas") character = "monster";
        if (icons.TryGetValue(character, out Sprite[] frames)) return frames;
        Texture2D texture = Resources.Load<Texture2D>("FunkinHud/Icons/icon-" + character);
        if (texture == null) return Icon("face");
        if (character == "bf-pixel" || character == "pico-pixel" || character == "senpai" || character == "senpai-angry" || character == "spirit")
            texture.filterMode = FilterMode.Point;
        int size = texture.height;
        frames = new Sprite[texture.width / size];
        for (int index = 0; index < frames.Length; index++)
            frames[index] = Sprite.Create(texture, new Rect(index * size, 0, size, size), new Vector2(0, 1), 100, 0, SpriteMeshType.FullRect);
        icons.Add(character, frames);
        return frames;
    }

    public static Glyph FontGlyph(char character)
    {
        if (glyphs == null)
        {
            glyphs = new Dictionary<char, Glyph>();
            Texture2D texture = Resources.Load<Texture2D>("FunkinHud/vcr-bmp");
            var data = XDocument.Parse(Resources.Load<TextAsset>("FunkinHud/vcr-bmp").text);
            foreach (XElement entry in data.Root.Element("chars").Elements("char"))
            {
                int x = (int)entry.Attribute("x");
                int y = (int)entry.Attribute("y");
                int width = (int)entry.Attribute("width");
                int height = (int)entry.Attribute("height");
                fontOriginX = Math.Max(fontOriginX, -(int)entry.Attribute("xoffset"));
                glyphs[(char)(int)entry.Attribute("id")] = new Glyph
                {
                    X = (int)entry.Attribute("xoffset"),
                    Y = (int)entry.Attribute("yoffset"),
                    Advance = (int)entry.Attribute("xadvance"),
                    Sprite = width == 0 || height == 0 ? null : Sprite.Create(texture,
                        new Rect(x, texture.height - y - height, width, height), new Vector2(0, 1), 100, 0, SpriteMeshType.FullRect)
                };
            }
        }
        return glyphs.TryGetValue(character, out Glyph glyph) ? glyph : null;
    }
}
