using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[DefaultExecutionOrder(300)]
public sealed partial class VanillaCampaignPresentation : MonoBehaviour
{
    public bool Busy { get; private set; }
    public bool OutroFinished { get; private set; }
    public bool OwnsCamera => winterIntro || winterTransition || weekendCamera || eggnogCamera || spaghettiCamera;
    private bool eggnogCamera;
    public float HudAlpha { get; private set; } = 1;
    private bool winterIntro;
    private bool winterTransition;
    private double transitionStart;
    private Vector3 cameraReturn;
    private float cameraReturnSize;
    private SpriteRenderer[] hudSprites;
    private CanvasGroup hudGroup;
    private MaterialPropertyBlock hudProperties;
    private bool playedIntro;
    private Image introCover;
    private bool introPrepared;
    private bool introHud;
    private bool introBattle;
    private bool IntroHud => introPrepared ? introHud : song.uiCamera.enabled;
    private bool IntroBattle => introPrepared ? introBattle : song.battleCanvas.enabled;
    private bool outroStarted;
    private bool advanceRequested;
    private float dialogueAge;
    private Image rosesFade;
    private Song song;
    private Canvas canvas;
    private RectTransform viewport;
    private AudioSource sound;
    private AudioSource music;
    private RawImage countdownImage;
    private Material retryFadeMaterial;
    private Canvas retryCanvas;
    private readonly System.Collections.Generic.List<AudioClip> clips = new System.Collections.Generic.List<AudioClip>();
    private string Root(int week) => Path.Combine(Application.streamingAssetsPath, week == 9 ? "Bundles/SpaghettiAssets" : "Bundles/Week" + week + "Assets");
    private bool Advance
    {
        get
        {
            bool value = advanceRequested || VanillaControls.Pressed("CUTSCENE_ADVANCE") || VanillaControls.Pressed("BACK");
            advanceRequested = false;
            return value;
        }
    }

    public void AdvanceDialogue() => advanceRequested = true;
    public bool TakeAdvanceInput() => Advance;

    public static bool HasIntro(string id, string variation, bool pixel, bool week3, bool campaignStage, bool story)
    {
        bool pico = variation == "pico";
        return id == "spaghetti" && campaignStage || id == "winter-horrorland"
            || pico && (week3 || id == "stress")
            || story && (id == "darnell" || id == "ugh" || id == "guns" || id == "stress")
            || pixel && (story || pico);
    }

    public void PrepareIntro(Song owner)
    {
        var playback = owner.vanillaPlayback;
        if (playback == null || playedIntro || Pause.PlayedCampaignIntro || Pause.DeathCount > 0 || introPrepared) return;
        string id = playback.SongId;
        bool hasIntro = HasIntro(id, playback.Variation, playback.IsPixel, playback.IsWeek3,
            playback.CampaignStage != null, VanillaStoryCampaign.Running);
        if (!hasIntro) return;
        Initialize(owner);
        introHud = owner.uiCamera.enabled;
        introBattle = owner.battleCanvas.enabled;
        introPrepared = Busy = true;
        owner.uiCamera.enabled = owner.battleCanvas.enabled = false;
        introCover = Overlay(Color.black);
    }

    private Image TakeIntroCover(Color color)
    {
        Image cover = introCover != null ? introCover : Overlay(color);
        introCover = null;
        cover.color = color;
        return cover;
    }

    private void ReleaseIntroCover()
    {
        if (introCover == null) return;
        Destroy(introCover.gameObject);
        introCover = null;
    }

