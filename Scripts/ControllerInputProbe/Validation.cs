using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class ControllerInputProbe
{
    private static Player player;
    private static Gamepad pad;
    private static Song song;
    private static int assertions;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Require(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }
    private static void Send(GamepadState state)
    {
        InputSystem.QueueStateEvent(pad, state);
        InputSystem.Update();
    }
    private static FunkinStrumline Reset(params int[] lanes)
    {
        Send(new GamepadState());
        player.ClearInput();
        player.ResetSong();
        var line = player.Strumlines[0];
        foreach (int lane in lanes) line.Add(new FunkinNoteState(1000, lane));
        line.Advance(1000, 0);
        return line;
    }
    public static void Run()
    {
        string saved = PlayerPrefs.HasKey("Funkin.Controls") ? PlayerPrefs.GetString("Funkin.Controls") : null;
        string legacy = PlayerPrefs.HasKey("Saved Keybinds") ? PlayerPrefs.GetString("Saved Keybinds") : null;
        var go = new GameObject("Controller Probe");
        try
        {
            PlayerPrefs.DeleteKey("Funkin.Controls");
            PlayerPrefs.DeleteKey("Saved Keybinds");
            VanillaControls.Reload();
            song = new Song();
            player = go.AddComponent<Player>();
            typeof(Player).GetField("owner", Flags).SetValue(player, song);
            player.ConfigureBindings();
            pad = InputSystem.AddDevice<Gamepad>();
            typeof(Player).GetMethod("OnDisable", Flags).Invoke(player, null);
            typeof(Player).GetMethod("OnEnable", Flags).Invoke(player, null);
            foreach (var button in new[] { GamepadButton.South, GamepadButton.East })
            {
                int lane = button == GamepadButton.South ? 1 : 3;
                var line = Reset(lane);
                int ghosts = 0;
                line.GhostMissed += _ => ghosts++;
                var state = new GamepadState().WithButton(button);
                Send(state);
                Require(line.Presses.Count == 1 && line.Presses[0].Direction == lane, button + " must create one mapped press");
                line.Advance(1000, 0);
                Require(line.Notes[0].Hit && ghosts == 0, button + " must hit without a ghost miss");
                Send(state);
                Require(line.Presses.Count == 0, button + " repeated state must not press again");
                Send(new GamepadState());
                Require(line.Releases.Count == 1, button + " must release once");
                line.Advance(1000, 0);
                Require(!line.IsHeld(lane), button + " must release lane");
            }
            var chord = Reset(1, 3);
            int chordGhosts = 0;
            chord.GhostMissed += _ => chordGhosts++;
            Send(new GamepadState().WithButton(GamepadButton.South).WithButton(GamepadButton.East));
            Require(chord.Presses.Count == 2, "A+B chord must produce two presses");
            chord.Advance(1000, 0);
            Require(chord.Notes.All(note => note.Hit) && chordGhosts == 0, "A+B chord must hit without a ghost miss");
            var wrong = Reset(0);
            int wrongGhosts = 0;
            wrong.GhostMissed += _ => wrongGhosts++;
            Send(new GamepadState().WithButton(GamepadButton.South));
            wrong.Advance(1000, 0);
            Require(wrongGhosts == 1 && !wrong.Notes[0].Hit, "Wrong-lane control must miss");

            foreach (var button in new[] { GamepadButton.South, GamepadButton.East })
            {
                int lane = button == GamepadButton.South ? 1 : 3;
                var resumed = Reset();
                int ghosts = 0;
                resumed.GhostMissed += _ => ghosts++;
                song.songStarted = false;
                var state = new GamepadState().WithButton(button);
                Send(state);
                song.songStarted = true;
                Send(state);
                resumed.Advance(1000, 0);
                Require(ghosts == 0 && !resumed.IsHeld(lane), button + " held through blocked input must not create a ghost miss");
                Send(new GamepadState());
                resumed.Add(new FunkinNoteState(1000, lane));
                resumed.Advance(1000, 0);
                Send(state);
                resumed.Advance(1000, 0);
                Require(resumed.Notes[0].Hit, button + " fresh press after resume must still hit");
                player.ClearInput();
                Send(state);
                resumed.Advance(1000, 0);
                Require(ghosts == 0 && !resumed.IsHeld(lane), button + " held through input clearing must not create a ghost miss");
            }
            foreach (var button in new[] { GamepadButton.South, GamepadButton.East })
            {
                int lane = button == GamepadButton.South ? 1 : 3;
                var hold = Reset(lane);
                hold.Notes[0].Length = hold.Notes[0].Remaining = 1000;
                var state = new GamepadState().WithButton(button);
                Send(state);
                hold.Advance(1000, 0);
                Send(state);
                hold.Advance(1100, .1);
                Require(hold.IsHeld(lane) && !hold.Notes[0].HoldDropped, button + " repeated state must preserve a sustain");
            }
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var line = Reset(1);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update();
                Require(line.Presses.Count == 1, "Fresh keyboard press must be captured");
                line.Advance(1000, 0);
                Require(line.Notes[0].Hit, "Fresh keyboard press must hit");
                player.ClearInput();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update();
                Require(line.Presses.Count == 0, "Held keyboard key must not retrigger after clearing input");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update();
                Require(line.Presses.Count == 1, "Repressed keyboard key must be captured");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
            Debug.Log("CONTROLLER PROBE PASSED: " + assertions);
            File.WriteAllText("result.txt", "Passed " + assertions + " assertions.");
        }
        finally
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
            UnityEngine.Object.DestroyImmediate(go);
            if (saved == null) PlayerPrefs.DeleteKey("Funkin.Controls"); else PlayerPrefs.SetString("Funkin.Controls", saved);
            if (legacy == null) PlayerPrefs.DeleteKey("Saved Keybinds"); else PlayerPrefs.SetString("Saved Keybinds", legacy);
        }
    }
}


