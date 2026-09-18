using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class VanillaTitleScreen : MonoBehaviour
{
    public static VanillaTitleScreen Active { get; private set; }
    public static bool Initialized { get; private set; }
    public static bool EnteredMainMenu { get; private set; }
    public bool IntroSkipped { get; private set; }
    public bool Transitioning { get; private set; }
    public bool CheatActive { get; private set; }
    public bool AttractPlaying => video != null;
    public int CurrentBeat { get; private set; }
    public RectTransform Viewport { get; private set; }
    public string CreditText => string.Join("\n", labels.Select(label => label.Text));
    public VanillaStorySprite Logo => logo;
    public VanillaStorySprite Girlfriend => girlfriend;
    public VanillaFreeplayAnimate Prompt => prompt;
    public float Hue => hue;
    private static int nextVideo;
    private static readonly int[] Cheat = { 0, 1, 0, 1, 2, 3, 2, 3 };
    private static readonly int[] LeftDance = new[] { 30 }.Concat(Enumerable.Range(0, 15)).ToArray();
    private static readonly int[] RightDance = Enumerable.Range(15, 15).ToArray();
    private static readonly string[] Videos = { "riftCollabTrailer", "mobileRelease", "boyfriendEverywhere" };
    private readonly List<VanillaPauseText> labels = new List<VanillaPauseText>();
    private MenuV2 menu;
    private VanillaStorySprite logo;
    private VanillaStorySprite girlfriend;
    private VanillaFreeplayAnimate prompt;
    private RectTransform credits;
    private RawImage newgrounds;
    private Image flash;
    private Image fade;
    private Material hueMaterial;
    private AudioClip ringtone;
    private AudioClip confirm;
    private AudioSource effects;
    private string[] wacky;
    private bool started;
    private bool danceRight;
    private bool animatedNewgrounds;
    private bool attractFading;
    private bool closing;
    private bool previousCursor;
    private float age;
    private float musicAge;
    private float confirmAge;
    private float flashAge = 1;
    private float attractAge;
    private float hue;
    private float previousHorizontal;
    private float previousVertical;
    private float holdTime;
    private int lastBeat;
    private int cheatPosition;
    private int enabledFrame;
    private VideoPlayer video;
    private RawImage videoImage;
    private RenderTexture videoTexture;
    private AudioSource videoAudio;
    private VanillaTitleSkipIndicator skipDial;
    private float videoWait;
    private float fadeVolume;
    private float viewportWidth = 1280;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Active = null;
        Initialized = false;
        EnteredMainMenu = false;
        nextVideo = 0;
    }

    public static VanillaTitleScreen Open(MenuV2 owner)
    {
        if (Active != null) return Active;
        var host = new GameObject("Vanilla Title", typeof(RectTransform));
        host.SetActive(false);
        var title = host.AddComponent<VanillaTitleScreen>();
        title.menu = owner;
        title.previousCursor = Cursor.visible;
        title.Build();
        host.AddComponent<VanillaTitleWindowMotion>();
        owner.mainScreen.gameObject.SetActive(false);
        owner.playScreen.gameObject.SetActive(false);
        owner.optionsScreen.gameObject.SetActive(false);
        owner.inputBlocker.enabled = false;
        title.enabledFrame = Time.frameCount;
        title.previousHorizontal = Input.GetAxisRaw("Horizontal");
        title.previousVertical = Input.GetAxisRaw("Vertical");
        if (!Initialized) owner.musicSource.Stop();
        Active = title;
        host.SetActive(true);
        if (Initialized) title.StartIntro();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        return title;
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Image matte = Solid("Letterbox", transform, Color.black);
        matte.rectTransform.anchorMin = Vector2.zero;
        matte.rectTransform.anchorMax = Vector2.one;
        matte.rectTransform.offsetMin = matte.rectTransform.offsetMax = Vector2.zero;
        Viewport = Rect("Viewport", transform, 0, 0, 1280, 720);
        Viewport.anchorMin = Viewport.anchorMax = Viewport.pivot = new Vector2(0.5f, 0.5f);
        Viewport.gameObject.AddComponent<Image>().raycastTarget = false;
        Viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Solid("Background", Viewport, Color.black);
        hueMaterial = new Material(Resources.Load<Shader>("VanillaTitle/TitleHue"));
        logo = Sprite("Logo", "logoBumpin", "logo bumpin", -150, -100);
        logo.loop = true;
        logo.material = hueMaterial;
        girlfriend = Sprite("Girlfriend", "gfDanceTitle", "gfDance", 512, 50.4f);
        girlfriend.paused = true;
        girlfriend.material = hueMaterial;
        prompt = Rect("Press Enter", Viewport, 100, 576, 0, 0).gameObject.AddComponent<VanillaFreeplayAnimate>();
        prompt.Initialize("title-screen-text", false, "VanillaTitle");
        prompt.Play("Idle", true);
        prompt.material = hueMaterial;
        credits = Rect("Intro Credits", Viewport, 0, 0, 1280, 720);
        Solid("Credits Background", credits, Color.black);
        newgrounds = Rect("Newgrounds", credits, 0, 374.4f, 0, 0).gameObject.AddComponent<RawImage>();
        newgrounds.raycastTarget = false;
        ChooseNewgrounds();
        string[] lines = Resources.Load<TextAsset>("VanillaTitle/introText").text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        wacky = lines[UnityEngine.Random.Range(0, lines.Length)].TrimEnd('\r').Split(new[] { "--" }, StringSplitOptions.None);
        ringtone = Resources.Load<AudioClip>("VanillaTitle/audio/girlfriendsRingtone");
        confirm = Resources.Load<AudioClip>("VanillaTitle/audio/confirmMenu");
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        flash = Solid("White Flash", Viewport, Color.clear);
        fade = Solid("Attract Fade", Viewport, Color.clear);
    }

    private void ChooseNewgrounds()
    {
        bool classic = UnityEngine.Random.value < 0.01f;
        animatedNewgrounds = !classic && UnityEngine.Random.value < 0.3f;
        string name = classic ? "newgrounds_logo_classic" : animatedNewgrounds ? "newgrounds_logo_animated" : "newgrounds_logo";
        Texture2D texture = Resources.Load<Texture2D>("VanillaTitle/" + name);
        newgrounds.texture = texture;
        float scale = classic ? 1 : animatedNewgrounds ? 330f / 600 : 289f / 362;
        Vector2 size = new Vector2(animatedNewgrounds ? 600 : texture.width, texture.height) * scale;
        newgrounds.rectTransform.sizeDelta = size;
        newgrounds.rectTransform.anchoredPosition = new Vector2((1280 - size.x) / 2, -374.4f - (animatedNewgrounds ? 25 : 0));
        newgrounds.uvRect = new Rect(0, 0, animatedNewgrounds ? 0.5f : 1, 1);
        newgrounds.enabled = false;
    }

    private void StartIntro()
    {
        bool returning = Initialized;
        started = true;
        age = 0;
        lastBeat = 0;
        CurrentBeat = 0;
        if (!menu.musicSource.isPlaying || menu.musicSource.clip != menu.menuClip)
        {
            menu.musicSource.clip = menu.menuClip;
            menu.musicSource.loop = true;
            menu.musicSource.volume = 0;
            menu.musicSource.Play();
            musicAge = 0;
        }
        else musicAge = 4;
        Initialized = true;
        if (returning) SkipIntro();
    }

    private void LateUpdate()
    {
        ApplyLayout(((RectTransform)transform).rect.width);
    }

    public void ApplyLayout(float availableWidth)
    {
        float width = Mathf.Clamp(Mathf.Round(availableWidth), 1280, 1600);
        if (Mathf.Approximately(width, viewportWidth)) return;
        viewportWidth = width;
        float cutout = width - 1280;
        Viewport.sizeDelta = new Vector2(width, 720);
        credits.sizeDelta = Viewport.sizeDelta;
        foreach (string name in new[] { "Background", "White Flash", "Attract Fade" })
            ((RectTransform)Viewport.Find(name)).sizeDelta = Viewport.sizeDelta;
        ((RectTransform)credits.Find("Credits Background")).sizeDelta = Viewport.sizeDelta;
        foreach (VanillaPauseText label in labels) label.rectTransform.sizeDelta = new Vector2(width, 100);
        logo.rectTransform.anchoredPosition = new Vector2(-150 + cutout / 2.5f, 100);
        girlfriend.rectTransform.anchoredPosition = new Vector2(width * 0.4f + cutout / 2.5f, -50.4f);
        prompt.rectTransform.anchoredPosition = new Vector2(100 + cutout / 2, -576);
        newgrounds.rectTransform.anchoredPosition = new Vector2((width - newgrounds.rectTransform.sizeDelta.x) / 2, newgrounds.rectTransform.anchoredPosition.y);
        if (videoImage != null) videoImage.rectTransform.anchoredPosition = new Vector2(cutout / 2, 0);
    }

    private void Update()
    {
        Cursor.visible = false;
        float delta = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (AttractPlaying) { UpdateVideo(delta); return; }
        age += delta;
        if (!started)
        {
            if (age >= 1) StartIntro();
            return;
        }
        Tick(delta);
        if (Time.frameCount == enabledFrame || closing || VanillaTitleTransition.BlocksInput) return;
        bool accept = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton0);
        if (accept) Accept();
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1)) menu.QuitGame();
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        ShiftHue(Mathf.Sign(horizontal) * (Mathf.Abs(horizontal) > 0.5f ? delta * 0.1f : 0));
        if (!CheatActive && IntroSkipped)
        {
            if (vertical < -0.5f && previousVertical >= -0.5f) CodePress(1);
            if (vertical > 0.5f && previousVertical <= 0.5f) CodePress(0);
            if (horizontal < -0.5f && previousHorizontal >= -0.5f) CodePress(2);
            if (horizontal > 0.5f && previousHorizontal <= 0.5f) CodePress(3);
        }
        previousHorizontal = horizontal;
        previousVertical = vertical;
    }

    public void Tick(float delta)
    {
        logo.Tick(delta);
        girlfriend.Tick(delta);
        foreach (VanillaPauseText label in labels) label.Tick(delta);
        if (animatedNewgrounds) newgrounds.uvRect = new Rect(Mathf.FloorToInt(age * 4) % 2 * 0.5f, 0, 0.5f, 1);
        flashAge += delta;
        flash.color = new Color(1, 1, 1, Mathf.Max(0, 1 - flashAge));
        musicAge += delta;
        if (!attractFading && musicAge - delta < 4) menu.musicSource.volume = OptionsV2.menuVolume * Mathf.Clamp01(musicAge / 4);
        CurrentBeat = Mathf.FloorToInt(menu.musicSource.time * (CheatActive ? 160 : 102) / 60);
        if (CurrentBeat < lastBeat) lastBeat = -1;
        if (CurrentBeat > lastBeat)
        {
            for (int beat = lastBeat + 1; beat <= CurrentBeat; beat++) Beat(beat, beat == CurrentBeat);
            lastBeat = CurrentBeat;
        }
        if (closing) return;
        if (Transitioning)
        {
            confirmAge += delta;
            if (confirmAge >= 2) MoveToMainMenu();
        }
        if (!CheatActive && !closing && age >= 37.5f && !attractFading)
        {
            attractFading = true;
            fadeVolume = menu.musicSource.volume;
        }
        if (attractFading)
        {
            attractAge += delta;
            float progress = Mathf.Clamp01(attractAge / 2);
            fade.color = new Color(0, 0, 0, progress);
            menu.musicSource.volume = fadeVolume * (1 - progress);
            if (progress >= 1) StartAttract();
        }
    }

    private void Beat(int beat, bool animate)
    {
        if (!IntroSkipped)
        {
            switch (beat)
            {
                case 1: AddText("The"); AddText("Funkin Crew Inc"); break;
                case 3: AddText("presents"); break;
                case 4: ClearText(); break;
                case 5: AddText("In association"); AddText("with"); break;
                case 7: AddText("newgrounds"); newgrounds.enabled = true; break;
                case 8: ClearText(); newgrounds.enabled = false; break;
                case 9: AddText(wacky[0]); break;
                case 11: AddText(wacky.Length > 1 ? wacky[1].Trim() : ""); break;
                case 12: ClearText(); break;
                case 13: AddText("Friday"); break;
                case 14: AddText(wacky[0] == "trending" ? "Nigth" : "Night"); break;
                case 15: AddText("Funkin"); break;
                case 16: SkipIntro(); break;
            }
        }
        if (!IntroSkipped || !animate) return;
        if (CheatActive && beat % 2 == 0) ShiftHue(0.125f);
        logo.Load("logoBumpin", "logo bumpin", null, "VanillaTitle");
        danceRight = !danceRight;
        girlfriend.Load("gfDanceTitle", "gfDance", danceRight ? RightDance : LeftDance, "VanillaTitle");
    }

    private void AddText(string value)
    {
        var label = Rect("Credit " + labels.Count, credits, 0, 200 + labels.Count * 60, viewportWidth, 100).gameObject.AddComponent<VanillaPauseText>();
        label.Initialize(true);
        label.centered = true;
        label.SetText(value);
        labels.Add(label);
    }

    private void ClearText()
    {
        foreach (VanillaPauseText label in labels) { label.gameObject.SetActive(false); Destroy(label.gameObject); }
        labels.Clear();
    }

    public void SkipIntro()
    {
        if (!started || IntroSkipped) return;
        IntroSkipped = true;
        credits.gameObject.SetActive(false);
        Flash();
    }

    public void Accept()
    {
        if (!started || closing || AttractPlaying || VanillaTitleTransition.BlocksInput) return;
        if (!IntroSkipped) { SkipIntro(); return; }
        if (Transitioning) { MoveToMainMenu(); return; }
        prompt.Play("Confirm", true);
        Flash();
        effects.PlayOneShot(confirm, OptionsV2.miscVolume * 0.7f);
        Transitioning = true;
        confirmAge = 0;
    }

    public void ShiftHue(float change)
    {
        if (change == 0) return;
        hue += change;
        hueMaterial.SetFloat("_Hue", hue);
        foreach (Graphic graphic in new Graphic[] { logo, girlfriend, prompt })
            if (graphic.materialForRendering != null) graphic.materialForRendering.SetFloat("_Hue", hue);
    }

    public void CodePress(int direction)
    {
        if (!IntroSkipped || CheatActive || closing || AttractPlaying || VanillaTitleTransition.BlocksInput) return;
        if (direction != Cheat[cheatPosition]) { cheatPosition = 0; return; }
        if (++cheatPosition < Cheat.Length) return;
        CheatActive = true;
        menu.musicSource.clip = ringtone;
        menu.musicSource.loop = true;
        menu.musicSource.volume = 0;
        menu.musicSource.Play();
        musicAge = 0;
        lastBeat = -1;
        attractFading = false;
        fade.color = Color.clear;
        Flash();
        effects.PlayOneShot(confirm, OptionsV2.miscVolume * 0.7f);
    }

    private void Flash()
    {
        flashAge = menu.vanillaMenu.flashingLights && !VanillaTitleTransition.IsRunning ? 0 : 1;
        flash.color = new Color(1, 1, 1, 1 - flashAge);
    }

    public void MoveToMainMenu()
    {
        if (closing || VanillaTitleTransition.BlocksInput) return;
        if (!VanillaTitleTransition.Begin(CompleteMainMenuTransition)) return;
        closing = true;
        attractFading = false;
        fade.color = Color.clear;
    }

    private void CompleteMainMenuTransition()
    {
        EnteredMainMenu = true;
        if (menu.musicSource.clip != menu.menuClip)
        {
            menu.musicSource.clip = menu.menuClip;
            menu.musicSource.loop = true;
            menu.musicSource.volume = OptionsV2.menuVolume * 0.8f;
            menu.musicSource.Play();
        }
        menu.mainScreen.gameObject.SetActive(true);
        gameObject.SetActive(false);
        Active = null;
        Destroy(gameObject);
    }

    private void StartAttract()
    {
        if (AttractPlaying || closing) return;
        menu.musicSource.Stop();
        videoImage = Rect("Attract Video", Viewport, (viewportWidth - 1280) / 2, 0, 1280, 720).gameObject.AddComponent<RawImage>();
        videoImage.raycastTarget = false;
        videoTexture = new RenderTexture(1280, 720, 0);
        videoImage.texture = videoTexture;
        videoAudio = videoImage.gameObject.AddComponent<AudioSource>();
        videoAudio.playOnAwake = false;
        videoAudio.volume = OptionsV2.menuVolume;
        video = videoImage.gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.source = VideoSource.Url;
        video.url = Path.Combine(Application.streamingAssetsPath, "VanillaTitle", Videos[nextVideo] + ".mp4");
        nextVideo = (nextVideo + 1) % Videos.Length;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = videoTexture;
        video.aspectRatio = VideoAspectRatio.Stretch;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.SetTargetAudioSource(0, videoAudio);
        video.loopPointReached += _ => EndAttract();
        video.errorReceived += (_, message) => { Debug.LogWarning("Title video: " + message); EndAttract(); };
        video.prepareCompleted += player => player.Play();
        skipDial = Rect("Hold To Skip", videoImage.transform, 1160, 600, 80, 80).gameObject.AddComponent<VanillaTitleSkipIndicator>();
        skipDial.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        skipDial.rectTransform.anchoredPosition = new Vector2(1200, -640);
        skipDial.SetAmount(0);
        skipDial.raycastTarget = false;
        holdTime = 0;
        videoWait = 0;
        video.Prepare();
    }

    private void UpdateVideo(float delta)
    {
        videoWait += delta;
        if (!video.isPrepared && videoWait > 15) { EndAttract(); return; }
        bool held = Input.anyKey && !Input.GetKey(KeyCode.Minus) && !Input.GetKey(KeyCode.Equals) && !Input.GetKey(KeyCode.Alpha0);
        holdTime = held ? holdTime + delta : Mathf.Lerp(holdTime, -0.1f, Mathf.Clamp01(delta * 3));
        holdTime = Mathf.Clamp(holdTime, 0, 1.5f);
        float amount = Mathf.Clamp01(holdTime / 1.5f * 1.025f);
        skipDial.SetAmount(amount);
        if (amount >= 1) EndAttract();
    }

    public void EndAttract()
    {
        if (!AttractPlaying) return;
        StopVideo();
        attractFading = false;
        attractAge = 0;
        fade.color = Color.clear;
        Transitioning = false;
        IntroSkipped = false;
        danceRight = false;
        cheatPosition = 0;
        ShiftHue(-hue);
        logo.Load("logoBumpin", "logo bumpin", null, "VanillaTitle");
        girlfriend.Load("gfDanceTitle", "gfDance", null, "VanillaTitle");
        girlfriend.paused = true;
        prompt.Play("Idle", true);
        StartIntro();
        enabledFrame = Time.frameCount;
    }

    private void StopVideo()
    {
        if (video != null) video.Stop();
        video = null;
        if (videoImage != null) { videoImage.gameObject.SetActive(false); Destroy(videoImage.gameObject); }
        if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
    }

    private void OnDestroy()
    {
        StopVideo();
        if (hueMaterial != null) Destroy(hueMaterial);
        if (Active == this) Active = null;
        Cursor.visible = previousCursor;
    }

    private VanillaStorySprite Sprite(string name, string path, string prefix, float x, float y)
    {
        var sprite = Rect(name, Viewport, x, y, 0, 0).gameObject.AddComponent<VanillaStorySprite>();
        sprite.Load(path, prefix, null, "VanillaTitle");
        return sprite;
    }

    private static Image Solid(string name, Transform parent, Color color)
    {
        Image image = Rect(name, parent, 0, 0, 1280, 720).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
}
