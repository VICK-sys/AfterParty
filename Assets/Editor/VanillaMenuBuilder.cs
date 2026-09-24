using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class VanillaMenuBuilder
{
    private const string Root = "Assets/VanillaMenu/";

    [MenuItem("Tools/Friday Fight Funkin'/Rebuild Vanilla Main Menu")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        AssetDatabase.Refresh();
        foreach (string file in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        MenuV2 menu = Object.FindFirstObjectByType<MenuV2>();
        if (menu.vanillaMenu != null)
            Object.DestroyImmediate(menu.vanillaMenu.effects.gameObject);
        Object.DestroyImmediate(menu.mainScreen.gameObject);

        RectTransform root = Rect("Vanilla Main Menu", null, Vector2.zero, Vector2.zero);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        RectTransform matte = Rect("Letterbox", root, Vector2.zero, Vector2.zero);
        Stretch(matte);
        matte.gameObject.AddComponent<Image>().color = Color.black;
        RectTransform viewport = Rect("Viewport", root, new Vector2(1280, 720), Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();

        RawImage background = Image("Background", viewport, "Images/menuBG.png");
        background.rectTransform.sizeDelta = new Vector2(1536, 1536f * background.texture.height / background.texture.width);
        RawImage magenta = Image("Confirm Background", viewport, "Images/menuBGMagenta.png");
        magenta.rectTransform.sizeDelta = background.rectTransform.sizeDelta;
        magenta.enabled = false;
        VanillaMainMenu main = root.gameObject.AddComponent<VanillaMainMenu>();
        main.menu = menu;
        main.background = background.rectTransform;
        main.magenta = magenta;
        string[] names = { "storymode", "freeplay", "merch", "options", "credits" };
        main.items = new VanillaMenuItem[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            RawImage graphic = Image(names[i], viewport, "Images/mainmenu/" + names[i] + ".png");
            VanillaMenuItem item = graphic.gameObject.AddComponent<VanillaMenuItem>();
            item.image = graphic;
            item.index = i;
            XElement[] frames = XDocument.Load(Root + "Images/mainmenu/" + names[i] + ".xml").Root.Elements("SubTexture").ToArray();
            item.idle = Frames(frames, names[i] + " idle", graphic.texture);
            item.selected = Frames(frames, names[i] + " selected", graphic.texture);
            item.Select(i == 0);
            item.Draw(0, -319.5f);
            main.items[i] = item;
        }
        background.rectTransform.anchoredPosition = new Vector2(0, -319.5f * 0.17f);
        magenta.rectTransform.anchoredPosition = background.rectTransform.anchoredPosition;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(Root + "vcr.ttf");
        GameObject audio = new GameObject("Main Menu Sounds", typeof(AudioSource));
        audio.transform.SetParent(menu.transform, false);
        main.effects = audio.GetComponent<AudioSource>();
        main.effects.playOnAwake = false;
        main.scrollSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Sounds/scrollMenu.ogg");
        main.confirmSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Sounds/confirmMenu.ogg");
        main.cancelSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Sounds/cancelMenu.ogg");
        BuildCredits(main, viewport, font);
        menu.mainScreen = root;
        menu.vanillaMenu = main;
        menu.menuClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "freakyMenu.ogg");
        menu.musicSource.clip = menu.menuClip;
        menu.musicSource.loop = true;
        menu.playScreen.gameObject.SetActive(false);
        menu.optionsScreen.gameObject.SetActive(false);
        menu.inputBlocker.enabled = false;
        EditorUtility.SetDirty(menu);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Vanilla main menu saved in Assets/Scenes/Title.unity.");
    }

    private static VanillaMenuItem.Frame[] Frames(XElement[] elements, string prefix, Texture texture)
    {
        return elements.Where(e => ((string)e.Attribute("name")).StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(e => (string)e.Attribute("name"), StringComparer.Ordinal).Select(e =>
            {
                float x = (float)e.Attribute("x"), y = (float)e.Attribute("y");
                float width = (float)e.Attribute("width"), height = (float)e.Attribute("height");
                float frameWidth = (float?)e.Attribute("frameWidth") ?? width;
                float frameHeight = (float?)e.Attribute("frameHeight") ?? height;
                float frameX = (float?)e.Attribute("frameX") ?? 0;
                float frameY = (float?)e.Attribute("frameY") ?? 0;
                return new VanillaMenuItem.Frame
                {
                    uv = new Rect(x / texture.width, 1 - (y + height) / texture.height, width / texture.width, height / texture.height),
                    size = new Vector2(width, height),
                    offset = new Vector2(-frameX + (width - frameWidth) / 2, frameY - (height - frameHeight) / 2)
                };
            }).ToArray();
    }

    private static void BuildCredits(VanillaMainMenu main, RectTransform parent, Font font)
    {
        main.creditsPanel = Rect("Credits", parent, new Vector2(1280, 720), Vector2.zero).gameObject;
        main.creditsPanel.SetActive(false);
    }

    private static RawImage Image(string name, RectTransform parent, string path)
    {
        RawImage image = Rect(name, parent, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + path);
        image.raycastTarget = false;
        return image;
    }

    private static Text Text(string name, RectTransform parent, Font font, string value, int size, Vector2 dimensions, Vector2 position)
    {
        Text text = Rect(name, parent, dimensions, position).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.supportRichText = false;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
