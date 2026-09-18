using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class VanillaPauseMenu : MonoBehaviour
{
    private sealed class Entry
    {
        public string label;
        public Action action;
        public VanillaPauseText text;
        public Vector2 from;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<VanillaPauseText> metadata = new List<VanillaPauseText>();
    private readonly float[] metadataY = { 15, 47, 79, 111, 143, 642, 665 };
    private Pause owner;
    private RectTransform viewport;
    private Image background;
    private AudioSource music;
    private AudioSource sound;
    private AudioClip scroll;
    private float age;
    private float selectionAge;
    private float offset;
    private float offsetHold;
    private bool fastOffset;
    private bool focused = true;
    private float previousVertical;
    private int openingFrame;
    public int SelectedIndex { get; private set; }
    public bool DifficultyMenu { get; private set; }
    public string[] Labels => entries.Select(entry => entry.label).ToArray();
    public AudioSource Music => music;

    public void Initialize(Pause pause)
    {
        owner = pause;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform screen = Rect("Input Blocker", transform, 0, 0, 0, 0);
        screen.anchorMin = Vector2.zero;
        screen.anchorMax = Vector2.one;
        screen.offsetMin = screen.offsetMax = Vector2.zero;
        screen.gameObject.AddComponent<Image>().color = Color.clear;
        viewport = Rect("Viewport", transform, 0, 0, 1280, 720);
        viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.gameObject.AddComponent<Image>().raycastTarget = false;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        background = Rect("Background", viewport, 0, 0, 1280, 720).gameObject.AddComponent<Image>();
        background.raycastTarget = false;
        for (int index = 0; index < metadataY.Length; index++)
        {
            var text = Text("Metadata " + index, viewport, false, index < 5 ? 32 : 16);
            text.rectTransform.sizeDelta = new Vector2(index < 5 ? 1240 : 1250, 80);
            text.rightAligned = true;
            metadata.Add(text);
        }
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        scroll = Resources.Load<AudioClip>("FunkinPause/scrollMenu");
    }

    private void OnEnable()
    {
        if (owner == null) return;
        age = 0;
        offset = Pause.GlobalOffset;
        offsetHold = 0;
        fastOffset = false;
        openingFrame = Time.frameCount;
        previousVertical = Input.GetAxisRaw("Vertical");
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        music.clip = Resources.Load<AudioClip>("FunkinPause/breakfast" + (Song.instance != null && Song.instance.vanillaPlayback != null && Song.instance.vanillaPlayback.IsPixel ? "-pixel" : ""));
        music.volume = 0;
        if (music.clip != null)
        {
            music.time = UnityEngine.Random.Range(0, (int)(music.clip.length * 500) + 1) / 1000f;
            music.Play();
        }
        ShowStandard();
        Render(0);
    }

    private void OnDisable()
    {
        StopMusic();
        PlayerPrefs.Save();
    }

    public void StopMusic()
    {
        if (music != null) music.Stop();
    }

    private void OnApplicationFocus(bool focus)
    {
        focused = focus;
        if (music == null) return;
        if (focus) music.UnPause();
        else music.Pause();
    }

    public void ShowStandard()
    {
        DifficultyMenu = false;
        ClearEntries();
        Add("Resume", owner.ContinueSong);
        Add("Restart Song", owner.RestartSong);
        Add("Change Difficulty", ShowDifficulties);
        if (!Pause.PracticeMode) Add("Enable Practice Mode", owner.EnablePractice);
        Add("Exit to Menu", owner.QuitSong);
        FinishEntries();
    }

    public void ShowDifficulties()
    {
        DifficultyMenu = true;
        ClearEntries();
        foreach (string difficulty in owner.Difficulties())
        {
            string selected = difficulty;
            Add(Pause.TitleCase(difficulty), () => owner.ChangeDifficulty(selected));
        }
        Add("Back", ShowStandard);
        FinishEntries();
    }

    private void ClearEntries()
    {
        foreach (Entry entry in entries)
        {
            entry.text.gameObject.SetActive(false);
            Destroy(entry.text.gameObject);
        }
        entries.Clear();
        SelectedIndex = 0;
    }

    private void Add(string label, Action action)
    {
        var text = Text(label, viewport, true);
        text.SetText(label);
        Position(text.rectTransform, 0, 70 * entries.Count + 30);
        entries.Add(new Entry { label = label, action = action, text = text });
    }

    private void FinishEntries()
    {
        metadata[4].gameObject.SetActive(Pause.PracticeMode);
        ChangeSelection(0);
    }

    public void ChangeSelection(int change)
    {
        if (entries.Count == 0 || owner.Transitioning) return;
        int previous = SelectedIndex;
        SelectedIndex = (SelectedIndex + change % entries.Count + entries.Count) % entries.Count;
        if (SelectedIndex != previous && scroll != null) sound.PlayOneShot(scroll, 0.4f * InGameVolume.menuVolume);
        foreach (Entry entry in entries) entry.from = entry.text.rectTransform.anchoredPosition;
        selectionAge = 0;
        RenderEntries();
    }

    public void Accept()
    {
        if (entries.Count != 0 && !owner.Transitioning) entries[SelectedIndex].action();
    }

    private void Update()
    {
        if (!focused || owner == null) return;
        Render(Time.unscaledDeltaTime);
        if (owner.Transitioning || Time.frameCount == openingFrame) return;
        float vertical = Input.GetAxisRaw("Vertical");
        bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || vertical > 0.5f && previousVertical <= 0.5f;
        bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || vertical < -0.5f && previousVertical >= -0.5f;
        previousVertical = vertical;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool heldUp = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || vertical > 0.5f;
        bool heldDown = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || vertical < -0.5f;
        if (shift && (heldUp || heldDown))
        {
            offsetHold += Time.unscaledDeltaTime;
            if (!fastOffset)
            {
                if (offsetHold > 0.5f) { fastOffset = true; offsetHold = 0; }
                if (up || down) offset += heldUp ? 1 : -1;
            }
            else offset += (heldUp ? 1 : -1) * Time.unscaledDeltaTime * 30;
            offset = Mathf.Clamp(offset, -1500, 1500);
            Pause.SetGlobalOffset(offset);
            return;
        }
        fastOffset = false;
        offsetHold = 0;
        if (up) ChangeSelection(-1);
        if (down) ChangeSelection(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0)) Accept();
        else if (Input.GetKeyDown(Player.pauseKey) || Input.GetKeyDown(KeyCode.Escape)
            || Input.GetKeyDown(KeyCode.JoystickButton7)) owner.ContinueSong();
    }

    public void Render(float delta)
    {
        age += delta;
        selectionAge += delta;
        background.color = new Color(0, 0, 0, 0.6f * Ease(age / 0.8f));
        music.volume = Mathf.Clamp01(age / 5) * 0.75f * InGameVolume.menuVolume;
        SongMetaV2 meta = Song.currentSongMeta;
        SongVariation variation = meta?.GetVariation(Song.difficulty);
        Dictionary<string, string> credits = variation?.credits ?? meta?.credits;
        string artist = Credit(credits, "Composer");
        string charter = Credit(credits, "Charter");
        float cycle = age % 33;
        bool showCharter = cycle >= 15.75f && cycle < 32.25f;
        metadata[0].SetText(variation?.songName ?? meta?.songName ?? "Song Name");
        metadata[1].SetText((showCharter ? "Charter: " : "Artist: ") + (showCharter ? charter : artist));
        metadata[2].SetText("Difficulty: " + Pause.TitleCase(Song.difficulty));
        metadata[3].SetText(Pause.DeathCount + " Blue Balls");
        metadata[4].SetText("PRACTICE MODE");
        metadata[5].SetText("Global Offset: " + Pause.GlobalOffset + "ms");
        metadata[6].SetText("Hold SHIFT-UP/DOWN,\nto change the offset.");
        for (int index = 0; index < metadata.Count; index++)
        {
            float enter = Ease((age - 0.1f * (index + 1)) / 1.8f);
            float alpha = enter;
            float fade = cycle % 16.5f;
            if (index == 1 && fade >= 15 && fade < 15.75f) alpha = 1 - Ease((fade - 15) / 0.75f);
            if (index == 1 && fade >= 15.75f) alpha = Ease((fade - 15.75f) / 0.75f);
            metadata[index].color = new Color(1, 1, 1, alpha);
            Position(metadata[index].rectTransform, 20, metadataY[index] + 5 * enter);
        }
        RenderEntries();
        foreach (Entry entry in entries) entry.text.Tick(delta);
    }

    private void RenderEntries()
    {
        for (int index = 0; index < entries.Count; index++)
        {
            Entry entry = entries[index];
            Vector2 target = new Vector2(90 + (index - SelectedIndex) * 26, -(720 * 0.48f + (index - SelectedIndex) * 156));
            entry.text.rectTransform.anchoredPosition = Vector2.Lerp(entry.from, target, Ease(selectionAge / 0.33f));
            entry.text.color = new Color(1, 1, 1, index == SelectedIndex ? 1 : 0.6f);
        }
    }

    private static string Credit(Dictionary<string, string> credits, string role)
    {
        if (credits != null)
            foreach (var credit in credits)
                if (string.Equals(credit.Key, role, StringComparison.OrdinalIgnoreCase)) return credit.Value ?? "Unknown";
        return "Unknown";
    }

    private static float Ease(float value) => 1 - Mathf.Pow(1 - Mathf.Clamp01(value), 4);

    private static VanillaPauseText Text(string name, Transform parent, bool bold, int size = 32)
    {
        VanillaPauseText text = Rect(name, parent, 0, 0, 1280, 100).gameObject.AddComponent<VanillaPauseText>();
        text.Initialize(bold, size);
        return text;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        Position(rect, x, y);
        return rect;
    }

    private static void Position(RectTransform rect, float x, float y) => rect.anchoredPosition = new Vector2(x, -y);
}
