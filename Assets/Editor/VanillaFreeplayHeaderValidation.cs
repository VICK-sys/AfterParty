using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class VanillaFreeplayHeaderValidation
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run header validation in an isolated batch editor.");
        string output = Environment.GetEnvironmentVariable("UNITY_PARTY_HEADER_TEST_PATH");
        Directory.CreateDirectory(output);
        var host = new GameObject("Header", typeof(RectTransform), typeof(Canvas));
        var canvas = host.GetComponent<Canvas>();
        var cameraHost = new GameObject("Capture", typeof(Camera));
        var camera = cameraHost.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1000;
        canvas.scaleFactor = 1;
        var freeplay = host.AddComponent<VanillaFreeplay>();
        freeplay.enabled = false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(VanillaFreeplay).GetField("headerRoot", flags).SetValue(freeplay, host.GetComponent<RectTransform>());
        var build = typeof(VanillaFreeplay).GetMethod("HeaderImage", flags);
        build.Invoke(freeplay, new object[] { "Heading", "heading", 8f, 8f });
        build.Invoke(freeplay, new object[] { "OST", "ost", 8f, 8f });
        var hintHost = new GameObject("Hint", typeof(RectTransform), typeof(VanillaFreeplayHeaderText));
        var rect = hintHost.GetComponent<RectTransform>();
        rect.SetParent(host.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(-40, -18);
        rect.sizeDelta = new Vector2(1264, 27);
        var hint = hintHost.GetComponent<VanillaFreeplayHeaderText>();
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        foreach (var feature in features) feature.SetActive(false);
        foreach (var child in host.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
        foreach (string key in new[] { "TAB", "SPACE", "intro", "blank" })
        {
            hint.Text = "Press [ " + (key == "intro" ? "TAB" : key) + " ] to change characters";
            typeof(VanillaFreeplay).GetMethod("SetHeaderStroke", flags).Invoke(freeplay, new object[] { key == "intro" });
            hint.color = new Color32(95, 95, 95, 153);
            camera.cullingMask = key == "blank" ? 0 : 1 << 31;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(output, key + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        foreach (var feature in features) feature.SetActive(true);
        Debug.Log("FREEPLAY HEADER CAPTURE PASSED");
        EditorApplication.Exit(0);
    }
}
