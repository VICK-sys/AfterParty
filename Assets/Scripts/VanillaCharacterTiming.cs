using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class VanillaCharacterTiming
{
    public static bool IsSinging(string animation) => animation != null && animation.StartsWith("sing") && !animation.EndsWith("-end");

    public static bool IsManualSide(int side)
    {
        if (Player.instance == null || side < 0 || side >= Player.instance.Strumlines.Length) return false;
        var line = Player.instance.Strumlines[side];
        return line.Controlled && !line.BotPlay;
    }

    public static bool IsHoldingInput(int side)
    {
        if (!IsManualSide(side)) return false;
        var line = Player.instance.Strumlines[side];
        for (int direction = 0; direction < 4; direction++)
            if (line.IsHeld(direction)) return true;
        return false;
    }

    public static bool AdvanceSinging(JObject data, string animation, ref float holdTimer, float delta, float stepMilliseconds, bool held)
    {
        if (!IsSinging(animation))
        {
            holdTimer = 0;
            return false;
        }
        holdTimer += delta;
        float duration = ((float?)data["singTime"] ?? 8) * stepMilliseconds / 1000;
        if (animation.EndsWith("miss")) duration *= 2;
        if (holdTimer <= duration || held) return false;
        holdTimer = 0;
        return true;
    }

    public static string SingEndAnimation(VanillaWeek2Graphic graphic)
    {
        string name = graphic.Animation;
        if (name.EndsWith("-hold")) name = name.Substring(0, name.Length - 5);
        name += "-end";
        return graphic.Has(name) ? name : null;
    }

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

    public static string DanceAnimation(VanillaWeek2Graphic graphic, bool force, bool locked, ref bool alternate)
    {
        string current = graphic.Animation ?? "";
        if (!force && (IsSinging(current) || locked && !graphic.Finished)) return null;
        if (!force && !graphic.Finished && !current.StartsWith("idle") && !current.StartsWith("dance")) return null;
        string name = graphic.Has("danceLeft") ? alternate ? "danceRight" : "danceLeft" : "idle";
        if (name == current && !graphic.Finished) return null;
        alternate = !alternate;
        return name;
    }
}
