using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class VanillaFreeplay
{
    private readonly List<(VanillaFreeplaySprite sprite, float speed, float x, float y)> picoLoops = new List<(VanillaFreeplaySprite, float, float, float)>();
    private VanillaFreeplayAnimate picoConfirm;
    private Material picoMultiply;
    private Material picoAdditive;
    private VanillaFreeplaySprite picoGlow;
    private VanillaFreeplaySprite picoDark;
    private VanillaFreeplaySprite picoBlue;
    private int picoBeat = -1;
    private float picoGlowAge = 1;
    private float characterTransitionAge = -1;
    private VanillaFreeplayTransition characterTransition;
    private float characterPreviewVolume;
    private readonly List<(RectTransform rect, Vector2 start, float distance)> characterMovers = new List<(RectTransform, Vector2, float)>();

    public void OpenCharacterSelect()
    {
        if (Busy || selectingMode) return;
        StartCoroutine(CharacterTransition());
    }

    private IEnumerator CharacterTransition()
    {
        closing = true;
        if (previewRoutine != null) StopCoroutine(previewRoutine);
        previewRoutine = null;
        if (previewLoad != null) { previewLoad.Abort(); previewLoad.Dispose(); previewLoad = null; }
        StopCartoon(false);
        Sound("confirmMenu", 1);
        dj.Play("To Character Select", false);
        for (float elapsed = 0; elapsed < .45f; elapsed += VanillaMenuTiming.Delta) yield return null;
        BeginCharacterWipe();
        while (characterTransitionAge < .9f || !dj.Finished || effects.isPlaying) yield return null;
        CancelPreview();
        Destroy(characterTransition.gameObject);
        characterTransition = null;
        VanillaCharacterSelect.Open(character =>
        {
            var owner = menu;
            string root = userBundleRoot;
            Active = null;
            Destroy(gameObject);
            Open(owner, false, root, availableSongs, true);
        });
        gameObject.SetActive(false);
    }

    private void BeginCharacterWipe()
    {
        characterTransitionAge = 0;
        characterPreviewVolume = preview.volume;
        AddCharacterMover(dj.rectTransform, 175);
        AddCharacterMover(backing.rectTransform, 100);
        AddCharacterMover(card.rectTransform, 100);
        AddCharacterMover((RectTransform)cardRoot.Find("Band"), 40);
        AddCharacterMover((RectTransform)cardRoot.Find("Band Edge"), 40);
        AddCharacterMover(difficultyRoot, 270);
        AddCharacterMover(difficultyLabelRoot, 270);
        AddCharacterMover(filters, 270);
        AddCharacterMover(scoreRoot, 270);
        AddCharacterMover(topBar, 300);
        AddCharacterMover(headerRoot, 300);
        AddCharacterMover(characterHint.rectTransform, 300);
        foreach (var capsule in capsules)
            if (Mathf.Abs(capsule.index - SelectedIndex) < 6) AddCharacterMover(capsule.root, 250);
        foreach (var line in marquees) AddCharacterMover(line.rectTransform, 60);
        foreach (var item in picoLoops) AddCharacterMover(item.sprite.rectTransform, item.y == 80 ? 90 : item.y == 346 ? 80 : item.y == 406 ? 60 : 50);
        if (picoBlue != null) AddCharacterMover(picoBlue.rectTransform, 70);
        characterTransition = VanillaFreeplayTransition.Create(GetComponent<UnityEngine.Canvas>());
    }

    private void AddCharacterMover(RectTransform rect, float distance)
    {
        characterMovers.Add((rect, rect.anchoredPosition, distance));
    }

    private void DrawCharacterWipe(float delta)
    {
        characterTransitionAge += delta;
        preview.volume = characterPreviewVolume * (1 - Mathf.Clamp01(characterTransitionAge / .9f));
        float t = Mathf.Clamp01(characterTransitionAge / .8f);
        float eased = 2.70158f * t * t * t - 1.70158f * t * t;
        float scrollTime = .8f * 2 / Mathf.PI * Mathf.Sin(t * Mathf.PI / 2);
        foreach (var item in characterMovers) item.rect.anchoredPosition = item.start + Vector2.up * (item.distance * eased);
        for (int i = 0; i < marquees.Count; i++) marquees[i].rectTransform.anchoredPosition += Vector2.right * (marqueeSpeeds[i] * scrollTime);
        foreach (var item in picoLoops) item.sprite.rectTransform.anchoredPosition += Vector2.right * (item.speed * scrollTime);
        foreach (var dot in difficultyDots)
        {
            Color tint = dot.color;
            tint.a = Mathf.Pow(1 - Mathf.Clamp01(characterTransitionAge / .25f), 4);
            dot.color = tint;
        }
        characterTransition?.Draw(characterTransitionAge);
    }

    private void BuildPicoCard()
    {
        picoMultiply = VanillaCharacterSelect.Blend(UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, true);
        picoAdditive = VanillaCharacterSelect.Blend(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, false);
        foreach (var line in marquees) line.gameObject.SetActive(false);
        AddPicoLoop("lowerLoop", 200, 110, .39f, true);
        AddPicoLoop("lowerLoop", 406, -110, 1, false);
        AddPicoLoop("topLoop", 80, -220, 1, false, "base");
        AddPicoLoop("middleLoop", 346, 220, 1, false);
        picoBlue = Sprite("Pico Blue Bar", cardRoot, "freeplay/backingCards/pico/blueBar", 0, 239);
        picoBlue.color = new Color(1,1,1,.4f);
        picoBlue.material = picoMultiply;
        picoDark = Sprite("Pico Dark Glow", cardRoot, "freeplay/backingCards/pico/glow", -300, 330);
        picoDark.material = picoMultiply;
        picoGlow = Sprite("Pico Glow", cardRoot, "freeplay/backingCards/pico/glow", -300, 330);
        picoGlow.material = picoAdditive;
        picoConfirm = Animate("Pico Confirm", cardRoot, "freeplay/backingCards/pico/pico-confirm", -120, 55, true);
        picoConfirm.UseTimelineBounds();
        foreach (string symbol in new[] { "backplate flat blue", "pink back", "black flash", "white flash", "blue flash", "blue flash 2", "pink flash" })
            picoConfirm.ScaleSymbolElement(symbol, 2, 100);
        picoConfirm.gameObject.SetActive(false);
    }

    private void AddPicoLoop(string name, float y, float speed, float alpha, bool flip, string prefix = "")
    {
        for (int i = -1; i <= 2; i++)
        {
            var sprite = Sprite("Pico " + name + i, cardRoot, "freeplay/backingCards/pico/" + name, 0, y, prefix);
            sprite.flipX = flip;
            sprite.color = new Color(1,1,1,alpha);
            sprite.centerScale = false;
            sprite.loop = false;
            picoLoops.Add((sprite, speed, i * (sprite.FrameSize.x + (name == "middleLoop" ? 15 : 20)), y));
        }
    }

    private void UpdatePicoGlow(float delta, bool playing, float position, float bpm)
    {
        if (playing)
        {
            int beat = Mathf.FloorToInt(position * bpm / 60);
            if (picoBeat >= 0 && beat > picoBeat && beat % (1 << Mathf.Clamp(Mathf.FloorToInt(bpm / 140), 0, 3)) == 0)
                picoGlowAge = 0;
            picoBeat = beat;
        }
        else picoBeat = -1;
        picoGlowAge += delta;
    }

    private void DrawPicoCard(float delta)
    {
        float bpm = SelectedSong == null ? 145 : SelectedSong.Bpm(Difficulty);
        UpdatePicoGlow(delta, preview.isPlaying, preview.time, bpm);
        picoGlow.color = new Color(1,1,1,Mathf.Pow(1-Mathf.Clamp01(picoGlowAge/(16f/24)),4));
        picoDark.color = new Color(1,1,1,1-Mathf.Pow(1-Mathf.Clamp01(picoGlowAge/(18f/24)),4));
        picoGlow.gameObject.SetActive(ready && confirmAge < 0);
        picoDark.gameObject.SetActive(ready && confirmAge < 0);
        picoBlue.gameObject.SetActive(ready && confirmAge < 0);
        foreach (var item in picoLoops)
        {
            item.sprite.gameObject.SetActive(ready);
            float width = item.sprite.FrameSize.x + (item.y == 346 ? 15 : 20);
            item.sprite.rectTransform.anchoredPosition = new Vector2(item.x + age * item.speed % width, -item.y);
            if (item.y == 80 && item.sprite.FrameIndex == item.sprite.FrameCount - 1)
            {
                float progress = Mathf.Abs(age * item.speed % width);
                string next = progress < 300 ? "uzi info" : progress >= 500 && progress < 700 ? "sniper info" : progress >= 700 && progress < 1300 ? "rifle info" : progress >= 1450 && progress < 2000 ? "rocket launcher info" : null;
                if (next != null && !item.sprite.CurrentFrameName.StartsWith(next)) item.sprite.TryPlay(next, false);
            }
        }
        cardRoot.Find("Band").gameObject.SetActive(false);
        if (confirmAge >= 0 && !picoConfirm.gameObject.activeSelf)
        {
            picoConfirm.gameObject.SetActive(true);
            picoConfirm.PlayAll(false);
        }
        if (confirmAge >= 0)
        {
            confirmGlow.gameObject.SetActive(false);
            confirmText.gameObject.SetActive(false);
        }
    }

    private Color PicoConfirmColor()
    {
        float[] frames = { 0, 10, 14, 18, 21, 24 };
        string[] from = { "FFFFFF", "343036", "27292D", "2D282D", "29292F", "29232C" };
        string[] to = { "8A8A8A", "696366", "686A6F", "676164", "62626B", "808080" };
        int index = 0;
        for (int i = 1; i < frames.Length; i++) if (confirmAge * 24 >= frames[i]) index = i;
        float t = Mathf.Clamp01((confirmAge * 24 - frames[index]) / (index == 0 ? 10 : 3));
        return Color.Lerp(Hex(from[index]), Hex(to[index]), t == 1 ? 1 : 1 - Mathf.Pow(2,-10*t));
    }
}
