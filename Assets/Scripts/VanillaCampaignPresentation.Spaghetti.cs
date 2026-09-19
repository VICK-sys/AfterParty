using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class VanillaCampaignPresentation
{
    private bool spaghettiCamera;
    public float SpaghettiIntroTime { get; private set; }
    public int SpaghettiEndingCard { get; private set; }

    public void FlashSpaghetti(float duration)
    {
        Initialize(Song.instance);
        StartCoroutine(SpaghettiFlash(duration));
    }

    private IEnumerator SpaghettiFlash(float duration)
    {
        var flash = Overlay(Color.white);
        for (float time = 0; time < duration; time += Time.deltaTime)
        {
            flash.color = new Color(1, 1, 1, 1 - time / duration);
            yield return null;
        }
        Destroy(flash.gameObject);
    }

    private IEnumerator SpaghettiIntro()
    {
        Busy = spaghettiCamera = true;
        var stage = song.vanillaPlayback.CampaignStage;
        song.uiCamera.enabled = false;
        song.battleCanvas.enabled = false;
        stage.SetSpaghettiCutscene(true);
        var fade = TakeIntroCover(Color.black);
        while (!stage.SpaghettiAudioReady) yield return null;
        var label = CreateSkipText();
        label.text = "Skip [ " + VanillaControls.Label(VanillaControls.Find("CUTSCENE_ADVANCE").keys[0], false) + " ]";
        label.alignment = TextAnchor.UpperLeft;
        label.rectTransform.anchoredPosition = new Vector2(936, -618);
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        float requested = -1;
        bool audioStarted = false;
        bool crashed = false;
        bool revealed = false;
        bool gotUp = false;
        bool[] taps = new bool[4];
        for (float time = 0; time < 730f / 24; time += Time.deltaTime)
        {
            SpaghettiIntroTime = time;
            float frame = time * 24;
            if (TakeAdvanceInput() && frame < 499)
            {
                if (requested < 0) requested = time;
                else if (time - requested >= .5f)
                {
                    for (float age = 0; age < .5f; age += Time.deltaTime)
                    {
                        fade.color = new Color(0, 0, 0, age * 2);
                        stage.FadeSpaghettiSound(1 - age * 2);
                        yield return null;
                    }
                    stage.StopSpaghettiSound();
                    stage.SetSpaghettiCutscene(false);
                    stage.SpaghettiAdjustment = new Vector4(6, -74, -24, -26);
                    stage.SetSpaghettiCover(0);
                    stage.SpaghettiGetUp(true);
                    Vector3 from = song.mainCamera.transform.position;
                    float zoom = 3.6f / song.mainCamera.orthographicSize;
                    Destroy(label.gameObject);
                    StartCoroutine(SpaghettiSkipReturn(fade, from, zoom));
                    song.uiCamera.enabled = song.battleCanvas.enabled = true;
                    Busy = false;
                    yield break;
                }
            }
            label.color = new Color(1, 1, 1, requested < 0 ? 0 : (1 - Mathf.Pow(1 - Mathf.Clamp01((time - requested) * 2), 2))
                * (1 - Mathf.Pow(Mathf.Clamp01((frame - 499) / 12), 2)));
            if (!audioStarted && frame >= 20) { stage.PlaySpaghettiSound("startCutscene"); audioStarted = true; }
            int[] frames = { 245, 251, 406, 411 };
            for (int i = 0; i < frames.Length; i++)
                if (!taps[i] && frame >= frames[i])
                {
                    stage.DarkenSpaghetti(i % 2 == 0 ? .2f : 0, i % 2 == 0 ? .01f : .8f);
                    taps[i] = true;
                }
            if (frame < 563)
            {
                float eased = Mathf.Sqrt(1 - Mathf.Pow(Mathf.Clamp01(time / 3) - 1, 2));
                song.mainCamera.transform.position = Vector3.Lerp(new Vector3(6.6f, 2, -10), new Vector3(6.6f, -3, -10), eased);
                song.mainCamera.orthographicSize = 3.6f / Mathf.Lerp(.5f, .7f, eased);
                fade.color = new Color(0, 0, 0, 1 - Mathf.Clamp01(time / 3));
                if (frame >= 548)
                    stage.SpaghettiAdjustment = Vector4.Lerp(new Vector4(0, 0, 55, -30), new Vector4(10, 0, 66, -17), Mathf.Pow(2, 10 * ((frame - 548) / 15 - 1)));
                else if (frame >= 499)
                    stage.SpaghettiAdjustment = new Vector4(0, 0, 55, -30) * Mathf.Sin((frame - 499) / 49 * Mathf.PI / 2);
            }
            else
            {
                if (!crashed)
                {
                    crashed = true;
                    stage.SetSpaghettiCutscene(false);
                    stage.SetSpaghettiCover(1);
                    stage.SpaghettiAdjustment = Vector4.zero;
                }
                fade.color = new Color(1, 1, 1, 1 - Mathf.Clamp01((frame - 563) / 30));
            }
            if (frame >= 650)
            {
                if (!revealed)
                {
                    revealed = true;
                    stage.SpaghettiAdjustment = new Vector4(6, -74, -24, -26);
                    stage.SpaghettiGetUp(still: true);
                }
                float progress = Mathf.Clamp01((frame - 650) / 72);
                float eased = Mathf.Sqrt(1 - (progress - 1) * (progress - 1));
                song.mainCamera.transform.position = new Vector3(10.7f, -4.7f, -10);
                song.mainCamera.orthographicSize = 3.6f / Mathf.Lerp(.7f, .55f, eased);
                stage.SetSpaghettiCover(1 - Mathf.Sin(progress * Mathf.PI / 2));
            }
            if (!gotUp && frame >= 710) { stage.SpaghettiGetUp(); gotUp = true; }
            yield return null;
        }
        stage.SetSpaghettiCover(0);
        Destroy(label.gameObject);
        Destroy(fade.gameObject);
        song.uiCamera.enabled = song.battleCanvas.enabled = true;
        Busy = spaghettiCamera = false;
    }

    private IEnumerator SpaghettiSkipReturn(Image fade, Vector3 from, float zoom)
    {
        for (float time = 0; time < 1; time += Time.deltaTime)
        {
            float eased = Mathf.Sqrt(1 - (time - 1) * (time - 1));
            song.mainCamera.transform.position = Vector3.Lerp(from, new Vector3(10.7f, -4.7f, -10), eased);
            song.mainCamera.orthographicSize = 3.6f / Mathf.Lerp(zoom, .55f, eased);
            fade.color = new Color(0, 0, 0, 1 - Mathf.Clamp01(time * 2));
            yield return null;
        }
        spaghettiCamera = false;
        Destroy(fade.gameObject);
    }

    public void BeginSpaghettiEnding(Song owner)
    {
        Initialize(owner);
        if (outroStarted) return;
        outroStarted = true;
        StartCoroutine(SpaghettiEnding());
    }

    private RawImage SpaghettiCard(string name, bool center)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(Path.Combine(Root(9), "effects/end", name, name + ".png")));
        float width = texture.width * .67f;
        float height = texture.height * .67f;
        var card = Rect(name, viewport, center ? (1280 - width) / 2 : 1280 - width - 40,
            center ? (720 - height) / 2 : 720 - height - 40, width, height).gameObject.AddComponent<RawImage>();
        card.texture = texture;
        return card;
    }

    private IEnumerator SpaghettiEnding()
    {
        var stage = song.vanillaPlayback.CampaignStage;
        stage.PlaySpaghettiSound("end1");
        yield return new WaitForSeconds(.05f);
        Busy = true;
        stage.SetSpaghettiCover(1);
        song.uiCamera.enabled = song.battleCanvas.enabled = false;
        var first = SpaghettiCard("end1", true);
        SpaghettiEndingCard = 1;
        yield return new WaitForSeconds(3.95f);
        Destroy(first.texture);
        Destroy(first.gameObject);
        var second = SpaghettiCard("end2", false);
        stage.PlaySpaghettiSound("end2");
        SpaghettiEndingCard = 2;
        yield return new WaitForSeconds(4);
        Destroy(second.texture);
        Destroy(second.gameObject);
        SpaghettiEndingCard = 0;
        yield return new WaitForSeconds(1);
        OutroFinished = true;
        Busy = false;
        song.CompleteScriptedSong();
    }
}
