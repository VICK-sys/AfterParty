using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class VanillaTitleTransition : MonoBehaviour
{
    public const float HalfDuration = 0.5f;
    public static VanillaTitleTransition Active { get; private set; }
    public static bool IsRunning => Active != null;
    public static bool BlocksInput => IsRunning || Time.frameCount == completedFrame;
    public float Progress { get; private set; }
    public bool Revealing { get; private set; }
    private static int completedFrame = -1;
    private Canvas canvas;
    private Material material;
    private Action switchScreen;
    private int startedFrame;
    private float elapsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Active = null;
        completedFrame = -1;
    }

    public static bool Begin(Action changeScreen)
    {
        if (BlocksInput) return false;
        if (changeScreen == null) throw new ArgumentNullException(nameof(changeScreen));
        var shader = Resources.Load<Shader>("VanillaTitle/DiamondWipe");
        if (shader == null) throw new InvalidOperationException("Missing title diamond wipe shader.");
        var host = new GameObject("Title Diamond Wipe", typeof(RectTransform));
        var transition = host.AddComponent<VanillaTitleTransition>();
        transition.canvas = host.AddComponent<Canvas>();
        transition.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transition.canvas.sortingOrder = 100;
        host.AddComponent<GraphicRaycaster>();
        var image = new GameObject("Diamond Mask", typeof(RectTransform)).AddComponent<RawImage>();
        image.transform.SetParent(host.transform, false);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        transition.material = new Material(shader);
        image.material = transition.material;
        image.raycastTarget = true;
        transition.switchScreen = changeScreen;
        transition.startedFrame = Time.frameCount;
        Active = transition;
        Canvas.willRenderCanvases += transition.UpdateResolution;
        transition.UpdateResolution();
        return true;
    }

    private void UpdateResolution()
    {
        Vector2 size = canvas.pixelRect.size;
        material.SetVector("_Resolution", new Vector4(Mathf.Max(1, size.x), Mathf.Max(1, size.y), 0, 0));
    }

    private void Update()
    {
        if (Time.frameCount != startedFrame) Tick(Time.unscaledDeltaTime);
    }

    public void Tick(float delta)
    {
        if (Active != this) return;
        elapsed += Mathf.Max(0, delta);
        Progress = Mathf.Clamp01(elapsed / HalfDuration);
        material.SetFloat("_Progress", Progress);
        if (Progress < 1) return;
        if (!Revealing)
        {
            Action changeScreen = switchScreen;
            switchScreen = null;
            Revealing = true;
            elapsed = 0;
            Progress = 0;
            material.SetFloat("_Revealing", 1);
            material.SetFloat("_Progress", 0);
            try { changeScreen(); }
            catch { Finish(); throw; }
        }
        else Finish();
    }

    private void Finish()
    {
        completedFrame = Time.frameCount;
        if (Active == this) Active = null;
        Canvas.willRenderCanvases -= UpdateResolution;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Canvas.willRenderCanvases -= UpdateResolution;
        if (Active == this) Active = null;
        switchScreen = null;
        if (material != null) Destroy(material);
    }
}
