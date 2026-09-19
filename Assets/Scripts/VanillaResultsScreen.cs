using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class VanillaResultsScreen : MonoBehaviour
{
    private sealed class TimedSprite
    {
        public VanillaFreeplaySprite sprite;
        public float delay;
        public int loopFrame = -1;
    }

    public static VanillaResultsScreen Active { get; private set; }
    public VanillaResultsData Data { get; private set; }
    public float Elapsed { get; private set; }
    public bool Closing { get; private set; }
    public string MusicPath { get; private set; }
    public int DisplayedClear { get; private set; }
    public string PicoVariant { get; private set; }
    public bool ManualClock { get; set; }
    private RectTransform viewport;
    private RectTransform backdrop;
    private RectTransform verticalRank;
    private RectTransform header;
    private RectTransform title;
    private RectTransform difficulty;
    private RectTransform topBar;
    private RawImage flash;
    private Image fade;
    private Material white;
    private AudioSource introMusic;
    private AudioSource loopMusic;
    private AudioSource effects;
    private AudioClip introClip;
    private AudioClip loopClip;
    private Action completed;
    private readonly List<TimedSprite> sprites = new List<TimedSprite>();
    private readonly List<RectTransform> rankRows = new List<RectTransform>();
    private double opened;
    private double musicStart;
    private double loopStart;
    private double exitStart;
    private bool musicScheduled;
    private bool confirmedClear;
    private int previousClear = -1;
    private float difficultyWidth;
    private float titleWidth;
    private Texture2D backgroundTexture;
    private Texture2D flashTexture;

    public static IEnumerator Enter(Action callback)
    {
        var root = new GameObject("Results Entry", typeof(RectTransform), typeof(Canvas));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 49;
        var image = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(root.transform, false);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        double start = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - start < 0.6)
        {
            image.color = new Color(0, 0, 0, (float)(Time.realtimeSinceStartupAsDouble - start) / 0.6f);
            yield return null;
        }
        image.color = Color.black;
        callback();
        Destroy(root);
    }

    public static VanillaResultsScreen Open(VanillaResultsData data, Action onComplete, string picoVariant = null)
    {
        if (Active != null) throw new InvalidOperationException("Results are already open.");
        var screen = new GameObject("Results", typeof(RectTransform)).AddComponent<VanillaResultsScreen>();
        Active = screen;
        screen.Data = data;
        screen.completed = onComplete;
        screen.PicoVariant = picoVariant;
        screen.Build();
        screen.opened = Time.realtimeSinceStartupAsDouble;
        screen.musicStart = AudioSettings.dspTime + data.MusicDelay + 0.05;
        screen.ScheduleMusic();
        screen.RenderAt(0);
        return screen;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width = 1280, float height = 720)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static VanillaFreeplaySprite Sprite(string name, Transform parent, string path, float x, float y, string prefix = "")
    {
        var sprite = Rect(name, parent, x, y).gameObject.AddComponent<VanillaFreeplaySprite>();
        sprite.Load("images/" + path, prefix, false, "VanillaResults");
        sprite.centerScale = false;
        sprite.FreezeFrame(0);
        return sprite;
    }

    private VanillaFreeplaySprite Timed(string name, string path, float x, float y, string prefix, float delay, int loopFrame = -1)
    {
        var sprite = Sprite(name, viewport, path, x, y, prefix);
        sprites.Add(new TimedSprite { sprite = sprite, delay = delay, loopFrame = loopFrame });
        return sprite;
    }

    private static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    private static float QuartOut(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 4);
    private static float ExpoOut(float t) => t <= 0 ? 0 : t >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * t);

    private RawImage Gradient(string name, uint top, uint bottom, out Texture2D texture)
    {
        texture = new Texture2D(1, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { Hex(bottom), Hex(top) });
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        var image = Rect(name, viewport, 0, 0).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var letterbox = Rect("Letterbox", transform, 0, 0).gameObject.AddComponent<Image>();
        letterbox.color = Color.black;
        letterbox.raycastTarget = false;
        letterbox.rectTransform.anchorMin = Vector2.zero;
        letterbox.rectTransform.anchorMax = Vector2.one;
        letterbox.rectTransform.offsetMin = letterbox.rectTransform.offsetMax = Vector2.zero;
        viewport = Rect("Viewport", transform, 0, 0);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.gameObject.AddComponent<RectMask2D>();
        Gradient("Background", 0xFECC5C, 0xFDC05C, out backgroundTexture);
        backdrop = Rect("Scrolling Rank", viewport, 0, 0, 1280, 864);
        backdrop.localRotation = Quaternion.Euler(0, 0, 3.8f);
        BuildRankText();
        flash = Gradient("Flash", 0xFFF1A6, 0xFFF1BE, out flashTexture);
        white = new Material(Resources.Load<Shader>("VanillaResults/PureWhite"));
        BuildClear();
        BuildCharacters();
        verticalRank.SetAsLastSibling();
        BuildHeader();
        topBar = Rect("Top Bar", viewport, 0, 0, 1295, 149);
        topBar.gameObject.AddComponent<VanillaResultsBar>();
        Timed("Sound System", "soundSystem", -15, -180, "sound system", 8 / 24f);
        Timed("Results", "results", -200, -10, "results instance 1", 6 / 24f);
        Timed("Ratings", "ratingsPopin", -135, 135, "Categories", 21 / 24f);
        Timed("Score Label", "scorePopin", -180, 515, "tally score", 36 / 24f);
        if (Data.newHighscore) Timed("New Highscore", "highscoreNew", 44, 557, "highscoreAnim0", Data.HighscoreDelay, 16);
        BuildCounters();
        fade = Rect("Exit Fade", viewport, 0, 0).gameObject.AddComponent<Image>();
        fade.color = Color.clear;
        fade.raycastTarget = false;
        introMusic = gameObject.AddComponent<AudioSource>();
        loopMusic = gameObject.AddComponent<AudioSource>();
        effects = gameObject.AddComponent<AudioSource>();
        introMusic.playOnAwake = loopMusic.playOnAwake = effects.playOnAwake = false;
        var player = JObject.Parse(Resources.Load<TextAsset>("VanillaResults/players/" + Data.Character).text);
        MusicPath = (string)player["results"]["music"][new[] { "SHIT", "GOOD", "GREAT", "EXCELLENT", "PERFECT", "PERFECT_GOLD" }[Data.Rank]];
        introClip = Resources.Load<AudioClip>("VanillaResults/music/" + MusicPath + "-intro");
        loopClip = Resources.Load<AudioClip>("VanillaResults/music/" + MusicPath);
    }

    private void ScheduleMusic()
    {
        loopMusic.clip = loopClip;
        loopMusic.loop = true;
        introMusic.volume = loopMusic.volume = OptionsV2.menuVolume;
        if (introClip != null)
        {
            introMusic.clip = introClip;
            introMusic.PlayScheduled(musicStart);
            double duration = introClip.samples / (double)introClip.frequency;
            introMusic.SetScheduledEndTime(musicStart + duration);
            loopStart = musicStart + duration;
            loopMusic.PlayScheduled(loopStart);
        }
        else
        {
            loopStart = musicStart;
            loopMusic.PlayScheduled(loopStart);
        }
        musicScheduled = true;
    }

    private void BuildRankText()
    {
        string rank = new[] { "LOSS", "GOOD", "GREAT", "EXCELLENT", "PERFECT", "PERFECT" }[Data.Rank];
        var horizontal = Resources.Load<Texture2D>("VanillaResults/images/rankText/rankScroll" + rank);
        for (int row = 0; row < 12; row++)
        {
            var root = Rect("Rank Row " + row, backdrop, 320, 60 + 67.5f * row);
            for (int column = -3; column < 5; column++)
                Sprite("Rank", root, "rankText/rankScroll" + rank, column * (horizontal.width + 10), 0);
            rankRows.Add(root);
        }
        verticalRank = Rect("Vertical Rank", viewport, 1236, 100);
        var vertical = Resources.Load<Texture2D>("VanillaResults/images/rankText/rankText" + rank);
        for (int row = -2; row < 6; row++)
            Sprite("Rank", verticalRank, "rankText/rankText" + rank, 0, row * (vertical.height + 30));
    }

    private void BuildHeader()
    {
        header = Rect("Header Mask", viewport, 520, -180, 760, 1000);
        header.gameObject.AddComponent<RectMask2D>();
        string difficultyPath = "diff_" + (Data.difficulty ?? "normal").ToLowerInvariant();
        if (Resources.Load<Texture2D>("VanillaResults/images/" + difficultyPath) == null) difficultyPath = "diff_normal";
        difficulty = Sprite("Difficulty", header, difficultyPath, 35, 180).rectTransform;
        difficultyWidth = difficulty.sizeDelta.x;
        smallClear.SetParent(header, false);
        title = Rect("Song Title", header, 0, 0, 1, 61);
        const string letters = "AaBbCcDdEeFfGgHhiIJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz:1234567890().-";
        var texture = Resources.Load<Texture2D>("VanillaResults/images/tardlingSpritesheet");
        string text = Data.title ?? "";
        titleWidth = Mathf.Max(0, text.Length * 34 + 15);
        title.sizeDelta = new Vector2(titleWidth, 61);
        title.pivot = new Vector2(0.5f, 0.5f);
        title.localRotation = Quaternion.Euler(0, 0, 4.4f);
        for (int i = 0; i < text.Length; i++)
        {
            int index = letters.IndexOf(text[i]);
            if (index < 0) continue;
            var glyph = Rect("Glyph", title, i * 34, 0, 49, 61).gameObject.AddComponent<RawImage>();
            int columns = texture.width / 49;
            glyph.texture = texture;
            glyph.uvRect = new Rect(index % columns * 49f / texture.width, 1 - (index / columns + 1) * 61f / texture.height, 49f / texture.width, 61f / texture.height);
            glyph.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (Closing)
        {
            float age = (float)(Time.realtimeSinceStartupAsDouble - exitStart);
            float pitch = age < 0.1f ? Mathf.Lerp(1, 3, age / 0.1f) : Mathf.Lerp(3, 0.5f, (age - 0.1f) / 0.4f);
            introMusic.pitch = loopMusic.pitch = pitch;
            introMusic.volume = loopMusic.volume = OptionsV2.menuVolume * (1 - Mathf.Clamp01(age / 0.8f));
            if (Data.rankImproved && !Data.storyMode)
            {
                fade.color = new Color(0, 0, 0, ExpoOut(age / 0.5f));
                if (age >= 0.5f) Complete();
            }
            return;
        }
        if (!ManualClock) RenderAt((float)(Time.realtimeSinceStartupAsDouble - opened));
        if (VanillaControls.Pressed("ACCEPT") || VanillaControls.Pressed("PAUSE") || VanillaControls.Pressed("BACK")) Accept();
    }

    public void RenderAt(float time)
    {
        Elapsed = time;
        foreach (var entry in sprites)
        {
            entry.sprite.gameObject.SetActive(time >= entry.delay);
            int frame = Mathf.Max(0, Mathf.FloorToInt((time - entry.delay) * 24));
            if (entry.loopFrame >= 0 && frame >= entry.sprite.FrameCount)
                frame = entry.loopFrame + (frame - entry.sprite.FrameCount) % (entry.sprite.FrameCount - entry.loopFrame);
            entry.sprite.FreezeFrame(frame);
        }
        float firstFlash = 1 - Mathf.Clamp01((time - 37 / 24f) / (5 / 24f));
        float secondFlash = 1 - Mathf.Clamp01((time - Data.FlashDelay) / (14 / 24f));
        flash.color = new Color(1, 1, 1, !VanillaPreferences.FlashingLights ? 0 : time >= Data.FlashDelay ? secondFlash : time >= 37 / 24f ? firstFlash : 0);
        backdrop.gameObject.SetActive(time >= Data.FlashDelay);
        float rankAge = time - Data.FlashDelay;
        verticalRank.gameObject.SetActive(rankAge >= 0 && (rankAge >= 0.25f || Mathf.FloorToInt(rankAge * 12) % 2 == 1));
        float verticalHeight = verticalRank.GetChild(0).GetComponent<RectTransform>().sizeDelta.y + 30;
        verticalRank.anchoredPosition = new Vector2(1236, -100 + Mathf.Max(0, rankAge - 1.25f) * 80 % verticalHeight);
        for (int i = 0; i < rankRows.Count; i++)
        {
            float width = rankRows[i].GetChild(0).GetComponent<RectTransform>().sizeDelta.x + 10;
            rankRows[i].anchoredPosition = new Vector2(320 + (i % 2 == 0 ? -7 : 7) * Mathf.Max(0, rankAge) % width, -(60 + 67.5f * i));
        }
        topBar.anchoredPosition = new Vector2(0, 149 * (1 - QuartOut((time - 3 / 24f) / (7 / 24f))));
        DrawHeader(time);
        DrawCounters(time);
        DrawCharacters(time);
    }

    private void DrawHeader(float time)
    {
        float startX = 555 + difficultyWidth + 154;
        float scrolling = Mathf.Max(0, time - Data.CharacterDelay - 2.5f);
        float travel = (startX + titleWidth - 100) / (60 * Mathf.Cos(4.4f * Mathf.Deg2Rad));
        float cycleTime = scrolling > travel ? (scrolling - travel) % (travel + 3 + 1.4f / 3) : -1;
        float ramp = Mathf.Max(0, cycleTime - 3);
        float repeatDistance = ramp <= 0.7f ? ramp * ramp * ramp / (3 * 0.7f * 0.7f) : ramp - 1.4f / 3;
        float distance = scrolling <= travel ? scrolling * 60 : repeatDistance * 60;
        float entrance = cycleTime >= 0 && cycleTime < 3 ? cycleTime : time;
        float dx = -distance * Mathf.Cos(4.4f * Mathf.Deg2Rad);
        float dy = distance * Mathf.Sin(4.4f * Mathf.Deg2Rad);
        float diffY = Mathf.Lerp(-difficulty.sizeDelta.y, 123, ExpoOut((entrance - 0.8f) / 0.5f));
        difficulty.anchoredPosition = new Vector2(35 + dx, -(diffY + dy + 180));
        float smallY = Mathf.Lerp(-smallClear.sizeDelta.y, 118, ExpoOut((entrance - 0.85f) / 0.5f));
        smallClear.anchoredPosition = new Vector2(35 + difficultyWidth + 60 + dx, -(smallY + dy + 180));
        float offset = titleWidth * 0.5f * Mathf.Sin(4.4f * Mathf.Deg2Rad) - 10;
        float titleY = Mathf.Lerp(-61, 98 - offset, ExpoOut((entrance - 0.9f) / 0.5f));
        title.anchoredPosition = new Vector2(startX - 520 + dx + titleWidth / 2, -(titleY + dy + 180 + 30.5f));
        title.gameObject.SetActive(time >= 10 / 24f);
    }

    private void Sound(string name, bool results = false)
    {
        AudioClip clip = Resources.Load<AudioClip>((results ? "VanillaResults/sounds/" : "VanillaFreeplay/audio/") + name);
        if (clip != null) effects.PlayOneShot(clip, OptionsV2.miscVolume);
    }

    public void Accept()
    {
        if (Closing) return;
        Closing = true;
        exitStart = Time.realtimeSinceStartupAsDouble;
        if (musicScheduled && AudioSettings.dspTime < musicStart) introMusic.Stop();
        if (AudioSettings.dspTime < loopStart) loopMusic.Stop();
        if (!Data.rankImproved || Data.storyMode) VanillaPauseStickers.Begin(Complete, Data.Character);
    }

    private void Complete()
    {
        if (!enabled) return;
        enabled = false;
        StartCoroutine(CompleteAfterSceneLoad());
    }

    private IEnumerator CompleteAfterSceneLoad()
    {
        var callback = completed;
        completed = null;
        introMusic.Stop();
        loopMusic.Stop();
        callback?.Invoke();
        yield return null;
        if (this != null) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
        if (white != null) Destroy(white);
        if (backgroundTexture != null) Destroy(backgroundTexture);
        if (flashTexture != null) Destroy(flashTexture);
    }
}
