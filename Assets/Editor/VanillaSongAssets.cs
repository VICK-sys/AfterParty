using System.IO;
using System.Linq;
using SimpleSpriteAnimator;
using UnityEditor;
using UnityEngine;

public static class VanillaSongAssets
{
    private const string Root = "Assets/Resources/VanillaSongs";

    [MenuItem("Tools/AfterParty/Build Tutorial Character")]
    public static void Build()
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        Character girlfriend = AssetDatabase.LoadAssetAtPath<Character>(Root + "/Girlfriend.asset");
        if (girlfriend == null)
        {
            girlfriend = ScriptableObject.CreateInstance<Character>();
            AssetDatabase.CreateAsset(girlfriend, Root + "/Girlfriend.asset");
        }
        girlfriend.characterName = "Girlfriend";
        girlfriend.scale = 1;
        girlfriend.cameraOffset = new Vector3(0, 1, -10);
        girlfriend.healthColor = new Color(0.65f, 0, 0.3f, 1);
        girlfriend.portraitSize = new Vector2(100, 100);
        var portrait = (TextureImporter)AssetImporter.GetAtPath(Root + "/Girlfriend Portrait.png");
        portrait.textureType = TextureImporterType.Sprite;
        portrait.spriteImportMode = SpriteImportMode.Single;
        portrait.textureCompression = TextureImporterCompression.Uncompressed;
        portrait.mipmapEnabled = false;
        portrait.SaveAndReimport();
        girlfriend.portrait = girlfriend.portraitDead = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Girlfriend Portrait.png");
        string[] prefixes = { "GFDancingBeat", "GFleftNote", "GFDownNote", "GFUpNote", "GFRightNote", "GFCheer" };
        string[] names = { "Idle", "Sing Left", "Sing Down", "Sing Up", "Sing Right", "Cheer" };
        girlfriend.animations = prefixes.Select((prefix, index) =>
        {
            Sprite[] sprites = Directory.GetFiles("Assets/Sprites/Characters/GF/Main", prefix + "*.png")
                .Where(path => char.IsDigit(Path.GetFileNameWithoutExtension(path)[prefix.Length]))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'))).ToArray();
            return Animation("Girlfriend " + names[index], names[index], sprites);
        }).ToList();
        Sprite[] hey = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Characters/BF/BOYFRIEND.png")
            .OfType<Sprite>().Where(sprite => sprite.name.StartsWith("BF HEY!!"))
            .OrderBy(sprite => sprite.name, System.StringComparer.Ordinal).ToArray();
        Animation("Boyfriend Hey", "BF Hey", hey);
        EditorUtility.SetDirty(girlfriend);
        AssetDatabase.SaveAssets();
        Debug.Log("Tutorial character animations and Boyfriend greeting saved.");
    }

    private static SpriteAnimation Animation(string file, string name, Sprite[] sprites)
    {
        if (sprites.Length == 0 || sprites.Any(sprite => sprite == null))
            throw new System.InvalidOperationException("Missing sprites for " + name);
        string path = Root + "/" + file + ".asset";
        SpriteAnimation animation = AssetDatabase.LoadAssetAtPath<SpriteAnimation>(path);
        if (animation == null)
        {
            animation = ScriptableObject.CreateInstance<SpriteAnimation>();
            AssetDatabase.CreateAsset(animation, path);
        }
        animation.Name = name;
        animation.FPS = 24;
        animation.SpriteAnimationType = name == "Idle" ? SpriteAnimationType.Looping : SpriteAnimationType.PlayOnce;
        animation.Frames = sprites.Select(sprite => new SpriteAnimationFrame { Sprite = sprite, Offset = Vector2.zero }).ToList();
        EditorUtility.SetDirty(animation);
        return animation;
    }
}
