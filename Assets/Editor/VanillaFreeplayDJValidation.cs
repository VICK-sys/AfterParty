using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaFreeplayDJValidation
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static VanillaFreeplay freeplay;
    private static VanillaFreeplayAnimate dj;
    private static MenuV2 menu;
    private static int phase;
    private static int assertions;
    private static int errors;
    private static double started;
    private static double changed;
    private static double cartoonStarted = -1;
    private static bool finishing;
    private static bool sawRemote;
    private static bool sawTvSound;
    private static bool sawBlink;
    private static bool sawLoop;
    private static bool sawChannel;
    private static bool capturedAfk;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_FREEPLAY_TEST_PATH") ?? Path.GetFullPath("Validation/FreeplayDJ");

    static VanillaFreeplayDJValidation()
    {
        if (!SessionState.GetBool("VanillaFreeplayDJValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run DJ validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "FreeplayDJValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaFreeplayDJValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal)
            && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static T Field<T>(string name) => (T)typeof(VanillaFreeplay).GetField(name, Instance).GetValue(freeplay);
    private static void Set(string name, object value) => typeof(VanillaFreeplay).GetField(name, Instance).SetValue(freeplay, value);
    private static void Call(string name, params object[] args) => typeof(VanillaFreeplay).GetMethod(name, Instance).Invoke(freeplay, args);

    private static void Advance(float duration)
    {
        while (duration > 0)
        {
            float delta = Mathf.Min(duration, 1f / 120);
            dj.Tick(delta);
            Call("UpdateDJ", delta);
            duration -= delta;
        }
    }

    private static void Reopen(bool automatic = false, bool intro = false)
    {
        if (freeplay != null) Object.DestroyImmediate(freeplay.gameObject);
        freeplay = VanillaFreeplay.Open(menu, !intro, Path.Combine(Output, "EmptyBundles"));
        dj = Field<VanillaFreeplayAnimate>("dj");
        freeplay.enabled = dj.enabled = automatic;
    }

    private static void CheckTiming()
    {
        Reopen();
        Advance(59);
        Require(dj.CurrentLabel == "Idle" && !Field<bool>("djSeenEasterEgg"), "AFK played before 60 seconds.");
        Advance(1);
        Require(dj.CurrentLabel == "Idle", "AFK interrupted an unfinished idle cycle.");
        Advance(0.7f);
        Require(dj.CurrentLabel == "AFK" && Field<bool>("djSeenEasterEgg"), "AFK did not trigger at the next idle boundary.");
        Advance(6);
        Require(dj.CurrentLabel == "Idle", "AFK did not finish and return to Idle.");
        Set("djIdleTime", 119f);
        Advance(0.9f);
        Require(dj.CurrentLabel == "Idle", "TV played before 120 subsequent idle seconds.");
        Advance(0.8f);
        Require(dj.CurrentLabel == "Watching TV", "TV did not follow AFK after 120 idle seconds.");
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(860);
        for (int i = 0; i < 100; i++)
        {
            dj.Tick(10);
            Call("UpdateDJ", 0f);
            sawBlink |= dj.LabelFrame == 126;
            sawLoop |= dj.LabelFrame == 148;
            sawChannel |= dj.LabelFrame == 59;
        }
        UnityEngine.Random.state = randomState;
        Require(sawBlink && sawLoop && sawChannel, "TV did not select blink, watch, and channel-change ranges.");
        Reopen();
        Action[] actions = { () => freeplay.MoveSelection(1), () => freeplay.ChangeDifficulty(1), () => freeplay.ChangeFilter(1),
            () => freeplay.SetMode(1) };
        foreach (Action action in actions)
        {
            Set("djIdleTime", 59.9f);
            Set("djSeenEasterEgg", true);
            action();
            Require(Field<float>("djIdleTime") == 0 && !Field<bool>("djSeenEasterEgg"), "Menu action did not reset AFK progress.");
            Advance(1);
            Require(dj.CurrentLabel == "Idle", "AFK ignored the activity reset.");
        }
        Set("filterIndex", 2);
        Call("RebuildList", false);
        if (freeplay.SelectedSong == null) freeplay.MoveSelection(1);
        Require(freeplay.SelectedSong != null, "Favorite control has no song.");
        Set("djIdleTime", 59.9f);
        Set("djSeenEasterEgg", true);
        freeplay.ToggleFavorite();
        Require(Field<float>("djIdleTime") == 0 && !Field<bool>("djSeenEasterEgg"), "Favorite action did not reset AFK progress.");
        freeplay.ToggleFavorite();
        dj.Play("AFK", false);
        freeplay.MoveSelection(1);
        Require(dj.CurrentLabel == "AFK", "Browsing interrupted the AFK animation.");
        Advance(6);
        Require(dj.CurrentLabel == "Idle", "AFK failed to finish after input.");
        Call("PlayCartoon", 0);
        freeplay.MoveSelection(1);
        freeplay.ChangeDifficulty(1);
        Require(dj.CurrentLabel == "Watching TV", "Browsing interrupted TV.");
        Reopen();
        Set("djIdleTime", 60f);
        Set("rankDjReaction", true);
        dj.PlayRange("Fist Pump", 4, -1, false);
        Advance(5);
        Require(dj.CurrentLabel == "Fist Pump" && Field<float>("djIdleTime") == 0, "AFK replaced a rank reaction.");
        Call("DrawRankAnimation", 0f);
        Require(dj.CurrentLabel == "Idle", "Rank reaction failed to restore Idle.");
        Debug.Log("FREEPLAY DJ DETERMINISTIC CHECKS PASSED: thresholds, idle boundaries, AFK completion, input resets, TV ranges, rank priority.");
    }

    private static void Capture(string filename)
    {
        typeof(VanillaFreeplayValidation).GetField("freeplay", Static).SetValue(null, freeplay);
        typeof(VanillaFreeplayValidation).GetField("menu", Static).SetValue(null, menu);
        typeof(VanillaFreeplayValidation).GetMethod("Capture", Static).Invoke(null, new object[] { filename, 1280, 720, false, 0.1f });
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (started == 0) started = changed = EditorApplication.timeSinceStartup;
        double wait = EditorApplication.timeSinceStartup - changed;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 150) throw new InvalidOperationException("DJ probe timed out at phase " + phase);
            switch (phase)
            {
                case 0:
                    if (wait < 4) return;
                    menu = Object.FindFirstObjectByType<MenuV2>();
                    Require(menu?.vanillaMenu != null, "Main menu missing.");
                    CheckTiming();
                    Reopen(true, true);
                    Require(dj.CurrentLabel == "Intro", "Opening skipped the DJ intro.");
                    Next();
                    break;
                case 1:
                    if (wait < 1.5f) return;
                    Require(!freeplay.Busy && dj.CurrentLabel == "Idle", "Intro did not return to Idle.");
                    Next();
                    break;
                case 2:
                    if (wait < 58)
                    {
                        if (dj.CurrentLabel != "Idle") Require(false, "Live AFK played early.");
                        return;
                    }
                    if (dj.CurrentLabel != "AFK") return;
                    Require(wait < 62, "Live AFK trigger drifted from 60 seconds.");
                    Next();
                    break;
                case 3:
                    if (!capturedAfk && wait >= 2 && dj.CurrentLabel == "AFK")
                    {
                        Capture("dj-afk.png");
                        capturedAfk = true;
                    }
                    if (dj.CurrentLabel != "Idle") return;
                    Require(capturedAfk, "Live AFK capture was skipped.");
                    Set("djIdleTime", 119f);
                    Next();
                    break;
                case 4:
                    if (dj.CurrentLabel != "Watching TV") return;
                    Require(wait >= 0.9f, "Live TV played before its threshold.");
                    Next();
                    break;
                case 5:
                    int frame = dj.LabelFrame;
                    AudioSource sound = Field<AudioSource>("tvSound");
                    sawRemote |= frame >= 79 && frame < 84 && sound.isPlaying;
                    sawTvSound |= sound.clip != null && sound.clip.name == "tv_on" && sound.isPlaying;
                    AudioSource cartoon = Field<AudioSource>("cartoon");
                    if (!cartoon.isPlaying) return;
                    if (cartoonStarted < 0) cartoonStarted = EditorApplication.timeSinceStartup;
                    if (EditorApplication.timeSinceStartup - cartoonStarted < 1.1) return;
                    Require(sawRemote && sawTvSound, "TV frame cues did not play remote and power sounds.");
                    Require(Field<AudioClip[]>("cartoonClips").Length == 24 && cartoon.clip.loadType == AudioClipLoadType.Streaming, "Cartoon library or streaming import failed.");
                    Require(Mathf.Abs(Field<float>("cartoonPreviewVolume") - 0.15f) < 0.001f, "TV did not duck previews to 15 percent.");
                    Require(freeplay.PreviewSource.isPlaying && freeplay.PreviewSource.volume <= 0.1051f * OptionsV2.menuVolume, "Preview audio ignored TV ducking.");
                    Capture("dj-tv.png");
                    cartoon.time = cartoon.clip.length - 0.2f;
                    Next();
                    break;
                case 6:
                    AudioSource channel = Field<AudioSource>("tvSound");
                    if (channel.clip == null || channel.clip.name != "channel_switch" || !channel.isPlaying) return;
                    Require(wait < 4, "Cartoon completion did not change channels promptly.");
                    Next();
                    break;
                case 7:
                    AudioSource nextCartoon = Field<AudioSource>("cartoon");
                    if (!nextCartoon.isPlaying || Field<bool>("cartoonPending") || wait < 0.5f) return;
                    Require(nextCartoon.clip != null && nextCartoon.time < nextCartoon.clip.length - 1, "Channel change did not load another cartoon segment.");
                    freeplay.Close();
                    Next();
                    break;
                case 8:
                    if (wait < 0.3f) return;
                    Require(!Field<AudioSource>("cartoon").isPlaying && !Field<AudioSource>("tvSound").isPlaying
                        && !Field<bool>("cartoonPending"), "Exit left cartoon audio or a pending cue active.");
                    Reopen();
                    Call("PlayCartoon", 84);
                    Call("UpdateDJ", 0f);
                    Require(Field<bool>("cartoonPending"), "Pending cue control did not start.");
                    freeplay.ConfirmSelection();
                    freeplay.StopAllCoroutines();
                    Require(dj.CurrentLabel == "Confirm" && !Field<bool>("cartoonPending"), "Confirm did not replace TV and cancel its pending cue.");
                    Advance(1);
                    Require(dj.CurrentLabel == "Confirm" && !Field<AudioSource>("cartoon").isPlaying, "TV restarted after confirmation.");
                    AudioSource destroyedAudio = Field<AudioSource>("cartoon");
                    Object.DestroyImmediate(freeplay.gameObject);
                    Require(destroyedAudio == null, "Destroy left a cartoon audio source alive.");
                    Finish(errors == 0);
                    break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Next() { phase++; changed = EditorApplication.timeSinceStartup; }

    private static void Finish(bool passed)
    {
        if (finishing) return;
        finishing = true;
        SessionState.SetBool("VanillaFreeplayDJValidation.Active", false);
        string result = "FREEPLAY DJ VALIDATION: passed=" + passed + ", errors=" + errors + ", assertions=" + assertions + ", phase=" + phase;
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        Debug.Log(result);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
