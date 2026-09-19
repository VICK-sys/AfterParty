using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public static class FunkinNoteSkin
{
    private static readonly Dictionary<(string atlas, string prefix), Sprite[]> animations = new Dictionary<(string, string), Sprite[]>();
    private static readonly Sprite[][] headFrames = new Sprite[2][];
    private static readonly Sprite[][][][] receptorFrames = new Sprite[2][][][];
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
        var key = (atlas, prefix);
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

    private static void Prepare(bool pixel)
    {
        int skin = pixel ? 1 : 0;
        if (headFrames[skin] != null) return;
        var heads = new Sprite[4];
        var receptors = new Sprite[4][][];
        for (int direction = 0; direction < 4; direction++)
        {
            heads[direction] = Frames(pixel ? "Pixel/arrows-pixels" : "notes", "note" + Directions[direction])[0];
            receptors[direction] = new Sprite[4][];
            receptors[direction][(int)FunkinStrumline.Animation.Static] = Frames(pixel ? "Pixel/arrows-pixels" : "noteStrumline", "static" + Directions[direction] + "0");
            receptors[direction][(int)FunkinStrumline.Animation.Press] = Frames(pixel ? "Pixel/arrows-pixels" : "noteStrumline", (pixel ? "pressed" : "press") + Directions[direction] + "0");
            receptors[direction][(int)FunkinStrumline.Animation.Confirm] = Frames(pixel ? "Pixel/arrows-pixels" : "noteStrumline", "confirm" + Directions[direction] + "0");
            receptors[direction][(int)FunkinStrumline.Animation.ConfirmHold] = receptors[direction][(int)FunkinStrumline.Animation.Confirm];
        }
        headFrames[skin] = heads;
        receptorFrames[skin] = receptors;
    }

    public static void WarmGameplay()
    {
        bool pixel = Pixel;
        Prepare(pixel);
        for (int direction = 0; direction < 4; direction++)
        {
            string color = Colors[direction];
            if (pixel)
            {
                string lower = direction == 3 ? "orange" : color.ToLowerInvariant();
                for (int variant = 1; variant <= 3; variant++) Frames("Pixel/pixelNoteSplash", lower + variant);
            }
            else
            {
                for (int variant = 1; variant <= 2; variant++)
                    Frames("noteSplashes", "note impact " + variant + " " + (direction == 1 && variant == 1 ? " " : "") + color.ToLowerInvariant() + "0");
                foreach (string phase in new[] { "Start", "", "End" }) Frames("holdCover" + color, "holdCover" + phase + color + "0");
            }
        }
        if (pixel)
            foreach (string phase in new[] { "loop0000", "loop", "explode" }) Frames("Pixel/pixelNoteHoldCover", phase);
        _ = HoldMaterial;
    }

    public static Sprite Head(int direction)
    {
        bool pixel = Pixel;
        Prepare(pixel);
        return headFrames[pixel ? 1 : 0][direction];
    }

    public static void ApplyFrame(SpriteRenderer renderer, Sprite sprite)
    {
        renderer.sprite = sprite;
        renderer.transform.localRotation = Quaternion.Euler(0, 0, rotatedFrames.Contains(sprite) ? 90 : 0);
    }

    public static double ConfirmDuration(int direction)
    {
        bool pixel = Pixel;
        Prepare(pixel);
        return receptorFrames[pixel ? 1 : 0][direction][(int)FunkinStrumline.Animation.Confirm].Length / 24.0;
    }

    public static Sprite Receptor(int direction, FunkinStrumline.Animation animation, double time)
    {
        bool pixel = Pixel;
        Prepare(pixel);
        Sprite[] frames = receptorFrames[pixel ? 1 : 0][direction][(int)animation];
        return frames[Math.Min((int)(time * 24), frames.Length - 1)];
    }

    public static void WorldScale(Transform target, float scale)
    {
        Vector3 parent = target.parent == null ? Vector3.one : target.parent.lossyScale;
        target.localScale = new Vector3(scale / parent.x, scale / parent.y, 1 / parent.z);
    }
}
