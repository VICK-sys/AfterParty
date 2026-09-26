using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class VanillaMainMenu : MonoBehaviour
{
    public MenuV2 menu;
    public RectTransform background;
    public RawImage magenta;
    public VanillaMenuItem[] items;
    public AudioSource effects;
    public AudioClip scrollSound;
    public AudioClip confirmSound;
    public AudioClip cancelSound;
    public GameObject creditsPanel;
    public ScrollRect creditsScroll;
    public bool flashingLights = true;
    public const string MerchUrl = "https://needlejuicerecords.com/en-ca/pages/friday-night-funkin";
    private const float ConfirmDuration = 16 * 0.06f;
    private const float MagentaDuration = 7 * 0.15f;

    public int SelectedIndex { get; private set; }
    public bool Busy { get; private set; }
    public float CameraScroll { get; private set; }
    private static int rememberedIndex;
    private float previousAxis;
    private bool showingCredits;
    private int enabledFrame;
    private bool freeplaySuspended;

    private void Start()
    {
        RawImage buildLabel = new GameObject("Build Version", typeof(RectTransform)).AddComponent<RawImage>();
        buildLabel.rectTransform.SetParent(background.parent, false);
        buildLabel.rectTransform.anchorMin = buildLabel.rectTransform.anchorMax = Vector2.zero;
        buildLabel.rectTransform.pivot = new Vector2(0, 1);
        buildLabel.rectTransform.anchoredPosition = new Vector2(0, 18);
        buildLabel.texture = Resources.Load<Texture2D>("VanillaText/made-in-unity");
        buildLabel.material = Resources.Load<Material>("VanillaText/Premultiplied");
        buildLabel.SetNativeSize();
        buildLabel.raycastTarget = false;
    }

    public void SetFreeplaySuspended(bool value)
    {
        freeplaySuspended = value;
        Busy = value;
        enabledFrame = Time.frameCount;
        previousAxis = Player.MenuAxis("Vertical");
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        if (items == null || items.Length == 0)
            return;
        Busy = false;
        flashingLights = VanillaPreferences.FlashingLights;
        showingCredits = false;
        creditsPanel.SetActive(false);
        magenta.enabled = false;
        SelectedIndex = Mathf.Clamp(rememberedIndex, 0, items.Length - 1);
        CameraScroll = TargetScroll;
        previousAxis = Player.MenuAxis("Vertical");
        for (int i = 0; i < items.Length; i++)
        {
            items[i].Select(i == SelectedIndex);
            items[i].image.enabled = true;
            items[i].image.color = Color.white;
        }
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        Draw(0);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        Busy = false;
    }

    private float TargetScroll => 40 + 160 * SelectedIndex + 0.5f - 360;

    private void Update()
    {
        if (freeplaySuspended) return;
        float delta = VanillaMenuTiming.Delta;
        CameraScroll = Mathf.Lerp(CameraScroll, TargetScroll, 1 - Mathf.Pow(0.94f, delta * 60));
        Draw(delta);
        if (Time.frameCount == enabledFrame) return;
        if (menu.musicSource.clip == menu.menuClip)
            menu.musicSource.volume = Mathf.MoveTowards(menu.musicSource.volume, OptionsV2.menuVolume * 0.8f, delta * 0.5f);
        float axis = Player.MenuAxis("Vertical");
        if (VanillaTitleTransition.BlocksInput || VanillaCreditsTransition.BlocksInput)
        {
            previousAxis = axis;
            return;
        }
        bool back = VanillaControls.Pressed("BACK");
        if (showingCredits)
        {
            if (back)
                CloseCredits();
        }
        else if (!Busy)
        {
            if (axis > 0.5f && previousAxis <= 0.5f)
                MoveSelection(-1);
            else if (axis < -0.5f && previousAxis >= -0.5f)
                MoveSelection(1);
            if (VanillaControls.Pressed("ACCEPT"))
                ConfirmSelection();
            else if (back)
                ReturnToTitle();
        }
        previousAxis = axis;
    }

    private void LateUpdate()
    {
        ApplyLayout(((RectTransform)transform).rect.width);
    }

    public void ApplyLayout(float availableWidth)
    {
        var viewport = (RectTransform)background.parent;
        float width = Mathf.Clamp(Mathf.Round(availableWidth), 1280, 1600);
        viewport.sizeDelta = new Vector2(width, 720);
        Texture texture = background.GetComponent<RawImage>().texture;
        float backgroundWidth = Mathf.Max(1536, width * 1.2f);
        background.sizeDelta = new Vector2(backgroundWidth, backgroundWidth * texture.height / texture.width);
        magenta.rectTransform.sizeDelta = background.sizeDelta;
    }

    private void Draw(float delta)
    {
        background.anchoredPosition = new Vector2(0, CameraScroll * 0.17f);
        magenta.rectTransform.anchoredPosition = background.anchoredPosition;
        foreach (VanillaMenuItem item in items)
            item.Draw(delta, CameraScroll);
    }

    public void MoveSelection(int change)
    {
        if (Busy || showingCredits || freeplaySuspended || VanillaTitleTransition.BlocksInput || VanillaCreditsTransition.BlocksInput)
            return;
        items[SelectedIndex].Select(false);
        SelectedIndex = (SelectedIndex + change % items.Length + items.Length) % items.Length;
        rememberedIndex = SelectedIndex;
        items[SelectedIndex].Select(true);
        effects.PlayOneShot(scrollSound, OptionsV2.miscVolume * 0.4f);
    }

    public void ConfirmSelection()
    {
        if (Busy || showingCredits || freeplaySuspended || VanillaTitleTransition.BlocksInput || VanillaCreditsTransition.BlocksInput)
            return;
        Busy = true;
        effects.PlayOneShot(confirmSound, OptionsV2.miscVolume);
        StartCoroutine(Confirm());
    }

    public void ReturnToTitle()
    {
        if (Busy || showingCredits || freeplaySuspended || VanillaTitleTransition.BlocksInput || VanillaCreditsTransition.BlocksInput) return;
        if (!VanillaCreditsTransition.Begin(() => VanillaTitleScreen.Open(menu))) return;
        effects.PlayOneShot(cancelSound, OptionsV2.miscVolume);
    }

    private IEnumerator Confirm()
    {
        float elapsed = 0;
        while (elapsed < ConfirmDuration)
        {
            magenta.enabled = flashingLights && Mathf.FloorToInt(elapsed / 0.15f) % 2 == 1;
            items[SelectedIndex].image.enabled = !flashingLights || Mathf.FloorToInt(elapsed / 0.06f) % 2 == 0;
            elapsed += VanillaMenuTiming.Delta;
            yield return null;
        }
        items[SelectedIndex].image.enabled = true;
        if (SelectedIndex == 2)
        {
            Application.OpenURL(MerchUrl);
            while (elapsed < MagentaDuration)
            {
                magenta.enabled = flashingLights && Mathf.FloorToInt(elapsed / 0.15f) % 2 == 1;
                elapsed += VanillaMenuTiming.Delta;
                yield return null;
            }
            magenta.enabled = false;
            Busy = false;
            yield break;
        }
        if (SelectedIndex != 1)
        {
            items[SelectedIndex].image.enabled = false;
            while (elapsed < ConfirmDuration + 0.4f)
            {
                float alpha = Mathf.Pow(1 - Mathf.Clamp01((elapsed - ConfirmDuration) / 0.4f), 2);
                foreach (VanillaMenuItem item in items)
                    item.image.color = new Color(1, 1, 1, alpha);
                magenta.enabled = flashingLights && elapsed < MagentaDuration && Mathf.FloorToInt(elapsed / 0.15f) % 2 == 1;
                elapsed += VanillaMenuTiming.Delta;
                yield return null;
            }
        }
        magenta.enabled = false;
        switch (SelectedIndex)
        {
            case 0:
                menu.OpenStoryMode();
                break;
            case 1:
                menu.OpenFreeplay();
                break;
            case 3:
                menu.OptionsScreenTransition(true);
                break;
            case 4:
                showingCredits = true;
                VanillaCreditsTransition.Begin(() => VanillaCreditsScreen.Open(menu));
                break;
        }
    }

    public void CloseCredits()
    {
        if (VanillaCreditsScreen.Active != null) VanillaCreditsScreen.Active.Close();
    }
}
