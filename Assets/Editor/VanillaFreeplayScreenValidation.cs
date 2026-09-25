using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaFreeplayScreenValidation
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static VanillaFreeplay freeplay;
    private static MenuV2 menu;
    private static double started;
    private static int errors;
    private static int launchChecks;
    private static int visibilityControls;
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_FREEPLAY_TEST_PATH");

    static VanillaFreeplayScreenValidation()
    {
        if (!SessionState.GetBool("FreeplayScreen.Active", false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
        Directory.CreateDirectory(Output);
        PlayerSettings.companyName = "UnityPartyValidation";
        PlayerSettings.productName = "VanillaFreeplayScreenValidation";
        SessionState.SetBool("FreeplayScreen.Active", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }

    private static T Field<T>(string name) => (T)typeof(VanillaFreeplay).GetField(name, Instance).GetValue(freeplay);
    private static void Set(string name, object value) => typeof(VanillaFreeplay).GetField(name, Instance).SetValue(freeplay, value);
    private static void Call(string name, params object[] args) => typeof(VanillaFreeplay).GetMethod(name, Instance).Invoke(freeplay, args);

    private static void Advance(float seconds)
    {
        while (seconds > 0)
        {
            float delta = Mathf.Min(seconds, 1f / 120);
            foreach (string field in new[] { "age", "capsuleAge", "selectionAge" }) Set(field, Field<float>(field) + delta);
            if (Field<float>("confirmAge") >= 0) Set("confirmAge", Field<float>("confirmAge") + delta);
            foreach (var sprite in freeplay.GetComponentsInChildren<VanillaFreeplaySprite>()) sprite.Tick(delta);
            foreach (var animation in freeplay.GetComponentsInChildren<VanillaFreeplayAnimate>()) animation.Tick(delta);
            Call("Draw", delta);
            seconds -= delta;
        }
    }

    private static void Capture(string name)
    {
        typeof(VanillaFreeplayValidation).GetField("freeplay", Static).SetValue(null, freeplay);
        typeof(VanillaFreeplayValidation).GetField("menu", Static).SetValue(null, menu);
        typeof(VanillaFreeplayValidation).GetMethod("Capture", Static).Invoke(null, new object[] {name, 1280, 720, false, .1f});
        var rows = new JArray();
        foreach (RectTransform row in Field<RectTransform>("list"))
            rows.Add(new JObject { ["song"] = row.name, ["x"] = row.anchoredPosition.x, ["y"] = -row.anchoredPosition.y,
                ["alpha"] = row.Find("Details").GetComponent<CanvasGroup>().alpha });
        File.WriteAllText(Path.Combine(Output, Path.ChangeExtension(name, ".json")), new JObject
        {
            ["character"] = freeplay.IsPico ? "pico" : "bf", ["difficulty"] = freeplay.Difficulty,
            ["selected"] = freeplay.SelectedIndex, ["confirmAge"] = Field<float>("confirmAge"), ["rows"] = rows,
            ["confirmFrame"] = freeplay.IsPico ? Field<VanillaFreeplayAnimate>("picoConfirm").CurrentFrame : -1
        }.ToString());
    }

    private static bool DetailsVisible()
    {
        var rows = Field<RectTransform>("list");
        for (int index = 0; index < rows.childCount; index++)
            if (index != freeplay.SelectedIndex && rows.GetChild(index).Find("Details").GetComponent<CanvasGroup>().alpha < .99f)
                return false;
        return true;
    }

    private static void CheckLaunchDetails()
    {
        if (VanillaMenuTiming.Delta <= 0) throw new InvalidOperationException("The launch probe requires a positive frame duration.");
        var launch = (IEnumerator)typeof(VanillaFreeplay).GetMethod("Launch", Instance).Invoke(freeplay, null);
        int steps = 0;
        while (Field<float>("confirmAge") < .6f)
        {
            if (++steps > 4096 || !launch.MoveNext()) throw new InvalidOperationException("The launch probe did not advance.");
            if (!DetailsVisible()) throw new InvalidOperationException("Launch faded another capsule's details.");
        }
        launchChecks++;
        var detail = Field<RectTransform>("list").GetChild(freeplay.SelectedIndex == 0 ? 1 : 0).Find("Details").GetComponent<CanvasGroup>();
        float alpha = detail.alpha;
        try
        {
            detail.alpha = .5f;
            if (DetailsVisible()) throw new InvalidOperationException("The faded-details control was accepted.");
            visibilityControls++;
        }
        finally { detail.alpha = alpha; }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - started < 3) return;
        EditorApplication.update -= Tick;
        bool passed = false;
        try
        {
            menu = Object.FindAnyObjectByType<MenuV2>();
            if (VanillaTitleScreen.Active != null)
                typeof(VanillaTitleScreen).GetMethod("CompleteMainMenuTransition", Instance).Invoke(VanillaTitleScreen.Active, null);
            foreach (string character in new[] {"bf", "pico"})
            {
                PlayerPrefs.SetString("Freeplay.Character", character);
                VanillaFreeplay.RememberDifficulty("Hard");
                freeplay = VanillaFreeplay.Open(menu, true, Path.Combine(Output, "EmptyBundles"));
                freeplay.enabled = false;
                Advance(2);
                int index = Field<List<VanillaFreeplaySong>>("filtered").FindIndex(song => song.meta.songName.StartsWith("Bopeebo", StringComparison.Ordinal)) + 1;
                if (index < 1) throw new InvalidOperationException("Bopeebo fixture is missing.");
                freeplay.MoveSelection(index - freeplay.SelectedIndex);
                Advance(3);
                Capture(character + "-bopeebo-hard.png");
                Call("ConfirmInstrumental", freeplay.SelectedSong.Instrumentals(freeplay.Difficulty)[0]);
                freeplay.StopAllCoroutines();
                Set("confirmAge", 0f);
                CheckLaunchDetails();
                Set("confirmAge", 0f);
                float previous = 0;
                foreach (float time in new[] {.1f, .35f, .6f, .9f, 1.2f})
                {
                    if (time >= freeplay.ConfirmationDelay) continue;
                    Advance(time - previous);
                    Capture(character + "-confirm-" + Mathf.RoundToInt(time * 1000) + ".png");
                    previous = time;
                }
                Object.DestroyImmediate(freeplay.gameObject);
            }
            passed = errors == 0;
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(Output, "failure.txt"), exception.ToString());
            Debug.LogException(exception);
        }
        SessionState.SetBool("FreeplayScreen.Active", false);
        File.WriteAllText(Path.Combine(Output, "result.txt"), passed ? "Captured" : "Failed");
        File.WriteAllText(Path.Combine(Output, "capture-result.json"), new JObject
        {
            ["passed"] = passed, ["errors"] = errors, ["launchChecks"] = launchChecks, ["visibilityControls"] = visibilityControls
        }.ToString());
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
