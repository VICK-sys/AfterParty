using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingTransition : MonoBehaviour
{
    public static LoadingTransition instance;
    public bool toggled;
    public float Progress { get; private set; }
    private Canvas overlay;
    private RectTransform artwork;
    private RectTransform loadBar;
    private CanvasGroup visibility;
    private const float FadeDuration = .35f;
    private float exitStartedAt;
    private bool leaving;
    private float shownAt;
    private float aspect;
    private bool ready;
    private AsyncOperation sceneLoad;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        Build();
    }

    private void Build()
    {
        overlay = new GameObject("Funkin Loading", typeof(RectTransform)).AddComponent<Canvas>();
        overlay.transform.SetParent(transform, false);
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 1500;
        visibility = overlay.gameObject.AddComponent<CanvasGroup>();
        var scaler = overlay.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        var background = Stretch("Background").gameObject.AddComponent<Image>();
        background.color = new Color32(202, 255, 77, 255);
        var texture = Resources.Load<Texture2D>("VanillaOptions/funkay");
        aspect = (float)texture.width / texture.height;
        artwork = new GameObject("Funkay", typeof(RectTransform)).GetComponent<RectTransform>();
        artwork.SetParent(overlay.transform, false);
        artwork.anchorMin = artwork.anchorMax = artwork.pivot = Vector2.one * .5f;
        var image = artwork.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        loadBar = new GameObject("Load Progress", typeof(RectTransform)).GetComponent<RectTransform>();
        loadBar.SetParent(overlay.transform, false);
        loadBar.anchorMin = loadBar.anchorMax = loadBar.pivot = Vector2.zero;
        loadBar.anchoredPosition = new Vector2(0, 10);
        var bar = loadBar.gameObject.AddComponent<Image>();
        bar.color = new Color32(255, 22, 210, 255);
        bar.raycastTarget = false;
        overlay.gameObject.SetActive(false);
    }

    private RectTransform Stretch(string label)
    {
        var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(overlay.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private bool Begin()
    {
        if (toggled) return false;
        toggled = true;
        ready = false;
        leaving = false;
        Progress = 0;
        visibility.alpha = 0;
        overlay.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        float height = ((RectTransform)overlay.transform).rect.height;
        artwork.sizeDelta = new Vector2(height * aspect, height);
        loadBar.sizeDelta = new Vector2(0, 10);
        shownAt = Time.realtimeSinceStartup;
        return true;
    }

    public void Show(Action action)
    {
        if (Begin()) StartCoroutine(ShowRoutine(action));
    }

    private IEnumerator ShowRoutine(Action action)
    {
        while (Time.realtimeSinceStartup - shownAt < 1.5f) yield return null;
        action?.Invoke();
    }

    public void LoadScene(string scene, Action beforeLoad = null)
    {
        if (Begin()) StartCoroutine(LoadSceneRoutine(scene, beforeLoad));
    }

    private IEnumerator LoadSceneRoutine(string scene, Action beforeLoad)
    {
        while (visibility.alpha < 1) yield return null;
        yield return null;
        beforeLoad?.Invoke();
        sceneLoad = SceneManager.LoadSceneAsync(scene);
        sceneLoad.allowSceneActivation = false;
        while (sceneLoad.progress < .9f || Time.realtimeSinceStartup - shownAt < 1.5f)
        {
            Progress = sceneLoad.progress;
            yield return null;
        }
        sceneLoad.allowSceneActivation = true;
        while (!sceneLoad.isDone) yield return null;
        sceneLoad = null;
    }

    public void Hide()
    {
        if (!toggled) return;
        ready = true;
        Progress = 1;
    }

    private void Update()
    {
        if (!toggled) return;
        float delta = Time.unscaledDeltaTime;
        float width = ((RectTransform)overlay.transform).rect.width;
        float imageWidth = Mathf.Lerp(artwork.sizeDelta.x, width * .88f, 1 - Mathf.Pow(.9f, delta * 60));
        if (VanillaControls.Pressed("ACCEPT")) imageWidth += 60;
        artwork.sizeDelta = new Vector2(imageWidth, imageWidth / aspect);
        loadBar.sizeDelta = new Vector2(Mathf.Lerp(loadBar.sizeDelta.x, width * Progress, 1 - Mathf.Pow(.8f, delta * 60)), 10);
        float now = Time.realtimeSinceStartup;
        if (!leaving && ready && sceneLoad == null && now - shownAt >= 1.5f)
        {
            leaving = true;
            exitStartedAt = now;
        }
        float progress = Mathf.Clamp01((now - (leaving ? exitStartedAt : shownAt)) / FadeDuration);
        float eased = progress * progress * (3 - 2 * progress);
        visibility.alpha = leaving ? 1 - eased : eased;
        if (leaving && progress >= 1)
        {
            toggled = false;
            overlay.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
