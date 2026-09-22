using System.Collections.Generic;
using UnityEngine;
public class SavedKeybinds
{
    public List<KeyCode> primary4K = new List<KeyCode>();
    public List<KeyCode> secondary4K = new List<KeyCode>();
    public KeyCode pauseKeyCode, resetKeyCode;
}
public static class OptionsV2 { public static bool GhostTapping; }
public class Pause
{
    public static Pause instance;
    public static bool PracticeMode;
    public bool Transitioning;
    public GameObject pauseScreen;
}
public class NoteObject
{
    public bool dummyNote;
    public FunkinNoteState State;
    public double strumTime;
}
public class ProbeStage
{
    public int Week;
    public bool CanHitWeekendNote(int side, int direction, double time) => true;
    public void Press(int side) { }
}
public class ProbePlayback
{
    public ProbeStage CampaignStage, CharacterStage;
}
public class Song
{
    public static Song instance;
    public bool songSetupDone = true, songStarted = true, IsCountingDown, isDead;
    public System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
    public ProbePlayback vanillaPlayback;
    public double SongPosition;
    public float health;
    public void ApplyFunkinHit(NoteObject note, double timing, bool automatic) { }
    public void ApplyFunkinMiss(NoteObject note) { }
    public void ApplyFunkinGhost(int side, int direction) { }
    public void ApplyFunkinHold(int side, double elapsed) { }
    public void ApplyFunkinDrop(int side, double penalty) { }
    public void KeepFunkinHoldPose(int side) { }
    public void RefreshFunkinStrums() { }
}
