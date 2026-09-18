using System;
using System.Linq;
using System.IO;
using System.Text.Json;

public static class FunkinRuleTests
{
    private static int assertions;

    public static void Main(string[] args)
    {
        Run();
        Charts(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
    }

    public static void Run()
    {
        assertions = 0;
        PlayModeSelection();
        Ratings();
        Windows();
        Inputs();
        Holds();
        Bots();
        Hud();
        Console.WriteLine("FUNKIN RULE TESTS PASSED: " + assertions + " assertions.");
    }

    private static void Require(bool value, string message)
    {
        assertions++;
        if (!value) throw new InvalidOperationException(message);
    }

    private static void PlayModeSelection()
    {
        int[] modes = Enumerable.Range(0, PlayModes.Count).Select(PlayModes.FromIndex).ToArray();
        Require(modes.SequenceEqual(new[] { 1, 2, 4 }), "Mode selection must preserve existing single-player score IDs.");
        Require(!modes.Contains(3), "Retired mode cannot be selected.");
        Require(modes.Select(PlayModes.Label).SequenceEqual(new[] { "BOYFRIEND", "OPPONENT", "AUTOPLAY" }), "Mode labels match their score IDs.");
        foreach (int mode in modes)
            Require(PlayModes.Normalize(mode) == mode, "Valid mode survives normalization.");
        foreach (int mode in new[] { -1, 0, 3, 5, int.MaxValue })
            Require(PlayModes.Normalize(mode) == PlayModes.Boyfriend, "Invalid or retired mode falls back to Boyfriend.");
        Require(PlayModes.FromIndex(PlayModes.Count) == PlayModes.Boyfriend, "Out-of-range menu selection falls back safely.");
    }

    private static void Hud()
    {
        Near(FunkinHudRules.SmoothHealth(100, 180, false), 112, "Health smoothing");
        Near(FunkinHudRules.SmoothHealth(100, 1, true), 200, "Bot display override");
        Require(FunkinHudRules.FillPixels(100) == 297, "Half health pixel rounding");
        Require(FunkinHudRules.FillPixels(101.9) == 297, "Fill quantizes to 100 divisions");
        Require(FunkinHudRules.FillPixels(102) == 302, "Next fill division");
        Require(FunkinHudRules.FillPixels(0) == 0 && FunkinHudRules.FillPixels(200) == 593, "Fill endpoints");
        Near(FunkinHudRules.Boundary(100), 640, "Icon boundary at half health");
        Near(FunkinHudRules.BarY(false), 648, "Upscroll health position");
        Near(FunkinHudRules.BarY(true), 72, "Downscroll health position");
        var icon = new FunkinHealthIconState();
        icon.UpdateFace(40, false);
        Require(icon.Animation == FunkinHealthIconState.Face.Idle, "Idle stays idle at exactly 20 percent");
        icon.UpdateFace(39.9, false);
        Require(icon.Animation == FunkinHealthIconState.Face.Losing, "Below 20 percent loses");
        icon.UpdateFace(40, false);
        Require(icon.Animation == FunkinHealthIconState.Face.Losing, "Losing stays losing at exactly 20 percent");
        icon.UpdateFace(40.1, false);
        Require(icon.Animation == FunkinHealthIconState.Face.Idle, "Above 20 percent exits losing");
        icon.UpdateFace(180, false);
        Require(icon.Animation == FunkinHealthIconState.Face.Idle, "Two-frame icon has no winning face");
        icon.UpdateFace(160, true);
        Require(icon.Animation == FunkinHealthIconState.Face.Idle, "Idle stays idle at exactly 80 percent");
        icon.UpdateFace(160.1, true);
        Require(icon.Animation == FunkinHealthIconState.Face.Winning, "Third frame wins above 80 percent");
        icon.UpdateFace(160, true);
        Require(icon.Animation == FunkinHealthIconState.Face.Winning, "Winning threshold hysteresis");
        icon.UpdateFace(159.9, true);
        Require(icon.Animation == FunkinHealthIconState.Face.Idle, "Winning exits below threshold");
        icon.Bop(150);
        Near(icon.Width, 180, "Icon bop starts at 180 pixels");
        icon.Advance(0.0875);
        Near(icon.Width, 165, "Icon bop linear midpoint");
        icon.Advance(0.0875);
        Near(icon.Width, 150, "Icon bop ends after 175 ms");
        icon.Bop(50);
        icon.Advance(0.05);
        Near(icon.Width, 165, "Fast tempo shortens icon tween");
        foreach (int rate in new[] { 30, 60, 144 })
        {
            var popup = new FunkinPopupState { X = 100, Y = 200, VelocityX = -5, VelocityY = -150, Gravity = 550, FadeDelay = 0.5 };
            for (int frame = 0; frame < rate / 2; frame++) popup.Advance(1.0 / rate);
            Near(popup.X, 97.5, "Popup horizontal motion at " + rate);
            Near(popup.Y, 193.75, "Popup gravity at " + rate);
            Near(popup.Alpha, 1, "Rating stays opaque for one beat");
            popup.Advance(0.1);
            Near(popup.Alpha, 0.5, "Rating fade midpoint");
            popup.Advance(0.101);
            Require(popup.Finished, "Rating expires after fade");
        }
    }

    private static void Near(double actual, double expected, string message)
    {
        Require(Math.Abs(actual - expected) < 0.00001, message + ": " + actual + " != " + expected);
    }

    private static FunkinStrumline Line(params FunkinNoteState[] notes)
    {
        var line = new FunkinStrumline();
        foreach (var note in notes) line.Add(note);
        line.Advance(1000, 0);
        return line;
    }

    private static void Press(FunkinStrumline line, int lane, int key, double timestamp, double frame)
    {
        line.Presses.Add(new FunkinInputEvent(lane, key, timestamp));
        line.Advance(frame, 0);
    }

    private static void Ratings()
    {
        var timings = new[] { 0d, 4.999, 5, 45, 45.999, 46, 90, 91, 135, 136, 160, 160.99, 161 };
        var expected = new[] { 0, 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 3, 4 };
        for (int i = 0; i < timings.Length; i++)
        {
            Require((int)FunkinRules.Judge(timings[i]) == expected[i], "Positive rating boundary " + timings[i]);
            Require((int)FunkinRules.Judge(-timings[i]) == expected[i], "Negative rating boundary " + timings[i]);
        }
        Require(FunkinRules.Score(0) == 500 && FunkinRules.Score(4.99) == 500, "Perfect score");
        Require(FunkinRules.Score(5) == 499, "Sigmoid begins at 5 ms");
        Require(FunkinRules.Score(45) == 353, "45 ms score");
        Require(FunkinRules.Score(90) == 37, "90 ms score");
        Require(FunkinRules.Score(135) == 9 && FunkinRules.Score(160) == 9, "Late hit minimum");
        Require(FunkinRules.Score(161) == -100, "Miss score");
        for (int time = -160; time <= 160; time++)
            Require(FunkinRules.Score(time) == FunkinRules.Score(-time), "Score symmetry");
        Require(!FunkinRules.BreaksCombo(FunkinRules.Judgement.Good), "Good keeps combo");
        Require(FunkinRules.BreaksCombo(FunkinRules.Judgement.Bad), "Bad breaks combo");
        Require(FunkinRules.BreaksCombo(FunkinRules.Judgement.Shit), "Shit breaks combo");
        Near(FunkinRules.NoteDistance(1000, 900, 2, false), -90, "Upscroll distance");
        Near(FunkinRules.NoteDistance(1000, 900, 2, true), 90, "Downscroll distance");
    }

    private static void Windows()
    {
        var note = new FunkinNoteState(1000, 0);
        var line = new FunkinStrumline();
        line.Add(note);
        line.Advance(839.999, 0);
        Require(!note.MayHit, "Outside early boundary control");
        line.Advance(840, 0);
        Require(note.MayHit, "Inclusive early boundary");
        line.Advance(1160, 0);
        Require(note.MayHit && !note.Missed, "Inclusive late boundary");
        int misses = 0;
        line.NoteMissed += _ => misses++;
        line.Advance(1160.001, 0);
        line.Advance(1200, 0);
        Require(misses == 1 && !note.MayHit, "Miss handled once after 160 ms");
    }

    private static void Inputs()
    {
        var first = new FunkinNoteState(1000, 0);
        var second = new FunkinNoteState(1010, 0);
        var chord = new FunkinNoteState(1000, 1);
        var line = Line(second, first, chord);
        double timing = 999;
        line.NoteHit += (_, difference, automatic) => timing = difference;
        Press(line, 0, 1, 1004.9, 1050);
        Require(first.Hit && !second.Hit && !chord.Hit, "One press hits earliest note only");
        Near(timing, 4.9, "Use event time instead of frame time");
        Require(FunkinRules.Judge(1050 - first.Time) != FunkinRules.Judge(timing), "Frame timestamp negative control");
        line.Presses.Add(new FunkinInputEvent(0, 2, 1010));
        line.Presses.Add(new FunkinInputEvent(1, 3, 1000));
        line.Advance(1050, 0);
        Require(second.Hit && chord.Hit, "Alternate bind and chord");
        line.Releases.Add(new FunkinInputEvent(0, 1, 1050));
        line.Advance(1050, 0);
        Require(line.IsHeld(0), "Releasing one bind preserves another");
        line.Releases.Add(new FunkinInputEvent(0, 2, 1050));
        line.Advance(1050, 0);
        Require(!line.IsHeld(0), "Last release clears direction");
        var low = new FunkinNoteState(1000, 0) { LowPriority = true };
        var normal = new FunkinNoteState(1001, 0);
        line = Line(low, normal);
        Press(line, 0, 1, 1000, 1000);
        Require(normal.Hit && !low.Hit, "Deprioritize low priority note");
        line = Line();
        int ghosts = 0;
        line.GhostMissed += _ => ghosts++;
        Press(line, 0, 1, 1000, 1000);
        Require(ghosts == 1, "Desktop ghost miss");
        line.GhostTapping = true;
        Press(line, 0, 2, 1000, 1000);
        Require(ghosts == 1, "Optional idle ghost tap");
        line.Add(new FunkinNoteState(1000, 1));
        line.Advance(1000, 0);
        Press(line, 0, 3, 1000, 1000);
        Require(ghosts == 2, "Optional ghost tapping still penalizes wrong lane near notes");
        line.ClearInput();
        Require(!line.IsHeld(0) && line.Presses.Count == 0, "Pause clears held and pending inputs");
        line = Line(new FunkinNoteState(1000, 0));
        Press(line, 0, 1, 1100, 1100);
        Require(line.Notes[0].HeadVisible, "Bad hit leaves desaturated head visible");
        line.Advance(1100, 0.3);
        Require(line.Animations[0] == FunkinStrumline.Animation.Press, "Confirm returns to press when held");
    }

    private static void Holds()
    {
        var hold = new FunkinNoteState(1000, 0, 1000);
        var line = Line(hold);
        double score = 0;
        int drops = 0;
        line.HoldScored += (_, elapsed) => score += 250 * elapsed;
        line.HoldMissed += (_, penalty) => { score += penalty; drops++; };
        Press(line, 0, 1, 1000, 1000);
        line.Advance(1250, 0.25);
        Near(hold.Remaining, 750, "Continuous tail clips to song time");
        Near(score, 62.5, "Hold bonus preserves fractions");
        line.Releases.Add(new FunkinInputEvent(0, 1, 1250));
        line.Advance(1250, 0);
        line.Advance(1260, 0);
        Require(hold.HoldDropped && drops == 1, "Release drops hold once");
        Near(score, 62.5 - 93.75, "Drop penalizes remaining duration");
        Press(line, 0, 2, 1300, 1300);
        line.Advance(1500, 0.25);
        Require(hold.HoldDropped && drops == 1, "Dropped tail cannot be recovered");
        Near(score, 62.5 - 93.75, "Dropped hold grants no more score");
        hold = new FunkinNoteState(1000, 0, 160);
        line = Line(hold);
        drops = 0;
        line.HoldMissed += (_, __) => drops++;
        line.Advance(1161, 0);
        Require(hold.Missed && drops == 0, "160 ms tail has no drop penalty");
        hold = new FunkinNoteState(1000, 0, 160.1);
        line = Line(hold);
        line.HoldMissed += (_, __) => drops++;
        line.Advance(1161, 0);
        Require(drops == 1, "Long missed head also penalizes tail");
        hold = new FunkinNoteState(1000, 0, 500);
        line = Line(hold);
        Press(line, 0, 1, 1000, 1000);
        Press(line, 0, 2, 1000, 1000);
        line.Releases.Add(new FunkinInputEvent(0, 1, 1200));
        line.Advance(1200, 0.2);
        line.Advance(1300, 0.1);
        Require(!hold.HoldDropped, "Alternate key keeps sustain held");
        line.Advance(1500, 0.2);
        line.Advance(1501, 0.001);
        Require(hold.HoldFinished && line.Animations[0] == FunkinStrumline.Animation.Press, "Completed sustain returns to press");
    }

    private static void Bots()
    {
        foreach (bool player in new[] { false, true })
        {
            var note = new FunkinNoteState(1000, 0, 100);
            var line = new FunkinStrumline { Controlled = player, BotPlay = player };
            line.Add(note);
            bool automatic = false;
            int bonuses = 0;
            line.NoteHit += (_, __, auto) => automatic = auto;
            line.HoldScored += (_, __) => bonuses++;
            line.Advance(1000, 0.016);
            Require(note.Hit && automatic && line.HeadsHit == 1, "Bot hits center without player judgement");
            Require(bonuses == 0, "Bot grants no hold score");
            line.Advance(1050, 0.05);
            Require(!note.HoldDropped, "Bot sustains without physical keys");
        }
    }

    private static void Charts(string root)
    {
        int charts = 0;
        int heads = 0;
        foreach (string path in Directory.GetFiles(Path.Combine(root, "Assets/StreamingAssets/Bundles"), "chart*.json", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == "Source"))
        {
            using JsonDocument data = JsonDocument.Parse(File.ReadAllText(path));
            foreach (string difficulty in data.RootElement.GetProperty("notes").EnumerateObject().Select(entry => entry.Name))
            {
                var entries = data.RootElement.GetProperty("notes").GetProperty(difficulty).EnumerateArray().ToArray();
                foreach (int fps in new[] { 30, 60, 144 })
                {
                    var lines = new[] { new FunkinStrumline { Controlled = false }, new FunkinStrumline { Controlled = false } };
                    double end = 0;
                    int[] expected = new int[2];
                    foreach (JsonElement entry in entries)
                    {
                        int direction = entry.GetProperty("d").GetInt32();
                        double time = entry.GetProperty("t").GetDouble();
                        double length = entry.TryGetProperty("l", out var sustain) ? sustain.GetDouble() : 0;
                        lines[direction / 4].Add(new FunkinNoteState(time, direction % 4, length));
                        expected[direction / 4]++;
                        end = Math.Max(end, time + length);
                    }
                    for (double position = -200; position < end + 500; position += 1000.0 / fps)
                        foreach (FunkinStrumline line in lines) line.Advance(position, 1.0 / fps);
                    for (int side = 0; side < 2; side++)
                    {
                        Require(lines[side].HeadsHit == expected[side], "Chart head consumption: " + path + " " + difficulty + " at " + fps);
                        Require(lines[side].Notes.All(note => !note.Missed && !note.HoldDropped && (note.Length == 0 || note.HoldFinished)),
                            "Chart sustains: " + path + " " + difficulty + " at " + fps);
                    }
                }
                charts++;
                heads += entries.Length;
            }
        }
        Require(charts == 87 && heads == 41590, "Expected all 87 bundled charts and 41,590 note heads.");
        Console.WriteLine("FUNKIN CHART TESTS PASSED: " + charts + " charts, " + heads + " heads, 30/60/144 FPS.");
    }
}
