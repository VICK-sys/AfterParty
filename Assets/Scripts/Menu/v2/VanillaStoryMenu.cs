using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class VanillaStoryMenu : MonoBehaviour
{
    public static VanillaStoryMenu Active { get; private set; }
    private static string rememberedLevel = "tutorial";
    private static string rememberedDifficulty = "normal";
    private readonly List<VanillaStorySprite> titles = new List<VanillaStorySprite>();
    private readonly List<VanillaStoryProp> props = new List<VanillaStoryProp>();
    private readonly Dictionary<string, VanillaStorySprite> difficultySprites = new Dictionary<string, VanillaStorySprite>();
    private List<VanillaStoryLevel> levels;
    private MenuV2 menu;
    private RectTransform viewport;
    private Image background;
    private Image fade;
    private VanillaStoryText scoreText;
    private VanillaStoryText levelText;
    private VanillaStoryText trackText;
    private Text statusText;
    private VanillaStorySprite left;
    private VanillaStorySprite right;
    private VanillaStorySprite difficultySprite;
    private float[] targetY;
    private float previousVertical;
    private float previousHorizontal;
    private float difficultyAge;
    private float backgroundAge;
    private Color backgroundFrom;
    private Color backgroundTo;
    private float statusAge;
    private float confirmAge;
    private double lastUpdate;
    private int lastStep;
    private int enabledFrame;
    private int displayedScore = 12345678;
    private bool leftPressed;
    private bool rightPressed;
    private bool closing;
    public int SelectedIndex { get; private set; }
    public bool Busy { get; private set; }
    public string Difficulty { get; private set; }
    public VanillaStoryLevel SelectedLevel => levels[SelectedIndex];
    public IReadOnlyList<VanillaStoryLevel> Levels => levels;
    public IReadOnlyList<VanillaStoryProp> Props => props;
    public RectTransform Viewport => viewport;
    public string TrackText => trackText.Text;
    public string Status => statusText.text;
    public int DisplayedScore => displayedScore;

    public static VanillaStoryMenu Open(MenuV2 owner)
    {
        if (Active != null) return Active;
        var root = new GameObject("Vanilla Story Mode", typeof(RectTransform));
        root.SetActive(false);
        var story = root.AddComponent<VanillaStoryMenu>();
        story.menu = owner;
        story.Build();
        owner.mainScreen.gameObject.SetActive(false);
        owner.playScreen.gameObject.SetActive(false);
        owner.inputBlocker.enabled = false;
        if (owner.musicSource.clip != owner.menuClip || !owner.musicSource.isPlaying)
        {
            owner.musicSource.clip = owner.menuClip;
            owner.musicSource.loop = true;
            owner.musicSource.Play();
        }
        VanillaStoryCampaign.ReturnToMenu();
        VanillaFreeplay.ReturnToFreeplay = false;
        root.SetActive(true);
        Active = story;
        story.enabledFrame = Time.frameCount;
        story.previousVertical = Input.GetAxisRaw("Vertical");
        story.previousHorizontal = Input.GetAxisRaw("Horizontal");
        story.lastUpdate = Time.realtimeSinceStartupAsDouble;
        story.lastStep = Mathf.FloorToInt(owner.musicSource.time * 102 / 60 * 4);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        return story;
    }

    private void Build()
    {
        levels = VanillaStoryCatalog.Load();
        SelectedIndex = Mathf.Max(0, levels.FindIndex(level => level.id == rememberedLevel));
        Difficulty = rememberedDifficulty;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 11;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        RectTransform matte = Rect("Letterbox", transform, 0, 0, 0, 0);
        matte.anchorMin = Vector2.zero;
        matte.anchorMax = Vector2.one;
        matte.offsetMin = matte.offsetMax = Vector2.zero;
        matte.gameObject.AddComponent<Image>().color = Color.black;
        viewport = Rect("Viewport", transform, 0, 0, 1280, 720);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.gameObject.AddComponent<Image>().raycastTarget = false;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        targetY = new float[levels.Count];
        foreach (VanillaStoryLevel level in levels)
        {
            VanillaStorySprite title = Sprite(level.id, viewport, level.titleAsset, 0, 466);
            title.rectTransform.anchoredPosition = new Vector2((1280 - title.FrameSize.x) / 2, -466);
            titles.Add(title);
        }
        Solid("Header", viewport, 0, 0, 1280, 456, Color.black);
        background = Solid("Level Background", viewport, 0, 56, 1280, 400, Hex(SelectedLevel.background));
        backgroundFrom = backgroundTo = background.color;
        backgroundAge = 1;
        trackText = BitmapLabel("Tracks", "TRACKS", 0, 500);
        trackText.centered = true;
        trackText.color = Hex("#E55777");
        left = Sprite("Previous Difficulty", viewport, "storymenu/ui/arrows", 870, 480, "leftIdle0");
        left.loop = true;
        right = Sprite("Next Difficulty", viewport, "storymenu/ui/arrows", 1245, 480, "rightIdle0");
        right.loop = true;
        DifficultyGraphic("normal");
        int maxProps = levels.Max(level => level.props.Length);
        for (int i = 0; i < maxProps; i++)
        {
            var graphic = Rect("Character " + i, viewport, 0, 0, 0, 0).gameObject.AddComponent<VanillaStorySprite>();
            props.Add(new VanillaStoryProp(graphic));
        }
        scoreText = BitmapLabel("Level Score", "HIGH SCORE: 42069420", 10, 10);
        levelText = BitmapLabel("Level Title", SelectedLevel.name, 0, 10);
        levelText.color = new Color(1, 1, 1, 0.7f);
        statusText = Label("Missing Songs", "", 0, 675, 20);
        statusText.color = Hex("#E55777");
        fade = Solid("Launch Fade", viewport, 0, 0, 1280, 720, Color.clear);
        ChangeLevel(0);
        ChangeDifficulty(0);
    }

    private VanillaStorySprite DifficultyGraphic(string difficulty)
    {
        if (difficultySprites.TryGetValue(difficulty, out VanillaStorySprite graphic)) return graphic;
        graphic = Sprite(difficulty, viewport, "storymenu/difficulties/" + difficulty, 928, 465);
        graphic.rectTransform.SetSiblingIndex(right.transform.GetSiblingIndex() + 1);
        difficultySprites[difficulty] = graphic;
        graphic.gameObject.SetActive(false);
        return graphic;
    }

    public void ChangeLevel(int change)
    {
        if (Busy || closing) return;
        int previous = SelectedIndex;
        int next = SelectedIndex + change;
        SelectedIndex = next < 0 ? levels.Count - 1 : next >= levels.Count ? 0 : next;
        rememberedLevel = SelectedLevel.id;
        for (int i = 0; i < titles.Count; i++) titles[i].color = new Color(1, 1, 1, i == SelectedIndex ? 1 : 0.6f);
        targetY[SelectedIndex] = 480;
        for (int i = SelectedIndex - 1; i >= 0; i--) targetY[i] = targetY[i + 1] - Mathf.Max(titles[i].FrameSize.y + 20, 125);
        for (int i = SelectedIndex + 1; i < titles.Count; i++) targetY[i] = targetY[i - 1] + titles[i - 1].FrameSize.y + 20;
        Color nextColor = Hex(SelectedLevel.background);
        if (nextColor != backgroundTo)
        {
            backgroundFrom = background.color;
            backgroundTo = nextColor;
            backgroundAge = 0;
        }
        for (int i = 0; i < props.Count; i++) props[i].Apply(i < SelectedLevel.props.Length ? SelectedLevel.props[i] : null, i);
        if (previous != SelectedIndex) Sound(menu.vanillaMenu.scrollSound, 0.4f);
        statusText.text = "";
        RefreshText();
    }

    public void ChangeDifficulty(int change)
    {
        if (Busy || closing) return;
        string[] available = SelectedLevel.difficulties;
        int next = Array.IndexOf(available, Difficulty) + change;
        next = next < 0 ? available.Length - 1 : next >= available.Length ? 0 : next;
        bool changed = Difficulty != available[next];
        Difficulty = available[next];
        rememberedDifficulty = Difficulty;
        left.gameObject.SetActive(available.Length > 1);
        right.gameObject.SetActive(available.Length > 1);
        if (changed || difficultySprite == null)
        {
            if (difficultySprite != null) difficultySprite.gameObject.SetActive(false);
            difficultySprite = DifficultyGraphic(Difficulty);
            difficultySprite.gameObject.SetActive(true);
            difficultyAge = 0;
            DrawDifficulty();
        }
        if (changed) Sound(menu.vanillaMenu.scrollSound, 0.4f);
        statusText.text = "";
        RefreshText();
    }

    private void RefreshText()
    {
        trackText.Text = "TRACKS\n\n" + string.Join("\n", SelectedLevel.Tracks);
        Position(trackText.rectTransform, 640 - trackText.rectTransform.sizeDelta.x / 2 - 1280 * 0.33f, 500);
        levelText.Text = SelectedLevel.name;
        Position(levelText.rectTransform, 1280 - levelText.rectTransform.sizeDelta.x - 10, 10);
    }

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        float delta = (float)(now - lastUpdate);
        lastUpdate = now;
        Draw(delta);
        if (Time.frameCount == enabledFrame || closing || VanillaPauseStickers.Active) return;
        if (menu.musicSource.volume < OptionsV2.menuVolume * 0.8f)
            menu.musicSource.volume = Mathf.MoveTowards(menu.musicSource.volume, OptionsV2.menuVolume * 0.8f, delta * 0.5f);
        int step = Mathf.FloorToInt(menu.musicSource.time * 102 / 60 * 4);
        if (step != lastStep)
        {
            if (step < lastStep) lastStep = -1;
            for (int current = Mathf.Max(lastStep + 1, step - 16); current <= step; current++)
                foreach (VanillaStoryProp prop in props) prop.Step(current);
            lastStep = step;
        }
        float vertical = Input.GetAxisRaw("Vertical");
        float horizontal = Input.GetAxisRaw("Horizontal");
        if (!Busy)
        {
            if (vertical > 0.5f && previousVertical <= 0.5f) { ChangeLevel(-1); ChangeDifficulty(0); }
            if (vertical < -0.5f && previousVertical >= -0.5f) { ChangeLevel(1); ChangeDifficulty(0); }
            if (Input.GetKeyDown(KeyCode.Home)) { ChangeLevel(levels.Count); ChangeDifficulty(0); }
            if (Input.GetKeyDown(KeyCode.End)) { ChangeLevel(-levels.Count); ChangeDifficulty(0); }
            int wheel = Mathf.RoundToInt(Mathf.Clamp(Input.mouseScrollDelta.y, -1, 1));
            if (wheel != 0) ChangeLevel(-wheel);
            if (horizontal > 0.5f && previousHorizontal <= 0.5f) ChangeDifficulty(1);
            if (horizontal < -0.5f && previousHorizontal >= -0.5f) ChangeDifficulty(-1);
            SetArrow(left, horizontal < -0.5f, ref leftPressed, "left");
            SetArrow(right, horizontal > 0.5f, ref rightPressed, "right");
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0)) Confirm();
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1)) Close();
        }
        previousVertical = vertical;
        previousHorizontal = horizontal;
    }

    private void Draw(float delta)
    {
        float score = Mathf.Lerp(VanillaStoryCampaign.HighScore(SelectedLevel.id, Difficulty), displayedScore, Mathf.Pow(0.01f, delta / 0.307f));
        int target = VanillaStoryCampaign.HighScore(SelectedLevel.id, Difficulty);
        displayedScore = Mathf.Abs(score - target) <= 1 ? target : (int)score;
        scoreText.Text = "LEVEL SCORE: " + displayedScore.ToString("N0", CultureInfo.InvariantCulture);
        for (int i = 0; i < titles.Count; i++)
        {
            Vector2 position = titles[i].rectTransform.anchoredPosition;
            position.y = Mathf.Lerp(-targetY[i], position.y, Mathf.Pow(0.01f, delta / 0.451f));
            titles[i].rectTransform.anchoredPosition = position;
        }
        backgroundAge += delta;
        background.color = Color.Lerp(backgroundFrom, backgroundTo, 1 - Mathf.Pow(1 - Mathf.Clamp01(backgroundAge / 0.9f), 4));
        difficultyAge += delta;
        DrawDifficulty();
        foreach (VanillaStoryProp prop in props) if (prop.sprite.gameObject.activeSelf) prop.sprite.Tick(delta);
        left.Tick(delta);
        right.Tick(delta);
        if (Busy)
        {
            confirmAge += delta;
            titles[SelectedIndex].color = !menu.vanillaMenu.flashingLights || (int)(confirmAge * 20) % 2 == 0 ? Color.white : Hex("#33FFFF");
        }
        if (statusAge > 0 && (statusAge -= delta) <= 0) statusText.text = "";
    }

    private void DrawDifficulty()
    {
        if (difficultySprite == null) return;
        float t = Mathf.Clamp01(difficultyAge / 0.07f);
        Vector2 normal = difficultySprites["normal"].FrameSize;
        Vector2 size = difficultySprite.FrameSize;
        Position(difficultySprite.rectTransform, 928 + (normal.x - size.x) / 2, Mathf.Lerp(465, 490 - (size.y - normal.y) / 2, t));
        difficultySprite.color = new Color(1, 1, 1, t);
    }

    private static void SetArrow(VanillaStorySprite sprite, bool pressed, ref bool previous, string direction)
    {
        if (pressed == previous) return;
        previous = pressed;
        sprite.Load("storymenu/ui/arrows", direction + (pressed ? "Confirm0" : "Idle0"));
    }

    public void Confirm()
    {
        if (Busy || closing) return;
        if (!VanillaStoryCatalog.TryPlaylist(SelectedLevel, Difficulty, out List<VanillaFreeplaySong> playlist, out string missing))
        {
            Sound(menu.vanillaMenu.cancelSound, 1);
            statusText.text = "Songs not installed: " + missing;
            Fit(statusText);
            Position(statusText.rectTransform, (1280 - statusText.rectTransform.sizeDelta.x) / 2, 675);
            statusAge = 4;
            return;
        }
        Busy = true;
        confirmAge = 0;
        Sound(menu.vanillaMenu.confirmSound, 1);
        foreach (VanillaStoryProp prop in props) prop.Confirm();
        StartCoroutine(Launch(playlist));
    }

    private IEnumerator Launch(List<VanillaFreeplaySong> playlist)
    {
        double started = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - started < 1) yield return null;
        VanillaStoryCampaign.Begin(SelectedLevel.id, Difficulty, playlist);
        while (Time.realtimeSinceStartupAsDouble - started < 1.2)
        {
            fade.color = new Color(0, 0, 0, Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - started - 1) / 0.2f));
            yield return null;
        }
        SceneManager.LoadScene("Game_Backup3");
    }

    public void Close()
    {
        if (Busy || closing) return;
        closing = true;
        Sound(menu.vanillaMenu.cancelSound, 1);
        VanillaStoryCampaign.ReturnToMenu();
        MenuV2.startPhase = MenuV2.StartPhase.Nothing;
        menu.mainScreen.gameObject.SetActive(true);
        menu.vanillaMenu.SetFreeplaySuspended(false);
        if (DiscordController.instance != null) DiscordController.instance.SetMenuState("Idle");
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    private void Sound(AudioClip clip, float volume)
    {
        menu.vanillaMenu.effects.PlayOneShot(clip, OptionsV2.miscVolume * volume);
    }

    private Text Label(string name, string value, float x, float y, int size)
    {
        Text text = Rect(name, viewport, x, y, 1280, 50).gameObject.AddComponent<Text>();
        text.font = Resources.Load<Font>("VanillaFreeplay/vcr");
        text.fontSize = size;
        text.text = value;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignment = TextAnchor.UpperLeft;
        Fit(text);
        return text;
    }

    private VanillaStoryText BitmapLabel(string name, string value, float x, float y)
    {
        var text = Rect(name, viewport, x, y, 0, 0).gameObject.AddComponent<VanillaStoryText>();
        text.Text = value;
        return text;
    }

    private static void Fit(Text text)
    {
        text.rectTransform.sizeDelta = new Vector2(text.preferredWidth + 4, text.preferredHeight + 4);
    }

    private static VanillaStorySprite Sprite(string name, Transform parent, string path, float x, float y, string prefix = "")
    {
        var sprite = Rect(name, parent, x, y, 0, 0).gameObject.AddComponent<VanillaStorySprite>();
        sprite.Load(path, prefix);
        return sprite;
    }

    private static Image Solid(string name, Transform parent, float x, float y, float width, float height, Color color)
    {
        var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        Position(rect, x, y);
        return rect;
    }

    private static void Position(RectTransform rect, float x, float y) => rect.anchoredPosition = new Vector2(x, -y);
    private static Color Hex(string value) => ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
}
