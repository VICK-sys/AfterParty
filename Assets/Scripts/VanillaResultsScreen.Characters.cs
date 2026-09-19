using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class VanillaResultsScreen
{
    private sealed class CharacterAnimation
    {
        public VanillaFreeplayAnimate atlas;
        public VanillaFreeplaySprite sparrow;
        public RectTransform rect;
        public float delay;
        public int start;
        public int length;
        public int loopStart = -1;
        public int loopLength;
        public string sound;
        public bool sounded;
    }

    private readonly List<CharacterAnimation> characters = new List<CharacterAnimation>();

    private void BuildCharacters()
    {
        JObject player = JObject.Parse(Resources.Load<TextAsset>("VanillaResults/players/" + Data.Character).text);
        string key = new[] { "loss", "good", "great", "excellent", "perfect", "perfectGold" }[Data.Rank];
        var ordered = new List<JToken>(player["results"][key]);
        ordered.Sort((a, b) => ((int?)a["zIndex"] ?? 500).CompareTo((int?)b["zIndex"] ?? 500));
        foreach (var data in ordered)
        {
            string filter = (string)data["filter"] ?? "both";
            if (filter != "both" && filter != (Data.naughty ? "naughty" : "safe")) continue;
            string script = (string)data["scriptClass"];
            string path = (string)data["assetPath"];
            switch (script)
            {
                case "BFBedPerfectResults": path = "resultScreen/results-bf/resultsPERFECT/bed"; break;
                case "BFShitResults": path = "resultScreen/results-bf/resultsSHIT"; break;
                case "PicoPerfectResults": path = "resultScreen/results-pico/resultsPERFECT"; break;
                case "PicoGreatResults": path = "resultScreen/results-pico/resultsGREAT"; break;
                case "PicoGoodResults": path = "resultScreen/results-pico/resultsGOOD"; break;
            }
            path = path.Replace("shared:", "").Replace("resultScreen/", "");
            float x = (float?)data["offsets"]?[0] ?? 0;
            float y = (float?)data["offsets"]?[1] ?? 0;
            var entry = new CharacterAnimation { delay = Data.CharacterDelay + ((float?)data["delay"] ?? 0), sound = ((string)data["sound"])?.Replace("shared:", "") };
            if ((string)data["renderType"] == "sparrow")
            {
                entry.sparrow = Sprite("Character " + path, viewport, path, x, y);
                entry.rect = entry.sparrow.rectTransform;
                entry.rect.SetSiblingIndex(flash.transform.GetSiblingIndex());
                entry.length = entry.sparrow.FrameCount;
            }
            else
            {
                entry.rect = Rect("Character " + path, viewport, x, y);
                entry.atlas = entry.rect.gameObject.AddComponent<VanillaFreeplayAnimate>();
                entry.atlas.Initialize("images/" + path, (bool?)data["applyStageMatrix"] ?? false, "VanillaResults");
                if ((bool?)data["applyStageMatrix"] != true) entry.atlas.UseTimelineBounds();
                if (script == "PicoGreatResults" || script == "PicoGoodResults")
                {
                    entry.atlas.ScaleSymbolElement(script == "PicoGoodResults" ? "white small" : "white", 10, 500);
                    entry.atlas.ScaleSymbolElement(script == "PicoGoodResults" ? "blacksmall" : "black", 15, 500);
                }
                string start = (string)data["startFrameLabel"];
                if (script == "PicoGoodResults")
                {
                    if (PicoVariant == null) PicoVariant = Random.value < 0.05f ? "intro fat gf" : Random.value < 0.2f ? "intro cass" : "intro";
                    start = PicoVariant;
                }
                entry.start = string.IsNullOrEmpty(start) ? 0 : entry.atlas.LabelStart(start);
                entry.length = string.IsNullOrEmpty(start) ? entry.atlas.TotalFrames : Mathf.RoundToInt(entry.atlas.GetLabelDuration(start) * entry.atlas.FrameRate);
                string loopLabel = script == "PicoGoodResults" ? "loop" : (string)data["loopFrameLabel"];
                if (loopLabel != null)
                {
                    entry.loopStart = entry.atlas.LabelStart(loopLabel);
                    entry.loopLength = Mathf.RoundToInt(entry.atlas.GetLabelDuration(loopLabel) * entry.atlas.FrameRate);
                }
                float scale = (float?)data["scale"] ?? 1;
                entry.rect.localScale = Vector3.one * scale;
                Vector2 centerOffset = entry.atlas.BoundsSize * ((1 - scale) * 0.5f);
                entry.rect.anchoredPosition += new Vector2(centerOffset.x, -centerOffset.y);
            }
            if (entry.loopStart < 0 && data["loopFrame"] != null)
            {
                entry.loopStart = (int)data["loopFrame"];
                entry.loopLength = entry.length - entry.loopStart;
            }
            if ((bool?)data["looped"] == false) entry.loopStart = -1;
            characters.Add(entry);
        }
    }

    private void DrawCharacters(float time)
    {
        foreach (var entry in characters)
        {
            bool visible = time >= entry.delay;
            entry.rect.gameObject.SetActive(visible);
            if (!visible) continue;
            int age = Mathf.FloorToInt((time - entry.delay) * (entry.atlas == null ? 24 : entry.atlas.FrameRate));
            int frame = entry.start + Mathf.Min(age, entry.length - 1);
            if (age >= entry.length && entry.loopStart >= 0)
                frame = entry.loopStart + (age - entry.length) % Mathf.Max(1, entry.loopLength);
            if (entry.atlas != null) entry.atlas.SetFrame(frame);
            else entry.sparrow.FreezeFrame(frame);
            if (!entry.sounded && !ManualClock && entry.sound != null && effects != null)
            {
                entry.sounded = true;
                Sound(entry.sound, true);
            }
        }
    }
}
