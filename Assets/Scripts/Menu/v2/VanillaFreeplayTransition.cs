using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed class VanillaFreeplayTransition : MonoBehaviour
{
    public Canvas Overlay { get; private set; }
    public float Progress { get; private set; }
    public RenderTexture Texture { get; private set; }
    private Camera capture;
    private Canvas source;
    private Transform[] transforms;
    private int[] layers;
    private Material blue;
    private RectTransform gradient;
    private bool characterSelect;
    private Image flash;
    private float width;

    public static VanillaFreeplayTransition Create(Canvas source, bool characterSelect = false)
    {
        var effect = new GameObject("Freeplay Transition Camera").AddComponent<VanillaFreeplayTransition>();
        effect.source = source;
        effect.characterSelect = characterSelect;
        effect.Initialize();
        return effect;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        return rect;
    }

    private void Initialize()
    {
        transforms = source.GetComponentsInChildren<Transform>(true);
        layers = transforms.Select(item => item.gameObject.layer).ToArray();
        foreach (var item in transforms) item.gameObject.layer = 31;
        width = ((RectTransform)source.transform.Find("Viewport")).rect.width;
        Texture = new RenderTexture(Mathf.RoundToInt(width), 720, 24, RenderTextureFormat.ARGB32);
        Texture.Create();
        capture = gameObject.AddComponent<Camera>();
        capture.enabled = false;
        capture.orthographic = true;
        capture.orthographicSize = 360;
        capture.aspect = width / 720;
        capture.clearFlags = CameraClearFlags.SolidColor;
        capture.backgroundColor = Color.black;
        capture.cullingMask = 1 << 31;
        capture.targetTexture = Texture;
        capture.transform.position = new Vector3(0, 0, -1000);
        capture.farClipPlane = 2000;
        capture.GetUniversalAdditionalCameraData().SetRenderer(0);
        source.renderMode = RenderMode.ScreenSpaceCamera;
        source.worldCamera = capture;
        source.planeDistance = 1000;
        source.GetComponent<CanvasScaler>().enabled = false;
        source.scaleFactor = 1;
        Overlay = new GameObject("Freeplay Character Transition", typeof(RectTransform)).AddComponent<Canvas>();
        Overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        Overlay.sortingOrder = source.sortingOrder + 1;
        var scaler = Overlay.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(width, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var black = Rect("Black", Overlay.transform, Vector2.zero).gameObject.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;
        black.rectTransform.anchorMax = Vector2.one;
        black.rectTransform.anchorMin = Vector2.zero;
        black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;
        var viewport = Rect("Viewport", Overlay.transform, new Vector2(width, 720));
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = Vector2.one * .5f;
        viewport.gameObject.AddComponent<RectMask2D>();
        var image = Rect("Blue Fade", viewport, new Vector2(width, 720)).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.texture = Texture;
        blue = new Material(Resources.Load<Shader>("VanillaFreeplay/BlueFade"));
        image.material = blue;
        gradient = Rect("Gradient Wipe", viewport, new Vector2(width, 720));
        var wipe = gradient.gameObject.AddComponent<RawImage>();
        wipe.raycastTarget = false;
        wipe.texture = Resources.Load<Texture2D>("VanillaFreeplay/freeplay/transitionGradient");
        if (characterSelect) wipe.uvRect = new UnityEngine.Rect(0, 1, 1, -1);
        Draw(0);
    }

    public void Draw(float elapsed)
    {
        Progress = Mathf.Clamp01(elapsed / .8f);
        blue.SetFloat("_Fade", 1 - Progress * Progress);
        float eased = 2.70158f * Progress * Progress * Progress - 1.70158f * Progress * Progress;
        gradient.anchoredPosition = new Vector2(0, characterSelect ? 720 - 570 * eased : -720 + 720 * eased);
    }

    public void DrawEntrance(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / .8f);
        blue.SetFloat("_Fade", 1 - (1 - t) * (1 - t));
        gradient.anchoredPosition = new Vector2(0, elapsed >= 1 ? 720 : 720 * (1 - Mathf.Pow(2, -10 * elapsed)));
        if (flash != null) flash.color = new Color(1, 1, 1, Mathf.Clamp01(1 - elapsed));
    }

    public void DrawFreeplayEntrance(float elapsed)
    {
        Progress = Mathf.Clamp01(elapsed / .8f);
        blue.SetFloat("_Fade", Progress * Progress);
        float t = Mathf.Clamp01(elapsed / 1.8f);
        gradient.anchoredPosition = new Vector2(0, -720 * (t >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * t)));
    }

    public void Flash()
    {
        flash = Rect("Lights Flash", Overlay.transform.Find("Viewport"), new Vector2(width, 720)).gameObject.AddComponent<Image>();
        flash.raycastTarget = false;
        flash.color = Color.white;
    }

    private void LateUpdate()
    {
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(capture, new UniversalRenderPipeline.SingleCameraRequest { destination = Texture });
    }

    private void OnDestroy()
    {
        if (source != null)
        {
            source.renderMode = RenderMode.ScreenSpaceOverlay;
            source.worldCamera = null;
            source.GetComponent<CanvasScaler>().enabled = true;
        }
        for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) transforms[i].gameObject.layer = layers[i];
        if (Overlay != null) Destroy(Overlay.gameObject);
        Destroy(blue);
        if (Texture != null) { Texture.Release(); Destroy(Texture); }
    }
}
