using System;
using System.Collections.Generic;

public static class FunkinRules
{
    public const double HitWindow = 160;
    public const double PixelsPerMillisecond = 0.45;
    public const double GhostTapDelay = 0.375;
    public const double ConfirmHoldTime = 0.15;

    public enum Judgement { Sick, Good, Bad, Shit, Miss }

    public static Judgement Judge(double difference)
    {
        double timing = Math.Abs((int)difference);
        if (timing <= 45) return Judgement.Sick;
        if (timing <= 90) return Judgement.Good;
        if (timing <= 135) return Judgement.Bad;
        if (timing <= 160) return Judgement.Shit;
        return Judgement.Miss;
    }

    public static int Score(double difference)
    {
        double timing = Math.Abs((int)difference);
        if (timing > 160) return -100;
        if (timing < 5) return 500;
        return (int)(500 * (1 - 1 / (1 + Math.Exp(-0.080 * (timing - 54.99)))) + 9);
    }

    public static double Health(Judgement judgement)
    {
        switch (judgement)
        {
            case Judgement.Sick: return 3;
            case Judgement.Good: return 1.5;
            case Judgement.Shit: return -2;
            default: return 0;
        }
    }

    public static bool BreaksCombo(Judgement judgement)
    {
        return judgement == Judgement.Bad || judgement == Judgement.Shit;
    }

    public static double NoteDistance(double time, double position, double speed, bool downscroll)
    {
        return PixelsPerMillisecond * (position - time) * speed * (downscroll ? -1 : 1);
    }
}

public sealed class FunkinNoteState
{
    public double Time;
    public double Length;
    public int Direction;
    public bool LowPriority;
    public bool Scoreable = true;
    public bool Hit;
    public bool Missed;
    public bool MayHit;
    public bool HandledMiss;
    public bool HoldDropped;
    public bool HandledDrop;
    public bool HoldFinished;
    public bool HeadVisible = true;
    public double Remaining;
    public object View;

    public FunkinNoteState(double time, int direction, double length = 0)
    {
        Time = time;
        Direction = direction;
        Length = Remaining = Math.Max(0, length);
    }
}

public readonly struct FunkinInputEvent
{
    public readonly int Direction;
    public readonly int Key;
    public readonly double Time;

    public FunkinInputEvent(int direction, int key, double time)
    {
        Direction = direction;
        Key = key;
        Time = time;
    }
}

public sealed class FunkinStrumline
{
    public enum Animation { Static, Press, Confirm, ConfirmHold }

    public readonly List<FunkinNoteState> Notes = new List<FunkinNoteState>();
    public readonly Animation[] Animations = new Animation[4];
    public readonly int[] AnimationVersions = new int[4];
    public readonly double[] AnimationTimes = new double[4];
    public readonly double[] ConfirmDurations = { 0.125, 0.125, 0.125, 0.125 };
    public readonly List<FunkinInputEvent> Presses = new List<FunkinInputEvent>();
    public readonly List<FunkinInputEvent> Releases = new List<FunkinInputEvent>();
    private readonly HashSet<int>[] held = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
    private readonly List<FunkinNoteState>[] candidates = { new List<FunkinNoteState>(), new List<FunkinNoteState>(), new List<FunkinNoteState>(), new List<FunkinNoteState>() };
    public bool Controlled = true;
    public bool BotPlay;
    public bool GhostTapping;
    public double RenderDistance = 1600;
    public double GhostTimer;
    public int HeadsHit;
    public event Action<FunkinNoteState, double, bool> NoteHit;
    public event Action<FunkinNoteState> NoteMissed;
    public event Action<int> GhostMissed;
    public event Action<FunkinNoteState, double> HoldScored;
    public event Action<FunkinNoteState, double> HoldMissed;

    public bool IsHeld(int direction) => held[direction].Count > 0;

    public void ClearInput()
    {
        Presses.Clear();
        Releases.Clear();
        for (int direction = 0; direction < 4; direction++)
        {
            held[direction].Clear();
            Play(direction, Animation.Static);
        }
    }

    public void Reset()
    {
        Notes.Clear();
        ClearInput();
        HeadsHit = 0;
        GhostTimer = 0;
    }

    public void Add(FunkinNoteState note)
    {
        int index = Notes.Count;
        while (index > 0 && Notes[index - 1].Time > note.Time) index--;
        Notes.Insert(index, note);
    }

    public void Play(int direction, Animation animation)
    {
        Animations[direction] = animation;
        AnimationTimes[direction] = 0;
        AnimationVersions[direction]++;
    }

    public void Advance(double position, double elapsed)
    {
        UpdateStrums(elapsed);
        UpdateHolds(position);
        UpdateGhostTimer(elapsed);
        ProcessInputs(position);
        ProcessNotes(position, elapsed);
    }

    private void UpdateStrums(double elapsed)
    {
        for (int direction = 0; direction < 4; direction++)
        {
            AnimationTimes[direction] += elapsed;
            double duration = FunkinRules.ConfirmHoldTime + (Controlled && !BotPlay ? ConfirmDurations[direction] : 0);
            if (Animations[direction] == Animation.Confirm && AnimationTimes[direction] >= duration)
                Play(direction, Animation.Static);
        }
    }

