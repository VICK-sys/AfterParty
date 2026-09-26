using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class VanillaDialogueTextValidation
{
    public static void RunAndPlay()
    {
        Run();
        if (Environment.GetEnvironmentVariable("UNITY_PARTY_DIALOGUE_TEXT_MIX") == "1")
            VanillaMixPresentationValidation.Begin();
        else
            VanillaCampaignPresentationValidation.Begin();
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
        string directory = Environment.GetEnvironmentVariable("UNITY_PARTY_DIALOGUE_TEXT_PATH");
        string output = Path.Combine(directory, "unity");
        Directory.CreateDirectory(output);
        var cases = JArray.Parse(File.ReadAllText(Path.Combine(directory, "reference", "cases.json")));
        var results = new JArray();
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        foreach (var feature in features) feature.SetActive(false);
        try
        {
            foreach (int height in new[] { 720, 1080 })
            {
                float scale = height / 720f;
                int width = height * 16 / 9;
                var root = new GameObject("Text Validation", typeof(RectTransform), typeof(Canvas));
                var canvas = root.GetComponent<Canvas>();
                var cameraHost = new GameObject("Capture", typeof(Camera));
                var camera = cameraHost.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 360;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.farClipPlane = 2000;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(18, 24, 36, 255);
                camera.cullingMask = 1 << 31;
                camera.GetUniversalAdditionalCameraData().SetRenderer(0);
                var target = new RenderTexture(width, height, 24);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1000;
                canvas.scaleFactor = scale;
                var menu = root.AddComponent<VanillaMainMenu>();
                menu.enabled = false;
                menu.background = Rect("Background", 0, 0, 1280, 720);
                typeof(VanillaMainMenu).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
                var label = root.transform.Find("Build Version").GetComponent<RawImage>();
                Capture("label");
                label.gameObject.SetActive(false);
                var baselineLabel = Rect("Baseline Label", 12, 696, 100, 16).gameObject.AddComponent<Text>();
                baselineLabel.font = Resources.Load<Font>("VanillaFreeplay/vcr");
                baselineLabel.fontSize = 12;
                baselineLabel.alignment = TextAnchor.MiddleLeft;
                baselineLabel.text = "Made in Unity";
                baselineLabel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1, -1);
                Capture("label-before");
                baselineLabel.gameObject.SetActive(false);
                var text = Rect("Dialogue", 205, 470, 800, 200).gameObject.AddComponent<VanillaDialogueText>();
                text.font = Resources.Load<Font>("FunkinHud/Pixel/dialogue");
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                var baseline = Rect("Baseline Dialogue", 205, 470, 800, 200).gameObject.AddComponent<Text>();
                baseline.font = text.font;
                baseline.fontSize = 32;
                baseline.horizontalOverflow = HorizontalWrapMode.Wrap;
                baseline.verticalOverflow = VerticalWrapMode.Overflow;
                var shadow = baseline.gameObject.AddComponent<Shadow>();
                baseline.gameObject.SetActive(false);
                foreach (JObject item in cases)
                {
                    string id = (string)item["id"];
                    ColorUtility.TryParseHtmlString((string)item["color"], out Color color);
                    ColorUtility.TryParseHtmlString((string)item["shadowColor"], out Color shadowColor);
                    text.rectTransform.sizeDelta = new Vector2((int)item["width"], 200);
                    text.Configure(color, shadowColor, (float)item["shadowWidth"], 32);
                    string wrapped = text.Prepare((string)item["text"]);
                    text.text = "";
                    Canvas.ForceUpdateCanvases();
                    text.text = wrapped.Substring(0, Math.Min((int?)item["visible"] ?? wrapped.Length, wrapped.Length));
                    Capture(id);
                    results.Add(new JObject { ["id"] = id, ["height"] = height, ["wrapped"] = wrapped,
                        ["shown"] = text.text, ["wrapMatches"] = wrapped == (string)item["wrapped"],
                        ["textureWidth"] = text.mainTexture.width, ["textureHeight"] = text.mainTexture.height });
                    if (id == "roses-pico-censored-0" || id == "thorns-2")
                    {
                        text.gameObject.SetActive(false);
                        baseline.gameObject.SetActive(true);
                        baseline.rectTransform.sizeDelta = text.rectTransform.sizeDelta;
                        baseline.color = color;
                        baseline.text = (string)item["text"];
                        shadow.effectColor = shadowColor;
                        shadow.effectDistance = new Vector2((float)item["shadowWidth"], -(float)item["shadowWidth"]);
                        Capture(id + "-before");
                        baseline.gameObject.SetActive(false);
                        text.gameObject.SetActive(true);
                    }
                }
                text.text = "";
                Capture("blank");
                text.text = "Control";
                text.rectTransform.anchoredPosition += new Vector2(1, 0);
                Capture("shifted-control");
                Object.DestroyImmediate(root);
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraHost);
                Object.DestroyImmediate(target);

                RectTransform Rect(string name, float x, float y, float w, float h)
                {
                    var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                    rect.SetParent(root.transform, false);
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(x, -y);
                    rect.sizeDelta = new Vector2(w, h);
                    return rect;
                }

                void Capture(string name)
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    Canvas.ForceUpdateCanvases();
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.Combine(output, name + "-" + height + "p.png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
            }
            File.WriteAllText(Path.Combine(output, "cases.json"), results.ToString());
            if (results.OfType<JObject>().Any(item => !(bool)item["wrapMatches"]))
                throw new InvalidOperationException("Dialogue wrapping differs from the source reference.");
            Debug.Log("DIALOGUE TEXT VALIDATION PASSED: " + results.Count);
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
        }
    }
}
