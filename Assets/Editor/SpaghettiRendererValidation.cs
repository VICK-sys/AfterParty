using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class SpaghettiRendererValidation
{
    public static void Capture()
    {
        string reference = Environment.GetEnvironmentVariable("UNITY_PARTY_SPAGHETTI_REFERENCE_PATH");
        string output = Path.Combine(reference, "Unity");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "MouthOnTop"));
        string root = Path.Combine(Application.streamingAssetsPath, "Bundles/SpaghettiAssets");
        var host = new GameObject("Spaghetti reference camera");
        var camera = host.AddComponent<Camera>();
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        camera.orthographic = true;
        camera.orthographicSize = 3.6f / .65f;
        camera.transform.position = new Vector3(6.4f, -3.6f, -10);
        camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(136, 136, 136, 255);
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        try
        {
            foreach (var feature in features) feature.SetActive(false);
            camera.targetTexture = target;
            foreach (JToken sample in JArray.Parse(File.ReadAllText(Path.Combine(reference, "metrics.json"))))
            {
                string name = (string)sample["name"];
                string animation = (string)sample["animation"];
                int mouth = (int)sample["mouth"];
                string file = name + "-" + animation + (sample["poseFrame"]?.Type == JTokenType.Integer ? "-frame" + sample["poseFrame"] : "") + "-" + mouth + ".png";
                var bodyObject = new GameObject(name);
                bodyObject.layer = 31;
                var body = bodyObject.AddComponent<VanillaWeek2Graphic>();
                var lipObject = new GameObject("Lip");
                lipObject.layer = 31;
                lipObject.transform.SetParent(bodyObject.transform, false);
                var lip = lipObject.AddComponent<VanillaWeek2Graphic>();
                try
                {
                    string folder = Path.Combine(root, "characters/sserafim-" + name);
                    body.Load(folder, 0);
                    body.Position = new Vector3((float)sample["x"] / 100, -(float)sample["y"] / 100);
                    body.Play(animation);
                    body.FrozenFrame = (int)sample["frame"];
                    body.Advance(0, camera.transform.position, 0);
                    lip.Load(Path.Combine(root, "effects/sserafim-lipsync" + (name == "yunjin" ? "-yunjin" : "")), 1);
                    lip.SetAnimationFrame(mouth);
                    var lips = JObject.Parse(File.ReadAllText(Path.Combine(folder, "lipsync.json")));
                    JToken pose = lips["poses"][animation];
                    Vector2 offset = pose == null ? Vector2.zero : new Vector2((float)pose["offset"][0], (float)pose["offset"][1]);
                    if (body.Attachments?.First is JToken attachment)
                        lip.VertexTransform = VanillaCampaignStage.SpaghettiLipTransform(body, lip, attachment, offset, (float?)pose?["angle"] ?? 0, (bool)lips["flipX"]);
                    else lip.gameObject.SetActive(false);
                    lip.Advance(0, Vector3.zero, 0);
                    VanillaWeek2Graphic foreground = null;
                    if (name == "yunjin")
                    {
                        var foregroundObject = new GameObject("Foreground");
                        foregroundObject.layer = 31;
                        foregroundObject.transform.SetParent(bodyObject.transform, false);
                        foreground = foregroundObject.AddComponent<VanillaWeek2Graphic>();
                        foreground.Load(Path.Combine(folder, "foreground"), 2);
                        foreground.ApplyAnimationOffsets = false;
                        foreground.FrozenFrame = body.Frame;
                        foreground.Advance(0, Vector3.zero, 0);
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
                    if (foreground != null)
                    {
                        lip.GetComponent<MeshRenderer>().sortingOrder = 3;
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(output, "MouthOnTop", file), image.EncodeToPNG());
                    }
                }
                finally { Object.DestroyImmediate(bodyObject); }
            }
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
            RenderTexture.active = previous;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
        Debug.Log("SPAGHETTI REFERENCE CAPTURED: character and mouth poses with foreground overlap controls.");
    }
}
