using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaCharacterValidation
{
    private static readonly string[] Songs = { "tutorial", "bopeebo", "dadbattle", "bopeebo", "spookeez", "pico", "satin-panties", "cocoa", "senpai" };
    private static readonly string[] Difficulties = { "Hard", "Hard", "Hard", "Erect", "Erect", "Hard", "Hard", "Hard", "Hard" };
    private static int index;
    private static int phase;
    private static int errors;
    private static int countdownChanges;
    private static string countdownAnimation;
    private static bool sawCountdown;
    private static bool finishing;
    private static double changed;
    private static double pausedPosition;
    private static int pausedFrame;
    private static Song song;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_CHARACTER_TEST_PATH");
    private static BindingFlags Private => BindingFlags.Instance | BindingFlags.NonPublic;

    static VanillaCharacterValidation()
    {
        if (!SessionState.GetBool("VanillaCharacterValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run character validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "SongValidation";
        CheckRules();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaCharacterValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void CheckRules()
    {
        CheckIdlePoseFrames();
        CheckManualHoldTimer();
        var data = new JObject();
        Require(VanillaCharacterTiming.IsDanceStep(data, -4) && VanillaCharacterTiming.IsDanceStep(data, 0)
            && VanillaCharacterTiming.IsDanceStep(data, 4) && !VanillaCharacterTiming.IsDanceStep(data, 2), "Default beat cadence changed.");
        data["danceEvery"] = 2;
        Require(VanillaCharacterTiming.IsDanceStep(data, 8) && !VanillaCharacterTiming.IsDanceStep(data, 4), "Two-beat cadence changed.");
        data["danceEvery"] = .5f;
        Require(VanillaCharacterTiming.IsDanceStep(data, 2) && !VanillaCharacterTiming.IsDanceStep(data, 1), "Half-beat cadence changed.");
        data["danceEvery"] = 0;
        Require(!VanillaCharacterTiming.IsDanceStep(data, 0), "Disabled-dance control passed.");
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week1Assets/characters");
        foreach (string id in new[] { "bf", "dad", "gf" })
        {
            var obj = new GameObject("Character Rule " + id);
            try
            {
                var graphic = obj.AddComponent<VanillaWeek2Graphic>();
                graphic.Load(Path.Combine(root, id), 0);
                bool alternate = false;
                if (id == "gf")
                {
                    graphic.Play("danceRight");
                    string left = VanillaCharacterTiming.DanceAnimation(graphic, false, false, ref alternate);
                    Require(left == "danceLeft", "Girlfriend did not alternate left.");
                    graphic.Play(left);
                    Require(VanillaCharacterTiming.DanceAnimation(graphic, false, false, ref alternate) == "danceRight", "Girlfriend did not alternate right.");
                }
                else
                {
                    graphic.Play("idle");
                    graphic.Advance(.3f, Vector3.zero, 0);
                    int frame = graphic.Frame;
                    Require(VanillaCharacterTiming.DanceAnimation(graphic, false, false, ref alternate) == null && graphic.Frame == frame,
                        "Fast-tempo idle restarted before completion: " + id);
                    graphic.Advance(.5f, Vector3.zero, 0);
                    Require(VanillaCharacterTiming.DanceAnimation(graphic, false, false, ref alternate) == "idle", "Finished idle did not restart: " + id);
                    if (id == "dad")
                    {
                        graphic.Play("idle-hold");
                        Require(VanillaCharacterTiming.DanceAnimation(graphic, false, false, ref alternate) == "idle", "Dad idle hold prevented the next bop.");
                    }
                }
                graphic.Play("singLEFT");
                Require(VanillaCharacterTiming.DanceAnimation(graphic, false, false, ref alternate) == null, "Singing priority control failed: " + id);
                Require(VanillaCharacterTiming.DanceAnimation(graphic, true, false, ref alternate) != null, "Released singing never returned to idle: " + id);
                if (id == "bf")
                {
                    graphic.Play("hey");
                    Require(VanillaCharacterTiming.DanceAnimation(graphic, false, false, ref alternate) == null, "Unfinished greeting was interrupted.");
                    graphic.Advance(2, Vector3.zero, 0);
                    Require(VanillaCharacterTiming.DanceAnimation(graphic, false, true, ref alternate) == "idle", "Finished greeting blocked idle.");
                }
            }
            finally { Object.DestroyImmediate(obj); }
        }
        var clock = new GameObject("Character Tempo Rule");
        try
        {
            var playback = clock.AddComponent<VanillaSongPlayback>();
            typeof(VanillaSongPlayback).GetField("timeChanges", Private).SetValue(playback,
                JArray.Parse("[{\"t\":0,\"b\":0,\"bpm\":120},{\"t\":2000,\"b\":4,\"bpm\":180}]").ToArray());
            Require(Mathf.Abs(playback.BeatAt(-500) + 1) < .0001f && Mathf.Abs(playback.BeatAt(3000) - 7) < .0001f,
                "Countdown or tempo-change beat clock drifted.");
        }
        finally { Object.DestroyImmediate(clock); }
        Debug.Log("CHARACTER RULES PASSED: default, fractional and disabled cadence, fast-tempo completion, alternating dances, singing and special priority, tempo changes.");
    }

    public static void CheckIdlePoseFrames()
    {
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/Week1Assets/characters/dad");
        var data = JObject.Parse(File.ReadAllText(Path.Combine(root, "graphic.json")));
        var idleFrames = data["animations"]["idle"]["frames"].Values<int>().ToArray();
        var singFrames = data["animations"]["singUP"]["frames"].Values<int>().ToArray();
        var obj = new GameObject("Dad Idle Frame Isolation");
        try
        {
            var graphic = obj.AddComponent<VanillaWeek2Graphic>();
            graphic.Load(root, 0);
            graphic.Play("idle-hold");
            for (int tick = 0; tick < 480; tick++)
            {
                graphic.Advance(1f / 120, Vector3.zero, 0);
                Require(graphic.Animation == "idle-hold" && idleFrames.Contains(graphic.Frame),
                    "Dad idle-hold displayed a singing frame without a note: " + graphic.Frame);
            }
            graphic.Play("singUP");
            graphic.Advance(1f / 120, Vector3.zero, 0);
            Require(singFrames.Contains(graphic.Frame) && !idleFrames.Contains(graphic.Frame), "Singing frame control did not leave idle.");
            graphic.Play("singUP-hold");
            for (int tick = 0; tick < 120; tick++)
            {
                graphic.Advance(1f / 120, Vector3.zero, 0);
                Require(singFrames.Contains(graphic.Frame), "Dad singing hold escaped its source clip.");
            }
        }
        finally { Object.DestroyImmediate(obj); }
        Debug.Log("DAD FRAME ISOLATION PASSED: four seconds of idle hold, explicit singing control, and one second of singing hold.");
    }

    public static void CheckManualHoldTimer()
    {
        var root = new GameObject("Manual Hold Timer Probe");
        Player previous = Player.instance;
        try
        {
            var owner = root.AddComponent<Song>();
            owner.stepCrochet = 150;
            Player.instance = root.AddComponent<Player>();
            var line = Player.instance.Strumlines[0];
            line.Controlled = true;
            line.BotPlay = false;
            string folder = Path.Combine(Application.streamingAssetsPath, "Bundles/Week1Assets/characters/bf");
            var graphic = root.AddComponent<VanillaWeek2Graphic>();
            graphic.Load(folder, 0);
            foreach (Type stageType in new[] { typeof(VanillaCampaignStage), typeof(VanillaWeek2Stage), typeof(VanillaWeek3Stage) })
            {
                line.ClearInput();
                line.Controlled = true;
                line.BotPlay = false;
                var component = root.AddComponent(stageType);
                stageType.GetField("song", Private).SetValue(component, owner);
                Array actors = (Array)stageType.GetField("actors", Private).GetValue(component);
                Type actorType = actors.GetType().GetElementType();
                object actor = Activator.CreateInstance(actorType, true);
                actorType.GetField("graphic").SetValue(actor, graphic);
                actorType.GetField("current")?.SetValue(actor, graphic);
                var data = JObject.Parse(File.ReadAllText(Path.Combine(folder, "character.json")));
                actorType.GetField("data").SetValue(actor, data);
                actors.SetValue(actor, 0);
                actors.SetValue(actor, 1);
                graphic.Play("singLEFT");
                var timer = actorType.GetField("holdTimer");
                var stage = (IVanillaCharacterStage)component;
                var update = actorType.GetMethod("UpdateSinging");
                void Tick(float delta) => update.Invoke(actor, new object[] { delta, owner.stepCrochet, VanillaCharacterTiming.IsHoldingInput(0) });
                void Check(bool value, string message) => Require(value, stageType.Name + ": " + message);
                void HoldKey(bool held)
                {
                    (held ? line.Presses : line.Releases).Add(new FunkinInputEvent(3, 3, 0));
                    line.Advance(0, 0);
                }
                int UntilIdle(float delta)
                {
                    int ticks = 0;
                    while (VanillaCharacterTiming.IsSinging(graphic.Animation) && ticks < 1000)
                    {
                        Tick(delta);
                        ticks++;
                    }
                    Check(graphic.Animation == "idle", "Singing did not return to idle.");
                    return ticks;
                }
                timer.SetValue(actor, .25f);
                stage.Hold(0);
                Require(Mathf.Abs((float)timer.GetValue(actor) - .25f) < .0001f,
                    "Manual sustain reset the sing timer: " + stageType.Name + ", timer=" + timer.GetValue(actor));
                stage.Press(0);
                Check((float)timer.GetValue(actor) == 0, "Manual press did not reset the timer.");
                timer.SetValue(actor, .25f);
                stage.Sing(0, 0, true);
                Check((float)timer.GetValue(actor) == .25f, "Miss reset the elapsed timer without a press.");
                stage.Sing(0, 0, false);
                Check((float)timer.GetValue(actor) == 0, "Successful hit did not reset the timer.");

                foreach (int fps in new[] { 30, 60, 144 })
                foreach (int bpm in new[] { 80, 100, 180 })
                {
                    owner.stepCrochet = 15000f / bpm;
                    float duration = 120f / bpm;
                    float delta = 1f / fps;
                    line.Controlled = true;
                    line.BotPlay = false;
                    line.ClearInput();
                    stage.Sing(0, 0, false);
                    float tapTime = UntilIdle(delta) * delta;
                    Check(tapTime >= duration - .0001f && tapTime <= duration + delta + .0001f, "Tap duration changed at " + bpm + " BPM / " + fps + " FPS.");

                    stage.Press(0);
                    stage.Sing(0, 0, true);
                    float missTime = UntilIdle(delta) * delta;
                    Check(missTime >= duration * 2 - .0001f && missTime <= duration * 2 + delta + .0001f, "Miss duration did not double.");

                    HoldKey(true);
                    stage.Sing(0, 0, false);
                    for (int tick = 0; tick < Mathf.CeilToInt((duration + .25f) / delta); tick++)
                    {
                        stage.Hold(0);
                        Tick(delta);
                    }
                    Check(graphic.Animation == "singLEFT" && (float)timer.GetValue(actor) > duration, "Manual sustain stopped or reset its elapsed timer.");
                    HoldKey(false);
                    Tick(delta);
                    Check(graphic.Animation == "idle", "Long manual sustain added a delay after release.");

                    foreach (bool bot in new[] { true, false })
                    {
                        line.Controlled = bot;
                        line.BotPlay = bot;
                        stage.Sing(0, 0, false);
                        for (int tick = 0; tick < Mathf.CeilToInt((duration + .25f) / delta); tick++)
                        {
                            stage.Hold(0);
                            Tick(delta);
                        }
                        Check(graphic.Animation == "singLEFT", "Automated sustain ended before release.");
                        float automaticTime = UntilIdle(delta) * delta;
                        Check(automaticTime >= duration - delta - .0001f && automaticTime <= duration + delta + .0001f,
                            "Automated sustain lost its source release delay.");
                    }
                }

                line.Controlled = true;
                line.BotPlay = false;
                owner.stepCrochet = 150;
                stage.Sing(0, 0, false);
                HoldKey(true);
                Tick(.3f);
                HoldKey(false);
                Tick(.3f);
                Check(graphic.Animation == "singLEFT", "Short manual sustain ended before its sing duration.");
                Tick(.61f);
                Check(graphic.Animation == "idle", "Short manual sustain restarted its timer on release.");

                stage.Sing(0, 0, false);
                HoldKey(true);
                Tick(2);
                Check(graphic.Animation == "singLEFT", "Held directional input without an active sustain failed to protect singing.");
                HoldKey(false);
                Tick(.01f);
                Check(graphic.Animation == "idle", "Released directional input failed to return to idle.");

                stage.Sing(0, 0, false);
                timer.SetValue(actor, 1.2f);
                Tick(0);
                Check(graphic.Animation == "singLEFT", "Singing ended at the strict duration boundary.");
                Tick(.001f);
                Check(graphic.Animation == "idle", "Singing did not end past the duration boundary.");

                stage.Sing(0, 0, false);
                Tick(.9f);
                owner.stepCrochet = 75;
                Tick(.01f);
                Check(graphic.Animation == "idle", "Faster tempo retained the old sing duration.");
                owner.stepCrochet = 150;
                stage.Sing(0, 0, false);
                Tick(.9f);
                owner.stepCrochet = 300;
                Tick(.4f);
                Check(graphic.Animation == "singLEFT", "Slower tempo retained the old sing duration.");

                data["singTime"] = 4;
                Tick(.01f);
                Check(graphic.Animation == "idle", "Character singTime did not override the default duration.");
                graphic.Play("hey");
                timer.SetValue(actor, .5f);
                Tick(.01f);
                Check((float)timer.GetValue(actor) == 0 && graphic.Animation == "hey", "Special animation retained a stale sing timer.");

                var opponentLine = Player.instance.Strumlines[1];
                opponentLine.Controlled = true;
                opponentLine.BotPlay = false;
                opponentLine.Presses.Add(new FunkinInputEvent(2, 2, 0));
                opponentLine.Advance(0, 0);
                graphic.Play("singLEFT");
                timer.SetValue(actor, .25f);
                stage.Hold(1);
                Check((float)timer.GetValue(actor) == .25f && VanillaCharacterTiming.IsHoldingInput(1), "Manual opponent mode used automated hold behavior.");
                opponentLine.ClearInput();
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            Player.instance = previous;
        }
        Debug.Log("SING TIMING PASSED: three controllers at 30, 60, and 144 FPS and 80, 100, and 180 BPM, taps, misses, manual release, automated release controls, held input, tempo changes, custom duration, and opponent control.");
    }

    private static Component Stage => (Component)song.vanillaPlayback.CharacterStage;
    private static VanillaWeek2Graphic Graphic(int actor) => (VanillaWeek2Graphic)Stage.GetType().GetMethod("CharacterGraphic").Invoke(Stage, new object[] { actor });
    private static int DanceStep => (int)Stage.GetType().GetField("lastDanceStep", Private).GetValue(Stage);

    private static void Next(int next)
    {
        phase = next;
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (changed == 0) changed = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - changed;
        try
        {
            Require(errors == 0 && elapsed < 65, "Character validation failed or timed out at phase " + phase);
            if (phase == 0)
            {
                if (Object.FindAnyObjectByType<MenuV2>() == null || elapsed < 3) return;
                if (index == Songs.Length) { Finish(true); return; }
                OptionsV2.DesperateMode = OptionsV2.LiteMode = OptionsV2.Middlescroll = false;
                Pause.ResetSession();
                VanillaStoryCampaign.ReturnToMenu();
                var item = VanillaFreeplayCatalog.Discover(Path.Combine(Application.streamingAssetsPath, "Bundles"))
                    .Single(entry => string.Equals(entry.meta.songName.Replace(" ", "-"), Songs[index], StringComparison.OrdinalIgnoreCase));
                Song.currentSongMeta = item.meta;
                Song.difficulty = item.Difficulty(Difficulties[index]);
                Song.modeOfPlay = PlayModes.Autoplay;
                countdownAnimation = null;
                countdownChanges = 0;
                sawCountdown = false;
                SceneManager.LoadScene("Game_Backup3");
                Next(1);
            }
            else if (phase == 1)
            {
                song = Object.FindAnyObjectByType<Song>();
                if (song?.vanillaPlayback?.CharacterStage == null) return;
                if (song.IsCountingDown)
                {
                    sawCountdown |= DanceStep < 0 && DanceStep != int.MinValue;
                    string animation = Graphic(index == 0 ? 1 : 2).Animation;
                    if (countdownAnimation != null && countdownAnimation != animation) countdownChanges++;
                    countdownAnimation = animation;
                }
                if (!song.songStarted || song.SongPosition < 400) return;
                Require(sawCountdown && countdownChanges > 0, "Countdown did not dance: " + Songs[index]);
                Require(Math.Abs(DanceStep - Mathf.FloorToInt(song.vanillaPlayback.BeatAt(song.SongPosition) * 4)) <= 1, "Dances diverged from the song clock.");
                Require(!song.boyfriendObject.GetComponent<SpriteRenderer>().enabled && !song.boyfriendAnimator.enabled, "Legacy Boyfriend renderer remained active.");
                if (index == 0)
                    Require(!Graphic(2).gameObject.activeSelf && Graphic(1).Has("singLEFT"), "Tutorial shows duplicate Girlfriend or lacks source singing.");
                VanillaSongValidation.CaptureStage(song, Path.Combine(Output, Songs[index] + "-" + Difficulties[index] + ".png"));
                CheckActiveActors();
                Pause.instance.PauseSong();
                Next(2);
            }
            else if (phase == 2)
            {
                if (elapsed < .2) return;
                pausedPosition = song.SongPosition;
                pausedFrame = Graphic(0).Frame;
                Next(3);
            }
            else if (phase == 3)
            {
                if (elapsed < .4) return;
                Require(Pause.instance.IsPaused && Math.Abs(song.SongPosition - pausedPosition) < .001 && Graphic(0).Frame == pausedFrame,
                    "Pause advanced character animation or song timing.");
                Pause.instance.ContinueSong();
                Next(4);
            }
            else if (phase == 4)
            {
                if (elapsed < 1) return;
                Require(song.SongPosition > pausedPosition + 500, "Resume did not advance the song clock.");
                Pause.instance.RestartSong();
                Next(5);
            }
            else if (phase == 5)
            {
                var restarted = Object.FindAnyObjectByType<Song>();
                if (restarted == null || restarted.vanillaPlayback?.CharacterStage == null || !restarted.IsCountingDown) return;
                Require(restarted == song && restarted.RestartCount > 0, "Retry replaced the song instance.");
                song = restarted;
                if (DanceStep == int.MinValue) return;
                Require(DanceStep < 0, "Retry retained the previous dance clock.");
                Debug.Log("CHARACTER STAGE PASSED: " + Songs[index] + "/" + Difficulties[index] + ", source actors, countdown, song clock, sing and hold priority, pause, resume, retry.");
                Pause.instance.QuitSong();
                Next(6);
            }
            else if (phase == 6)
            {
                if (SceneManager.GetActiveScene().name != "Title") return;
                index++;
                Next(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(false);
        }
    }

    private static void CheckActiveActors()
    {
        var stage = song.vanillaPlayback.CharacterStage;
        Array actors = (Array)Stage.GetType().GetField("actors", Private).GetValue(Stage);
        for (int side = 0; side < 2; side++)
        {
            object actor = actors.GetValue(side);
            var type = actor.GetType();
            type.GetField("locked").SetValue(actor, false);
            stage.Sing(side, 0, false);
            string animation = Graphic(side).Animation;
            Require(animation == "singLEFT", "Source singing did not play.");
            type.GetMethod("Dance").Invoke(actor, new object[] { false });
            Require(Graphic(side).Animation == animation, "Beat interrupted singing.");
            type.GetField("holdTimer").SetValue(actor, .01f);
            stage.Hold(side);
            type.GetMethod("Dance").Invoke(actor, new object[] { false });
            Require(Graphic(side).Animation == animation, "Beat interrupted a sustain.");
            type.GetMethod("UpdateSinging").Invoke(actor, new object[] { 10f, song.stepCrochet, false });
            Require(Graphic(side).Animation.StartsWith("idle") || Graphic(side).Animation.StartsWith("dance"), "Released singing did not return to idle.");
        }
    }

    private static void Finish(bool passed)
    {
        finishing = true;
        SessionState.SetBool("VanillaCharacterValidation.Active", false);
        string result = "CHARACTER VALIDATION: passed=" + passed + ", errors=" + errors + ", song=" + index + ", phase=" + phase;
        Debug.Log(result);
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