    private void Update()
    {
        ReturnWeekendCamera();
        if (winterTransition && (song.IsCountingDown || song.songStarted))
        {
            float t = Mathf.Clamp01((float)((song.SongPosition - Pause.GlobalOffset - transitionStart) / 2000));
            float eased = t < .5f ? 2*t*t : 1-Mathf.Pow(-2*t+2,2)/2;
            if (song.vanillaPlayback.CampaignStage != null)
            {
                cameraReturn = song.vanillaPlayback.CameraFocusTarget;
                cameraReturnSize = song.vanillaPlayback.CameraSize;
            }
            song.mainCamera.transform.position = Vector3.Lerp(new Vector3(4,20.5f,-10), cameraReturn, eased);
            song.mainCamera.orthographicSize = Mathf.Lerp(3.6f/2.5f, cameraReturnSize, eased);
            FadeHud(eased);
            if (t >= 1) winterTransition = false;
        }
        if (song == null || !song.songStarted || !VanillaStoryCampaign.Running || song.vanillaPlayback.SongId != "roses") return;
        float beat = song.vanillaPlayback.BeatAt(song.SongPosition);
        if (beat < 180) return;
        if (rosesFade == null) rosesFade = Overlay(new Color32(255,27,49,0));
        float elapsed = (beat-180)*song.beatsPerSecond;
        rosesFade.color = new Color(1,27/255f,49/255f,Mathf.Clamp01(elapsed/2));
    }

