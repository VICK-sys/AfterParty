using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class VanillaFreeplay
{
    private RectTransform instrumentalRoot;
    private VanillaFreeplaySprite instrumentalBox;
    private VanillaFreeplaySprite instrumentalLeft;
    private VanillaFreeplaySprite instrumentalRight;
    private Text instrumentalLabel;
    private string[] instrumentalChoices;
    private int instrumentalIndex;
    private int instrumentalOpenedFrame;
    private bool instrumentalClosing;
    private Material instrumentalWhite;
    private Coroutine instrumentalLeftPulse;
    private Coroutine instrumentalRightPulse;
    public bool InstrumentalMenuOpen => instrumentalRoot != null;
    public string SelectedInstrumental => instrumentalChoices == null ? null : instrumentalChoices[instrumentalIndex];

    private void OpenInstrumentalMenu()
    {
        instrumentalChoices = SelectedSong?.Instrumentals(Difficulty) ?? new[] { "default", "random" };
        instrumentalIndex = 0;
        instrumentalClosing = false;
        if (instrumentalWhite == null) instrumentalWhite = new Material(Resources.Load<Shader>("VanillaResults/PureWhite"));
        instrumentalOpenedFrame = Time.frameCount;
        heldDirection = 0;
        previousHorizontal = Player.MenuAxis("Horizontal");
        Vector2 position = Target(SelectedIndex) + new Vector2(175, 115);
        instrumentalRoot = Rect("Instrumental Choice", list.parent, position.x, position.y, 329, 100);
        instrumentalBox = Sprite("Box", instrumentalRoot, "freeplay/instBox/instBox", 0, 0, "open0");
        instrumentalBox.TryPlay("open0", false, "idle0");
        float width = instrumentalBox.FrameSize.x;
        instrumentalLeft = Sprite("Previous Instrumental", instrumentalRoot, "freeplay/freeplaySelector", 4, 30, "arrow pointer loop");
        instrumentalRight = Sprite("Next Instrumental", instrumentalRoot, "freeplay/freeplaySelector", width - instrumentalLeft.FrameSize.x * .6f - 4, 30, "arrow pointer loop");
        instrumentalLeft.drawScale = instrumentalRight.drawScale = .6f;
        instrumentalLeft.centerScale = instrumentalRight.centerScale = false;
        instrumentalRight.flipX = true;
        Text heading = Label("Heading", instrumentalRoot, "INSTRUMENTAL", 0, 5, width, 32, 24, vcrFont);
        heading.alignment = TextAnchor.UpperCenter;
        instrumentalLabel = Label("Instrumental", instrumentalRoot, "", 0, 36, width, 54, 40, vcrFont);
        instrumentalLabel.alignment = TextAnchor.UpperCenter;
        Hit("Previous", instrumentalRoot, 4, 30, 30, 54, () => ChangeInstrumental(1));
        Hit("Next", instrumentalRoot, width - 34, 30, 30, 54, () => ChangeInstrumental(-1));
        Hit("Accept", instrumentalRoot, 34, 30, width - 68, 54, AcceptInstrumental);
        RefreshInstrumentalLabel();
    }

    private void UpdateInstrumentalMenu()
    {
        if (instrumentalClosing || Time.frameCount == instrumentalOpenedFrame) return;
        if (BackPressed()) { CancelInstrumental(); return; }
        float horizontal = Player.MenuAxis("Horizontal");
        if (horizontal < -.5f && previousHorizontal >= -.5f) ChangeInstrumental(1);
        if (horizontal > .5f && previousHorizontal <= .5f) ChangeInstrumental(-1);
        previousHorizontal = horizontal;
        if (VanillaControls.Pressed("ACCEPT")) AcceptInstrumental();
    }

    public void ChangeInstrumental(int direction)
    {
        if (instrumentalRoot == null || instrumentalClosing || closing) return;
        instrumentalIndex = (instrumentalIndex + direction % instrumentalChoices.Length + instrumentalChoices.Length) % instrumentalChoices.Length;
        RefreshInstrumentalLabel();
        if (direction > 0)
        {
            if (instrumentalLeftPulse != null) StopCoroutine(instrumentalLeftPulse);
            instrumentalLeftPulse = StartCoroutine(PulseInstrumentalArrow(instrumentalLeft));
        }
        else
        {
            if (instrumentalRightPulse != null) StopCoroutine(instrumentalRightPulse);
            instrumentalRightPulse = StartCoroutine(PulseInstrumentalArrow(instrumentalRight));
        }
    }

    private void RefreshInstrumentalLabel()
    {
        instrumentalLabel.text = string.IsNullOrEmpty(SelectedInstrumental) ? "Default" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(SelectedInstrumental);
    }

    private IEnumerator PulseInstrumentalArrow(VanillaFreeplaySprite arrow)
    {
        arrow.drawScale = .3f;
        arrow.material = instrumentalWhite;
        Vector2 position = arrow.rectTransform.anchoredPosition;
        arrow.rectTransform.anchoredPosition = new Vector2(position.x, -35);
        arrow.SetVerticesDirty();
        yield return new WaitForSecondsRealtime(2f / 24);
        if (arrow == null) yield break;
        arrow.drawScale = .6f;
        arrow.rectTransform.anchoredPosition = new Vector2(position.x, -30);
        arrow.material = null;
        arrow.SetVerticesDirty();
    }

    public void AcceptInstrumental()
    {
        if (instrumentalRoot == null || instrumentalClosing || closing || Time.frameCount == instrumentalOpenedFrame) return;
        ConfirmInstrumental(SelectedInstrumental);
    }

    public void CancelInstrumental()
    {
        if (instrumentalRoot == null || instrumentalClosing || closing) return;
        instrumentalClosing = true;
        instrumentalBox.PlayReverse("open0", null);
        StartCoroutine(CloseInstrumentalMenu());
    }

    private IEnumerator CloseInstrumentalMenu()
    {
        yield return new WaitForSecondsRealtime(instrumentalBox.FrameCount / 24f);
        Destroy(instrumentalRoot.gameObject);
        instrumentalRoot = null;
        instrumentalChoices = null;
        previousHorizontal = Player.MenuAxis("Horizontal");
        openedFrame = Time.frameCount;
    }
}
