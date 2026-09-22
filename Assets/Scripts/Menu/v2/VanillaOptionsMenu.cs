using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class VanillaOptionsMenu : MonoBehaviour
{
    public enum Page { Options, Preferences, Controls, Offsets }
    private sealed class Row
    {
        public VanillaOptionsText text, value, secondary;
        public VanillaFreeplaySprite checkbox;
        public float y;
        public int binding;
        public VanillaPreferences.Preference preference;
        public Action action;
    }

    public static VanillaOptionsMenu Active { get; private set; }
    private static int rememberedIndex;
    public Page CurrentPage { get; private set; }
    public int SelectedIndex { get; private set; }
    public int SelectedColumn { get; private set; }
    public bool Busy { get; private set; }
    public bool PromptOpen => prompt != null;
    public bool Rebinding => rebinding;
    public float CameraScroll { get; private set; }
    public string[] Labels => rows.Select(r => r.text.Text).ToArray();
    public RectTransform Viewport { get; private set; }
    private MenuV2 menu;
    private RectTransform content, prompt;
    private readonly List<Row> rows = new List<Row>();
    private readonly List<(VanillaOptionsText text, float y)> headers = new List<(VanillaOptionsText, float)>();
    private VanillaStoryText description;
    private Image descriptionBox;
    private AudioSource effects, drums;
    private AudioClip scrollSound, confirmSound, cancelSound;
    private Material backgroundMaterial;
    private float verticalHeld, horizontalHeld, lastVertical, lastHorizontal;
    private int openedFrame, promptFrame;
    private bool previousCursor, gamepadDevice, deviceSelected, rebinding, forbidden;
    private bool controlSelectionChanged;
    private bool promptDeleteSelected;
    private KeyCode enteringKey;
    private int enteringButton = -1;
    private VanillaOptionsText keyboardTab, gamepadTab, deleteButton, cancelButton;
    private readonly KeyCode[] allKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode))).Where(k => (int)k > 0 && (int)k < (int)KeyCode.Mouse0).Distinct().ToArray();

    public static VanillaOptionsMenu Open(MenuV2 owner)
    {
        if (Active != null) return Active;
        var host = new GameObject("Vanilla Options", typeof(RectTransform));
        host.SetActive(false);
        var screen = host.AddComponent<VanillaOptionsMenu>();
        screen.menu = owner;
        screen.previousCursor = Cursor.visible;
        VanillaPreferences.Migrate();
        screen.Build();
        owner.mainScreen.gameObject.SetActive(false);
        owner.optionsScreen.gameObject.SetActive(false);
        owner.inputBlocker.enabled = false;
        LeanTween.cancel(owner.musicSource.gameObject);
        Active = screen;
        screen.openedFrame = Time.frameCount;
        screen.ShowPage(Page.Options);
        host.SetActive(true);
        Cursor.visible = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (DiscordController.instance != null) DiscordController.instance.SetMenuState("Editing Options");
        return screen;
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        var matte = Rect("Matte", transform, 0,0,0,0);
        matte.anchorMin = Vector2.zero;
        matte.anchorMax = Vector2.one;
        matte.offsetMin = matte.offsetMax = Vector2.zero;
        matte.gameObject.AddComponent<Image>().color = new Color32(88,80,35,255);
        Viewport = Rect("Viewport", transform, 0,0,1280,720);
        Viewport.anchorMin = Viewport.anchorMax = Viewport.pivot = new Vector2(.5f,.5f);
        Viewport.gameObject.AddComponent<Image>().raycastTarget = false;
        Viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var background = Rect("Background", Viewport, -64,-36,1408,792).gameObject.AddComponent<RawImage>();
        background.texture = Resources.Load<Texture2D>("VanillaOptions/menuBG");
        float height = 1408f * background.texture.height / background.texture.width;
        background.rectTransform.sizeDelta = new Vector2(1408,height);
        background.rectTransform.anchoredPosition = new Vector2(-64,(height-720)/2);
        backgroundMaterial = new Material(Resources.Load<Shader>("VanillaOptions/Background"));
        background.material = backgroundMaterial;
        content = Rect("Page", Viewport,0,0,1280,720);
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        drums = gameObject.AddComponent<AudioSource>();
        drums.playOnAwake = false;
        drums.loop = true;
        drums.clip = Resources.Load<AudioClip>("VanillaOptions/drumsLoop");
        drums.volume = 0;
        scrollSound = Resources.Load<AudioClip>("VanillaOptions/scrollMenu");
        confirmSound = Resources.Load<AudioClip>("VanillaOptions/confirmMenu");
        cancelSound = Resources.Load<AudioClip>("VanillaOptions/cancelMenu");
    }

    public static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y);
        rect.sizeDelta = new Vector2(width,height);
        return rect;
    }

    private static VanillaOptionsText Atlas(string label, Transform parent, float x, float y, bool bold=true)
    {
        var text = Rect(label,parent,x,y,0,0).gameObject.AddComponent<VanillaOptionsText>();
        text.Initialize(label,bold);
        return text;
    }

    private static Text Vcr(string label, Transform parent, float x, float y, float width, float height)
    {
        var text = Rect(label,parent,x,y,width,height).gameObject.AddComponent<Text>();
        text.font = Resources.Load<Font>("FunkinHud/Countdown/vcr");
        text.fontSize = 32;
        text.alignment = TextAnchor.UpperCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.text = label;
        text.raycastTarget = false;
        text.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3,-3);
        return text;
    }

    public void ShowPage(Page page)
    {
        if (PromptOpen || Busy) return;
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        rows.Clear(); headers.Clear();
        description = null; descriptionBox = null;
        keyboardTab = gamepadTab = null;
        CurrentPage = page;
        CameraScroll = 0;
        SelectedIndex = page == Page.Options ? rememberedIndex : 0;
        SelectedColumn = 0;
        deviceSelected = page == Page.Controls && Gamepad.all.Count > 0;
        controlSelectionChanged = false;
        openedFrame = Time.frameCount;
        lastVertical = VanillaControls.Axis(false);
        lastHorizontal = VanillaControls.Axis(true);
        verticalHeld = horizontalHeld = 0;
        switch (page)
        {
            case Page.Options:
                AddRoot("PREFERENCES", () => ShowPage(Page.Preferences));
                AddRoot("CONTROLS", () => ShowPage(Page.Controls));
                AddRoot("LAG ADJUSTMENT", () => StartCoroutine(EnterOffsets()));
                AddRoot("CLEAR SAVE DATA", OpenSavePrompt);
                AddRoot("EXIT", Close);
                break;
            case Page.Preferences: BuildPreferences(); break;
            case Page.Controls: BuildControls(); break;
            case Page.Offsets: BuildOffsets(); break;
        }
        Draw(0);
    }

    private void AddRoot(string label, Action action)
    {
        float y = 100 + rows.Count*100;
        var text = Atlas(label,content,0,y);
        text.rectTransform.anchoredPosition = new Vector2((1280-text.TextWidth)/2,-y);
        rows.Add(new Row { text=text, y=y, action=action });
    }

    private void BuildPreferences()
    {
        foreach (var preference in VanillaPreferences.Items)
        {
            float y = rows.Count*120+30;
            var row = new Row { text=Atlas(preference.label,content,120,y), y=y, preference=preference };
            if (preference.checkbox)
            {
                row.checkbox = Rect("Checkbox",content,0,y-30,0,0).gameObject.AddComponent<VanillaFreeplaySprite>();
                row.checkbox.drawScale = .7f;
                row.checkbox.centerScale = false;
                UpdateCheckbox(row);
            }
            else row.value = Atlas(preference.Display,content,15,y,false);
            rows.Add(row);
        }
        descriptionBox = Rect("Description Background",content,40,589,1200,87).gameObject.AddComponent<Image>();
        descriptionBox.color = new Color(0,0,0,.6f);
        description = Rect("Description",content,50,599,1180,62).gameObject.AddComponent<VanillaStoryText>();
        description.centered = true;
        description.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3,-3);
    }

    private void UpdateCheckbox(Row row)
    {
        bool selected = row.preference.Value != 0;
        row.checkbox.Load("checkboxThingie", selected ? "Check Box selecting animation" : "Check Box unselected",false,"VanillaOptions");
    }

    private void BuildControls()
    {
        string header = null;
        float y = Gamepad.all.Count > 0 ? 120 : 30;
        for (int i=0;i<VanillaControls.Bindings.Count;i++)
        {
            var binding = VanillaControls.Bindings[i];
            if (header != binding.header)
            {
                header = binding.header;
                var heading = Atlas(header,content,0,y);
                heading.rectTransform.anchoredPosition = new Vector2((1280-heading.TextWidth)/2,-y);
                headers.Add((heading,y));
                y += 70;
            }
            rows.Add(new Row
            {
                text=Atlas(binding.label,content,50,y), value=Atlas("---",content,750,y,false),
                secondary=Atlas("---",content,1050,y,false), y=y, binding=i
            });
            y += 70;
        }
        if (Gamepad.all.Count > 0)
        {
            var background = Rect("Device Background",content,0,0,1280,100).gameObject.AddComponent<Image>();
            background.color = new Color32(250,253,109,255);
            keyboardTab = Atlas("Keyboard",content,0,15);
            keyboardTab.rectTransform.anchoredPosition = new Vector2(640-keyboardTab.TextWidth-30,-15);
            gamepadTab = Atlas("Gamepad",content,670,15);
        }
        RefreshBindings();
    }

    private void RefreshBindings()
    {
        foreach (Row row in rows)
        {
            var binding = VanillaControls.Bindings[row.binding];
            int[] values = gamepadDevice ? binding.buttons : binding.keys;
            row.value.SetText(VanillaControls.Label(values[0],gamepadDevice));
            row.secondary.SetText(VanillaControls.Label(values[1],gamepadDevice));
        }
    }

    private void Update()
    {
        Cursor.visible = false;
        float delta = VanillaMenuTiming.Delta;
        Draw(delta);
        if (CurrentPage == Page.Offsets) TickOffsets(delta);
        if (Time.frameCount == openedFrame || VanillaTitleTransition.BlocksInput || Busy) return;
        if (PromptOpen) { UpdatePrompt(); return; }
        if (offsetMode != 0) return;
        bool back = VanillaControls.Pressed("BACK");
        if (back) { Back(); return; }
        float vertical = VanillaControls.Axis(false), horizontal = VanillaControls.Axis(true);
        int move = Repeat(vertical, ref lastVertical, ref verticalHeld,delta,float.PositiveInfinity);
        if (move != 0) MoveSelection(-move);
        bool numbers = CurrentPage == Page.Offsets && SelectedIndex == 0 || CurrentPage == Page.Preferences && rows[SelectedIndex].preference.choices == null;
        int adjust = Repeat(horizontal, ref lastHorizontal, ref horizontalHeld,delta,numbers?.08f:float.PositiveInfinity);
        if (adjust != 0) Adjust(adjust);
        if (VanillaControls.Pressed("ACCEPT")) Confirm();
    }

    private static int Repeat(float axis, ref float previous, ref float held, float delta, float rate)
    {
        if (axis == 0) { previous=0; held=0; return 0; }
        if (axis != previous) { previous=axis; held=.3f; return (int)axis; }
        if (float.IsPositiveInfinity(rate)) return 0;
        held -= delta;
        if (held > 0) return 0;
        held += rate;
        return (int)axis;
    }

    public void MoveSelection(int change)
    {
        if (Busy || PromptOpen || offsetMode != 0 || deviceSelected) return;
        SelectedIndex = (SelectedIndex+change%rows.Count+rows.Count)%rows.Count;
        if (CurrentPage == Page.Controls) controlSelectionChanged = true;
        if (CurrentPage == Page.Options) rememberedIndex = SelectedIndex;
        Play(scrollSound);
        Draw(0);
    }

    public void Adjust(int direction)
    {
        if (Busy || PromptOpen || offsetMode != 0) return;
        if (CurrentPage == Page.Controls)
        {
            if (deviceSelected) gamepadDevice = direction > 0;
            else SelectedColumn = Mathf.Clamp(SelectedColumn+direction,0,1);
            RefreshBindings();
            Play(scrollSound);
        }
        else if (CurrentPage == Page.Preferences)
        {
            var preference = rows[SelectedIndex].preference;
            if (preference.checkbox) return;
            int value = preference.Value+direction*preference.step;
            if (preference.choices != null) value = (value+preference.choices.Length)%preference.choices.Length;
            VanillaPreferences.Set(preference,value);
            rows[SelectedIndex].value.SetText(preference.Display);
        }
        else if (CurrentPage == Page.Offsets && SelectedIndex == 0) SetOffset(Pause.GlobalOffset+direction);
        Draw(0);
    }

    public void Confirm()
    {
        if (Busy || PromptOpen || offsetMode != 0) return;
        if (CurrentPage == Page.Controls)
        {
            if (deviceSelected) { deviceSelected=false; controlSelectionChanged=true; Draw(0); }
            else OpenRebindPrompt();
            return;
        }
        Row row = rows[SelectedIndex];
        if (row.preference != null && !row.preference.checkbox) return;
        if (CurrentPage == Page.Offsets && SelectedIndex == 0) return;
        StartCoroutine(ConfirmRow(row));
    }

    private IEnumerator ConfirmRow(Row row)
    {
        Busy = true;
        Play(confirmSound);
        float age = 0;
        while (age < 1)
        {
            row.text.enabled = !VanillaPreferences.FlashingLights || (int)(age/.06f)%2 == 0;
            age += VanillaMenuTiming.Delta;
            yield return null;
        }
        row.text.enabled = true;
        Busy = false;
        if (row.preference != null)
        {
            VanillaPreferences.Set(row.preference,1-row.preference.Value);
            UpdateCheckbox(row);
        }
        else row.action?.Invoke();
    }

    public void Back()
    {
        if (Busy) return;
        if (PromptOpen) { ClosePrompt(); return; }
        if (offsetMode != 0) { ExitCalibration(true); return; }
        Play(cancelSound);
        if (CurrentPage == Page.Options) Close();
        else if (CurrentPage == Page.Controls && !deviceSelected && keyboardTab != null) deviceSelected = true;
        else if (CurrentPage == Page.Offsets) StartCoroutine(LeaveOffsets());
        else ShowPage(Page.Options);
    }

    private void Draw(float delta)
    {
        if (rows.Count == 0) return;
        float margin = CurrentPage == Page.Preferences ? 160 : CurrentPage == Page.Controls ? 100 : 75;
        float follow = deviceSelected ? 15 : rows[SelectedIndex].y;
        float target = CameraScroll;
        if (follow < CameraScroll+margin) target = follow-margin;
        if (follow+70 > CameraScroll+720-margin) target = follow+70-(720-margin);
        target = Mathf.Max(CurrentPage == Page.Options ? -75 : 0,target);
        CameraScroll = Mathf.Lerp(CameraScroll,target,1-Mathf.Pow(CurrentPage == Page.Controls ? .94f : .915f,delta*60));
        for (int i=0;i<rows.Count;i++)
        {
            Row row = rows[i];
            bool selected = i == SelectedIndex && !deviceSelected;
            row.text.color = new Color(1,1,1,selected && (CurrentPage != Page.Controls || controlSelectionChanged)?1:.6f);
            float y = row.y-CameraScroll;
            if (CurrentPage == Page.Preferences)
            {
                float x = selected?150:120;
                if (row.value != null) x += row.value.TextWidth-75;
                row.text.rectTransform.anchoredPosition = new Vector2(x,-y);
                if (row.value != null) row.value.rectTransform.anchoredPosition = new Vector2(15,-y);
                if (row.checkbox != null)
                {
                    bool on = row.preference.Value != 0;
                    row.checkbox.rectTransform.anchoredPosition = new Vector2(21+(on?-17:0),-(y-30)-33.75f+(on?70:0));
                }
            }
            else if (CurrentPage == Page.Controls)
            {
                row.text.rectTransform.anchoredPosition = new Vector2(50,-y);
                row.value.rectTransform.anchoredPosition = new Vector2(750,-y);
                row.secondary.rectTransform.anchoredPosition = new Vector2(1050,-y);
                row.value.color = new Color(1,1,1,selected && SelectedColumn==0?1:.6f);
                row.secondary.color = new Color(1,1,1,selected && SelectedColumn==1?1:.6f);
            }
            else if (CurrentPage == Page.Options) row.text.rectTransform.anchoredPosition = new Vector2((1280-row.text.TextWidth)/2,-y);
        }
        foreach (var heading in headers)
            heading.text.rectTransform.anchoredPosition = new Vector2((1280-heading.text.TextWidth)/2,-heading.y+CameraScroll);
        if (description != null)
        {
            description.Text = WrapDescription(rows[SelectedIndex].preference.description);
            description.rectTransform.anchoredPosition = new Vector2((1280-description.rectTransform.sizeDelta.x)/2,-599);
        }
        if (keyboardTab != null)
        {
            keyboardTab.color = new Color(1,1,1,deviceSelected && !gamepadDevice?1:.6f);
            gamepadTab.color = new Color(1,1,1,deviceSelected && gamepadDevice?1:.6f);
        }
    }

    private static string WrapDescription(string value)
    {
        var lines = new List<string>();
        foreach (string paragraph in value.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                if (line.Length > 0 && line.Length+word.Length+1 > 61) { lines.Add(line); line=""; }
                line += (line.Length==0?"":" ")+word;
            }
            lines.Add(line);
        }
        return string.Join("\n",lines);
    }

    private void CreatePrompt(string message)
    {
        prompt = Rect("Prompt",Viewport,100,100,1080,520);
        prompt.gameObject.AddComponent<Image>().color = new Color32(250,253,109,255);
        var text = Atlas(message,prompt,0,0);
        text.rectTransform.anchoredPosition = new Vector2((1080-text.TextWidth)/2,0);
        promptFrame = Time.frameCount;
    }

    public void OpenSavePrompt()
    {
        if (PromptOpen) return;
        CreatePrompt("This will delete\n\nALL your save data.\n\nAre you sure?");
        deleteButton = Atlas("Delete",prompt,0,450);
        deleteButton.rectTransform.anchoredPosition = new Vector2(1080-deleteButton.TextWidth,-450);
        cancelButton = Atlas("Cancel",prompt,0,450);
        promptDeleteSelected = true;
        DrawPromptButtons();
    }

    public void OpenRebindPrompt()
    {
        enteringKey = allKeys.FirstOrDefault(Input.GetKeyDown);
        enteringButton = -1;
        if (Gamepad.current != null)
            for (int i=0;i<VanillaControls.ButtonNames.Length;i++)
                if (VanillaControls.Button(Gamepad.current,i).wasPressedThisFrame) { enteringButton=i; break; }
        rebinding = true;
        VanillaControls.Capturing = true;
        CreatePrompt(gamepadDevice ? "\nPress any button\n   to rebind\n\n\n Back to cancel" : "\nPress any key to rebind\n\n\nBackspace to unbind\n    Escape to cancel");
    }

    private void UpdatePrompt()
    {
        if (Time.frameCount == promptFrame) return;
        if (forbidden)
        {
            if (Input.GetKeyUp(KeyCode.Escape) || Gamepad.current?.selectButton.wasReleasedThisFrame == true) ClosePrompt();
            return;
        }
        if (rebinding)
        {
            foreach (KeyCode key in allKeys)
            {
                if (!Input.GetKeyUp(key)) continue;
                if (key == enteringKey) { enteringKey=KeyCode.None; continue; }
                if (key == KeyCode.Escape) { ClosePrompt(); return; }
                if (key == KeyCode.Backspace) { FinishRebind(-1); return; }
                if (!gamepadDevice) { FinishRebind((int)key); return; }
            }
            if (gamepadDevice && Gamepad.current != null)
                for (int i=0;i<VanillaControls.ButtonNames.Length;i++)
                {
                    if (!VanillaControls.Button(Gamepad.current,i).wasReleasedThisFrame) continue;
                    if (i == enteringButton) { enteringButton=-1; continue; }
                    if (i == 5) ClosePrompt(); else FinishRebind(i);
                    return;
                }
            return;
        }
        if (VanillaControls.Pressed("BACK")) { ClosePrompt(); Play(cancelSound); return; }
        if (VanillaControls.Pressed("UI_LEFT") || VanillaControls.Pressed("UI_RIGHT"))
        {
            promptDeleteSelected = !promptDeleteSelected;
            Play(scrollSound);
            DrawPromptButtons();
        }
        if (!VanillaControls.Pressed("ACCEPT")) return;
        if (!promptDeleteSelected) { ClosePrompt(); return; }
        StartCoroutine(DeleteSave());
    }

    private IEnumerator DeleteSave()
    {
        Busy = true;
        Play(confirmSound);
        yield return new WaitForSecondsRealtime(1);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        VanillaControls.Reload();
        VanillaPreferences.Apply();
        VanillaTitleScreen.ResetEntry();
        MenuV2.startPhase = MenuV2.StartPhase.Nothing;
        SceneManager.LoadScene("Title");
    }

    private void DrawPromptButtons()
    {
        deleteButton.color = new Color(1,1,1,promptDeleteSelected?1:.6f);
        cancelButton.color = new Color(1,1,1,promptDeleteSelected?.6f:1);
    }

    public bool FinishRebind(int input)
    {
        bool accepted = VanillaControls.Rebind(rows[SelectedIndex].binding,SelectedColumn,input,gamepadDevice);
        ClosePrompt();
        RefreshBindings();
        if (!accepted)
        {
            forbidden = true;
            CreatePrompt("\nYou cannot unbind\nthat key!\n\n\nEscape to exit");
        }
        return accepted;
    }

    public void ClosePrompt()
    {
        if (prompt != null) { prompt.gameObject.SetActive(false); Destroy(prompt.gameObject); }
        prompt = null;
        rebinding = forbidden = false;
        VanillaControls.Capturing = false;
        openedFrame = Time.frameCount;
    }

    private void Play(AudioClip clip) => effects.PlayOneShot(clip,OptionsV2.miscVolume*(clip == scrollSound?.4f:1));

    public void Close()
    {
        if (Busy || CurrentPage != Page.Options) return;
        PlayerPrefs.Save();
        Active = null;
        Cursor.visible = previousCursor;
        menu.mainScreen.gameObject.SetActive(true);
        menu.inputBlocker.enabled = false;
        if (DiscordController.instance != null) DiscordController.instance.SetMenuState("Idle");
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        InputSystem.onEvent -= CaptureOffsetInput;
        if (Active == this) Active = null;
        VanillaControls.Capturing = false;
        if (backgroundMaterial != null) Destroy(backgroundMaterial);
    }
}
