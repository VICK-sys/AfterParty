using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public sealed class VanillaFreeplay : MonoBehaviour
{
    private sealed class Capsule
    {
        public RectTransform root;
        public VanillaFreeplaySprite body;
        public VanillaFreeplaySprite icon;
        public Text title;
        public Outline glow;
        public VanillaFreeplaySong song;
        public CanvasGroup detail;
        public int index;
    }

    private sealed class ExitTween
    {
        public RectTransform rect;
        public Vector2 start;
        public Vector2 end;
        public float duration;
    }

    public static bool ReturnToFreeplay;
    public static VanillaFreeplay Active { get; private set; }
    private static string rememberedSong;
    private static bool hasRememberedSelection;
    private static string rememberedDifficulty = "Normal";
    private static int rememberedMode = 1;
    private static readonly string[] Filters = { "#", "fav", "ALL", "A-B", "C-D", "E-H", "I-L", "M-N", "O-R", "S", "T", "U-Z" };
    private static readonly string[] Numbers = { "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE" };
    private static readonly string[] Ranks = { "LOSS rank", "GOOD rank", "GREAT rank", "EXCELLENT rank", "PERFECT rank0", "PERFECT rank GOLD" };
    private static readonly float[] CapsuleStretch = { 1.7f, 1.8f, 0.85f, 0.85f, 0.97f, 0.97f, 1 };
    private static readonly float[] CapsuleExitX = { 0.245f, 0.75f, 0.98f, 0.98f, 1.2f, 1.2f, 1.2f };
    private const float IntroDuration = 17f / 24;
    private MenuV2 menu;
    private RectTransform viewport;
    private RectTransform list;
    private RectTransform filters;
    private RectTransform albumRoot;
    private RectTransform difficultyRoot;
    private RectTransform topBar;
    private RectTransform headerRoot;
    private RectTransform scoreRoot;
    private VanillaFreeplayAnimate dj;
    private VanillaFreeplayAnimate album;
    private VanillaFreeplayAnimate stars;
    private VanillaFreeplaySprite albumTitle;
    private VanillaFreeplaySprite backing;
    private VanillaFreeplaySprite card;
    private VanillaFreeplaySprite confirmGlow;
    private VanillaFreeplaySprite confirmText;
    private RectTransform cardRoot;
    private CanvasGroup chrome;
    private readonly List<Capsule> capsules = new List<Capsule>();
    private readonly List<ExitTween> exitTweens = new List<ExitTween>();
    private readonly List<Text> marquees = new List<Text>();
    private readonly List<float> marqueeSpeeds = new List<float>();
    private readonly List<VanillaFreeplaySprite> scoreDigits = new List<VanillaFreeplaySprite>();
    private readonly List<VanillaFreeplaySprite> difficultyDots = new List<VanillaFreeplaySprite>();
    private RectTransform clearDigits;
    private Text emptyText;
    private Text modeHint;
    private Text status;
    private Font pixelFont;
    private Font vcrFont;
    private Font weekFont;
    private AudioSource preview;
    private AudioSource effects;
    private AudioClip randomClip;
    private UnityWebRequest previewLoad;
    private AudioClip ownedClip;
    private Coroutine previewRoutine;
    private float previewStart;
    private float previewEnd;
    private float previewFade;
    private float age;
    private double lastUpdateTime;
    private float confirmAge = -1;
    private float exitAge = -1;
    private float capsuleAge;
    private float selectionAge;
    private float heldTime;
    private float repeatTime;
    private int heldDirection;
    private float previousHorizontal;
    private int previousScore = -1;
    private float displayedScore;
    private int filterIndex = 2;
    private bool ready;
    private bool closing;
    private bool selectingMode;
    private int openedFrame;
    private GameObject modePanel;
    private List<string> difficulties;
    private List<VanillaFreeplaySong> songs;
    private List<VanillaFreeplaySong> filtered;
    public int SelectedIndex { get; private set; }
    public string Difficulty { get; private set; }
    public int Mode { get; private set; }
    public bool Busy => !ready || closing;
    public int SongCount => songs?.Count ?? 0;
    public int VisibleSongCount => filtered?.Count ?? 0;
    public VanillaFreeplaySong SelectedSong => SelectedIndex > 0 && SelectedIndex <= filtered.Count ? filtered[SelectedIndex - 1] : null;
    public string PreviewPath { get; private set; }
    public AudioSource PreviewSource => preview;
    public RectTransform Viewport => viewport;

    public static VanillaFreeplay Open(MenuV2 owner, bool skipIntro = false, string userRoot = null)
    {
        if (Active != null) return Active;
        var root = new GameObject("Vanilla Freeplay", typeof(RectTransform));
        root.SetActive(false);
        var freeplay = root.AddComponent<VanillaFreeplay>();
        freeplay.menu = owner;
        freeplay.Build(userRoot);
        owner.vanillaMenu?.SetFreeplaySuspended(true);
        owner.mainScreen.gameObject.SetActive(!skipIntro);
        owner.playScreen.gameObject.SetActive(false);
        owner.inputBlocker.enabled = false;
        owner.musicSource.Stop();
        root.SetActive(true);
        Active = freeplay;
        freeplay.openedFrame = Time.frameCount;
        freeplay.age = skipIntro ? 2 : 0;
        freeplay.capsuleAge = skipIntro ? 2 : 0;
        freeplay.ready = skipIntro;
        freeplay.RebuildList(true);
        freeplay.dj.Play(skipIntro ? "Idle" : "Intro", skipIntro);
        freeplay.Draw(0);
        freeplay.lastUpdateTime = Time.realtimeSinceStartupAsDouble;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        return freeplay;
    }

    private void Build(string userRoot)
    {
        pixelFont = Resources.Load<Font>("VanillaFreeplay/5by7");
        vcrFont = Resources.Load<Font>("VanillaFreeplay/vcr");
        weekFont = Resources.Load<Font>("VanillaFreeplay/YoureGone");
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 11;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform matte = Rect("Letterbox", transform, 0, 0, 0, 0);
        matte.anchorMin = Vector2.zero;
        matte.anchorMax = Vector2.one;
        matte.offsetMin = matte.offsetMax = Vector2.zero;
        Letterbox(matte, "Left", Vector2.zero, new Vector2(0.5f, 1), Vector2.zero, new Vector2(-640, 0));
        Letterbox(matte, "Right", new Vector2(0.5f, 0), Vector2.one, new Vector2(640, 0), Vector2.zero);
        Letterbox(matte, "Top", new Vector2(0, 0.5f), Vector2.one, new Vector2(0, 360), Vector2.zero);
        Letterbox(matte, "Bottom", Vector2.zero, new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, -360));
        viewport = Rect("Viewport", transform, 0, 0, 1280, 720);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect("Content", viewport, 0, 0, 1280, 720);
        cardRoot = Rect("Card", content, 0, 0, 524, 760);
        card = Sprite("Backing Card", cardRoot, "freeplay/pinkBack", 0, 0);
        card.color = Hex("FFD863");
        Solid("Band", cardRoot, 0, 440, 524, 75, Hex("FEDA00"));
        string[] lines = { "HOT BLOODED IN MORE WAYS THAN ONE", "BOYFRIEND", "PROTECT YO NUTS", "BOYFRIEND", "HOT BLOODED IN MORE WAYS THAN ONE", "BOYFRIEND" };
        float[] ys = { 160, 220, 285, 335, 397, 450 };
        for (int i = 0; i < lines.Length; i++)
        {
            Text line = Label("Backing Text " + i, cardRoot, string.Join("   ", Enumerable.Repeat(lines[i], 8)), 0, ys[i], 9000, 85, i % 2 == 1 ? 60 : 43,
                i % 2 == 1 ? pixelFont : Resources.Load<Font>("VanillaFreeplay/5by7-bold"));
            line.color = i % 2 == 1 ? Hex(i == 5 ? "FEA400" : "FF9963") : i == 2 ? Color.white : Hex("FFF383");
            marquees.Add(line);
            marqueeSpeeds.Add(i % 2 == 1 ? -228 : i == 2 ? 210 : 408);
        }
        confirmGlow = Sprite("Confirm Glow", content, "freeplay/confirmGlow2", -30, 240);
        confirmGlow.gameObject.SetActive(false);
        confirmText = Sprite("Confirm Text", content, "freeplay/glowingText", -8, 115);
        confirmText.gameObject.SetActive(false);
        dj = Animate("Boyfriend DJ", content, "freeplay/freeplay-boyfriend", 0, 0, true);
        backing = Sprite("Dad Backdrop", content, "freeplay/freeplayBGweek1-bf", 387.76f, 0);
        backing.centerScale = false;
        backing.drawScale = 721f / backing.FrameSize.y;
        backing.leftSlant = 90 * backing.drawScale;
        list = Rect("Capsules", content, 0, 0, 1280, 720);
        topBar = Rect("Top Bar", content, 0, -164, 1280, 164);
        topBar.gameObject.AddComponent<Image>().color = Color.black;
        content = Rect("Chrome", content, 0, 0, 1280, 720);
        chrome = content.gameObject.AddComponent<CanvasGroup>();
        difficultyRoot = Rect("Difficulty", content, 0, 0, 400, 180);
        VanillaFreeplaySprite left = Sprite("Previous Difficulty", difficultyRoot, "freeplay/freeplaySelector/freeplaySelector", 20, 70, "arrow pointer loop");
        Hit("Previous Difficulty Button", difficultyRoot, 15, 65, 65, 100, () => ChangeDifficulty(-1));
        Sprite("Next Difficulty", difficultyRoot, "freeplay/freeplaySelector/freeplaySelector", 325, 70, "arrow pointer loop").flipX = true;
        Hit("Next Difficulty Button", difficultyRoot, 315, 65, 65, 100, () => ChangeDifficulty(1));
        filters = Rect("Filters", content, 400, 75, 400, 60);
        emptyText = Label("Empty Filter", content, "NO SONGS", 440, 420, 430, 100, 32, pixelFont);
        albumRoot = Rect("Album", content, 0, 0, 1280, 720);
        album = Animate("Album Art", albumRoot, "freeplay/albumRoll/freeplayAlbum", 920, 220, false);
        stars = Animate("Difficulty Stars", albumRoot, "freeplay/freeplayStars", 950, 209, false);
        albumTitle = Sprite("Album Title", albumRoot, "freeplay/albumRoll/volume1-text", 925, 500, "idle");
        scoreRoot = Rect("Score", content, 0, 0, 1280, 720);
        Sprite("Highscore", scoreRoot, "freeplay/highscore", 860, 70, "highscore small instance 1").loop = false;
        for (int i = 0; i < 7; i++)
        {
            VanillaFreeplaySprite digit = Sprite("Score " + i, scoreRoot, "digital_numbers", 927 + 45 * i, 120, "ZERO DIGITAL");
            digit.drawScale = 0.4f;
            digit.centerScale = false;
            digit.loop = false;
            scoreDigits.Add(digit);
        }
        Sprite("Clear Box", scoreRoot, "freeplay/clearBox", 1165, 65);
        clearDigits = Rect("Clear Digits", scoreRoot, 1185, 87, 95, 30);
        headerRoot = Rect("Header", content, 0, 0, 1280, 64);
        Label("Heading", headerRoot, "FREEPLAY", 8, 3, 280, 61, 48, vcrFont);
        Hit("Back", headerRoot, 0, 0, 285, 64, Close);
        Text ost = Label("OST", headerRoot, "OFFICIAL OST", 600, 3, 670, 61, 48, vcrFont);
        ost.alignment = TextAnchor.UpperRight;
        modeHint = Label("Controls", content, "F: FAVORITE   Q/E: FILTER   TAB: PLAY MODE", 8, 686, 900, 26, 20, pixelFont);
        modeHint.color = new Color(1, 1, 1, 0.75f);
        modeHint.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1, -1);
        status = Label("Status", content, "", 415, 650, 820, 30, 24, pixelFont);
        BuildModes(content);
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        preview = gameObject.AddComponent<AudioSource>();
        preview.playOnAwake = false;
        randomClip = Resources.Load<AudioClip>("VanillaFreeplay/audio/freeplayRandom");
        songs = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"), userRoot ?? Path.Combine(Application.persistentDataPath, "Bundles"));
        difficulties = VanillaFreeplayCatalog.Difficulties(songs);
        Difficulty = difficulties.FirstOrDefault(d => string.Equals(d, rememberedDifficulty, StringComparison.OrdinalIgnoreCase)) ?? difficulties.FirstOrDefault() ?? "Normal";
        Mode = rememberedMode = PlayModes.Normalize(rememberedMode);
        if (!hasRememberedSelection && songs.Count > 0) rememberedSong = songs[0].id;
    }

    private void BuildModes(RectTransform parent)
    {
        RectTransform panel = Rect("Play Mode", parent, 365, 220, 570, 290);
        panel.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.09f, 0.97f);
        Label("Title", panel, "PLAY MODE", 30, 18, 500, 50, 38, vcrFont);
        for (int i = 0; i < PlayModes.Count; i++)
        {
            int value = PlayModes.FromIndex(i);
            string label = PlayModes.Label(value);
            Label(label, panel, (i + 1) + ". " + label, 40, 78 + i * 51, 500, 45, 32, pixelFont);
            Hit("Select " + label, panel, 20, 78 + i * 51, 530, 45, () => SetMode(value));
        }
        modePanel = panel.gameObject;
        modePanel.SetActive(false);
    }

    public void SetMode(int value)
    {
        Mode = rememberedMode = PlayModes.Normalize(value);
        selectingMode = false;
        modePanel.SetActive(false);
        RebuildList(false);
    }

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        float delta = (float)(now - lastUpdateTime);
        lastUpdateTime = now;
        age += delta;
        capsuleAge += delta;
        selectionAge += delta;
        if (!ready && age >= IntroDuration && !closing)
        {
            ready = true;
            menu.mainScreen.gameObject.SetActive(false);
            dj.Play("Idle", true);
            if (albumRoot.gameObject.activeSelf) album.Play("intro", false);
            StartPreview();
        }
        Draw(delta);
        UpdatePreview(delta);
        if (Time.frameCount == openedFrame || Busy) return;
        if (selectingMode)
        {
            for (int i = 0; i < PlayModes.Count; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetMode(PlayModes.FromIndex(i));
            if (BackPressed() || Input.GetKeyDown(KeyCode.Tab)) { selectingMode = false; modePanel.SetActive(false); }
            return;
        }
        if (BackPressed()) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.Tab)) { selectingMode = true; modePanel.SetActive(true); return; }
        float vertical = Input.GetAxisRaw("Vertical");
        int direction = vertical > 0.5f ? -1 : vertical < -0.5f ? 1 : 0;
        if (direction != heldDirection)
        {
            heldDirection = direction;
            heldTime = repeatTime = 0;
            if (direction != 0) MoveSelection(direction);
        }
        else if (direction != 0)
        {
            heldTime += delta;
            repeatTime += delta;
            if (heldTime >= 0.9f && repeatTime >= 0.07f) { repeatTime = 0; MoveSelection(direction); }
        }
        float horizontal = Input.GetAxisRaw("Horizontal");
        if (horizontal > 0.5f && previousHorizontal <= 0.5f) ChangeDifficulty(1);
        if (horizontal < -0.5f && previousHorizontal >= -0.5f) ChangeDifficulty(-1);
        previousHorizontal = horizontal;
        if (Input.mouseScrollDelta.y != 0) MoveSelection(Input.mouseScrollDelta.y > 0 ? -1 : 1);
        if (Input.GetKeyDown(KeyCode.Home)) MoveSelection(-SelectedIndex);
        if (Input.GetKeyDown(KeyCode.End)) MoveSelection(filtered.Count - SelectedIndex);
        if (Input.GetKeyDown(KeyCode.Q)) ChangeFilter(-1);
        if (Input.GetKeyDown(KeyCode.E)) ChangeFilter(1);
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton2)) ToggleFavorite();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.JoystickButton0)) ConfirmSelection();
    }

    private static bool BackPressed() => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.JoystickButton1);

    public void MoveSelection(int change)
    {
        if (Busy || selectingMode) return;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        SelectedIndex = (SelectedIndex + change % (filtered.Count + 1) + filtered.Count + 1) % (filtered.Count + 1);
        if (change != 0) Sound("scrollMenu", 0.4f);
        RefreshSelection();
    }

    public void ChangeDifficulty(int change)
    {
        if (Busy || selectingMode || difficulties.Count == 0) return;
        string selected = SelectedSong?.id;
        int index = difficulties.IndexOf(Difficulty);
        Difficulty = rememberedDifficulty = difficulties[(index + change % difficulties.Count + difficulties.Count) % difficulties.Count];
        rememberedSong = selected;
        Sound("scrollMenu", 0.4f);
        RebuildList(false);
    }

    public void ChangeFilter(int change)
    {
        if (Busy || selectingMode) return;
        filterIndex = (filterIndex + change % Filters.Length + Filters.Length) % Filters.Length;
        rememberedSong = SelectedSong?.id;
        Sound("scrollMenu", 0.4f);
        RebuildList(false);
    }

    public void ToggleFavorite()
    {
        if (Busy || selectingMode || SelectedSong == null) return;
        SelectedSong.ToggleFavorite();
        Sound(SelectedSong.Favorite ? "fav" : "unfav", 1);
        rememberedSong = SelectedSong.id;
        RebuildList(false);
    }

    private void RebuildList(bool initial)
    {
        capsuleAge = initial && ready ? 2 : 0;
        filtered = songs.Where(s => s.Difficulty(Difficulty) != null && VanillaFreeplayCatalog.Matches(s, Filters[filterIndex])).ToList();
        SelectedIndex = rememberedSong == null ? 0 : filtered.FindIndex(s => s.id == rememberedSong) + 1;
        if (SelectedIndex == 0 && rememberedSong != null && filtered.Count > 0) SelectedIndex = 1;
        Clear(list);
        capsules.Clear();
        for (int i = 0; i <= filtered.Count; i++) BuildCapsule(i, i == 0 ? null : filtered[i - 1]);
        RebuildFilters();
        RebuildDifficulty();
        emptyText.gameObject.SetActive(filtered.Count == 0);
        emptyText.text = songs.Count == 0 ? "NO PLAYABLE SONGS" : "NO SONGS IN THIS FILTER";
        RefreshSelection();
        foreach (Capsule capsule in capsules)
            capsule.root.anchoredPosition = initial && ready ? ToUI(Target(capsule.index)) : new Vector2(1280, -130 - 115.6f * capsule.index);
    }

    private void BuildCapsule(int index, VanillaFreeplaySong song)
    {
        RectTransform root = Rect(song?.meta.songName ?? "Random", list, 0, 0, 612, 132);
        var capsule = new Capsule { root = root, song = song, index = index };
        capsule.body = Sprite("Capsule", root, "freeplay/freeplayCapsule/capsule/freeplayCapsule", 0, 0, "mp3 capsule w backing NOT SELECTED");
        capsule.body.drawScale = 0.8f;
        RectTransform detail = Rect("Details", root, 0, 0, 612, 132);
        capsule.detail = detail.gameObject.AddComponent<CanvasGroup>();
        int rank = song == null ? -1 : PlayerPrefs.GetInt("Freeplay.Rank." + song.ScoreKey(Difficulty, Mode), -1);
        int titleSlots = (rank >= 0 ? 1 : 0) + (song != null && song.Favorite ? 1 : 0);
        RectTransform titleMask = Rect("Title Clip", detail, 159.12f, 43, titleSlots == 2 ? 210 : titleSlots == 1 ? 245 : 290, 42);
        titleMask.gameObject.AddComponent<RectMask2D>();
        capsule.title = Label("Title", titleMask, song?.Title(Difficulty) ?? "Random", 0, 0, 1000, 42, 32, pixelFont);
        capsule.glow = capsule.title.gameObject.AddComponent<Outline>();
        capsule.glow.effectColor = Hex("00CCFF");
        capsule.glow.effectDistance = new Vector2(1, -1);
        if (song != null)
        {
            string character = song.Icon(Difficulty);
            string icon = "freeplay/icons/" + character + "pixel";
            while (Resources.Load<Texture2D>("VanillaFreeplay/" + icon) == null && character.Contains("-"))
            {
                character = character.Substring(0, character.LastIndexOf('-'));
                icon = "freeplay/icons/" + character + "pixel";
            }
            if (Resources.Load<Texture2D>("VanillaFreeplay/" + icon) != null)
            {
                capsule.icon = Sprite("Icon", detail, icon, 60, 16, "idle0");
                capsule.icon.drawScale = 2;
                capsule.icon.centerScale = false;
                capsule.icon.fps = 10;
                float originX = character == "parents-christmas" ? 140 : character == "sserafim-kazuha" ? 195 : 100;
                capsule.icon.rectTransform.anchoredPosition = ToUI(new Vector2(160 - originX, 35 - capsule.icon.FrameSize.y / 2));
            }
            Sprite("BPM", detail, "freeplay/freeplayCapsule/bpmtext", 144, 87).drawScale = 0.9f;
            NumberSprites(detail, "BPM Digits", "freeplay/freeplayCapsule/smallnumbers", Mathf.RoundToInt(song.Bpm(Difficulty)), 3, 185, 88.5f, 11, 0.9f);
            Text week = Label("Week", detail, song.week, 291, 87, 118, 27, 18, weekFont);
            week.color = Hex("21242E");
            Sprite("Difficulty Label", detail, "freeplay/freeplayCapsule/difficultytext", 414, 87).drawScale = 0.9f;
            NumberSprites(detail, "Rating", "freeplay/freeplayCapsule/bignumbers", song.Rating(Difficulty), 2, 466, 32, 30, 0.9f);
            if (song.Favorite) Sprite("Favorite", detail, "freeplay/favHeart", rank >= 0 ? 370 : 405, 40, "favorite heart").loop = false;
            if (rank >= 0)
            {
                var badge = Sprite("Rank", detail, "freeplay/rankbadges", 420, 41, Ranks[Mathf.Clamp(rank, 0, 5)]);
                badge.drawScale = 0.9f;
                badge.loop = false;
            }
        }
        Hit("Select", root, 70, 15, 475, 105, () =>
        {
            if (Busy || selectingMode) return;
            if (SelectedIndex == index) ConfirmSelection();
            else MoveSelection(index - SelectedIndex);
        });
        capsules.Add(capsule);
    }

    private void RebuildFilters()
    {
        Clear(filters);
        for (int i = -2; i <= 2; i++)
        {
            int change = i;
            string value = Filters[(filterIndex + i + Filters.Length) % Filters.Length];
            float x = (i + 2) * 80;
            var letters = Animate(value, filters, "freeplay/sortedLetters", x, -10, false);
            letters.PlaySymbol(value.Replace("-", "") + " move", i == 0);
            float brightness = 1 - Mathf.Max(Mathf.Abs(i) / 6f, 0.01f);
            letters.color = new Color(brightness, brightness, brightness);
            if (i != 0)
            {
                letters.rectTransform.localScale = Vector3.one * 0.8f;
                letters.rectTransform.anchoredPosition += new Vector2(letters.BoundsSize.x * 0.1f, -letters.BoundsSize.y * 0.1f);
            }
            if (i < 2) Sprite("Separator", filters, "freeplay/seperator", x + 60, 20);
            Hit("Filter " + value, filters, x - 5, -5, 65, 60, () => ChangeFilter(change));
        }
        var left = Sprite("Previous Filter", filters, "freeplay/miniArrow", -20, 15);
        left.flipX = true;
        Sprite("Next Filter", filters, "freeplay/miniArrow", 380, 15);
        Hit("Previous Filter Button", filters, -30, 0, 30, 55, () => ChangeFilter(-1));
        Hit("Next Filter Button", filters, 375, 0, 35, 55, () => ChangeFilter(1));
    }

    private void RebuildDifficulty()
    {
        Transform old = difficultyRoot.Find("Name");
        if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
        Transform oldDots = difficultyRoot.Find("Dots");
        if (oldDots != null) { oldDots.gameObject.SetActive(false); Destroy(oldDots.gameObject); }
        string path = "freeplay/freeplay" + Difficulty.ToLowerInvariant();
        if (Resources.Load<Texture2D>("VanillaFreeplay/" + path) != null) Sprite("Name", difficultyRoot, path, 90, 80, Difficulty == "Nightmare" ? "idle" : "");
        else Label("Name", difficultyRoot, Difficulty.ToUpperInvariant(), 80, 85, 240, 80, 40, pixelFont);
        RectTransform dots = Rect("Dots", difficultyRoot, 260 - 14.7f * (Mathf.Min(difficulties.Count, 8) - 1) - 75, 170, 250, 60);
        difficultyDots.Clear();
        for (int i = 0; i < difficulties.Count; i++)
        {
            difficultyDots.Add(Sprite("Dot " + i, dots, "freeplay/seperator", i % 8 * 30, i / 8 * 30));
        }
    }

    private void RefreshSelection()
    {
        selectionAge = 0;
        rememberedSong = SelectedSong?.id;
        hasRememberedSelection = true;
        rememberedDifficulty = Difficulty;
        foreach (Capsule capsule in capsules)
        {
            bool selected = capsule.index == SelectedIndex;
            capsule.body.Load("freeplay/freeplayCapsule/capsule/freeplayCapsule", selected ? "mp3 capsule w backing0" : "mp3 capsule w backing NOT SELECTED");
            capsule.body.rectTransform.anchoredPosition = new Vector2(selected ? 0 : 5, 0);
            capsule.detail.alpha = selected ? 1 : 0.6f;
            capsule.glow.enabled = selected;
            capsule.title.rectTransform.anchoredPosition = Vector2.zero;
        }
        RefreshAlbum();
        for (int i = 0; i < difficultyDots.Count; i++)
        {
            bool erect = string.Equals(difficulties[i], "Erect", StringComparison.OrdinalIgnoreCase) || string.Equals(difficulties[i], "Nightmare", StringComparison.OrdinalIgnoreCase);
            bool available = SelectedSong == null || SelectedSong.Difficulty(difficulties[i]) != null;
            difficultyDots[i].color = !available ? new Color(0.07f, 0.07f, 0.07f, 0.33f)
                : difficulties[i] == Difficulty ? Hex(erect ? "C28AFF" : "FAFAFA") : Hex(erect ? "34296A" : "484848");
        }
        Clear(clearDigits);
        float clear = SelectedSong == null ? 0 : PlayerPrefs.GetFloat("Freeplay.Clear." + SelectedSong.ScoreKey(Difficulty, Mode), 0);
        string digits = Mathf.FloorToInt(clear * 100).ToString();
        float x = digits.Length == 1 ? 24 : digits.Length == 3 ? -10 : 0;
        foreach (char digit in digits)
        {
            VanillaFreeplaySprite image = Sprite("Clear " + x, clearDigits, "fonts/freeplay-clear", x, 0, digit + "0000");
            x += image.FrameSize.x;
        }
        modeHint.text = Mode == PlayModes.Boyfriend ? "F: FAVORITE   Q/E: FILTER   TAB: PLAY MODE" : "MODE: " + PlayModes.Label(Mode) + "   TAB: CHANGE";
        modeHint.gameObject.SetActive(Mode != 1);
        status.text = string.Empty;
        if (ready) StartPreview();
    }

    private void RefreshAlbum()
    {
        string id = SelectedSong?.Album(Difficulty);
        Texture2D texture = id == null ? null : Resources.Load<Texture2D>("VanillaFreeplay/freeplay/albumRoll/" + id);
        albumRoot.gameObject.SetActive(texture != null);
        if (texture == null) return;
        album.SetSymbolTexture("album art placeholder", texture);
        album.Play("switch", false);
        albumTitle.Load("freeplay/albumRoll/" + id + "-text", "idle");
        Vector2 titleOffset = id == "volume1" ? new Vector2(8, 0) : id.StartsWith("volume", StringComparison.Ordinal) ? new Vector2(8, -7) : id == "spaghetti" ? new Vector2(-35, 0) : new Vector2(-22, -3);
        albumTitle.rectTransform.anchoredPosition = new Vector2(925 + titleOffset.x, -500 - titleOffset.y);
        if (SelectedSong.Rating(Difficulty) <= 0) stars.SetFrame(1500);
        else stars.PlayFrames((Mathf.Clamp(SelectedSong.Rating(Difficulty), 1, 15) - 1) * 100, 100, true);
    }

    private Vector2 Target(int index)
    {
        int relative = index + 1 - SelectedIndex;
        float y = 120 + 115.6f * relative;
        if (relative < 0) y -= 50;
        else if (relative > 4) y += 10;
        if (index + 1 < SelectedIndex) y -= 100;
        return new Vector2(270 + 60 * Mathf.Sin(relative), y);
    }

    private void Draw(float delta)
    {
        if (exitAge >= 0) { DrawExit(delta); return; }
        float intro = Mathf.Clamp01(age / 0.6f);
        cardRoot.anchoredPosition = new Vector2(-524 * Mathf.Pow(1 - intro, 4), 0);
        backing.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(1280, 387.76f, 1 - Mathf.Pow(1 - Mathf.Clamp01(age / 0.7f), 5)), 0);
        chrome.alpha = ready ? 1 : 0;
        topBar.anchoredPosition = new Vector2(0, 164 - 64 * (1 - Mathf.Pow(1 - Mathf.Clamp01(age / 0.3f), 4)));
        Transform difficultyName = difficultyRoot.Find("Name");
        if (difficultyName != null) ((RectTransform)difficultyName).anchoredPosition = new Vector2(90 - 390 * Mathf.Pow(1 - Mathf.Clamp01((age - IntroDuration) / 0.6f), 4), -80);
        headerRoot.gameObject.SetActive(age >= IntroDuration + 1f / 24);
        scoreRoot.gameObject.SetActive(age >= IntroDuration + 1f / 24);
        stars.enabled = albumTitle.enabled = age >= IntroDuration + 0.75f;
        card.color = ready ? Hex("FFD863") : Hex("FFD4E9");
        cardRoot.Find("Band").gameObject.SetActive(ready && confirmAge < 0);
        float light = ready ? 1 - Mathf.Pow(2, -10 * Mathf.Clamp01((age - IntroDuration) / 0.6f)) : 0;
        backing.color = new Color(light, light, light);
        if (confirmAge >= 0)
        {
            card.color = Color.Lerp(Hex("FFD0D5"), Hex("171831"), Mathf.Clamp01(confirmAge / 0.33f));
            backing.color = Color.Lerp(Hex("A8A8A8"), Hex("646464"), Mathf.Clamp01(confirmAge / 0.5f));
            confirmGlow.color = new Color(1, 1, 1, 0.6f * Mathf.Clamp01(confirmAge / 0.33f));
            confirmText.color = new Color(1, 1, 1, confirmAge < 0.33f ? 0 : Mathf.Lerp(1, 0.4f, (confirmAge - 0.33f) / 0.5f));
        }
        for (int i = 0; i < marquees.Count; i++)
        {
            marquees[i].gameObject.SetActive(ready && confirmAge < 0);
            float period = marquees[i].preferredWidth / 8;
            float x = -Mathf.Repeat(age * marqueeSpeeds[i], period);
            marquees[i].rectTransform.anchoredPosition = new Vector2(x, marquees[i].rectTransform.anchoredPosition.y);
        }
        foreach (Capsule capsule in capsules)
        {
            SetCapsuleStretch(capsule, capsuleAge);
            Vector2 target = ToUI(Target(capsule.index));
            if (confirmAge >= 0 && capsule.index != SelectedIndex) target.x = Mathf.Lerp(target.x, 1536, Mathf.Clamp01(confirmAge / 0.3f));
            Vector2 current = capsule.root.anchoredPosition;
            capsule.root.anchoredPosition = new Vector2(Mathf.Lerp(target.x, current.x, Mathf.Pow(0.01f, delta / 0.256f)), Mathf.Lerp(target.y, current.y, Mathf.Pow(0.01f, delta / 0.192f)));
            if (capsule.index != SelectedIndex || selectionAge < 0.6f) continue;
            float excess = Mathf.Max(0, capsule.title.preferredWidth - ((RectTransform)capsule.title.transform.parent).rect.width);
            float time = Mathf.Repeat(selectionAge - 0.6f, 4.6f);
            float progress = time < 2 ? time / 2 : time < 2.3f ? 1 : time < 4.3f ? 1 - (time - 2.3f) / 2 : 0;
            capsule.title.rectTransform.anchoredPosition = new Vector2(-excess * (1 - Mathf.Cos(progress * Mathf.PI)) / 2, 0);
        }
        int score = SelectedSong == null || Mode == PlayModes.Autoplay ? 0 : PlayerPrefs.GetInt(SelectedSong.ScoreKey(Difficulty, Mode), 0);
        displayedScore = Mathf.Lerp(displayedScore, score, 1 - Mathf.Pow(0.85f, delta * 60));
        int shown = Mathf.Clamp(Mathf.RoundToInt(displayedScore), 0, 9999999);
        if (shown != previousScore)
        {
            string text = shown.ToString("D7");
            for (int i = 0; i < 7; i++)
            {
                scoreDigits[i].Load("digital_numbers", Numbers[text[i] - '0'] + " DIGITAL", false);
                scoreDigits[i].rectTransform.anchoredPosition = new Vector2(927 + 45 * i + (text[i] == '1' ? 15 : 0), -120);
            }
            previousScore = shown;
        }
        if (albumRoot.gameObject.activeSelf && album.Finished) album.Play("idle", true);
    }

    private void StartPreview()
    {
        CancelPreview();
        previewRoutine = StartCoroutine(LoadPreview());
    }

    private void CancelPreview()
    {
        if (previewRoutine != null) StopCoroutine(previewRoutine);
        previewRoutine = null;
        if (previewLoad != null) { previewLoad.Abort(); previewLoad.Dispose(); previewLoad = null; }
        if (preview != null) { preview.Stop(); preview.clip = null; }
        if (ownedClip != null) Destroy(ownedClip);
        ownedClip = null;
        PreviewPath = null;
    }

    private IEnumerator LoadPreview()
    {
        yield return new WaitForSecondsRealtime(0.25f);
        VanillaFreeplaySong song = SelectedSong;
        if (song == null)
        {
            preview.clip = randomClip;
            PreviewPath = "freeplayRandom";
        }
        else
        {
            string path = song.meta.AssetPath("Inst.ogg", Difficulty);
            previewLoad = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS);
            yield return previewLoad.SendWebRequest();
            if (previewLoad.result != UnityWebRequest.Result.Success)
            {
                status.text = "PREVIEW UNAVAILABLE";
                Debug.LogWarning("Freeplay preview failed: " + path + ": " + previewLoad.error);
                previewLoad.Dispose();
                previewLoad = null;
                yield break;
            }
            ownedClip = DownloadHandlerAudioClip.GetContent(previewLoad);
            preview.clip = ownedClip;
            previewLoad.Dispose();
            previewLoad = null;
            PreviewPath = path;
        }
        if (preview.clip == null) yield break;
        float length = preview.clip.length;
        float start = (float?)song?.Details(Difficulty)?["playData"]?["previewStart"] ?? 0;
        float end = (float?)song?.Details(Difficulty)?["playData"]?["previewEnd"] ?? (song == null ? 1 : 0.2f);
        if (start >= end || start < 0 || end > 1) { start = 0; end = song == null ? 1 : 0.2f; }
        previewStart = Mathf.Clamp(start * length, 0, Mathf.Max(0, length - 1));
        previewEnd = Mathf.Clamp(end * length, previewStart + 0.1f, length);
        preview.time = previewStart;
        previewFade = 0;
        preview.volume = 0;
        preview.Play();
        previewRoutine = null;
    }

    private void UpdatePreview(float delta)
    {
        if (preview.clip == null || closing) return;
        previewFade += delta;
        if (preview.time >= previewEnd || (!preview.isPlaying && previewFade > 0.1f))
        {
            preview.time = previewStart;
            previewFade = 0;
            preview.Play();
        }
        preview.volume = 0.7f * OptionsV2.menuVolume * Mathf.Clamp01(previewFade / 2) * Mathf.Clamp01((previewEnd - preview.time) / 2);
    }

    public void ConfirmSelection()
    {
        if (Busy || selectingMode || filtered.Count == 0) return;
        if (SelectedSong == null)
        {
            SelectedIndex = Random.Range(1, filtered.Count + 1);
            RefreshSelection();
        }
        closing = true;
        Capsule selected = capsules[SelectedIndex];
        selected.root.anchoredPosition = ToUI(Target(SelectedIndex));
        selected.body.stretch = Vector2.one;
        selected.body.SetVerticesDirty();
        selected.icon?.TryPlay("confirm0", false, "confirm-hold0");
        capsuleAge = 2;
        confirmAge = 0;
        confirmGlow.gameObject.SetActive(true);
        confirmText.gameObject.SetActive(true);
        cardRoot.Find("Band").gameObject.SetActive(false);
        Sound("confirmMenu", 1);
        CancelPreview();
        dj.Play("Confirm", false);
        StartCoroutine(Launch());
    }

    private IEnumerator Launch()
    {
        VanillaFreeplaySong song = SelectedSong;
        double started = Time.realtimeSinceStartupAsDouble;
        float elapsed = 0;
        while (elapsed < 1)
        {
            elapsed = (float)(Time.realtimeSinceStartupAsDouble - started);
            confirmAge = elapsed;
            foreach (Capsule capsule in capsules)
            {
                if (capsule.index != SelectedIndex) capsule.detail.alpha = Mathf.Clamp01(1 - elapsed * 3);
                else if (menu.vanillaMenu.flashingLights) capsule.title.color = (int)(elapsed * 24) % 2 == 0 ? Color.white : Hex("00CCFF");
            }
            yield return null;
        }
        Song.currentSongMeta = song.meta;
        Song.difficulty = song.Difficulty(Difficulty);
        Song.modeOfPlay = Mode;
        ReturnToFreeplay = true;
        LoadingTransition.instance.Show(() => SceneManager.LoadScene("Game_Backup3"));
    }

    public void Close()
    {
        if (Busy || selectingMode) return;
        closing = true;
        exitAge = 0;
        Sound("cancelMenu", 1);
        CancelPreview();
        ReturnToFreeplay = false;
        MenuV2.startPhase = MenuV2.StartPhase.Nothing;
        menu.mainScreen.gameObject.SetActive(true);
        menu.vanillaMenu?.SetFreeplaySuspended(true);
        cardRoot.Find("Band").gameObject.SetActive(false);
        foreach (Text marquee in marquees) marquee.gameObject.SetActive(false);
        QueueExit(cardRoot, 0.4f, -524);
        QueueExit(dj.rectTransform, 0.5f, -dj.rectTransform.rect.width * 1.6f);
        QueueExit(backing.rectTransform, 0.4f, 1920);
        Transform difficultyName = difficultyRoot.Find("Name");
        if (difficultyName != null) QueueExit((RectTransform)difficultyName, 0.25f, -300);
        foreach (VanillaFreeplaySprite arrow in difficultyRoot.GetComponentsInChildren<VanillaFreeplaySprite>())
            if (arrow.name.EndsWith("Difficulty", StringComparison.Ordinal)) QueueExit(arrow.rectTransform, 0.26f, -arrow.FrameSize.x * 2);
        QueueExit(filters, 0.3f, null, -100);
        foreach (RectTransform score in scoreRoot)
            QueueExit(score, score == clearDigits ? 0.315f : 0.3f, score == clearDigits ? 1344 : 1280);
        QueueExit(album.rectTransform, 0.4f, 1280);
        QueueExit(stars.rectTransform, 0.4f, 1280);
        QueueExit(albumTitle.rectTransform, 0.4f, 1280);
        QueueExit(topBar, 0.2f, 0, -164);
        QueueExit(headerRoot, 0.2f, 0, -164);
        modeHint.gameObject.SetActive(false);
        status.gameObject.SetActive(false);
        emptyText.gameObject.SetActive(false);
        lastUpdateTime = Time.realtimeSinceStartupAsDouble;
    }

    private void QueueExit(RectTransform rect, float duration, float? x = null, float? y = null)
    {
        Vector2 start = rect.anchoredPosition;
        exitTweens.Add(new ExitTween { rect = rect, start = start, end = new Vector2(x ?? start.x, y.HasValue ? -y.Value : start.y), duration = duration });
    }

    private static void SetCapsuleStretch(Capsule capsule, float elapsed)
    {
        int frame = Mathf.Clamp(Mathf.FloorToInt(elapsed * 24) - 1, 0, CapsuleStretch.Length - 1);
        float stretch = elapsed < 1f / 24 ? 1 : CapsuleStretch[frame];
        Vector2 scale = new Vector2(stretch, 1 / stretch);
        if (capsule.body.stretch == scale) return;
        capsule.body.stretch = scale;
        capsule.body.SetVerticesDirty();
    }

    private void DrawExit(float delta)
    {
        exitAge += delta;
        foreach (ExitTween tween in exitTweens)
        {
            float t = Mathf.Clamp01(exitAge / tween.duration);
            float eased = t == 0 || t == 1 ? t : Mathf.Pow(2, 10 * (t - 1));
            tween.rect.anchoredPosition = Vector2.LerpUnclamped(tween.start, tween.end, eased);
        }
        card.color = Color.Lerp(Hex("FFD863"), Hex("FFD4E9"), 1 - Mathf.Pow(1 - Mathf.Clamp01(exitAge / 0.25f), 2));
        foreach (VanillaFreeplaySprite dot in difficultyDots)
        {
            Color tint = dot.color;
            tint.a = Mathf.Pow(1 - Mathf.Clamp01(exitAge / 0.25f), 4);
            dot.color = tint;
        }
        foreach (Capsule capsule in capsules)
        {
            SetCapsuleStretch(capsule, exitAge);
            if (exitAge < 1f / 24) continue;
            int frame = Mathf.Clamp(Mathf.FloorToInt(exitAge * 24) - 1, 0, CapsuleExitX.Length - 1);
            capsule.root.anchoredPosition = new Vector2(1280 * CapsuleExitX[frame], capsule.root.anchoredPosition.y);
        }
        if (exitAge < 0.5f) return;
        menu.musicSource.clip = menu.menuClip;
        menu.musicSource.volume = 0;
        menu.musicSource.Play();
        menu.vanillaMenu?.SetFreeplaySuspended(false);
        viewport.gameObject.SetActive(false);
        GetComponent<Canvas>().enabled = false;
        enabled = false;
        Destroy(gameObject, 0.3f);
        Active = null;
    }

    private static void Letterbox(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = Rect(name, parent, 0, 0, 0, 0);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.gameObject.AddComponent<Image>().color = Color.black;
    }

    private void OnDestroy()
    {
        CancelPreview();
        if (Active == this) Active = null;
    }

    private void Sound(string name, float volume)
    {
        AudioClip clip = Resources.Load<AudioClip>("VanillaFreeplay/audio/" + name);
        if (clip == null && menu.vanillaMenu != null) clip = name == "scrollMenu" ? menu.vanillaMenu.scrollSound : name == "cancelMenu" ? menu.vanillaMenu.cancelSound : menu.vanillaMenu.confirmSound;
        if (clip != null) effects.PlayOneShot(clip, OptionsV2.miscVolume * volume);
    }

    private static void NumberSprites(RectTransform parent, string name, string atlas, int value, int count, float x, float y, float step, float scale)
    {
        string text = Mathf.Clamp(value, 0, (int)Mathf.Pow(10, count) - 1).ToString("D" + count);
        for (int i = 0; i < count; i++)
        {
            var digit = Sprite(name + i, parent, atlas, x + i * step + (text[i] == '1' ? 4 : text[i] == '3' ? 1 : 0), y, Numbers[text[i] - '0']);
            digit.drawScale = scale;
            digit.centerScale = false;
            digit.loop = false;
        }
    }

    private static VanillaFreeplayAnimate Animate(string name, RectTransform parent, string path, float x, float y, bool stage)
    {
        var graphic = Rect(name, parent, x, y, 1280, 720).gameObject.AddComponent<VanillaFreeplayAnimate>();
        graphic.Initialize(path, stage);
        return graphic;
    }

    private static VanillaFreeplaySprite Sprite(string name, RectTransform parent, string path, float x, float y, string prefix = "")
    {
        var graphic = Rect(name, parent, x, y, 0, 0).gameObject.AddComponent<VanillaFreeplaySprite>();
        graphic.Load(path, prefix);
        return graphic;
    }

    private static Text Label(string name, RectTransform parent, string text, float x, float y, float width, float height, int size, Font font)
    {
        Text label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.text = text;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.supportRichText = false;
        label.color = Color.white;
        return label;
    }

    private static void Solid(string name, RectTransform parent, float x, float y, float width, float height, Color color)
    {
        Image image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void Hit(string name, RectTransform parent, float x, float y, float width, float height, Action action)
    {
        Image image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = Color.clear;
        Button button = image.gameObject.AddComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            action();
        });
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Vector2 ToUI(Vector2 point) => new Vector2(point.x, -point.y);
    private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
    private static void Clear(Transform parent) { foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
}
