using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class VanillaPauseStickers : MonoBehaviour
{
    private sealed class Sticker
    {
        public RectTransform rect;
        public float timing;
        public float popTime;
        public float scale;
        public bool visible;
        public bool popped;
    }

    private readonly List<Sticker> stickers = new List<Sticker>();
    private AudioClip[] sounds;
    private AudioSource sound;
    private Action changeScene;
    private float age;
    private bool uncovering;
    private bool waiting;
    public static bool Active { get; private set; }

    public static void Begin(Action action, string character = null)
    {
        var root = new GameObject("Pause Stickers", typeof(RectTransform));
        DontDestroyOnLoad(root);
        root.AddComponent<VanillaPauseStickers>().Build(action, character);
    }

    private void Build(Action action, string character)
    {
        Active = true;
        changeScene = action;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        var blocker = new GameObject("Input Blocker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        blocker.SetParent(transform, false);
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.offsetMin = blocker.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = Color.clear;
        var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.SetParent(transform, false);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.sizeDelta = new Vector2(1280, 720);
        viewport.gameObject.AddComponent<Image>().raycastTarget = false;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        sound = gameObject.AddComponent<AudioSource>();
        sounds = Resources.LoadAll<AudioClip>("FunkinPause/stickerSounds");
        string player = character ?? Song.instance?.vanillaPlayback?.PlayerId;
        string pack = player == null ? "stickers" : "stickerPacks/" + (player.StartsWith("pico", StringComparison.Ordinal) ? "pico" : "bf");
        Texture2D[] textures = Resources.LoadAll<Texture2D>("FunkinPause/" + pack);
        float x = -100;
        float y = -100;
        while (x <= 1280)
        {
            Texture2D texture = textures[UnityEngine.Random.Range(0, textures.Length)];
            Add(viewport, texture, x, y, UnityEngine.Random.Range(-60, 71));
            x += texture.width * 0.5f;
            if (x >= 1280 && y <= 720)
            {
                x = -100;
                y += UnityEngine.Random.Range(70f, 120f);
            }
        }
        for (int index = stickers.Count - 1; index > 0; index--)
        {
            int other = UnityEngine.Random.Range(0, index + 1);
            Sticker item = stickers[index];
            stickers[index] = stickers[other];
            stickers[other] = item;
        }
        Texture2D last = textures[UnityEngine.Random.Range(0, textures.Length)];
        Add(viewport, last, (1280 - last.width) * 0.5f, (720 - last.height) * 0.5f, 0);
        for (int index = 0; index < stickers.Count; index++)
        {
            Sticker sticker = stickers[index];
            sticker.rect.SetAsLastSibling();
            sticker.timing = index * 0.9f / stickers.Count;
            sticker.popTime = sticker.timing + (index == stickers.Count - 1 ? 2 : UnityEngine.Random.Range(0, 3)) / 24f;
            sticker.scale = UnityEngine.Random.Range(0.97f, 1.02f);
        }
    }

    private void Add(Transform parent, Texture2D texture, float x, float y, int angle)
    {
        var rect = new GameObject(texture.name, typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(texture.width, texture.height);
        rect.anchoredPosition = new Vector2(x + texture.width * 0.5f, -y - texture.height * 0.5f);
        rect.localRotation = Quaternion.Euler(0, 0, -angle);
        rect.GetComponent<RawImage>().texture = texture;
        rect.GetComponent<RawImage>().raycastTarget = false;
        rect.gameObject.SetActive(false);
        stickers.Add(new Sticker { rect = rect });
    }

    private void Update()
    {
        if (waiting)
        {
            if (VanillaFreeplay.Active == null && VanillaStoryMenu.Active == null) return;
            waiting = false;
            uncovering = true;
            age = 0;
        }
        age += Time.unscaledDeltaTime;
        foreach (Sticker sticker in stickers)
        {
            if (age < sticker.timing) continue;
            if (sticker.visible == uncovering)
            {
                sticker.visible = !uncovering;
                sticker.rect.gameObject.SetActive(sticker.visible);
                if (sounds.Length != 0) sound.PlayOneShot(sounds[UnityEngine.Random.Range(0, sounds.Length)], OptionsV2.miscVolume);
            }
            if (!uncovering && !sticker.popped && age >= sticker.popTime)
            {
                sticker.popped = true;
                sticker.rect.localScale = Vector3.one * sticker.scale;
            }
        }
        if (uncovering && age >= stickers[stickers.Count - 1].timing)
        {
            Active = false;
            Destroy(gameObject);
        }
        else if (!uncovering && age >= stickers[stickers.Count - 1].popTime)
        {
            waiting = true;
            changeScene();
            changeScene = null;
        }
    }

    private void OnDestroy() => Active = false;
}
