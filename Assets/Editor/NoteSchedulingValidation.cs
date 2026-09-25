using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FridayNightFunkin;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class NoteSchedulingValidation
{
    public static void RunAndBeginPause()
    {
        Run();
        VanillaPauseValidation.Begin();
    }

    public static void RunAndBeginGameplay()
    {
        Run();
        FunkinGameplayValidation.Begin();
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run note scheduling validation in an isolated batch editor.");
        string output = Environment.GetEnvironmentVariable("UNITY_PARTY_NOTE_SCHEDULE_PATH");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set UNITY_PARTY_NOTE_SCHEDULE_PATH.");
        Directory.CreateDirectory(output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "NoteScheduleValidation";
        Song previous = Song.instance;
        SongMetaV2 previousMeta = Song.currentSongMeta;
        float previousOffset = Player.visualOffset;
        var root = new GameObject("Note schedule validation");
        root.SetActive(false);
        try
        {
            var song = root.AddComponent<Song>();
            song.stopwatch = new Stopwatch();
            Song.instance = song;
            Song.currentSongMeta = null;
            var chart = new FNFSong(Path.Combine(Application.streamingAssetsPath, "Bundles/08-Weekend1/01-Darnell/Chart-erect.json"));
            var results = new JArray();
            foreach (int position in new[] { 0, 60000, 120000 })
            {
                Player.visualOffset = (float)(song.SongPosition - position);
                var notes = new List<NoteBehaviour>();
                foreach (var section in chart.Sections)
                    foreach (var data in section.Notes)
                    {
                        var note = new NoteBehaviour(section, data);
                        if (note.StrumTime <= position + song.FunkinRenderDistance) note.count = 1;
                        notes.Add(note);
                    }
                var schedule = new NoteSchedule();
                schedule.Reset(notes);
                schedule.Advance(song);
                int pending = notes.Count(note => note.count == 0);
                Require(pending > 0, "The future-note control is empty.");
                Action indexed = () => schedule.Advance(song);
                Action scan = () =>
                {
                    foreach (var note in notes)
                        if (note.count < 1) note.GenerateNote();
                };
                for (int i = 0; i < 100; i++) { scan(); indexed(); }
                var scanTimes = new List<double>();
                var indexedTimes = new List<double>();
                for (int repeat = 0; repeat < 7; repeat++)
                {
                    if (repeat % 2 == 0)
                    {
                        scanTimes.Add(Measure(scan));
                        indexedTimes.Add(Measure(indexed));
                    }
                    else
                    {
                        indexedTimes.Add(Measure(indexed));
                        scanTimes.Add(Measure(scan));
                    }
                }
                scanTimes.Sort();
                indexedTimes.Sort();
                Require(notes.Count(note => note.count == 0) == pending && song.lastNote == null, "A settled control spawned a note.");
                Require(indexedTimes[3] < scanTimes[3], "The cursor did not outperform the full-scan control.");
                results.Add(new JObject { ["positionMs"] = position, ["chartHeads"] = notes.Count,
                    ["pendingNotes"] = pending, ["fullScanMicroseconds"] = scanTimes[3], ["cursorMicroseconds"] = indexedTimes[3] });
            }
            File.WriteAllText(Path.Combine(output, "scheduler.json"), results.ToString());
            UnityEngine.Debug.Log("NOTE SCHEDULING VALIDATION PASSED: " + results.ToString(Newtonsoft.Json.Formatting.None));
        }
        finally
        {
            Song.instance = previous;
            Song.currentSongMeta = previousMeta;
            Player.visualOffset = previousOffset;
            Object.DestroyImmediate(root);
        }
    }

    private static double Measure(Action action)
    {
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < 200; i++) action();
        return (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency / 200;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
