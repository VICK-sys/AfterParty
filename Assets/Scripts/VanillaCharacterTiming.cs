using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class VanillaCharacterTiming
{
    public static void Advance(Song song, ref int previousStep, Action<int> onStep)
    {
        if (!song.songStarted && !song.IsCountingDown) return;
        int step = Mathf.FloorToInt(song.vanillaPlayback.BeatAt(song.SongPosition) * 4);
        if (previousStep == int.MinValue || step < previousStep) previousStep = step - 1;
        while (previousStep < step) onStep(++previousStep);
    }

    public static bool IsDanceStep(JObject data, int step)
    {
        float every = (float?)data["danceEvery"] ?? 1;
        return every > 0 && Mathf.Abs(step % (every * 4)) < .001f;
    }

    public static string DanceAnimation(VanillaWeek2Graphic graphic, float singing, bool locked, ref bool alternate)
    {
        if (singing > 0 || locked && !graphic.Finished) return null;
        string current = graphic.Animation ?? "";
        if (!graphic.Finished && !current.StartsWith("idle") && !current.StartsWith("dance") && !current.StartsWith("sing")) return null;
        string name = graphic.Has("danceLeft") ? alternate ? "danceRight" : "danceLeft" : "idle";
        if (name == current && !graphic.Finished) return null;
        alternate = !alternate;
        return name;
    }
}
