using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class VanillaCreditsScreen : MonoBehaviour
{
    [Serializable]
    public sealed class Variant
    {
        public int minWidth;
        public string atlas;
        public int x;
        public int y;
        public int width;
        public int height;
        public float trimX;
        public float trimY;
        public float fullHeight;
        public int numLines;
    }

    [Serializable]
    public sealed class Line
    {
        public string text;
        public bool header;
        public bool endEntry;
        public Variant[] variants;
    }

    [Serializable]
    private sealed class Document
    {
        public Line[] lines;
    }

    public sealed class RenderedLine
    {
        public Line data;
        public Variant variant;
        public RawImage image;
        public double y;
    }

    public static VanillaCreditsScreen Active { get; private set; }
    public RectTransform Viewport { get; private set; }
    public double GroupY { get; private set; } = 720;
    public double NextY { get; private set; }
    public int BuiltLineCount { get; private set; }
    public int TotalLineCount => lines.Length;
    public IReadOnlyList<RenderedLine> VisibleLines => visible;
    public float MusicAge { get; private set; }
    public double ScrollAge { get; private set; }
    private readonly List<RenderedLine> visible = new List<RenderedLine>();
    private readonly Stack<RawImage> pool = new Stack<RawImage>();
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private MenuV2 menu;
    private Line[] lines;
    private int viewportWidth = 1280;
    private int openedFrame;
    private bool closing;
    private bool previousCursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => Active = null;

    public static VanillaCreditsScreen Open(MenuV2 owner)
    {
        if (Active != null) return Active;
        var host = new GameObject("Vanilla Credits", typeof(RectTransform));
        host.SetActive(false);
        var screen = host.AddComponent<VanillaCreditsScreen>();
        screen.menu = owner;
        screen.previousCursor = Cursor.visible;
        screen.Build();
        owner.mainScreen.gameObject.SetActive(false);
        owner.inputBlocker.enabled = false;
        LeanTween.cancel(owner.musicSource.gameObject);
        owner.musicSource.Stop();
        owner.musicSource.clip = Resources.Load<AudioClip>("VanillaFreeplay/audio/freeplayRandom");
        owner.musicSource.loop = true;
        owner.musicSource.volume = 0;
        owner.musicSource.Play();
        screen.openedFrame = Time.frameCount;
        Active = screen;
        host.SetActive(true);
        Cursor.visible = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        return screen;
    }

    private void Build()
    {
        lines = JsonUtility.FromJson<Document>(Resources.Load<TextAsset>("VanillaCredits/lines").text).lines;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        var matte = Rect("Background", transform);
        matte.anchorMin = Vector2.zero;
        matte.anchorMax = Vector2.one;
        matte.offsetMin = matte.offsetMax = Vector2.zero;
        matte.gameObject.AddComponent<Image>().color = Color.black;
        Viewport = Rect("Viewport", transform);
        Viewport.anchorMin = Viewport.anchorMax = Viewport.pivot = new Vector2(0.5f, 0.5f);
        Viewport.sizeDelta = new Vector2(1280, 720);
        Viewport.gameObject.AddComponent<Image>().raycastTarget = false;
        Viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private void Update()
    {
        Cursor.visible = false;
        ApplyLayout(((RectTransform)transform).rect.width);
        bool fast = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
        bool pause = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            || Input.GetKey(KeyCode.P) || Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.JoystickButton7);
        bool back = Time.frameCount != openedFrame && !VanillaCreditsTransition.BlocksInput && (Input.GetKeyDown(KeyCode.Escape)
            || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Backspace) || Player.ControllerBackPressed);
        Tick(Time.unscaledDeltaTime, fast, pause, back);
    }

    public void Tick(double delta, bool fast = false, bool pause = false, bool back = false)
    {
        if (Active != this) return;
        MusicAge += (float)delta;
        menu.musicSource.volume = Mathf.Clamp01(MusicAge / 6) * OptionsV2.menuVolume * 0.8f;
        if (closing || VanillaCreditsTransition.IsRunning) return;
        ScrollAge += delta;
        for (int i = visible.Count - 1; i >= 0; i--)
        {
            RenderedLine line = visible[i];
            if (line.y + line.variant.fullHeight > 0) continue;
            line.image.gameObject.SetActive(false);
            pool.Push(line.image);
            visible.RemoveAt(i);
        }
        if (BuiltLineCount < lines.Length && GroupY + NextY < 720)
        {
            Line data = lines[BuiltLineCount++];
            Variant variant = SelectVariant(data);
            RawImage image = pool.Count > 0 ? pool.Pop() : Rect("Credit Line", Viewport).gameObject.AddComponent<RawImage>();
            var line = new RenderedLine { data = data, variant = variant, image = image, y = GroupY + NextY };
            SetImage(line);
            visible.Add(line);
            NextY += Advance(data, variant);
        }
        double previousY = GroupY;
        GroupY -= (fast ? 400.0 : pause ? 0 : 100.0) * delta;
        foreach (RenderedLine line in visible) line.y += GroupY - previousY;
        Draw();
        if (back || BuiltLineCount == lines.Length && visible.Count == 0) Close();
    }

    private static int Advance(Line line, Variant variant) => (line.header ? 32 + variant.numLines * 32 : variant.numLines * 24)
        + (line.endEntry ? 60 : 0);

    private Variant SelectVariant(Line line)
    {
        for (int i = line.variants.Length - 1; i >= 0; i--)
            if (viewportWidth >= line.variants[i].minWidth) return line.variants[i];
        return line.variants[0];
    }

    private void SetImage(RenderedLine line)
    {
        Variant variant = line.variant;
        if (!textures.TryGetValue(variant.atlas, out Texture2D texture))
        {
            texture = Resources.Load<Texture2D>("VanillaCredits/" + variant.atlas);
            textures.Add(variant.atlas, texture);
        }
        line.image.gameObject.name = line.data.text;
        line.image.texture = texture;
        line.image.uvRect = new Rect((float)variant.x / texture.width, 1 - (float)(variant.y + variant.height) / texture.height,
            (float)variant.width / texture.width, (float)variant.height / texture.height);
        line.image.rectTransform.sizeDelta = new Vector2(variant.width, variant.height);
        line.image.raycastTarget = false;
        line.image.gameObject.SetActive(true);
    }

    private void Draw()
    {
        foreach (RenderedLine line in visible)
            line.image.rectTransform.anchoredPosition = new Vector2(24 + line.variant.trimX,
                -(float)(line.y + line.variant.trimY));
    }

    public void ApplyLayout(float availableWidth)
    {
        int width = Mathf.Clamp(Mathf.CeilToInt(availableWidth), 1280, 1600);
        if (width == viewportWidth) return;
        viewportWidth = width;
        Viewport.sizeDelta = new Vector2(width, 720);
        double y = 0;
        for (int i = 0; i < BuiltLineCount; i++)
        {
            Line data = lines[i];
            Variant variant = SelectVariant(data);
            RenderedLine shown = visible.Find(line => line.data == data);
            if (shown != null)
            {
                shown.y = GroupY + y;
                shown.variant = variant;
                SetImage(shown);
            }
            y += Advance(data, variant);
        }
        NextY = y;
        Draw();
    }

    public void Close(bool transition = true)
    {
        if (closing) return;
        closing = true;
        if (!transition) CompleteClose();
        else if (!VanillaCreditsTransition.Begin(CompleteClose)) closing = false;
    }

    private void CompleteClose()
    {
        menu.musicSource.Stop();
        menu.musicSource.clip = menu.menuClip;
        menu.musicSource.loop = true;
        menu.musicSource.volume = OptionsV2.menuVolume * 0.8f;
        menu.musicSource.Play();
        Active = null;
        gameObject.SetActive(false);
        Cursor.visible = previousCursor;
        menu.mainScreen.gameObject.SetActive(true);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Active != this) return;
        Active = null;
        Cursor.visible = previousCursor;
    }
}
