using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class VanillaOptionsRuntime : MonoBehaviour
{
    private static VanillaOptionsRuntime instance;
    private Canvas canvas;
    private Text debugText;
    private Image debugBackground;
    private RawImage preview;
    private Texture2D screenshot;
    private float frameTime, nextDisplay, previewAge;
    private RectTransform volumeTray;
    private CanvasGroup volumeTrayAlpha;
    private readonly RawImage[] volumeBars = new RawImage[10];
    private AudioSource volumeSound;
    private AudioClip volumeUpSound, volumeDownSound, volumeMaxSound;
    private float masterVolume = 1;
    private bool masterMuted;
    private float volumeTrayTimer;
    private float volumeTrayHeight;
    private bool takingScreenshot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;
        instance = new GameObject("Options Runtime").AddComponent<VanillaOptionsRuntime>();
        DontDestroyOnLoad(instance.gameObject);
        VanillaPreferences.Migrate();
        VanillaControls.Reload();
        if (!Application.isEditor) Screen.fullScreen = VanillaPreferences.Get("AutoFullscreen") != 0;
        instance.Build();
    }

    private void Build()
    {
        canvas = new GameObject("System Overlay",typeof(RectTransform)).AddComponent<Canvas>();
        canvas.transform.SetParent(transform,false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        debugBackground = VanillaOptionsMenu.Rect("Debug Background",canvas.transform,0,0,210,68).gameObject.AddComponent<Image>();
        debugText = VanillaOptionsMenu.Rect("Debug Display",canvas.transform,10,3,210,100).gameObject.AddComponent<Text>();
        debugText.font = Resources.Load<Font>("FunkinHud/Countdown/vcr");
        debugText.fontSize = 16;
        debugText.color = Color.white;
        debugText.raycastTarget = false;
        preview = VanillaOptionsMenu.Rect("Screenshot Preview",canvas.transform,0,0,320,180).gameObject.AddComponent<RawImage>();
        preview.rectTransform.anchorMin = preview.rectTransform.anchorMax = new Vector2(1,1);
        preview.rectTransform.pivot = new Vector2(1,1);
        preview.gameObject.SetActive(false);
        BuildVolumeTray();
    }

    private void BuildVolumeTray()
    {
        var overlay = new GameObject("Volume Overlay", typeof(RectTransform)).AddComponent<Canvas>();
        overlay.transform.SetParent(transform, false);
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 2000;
        var scaler = overlay.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        volumeTray = VanillaOptionsMenu.Rect("Volume Tray", overlay.transform, 0, 0, 0, 0);
        volumeTray.anchorMin = volumeTray.anchorMax = new Vector2(.5f, 1);
        volumeTray.pivot = new Vector2(.5f, 1);
        volumeTrayAlpha = volumeTray.gameObject.AddComponent<CanvasGroup>();
        volumeTrayAlpha.blocksRaycasts = false;
        volumeTrayAlpha.interactable = false;
        var background = VolumeImage("volumebox", "Background", 0, 0);
        volumeTray.sizeDelta = background.rectTransform.sizeDelta;
        volumeTrayHeight = volumeTray.sizeDelta.y;
        var backing = VolumeImage("bars_10", "Inactive Bars", 9, 5);
        backing.color = new Color(1, 1, 1, .4f);
        for (int i = 0; i < volumeBars.Length; i++) volumeBars[i] = VolumeImage("bars_" + (i + 1), "Bar " + (i + 1), 9, 5);
        volumeTray.anchoredPosition = new Vector2(0, volumeTrayHeight + 10);
        volumeTrayAlpha.alpha = 0;
        volumeSound = gameObject.AddComponent<AudioSource>();
        volumeSound.playOnAwake = false;
        volumeUpSound = Resources.Load<AudioClip>("VanillaOptions/soundtray/Volup");
        volumeDownSound = Resources.Load<AudioClip>("VanillaOptions/soundtray/Voldown");
        volumeMaxSound = Resources.Load<AudioClip>("VanillaOptions/soundtray/VolMAX");
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Funkin.MasterVolume", 1));
        masterMuted = PlayerPrefs.GetInt("Funkin.MasterMuted", 0) != 0;
        ApplyMasterVolume();
        volumeTray.gameObject.SetActive(masterMuted || masterVolume == 0);
    }

    private RawImage VolumeImage(string asset, string label, float x, float y)
    {
        var texture = Resources.Load<Texture2D>("VanillaOptions/soundtray/" + asset);
        var image = VanillaOptionsMenu.Rect(label, volumeTray, x, y, texture.width * .3f, texture.height * .3f).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private void ApplyMasterVolume()
    {
        AudioListener.volume = masterMuted ? 0 : masterVolume;
        int bars = masterMuted ? 0 : Mathf.RoundToInt(masterVolume * 10);
        for (int i = 0; i < volumeBars.Length; i++) volumeBars[i].enabled = i < bars;
    }

    public void ChangeMasterVolume(int direction)
    {
        if (direction == 0) masterMuted = !masterMuted;
        else
        {
            masterMuted = false;
            masterVolume = Mathf.Clamp01(Mathf.Round(masterVolume * 10 + direction) / 10);
        }
        ApplyMasterVolume();
        volumeTrayTimer = 1;
        volumeTray.gameObject.SetActive(true);
        if (direction != 0)
        {
            var clip = masterVolume == 1 ? volumeMaxSound : direction > 0 ? volumeUpSound : volumeDownSound;
            if (clip != null) volumeSound.PlayOneShot(clip);
        }
        PlayerPrefs.SetFloat("Funkin.MasterVolume", masterVolume);
        PlayerPrefs.SetInt("Funkin.MasterMuted", masterMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateVolumeTray(float delta)
    {
        bool muted = masterMuted || masterVolume == 0;
        volumeTrayTimer = Mathf.Max(0, volumeTrayTimer - delta);
        bool visible = muted || volumeTrayTimer > 0;
        float targetY = visible ? -10 : volumeTrayHeight + 10;
        var position = volumeTray.anchoredPosition;
        position.y = Mathf.Lerp(position.y, targetY, 1 - Mathf.Pow(.01f, delta / .768f));
        volumeTray.anchoredPosition = position;
        volumeTrayAlpha.alpha = Mathf.Lerp(volumeTrayAlpha.alpha, visible ? 1 : 0, 1 - Mathf.Pow(.01f, delta / .307f));
        volumeTray.gameObject.SetActive(visible || position.y < volumeTrayHeight);
    }

    private void Update()
    {
        UpdateVolumeTray(Time.unscaledDeltaTime);
        frameTime = Mathf.Lerp(frameTime,Time.unscaledDeltaTime,.1f);
        int mode = VanillaPreferences.Get("DebugDisplay",2);
        debugText.enabled = debugBackground.enabled = mode != 2;
        debugBackground.color = new Color(0,0,0,VanillaPreferences.Get("DebugDisplayBG",50)/100f);
        debugBackground.rectTransform.sizeDelta = new Vector2(210,mode==0?65:24);
        if (mode != 2 && Time.unscaledTime >= nextDisplay)
        {
            nextDisplay = Time.unscaledTime+.1f;
            debugText.text = "FPS: "+Mathf.RoundToInt(1/Mathf.Max(.0001f,frameTime));
            if (mode == 0) debugText.text += "\nMemory: "+(GC.GetTotalMemory(false)/1048576)+" MB\n"+Application.unityVersion;
        }
        if (preview.gameObject.activeSelf)
        {
            previewAge += Time.unscaledDeltaTime;
            preview.color = new Color(1,1,1,Mathf.Clamp01(3-previewAge));
            if (previewAge>=3) preview.gameObject.SetActive(false);
        }
        if (VanillaControls.Capturing || VanillaOptionsMenu.Active?.PromptOpen == true) return;
        if (VanillaControls.Pressed("WINDOW_FULLSCREEN")) Screen.fullScreen = !Screen.fullScreen;
        if (VanillaControls.Pressed("DEBUG_DISPLAY"))
        {
            var preference = Array.Find(VanillaPreferences.Items,p => p.id == "DebugDisplay");
            VanillaPreferences.Set(preference,(mode+1)%3);
        }
        if (VanillaControls.Pressed("VOLUME_MUTE")) ChangeMasterVolume(0);
        else if (VanillaControls.Pressed("VOLUME_UP")) ChangeMasterVolume(1);
        else if (VanillaControls.Pressed("VOLUME_DOWN")) ChangeMasterVolume(-1);
        if (VanillaControls.Pressed("WINDOW_SCREENSHOT") && !takingScreenshot) StartCoroutine(Capture());
    }

    private IEnumerator Capture()
    {
        takingScreenshot = true;
        bool cursor = Cursor.visible;
        if (VanillaPreferences.Get("HideMouse",1) != 0) Cursor.visible = false;
        preview.gameObject.SetActive(false);
        yield return new WaitForEndOfFrame();
        if (screenshot != null) Destroy(screenshot);
        screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        Cursor.visible = cursor;
        bool fancy = VanillaPreferences.Get("FancyPreview",1) != 0;
        bool afterSave = VanillaPreferences.Get("PreviewOnSave",1) != 0;
        if (fancy && !afterSave) ShowPreview();
        string folder = Path.Combine(Application.persistentDataPath,"screenshots");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder,DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")+".png"),screenshot.EncodeToPNG());
        if (fancy && afterSave) ShowPreview();
        takingScreenshot = false;
    }

    private void ShowPreview()
    {
        preview.texture = screenshot;
        preview.rectTransform.sizeDelta = new Vector2(320,320f*screenshot.height/screenshot.width);
        preview.color = Color.white;
        previewAge = 0;
        preview.gameObject.SetActive(true);
    }
}
