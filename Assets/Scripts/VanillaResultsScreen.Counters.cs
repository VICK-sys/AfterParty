using System.Collections.Generic;
using UnityEngine;

public sealed partial class VanillaResultsScreen
{
    private sealed class Counter
    {
        public RectTransform root;
        public readonly List<VanillaFreeplaySprite> digits = new List<VanillaFreeplaySprite>();
        public int target;
        public int shown = -1;
        public float delay;
        public Color color;
    }

    private readonly List<Counter> tallies = new List<Counter>();
    private readonly List<VanillaFreeplaySprite> scoreDigits = new List<VanillaFreeplaySprite>();
    private readonly List<VanillaFreeplaySprite> largePercentDigits = new List<VanillaFreeplaySprite>();
    private readonly List<VanillaFreeplaySprite> smallPercentDigits = new List<VanillaFreeplaySprite>();
    private RectTransform largeClear;
    private RectTransform smallClear;
    private string scoreText;
    private static readonly string[] DigitNames = { "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE" };
    private readonly string[] digitPrefixes = new string[10];

    private void BuildClear()
    {
        largeClear = Rect("Clear Percent", viewport, 830, 290, 260, 180);
        Sprite("Clear Label", largeClear, "clearPercent/clearPercentText", 0, 0);
        smallClear = Rect("Small Clear Percent", viewport, 0, 0, 135, 40);
        Sprite("Clear Label", smallClear, "clearPercent/clearPercentTextSmall", 40, 0);
        for (int i = 0; i < 3; i++)
        {
            largePercentDigits.Add(Sprite("Digit " + i, largeClear, "clearPercent/clearPercentNumberRight", 0, 72, "number 0 0"));
            smallPercentDigits.Add(Sprite("Digit " + i, smallClear, "clearPercent/clearPercentNumberSmall", 0, 0, "number 0 0"));
        }
    }

    private void BuildCounters()
    {
        int[] values = { Data.totalNotesHit, Data.maxCombo, Data.sick, Data.good, Data.bad, Data.shit, Data.missed };
        string[] names = { "Total Hit", "Max Combo", "Sick", "Good", "Bad", "Shit", "Missed" };
        float shift = Data.totalNotesHit >= 1000 ? -30 : 0;
        float[] x = { 375 + shift, 375 + shift, 230, 210, 190, 220, 260 };
        float[] y = { 150, 200, 277, 331, 385, 439, 493 };
        uint[] colors = { 0xFFFFFF, 0xFFFFFF, 0x89E59E, 0x89C9E5, 0xE6CF8A, 0xE68C8A, 0xC68AE6 };
        for (int i = 0; i < values.Length; i++)
            tallies.Add(new Counter { root = Rect(names[i], viewport, x[i], y[i]), target = values[i], delay = 1.2f + i * 0.3f, color = Hex(colors[i]) });
        scoreText = Mathf.Max(0, Data.score).ToString();
        if (Data.score <= 0) scoreText = "";
        var score = Rect("Score Digits", viewport, 35, 305);
        for (int i = 0; i < 10; i++)
            scoreDigits.Add(Sprite("Score " + i, score, "score-digital-numbers", 35 + 65 * i, 305, "DISABLED"));
    }

