#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class VanillaFullscreenValidationBuild
{
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = "Builds/FullscreenValidation/Player/FridayFightFunkin.exe",
            target = BuildTarget.StandaloneWindows64,
            extraScriptingDefines = new[] { "FULLSCREEN_VALIDATION" },
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Fullscreen probe build failed");
    }
}
#endif
#if FULLSCREEN_VALIDATION && UNITY_STANDALONE_WIN
public sealed class VanillaFullscreenValidation : UnityEngine.MonoBehaviour
{
    private static bool failed;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        new UnityEngine.GameObject("Fullscreen Validation").AddComponent<VanillaFullscreenValidation>();
    }

    private System.Collections.IEnumerator Start()
    {
        yield return new UnityEngine.WaitForSecondsRealtime(3);
        if (VanillaTitleScreen.Active != null) VanillaTitleScreen.Active.gameObject.SetActive(false);
        MenuV2.Instance.mainScreen.gameObject.SetActive(true);
        yield return new UnityEngine.WaitForSecondsRealtime(1);
        Record("startup", true);
        yield return new UnityEngine.WaitForEndOfFrame();
        var image = UnityEngine.ScreenCapture.CaptureScreenshotAsTexture();
        var output = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../.."));
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, "fullscreen.png"), UnityEngine.ImageConversion.EncodeToPNG(image));
        Destroy(image);
        var runtime = FindFirstObjectByType<VanillaOptionsRuntime>();
        runtime.SetFullscreen(false);
        yield return new UnityEngine.WaitForSecondsRealtime(2);
        Record("windowed", false);
        UnityEngine.Screen.SetResolution(1024, 768, UnityEngine.FullScreenMode.Windowed);
        yield return new UnityEngine.WaitForSecondsRealtime(2);
        Record("4-by-3-control", false);
        Require(UnityEngine.Screen.width == 1024 && UnityEngine.Screen.height == 768, "4-by-3 window dimensions");
        runtime.SetFullscreen(true);
        yield return new UnityEngine.WaitForSecondsRealtime(2);
        Record("fullscreen-toggle", true);
        runtime.SetFullscreen(false);
        yield return new UnityEngine.WaitForSecondsRealtime(2);
        Record("restored-window", false);
        Require(UnityEngine.Screen.width == 1024 && UnityEngine.Screen.height == 768, "Window dimensions survived fullscreen");
        runtime.SetFullscreen(true);
        yield return new UnityEngine.WaitForSecondsRealtime(2);
        Record("final-fullscreen", true);
        UnityEngine.Debug.Log("FULLSCREEN RESULT " + (failed ? "FAIL" : "PASS"));
        UnityEngine.Application.Quit(failed ? 1 : 0);
    }

    private static void Require(bool condition, string assertion)
    {
        if (condition) return;
        failed = true;
        UnityEngine.Debug.LogError("FULLSCREEN ASSERTION " + assertion);
    }

    private static void Record(string phase, bool fullscreen)
    {
        var canvas = MenuV2.Instance.mainScreen.GetComponent<UnityEngine.Canvas>();
        var viewport = MenuV2.Instance.mainScreen.Find("Viewport") as UnityEngine.RectTransform;
        var corners = new UnityEngine.Vector3[4];
        viewport.GetWorldCorners(corners);
        var display = UnityEngine.Screen.mainWindowDisplayInfo;
        float scale = UnityEngine.Mathf.Min(UnityEngine.Screen.width / 1280f, UnityEngine.Screen.height / 720f);
        float left = (UnityEngine.Screen.width - 1280 * scale) / 2;
        float bottom = (UnityEngine.Screen.height - 720 * scale) / 2;
        Require(UnityEngine.Screen.fullScreen == fullscreen, phase + " mode");
        if (fullscreen) Require(UnityEngine.Screen.width == display.width && UnityEngine.Screen.height == display.height, phase + " native resolution");
        Require(UnityEngine.Mathf.Abs(corners[0].x - left) < 1 && UnityEngine.Mathf.Abs(corners[0].y - bottom) < 1
            && UnityEngine.Mathf.Abs(corners[2].x - (UnityEngine.Screen.width - left)) < 1
            && UnityEngine.Mathf.Abs(corners[2].y - (UnityEngine.Screen.height - bottom)) < 1, phase + " viewport fit");
        UnityEngine.Debug.Log("FULLSCREEN " + phase + " screen=" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height
            + " mode=" + UnityEngine.Screen.fullScreenMode + " native=" + display.width + "x" + display.height
            + " display=" + canvas.renderingDisplaySize + " canvas=" + canvas.pixelRect + " scale=" + canvas.scaleFactor
            + " viewport=" + corners[0] + " to " + corners[2]);
    }
}
#endif
