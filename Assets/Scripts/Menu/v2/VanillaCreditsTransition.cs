using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class VanillaCreditsTransition : MonoBehaviour
{
    public const float CoverDuration = 0.7f;
    public const float RevealDuration = 1;
    public static VanillaCreditsTransition Active { get; private set; }
    public static bool IsRunning => Active != null;
    public static bool BlocksInput => IsRunning || completedFrame == Time.frameCount;
    public bool Revealing { get; private set; }
    public float Progress { get; private set; }
    public RectTransform Viewport { get; private set; }
    private static int completedFrame = -1;
    private Action changeScreen;
    private RawImage image;
    private float elapsed;
    private int startedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { Active = null; completedFrame = -1; }

    public static bool Begin(Action changeScreen)
    {
        if (IsRunning || VanillaTitleTransition.IsRunning) return false;
        if (changeScreen == null) throw new ArgumentNullException(nameof(changeScreen));
        var host = new GameObject("Credits Transition", typeof(RectTransform));
        var transition = host.AddComponent<VanillaCreditsTransition>();
        transition.changeScreen = changeScreen;
        transition.startedFrame = Time.frameCount;
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        host.AddComponent<GraphicRaycaster>();
        var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.SetParent(host.transform, false);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.sizeDelta = new Vector2(1280, 720);
        viewport.gameObject.AddComponent<Image>().raycastTarget = false;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        transition.Viewport = viewport;
        var rect = new GameObject("Fade", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(viewport, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        transition.image = rect.gameObject.AddComponent<RawImage>();
        Active = transition;
        transition.SetTexture();
        transition.Draw();
        return true;
    }

    private void SetTexture()
    {
        image.texture = Resources.Load<Texture2D>("VanillaCredits/fade-" + (Revealing ? "reveal" : "cover"));
        if (image.texture == null) throw new InvalidOperationException("Missing credits transition gradient.");
        image.texture.filterMode = FilterMode.Point;
    }

    private void Update()
    {
        if (Time.frameCount == startedFrame) return;
        Tick(VanillaMenuTiming.Delta);
    }

    public void Tick(float delta)
    {
        if (Active != this) return;
        elapsed += delta;
        Progress = Mathf.Clamp01(elapsed / (Revealing ? RevealDuration : CoverDuration));
        Draw();
        if (Progress < 1) return;
        if (Revealing) { Finish(); return; }
        Revealing = true;
        elapsed = 0;
        Progress = 0;
        SetTexture();
        Draw();
        Action callback = changeScreen;
        changeScreen = null;
        try { callback(); }
        catch { Finish(); throw; }
    }

    public void ApplyLayout(float availableWidth)
    {
        Viewport.sizeDelta = new Vector2(Mathf.Clamp(Mathf.Ceil(availableWidth), 1280, 1600), 720);
        Draw();
    }

    private void LateUpdate() => ApplyLayout(((RectTransform)transform).rect.width);

    private void Draw()
    {
        float height = image.texture.height;
        float y = Revealing ? Mathf.Lerp(-height / 2, 720, Progress) : Mathf.Lerp(-height, 0, Progress);
        image.rectTransform.sizeDelta = new Vector2(Viewport.rect.width * 1.4f, height);
        image.rectTransform.anchoredPosition = new Vector2(-200, -y);
    }

    private void Finish()
    {
        completedFrame = Time.frameCount;
        Active = null;
        changeScreen = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
        changeScreen = null;
    }
}
