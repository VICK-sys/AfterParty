using System;
using System.IO;
using System.Linq;
using QFSW.MOP2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class UpgradeValidation
{
    public static void Run()
    {
        ValidatePool();
        ValidateRendering();
        Debug.Log("UPGRADE VALIDATION PASSED: object reuse, component identity, CRT rendering and control.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ValidatePool()
    {
        var template = new GameObject("Pool validation template");
        var pool = ObjectPool.CreateAndInitialize(template, 1);
        try
        {
            GameObject first = pool.GetObject();
            GameObject second = pool.GetObject();
            Require(first != second, "Active objects must have distinct identities.");
            Require(pool.GetObjectComponent<Transform>(first) == first.transform, "First component cache returned the wrong object.");
            Require(pool.GetObjectComponent<Transform>(second) == second.transform, "Second component cache returned the wrong object.");
            pool.Release(first);
            Require(!first.activeSelf && second.activeSelf, "Release affected the wrong object.");
            GameObject reused = pool.GetObject(new Vector3(3, 4, 5));
            Require(reused == first && reused.activeSelf, "Pool did not reuse the released object.");
            Require(reused.transform.position == new Vector3(3, 4, 5), "Reused object position was not updated.");
            Require(pool.GetObjectComponent<Transform>(reused) == first.transform, "Component identity changed after reuse.");
            pool.ReleaseAll();
            Require(!first.activeSelf && !second.activeSelf, "ReleaseAll left an object active.");
        }
        finally
        {
            Object.DestroyImmediate(pool.ObjectParent.gameObject);
            Object.DestroyImmediate(pool);
            Object.DestroyImmediate(template);
        }
    }

    private static void ValidateRendering()
    {
        Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Rendering validation requires a graphics device.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/UniversalRenderPipelineAsset_Renderer.asset");
        var feature = renderer.rendererFeatures.OfType<Blit>().Single();
        Blit.BlitSettings original = feature.settings;
        bool active = feature.isActive;
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/Retro-CRT/Retro/Retro.mat"));
        var cameraObject = new GameObject("CRT validation camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.green;
        camera.cullingMask = 0;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(256, 256, 24);
        var source = new RenderTexture(256, 256, 0);
        var pattern = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        var colors = new Color[256 * 256];
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
                colors[y * 256 + x] = ((x / 32 + y / 32) % 2 == 0) ? Color.blue : Color.yellow;
        pattern.SetPixels(colors);
        pattern.Apply();
        Graphics.Blit(pattern, source);
        try
        {
            feature.SetActive(false);
            renderer.SetDirty();
            Color[] control = Render(camera, target, "crt-control.png");
            Require(control.Average(c => c.g) > 0.9f && control.Average(c => c.r + c.b) < 0.1f, "Control camera did not render its green background.");
            feature.settings = new Blit.BlitSettings
            {
                Event = RenderPassEvent.AfterRenderingTransparents,
                blitMaterial = material,
                srcType = Blit.Target.RenderTextureObject,
                srcTextureObject = source,
                dstType = Blit.Target.CameraColor
            };
            feature.SetActive(true);
            renderer.SetDirty();
            Color[] external = Render(camera, target, "crt-texture.png");
            Require(Difference(control, external) > 0.2f, "CRT pass did not replace the camera image.");
            Require(external.Count(c => c.b > c.r + 0.1f) > 1000, "CRT output lost the blue source regions.");
            Require(external.Count(c => c.r > 0.2f && c.g > 0.2f && c.b < 0.1f) > 1000, "CRT output lost the yellow source regions.");
            feature.settings.srcType = Blit.Target.CameraColor;
            renderer.SetDirty();
            Color[] cameraSource = Render(camera, target, "crt-camera.png");
            Require(cameraSource.Average(c => c.g) > cameraSource.Average(c => c.r + c.b), "CRT camera source did not preserve green content.");
            Require(Difference(control, cameraSource) > 0.01f, "CRT camera pass had no visible effect.");
        }
        finally
        {
            feature.settings = original;
            feature.SetActive(active);
            renderer.SetDirty();
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(pattern);
            RenderTexture.active = null;
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    private static Color[] Render(Camera camera, RenderTexture target, string fileName)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        Color[] result = image.GetPixels();
        string directory = Environment.GetEnvironmentVariable("UNITY_PARTY_VALIDATION_PATH");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), image.EncodeToPNG());
        }
        Object.DestroyImmediate(image);
        return result;
    }

    private static float Difference(Color[] left, Color[] right)
    {
        return left.Zip(right, (a, b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)).Average();
    }
}
