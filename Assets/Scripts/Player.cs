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
    public static bool playAsEnemy;
    public static float maxHitRoom = -160;
    public static float safeZoneOffset = 160;
    public static Player instance;
    public static float inputOffset;
    public static float visualOffset;
    public static KeyMode currentKeyMode = KeyMode.FourKey;
    public static SavedKeybinds keybinds;
    public static bool ControllerConfirmPressed => VanillaControls.PadPressed("ACCEPT");
    public static bool ControllerBackPressed => VanillaControls.PadPressed("BACK");
    public static bool ControllerPausePressed => VanillaControls.PadPressed("PAUSE");
    public static bool ControllerFavoritePressed => VanillaControls.PadPressed("FREEPLAY_FAVORITE");

    public static bool ControllerPressed(GamepadButton button)
    {
        foreach (Gamepad pad in Gamepad.all)
            if (pad[button].wasPressedThisFrame) return true;
        return false;
    }

    public static float MenuAxis(string name)
    {
        return VanillaControls.Axis(name == "Horizontal");
    }
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
        VanillaControls.Reload();
        if (keybinds != null)
        {
            if (keybinds.primary4K.Count == 4) primaryKeyCodes = keybinds.primary4K;
            if (keybinds.secondary4K.Count == 4) secondaryKeyCodes = keybinds.secondary4K;
        }
        for (int side = 0; side < 2; side++)
        {
            int index = side;
            Strumlines[side].CanHit = note => owner.vanillaPlayback?.CampaignStage?.Week != 8 || owner.vanillaPlayback.CampaignStage.CanHitWeekendNote(index, note.Direction, note.Time);
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
        int side = playAsEnemy ? 1 : 0;
        AddBindings(primaryKeyCodes, side);
        AddBindings(secondaryKeyCodes, side);
        Strumlines[0].Controlled = !playAsEnemy || demoMode;
        Strumlines[1].Controlled = playAsEnemy;
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
            (Pause.instance == null || !Pause.instance.pauseScreen.activeSelf && !Pause.instance.Transitioning);
    }

    private void OnInput(InputEventPtr input, InputDevice device)
    {
        if (!input.IsA<StateEvent>() && !input.IsA<DeltaStateEvent>()) return;
        if (!CanAcceptInput()) return;
        if (device is Gamepad pad)
        {
            int side = playAsEnemy ? 1 : 0;
            for (int direction = 0; direction < 4; direction++)
                foreach (int code in VanillaControls.Bindings[direction].buttons)
                {
                    ButtonControl control = VanillaControls.Button(pad, code);
                    if (control != null) CaptureButton(input, control, side, direction, 512 + code);
                }
            return;
        }
        if (!(device is Keyboard keyboard)) return;
        foreach (var binding in bindings)
        {
            var control = keyboard[binding.Key];
            if (!control.ReadValueFromEvent(input, out float value)) continue;
            int identity = device.deviceId * 1024 + (int)binding.Key;
            bool down = value >= 0.5f;
            if (down && control.ReadValue() >= 0.5f) continue;
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
        if (down && control.ReadValue() >= 0.5f) return;
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
        if (VanillaControls.Pressed("RESET") && !demoMode && !playAsEnemy && !Pause.PracticeMode) owner.health = 0;
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
            if (line.Presses.Count > 0) owner.vanillaPlayback?.CharacterStage?.Press(side);
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
