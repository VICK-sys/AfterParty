using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaMenuHitchValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static VanillaFreeplay freeplay;
    private static MenuV2 menu;
    private static double started;
    private static int frame = -1;
    private static int phase;
    private static int changes;
    private static int partialFrames;
    private static int previousCount;
    private static bool revealed;
    private static float before;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_FREEPLAY_TEST_PATH");

    static VanillaMenuHitchValidation()
    {
        if (SessionState.GetBool("VanillaMenuHitchValidation.Active", false)) EditorApplication.update += Tick;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run menu validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "MenuHitchValidation";
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaMenuHitchValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static float Age => (float)typeof(VanillaFreeplay).GetField("age", Private).GetValue(freeplay);

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        if (started == 0)
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.LockReloadAssemblies();
        }
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 120, "Menu validation timed out at phase " + phase);
            if (phase == 0)
            {
                menu = Object.FindAnyObjectByType<MenuV2>();
                if (menu?.vanillaMenu == null || EditorApplication.timeSinceStartup - started < 4) return;
                Require(Mathf.Approximately(VanillaMenuTiming.Clamp(1f / 60), 1f / 60), "Normal frame control changed speed.");
                freeplay = VanillaFreeplay.Open(menu, false, Path.Combine(Output, "EmptyBundles"));
                before = Age;
                Thread.Sleep(1000);
                phase = 1;
            }
            else if (phase == 1)
            {
                Require(Age - before < .1f && !freeplay.GetComponentsInChildren<VanillaFreeplayAnimate>().First(a => a.name == "Boyfriend DJ").Finished,
                    "A one-second hitch skipped the freeplay intro.");
                var dj = freeplay.GetComponentsInChildren<VanillaFreeplayAnimate>().First(a => a.name == "Boyfriend DJ");
                int oldFrame = dj.CurrentFrame;
                typeof(VanillaFreeplayAnimate).GetField("lastUpdateTime", Private).SetValue(dj, Time.realtimeSinceStartupAsDouble - 1);
                typeof(VanillaFreeplayAnimate).GetMethod("Update", Private).Invoke(dj, null);
                Require(dj.CurrentFrame - oldFrame <= 1 && !dj.Finished, "Animate playback consumed a stalled second.");
                var sprite = freeplay.GetComponentsInChildren<VanillaFreeplaySprite>().First(a => a.FrameCount > 3);
                sprite.Tick(0);
                int oldSprite = sprite.FrameIndex;
                typeof(VanillaFreeplaySprite).GetField("lastUpdateTime", Private).SetValue(sprite, Time.realtimeSinceStartupAsDouble - 1);
                typeof(VanillaFreeplaySprite).GetMethod("Update", Private).Invoke(sprite, null);
                Require((sprite.FrameIndex - oldSprite + sprite.FrameCount) % sprite.FrameCount <= 1, "Sprite playback consumed a stalled second.");
                Debug.Log("MENU HITCH PASSED: normal delta control, stalled intro, Animate and sprite playback.");
                phase = 2;
            }
            else if (phase == 2)
            {
                if (Age < 3) return;
                Capture("normal.png", 1280, false);
                Capture("wide.png", 1600, false);
                Require(freeplay.Viewport.rect.width == 1600, "Wide freeplay kept its fixed viewport.");
                var score = freeplay.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Score");
                Require(score.anchoredPosition.x == 320, "Wide score did not follow the right edge.");
                Capture("blank.png", 1600, true);
                var transition = VanillaFreeplayTransition.Create(freeplay.GetComponent<Canvas>());
                Require(transition.Texture.width == 1600, "Transition cropped the wide viewport.");
                Object.DestroyImmediate(transition.gameObject);
                freeplay.ApplyLayout(1280);
                Require(score.anchoredPosition.x == 0, "Returning to 16:9 retained the wide offset.");
                Debug.Log("MENU LAYOUT PASSED: 16:9, 20:9, blank control, transition width and resize restoration.");
                VanillaPauseStickers.Begin(() => { changes++; Thread.Sleep(1000); });
                phase = 3;
            }
            else if (phase == 3)
            {
                var stickers = Object.FindAnyObjectByType<VanillaPauseStickers>();
                if (stickers == null)
                {
                    Require(changes == 1 && revealed && partialFrames >= 5, "Sticker reveal did not progress across multiple frames.");
                    Debug.Log("MENU STICKERS PASSED: one-second scene stall, individual removal, one scene change, reveal frames=" + partialFrames);
                    Finish(true, "All menu hitch and layout checks passed.");
                    return;
                }
                if (!(bool)typeof(VanillaPauseStickers).GetField("uncovering", Private).GetValue(stickers)) return;
                var images = stickers.GetComponentsInChildren<RawImage>(true);
                int count = images.Count(image => image.gameObject.activeSelf);
                if (!revealed)
                {
                    Require(count > images.Length / 2, "Scene loading removed all stickers on the first reveal frame.");
                    revealed = true;
                    previousCount = count;
                    Thread.Sleep(1000);
                }
                Require(count <= previousCount, "A removed sticker reappeared.");
                if (count > 0 && count < previousCount) partialFrames++;
                previousCount = count;
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Capture(string name, int width, bool blank)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(VanillaFreeplayValidation).GetField("freeplay", flags).SetValue(null, freeplay);
        typeof(VanillaFreeplayValidation).GetField("menu", flags).SetValue(null, menu);
        typeof(VanillaFreeplayValidation).GetMethod("Capture", flags).Invoke(null, new object[] { name, width, 720, blank, .3f });
    }

    private static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool("VanillaMenuHitchValidation.Active", false);
        File.WriteAllText(Path.Combine(Output, "result.txt"), "Passed=" + passed + "\n" + message);
        Debug.Log("MENU HITCH VALIDATION: passed=" + passed + " " + message);
        EditorApplication.UnlockReloadAssemblies();
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
