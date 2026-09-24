using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class VanillaMenuWidthValidation
{
    private static int assertions;
    private static readonly string Output = "Builds/MenuWidthValidation";

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            var menu = Object.FindFirstObjectByType<MenuV2>();
            var main = menu.vanillaMenu;
            main.gameObject.SetActive(true);
            Check(main, main.ApplyLayout, "main", "Background");
            var story = Build<VanillaStoryMenu>("menu", menu);
            typeof(VanillaStoryMenu).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(story, new object[] { 10f });
            Check(story, story.ApplyLayout, "story", "Content/Level Background");
            var options = Build<VanillaOptionsMenu>("menu", menu);
            options.ShowPage(VanillaOptionsMenu.Page.Options);
            Check(options, options.ApplyLayout, "options", "Background");
            var characters = Build<VanillaCharacterSelect>("original", "bf");
            Check(characters, characters.ApplyLayout, "characters", null);
            Debug.Log("MENU WIDTH VALIDATION PASSED: " + assertions + " assertions.");
            File.WriteAllText(Path.Combine(Output, "result.txt"), assertions + " assertions passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static T Build<T>(string field, object value) where T : MonoBehaviour
    {
        var host = new GameObject(typeof(T).Name, typeof(RectTransform));
        host.SetActive(false);
        var component = host.AddComponent<T>();
        typeof(T).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
        typeof(T).GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
        host.SetActive(true);
        return component;
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Check(MonoBehaviour screen, Action<float> layout, string name, string backgroundPath)
    {
        var viewport = (RectTransform)screen.transform.Find("Viewport");
        foreach (float width in new[] { 1280f, 1440f, 1600f, 2200f, 960f, 1600f, 1280f })
        {
            layout(width);
            Canvas.ForceUpdateCanvases();
            float expected = Mathf.Clamp(width, 1280, 1600);
            Require(viewport.rect.width == expected && viewport.rect.height == 720, name + " viewport dimensions");
            var content = viewport.Find(name == "options" ? "Page" : "Content") as RectTransform;
            if (content != null)
            {
                Vector3 center = viewport.InverseTransformPoint(content.TransformPoint(content.rect.center));
                Require(Mathf.Abs(center.x) < .01f, name + " content moved off center");
                Require(content.localScale == Vector3.one, name + " controls scaled");
            }
            if (backgroundPath != null)
            {
                var background = (RectTransform)viewport.Find(backgroundPath);
                var corners = new Vector3[4];
                background.GetWorldCorners(corners);
                float left = viewport.InverseTransformPoint(corners[0]).x;
                float right = viewport.InverseTransformPoint(corners[2]).x;
                Require(left <= viewport.rect.xMin + .01f && right >= viewport.rect.xMax - .01f, name + " exposed background edges");
            }
        }
        Capture(screen, layout, name, 1280);
        Capture(screen, layout, name, 1600);
        Capture(screen, layout, name, 1600, true);
        screen.gameObject.SetActive(false);
    }

    private static void Capture(MonoBehaviour screen, Action<float> layout, string name, int width, bool blank = false)
    {
        var canvas = screen.GetComponent<Canvas>();
        var scaler = screen.GetComponent<CanvasScaler>();
        var transforms = screen.GetComponentsInChildren<Transform>(true);
        int[] layers = transforms.Select(item => item.gameObject.layer).ToArray();
        var host = new GameObject("Width Capture", typeof(Camera));
        var camera = host.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(width, 720, 24);
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        try
        {
            foreach (var child in transforms) child.gameObject.layer = 31;
            foreach (var feature in features) feature.SetActive(false);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1000;
            scaler.enabled = false;
            canvas.scaleFactor = 1;
            layout(width);
            screen.transform.Find("Viewport").gameObject.SetActive(!blank);
            if (blank) canvas.enabled = false;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, 720, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, 720), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            float light = image.GetPixels().Average(color => color.r + color.g + color.b);
            Require(blank ? light < .001f : light > .01f, name + " capture control");
            File.WriteAllBytes(Path.Combine(Output, name + "-" + width + (blank ? "-blank" : "") + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            canvas.enabled = true;
            screen.transform.Find("Viewport").gameObject.SetActive(true);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            scaler.enabled = true;
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            foreach (var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }
}