    private void DrawCounters(float time)
    {
        foreach (var counter in tallies)
        {
            counter.root.gameObject.SetActive(time >= counter.delay);
            int value = Mathf.RoundToInt(counter.target * QuartOut((time - counter.delay) / 0.5f));
            if (value == counter.shown) continue;
            counter.shown = value;
            string text = value.ToString();
            while (counter.digits.Count < text.Length)
            {
                var digit = Sprite("Digit", counter.root, "tallieNumber", counter.digits.Count * 43, 0, "0 small");
                digit.color = counter.color;
                counter.digits.Add(digit);
            }
            for (int i = 0; i < counter.digits.Count; i++)
            {
                counter.digits[i].gameObject.SetActive(i < text.Length);
                if (i < text.Length)
                {
                    counter.digits[i].TryPlay(text[i] + " small", false);
                    counter.digits[i].FreezeFrame(counter.digits[i].FrameCount - 1);
                }
            }
        }
        scoreDigits[0].transform.parent.gameObject.SetActive(time >= 37 / 24f);
        int start = 10 - scoreText.Length;
        for (int i = 0; i < 10; i++)
        {
            string prefix = "DISABLED";
            int frame = 0;
            if (i >= start)
            {
                int final = scoreText[i - start] - '0';
                float age = time - 37 / 24f - (i - 1) / 24f;
                if (age < 0) prefix = "GONE";
                else
                {
                    int digit = age < 41 / 24f ? (Mathf.FloorToInt(age * 24) + 1) % 10
                        : Mathf.FloorToInt(final * (1 - Mathf.Pow(1 - Mathf.Clamp01((age - 41 / 24f) / (23 / 24f)), 2)));
                    prefix = DigitNames[digit] + " DIGITAL";
                    float glow = 64 / 24f + (scoreText.Length - (i - 1)) / 24f;
                    frame = age >= glow ? Mathf.FloorToInt((age - glow) * 24) : age < 1 / 24f ? 0 : 4;
                }
            }
            if (digitPrefixes[i] != prefix)
            {
                digitPrefixes[i] = prefix;
                scoreDigits[i].TryPlay(prefix, false);
            }
            scoreDigits[i].FreezeFrame(frame);
        }
        float clearStart = 37 / 24f;
        float clearEnd = clearStart + 58 / 24f;
        int from = Mathf.Max(0, Data.ClearPercent - 36);
        DisplayedClear = Mathf.RoundToInt(Mathf.Lerp(from, Data.ClearPercent, QuartOut((time - clearStart) / (58 / 24f))));
        float alpha = 1 - QuartOut((time - clearEnd - 0.75f) / 0.5f);
        largeClear.gameObject.SetActive(time >= clearStart && alpha > 0);
        DrawPercent(largePercentDigits, DisplayedClear, false, alpha, time >= clearEnd && time < clearEnd + 0.4f);
        smallClear.gameObject.SetActive(time >= Data.CharacterDelay);
        DrawPercent(smallPercentDigits, Data.ClearPercent, true, 1, time < Data.CharacterDelay + 0.4f);
        foreach (var graphic in largeClear.GetComponentsInChildren<UnityEngine.UI.Graphic>()) graphic.color = new Color(1, 1, 1, alpha);
        if (!ManualClock && effects != null && time >= clearStart && time <= clearEnd && previousClear != DisplayedClear)
        {
            if (previousClear >= 0) Sound("scrollMenu");
            previousClear = DisplayedClear;
        }
        if (!ManualClock && effects != null && time >= clearEnd && !confirmedClear)
        {
            confirmedClear = true;
            Sound("confirmMenu");
        }
    }

    private void DrawPercent(List<VanillaFreeplaySprite> digits, int value, bool small, float alpha, bool flashing)
    {
        string text = value.ToString();
        int offset = text.Length == 1 ? 1 : text.Length == 3 ? -1 : 0;
        for (int i = 0; i < digits.Count; i++)
        {
            var digit = digits[i];
            digit.gameObject.SetActive(i < text.Length);
            if (i >= text.Length) continue;
            string prefix = "number " + text[i] + " 0";
            if (digit.CurrentFrameName == null || !digit.CurrentFrameName.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                digit.TryPlay(prefix, false);
                digit.FreezeFrame(0);
            }
            digit.rectTransform.anchoredPosition = new Vector2((i + offset) * (small ? 32 : 72) - (small ? 24 : 0), small ? (i + offset) * 4 : -72);
            digit.material = flashing ? white : null;
            digit.color = new Color(1, 1, 1, alpha);
        }
    }
}