    private void FadeHud(float alpha)
    {
        HudAlpha = alpha;
        if (hudSprites == null)
        {
            hudProperties = new MaterialPropertyBlock();
            int layer = song.player1NoteSprites[0].gameObject.layer;
            hudSprites = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(renderer => renderer.gameObject.layer == layer).ToArray();
            hudGroup = song.battleCanvas.GetComponent<CanvasGroup>();
            if (hudGroup == null) hudGroup = song.battleCanvas.gameObject.AddComponent<CanvasGroup>();
        }
        hudGroup.alpha = alpha;
        foreach (var renderer in hudSprites)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(hudProperties);
            hudProperties.SetFloat("_HudOpacity", alpha);
            renderer.SetPropertyBlock(hudProperties);
        }
    }

    private void Initialize(Song owner)
    {
        song = owner;
        if (canvas != null) return;
        var obj = new GameObject("Campaign Presentation");
        obj.transform.SetParent(transform, false);
        canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        var scaler = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        viewport = Rect("Viewport", canvas.transform, 0, 0, 1280, 720);
        viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
        viewport.pivot = new Vector2(.5f, .5f);
        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.outputAudioMixerGroup = song.oopsSource.outputAudioMixerGroup;
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.outputAudioMixerGroup = song.musicSources[0].outputAudioMixerGroup;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y);
        rect.sizeDelta = new Vector2(width,height);
        return rect;
    }

    private Image Overlay(Color color)
    {
        var image = Rect("Overlay", viewport, -1280, -720, 3840, 2160).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private IEnumerator LoadAudio(int week, string name, Action<AudioClip> assign, AudioType type = AudioType.OGGVORBIS)
    {
        using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(Root(week), "audio", name + (type == AudioType.WAV ? ".wav" : ".ogg"))).AbsoluteUri, type))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
            var clip = DownloadHandlerAudioClip.GetContent(request);
            clips.Add(clip);
            assign(clip);
        }
    }

    public IEnumerator Intro(Song owner)
    {
        try { yield return RunIntro(owner); }
        finally
        {
            introPrepared = false;
            ReleaseIntroCover();
        }
    }

    private IEnumerator RunIntro(Song owner)
    {
        Initialize(owner);
        if (playedIntro || Pause.PlayedCampaignIntro || Pause.DeathCount > 0) yield break;
        playedIntro = true;
        Pause.PlayedCampaignIntro = true;
        string id = song.vanillaPlayback.SongId;
        if (id == "spaghetti" && song.vanillaPlayback.CampaignStage != null)
        {
            yield return SpaghettiIntro();
            yield break;
        }
        bool pico = song.vanillaPlayback.Variation == "pico";
        if (pico && song.vanillaPlayback.IsWeek3)
        {
            yield return PicoDoppelgangerIntro();
            yield break;
        }
        if (pico && id == "stress")
        {
            yield return Week7Video("stress-pico", keepCovered: true);
            yield break;
        }
        if (id == "darnell" && VanillaStoryCampaign.Running)
        {
            yield return Week7Video(id, 8, 0, true);
            yield return DarnellIntro();
            yield break;
        }
        if (id == "ugh" || id == "guns" || id == "stress")
        {
            if (VanillaStoryCampaign.Running) yield return Week7Video(id, keepCovered: true);
            yield break;
        }
        if (id != "winter-horrorland" && (!song.vanillaPlayback.IsPixel || !VanillaStoryCampaign.Running && !pico)) yield break;
        Busy = true;
        bool hud = IntroHud;
        bool battle = IntroBattle;
        song.uiCamera.enabled = false;
        song.battleCanvas.enabled = false;
        if (id == "winter-horrorland")
        {
            winterIntro = true;
            cameraReturn = song.mainCamera.transform.position;
            cameraReturnSize = song.mainCamera.orthographicSize;
            var black = TakeIntroCover(Color.black);
            yield return LoadAudio(5, "Lights_Turn_On", clip => sound.clip = clip);
            yield return new WaitForSeconds(.1f);
            Destroy(black.gameObject);
            song.mainCamera.transform.position = new Vector3(4,20.5f,-10);
            song.mainCamera.orthographicSize = 3.6f / 2.5f;
            sound.Play();
            while (sound.isPlaying) yield return null;
            FadeHud(0);
            winterIntro = false;
            winterTransition = true;
        }
        else
        {
            if (id == "senpai")
            {
                var black = TakeIntroCover(Color.black);
                yield return new WaitForSeconds(.25f);
                for (float time = 0; time < 2; time += Time.deltaTime)
                {
                    black.color = new Color(0,0,0,1 - Mathf.Floor(time / 2 * 12) / 12);
                    yield return null;
                }
                Destroy(black.gameObject);
            }
            if (id == "roses")
            {
                yield return LoadAudio(6, "ANGRY_TEXT_BOX", clip => sound.clip = clip);
                sound.PlayOneShot(sound.clip);
            }
            if (id == "thorns") yield return Explosion();
            yield return Dialogue(id + (pico ? "-pico" : ""));
        }
        song.uiCamera.enabled = hud;
        song.battleCanvas.enabled = battle;
        Busy = false;
    }

    private IEnumerator Explosion()
    {
        var red = TakeIntroCover(new Color32(255,27,49,255));
        var actor = Rect("Senpai Explosion", viewport, 0,0,1280,720).gameObject.AddComponent<VanillaDialogueGraphic>();
        actor.Load(Path.Combine(Root(6), "dialogue/explosion"));
        actor.DrawScale = 6;
        actor.rectTransform.anchoredPosition = new Vector2((1280-actor.Size.x*6)/2+actor.Size.x*6/5, -(720-actor.Size.y*6)/2);
        actor.enabled = false;
        yield return LoadAudio(6, "Senpai_Dies", clip => sound.clip = clip);
        actor.enabled = true;
        for (float time = 0; time < 2.1f; time += Time.deltaTime)
        {
            actor.Play("idle");
            actor.color = new Color(1,1,1,Mathf.Min(1,Mathf.Floor(time/.3f)*.15f));
            yield return null;
        }
        actor.color = Color.white;
        actor.Play("idle");
        sound.Play();
        Image flash = null;
        while (sound.isPlaying)
        {
            if (sound.time >= 3.2f)
            {
                if (flash == null) flash = Overlay(Color.clear);
                flash.color = new Color(1,1,1,Mathf.Min(1,Mathf.Floor((sound.time-3.2f)/1.4f*8)/8));
            }
            yield return null;
        }
        Destroy(actor.gameObject);
        Destroy(red.gameObject);
        if (flash != null) Destroy(flash.gameObject);
        var black = Overlay(Color.black);
        for (float time = 0; time < 1.6f; time += Time.deltaTime)
        {
            black.color = new Color(0,0,0,1-Mathf.Min(1,Mathf.Floor(time/1.4f*6)/6));
            yield return null;
        }
        Destroy(black.gameObject);
    }

    private IEnumerator Dialogue(string id)
    {
        string root = Path.Combine(Root(6), "dialogue");
        if (!VanillaPreferences.Naughtyness && File.Exists(Path.Combine(root,id+"-censored.json"))) id += "-censored";
        JObject conversation = JObject.Parse(File.ReadAllText(Path.Combine(root, id + ".json")));
        var backdrop = Overlay(new Color(179/255f,223/255f,216/255f,0));
        dialogueAge = 0;
        string track = (string)conversation["music"]["asset"];
        if (!string.IsNullOrEmpty(track))
        {
            yield return LoadAudio(6, track, clip => music.clip = clip);
            music.volume = 0;
            music.Play();
        }
        yield return LoadAudio(6, "pixelText", clip => sound.clip = clip);
        AudioClip click = null;
        yield return LoadAudio(6, "textboxClick", clip => click = clip);
        var portrait = Rect("Speaker", viewport,0,0,1280,720).gameObject.AddComponent<VanillaDialogueGraphic>();
        var box = Rect("Dialogue Box",viewport,-20,20,1280,720).gameObject.AddComponent<VanillaDialogueGraphic>();
        var text = Rect("Dialogue",viewport,205,470,900,200).gameObject.AddComponent<VanillaDialogueText>();
        text.font = Resources.Load<Font>("FunkinHud/Pixel/dialogue");
        text.fontSize = 32;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        ReleaseIntroCover();
        string lastBox = null;
        string lastSpeaker = null;
        foreach (JToken line in conversation["dialogue"])
        {
            string speaker = (string)line["speaker"];
            if (speaker == "senpai-bwuh") music.Pause();
            else if (music.clip != null && !music.isPlaying) music.UnPause();
            string boxId = (string)line["box"];
            bool continuing = boxId == lastBox;
            if (!continuing) box.Load(Path.Combine(root,"boxes",boxId));
            lastBox = boxId;
            JObject boxData = JObject.Parse(File.ReadAllText(Path.Combine(root,"boxes",boxId,"data.json")));
            box.DrawScale = (float)boxData["scale"];
            box.rectTransform.anchoredPosition = new Vector2(640+(float)boxData["offsets"][0],-(360+(float)boxData["offsets"][1]));
            box.FlipX = (bool?)boxData["flipX"] ?? false;
            box.FlipY = (bool?)boxData["flipY"] ?? false;
            box.Play(continuing ? "click" : (string)line["boxAnimation"]);
            if (continuing && boxId == "roses") sound.PlayOneShot(click,.6f);
            text.text = "";
            if (!continuing) portrait.color = Color.clear;
            while (!box.Finished && !box.Looping)
            {
                FadeDialogue(backdrop);
                yield return null;
            }
            JObject character = JObject.Parse(File.ReadAllText(Path.Combine(root,"speakers",speaker,"data.json")));
            if (speaker != lastSpeaker) portrait.Load(Path.Combine(root,"speakers",speaker));
            lastSpeaker = speaker;
            portrait.FlipX = (bool?)character["flipX"] ?? false;
            portrait.FlipY = (bool?)character["flipY"] ?? false;
            portrait.DrawScale = (float)character["scale"];
            portrait.rectTransform.anchoredPosition = new Vector2((1280-portrait.Size.x)/2+(float)character["offsets"][0],
                -((720-portrait.Size.y)/2+(float)character["offsets"][1]));
            box.Play("speaking");
            portrait.color = Color.white;
            portrait.Play((string)line["speakerAnimation"]);
            text.rectTransform.anchoredPosition = box.rectTransform.anchoredPosition + new Vector2((float)boxData["text"]["offsets"][0],-(float)boxData["text"]["offsets"][1]);
            text.rectTransform.sizeDelta = new Vector2((float)boxData["text"]["width"],200);
            ColorUtility.TryParseHtmlString((string)boxData["text"]["color"],out Color color);
            ColorUtility.TryParseHtmlString((string)boxData["text"]["shadowColor"], out Color shadowColor);
            float shadowWidth = (float?)boxData["text"]["shadowWidth"] ?? 2;
            text.Configure(color, shadowColor, shadowWidth, (int?)boxData["text"]["size"] ?? 32);
            string content = "";
            foreach (JToken segment in line["text"])
            {
                int previousLength = text.text.Length;
                content += (string)segment;
                string wrapped = text.Prepare(content);
                int shown = previousLength;
                float age = 0;
                while (shown < wrapped.Length)
                {
                    age += Time.deltaTime;
                    int next = Mathf.Min(wrapped.Length,previousLength + (int)(age / (.05f * ((float?)line["speed"] ?? 1))));
                    if (Advance) next = wrapped.Length;
                    if (next > shown) sound.PlayOneShot(sound.clip,.6f);
                    shown = next;
                    text.text = wrapped.Substring(0,shown);
                    FadeDialogue(backdrop);
                    yield return null;
                }
                box.Play("sentenceEnd");
                yield return null;
                while (!Advance)
                {
                    FadeDialogue(backdrop);
                    if (box.Finished && box.Animation == "sentenceEnd") box.Play("idle");
                    yield return null;
                }
                yield return null;
                if (segment != line["text"].Last) box.Play("speaking");
            }
        }
        if (lastBox == "roses") sound.PlayOneShot(click,.6f);
        box.Play("exit");
        text.enabled = false;
        for (float time = 0; time < 1; time += Time.deltaTime)
        {
            backdrop.color = new Color(backdrop.color.r,backdrop.color.g,backdrop.color.b,.75f*(1-time));
            box.color = portrait.color = text.color = new Color(1,1,1,1-time);
            music.volume = 1-time;
            yield return null;
        }
        music.Stop();
        Destroy(backdrop.gameObject);
        Destroy(box.gameObject);
        Destroy(portrait.gameObject);
        Destroy(text.gameObject);
    }

    private void FadeDialogue(Image backdrop)
    {
        dialogueAge += Time.deltaTime;
        music.volume = Mathf.Min(1,dialogueAge/2);
        backdrop.color = new Color(179/255f,223/255f,216/255f,.75f*Mathf.Min(1,Mathf.Floor(dialogueAge/2*10)/10));
    }

    public Text CreateSkipText()
    {
        var label = Rect("Skip Cutscene",viewport,900,618,360,65).gameObject.AddComponent<Text>();
        label.font = Resources.Load<Font>("FunkinHud/Countdown/vcr");
        label.fontSize = 40;
        label.alignment = TextAnchor.MiddleRight;
        label.text = "Skip [ Enter ]";
        label.color = Color.clear;
        return label;
    }

    public Image CreateFade() => Overlay(Color.clear);

    public void ResetForRetry(Song owner)
    {
        Initialize(owner);
        StopAllCoroutines();
        ReleaseVideo();
        if (retryCanvas != null) Destroy(retryCanvas.gameObject);
        retryCanvas = null;
        sound.Stop();
        music.Stop();
        foreach (Transform child in viewport) Destroy(child.gameObject);
        introCover = rosesFade = videoHandoffCover = null;
        Busy = OutroFinished = outroStarted = introPrepared = false;
        winterIntro = winterTransition = weekendCamera = eggnogCamera = spaghettiCamera = false;
        weekendReturnAge = -1;
        advanceRequested = false;
        FadeHud(1);
    }

    public void RevealRetry()
    {
        StartCoroutine(RetryFade(1, 0, 1));
    }

    public IEnumerator RetryFade(float from, float to, float duration)
    {
        var root = new GameObject("Retry Fade", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        retryCanvas = root.AddComponent<Canvas>();
        retryCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        retryCanvas.worldCamera = song.isDead ? song.deadCamera : song.mainCamera;
        retryCanvas.planeDistance = retryCanvas.worldCamera.nearClipPlane + .1f;
        retryCanvas.sortingOrder = 32760;
        int mask = retryCanvas.worldCamera.cullingMask;
        for (int layer = 0; layer < 32; layer++)
            if ((mask & (1 << layer)) != 0) { root.layer = layer; break; }
        var cover = new GameObject("Retry Cover", typeof(RectTransform)).AddComponent<Image>();
        cover.transform.SetParent(root.transform, false);
        cover.gameObject.layer = root.layer;
        cover.rectTransform.anchorMin = Vector2.zero;
        cover.rectTransform.anchorMax = Vector2.one;
        cover.rectTransform.offsetMin = cover.rectTransform.offsetMax = Vector2.zero;
        cover.color = new Color(0, 0, 0, from);
        cover.raycastTarget = false;
        bool pixel = song.vanillaPlayback.IsPixel;
        if (pixel)
        {
            if (retryFadeMaterial == null) retryFadeMaterial = new Material(Resources.Load<Shader>("FunkinHud/RetryFade"));
            cover.material = retryFadeMaterial;
        }
        float end = duration + (pixel && to == 0 ? duration / 10 : 0);
        for (float elapsed = 0; elapsed < end; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            if (pixel) progress = Mathf.Clamp01((Mathf.Floor(elapsed / duration * 10) - 1) / 10);
            cover.color = new Color(0, 0, 0, Mathf.Lerp(from, to, progress));
            yield return null;
        }
        cover.color = new Color(0, 0, 0, to);
        if (to == 0)
        {
            Destroy(root);
            retryCanvas = null;
        }
    }

    public void CancelCountdown()
    {
        if (sound != null) sound.Stop();
        if (music != null) music.Stop();
        if (countdownImage != null)
        {
            countdownImage.gameObject.SetActive(false);
            Destroy(countdownImage.gameObject);
            countdownImage = null;
        }
    }

    public IEnumerator Countdown(Song owner, bool retry = false)
    {
        Initialize(owner);
        float beat = song.beatsPerSecond;
        song.BeginFunkinCountdown(beat*5, retry);
        transitionStart = song.SongPosition - Pause.GlobalOffset;
        if (videoHandoffCover != null)
        {
            var cover = videoHandoffCover;
            videoHandoffCover = null;
            Busy = false;
            StartCoroutine(RevealVideoGameplay(cover));
        }
        string folder = song.vanillaPlayback.IsPixel ? "Pixel" : "Countdown";
        string[] sounds = { "introTHREE", "introTWO", "introONE", "introGO" };
        string[] images = { null, "ready", "set", "go" };
        for (int step = 0; step < 4; step++)
        {
            double start = -(4-step)*beat*1000;
            while (song.CountdownPosition < start) yield return null;
            sound.PlayOneShot(Resources.Load<AudioClip>("FunkinHud/"+folder+"/"+sounds[step]),.6f);
            RawImage image = null;
            if (images[step] != null)
            {
                var texture = Resources.Load<Texture2D>("FunkinHud/"+folder+"/"+images[step]);
                float scale = song.vanillaPlayback.IsPixel ? 6 : 1;
                image = Rect("Countdown",viewport,(1280-texture.width*scale)/2,(720-texture.height*scale)/2,texture.width*scale,texture.height*scale).gameObject.AddComponent<RawImage>();
                image.texture = texture;
                countdownImage = image;
            }
            while (song.CountdownPosition < start+beat*1000)
            {
                if (Pause.instance != null && (Pause.instance.IsPaused || Pause.instance.Transitioning))
                {
                    yield return null;
                    continue;
                }
                float t = (float)((song.CountdownPosition-start)/(beat*1000));
                float eased = song.vanillaPlayback.IsPixel ? Mathf.Floor(t*8)/8 : t < .5f ? 4*t*t*t : 1-Mathf.Pow(-2*t+2,3)/2;
                if (image != null) image.color = new Color(1,1,1,(1-eased)*HudAlpha);
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
            countdownImage = null;
        }
    }

    public bool AllowEnd(Song owner)
    {
        Initialize(owner);
        if (song.vanillaPlayback.IsSpaghetti && song.vanillaPlayback.CampaignStage != null)
        {
            BeginSpaghettiEnding(owner);
            return OutroFinished;
        }
        if (song.vanillaPlayback.SongId == "stress" && song.vanillaPlayback.Variation == "pico" && !OptionsV2.DesperateMode)
        {
            if (!outroStarted) { outroStarted = true; StartCoroutine(StressPicoOutro()); }
            return OutroFinished;
        }
        if (VanillaStoryCampaign.Running && (song.vanillaPlayback.SongId == "2hot" || song.vanillaPlayback.SongId == "blazin"))
        {
            if (!outroStarted) { outroStarted = true; StartCoroutine(WeekendOutro()); }
            return OutroFinished;
        }
        if (song.vanillaPlayback.SongId != "eggnog" || !song.vanillaPlayback.IsErect || OptionsV2.DesperateMode) return true;
        if (!outroStarted) { outroStarted = true; StartCoroutine(Outro()); }
        return OutroFinished;
    }

    private IEnumerator Outro()
    {
        Busy = true;
        eggnogCamera = true;
        yield return song.vanillaPlayback.CampaignStage.EggnogOutro();
        OutroFinished = true;
        Busy = false;
    }

    private void OnDestroy()
    {
        ReleaseVideo();
        if (retryFadeMaterial != null) Destroy(retryFadeMaterial);
        foreach (AudioClip clip in clips) if (clip != null) Destroy(clip);
    }
}
