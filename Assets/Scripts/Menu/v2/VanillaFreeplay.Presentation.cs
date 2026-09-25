using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class VanillaFreeplay
{
    private VanillaFreeplaySprite bfGlow, bfDark, cardBurst, confirmFlash;
    private VanillaFreeplayAnimate backingYeah;
    private Material bfMultiply;
    private int bfBeat = -1;
    private float bfGlowAge = 1;
    private bool specialBacking;
    private string albumId;
    private bool albumTitleEntered;
    private float intendedClear, displayedClear;
    private int previousClear = -1;
    private readonly List<VanillaFreeplaySprite> completionDigits = new List<VanillaFreeplaySprite>();
    private readonly Dictionary<string, Graphic> difficultyLabels = new Dictionary<string, Graphic>();
    private readonly List<Color> dotStart = new List<Color>();
    private readonly List<Color> dotTarget = new List<Color>();
    private readonly List<float> dotAge = new List<float>();
    private bool difficultyTransition;
    private float difficultyAge = 1;
    private int difficultyDirection;
    private readonly List<VanillaFreeplayAnimate> filterLetters = new List<VanillaFreeplayAnimate>();
    private readonly List<VanillaFreeplaySprite> filterSeparators = new List<VanillaFreeplaySprite>();
    private Vector2[] filterPositions;
    private float filterMoveAge = 1;
    private int filterMoveDirection;
    private bool favoriteRefresh;
    private Color previousBackingTint = Color.white;
    private static readonly string[] DifficultyArrowNames = { "Previous Difficulty", "Next Difficulty" };
    private static readonly float[] EntrancePositions = { 0, 0, .16f, .16f, .22f, .22f, .245f };
    public float ConfirmationDelay => IsPico ? 1.45f : 1;

    private Color ConfirmationBackdropColor()
    {
        Color tint = IsPico ? PicoConfirmColor() : confirmAge < .33f
            ? Color.Lerp(Hex("A8A8A8"), Hex("646464"), Mathf.Clamp01(confirmAge / .5f))
            : Color.Lerp(Hex("CDCDCD"), Hex("555555"), ExpoOut((confirmAge - .33f) / 2));
        tint = QuantizeTint(tint);
        Color extra = !IsPico && confirmAge >= .33f && confirmAge <= .5f
            ? QuantizeTint(Color.Lerp(Hex("A8A8A8"), Hex("646464"), confirmAge / .5f))
            : previousBackingTint;
        previousBackingTint = tint;
        return tint * extra;
    }

    private static Color QuantizeTint(Color value) => new Color(Mathf.Floor(value.r * 255) / 255,
        Mathf.Floor(value.g * 255) / 255, Mathf.Floor(value.b * 255) / 255, 1);

    private void BuildBackingEffects()
    {
        cardBurst = Sprite("Card Flash", cardRoot, "freeplay/cardGlow", -30, -30);
        cardBurst.gameObject.SetActive(false);
        if (IsPico) return;
        bfMultiply = VanillaCharacterSelect.Blend(UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, true);
        bfDark = Sprite("BF Dark Glow", cardRoot, "freeplay/beatglow", -300, 330);
        bfDark.material = bfMultiply;
        bfGlow = Sprite("BF Beat Glow", cardRoot, "freeplay/beatglow", -300, 330);
        confirmGlow.rectTransform.SetParent(cardRoot, false);
        confirmFlash = Sprite("Confirm Flash", cardRoot, "freeplay/confirmGlow", -30, 240);
        confirmFlash.gameObject.SetActive(false);
        confirmText.rectTransform.SetParent(cardRoot, false);
        backingYeah = Animate("Backing Yeah", cardRoot, "freeplay/backing-text-yeah", -320, 120, false);
        backingYeah.gameObject.SetActive(false);
        cardBurst.transform.SetAsLastSibling();
    }

    private void DrawBackingEffects(float delta)
    {
        DrawCardBurst(age - IntroDuration, .45f);
        if (IsPico) return;
        bool visible = ready && confirmAge < 0;
        bfGlow.gameObject.SetActive(visible);
        bfDark.gameObject.SetActive(visible);
        float bpm = SelectedSong?.Bpm(Difficulty) ?? 145;
        if (preview.isPlaying)
        {
            int beat = Mathf.FloorToInt(preview.time * bpm / 60);
            int frequency = 1 << Mathf.Clamp(Mathf.FloorToInt(bpm / 140), 0, 3);
            if (bfBeat >= 0 && beat != bfBeat && beat % frequency == 0) bfGlowAge = 0;
            bfBeat = beat;
        }
        else bfBeat = -1;
        bfGlowAge += delta;
        bfGlow.color = new Color(1, 1, 1, .8f * Mathf.Pow(1 - Mathf.Clamp01(bfGlowAge / (16f / 24)), 4));
        bfDark.color = new Color(1, 1, 1, .6f * (1 - Mathf.Pow(1 - Mathf.Clamp01(bfGlowAge / (18f / 24)), 4)));
        confirmFlash.gameObject.SetActive(confirmAge >= .33f);
        confirmFlash.color = new Color(1, 1, 1, 1 - Mathf.Clamp01((confirmAge - .33f) / .5f));
        backingYeah.gameObject.SetActive(confirmAge >= 0);
    }

    private void DrawCardBurst(float elapsed, float duration)
    {
        bool visible = elapsed >= 0 && elapsed < duration && menu.vanillaMenu.flashingLights;
        cardBurst.gameObject.SetActive(visible);
        if (!visible) return;
        float eased = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI / 2);
        cardBurst.color = new Color(1, 1, 1, 1 - eased);
        cardBurst.stretch = Vector2.one * (1 + .2f * eased);
        cardBurst.SetVerticesDirty();
    }

    private void RefreshBackingText()
    {
        bool special = SelectedSong?.Album(Difficulty) == "spaghetti";
        if (specialBacking == special) return;
        specialBacking = special;
        for (int i = 0; i < marquees.Count; i++)
        {
            string text = special ? i % 2 == 1 ? "SPECIAL SPECIAL" : "SPECIAL"
                : i % 2 == 1 ? "BOYFRIEND" : i == 2 ? "PROTECT YO NUTS" : "HOT BLOODED IN MORE WAYS THAN ONE";
            marquees[i].text = string.Join("   ", System.Linq.Enumerable.Repeat(text, 8));
        }
    }

    private void EnsureDifficulties()
    {
        foreach (string difficulty in difficulties)
        {
            if (difficultyLabels.ContainsKey(difficulty)) continue;
            string path = "freeplay/freeplay" + difficulty.ToLowerInvariant();
            Graphic label = Resources.Load<Texture2D>("VanillaFreeplay/" + path) != null
                ? Sprite(difficulty, difficultyLabelRoot, path, 90, 80, difficulty == "Nightmare" ? "idle" : "")
                : Label(difficulty, difficultyLabelRoot, difficulty.ToUpperInvariant(), 90, 80, 240, 80, 40, pixelFont);
            difficultyLabels.Add(difficulty, label);
        }
        foreach (var pair in difficultyLabels)
        {
            pair.Value.name = pair.Key == Difficulty ? "Name" : pair.Key;
            pair.Value.gameObject.SetActive(pair.Key == Difficulty);
            pair.Value.rectTransform.anchoredPosition = new Vector2(90, -80);
            pair.Value.color = Color.white;
        }
        difficultyTransition = false;
        if (difficultyDots.Count == 0)
        {
            BuildDifficultyDots();
            foreach (var dot in difficultyDots)
            {
                dotStart.Add(Color.clear);
                dotTarget.Add(Color.clear);
                dotAge.Add(1);
            }
        }
    }

    private void BeginDifficultyChange(string previous, int direction)
    {
        if (direction == 0 || previous == Difficulty) return;
        difficultyLabels[previous].gameObject.SetActive(false);
        difficultyTransition = true;
        difficultyDirection = direction > 0 ? 1 : -1;
        difficultyAge = 0;
        DrawDifficulty(0);
    }

    private void SetDifficultyDot(int index, Color value)
    {
        if (dotTarget[index] == value) return;
        dotStart[index] = difficultyDots[index].color;
        dotTarget[index] = value;
        dotAge[index] = 0;
    }

    private void DrawDifficulty(float delta)
    {
        difficultyAge += delta;
        if (difficultyTransition)
        {
            float t = Mathf.Clamp01(difficultyAge / .2f);
            float eased = t < .5f ? (1 - Mathf.Sqrt(1 - 4 * t * t)) / 2
                : (Mathf.Sqrt(1 - Mathf.Pow(-2 * t + 2, 2)) + 1) / 2;
            var current = difficultyLabels[Difficulty];
            current.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(difficultyDirection > 0 ? 500 : -320, 90, eased), -80);
            current.color = new Color(1, 1, 1, difficultyAge < 1f / 24 ? .5f : 1);
            if (t == 1) difficultyTransition = false;
        }
        else if (difficultyLabels.TryGetValue(Difficulty, out Graphic label))
            label.rectTransform.anchoredPosition = new Vector2(90 - 390 * Mathf.Pow(1 - Mathf.Clamp01((age - IntroDuration) / .6f), 4), -80);
        for (int i = 0; i < difficultyDots.Count; i++)
        {
            dotAge[i] += delta;
            Color tint = Color.Lerp(dotStart[i], dotTarget[i], 1 - Mathf.Pow(1 - Mathf.Clamp01(dotAge[i] / .5f), 4));
            tint.a = dotTarget[i].a * (1 - Mathf.Pow(1 - Mathf.Clamp01((age - IntroDuration) / .5f), 4));
            if (confirmAge >= 0) tint.a *= Mathf.Pow(1 - Mathf.Clamp01(confirmAge / .25f), 4);
            difficultyDots[i].color = tint;
        }
        foreach (string name in DifficultyArrowNames)
        {
            var arrow = difficultyRoot.Find(name).GetComponent<VanillaFreeplaySprite>();
            bool pressed = difficultyAge < 2f / 24 && (difficultyDirection > 0 ? name == "Next Difficulty" : name == "Previous Difficulty");
            arrow.drawScale = pressed ? .5f : 1;
            arrow.rectTransform.anchoredPosition = new Vector2(name == "Next Difficulty" ? 325 : 20, pressed ? -75 : -70);
            if (pressed && instrumentalWhite == null) instrumentalWhite = new Material(Resources.Load<Shader>("VanillaResults/PureWhite"));
            arrow.material = pressed ? instrumentalWhite : null;
            arrow.SetVerticesDirty();
        }
    }

    private void DrawFilters(float delta)
    {
        filterMoveAge += delta;
        int frame = Mathf.FloorToInt(filterMoveAge * 24);
        float offset = frame == 0 ? -10 : frame == 1 ? -22 : frame == 2 ? 2 : 0;
        for (int i = 0; i < filterLetters.Count; i++)
        {
            filterLetters[i].rectTransform.anchoredPosition = filterPositions[i] + Vector2.left * (offset * filterMoveDirection);
            filterLetters[i].gameObject.SetActive(i != 0 || frame != 1);
        }
        for (int i = 0; i < filterSeparators.Count; i++)
            filterSeparators[i].rectTransform.anchoredPosition = new Vector2(i * 80 + 60 - offset * filterMoveDirection, -20);
        ((RectTransform)filters.Find("Previous Filter")).anchoredPosition = new Vector2(-20 + (frame < 2 && filterMoveDirection < 0 ? 3 : 0), -15);
        ((RectTransform)filters.Find("Next Filter")).anchoredPosition = new Vector2(380 - (frame < 2 && filterMoveDirection > 0 ? 3 : 0), -15);
    }

    private void PresentCapsule(Capsule capsule, bool selected)
    {
        capsule.title.color = new Color(1, 1, 1, selected ? 1 : .6f);
        ((VanillaFreeplayCapsuleText)capsule.title).Present(Hex(IsPico ? "CC6600" : "00CCFF"), selected, false, false);
        if (capsule.favorite != null)
        {
            capsule.favorite.color = new Color(1, 1, 1, selected ? 1 : .6f);
            capsule.favoriteGlow.color = new Color(1, 1, 1, selected ? 1 : 0);
        }
        if (capsule.rank != null)
            capsule.rank.color = selected ? Color.white : new Color(2f / 3, 2f / 3, 2f / 3, .7f);
    }

    private void DrawCapsulePresentation(Capsule capsule, float delta)
    {
        capsule.titleMask.gameObject.SetActive(true);
        if (capsule.icon != null) capsule.icon.enabled = true;
        float appear = confirmAge >= 0 && capsule.index == SelectedIndex ? confirmAge : capsuleAge;
        capsule.title.rectTransform.localScale = appear >= 0 && appear < 1f / 24 ? new Vector3(1.7f, .2f, 1)
            : appear >= 1f / 24 && appear < 3f / 24 ? new Vector3(.4f, 1.4f, 1) : Vector3.one;
        if (capsule.newMarker != null && capsule.newMarker.gameObject.activeSelf)
            capsule.newMarker.FreezeFrame((Mathf.FloorToInt(age * 24) + 45 - capsule.index * 4 % 45) % capsule.newMarker.FrameCount);
        if (confirmAge >= 0 && capsule.index == SelectedIndex && menu.vanillaMenu.flashingLights)
        {
            int flicker = Mathf.Min(19, Mathf.FloorToInt(confirmAge * 24));
            bool white = flicker > 0 && flicker % 2 == 0;
            capsule.title.color = flicker == 0 || white ? Color.white : new Color32(221, 221, 221, 255);
            ((VanillaFreeplayCapsuleText)capsule.title).Present(Hex(IsPico ? "CC6600" : "00CCFF"), true, flicker >= 2, white, flicker > 0 && !white);
        }
        if (capsule.favoriteAge < 0) return;
        capsule.favoriteAge += delta;
        if (capsule.favoriteAge < .2f) return;
        bool removed = capsule.removingFavorite;
        capsule.root.anchoredPosition = new Vector2(capsule.root.anchoredPosition.x, capsule.favoriteStartY + (removed ? 5 : -5));
        capsule.favoriteAge = -1;
        UpdateCapsule(capsule);
        PresentCapsule(capsule, capsule.index == SelectedIndex);
        if (removed && filterIndex == 1) favoriteRefresh = true;
    }

    private static float FavoriteOffset(Capsule capsule)
    {
        float t = capsule.favoriteAge;
        float amount = t < .1f ? ExpoOut(t / .1f) : 1 - 2 * ExpoIn((t - .1f) / .1f);
        return (capsule.removingFavorite ? -5 : 5) * amount;
    }

    private void DrawCompletion(float delta)
    {
        displayedClear = SmoothValue(displayedClear, intendedClear, delta, .5f, .01f);
        int shown = Mathf.Clamp(Mathf.FloorToInt(displayedClear * 100), 0, 100);
        if (shown == previousClear) return;
        previousClear = shown;
        string digits = shown.ToString();
        float x = digits.Length == 1 ? 24 : digits.Length == 3 ? -10 : 0;
        for (int i = 0; i < 3; i++)
        {
            if (i >= completionDigits.Count) completionDigits.Add(Sprite("Clear " + i, clearDigits, "fonts/freeplay-clear", 0, 0, "00000"));
            var image = completionDigits[i];
            image.gameObject.SetActive(i < digits.Length);
            if (i >= digits.Length) continue;
            image.Load("fonts/freeplay-clear", digits[i] + "0000");
            image.rectTransform.anchoredPosition = new Vector2(x, 0);
            x += image.FrameSize.x;
        }
    }

    private static float SmoothValue(float current, float target, float delta, float duration, float snap)
    {
        float value = Mathf.Lerp(target, current, Mathf.Pow(.01f, delta / duration));
        return Mathf.Abs(value - target) < snap ? target : value;
    }
}
