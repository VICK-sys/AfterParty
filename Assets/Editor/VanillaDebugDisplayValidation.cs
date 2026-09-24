using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class VanillaDebugDisplayValidation
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int assertions;

    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("UNITY_PARTY_DEBUG_TEST_PATH") ?? Path.GetFullPath("Builds/DebugDisplayValidation");
        Directory.CreateDirectory(output);
        var cameraObject = new GameObject("Capture Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(80, 100, 120, 255);
        camera.orthographic = true;
        camera.orthographicSize = 360;
        var canvasObject = new GameObject("Debug Probe", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var host = new GameObject("Debug Display", typeof(RectTransform));
        var rect = host.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(10, -10);
        var display = host.AddComponent<VanillaDebugDisplay>();
        if (Field(display, "panel") == null) typeof(VanillaDebugDisplay).GetMethod("Awake", Private).Invoke(display, null);
        try
        {
            Require(VanillaDebugDisplay.FormatBytes(0) == "0bytes", "Zero bytes formatting");
            Require(VanillaDebugDisplay.FormatBytes(1536) == "1.5kb", "Fractional bytes formatting");
            Require(VanillaDebugDisplay.FormatBytes(1073741824) == "1gb", "Gigabyte formatting");
            display.Tick(0, .5f, 0, 0);
            var graph = (VanillaDebugGraph)Field(display, "fpsGraph");
            graph.Clear();
            graph.Sample(1, 60);
            for (int i = 0; i < 100; i++) graph.Sample(60, 60);
            Require(graph.Average == 60 && graph.Lowest == 60, "History did not evict the old low sample");
            graph.Sample(20, 60);
            Require(Math.Abs(graph.Average - 59.6) < .001 && graph.Lowest == 20, "Low sample control did not affect statistics");
            display.Tick(1, .5f, 0, 0);
            for (int i = 1; i <= 120; i++) display.Tick(1, .5f, i / 60.0, 1f / 60);
            Require((int)Field(display, "fps") >= 59 && (int)Field(display, "fps") <= 61, "Rolling second FPS failed");
            Require((double)Field(display, "gcMemory") > 0 && (double)Field(display, "taskMemory") > 0, "Live memory counters failed");
            Set(display, "elapsed", .1);
            display.Tick(1, .5f, 5, 3);
            Require((int)Field(display, "fps") == 1, "Stall control did not expire old frames");
            Require((int)Field(display, "fpsPeak") >= 59, "FPS peak was lost after a stall");
            Set(display, "fps", 60);
            Set(display, "fpsPeak", 60);
            Set(display, "gcMemory", 134217728.0);
            Set(display, "gcPeak", 268435456.0);
            Set(display, "taskMemory", 805306368.0);
            Set(display, "taskPeak", 1073741824.0);
            Set(display, "elapsed", 0.0);
            foreach (int mode in new[] { 0, 1, 2 })
            {
                display.Tick(mode, .5f, 5, 0);
                if (mode == 0)
                {
                    Require(rect.sizeDelta == new Vector2(240, 207), "Advanced panel dimensions");
                    foreach (string name in new[] { "fpsGraph", "gcGraph", "taskGraph" }) ((VanillaDebugGraph)Field(display, name)).Clear();
                    for (int i = 0; i < 100; i++) typeof(VanillaDebugDisplay).GetMethod("RefreshDisplay", Private).Invoke(display, null);
                    Require(((VanillaDebugText)Field(display, "fpsText")).Text == "FPS: 60\nAVG FPS: 60\n1% LOW FPS: 60", "Advanced labels");
                }
                if (mode == 1)
                {
                    Require(Math.Abs(rect.sizeDelta.y - 66.3f) < .001, "Simple panel dimensions");
                    Require(((VanillaDebugText)Field(display, "info")).Text == "FPS: 60\nGC MEM: 128mb / 256mb\nTASK MEM: 768mb / 1gb", "Simple memory labels");
                    Require(!graph.gameObject.activeSelf, "Simple mode retained graphs");
                }
                if (mode == 2) Require(!host.activeSelf, "Off mode retained overlay");
                Capture(camera, Path.Combine(output, mode == 0 ? "advanced.png" : mode == 1 ? "simple.png" : "off-control.png"), 1280, 720);
            }
            display.Tick(0, 0, 5, 0);
            Require(graph.Average == 60 && graph.Lowest == 60, "Mode switch did not rebuild history");
            Capture(camera, Path.Combine(output, "transparent.png"), 1280, 720);
            Capture(camera, Path.Combine(output, "large.png"), 1920, 1080);
            File.WriteAllText(Path.Combine(output, "results.json"), "{\"assertions\":" + assertions + ",\"passed\":true}");
            Debug.Log("DEBUG DISPLAY VALIDATION PASSED: " + assertions + " assertions");
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    private static object Field(VanillaDebugDisplay display, string name) => typeof(VanillaDebugDisplay).GetField(name, Private).GetValue(display);
    private static void Set(VanillaDebugDisplay display, string name, object value) => typeof(VanillaDebugDisplay).GetField(name, Private).SetValue(display, value);
    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Capture(Camera camera, string path, int width, int height)
    {
        var target = new RenderTexture(width, height, 24);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }
    }
}