    private void UpdateHolds(double position)
    {
        foreach (FunkinNoteState note in Notes)
        {
            if (note.Length <= 0 || note.HoldFinished) continue;
            int direction = note.Direction;
            if (position > note.Time && note.Hit && !note.HoldDropped && Controlled && !BotPlay && !IsHeld(direction))
            {
                Play(direction, Animation.Static);
                note.HoldDropped = true;
            }
            if (note.Hit && note.Remaining <= 0)
            {
                note.HoldFinished = true;
                Play(direction, IsHeld(direction) ? Animation.Press : Animation.Static);
            }
            else if (position > note.Time && note.Hit && !note.HoldDropped)
            {
                if (Animations[direction] == Animation.Confirm && AnimationTimes[direction] >= ConfirmDurations[direction])
                    Play(direction, Animation.ConfirmHold);
                else if (Animations[direction] != Animation.Confirm && Animations[direction] != Animation.ConfirmHold)
                    Play(direction, Animation.Confirm);
                note.Remaining = Math.Max(0, note.Time + note.Length - position);
            }
        }
        for (int direction = 0; direction < 4; direction++)
            if (IsHeld(direction) && Animations[direction] == Animation.Static)
                Play(direction, Animation.Press);
    }

    private void UpdateGhostTimer(double elapsed)
    {
        foreach (FunkinNoteState note in Notes)
            if (!note.Hit) return;
        GhostTimer = Math.Max(0, GhostTimer - elapsed);
    }

    private bool MayGhostTap()
    {
        if (GhostTimer > 0) return false;
        foreach (FunkinNoteState note in Notes)
            if ((!note.Hit && note.MayHit) || (note.Length > 0 && !note.HoldFinished && (note.Hit || note.HoldDropped)))
                return false;
        return true;
    }

    private void ProcessInputs(double position)
    {
        foreach (var lane in candidates) lane.Clear();
        foreach (FunkinNoteState note in Notes)
            if (!note.Hit && note.MayHit) candidates[note.Direction].Add(note);
        foreach (FunkinInputEvent input in Presses)
        {
            held[input.Direction].Add(input.Key);
            if (BotPlay) continue;
            List<FunkinNoteState> lane = candidates[input.Direction];
            if (lane.Count == 0)
            {
                if (!GhostTapping || !MayGhostTap()) GhostMissed?.Invoke(input.Direction);
                Play(input.Direction, Animation.Press);
                continue;
            }
            FunkinNoteState target = lane.Find(note => !note.LowPriority) ?? lane[0];
            Hit(target, input.Time - target.Time, false, position);
            lane.Remove(target);
        }
        foreach (FunkinInputEvent input in Releases)
        {
            Play(input.Direction, Animation.Static);
            held[input.Direction].Remove(input.Key);
        }
        Presses.Clear();
        Releases.Clear();
    }

    public Func<FunkinNoteState, bool> CanHit;

    public void Hit(FunkinNoteState note, double difference, bool automatic, double position)
    {
        if (note.Hit || note.HandledMiss || CanHit != null && !CanHit(note)) return;
        note.Hit = true;
        note.MayHit = false;
        note.HeadVisible = !automatic && FunkinRules.BreaksCombo(FunkinRules.Judge(difference));
        note.HoldDropped = false;
        note.Remaining = Math.Min(note.Length, Math.Max(0, note.Time + note.Length - position));
        if (note.Scoreable) HeadsHit++;
        Play(note.Direction, Animation.Confirm);
        GhostTimer = FunkinRules.GhostTapDelay;
        NoteHit?.Invoke(note, difference, automatic);
    }

    private void ProcessNotes(double position, double elapsed)
    {
        foreach (FunkinNoteState note in Notes)
        {
            if (note.Hit || note.Missed) continue;
            if (position > note.Time + FunkinRules.HitWindow)
            {
                note.MayHit = false;
                note.Missed = note.HandledMiss = true;
                note.HoldDropped = note.Length > 0;
                if (Controlled && !BotPlay) NoteMissed?.Invoke(note);
            }
            else if ((!Controlled || BotPlay) && position >= note.Time)
                Hit(note, 0, true, position);
            else
                note.MayHit = position >= note.Time - FunkinRules.HitWindow;
        }
        foreach (FunkinNoteState note in Notes)
        {
            if (note.Length <= 0 || note.HoldFinished) continue;
            if (note.Hit && !note.HoldDropped && note.Remaining > 0 && Controlled && !BotPlay && note.Scoreable)
                HoldScored?.Invoke(note, elapsed);
            if (note.HoldDropped && !note.HandledDrop)
            {
                note.HandledDrop = true;
                if (Controlled && !BotPlay && note.Scoreable && note.Remaining > 160)
                    HoldMissed?.Invoke(note, -125 * note.Remaining / 1000);
            }
        }
    }
}
