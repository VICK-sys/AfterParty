using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class VanillaCharacterSelect : MonoBehaviour
{
    public static VanillaCharacterSelect Active { get; private set; }
    public static bool PicoUnlocked => true;
    public static string SelectedCharacter => PicoUnlocked && PlayerPrefs.GetString("Freeplay.Character", "bf") == "pico" ? "pico" : "bf";
    public string Character { get; private set; }
    public int SelectedSlot { get; private set; }
    public bool Busy => age < 1.5f || !charactersReady || introPlaying || leaving;
    public bool Confirming => confirmAge >= 0;
    private RectTransform viewport;
    private RectTransform icons;
    private RectTransform cursorRoot;
    private VanillaFreeplayAnimate player;
    private VanillaFreeplayAnimate outgoing;
    private VanillaFreeplayAnimate girlfriend;
    private VanillaFreeplaySprite nametag;
    private readonly List<(RectTransform rect, Vector2 position, float scroll)> layers = new List<(RectTransform, Vector2, float)>();
    private readonly List<(RectTransform rect, Vector2 position, float offset, float duration)> entrance = new List<(RectTransform, Vector2, float, float)>();
    private readonly VanillaFreeplaySprite[] cursors = new VanillaFreeplaySprite[3];
    private VanillaFreeplaySprite confirmCursor;
    private VanillaFreeplaySprite denyCursor;
    private AudioSource music;
    private AudioSource effects;
    private AudioSource staticSound;
    private Action<string> completed;
    private string original;
    private float age;
    private float confirmAge = -1;
    private float exitAge;
    private Vector2 heldAge;
    private Vector2 held;
    private Vector2 cameraOffset;
    private bool leaving;
    private bool introPlaying;
    private bool freeplayReady;
    private bool charactersReady;
    private int beat = -1;
    private VanillaFreeplayTransition transition;
    private CanvasGroup cursorAlpha;
    private Vector2 exitCamera;
    private Vector2 nametagPosition;
    private float recoveryAge = -1;
    private float recoveryPitch;
    private float recoveryVolume;
    private float confirmPitch;
    private float confirmVolume;
    private float exitVolume;
    private int step = -1;
    private VideoPlayer video;
    private RenderTexture videoTexture;
    private Material multiply;
    private Material additive;
    private Material screen;
    private readonly float[] spectrum = new float[512];
    private readonly int[] vizFrames = new int[7];
    private readonly Dictionary<int, VanillaFreeplayAnimate> slotLocks = new Dictionary<int, VanillaFreeplayAnimate>();
    private readonly List<Material> lockMaterials = new List<Material>();
    private Material nametagMaterial;
    private Material iconMaterial;
    private float nametagAge = 1;
    private readonly Dictionary<int, VanillaFreeplaySprite> slotIcons = new Dictionary<int, VanillaFreeplaySprite>();

    public static VanillaCharacterSelect Open(Action<string> callback, bool skipIntro = false)
    {
        if (Active != null) return Active;
        var obj = new GameObject("Character Select", typeof(RectTransform));
        var screen = obj.AddComponent<VanillaCharacterSelect>();
        Active = screen;
        screen.completed = callback;
        screen.original = SelectedCharacter;
        screen.SelectedSlot = screen.original == "pico" ? 3 : 4;
        screen.Build();
        screen.SwitchCharacter(false);
        screen.StartCoroutine(screen.PreloadFreeplay());
        if (!skipIntro && PlayerPrefs.GetInt("CharacterSelect.SeenIntro", 0) == 0) screen.StartCoroutine(screen.Intro());
        else { screen.music.Play(); screen.BeginEntrance(); }
        return screen;
    }

    private IEnumerator PreloadFreeplay()
    {
        foreach (string character in new[] { "bfChill", "picoChill", "lockedChill", "gfChill", "neneChill" })
            yield return VanillaFreeplayAnimate.Preload("charSelect/" + character);
        charactersReady = true;
        var requests = new List<ResourceRequest>();
        foreach (string path in new[] {
            "digital_numbers_pico", "freeplay/freeplayBGweek1-pico", "freeplay/freeplaySelector/freeplaySelector_pico",
            "freeplay/freeplayCapsule/capsule/freeplayCapsule_pico", "freeplay/backingCards/pico/lowerLoop",
            "freeplay/backingCards/pico/middleLoop", "freeplay/backingCards/pico/topLoop", "freeplay/backingCards/pico/blueBar",
            "freeplay/backingCards/pico/glow", "freeplay/albumRoll/expansion1", "freeplay/albumRoll/expansion1-text" })
            requests.Add(Resources.LoadAsync<Texture2D>("VanillaFreeplay/" + path));
        yield return VanillaFreeplayAnimate.Preload("freeplay/freeplay-pico");
        yield return VanillaFreeplayAnimate.Preload("freeplay/backingCards/pico/pico-confirm");
        yield return VanillaFreeplayAnimate.Preload("freeplay/freeplay-boyfriend");
        foreach (var request in requests) yield return request;
        freeplayReady = true;
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

    private VanillaFreeplaySprite Sprite(string name, Transform parent, float x, float y, string prefix = "")
    {
        var graphic = Rect(name, parent, x, y).gameObject.AddComponent<VanillaFreeplaySprite>();
        graphic.Load("charSelect/" + name, prefix);
        graphic.centerScale = false;
        return graphic;
    }

    private VanillaFreeplayAnimate Animate(string name, Transform parent, float x, float y)
    {
        var graphic = Rect(name, parent, x, y).gameObject.AddComponent<VanillaFreeplayAnimate>();
        graphic.Initialize("charSelect/" + name, true);
        graphic.PlayAll(true);
        return graphic;
    }

    private void Layer(RectTransform rect, float scroll)
    {
        layers.Add((rect, rect.anchoredPosition, scroll));
    }

    private void Enter(RectTransform rect, float offset, float duration)
    {
        entrance.Add((rect, rect.anchoredPosition, offset, duration));
    }

    private void Build()
    {
        multiply = Blend(UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, true);
        additive = Blend(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, false);
        screen = Blend(UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.OneMinusSrcColor, true);
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        iconMaterial = Blend(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 14;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        var black = Rect("Letterbox", transform, 0, 0).gameObject.AddComponent<Image>();
        black.color = Color.black;
        black.rectTransform.anchorMin = Vector2.zero;
        black.rectTransform.anchorMax = Vector2.one;
        black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;
        viewport = Rect("Viewport", transform, 0, 0);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(.5f, .5f);
        viewport.gameObject.AddComponent<RectMask2D>();
        Layer(Sprite("charSelectBG", viewport, -153, -140).rectTransform, .1f);
        Layer(Animate("crowd", viewport, 0, 0).rectTransform, .3f);
        Layer(Animate("charSelectStage", viewport, -2, 1).rectTransform, 1);
        Layer(Sprite("curtains", viewport, -212, -99).rectTransform, 1.4f);
        var bar = Animate("barThing", viewport, 0, 0);
        bar.material = multiply;
        bar.transform.localScale = new Vector3(2.5f, 1, 1);
        Enter(bar.rectTransform, 80, 1.3f);
        Layer(Sprite("charLight", viewport, 800, 250).rectTransform, 1);
        Layer(Sprite("charLight", viewport, 180, 240).rectTransform, 1);
        girlfriend = Animate(original == "pico" ? "neneChill" : "gfChill", viewport, 0, 0);
        outgoing = Animate(original + "Chill", viewport, 0, 0);
        outgoing.gameObject.SetActive(false);
        player = Animate(original + "Chill", viewport, 0, 0);
        Layer(girlfriend.rectTransform, 1);
        Layer(outgoing.rectTransform, 1);
        Layer(player.rectTransform, 1);
        var speakers = Animate("charSelectSpeakers", viewport, -10, 0);
        speakers.transform.localScale = Vector3.one * 1.05f;
        Layer(speakers.rectTransform, 1.8f);
        var foreground = Sprite("foregroundBlur", viewport, -125, 170);
        foreground.material = multiply;
        Layer(foreground.rectTransform, 1);
        var header = Sprite("dipshitBlur", viewport, 419, -65, "CHOOSE vertical offset instance 1");
        header.material = additive;
        Enter(header.rectTransform, 220, 1.2f);
        var headerBacking = Sprite("dipshitBacking", viewport, 423, -17, "CHOOSE horizontal offset instance 1");
        headerBacking.material = additive;
        Enter(headerBacking.rectTransform, 210, 1.1f);
        Enter(Sprite("chooseDipshit", viewport, 426, -13).rectTransform, 200, 1);
        nametag = Sprite(original == "bf" ? "boyfriendNametag" : "picoNametag", viewport, 1008, 100);
        nametag.drawScale = .77f;
        nametagMaterial = Blend(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, false);
        nametag.material = nametagMaterial;
        CenterNametag();
        icons = Rect("Icons", viewport, 450, 120);
        Enter(icons, 300, 1);
        for (int i = 0; i < 9; i++)
        {
            if (i == 3 || i == 4)
            {
                CreateIcon(i);
            }
            else
            {
                var item = Animate("lock", icons, i % 3 * 107 - 230, i / 3 * 127 - 110);
                string[] lockColors = { "31F2A5", "20ECCD", "24D9E8", "20ECCD", "20C8D4", "209BDD", "209BDD", "2362C9", "243FB9" };
                var lockMaterial = Blend(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, false);
                ColorUtility.TryParseHtmlString("#" + lockColors[i], out Color tint);
                lockMaterial.SetFloat("_ReplaceGreen", 1);
                lockMaterial.SetColor("_GreenTint", tint);
                item.material = lockMaterial;
                lockMaterials.Add(lockMaterial);
                slotLocks[i] = item;
                item.Play("idle", true);
                item.UseTimelineBounds();
                item.rectTransform.anchoredPosition = new Vector2(i % 3 * 107 - 230, -i / 3 * 127 + 110);
            }
            var hit = Rect("Slot " + i, icons, i % 3 * 107 + 20, i / 3 * 127 + 20, 86, 86);
            hit.gameObject.AddComponent<Image>().color = Color.clear;
            int slot = i;
            hit.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (SelectedSlot == slot) Confirm(); else SelectSlot(slot); });
        }
        cursorRoot = Rect("Cursors", viewport, 450, 138);
        cursorRoot.SetSiblingIndex(icons.GetSiblingIndex());
        cursorAlpha = cursorRoot.gameObject.AddComponent<CanvasGroup>();
        Color[] colors = { new Color32(60,116,247,255), new Color32(62,187,255,255), Color.yellow };
        for (int i = 0; i < cursors.Length; i++)
        {
            cursors[i] = Sprite("charSelector", cursorRoot, 0, 0);
            cursors[i].color = colors[i];
            if (i < 2) cursors[i].material = screen;
        }
        confirmCursor = Sprite("charSelectorConfirm", cursorRoot, 0, 0, "cursor ACCEPTED instance 1");
        denyCursor = Sprite("charSelectorDenied", cursorRoot, 0, 0, "cursor DENIED instance 1");
        confirmCursor.gameObject.SetActive(false);
        denyCursor.gameObject.SetActive(false);
        music = gameObject.AddComponent<AudioSource>();
        effects = gameObject.AddComponent<AudioSource>();
        staticSound = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = effects.playOnAwake = staticSound.playOnAwake = false;
        music.clip = Resources.Load<AudioClip>("VanillaFreeplay/audio/charSelect/stayFunky");
        music.loop = true;
        staticSound.clip = Resources.Load<AudioClip>("VanillaFreeplay/audio/charSelect/static loop");
        staticSound.loop = true;
        foreach (var cursor in cursors) cursor.rectTransform.anchoredPosition = CursorPosition();

    }

    private void CreateIcon(int slot)
    {
        var icon = Rect("Character Icon " + slot, icons, slot % 3 * 107, slot / 3 * 127).gameObject.AddComponent<VanillaFreeplaySprite>();
        icon.Load("freeplay/icons/" + (slot == 3 ? "pico" : "bf") + "pixel", "idle0");
        icon.centerScale = false;
        icon.fps = 10;
        icon.material = iconMaterial;
        var outline = icon.gameObject.AddComponent<VanillaIconOutline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(2, 2);
        var shadow = icon.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(3.5355f, -3.5355f);
        slotIcons[slot] = icon;
    }

    public static Material Blend(UnityEngine.Rendering.BlendMode source, UnityEngine.Rendering.BlendMode destination, bool premultiply)
    {
        var material = new Material(Resources.Load<Shader>("VanillaFreeplay/MenuBlend"));
        material.SetInt("_SrcBlend", (int)source);
        material.SetInt("_DstBlend", (int)destination);
        material.SetFloat("_Premultiply", premultiply ? 1 : 0);
        return material;
    }

    private void CenterNametag()
    {
        nametagPosition = new Vector2(1008 - nametag.FrameSize.x * .77f / 2, -100 + nametag.FrameSize.y * .77f / 2);
        nametag.rectTransform.anchoredPosition = nametagPosition;
    }

    private void Sound(string name, float volume = 1)
    {
        effects.PlayOneShot(Resources.Load<AudioClip>("VanillaFreeplay/audio/charSelect/" + name), volume * OptionsV2.miscVolume);
    }

    public void SelectSlot(int slot)
    {
        if (Busy || Confirming) return;
        SelectedSlot = (slot % 9 + 9) % 9;
        foreach (var item in slotLocks)
            if (item.Value != null) item.Value.Play(item.Key == SelectedSlot ? "selected" : "idle", true);
        denyCursor.gameObject.SetActive(false);
        Sound("CS_select", .7f);
        SwitchCharacter(true);
    }

    public void Move(int x, int y)
    {
        int column = ((SelectedSlot % 3 + x) % 3 + 3) % 3;
        int row = ((SelectedSlot / 3 + y) % 3 + 3) % 3;
        SelectSlot(row * 3 + column);
    }

    private void SwitchCharacter(bool slide)
    {
        string next = SelectedSlot == 3 && PicoUnlocked ? "pico" : SelectedSlot == 4 ? "bf" : "locked";
        if (next == Character) return;
        if (slide)
        {
            outgoing.Initialize("charSelect/" + Character + "Chill", true);
            outgoing.gameObject.SetActive(true);
            outgoing.Play("slideout", false);
        }
        Character = next;
        player.Initialize("charSelect/" + Character + "Chill", true);
        player.Play(slide ? "slidein" : "idle", !slide && Character == "locked");
        girlfriend.gameObject.SetActive(Character != "locked");
        if (Character != "locked")
        {
            girlfriend.Initialize("charSelect/" + (Character == "pico" ? "neneChill" : "gfChill"), true);
            girlfriend.Play("idle", false);
            staticSound.Stop();
        }
        else staticSound.Play();
        nametag.Load("charSelect/" + (Character == "bf" ? "boyfriend" : Character) + "Nametag");
        nametagAge = slide ? 0 : 1;
        CenterNametag();
    }

    public void Confirm()
    {
        if (Busy || Confirming) return;
        if (Character == "locked")
        {
            Sound("CS_locked");
            if (slotLocks.TryGetValue(SelectedSlot, out var locked)) locked.Play("clicked", false);
            player.Play("cannot select Label", false);
            denyCursor.gameObject.SetActive(true);
            denyCursor.TryPlay("cursor DENIED instance 1", false);
            return;
        }
        Sound("CS_confirm");
        confirmAge = 0;
        recoveryAge = -1;
        confirmPitch = music.pitch;
        confirmVolume = music.volume;
        held = Vector2.zero;
        heldAge = Vector2.zero;
        player.Play("select", false);
        girlfriend.Play("confirm", true);
        foreach (var cursor in cursors) cursor.gameObject.SetActive(false);
        confirmCursor.gameObject.SetActive(true);
        confirmCursor.TryPlay("cursor ACCEPTED instance 1", true);
    }

    public void Back()
    {
        if (Busy) return;
        if (Confirming)
        {
            confirmAge = -1;
            recoveryAge = 0;
            recoveryPitch = music.pitch;
            recoveryVolume = music.volume;
            slotIcons[SelectedSlot].PlayReverse("confirm0", "idle0");
            player.Play("deselect", false);
            girlfriend.Play("deselect", false);
            foreach (var cursor in cursors) cursor.gameObject.SetActive(true);
            confirmCursor.gameObject.SetActive(false);
        }
        else
        {
            effects.PlayOneShot(Resources.Load<AudioClip>("VanillaFreeplay/audio/cancelMenu"), OptionsV2.miscVolume);
            BeginExit();
        }
    }

    private void BeginEntrance(bool lightsFlash = false)
    {
        transition = VanillaFreeplayTransition.Create(GetComponent<Canvas>(), true);
        transition.DrawEntrance(0);
        if (lightsFlash) transition.Flash();
    }

    private void BeginExit()
    {
        leaving = true;
        exitCamera = cameraOffset;
        exitVolume = music.volume;
        if (transition == null) transition = VanillaFreeplayTransition.Create(GetComponent<Canvas>(), true);
        transition.Draw(0);
    }

    private static float QuadInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t < .5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
    }

    private static float ExpoOut(float t) => t >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * Mathf.Max(0, t));

    private static float BackIn(float t)
    {
        t = Mathf.Clamp01(t);
        return 2.70158f * t * t * t - 1.70158f * t * t;
    }

    private static Vector2 CursorLerp(Vector2 current, Vector2 target, float delta, float duration)
    {
        return Vector2.Lerp(target, current, Mathf.Pow(.01f, delta / duration));
    }

    private Vector2 CursorPosition()
    {
        Vector2 size = cursors[2].FrameSize;
        return SlotCenter(SelectedSlot) + new Vector2(-size.x / 2, size.y / 2);
    }

    private static Vector2 SlotCenter(int slot) => new Vector2(slot % 3 * 110 + 64, -(slot / 3 * 110 + 64));

    private void Update() => Tick(VanillaMenuTiming.Delta);

    public void Tick(float delta)
    {
        if (introPlaying) return;
        bool wasLeaving = leaving;
        age += delta;
        nametagAge += delta;
        foreach (var item in slotIcons)
        {
            bool selected = item.Key == SelectedSlot;
            float scale = selected ? 2.6f : 2;
            item.Value.drawScale = scale;
            item.Value.rectTransform.anchoredPosition = new Vector2(item.Key % 3 * 107 + 64, -(item.Key / 3 * 127 + 64)) + new Vector2(-item.Value.FrameSize.x * scale / 2, item.Value.FrameSize.y * scale / 2);
            foreach (var effect in item.Value.GetComponents<Shadow>()) effect.enabled = selected;
            if (selected && Confirming && item.Value.CurrentFrameName.StartsWith("idle")) item.Value.TryPlay("confirm0", false, "confirm-hold0");
            item.Value.SetVerticesDirty();
        }
        int mosaicFrame = Mathf.FloorToInt(nametagAge * 30);
        Vector2 mosaic = mosaicFrame == 0 || mosaicFrame == 2 || mosaicFrame == 4 ? new Vector2(10,10) : mosaicFrame == 1 ? new Vector2(73,6) : mosaicFrame == 3 ? new Vector2(27,26) : Vector2.zero;
        nametagMaterial.SetVector("_Mosaic", new Vector4(mosaic.x,mosaic.y,0,0));
        staticSound.volume = .6f * OptionsV2.miscVolume;
        if (Character == "pico" && music.isPlaying)
        {
            music.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            for (int i = 0; i < vizFrames.Length; i++)
            {
                float level = 0;
                int start = 1 << i;
                for (int j = start; j < start * 2; j++) level = Mathf.Max(level, spectrum[j]);
                vizFrames[i] = 12 - Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(level) * 60), 0, 12);
            }
            girlfriend.SetLayerFrames("VIZ_bars", vizFrames);
        }
        if (Confirming)
        {
            confirmAge += delta;
            music.pitch = Mathf.Lerp(confirmPitch, .1f, QuadInOut(confirmAge));
            music.volume = Mathf.Lerp(confirmVolume, 0, QuadInOut(confirmAge / 1.5f));
            if (confirmAge >= 1.5f && !leaving) BeginExit();
        }
        else if (recoveryAge >= 0)
        {
            recoveryAge += delta;
            float t = Mathf.Clamp01(recoveryAge);
            float ease = t < .5f ? 8 * t * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 4) / 2;
            music.pitch = Mathf.Lerp(recoveryPitch, 1, ease);
            music.volume = Mathf.Lerp(recoveryVolume, OptionsV2.menuVolume, ease);
            if (recoveryAge >= 1)
            {
                recoveryAge = -1;
                if (player.CurrentLabel == "deselect" || player.CurrentLabel == "deselect loop start")
                {
                    player.Play("idle", true);
                    girlfriend.Play("idle", true);
                }
            }
        }
        else if (!leaving) { music.pitch = 1; music.volume = OptionsV2.menuVolume; }
        if (leaving) exitAge += wasLeaving ? delta : Mathf.Max(0, confirmAge - 1.5f);
        int currentBeat = Mathf.FloorToInt(music.time * 90 / 60);
        if (currentBeat != beat)
        {
            beat = currentBeat;
            if (player.CurrentLabel == "idle" && player.Finished) player.Play("idle", Character == "locked");
            if (girlfriend.CurrentLabel == "idle" && beat % 2 == 0) girlfriend.Play("idle", false);
        }
        if (outgoing.gameObject.activeSelf && outgoing.Finished) outgoing.gameObject.SetActive(false);
        if (player.Finished)
        {
            if (player.CurrentLabel == "slidein")
            {
                string next = player.HasLabel("slidein idle point") ? "slidein idle point" : "idle";
                player.Play(next, next == "idle" && Character == "locked");
            }
            else if (player.CurrentLabel == "deselect") player.Play("deselect loop start", false);
            else if (player.CurrentLabel == "slidein idle point" || player.CurrentLabel == "cannot select Label" || player.CurrentLabel == "unlock") player.Play("idle", Character == "locked");
        }
        if (denyCursor.FrameIndex == denyCursor.FrameCount - 1) denyCursor.gameObject.SetActive(false);
        Vector2 target = age < 1.5f ? new Vector2(0, -150 * Mathf.Pow(2, -10 * age / 1.5f))
            : new Vector2((SelectedSlot % 3 - 1) * 10, (SelectedSlot / 3 - 1) * 10);
        cameraOffset = leaving ? exitCamera + new Vector2(0, -150 * BackIn(exitAge / .8f))
            : Vector2.Lerp(cameraOffset, target, age < 1.5f ? 1 : Mathf.Clamp01(.01f * delta * 60));
        foreach (var layer in layers) layer.rect.anchoredPosition = layer.position + new Vector2(-cameraOffset.x, cameraOffset.y) * layer.scroll;
        foreach (var item in entrance)
        {
            float offset = item.offset * (1 - ExpoOut(age / item.duration));
            if (leaving) offset = item.offset * BackIn(exitAge / .8f);
            item.rect.anchoredPosition = item.position + Vector2.down * offset;
        }
        nametag.rectTransform.anchoredPosition = nametagPosition + Vector2.down * (leaving ? 80 * BackIn(exitAge / .8f) : 200 * (1 - ExpoOut(age)));
        Vector2 cursorTarget = CursorPosition();
        Vector2 main = CursorLerp(cursors[2].rectTransform.anchoredPosition, cursorTarget, delta, .1f);
        if (Mathf.Abs(main.x - cursorTarget.x) <= 1) main.x = cursorTarget.x;
        if (Mathf.Abs(main.y - cursorTarget.y) <= 1) main.y = cursorTarget.y;
        cursors[2].rectTransform.anchoredPosition = main;
        cursors[1].rectTransform.anchoredPosition = CursorLerp(cursors[1].rectTransform.anchoredPosition, main, delta, .202f);
        cursors[0].rectTransform.anchoredPosition = CursorLerp(cursors[0].rectTransform.anchoredPosition, cursorTarget, delta, .404f);
        cursors[2].color = Color.Lerp(Color.yellow, new Color(1,.8f,0), Mathf.PingPong(age*5,1));
        confirmCursor.rectTransform.anchoredPosition = denyCursor.rectTransform.anchoredPosition = cursors[2].rectTransform.anchoredPosition + new Vector2(-2,4);
        if (leaving)
        {
            cursorAlpha.alpha = 1 - ExpoOut(exitAge / .8f);
            transition.Draw(exitAge);
            if (!Confirming) music.volume = exitVolume * (1 - QuadInOut(exitAge / .7f));
            if (exitAge >= .8f && freeplayReady)
            {
                if (!Confirming) Character = original;
                PlayerPrefs.SetString("Freeplay.Character", Character);
                PlayerPrefs.Save();
                completed?.Invoke(Character);
                Destroy(gameObject);
            }
            return;
        }
        if (transition != null)
        {
            transition.DrawEntrance(age);
            if (age >= 1) { Destroy(transition.gameObject); transition = null; }
        }
        if (Busy) return;
        if (VanillaControls.Pressed("BACK")) { Back(); return; }
        if (Confirming) return;
        if (VanillaControls.Pressed("ACCEPT")) { Confirm(); return; }
        Vector2 direction = new Vector2(Mathf.Round(Player.MenuAxis("Horizontal")), -Mathf.Round(Player.MenuAxis("Vertical")));
        int currentStep = Mathf.FloorToInt(music.time * 90 / 60 * 4);
        int moveX = direction.x != 0 && direction.x != held.x ? (int)direction.x : 0;
        int moveY = direction.y != 0 && direction.y != held.y ? (int)direction.y : 0;
        heldAge.x = direction.x == 0 || direction.x != held.x ? 0 : heldAge.x + delta;
        heldAge.y = direction.y == 0 || direction.y != held.y ? 0 : heldAge.y + delta;
        if (currentStep != step)
        {
            if (heldAge.x >= .5f) moveX = (int)direction.x;
            if (heldAge.y >= .5f) moveY = (int)direction.y;
        }
        if (moveX != 0 || moveY != 0) Move(moveX, moveY);
        held = direction;
        step = currentStep;
    }

    private IEnumerator Intro()
    {
        introPlaying = true;
        var image = Rect("Lights Intro", viewport, 0, 0).gameObject.AddComponent<RawImage>();
        image.color = Color.black;
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.source = VideoSource.Url;
        video.url = new Uri(Path.Combine(Application.streamingAssetsPath, "CharacterSelect/introSelect.mp4")).AbsoluteUri;
        videoTexture = new RenderTexture(1280,720,0);
        videoTexture.Create();
        video.targetTexture = videoTexture;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.SetTargetAudioSource(0, effects);
        bool failed = false;
        video.errorReceived += (_, message) => { failed = true; Debug.LogWarning(message); };
        video.Prepare();
        float deadline = Time.realtimeSinceStartup + 20;
        while (!video.isPrepared && !failed && Time.realtimeSinceStartup < deadline) yield return null;
        if (video.isPrepared)
        {
            image.texture = videoTexture;
            image.color = Color.white;
            video.Play();
            yield return null;
            while (!failed && video.isPlaying) yield return null;
        }
        Destroy(image.gameObject);
        video.Stop();
        video.targetTexture = null;
        Destroy(video);
        videoTexture.Release();
        Destroy(videoTexture);
        videoTexture = null;
        PlayerPrefs.SetInt("CharacterSelect.SeenIntro",1);
        PlayerPrefs.Save();
        Sound("CS_Lights");
        music.Play();
        introPlaying = false;
        BeginEntrance(true);
    }

    private void OnDestroy()
    {
        if (transition != null) Destroy(transition.gameObject);
        Destroy(multiply);
        Destroy(additive);
        Destroy(screen);
        Destroy(nametagMaterial);
        Destroy(iconMaterial);
        foreach (var material in lockMaterials) Destroy(material);
        if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
        if (Active == this) Active = null;
    }
}
