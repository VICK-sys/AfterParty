using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaStickerAudioValidation
{
    private static readonly float[] samples = new float[1024];
    private static readonly FieldInfo uncovering = typeof(VanillaPauseStickers).GetField("uncovering", BindingFlags.Instance | BindingFlags.NonPublic);
    private static double started;
    private static double changed;
    private static int phase;
    private static int errors;
    private static int changes;
    private static float coverPeak;
    private static float uncoverPeak;
    private static bool finishing;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_STICKER_AUDIO_TEST_PATH");

    static VanillaStickerAudioValidation()
    {
        if (!SessionState.GetBool("VanillaStickerAudioValidation.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run sticker audio validation in an isolated batch editor.");
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "StickerAudioValidation";
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaStickerAudioValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void StartTransition(bool muted)
    {
        OptionsV2.miscVolume = muted ? 0 : .6f;
        InGameVolume.miscVolume = muted ? 1 : 0;
        coverPeak = uncoverPeak = 0;
        VanillaPauseStickers.Begin(() => changes++);
        changed = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (finishing || !EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        try
        {
            Require(errors == 0 && EditorApplication.timeSinceStartup - started < 90, "Sticker audio validation failed or timed out.");
            if (phase == 0)
            {
                if (EditorApplication.timeSinceStartup - started < 7) return;
                var menu = Object.FindAnyObjectByType<MenuV2>();
                if (menu == null) return;
                menu.OpenFreeplay(true);
                phase = 1;
                return;
            }
            if (phase == 1)
            {
                if (VanillaFreeplay.Active == null) return;
                AudioListener.pause = false;
                AudioListener.volume = 1;
                var clips = Resources.LoadAll<AudioClip>("FunkinPause/stickerSounds");
                Require(clips.Length == 8, "Expected all eight source sticker clips.");
                StartTransition(false);
                phase = 2;
                return;
            }
            var stickers = Object.FindAnyObjectByType<VanillaPauseStickers>();
            if (stickers != null)
            {
                stickers.GetComponent<AudioSource>().GetOutputData(samples, 0);
                float peak = samples.Max(value => Mathf.Abs(value));
                if ((bool)uncovering.GetValue(stickers)) uncoverPeak = Mathf.Max(uncoverPeak, peak);
                else coverPeak = Mathf.Max(coverPeak, peak);
                return;
            }
            if (VanillaPauseStickers.Active || EditorApplication.timeSinceStartup - changed < 2) return;
            if (phase == 2)
            {
                Require(changes == 1 && coverPeak > .0001f && uncoverPeak > .0001f,
                    "Sticker output was silent: cover=" + coverPeak + ", uncover=" + uncoverPeak);
                Debug.Log("STICKER AUDIO PASSED: appearance peak=" + coverPeak + ", removal peak=" + uncoverPeak + ", legacy volume=0.");
                StartTransition(true);
                phase = 3;
            }
            else
            {
                Require(changes == 2 && coverPeak == 0 && uncoverPeak == 0, "Effects-volume mute control produced sticker audio.");
                Debug.Log("STICKER AUDIO PASSED: effects-volume mute control, legacy volume=1.");
                Finish(true);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Finish(bool passed)
    {
        finishing = true;
        SessionState.SetBool("VanillaStickerAudioValidation.Active", false);
        string result = "STICKER AUDIO VALIDATION: passed=" + passed + ", errors=" + errors;
        Debug.Log(result);
        File.WriteAllText(Path.Combine(Output, "result.txt"), result + "\n");
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
