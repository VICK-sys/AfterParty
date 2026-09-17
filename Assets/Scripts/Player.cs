using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

[DefaultExecutionOrder(100)]
public class Player : MonoBehaviour
{
    public float safeFrames = 10;
    public static List<KeyCode> primaryKeyCodes = new List<KeyCode> { KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.UpArrow, KeyCode.RightArrow };
    public static List<KeyCode> secondaryKeyCodes = new List<KeyCode> { KeyCode.A, KeyCode.S, KeyCode.W, KeyCode.D };
    public static KeyCode pauseKey = KeyCode.Return;
    public static KeyCode resetKey = KeyCode.R;
    public static KeyCode startSongKey = KeyCode.Space;
    public static bool demoMode;
    public static bool twoPlayers;
    public static bool playAsEnemy;
    public static float maxHitRoom = -160;
    public static float safeZoneOffset = 160;
    public static Player instance;
    public static float inputOffset;
    public static float visualOffset;
    public static KeyMode currentKeyMode = KeyMode.FourKey;
    public static SavedKeybinds keybinds;
    public List<NoteObject> player1DummyNotes = new List<NoteObject>();
    public List<NoteObject> player2DummyNotes = new List<NoteObject>();
    public readonly FunkinStrumline[] Strumlines = { new FunkinStrumline(), new FunkinStrumline() };
    private readonly HashSet<int> pressedKeys = new HashSet<int>();
    private readonly Dictionary<Key, List<(int side, int direction)>> bindings = new Dictionary<Key, List<(int, int)>>();
    private bool accepting;
    private Song owner;
    public enum KeyMode { FourKey, FiveKey, SixKey, SevenKey, EightKey, NineKey }

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        instance = this;
        InputSystem.onEvent += OnInput;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onEvent -= OnInput;
        InputSystem.onDeviceChange -= OnDeviceChange;
        ClearInput();
        if (instance == this) instance = null;
    }

    private void Start()
    {
        owner = Song.instance;
        inputOffset = PlayerPrefs.GetFloat("Input Offset", 0);
        visualOffset = PlayerPrefs.GetFloat("Visual Offset", 0);
        if (keybinds != null)
        {
            if (keybinds.primary4K.Count == 4) primaryKeyCodes = keybinds.primary4K;
            if (keybinds.secondary4K.Count == 4) secondaryKeyCodes = keybinds.secondary4K;
        }
        for (int side = 0; side < 2; side++)
        {
            int index = side;
            Strumlines[side].NoteHit += (note, timing, automatic) => owner.ApplyFunkinHit((NoteObject)note.View, timing, automatic);
            Strumlines[side].NoteMissed += note => owner.ApplyFunkinMiss((NoteObject)note.View);
            Strumlines[side].GhostMissed += direction => owner.ApplyFunkinGhost(index, direction);
            Strumlines[side].HoldScored += (note, elapsed) => owner.ApplyFunkinHold(index, elapsed);
            Strumlines[side].HoldMissed += (note, penalty) => owner.ApplyFunkinDrop(index, penalty);
        }
        ConfigureBindings();
    }

    public void ResetSong()
    {
        foreach (FunkinStrumline line in Strumlines) line.Reset();
        pressedKeys.Clear();
        ConfigureBindings();
    }

    public void ClearInput()
    {
        accepting = false;
        pressedKeys.Clear();
        foreach (FunkinStrumline line in Strumlines) line.ClearInput();
    }

    public void ConfigureBindings()
    {
        bindings.Clear();
        AddBindings(primaryKeyCodes, playAsEnemy && !twoPlayers ? 1 : 0);
        AddBindings(secondaryKeyCodes, twoPlayers || playAsEnemy ? 1 : 0);
        Strumlines[0].Controlled = !playAsEnemy || twoPlayers || demoMode;
        Strumlines[1].Controlled = playAsEnemy || twoPlayers;
        foreach (FunkinStrumline line in Strumlines)
        {
            line.BotPlay = demoMode;
            line.GhostTapping = OptionsV2.GhostTapping;
        }
    }

    private void AddBindings(List<KeyCode> keys, int side)
    {
        for (int direction = 0; direction < Math.Min(4, keys.Count); direction++)
        {
            if (!TryConvertKey(keys[direction], out Key key)) continue;
            if (!bindings.TryGetValue(key, out var targets)) bindings[key] = targets = new List<(int, int)>();
            if (!targets.Contains((side, direction))) targets.Add((side, direction));
        }
    }

    public static bool TryConvertKey(KeyCode code, out Key key)
    {
        string name = code.ToString();
        if (name.StartsWith("Alpha", StringComparison.Ordinal)) name = "Digit" + name.Substring(5);
        else if (name.StartsWith("Keypad", StringComparison.Ordinal)) name = "Numpad" + name.Substring(6);
        switch (code)
        {
            case KeyCode.Return: name = "Enter"; break;
            case KeyCode.KeypadPeriod: name = "NumpadPeriod"; break;
            case KeyCode.KeypadEquals: name = "NumpadEquals"; break;
            case KeyCode.LeftControl: name = "LeftCtrl"; break;
            case KeyCode.RightControl: name = "RightCtrl"; break;
            case KeyCode.LeftCommand: name = "LeftMeta"; break;
            case KeyCode.RightCommand: name = "RightMeta"; break;
            case KeyCode.LeftWindows: name = "LeftMeta"; break;
            case KeyCode.RightWindows: name = "RightMeta"; break;
            case KeyCode.Menu: name = "ContextMenu"; break;
            case KeyCode.BackQuote: name = "Backquote"; break;
            case KeyCode.Numlock: name = "NumLock"; break;
            case KeyCode.Print: name = "PrintScreen"; break;
        }
        return Enum.TryParse(name, true, out key) && key != Key.None;
    }

    private bool CanAcceptInput()
    {
        return owner != null && owner.songSetupDone && (owner.songStarted || owner.IsCountingDown) && !owner.isDead &&
            owner.stopwatch != null && (owner.stopwatch.IsRunning || owner.IsCountingDown) &&
            (Pause.instance == null || !Pause.instance.pauseScreen.activeSelf);
    }

    private void OnInput(InputEventPtr input, InputDevice device)
    {
        if (!input.IsA<StateEvent>() && !input.IsA<DeltaStateEvent>()) return;
        if (!CanAcceptInput()) return;
        if (device is Gamepad pad)
        {
            int side = playAsEnemy && !twoPlayers ? 1 : 0;
            if (twoPlayers && Gamepad.all.Count > 0 && pad != Gamepad.all[0]) side = 1;
            CaptureButton(input, pad.dpad.left, side, 0, 512);
            CaptureButton(input, pad.dpad.down, side, 1, 513);
            CaptureButton(input, pad.dpad.up, side, 2, 514);
            CaptureButton(input, pad.dpad.right, side, 3, 515);
            CaptureButton(input, pad.buttonWest, side, 0, 516);
            CaptureButton(input, pad.buttonSouth, side, 1, 517);
            CaptureButton(input, pad.buttonNorth, side, 2, 518);
            CaptureButton(input, pad.buttonEast, side, 3, 519);
            CaptureButton(input, pad.leftStick.left, side, 0, 520);
            CaptureButton(input, pad.leftStick.down, side, 1, 521);
            CaptureButton(input, pad.leftStick.up, side, 2, 522);
            CaptureButton(input, pad.leftStick.right, side, 3, 523);
            CaptureButton(input, pad.rightStick.left, side, 0, 524);
            CaptureButton(input, pad.rightStick.down, side, 1, 525);
            CaptureButton(input, pad.rightStick.up, side, 2, 526);
            CaptureButton(input, pad.rightStick.right, side, 3, 527);
            return;
        }
        if (!(device is Keyboard keyboard)) return;
        foreach (var binding in bindings)
        {
            var control = keyboard[binding.Key];
            if (!control.ReadValueFromEvent(input, out float value)) continue;
            int identity = device.deviceId * 1024 + (int)binding.Key;
            bool down = value >= 0.5f;
            if (down ? !pressedKeys.Add(identity) : !pressedKeys.Remove(identity)) continue;
            foreach (var target in binding.Value)
            {
                var entry = new FunkinInputEvent(target.direction, identity, input.time * 1000);
                if (down) Strumlines[target.side].Presses.Add(entry);
                else Strumlines[target.side].Releases.Add(entry);
            }
        }
    }

    private void CaptureButton(InputEventPtr input, ButtonControl control, int side, int direction, int key)
    {
        if (!control.ReadValueFromEvent(input, out float value)) return;
        int identity = control.device.deviceId * 1024 + key;
        bool down = value >= 0.5f;
        if (down ? !pressedKeys.Add(identity) : !pressedKeys.Remove(identity)) return;
        var entry = new FunkinInputEvent(direction, identity, input.time * 1000);
        if (down) Strumlines[side].Presses.Add(entry);
        else Strumlines[side].Releases.Add(entry);
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed ||
            change == InputDeviceChange.SoftReset || change == InputDeviceChange.HardReset)
            ClearInput();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) ClearInput();
    }

    private void Update()
    {
        if (!CanAcceptInput())
        {
            if (accepting) ClearInput();
            return;
        }
        accepting = true;
        double position = owner.SongPosition - visualOffset;
        double realtime = Time.realtimeSinceStartupAsDouble * 1000;
        AdvanceFrame(position, realtime, Time.deltaTime);
    }

    public void AdvanceFrame(double position, double realtime, double elapsed)
    {
        for (int side = 0; side < Strumlines.Length; side++)
        {
            FunkinStrumline line = Strumlines[side];
            for (int i = 0; i < line.Presses.Count; i++)
            {
                FunkinInputEvent entry = line.Presses[i];
                line.Presses[i] = new FunkinInputEvent(entry.Direction, entry.Key, position - (realtime - entry.Time) - inputOffset);
            }
            line.Advance(position, elapsed);
            if (line.Notes.Exists(note => note.Hit && !note.HoldDropped && !note.HoldFinished && note.Remaining > 0))
                owner.KeepFunkinHoldPose(side);
        }
        owner.RefreshFunkinStrums();
    }

    public bool CanHitNote(NoteObject note)
    {
        if (note == null || note.dummyNote || note.State == null || note.State.Hit || note.State.Missed) return false;
        return Math.Abs(owner.SongPosition - visualOffset - note.strumTime) <= FunkinRules.HitWindow;
    }
}
