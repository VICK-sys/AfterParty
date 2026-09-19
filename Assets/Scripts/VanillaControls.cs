using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public static class VanillaControls
{
    public sealed class Binding
    {
        public string id, header, label;
        public int group;
        public int[] keys, buttons;
    }

    private static List<Binding> bindings;
    public static IReadOnlyList<Binding> Bindings { get { EnsureLoaded(); return bindings; } }
    public static bool Capturing { get; set; }
    public static readonly string[] ButtonNames = { "A", "B", "X", "Y", "Start", "Back", "Left", "Down", "Up", "Right", "Lb", "Rb", "Lt", "Rt", "Ls", "Rs", "L Left", "L Down", "L Up", "L Right", "R Left", "R Down", "R Up", "R Right" };

    public static void Reload() { bindings = null; EnsureLoaded(); }
    private static void EnsureLoaded()
    {
        if (bindings != null) return;
        bindings = new List<Binding>();
        Add("NOTE_LEFT", "NOTES", "LEFT", 0, new[]{KeyCode.A,KeyCode.LeftArrow}, 6,2,16,20);
        Add("NOTE_DOWN", "NOTES", "DOWN", 0, new[]{KeyCode.S,KeyCode.DownArrow}, 7,0,17,21);
        Add("NOTE_UP", "NOTES", "UP", 0, new[]{KeyCode.W,KeyCode.UpArrow}, 8,3,18,22);
        Add("NOTE_RIGHT", "NOTES", "RIGHT", 0, new[]{KeyCode.D,KeyCode.RightArrow}, 9,1,19,23);
        Add("UI_LEFT", "UI", "LEFT", 1, new[]{KeyCode.A,KeyCode.LeftArrow}, 6,16);
        Add("UI_DOWN", "UI", "DOWN", 1, new[]{KeyCode.S,KeyCode.DownArrow}, 7,17);
        Add("UI_UP", "UI", "UP", 1, new[]{KeyCode.W,KeyCode.UpArrow}, 8,18);
        Add("UI_RIGHT", "UI", "RIGHT", 1, new[]{KeyCode.D,KeyCode.RightArrow}, 9,19);
        Add("ACCEPT", "UI", "ACCEPT", 1, new[]{KeyCode.Z,KeyCode.Space,KeyCode.Return}, 0);
        Add("BACK", "UI", "BACK", 1, new[]{KeyCode.X,KeyCode.Backspace,KeyCode.Escape}, 1);
        Add("PAUSE", "UI", "PAUSE", -1, new[]{KeyCode.P,KeyCode.Return,KeyCode.Escape}, 4);
        Add("RESET", "UI", "RESET", -1, new[]{KeyCode.R}, 5);
        Add("CUTSCENE_ADVANCE", "CUTSCENE", "ADVANCE", 2, new[]{KeyCode.Z,KeyCode.Return}, 0);
        Add("FREEPLAY_FAVORITE", "FREEPLAY", "FAVORITE", 3, new[]{KeyCode.F}, 3);
        Add("FREEPLAY_LEFT", "FREEPLAY", "LEFT", 3, new[]{KeyCode.Q}, 10);
        Add("FREEPLAY_RIGHT", "FREEPLAY", "RIGHT", 3, new[]{KeyCode.E}, 11);
        Add("FREEPLAY_CHAR_SELECT", "FREEPLAY", "CHAR SELECT", 3, new[]{KeyCode.Tab}, 2);
        Add("FREEPLAY_JUMP_TO_TOP", "FREEPLAY", "JUMP TO TOP", -1, new[]{KeyCode.Home}, 22);
        Add("FREEPLAY_JUMP_TO_BOTTOM", "FREEPLAY", "JUMP TO BOTTOM", -1, new[]{KeyCode.End}, 21);
        Add("WINDOW_SCREENSHOT", "WINDOW", "SCREENSHOT", 4, new[]{KeyCode.F3});
        Add("WINDOW_FULLSCREEN", "WINDOW", "FULLSCREEN", 4, new[]{KeyCode.F11});
        Add("VOLUME_UP", "VOLUME", "UP", 5, new[]{KeyCode.Equals,KeyCode.KeypadPlus});
        Add("VOLUME_DOWN", "VOLUME", "DOWN", 5, new[]{KeyCode.Minus,KeyCode.KeypadMinus});
        Add("VOLUME_MUTE", "VOLUME", "MUTE", 5, new[]{KeyCode.Alpha0,KeyCode.Keypad0});
        Add("DEBUG_DISPLAY", "DEBUG", "DISPLAY", 6, new[]{KeyCode.F6});
        try
        {
            var saved = JsonConvert.DeserializeObject<List<Binding>>(PlayerPrefs.GetString("Funkin.Controls", "[]"));
            if (saved != null && saved.Count > 0)
            {
                foreach (Binding entry in bindings)
                {
                    Binding source = saved.Find(b => b.id == entry.id);
                    if (source == null) continue;
                    if (source.keys != null) entry.keys = Normalize(source.keys);
                    if (source.buttons != null) entry.buttons = Normalize(source.buttons);
                    if (entry.group == 1 && entry.keys.All(k => k < 0)) entry.keys[0] = (int)(entry.id == "ACCEPT" ? KeyCode.Return : entry.id == "BACK" ? KeyCode.Escape : KeyCode.None);
                }
            }
            else
            {
                var legacy = JsonConvert.DeserializeObject<SavedKeybinds>(PlayerPrefs.GetString("Saved Keybinds", "null"));
                if (legacy != null)
                {
                    for (int i=0;i<4;i++)
                        if (legacy.primary4K.Count > i && legacy.secondary4K.Count > i)
                            bindings[i].keys = new[]{Code(legacy.primary4K[i]),Code(legacy.secondary4K[i])};
                }
            }
        }
        catch (JsonException) { }
        SyncNotes();
    }

    private static int Code(KeyCode key) => key == KeyCode.None ? -1 : (int)key;
    private static int[] Normalize(int[] values) => values.Length < 2 ? values.Concat(Enumerable.Repeat(-1, 2-values.Length)).ToArray() : values;
    private static void Add(string id, string header, string label, int group, KeyCode[] keys, params int[] buttons)
    {
        bindings.Add(new Binding { id=id, header=header, label=label, group=group, keys=Normalize(keys.Select(Code).ToArray()), buttons=Normalize(buttons) });
    }

    public static Binding Find(string id) { EnsureLoaded(); return bindings.Find(b => b.id == id); }
    public static bool Pressed(string id) => Read(id, 1);
    public static bool Held(string id) => Read(id, 0);
    private static bool Read(string id, int mode)
    {
        Binding binding = Find(id);
        if (binding == null) return false;
        foreach (int key in binding.keys)
            if (key > 0 && (mode == 1 ? Input.GetKeyDown((KeyCode)key) : Input.GetKey((KeyCode)key))) return true;
        return PadRead(binding, mode);
    }
    public static bool PadPressed(string id) => PadRead(Find(id), 1);
    private static bool PadRead(Binding binding, int mode)
    {
        foreach (Gamepad pad in Gamepad.all)
            foreach (int code in binding.buttons)
            {
                ButtonControl control = Button(pad, code);
                if (control != null && (mode == 1 ? control.wasPressedThisFrame : control.isPressed)) return true;
            }
        return false;
    }

    public static float Axis(bool horizontal) => (Held(horizontal ? "UI_RIGHT" : "UI_UP") ? 1 : 0) - (Held(horizontal ? "UI_LEFT" : "UI_DOWN") ? 1 : 0);
    public static ButtonControl Button(Gamepad pad, int code)
    {
        switch (code)
        {
            case 0:return pad.buttonSouth; case 1:return pad.buttonEast; case 2:return pad.buttonWest; case 3:return pad.buttonNorth;
            case 4:return pad.startButton; case 5:return pad.selectButton;
            case 6:return pad.dpad.left; case 7:return pad.dpad.down; case 8:return pad.dpad.up; case 9:return pad.dpad.right;
            case 10:return pad.leftShoulder; case 11:return pad.rightShoulder; case 12:return pad.leftTrigger; case 13:return pad.rightTrigger;
            case 14:return pad.leftStickButton; case 15:return pad.rightStickButton;
            case 16:return pad.leftStick.left; case 17:return pad.leftStick.down; case 18:return pad.leftStick.up; case 19:return pad.leftStick.right;
            case 20:return pad.rightStick.left; case 21:return pad.rightStick.down; case 22:return pad.rightStick.up; case 23:return pad.rightStick.right;
            default:return null;
        }
    }

    public static bool Rebind(int row, int column, int input, bool gamepad)
    {
        EnsureLoaded();
        Binding entry = bindings[row];
        int[] target = gamepad ? entry.buttons : entry.keys;
        if (input >= 0 && target.Contains(input)) return true;
        if (input < 0 && entry.group == 1 && !target.Where((v,i) => i != column).Any(v => v >= 0)) return false;
        int previous = target[column];
        if (input >= 0 && previous < 0 && entry.group == 1)
            foreach (Binding other in bindings.Where(b => b.group == 1 && b != entry))
            {
                int[] values = gamepad ? other.buttons : other.keys;
                if (values.Contains(input) && !values.Any(v => v >= 0 && v != input)) return false;
            }
        if (input >= 0 && entry.group >= 0)
        {
            foreach (Binding other in bindings.Where(b => b.group == entry.group && b != entry))
            {
                int[] values = gamepad ? other.buttons : other.keys;
                for (int i=0;i<values.Length;i++)
                    if (values[i] == input) values[i] = previous;
            }
        }
        target[column] = input;
        foreach (Binding binding in bindings)
        {
            int[] values = gamepad ? binding.buttons : binding.keys;
            int[] compact = Normalize(values.Where(v => v >= 0).Distinct().ToArray());
            if (gamepad) binding.buttons = compact; else binding.keys = compact;
        }
        Save();
        return true;
    }

    public static string Label(int input, bool gamepad)
    {
        if (input < 0) return "---";
        if (gamepad) return input < ButtonNames.Length ? ButtonNames[input] : "---";
        string name = ((KeyCode)input).ToString();
        switch ((KeyCode)input)
        {
            case KeyCode.LeftArrow:return "Left"; case KeyCode.DownArrow:return "Down";
            case KeyCode.UpArrow:return "Up"; case KeyCode.RightArrow:return "Right";
            case KeyCode.Return:return "Enter"; case KeyCode.Escape:return "Escape";
            case KeyCode.Backspace:return "BckSpc";
            case KeyCode.Equals:return "Plus"; case KeyCode.Minus:return "Minus";
            case KeyCode.PageUp:return "PgUp"; case KeyCode.PageDown:return "PgDown";
            case KeyCode.CapsLock:return "Caps"; case KeyCode.Print:return "PrtScrn";
            case KeyCode.LeftControl:case KeyCode.RightControl:return "Ctrl";
            case KeyCode.LeftShift:case KeyCode.RightShift:return "Shift";
            case KeyCode.LeftAlt:case KeyCode.RightAlt:return "Alt";
            case KeyCode.LeftBracket:return "["; case KeyCode.RightBracket:return "]";
            case KeyCode.Backslash:return "\\"; case KeyCode.Semicolon:return ";";
            case KeyCode.Quote:return "'"; case KeyCode.Comma:return ",";
            case KeyCode.Period:return "."; case KeyCode.Slash:return "/";
            case KeyCode.KeypadPlus:return "#+"; case KeyCode.KeypadMinus:return "#-";
            case KeyCode.KeypadMultiply:return "#*"; case KeyCode.KeypadPeriod:return "#.";
        }
        name = name.Replace("Alpha", "").Replace("Keypad", "#");
        return name.Length < 2 ? name : char.ToUpperInvariant(name[0])+name.Substring(1).ToLowerInvariant();
    }

    private static void SyncNotes()
    {
        Player.keybinds ??= new SavedKeybinds();
        Player.keybinds.primary4K = bindings.Take(4).Select(b => b.keys[0] < 0 ? KeyCode.None : (KeyCode)b.keys[0]).ToList();
        Player.keybinds.secondary4K = bindings.Take(4).Select(b => b.keys[1] < 0 ? KeyCode.None : (KeyCode)b.keys[1]).ToList();
        Player.primaryKeyCodes = Player.keybinds.primary4K;
        Player.secondaryKeyCodes = Player.keybinds.secondary4K;
        Player.keybinds.pauseKeyCode = Player.pauseKey = (KeyCode)Math.Max(0, bindings.Find(b => b.id == "PAUSE").keys[0]);
        Player.keybinds.resetKeyCode = Player.resetKey = (KeyCode)Math.Max(0, bindings.Find(b => b.id == "RESET").keys[0]);
        Player.instance?.ConfigureBindings();
    }

    public static void Save()
    {
        SyncNotes();
        PlayerPrefs.SetString("Funkin.Controls", JsonConvert.SerializeObject(bindings));
        PlayerPrefs.SetString("Saved Keybinds", JsonConvert.SerializeObject(Player.keybinds));
        PlayerPrefs.Save();
    }
}
