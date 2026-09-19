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
    public bool OwnsCamera => winterIntro || winterTransition || weekendCamera || eggnogCamera;
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
    private bool outroStarted;
    private bool advanceRequested;
    private float dialogueAge;
    private Image rosesFade;
    private Song song;
    private Canvas canvas;
    private RectTransform viewport;
    private AudioSource sound;
    private AudioSource music;
    private readonly System.Collections.Generic.List<AudioClip> clips = new System.Collections.Generic.List<AudioClip>();
    private string Root(int week) => Path.Combine(Application.streamingAssetsPath, "Bundles/Week" + week + "Assets");
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
        Initialize(owner);
        if (playedIntro || Pause.PlayedCampaignIntro || Pause.DeathCount > 0) yield break;
        playedIntro = true;
        Pause.PlayedCampaignIntro = true;
        string id = song.vanillaPlayback.SongId;
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
            if (VanillaStoryCampaign.Running) yield return Week7Video(id);
            yield break;
        }
        if (id != "winter-horrorland" && (!song.vanillaPlayback.IsPixel || !VanillaStoryCampaign.Running && !pico)) yield break;
        Busy = true;
        bool hud = song.uiCamera.enabled;
        bool battle = song.battleCanvas.enabled;
        song.uiCamera.enabled = false;
        song.battleCanvas.enabled = false;
        if (id == "winter-horrorland")
        {
            winterIntro = true;
            cameraReturn = song.mainCamera.transform.position;
            cameraReturnSize = song.mainCamera.orthographicSize;
            var black = Overlay(Color.black);
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
                var black = Overlay(Color.black);
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
        var red = Overlay(new Color32(255,27,49,255));
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
        var text = Rect("Dialogue",viewport,205,470,900,200).gameObject.AddComponent<Text>();
        text.font = Resources.Load<Font>("FunkinHud/Pixel/dialogue");
        text.fontSize = 32;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var shadow = text.gameObject.AddComponent<Shadow>();
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
            while (!box.Finished)
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
            text.color = color;
            text.fontSize = (int?)boxData["text"]["size"] ?? 32;
            ColorUtility.TryParseHtmlString((string)boxData["text"]["shadowColor"], out Color shadowColor);
            shadow.effectColor = shadowColor;
            float shadowWidth = (float?)boxData["text"]["shadowWidth"] ?? 2;
            shadow.effectDistance = new Vector2(shadowWidth,-shadowWidth);
            string content = "";
            foreach (JToken segment in line["text"])
            {
                int previousLength = content.Length;
                content += (string)segment;
                int shown = previousLength;
                float age = 0;
                while (shown < content.Length)
                {
                    age += Time.deltaTime;
                    int next = Mathf.Min(content.Length,previousLength + (int)(age / (.05f * ((float?)line["speed"] ?? 1))));
                    if (Advance) next = content.Length;
                    if (next > shown) sound.PlayOneShot(sound.clip,.6f);
                    shown = next;
                    text.text = content.Substring(0,shown);
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

    public IEnumerator Countdown(Song owner)
    {
        Initialize(owner);
        float beat = song.beatsPerSecond;
        song.BeginFunkinCountdown(beat*5);
        transitionStart = -beat*5000;
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
            while (song.SongPosition-Pause.GlobalOffset < start) yield return null;
            sound.PlayOneShot(Resources.Load<AudioClip>("FunkinHud/"+folder+"/"+sounds[step]),.6f);
            RawImage image = null;
            if (images[step] != null)
            {
                var texture = Resources.Load<Texture2D>("FunkinHud/"+folder+"/"+images[step]);
                float scale = song.vanillaPlayback.IsPixel ? 6 : 1;
                image = Rect("Countdown",viewport,(1280-texture.width*scale)/2,(720-texture.height*scale)/2,texture.width*scale,texture.height*scale).gameObject.AddComponent<RawImage>();
                image.texture = texture;
            }
            while (song.SongPosition-Pause.GlobalOffset < start+beat*1000)
            {
                float t = (float)((song.SongPosition-Pause.GlobalOffset-start)/(beat*1000));
                float eased = song.vanillaPlayback.IsPixel ? Mathf.Floor(t*8)/8 : t < .5f ? 4*t*t*t : 1-Mathf.Pow(-2*t+2,3)/2;
                if (image != null) image.color = new Color(1,1,1,(1-eased)*HudAlpha);
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
        }
    }

    public bool AllowEnd(Song owner)
    {
        Initialize(owner);
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
        foreach (AudioClip clip in clips) if (clip != null) Destroy(clip);
    }
}
