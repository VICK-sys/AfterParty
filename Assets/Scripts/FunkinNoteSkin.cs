using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public static class FunkinNoteSkin
{
    private static readonly Dictionary<string, Sprite[]> animations = new Dictionary<string, Sprite[]>();
    private static readonly Dictionary<string, Dictionary<string, Sprite>> atlases = new Dictionary<string, Dictionary<string, Sprite>>();
    private static readonly HashSet<Sprite> rotatedFrames = new HashSet<Sprite>();
    public static readonly string[] Directions = { "Left", "Down", "Up", "Right" };
    public static readonly string[] Colors = { "Purple", "Blue", "Green", "Red" };
    private static Material holdMaterial;
    private static Material noteMaterial;
    private static Material desaturatedMaterial;
    private static Material pixelHoldMaterial;
    private static Material screenMaterial;
    public static bool Pixel => Song.instance != null && Song.instance.vanillaPlayback != null && Song.instance.vanillaPlayback.IsPixel;
    public static float Scale => Pixel ? 6 : .7f;

    public static Material NoteMaterial => noteMaterial != null ? noteMaterial :
        noteMaterial = new Material(Resources.Load<Shader>("FunkinNotes/FunkinNote"));

    public static Material ScreenMaterial
    {
        get
        {
            if (screenMaterial == null)
            {
                screenMaterial = new Material(NoteMaterial);
                screenMaterial.SetFloat("_Screen", 1);
                screenMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                screenMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcColor);
            }
            return screenMaterial;
        }
    }

    public static Material DesaturatedMaterial
    {
        get
        {
            if (desaturatedMaterial == null)
            {
                desaturatedMaterial = new Material(NoteMaterial);
                desaturatedMaterial.SetFloat("_Saturation", 0.2f);
            }
            return desaturatedMaterial;
        }
    }

    public static Material HoldMaterial
    {
        get
        {
            if (Pixel)
            {
                if (pixelHoldMaterial == null)
                {
                    pixelHoldMaterial = new Material(Shader.Find("Sprites/Default"));
                    pixelHoldMaterial.mainTexture = Resources.Load<Texture2D>("FunkinNotes/Pixel/arrowEndsNew");
                }
                return pixelHoldMaterial;
            }
            if (holdMaterial == null)
            {
                holdMaterial = new Material(Shader.Find("Sprites/Default"));
                holdMaterial.mainTexture = Resources.Load<Texture2D>("FunkinNotes/NOTE_hold_assets");
            }
            return holdMaterial;
        }
    }

    public static Sprite[] Frames(string atlas, string prefix)
    {
        string key = atlas + "/" + prefix;
        if (animations.TryGetValue(key, out Sprite[] frames)) return frames;
        if (!atlases.TryGetValue(atlas, out var sprites))
        {
            sprites = new Dictionary<string, Sprite>();
            var texture = Resources.Load<Texture2D>("FunkinNotes/" + atlas);
            var xml = Resources.Load<TextAsset>("FunkinNotes/" + atlas);
            if (texture == null || xml == null) throw new InvalidOperationException("Missing Funkin note atlas: " + atlas);
            foreach (XElement frame in XDocument.Parse(xml.text).Root.Elements("SubTexture"))
            {
                float x = (float)frame.Attribute("x");
                float y = (float)frame.Attribute("y");
                float width = (float)frame.Attribute("width");
                float height = (float)frame.Attribute("height");
                float fullWidth = (float?)frame.Attribute("frameWidth") ?? width;
                float fullHeight = (float?)frame.Attribute("frameHeight") ?? height;
                float offsetX = (float?)frame.Attribute("frameX") ?? 0;
                float offsetY = (float?)frame.Attribute("frameY") ?? 0;
                bool rotated = (string)frame.Attribute("rotated") == "true";
                var pivot = rotated ? new Vector2(1 - (fullHeight / 2 + offsetY) / width, 1 - (fullWidth / 2 + offsetX) / height) :
                    new Vector2((fullWidth / 2 + offsetX) / width, 1 - (fullHeight / 2 + offsetY) / height);
                Sprite sprite = Sprite.Create(texture, new Rect(x, texture.height - y - height, width, height), pivot, 100, 0, SpriteMeshType.FullRect);
                sprite.name = (string)frame.Attribute("name");
                sprites.Add(sprite.name, sprite);
                if (rotated) rotatedFrames.Add(sprite);
            }
            atlases.Add(atlas, sprites);
        }
        frames = sprites.Where(entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => entry.Value).ToArray();
        if (frames.Length == 0) throw new InvalidOperationException("Missing Funkin note animation: " + key);
        animations.Add(key, frames);
        return frames;
    }

    public static Sprite Head(int direction) => Frames(Pixel ? "Pixel/arrows-pixels" : "notes", "note" + Directions[direction])[0];

    public static void ApplyFrame(SpriteRenderer renderer, Sprite sprite)
    {
        renderer.sprite = sprite;
        renderer.transform.localRotation = Quaternion.Euler(0, 0, rotatedFrames.Contains(sprite) ? 90 : 0);
    }

    public static double ConfirmDuration(int direction) => Frames(Pixel ? "Pixel/arrows-pixels" : "noteStrumline", "confirm" + Directions[direction] + "0").Length / 24.0;

    public static Sprite Receptor(int direction, FunkinStrumline.Animation animation, double time)
    {
        string prefix = animation == FunkinStrumline.Animation.Static ? "static" : animation == FunkinStrumline.Animation.Press ? "press" : "confirm";
        if (Pixel && prefix == "press") prefix = "pressed";
        Sprite[] frames = Frames(Pixel ? "Pixel/arrows-pixels" : "noteStrumline", prefix + Directions[direction] + "0");
        return frames[Math.Min((int)(time * 24), frames.Length - 1)];
    }

    public static void WorldScale(Transform target, float scale)
    {
        Vector3 parent = target.parent == null ? Vector3.one : target.parent.lossyScale;
        target.localScale = new Vector3(scale / parent.x, scale / parent.y, 1 / parent.z);
    }
}
