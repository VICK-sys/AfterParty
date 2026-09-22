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
    private bool fadingToBlack;
    private bool loadingRevealed;
    private AsyncOperation sceneLoad;
    private GameObject failurePanel;
    private Action failureReturn;
    private RawImage storyFrame;
    private Texture2D storyTexture;
    private Image background;
    public bool HoldingStoryFrame => storyFrame != null;

    public void HoldStoryFrame(Action continuation)
    {
        StartCoroutine(HoldStoryFrameRoutine(continuation));
    }

    private IEnumerator HoldStoryFrameRoutine(Action continuation)
    {
        yield return new WaitForEndOfFrame();
        storyTexture = ScreenCapture.CaptureScreenshotAsTexture();
        if (storyTexture == null)
        {
            continuation();
            yield break;
        }
        storyFrame = Stretch("Story Transition").gameObject.AddComponent<RawImage>();
        storyFrame.texture = storyTexture;
        storyFrame.raycastTarget = true;
        background.gameObject.SetActive(false);
        artwork.gameObject.SetActive(false);
        loadBar.gameObject.SetActive(false);
        overlay.gameObject.SetActive(true);
        visibility.alpha = 1;
        continuation();
    }

    private void ReleaseStoryFrame()
    {
        if (storyFrame != null) Destroy(storyFrame.gameObject);
        if (storyTexture != null) Destroy(storyTexture);
        storyFrame = null;
        storyTexture = null;
        if (background != null) background.gameObject.SetActive(true);
        if (artwork != null) artwork.gameObject.SetActive(true);
        if (loadBar != null) loadBar.gameObject.SetActive(true);
    }

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
        background = Stretch("Background").gameObject.AddComponent<Image>();
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
        loadingRevealed = false;
        leaving = false;
        Progress = 0;
        visibility.alpha = HoldingStoryFrame ? 1 : 0;
        overlay.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        float height = ((RectTransform)overlay.transform).rect.height;
        artwork.sizeDelta = new Vector2(height * aspect, height);
        loadBar.sizeDelta = new Vector2(0, 10);
        shownAt = Time.realtimeSinceStartup;
        return true;
    }

    public void ShowFailure(string message, Action returnToMenu)
    {
        ReleaseStoryFrame();
        if (!toggled) Begin();
        ready = leaving = false;
        failureReturn = returnToMenu;
        if (failurePanel != null) Destroy(failurePanel);
        failurePanel = Stretch("Loading Error").gameObject;
        failurePanel.AddComponent<Image>().color = new Color(0, 0, 0, .9f);
        var button = failurePanel.AddComponent<Button>();
        button.onClick.AddListener(ReturnFromFailure);
        var text = new GameObject("Message", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
        text.transform.SetParent(failurePanel.transform, false);
        text.rectTransform.anchorMin = new Vector2(.1f, .25f);
        text.rectTransform.anchorMax = new Vector2(.9f, .75f);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.fontSize = 32;
        text.raycastTarget = false;
        text.text = message + "\n\nPress Accept or Back, or click to return to Freeplay.";
    }

    private void ReturnFromFailure()
    {
        var action = failureReturn;
        failureReturn = null;
        if (failurePanel != null) Destroy(failurePanel);
        action?.Invoke();
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

    public void LoadScene(string scene, Action beforeLoad = null, bool fadeThroughBlack = false)
    {
        if (Begin()) StartCoroutine(LoadSceneRoutine(scene, beforeLoad, fadeThroughBlack));
    }

    private IEnumerator LoadSceneRoutine(string scene, Action beforeLoad, bool fadeThroughBlack)
    {
        if (fadeThroughBlack && !HoldingStoryFrame)
        {
            fadingToBlack = true;
            Color loadingColor = background.color;
            background.color = Color.black;
            artwork.gameObject.SetActive(false);
            loadBar.gameObject.SetActive(false);
            while (Time.realtimeSinceStartup - shownAt < FadeDuration)
            {
                float fade = Mathf.Clamp01((Time.realtimeSinceStartup - shownAt) / FadeDuration);
                visibility.alpha = fade * fade * (3 - 2 * fade);
                yield return null;
            }
            visibility.alpha = 1;
            yield return null;
            background.color = loadingColor;
            artwork.gameObject.SetActive(true);
            loadBar.gameObject.SetActive(true);
            shownAt = Time.realtimeSinceStartup;
            loadingRevealed = true;
            fadingToBlack = false;
        }
        while (visibility.alpha < 1) yield return null;
        yield return null;
        beforeLoad?.Invoke();
        if (scene == "Game_Backup3") SongLoadingDiagnostics.Begin(Song.currentSongMeta?.songPath, Song.difficulty, true);
        sceneLoad = SceneManager.LoadSceneAsync(scene);
        sceneLoad.allowSceneActivation = false;
        while (sceneLoad.progress < .9f || !HoldingStoryFrame && Time.realtimeSinceStartup - shownAt < 1.5f)
        {
            Progress = sceneLoad.progress;
            yield return null;
        }
        SongLoadingDiagnostics.Record("activate scene: " + scene);
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
        if (!toggled || fadingToBlack) return;
        if (failureReturn != null && (VanillaControls.Pressed("ACCEPT") || VanillaControls.Pressed("BACK")))
            ReturnFromFailure();
        float delta = Time.unscaledDeltaTime;
        float width = ((RectTransform)overlay.transform).rect.width;
        float imageWidth = Mathf.Lerp(artwork.sizeDelta.x, width * .88f, 1 - Mathf.Pow(.9f, delta * 60));
        if (VanillaControls.Pressed("ACCEPT")) imageWidth += 60;
        artwork.sizeDelta = new Vector2(imageWidth, imageWidth / aspect);
        loadBar.sizeDelta = new Vector2(Mathf.Lerp(loadBar.sizeDelta.x, width * Progress, 1 - Mathf.Pow(.8f, delta * 60)), 10);
        float now = Time.realtimeSinceStartup;
        if (!leaving && ready && sceneLoad == null && (HoldingStoryFrame || now - shownAt >= 1.5f))
        {
            leaving = true;
            exitStartedAt = now;
        }
        float progress = Mathf.Clamp01((now - (leaving ? exitStartedAt : shownAt)) / FadeDuration);
        float eased = progress * progress * (3 - 2 * progress);
        visibility.alpha = leaving ? 1 - eased : HoldingStoryFrame || loadingRevealed ? 1 : eased;
        if (leaving && progress >= 1)
        {
            toggled = false;
            overlay.gameObject.SetActive(false);
            ReleaseStoryFrame();
        }
    }

    private void OnDestroy()
    {
        ReleaseStoryFrame();
        if (instance == this) instance = null;
    }
}
