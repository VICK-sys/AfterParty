using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using FridayNightFunkin;

public static class NoteScheduleTests
{
    private static int assertions;
    private static int chartHeads;

    public static void Main(string[] args)
    {
        Boundaries();
        ResetAndControls();
        int charts = 0;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(args[0], "Assets/StreamingAssets/Bundles"), "Chart-*.json", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(Path.GetDirectoryName(path)) == "Source") continue;
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var chart = json.RootElement.GetProperty("song");
            var notes = new List<NoteBehaviour>();
            foreach (var sectionData in chart.GetProperty("notes").EnumerateArray())
            {
                var section = new FNFSong.FNFSection { MustHitSection = sectionData.GetProperty("mustHitSection").GetBoolean() };
                foreach (var data in sectionData.GetProperty("sectionNotes").EnumerateArray())
                    notes.Add(new NoteBehaviour(section, new FNFSong.FNFNote(data.EnumerateArray().Take(3).Select(v => v.GetDecimal()).ToList())));
            }
            double speed = chart.GetProperty("speed").GetDouble();
            CompareChart(notes, speed, 60, path);
            if (path.Contains("01-Darnell") && path.EndsWith("Chart-erect.json"))
                foreach (int fps in new[] { 30, 144, 240 }) CompareChart(notes, speed, fps, path);
            chartHeads += notes.Count;
            charts++;
        }
        Require(charts >= 166, "Bundled chart coverage shrank.");
        TailCost();
        Console.WriteLine($"NOTE SCHEDULE PASSED: {assertions} assertions, {charts} charts, {chartHeads} heads.");
    }

    private static NoteBehaviour Note(decimal time, decimal direction = 0, decimal length = 0)
    {
        return new NoteBehaviour(new FNFSong.FNFSection(), new FNFSong.FNFNote(new List<decimal> { time, direction, length }));
    }

    private static Song NewSong()
    {
        Player.visualOffset = 0;
        return Song.instance = new Song();
    }

    private static void Boundaries()
    {
        Song song = NewSong();
        var early = Note(1000);
        var same = Note(1000, 1, 600);
        var late = Note(9000);
        var notes = new List<NoteBehaviour> { late, early, same };
        var schedule = new NoteSchedule();
        schedule.Reset(notes);
        Require(notes.SequenceEqual(new[] { early, same, late }), "Stable time order failed.");
        song.Position = -601;
        schedule.Advance(song);
        Require(song.Generated.Count == 0, "A note spawned before the window.");
        song.Position = -600;
        schedule.Advance(song);
        Require(song.Generated.Select(n => n.data).SequenceEqual(new[] { early.noteData.ConvertToNote(), same.noteData.ConvertToNote() }), "Window boundary or chord order failed.");
        Require(ReferenceEquals(song.Generated[1].section, same.section) && song.Generated[1].data[2] == 600, "Section or sustain changed.");
        schedule.Advance(song);
        Require(song.Generated.Count == 2, "A repeated frame duplicated a note.");
        song.Position = 9161;
        schedule.Advance(song);
        Require(late.count == 1 && song.Generated.Count == 2, "Late frame did not consume its skipped note.");
        int reads = song.PositionReads;
        schedule.Advance(song);
        Require(song.PositionReads == reads, "An exhausted schedule read the clock.");
        var boundary = Note(1000);
        schedule.Reset(new List<NoteBehaviour> { boundary });
        song.Position = 1160;
        schedule.Advance(song);
        Require(song.Generated.Count == 3 && boundary.count == 1, "Inclusive late boundary changed.");
        var slow = Note(10000);
        schedule.Reset(new List<NoteBehaviour> { slow });
        song.Position = 8000;
        song.Distance = 1600;
        schedule.Advance(song);
        Require(slow.count == 0, "Fast scroll spawned too early.");
        song.Distance = 3200;
        schedule.Advance(song);
        Require(slow.count == 1, "A widened scroll window failed to spawn.");
        var narrowed = Note(12000);
        schedule.Reset(new List<NoteBehaviour> { narrowed });
        song.Distance = 800;
        song.Position = 11199;
        schedule.Advance(song);
        Require(narrowed.count == 0, "A narrowed window spawned too early.");
        song.Position = 11200;
        schedule.Advance(song);
        Require(narrowed.count == 1, "A narrowed window boundary failed.");
        var offset = Note(1000);
        schedule.Reset(new List<NoteBehaviour> { offset });
        song.Distance = 1600;
        song.Position = -600;
        Player.visualOffset = 1;
        schedule.Advance(song);
        Require(offset.count == 0, "Visual offset was ignored.");
        Player.visualOffset = -1;
        schedule.Advance(song);
        Require(offset.count == 1, "Negative visual offset was ignored.");
        Player.visualOffset = 2000;
        int generated = song.Generated.Count;
        schedule.Advance(song);
        Require(song.Generated.Count == generated, "Moving the visual clock backward duplicated a note.");
    }

    private static void ResetAndControls()
    {
        Song song = NewSong();
        var notes = new List<NoteBehaviour> { Note(0), Note(900000) };
        var schedule = new NoteSchedule();
        schedule.Reset(notes);
        schedule.Advance(song);
        Require(notes.Count == 2 && song.Generated.Count == 1, "Chart totals changed after consumption.");
        notes.Clear();
        schedule.Advance(song);
        Require(song.Generated.Count == 1, "Cleared gameplay fixture retained scheduled notes.");
        notes.Add(Note(0));
        schedule.Reset(notes);
        schedule.Advance(song);
        Require(song.Generated.Count == 2, "Retry failed to rewind the schedule.");
        notes.Clear();
        notes.Add(Note(0));
        notes.Add(Note(1));
        notes[0].count = 1;
        schedule.Reset(notes);
        schedule.Advance(song);
        Require(song.Generated.Count == 3 && ReferenceEquals(song.Generated[2].data, notes[1].noteData.ConvertToNote()), "Preconsumed-note control failed.");
        schedule.Reset(new List<NoteBehaviour>());
        schedule.Advance(song);
        Require(song.Generated.Count == 3, "Empty schedule produced a note.");
    }

    private static void CompareChart(List<NoteBehaviour> notes, double speed, int fps, string path)
    {
        Song song = NewSong();
        song.Distance = 720 / FunkinRules.PixelsPerMillisecond / Math.Min(Math.Max(speed, .01), 1);
        var input = notes.ToArray();
        foreach (var note in input) note.count = 0;
        var consumed = new bool[input.Length];
        var expected = new List<NoteBehaviour>();
        var schedule = new NoteSchedule();
        schedule.Reset(notes);
        double end = input.Length == 0 ? 0 : input.Max(n => n.StrumTime) + 200;
        int heads = 0;
        for (int frame = 0; ; frame++)
        {
            double position = -5000 + frame * (1000.0 / fps);
            song.Position = position;
            expected.Clear();
            song.Generated.Clear();
            for (int i = 0; i < input.Length; i++)
            {
                if (consumed[i] || position < input[i].StrumTime - song.Distance) continue;
                consumed[i] = true;
                if (position <= input[i].StrumTime + FunkinRules.HitWindow) expected.Add(input[i]);
            }
            schedule.Advance(song);
            Require(song.Generated.Select(n => n.data).SequenceEqual(expected.OrderBy(n => n.StrumTime).Select(n => n.noteData.ConvertToNote())), "Spawn identity or frame mismatch: " + path + " at " + position);
            heads += song.Generated.Count;
            if (position > end) break;
        }
        Require(heads == input.Length && notes.Count == input.Length && notes.All(n => n.count == 1), "Lost or duplicate chart heads: " + path);
    }

    private static void TailCost()
    {
        Song song = NewSong();
        var notes = new List<NoteBehaviour> { Note(0) };
        for (int i = 0; i < 10000; i++) notes.Add(Note(900000 + i));
        var schedule = new NoteSchedule();
        schedule.Reset(notes);
        schedule.Advance(song);
        for (int i = 0; i < 1000; i++) schedule.Advance(song);
        song.PositionReads = song.DistanceReads = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < 10000; i++) schedule.Advance(song);
        long ticks = Stopwatch.GetTimestamp() - start;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(song.PositionReads == 10000 && song.DistanceReads == 10000, "Future tail caused repeated clock reads.");
        Require(song.Generated.Count == 1 && bytes == 0, "Settled scheduling generated notes or allocated memory.");
        int baselineReads = 0;
        foreach (var note in notes)
            if (note.count == 0 && song.Position < note.StrumTime - song.Distance) baselineReads++;
        Require(baselineReads == 10000, "Full-scan negative control failed.");
        Console.WriteLine($"TAIL CONTROL PASSED: 10000 future notes, one clock read per frame, {bytes} allocated bytes, {ticks * 1000000.0 / Stopwatch.Frequency / 10000:F3} us per call (.NET fixture).");
    }

    private static void Require(bool value, string message)
    {
        assertions++;
        if (!value) throw new InvalidOperationException(message);
    }
}
