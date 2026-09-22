using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class VanillaAtlasSeamValidation
{
    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("UNITY_PARTY_ATLAS_SEAM_PATH");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set UNITY_PARTY_ATLAS_SEAM_PATH.");
        Directory.CreateDirectory(output);
        Shader shader = Resources.Load<Shader>("VanillaSongs/Week2Graphic");
        Require(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Atlas shader did not compile.");
        var results = new JArray();
        foreach (bool rotated in new[] { false, true })
            foreach (bool pixel in new[] { false, true })
                CheckEdges(CreateFixture(output, rotated, pixel), pixel, results);
        CaptureCharacters(output, results);
        File.WriteAllText(Path.Combine(output, "results.json"), results.ToString());
        UnityEngine.Debug.Log("ATLAS SEAM VALIDATION PASSED: 36 edge comparisons, unchanged visible pixels and alpha, replacement textures, point filtering, and unpadded controls.");
    }

    private static string CreateFixture(string output, bool rotated, bool pixel)
    {
        string directory = Path.Combine(output, "fixture-" + rotated + "-" + pixel);
        Directory.CreateDirectory(directory);
        var pixels = new Color32[64 * 16];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                pixels[y * 64 + x] = new Color32(190, 90, 45, 255);
                pixels[y * 64 + x + 16] = new Color32(190, 90, 45, 255);
                byte alpha = (byte)(x >= y ? 255 : x == y - 1 ? 128 : 0);
                pixels[y * 64 + x + 48] = alpha > 0 ? new Color32(190, 90, 45, alpha) : new Color32();
            }
        SaveImage(pixels, 64, 16, Path.Combine(directory, "atlas.png"));
        var quads = new JArray();
        for (int tile = 0; tile < 2; tile++)
            quads.Add(new JObject { ["xy"] = new JArray(0, 0, 16, 0, 16, 16, 0, 16),
                ["rect"] = new JArray(tile * 48, 0, 16, 16), ["image"] = "atlas.png", ["rotated"] = rotated, ["alpha"] = 1 });
        var data = new JObject { ["bounds"] = new JArray(0, 0, 16, 16), ["pixel"] = pixel,
            ["frames"] = new JArray { quads }, ["animations"] = new JObject { ["idle"] = new JObject {
                ["frames"] = new JArray(0), ["fps"] = 24, ["loop"] = true, ["offset"] = new JArray(0, 0) } } };
        File.WriteAllText(Path.Combine(directory, "graphic.json"), data.ToString());
        return directory;
    }

    private static void CheckEdges(string directory, bool pixel, JArray results)
    {
        var obj = new GameObject("Atlas edge validation");
        Texture2D raw = null;
        Mesh reference = null;
        try
        {
            var graphic = obj.AddComponent<VanillaWeek2Graphic>();
            graphic.Load(directory, 0);
            graphic.Advance(0, Vector3.zero, 0);
            var material = obj.GetComponent<MeshRenderer>().sharedMaterial;
            var padded = (Texture2D)material.mainTexture;
            raw = ReadRaw(Path.Combine(directory, "atlas.png"), padded.filterMode);
            Color32[] original = raw.GetPixels32();
            Color32[] prepared = padded.GetPixels32();
            for (int i = 0; i < original.Length; i++)
            {
                Require(original[i].a == prepared[i].a, "Atlas padding changed alpha.");
                if (pixel || original[i].a != 0) Require(original[i].Equals(prepared[i]), "Atlas padding changed visible pixels or pixel art.");
            }
            var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            reference = Object.Instantiate(mesh);
            reference.triangles = new[] { 0, 1, 2, 2, 3, 0 };
            var rect = new Rect(-.32f, -.32f, .64f, .64f);
            foreach (float angle in new[] { 0f, 17f, -31f })
                foreach (float scale in new[] { .73f, 1f, 1.67f })
                {
                    Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(.0137f, .0163f), Quaternion.Euler(0, 0, angle), Vector3.one * scale)
                        * Matrix4x4.Translate(new Vector3(-.08f, .08f));
                    material.mainTexture = padded;
                    Color32[] expected = Render(reference, material, matrix, rect, 256, 256);
                    Color32[] actual = Render(mesh, material, matrix, rect, 256, 256);
                    material.mainTexture = raw;
                    Color32[] broken = Render(mesh, material, matrix, rect, 256, 256);
                    int error = Difference(actual, expected);
                    int controlError = Difference(broken, expected);
                    Require(error <= 1, "Transparent overlap has a dark seam: " + error);
                    Require(pixel ? controlError <= 1 : controlError > 20, "Unpadded edge control failed: " + controlError);
                    results.Add(new JObject { ["fixture"] = Path.GetFileName(directory), ["angle"] = angle, ["scale"] = scale,
                        ["maximumError"] = error, ["controlError"] = controlError });
                }
            graphic.ReplaceTexture(Path.Combine(directory, "atlas.png"));
            Color32[] replaced = ((Texture2D)material.mainTexture).GetPixels32();
            Require(prepared.SequenceEqual(replaced), "Replacement texture skipped edge preparation.");
        }
        finally
        {
            Object.DestroyImmediate(reference);
            Object.DestroyImmediate(raw);
            Object.DestroyImmediate(obj);
        }
    }

    private static void CaptureCharacters(string output, JArray results)
    {
        string bundleRoot = Environment.GetEnvironmentVariable("UNITY_PARTY_ATLAS_SEAM_BUNDLES")
            ?? Path.Combine(Application.streamingAssetsPath, "Bundles");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (string path in new[] { "Week2Assets/characters/gf", "Week2Assets/characters/gf-dark", "Week8Assets/characters/nene" })
        {
            var obj = new GameObject("Atlas seam " + path);
            Texture2D raw = null;
            try
            {
                string directory = Path.Combine(bundleRoot, path);
                var graphic = obj.AddComponent<VanillaWeek2Graphic>();
                var timer = Stopwatch.StartNew();
                graphic.Load(directory, 0);
                timer.Stop();
                results.Add(new JObject { ["character"] = path, ["loadMilliseconds"] = timer.Elapsed.TotalMilliseconds });
                graphic.ApplyAnimationOffsets = false;
                graphic.Play("danceLeft");
                var material = (Material)typeof(VanillaWeek2Graphic).GetField("material", flags).GetValue(graphic);
                Texture padded = material.mainTexture;
                raw = ReadRaw(Path.Combine(directory, "spritemap1.png"), padded.filterMode);
                foreach (bool grouped in new[] { false, true })
                    foreach (int frame in new[] { 0, 3 })
                    {
                        graphic.CompositeAlpha = grouped;
                        graphic.Alpha = grouped ? .6f : 1;
                        graphic.SetAnimationFrame(frame);
                        Color32[] original = null;
                        foreach (bool before in new[] { true, false })
                        {
                            material.mainTexture = before ? raw : padded;
                            typeof(VanillaWeek2Graphic).GetField("compositeFrame", flags).SetValue(graphic, -1);
                            graphic.Advance(0, Vector3.zero, 0);
                            var rect = new Rect(-.2f, -graphic.Size.y / 100 - .2f, graphic.Size.x / 100 + .4f, graphic.Size.y / 100 + .4f);
                            int width = Mathf.CeilToInt(rect.width * 140);
                            int height = Mathf.CeilToInt(rect.height * 140);
                            Color32[] rendered = Render(obj.GetComponent<MeshFilter>().sharedMesh, obj.GetComponent<MeshRenderer>().sharedMaterial,
                                Matrix4x4.identity, rect, width, height);
                            string name = Path.GetFileName(path) + "-" + frame + (grouped ? "-composite" : "-direct") + (before ? "-before" : "-after") + ".png";
                            SaveImage(rendered, width, height, Path.Combine(output, name), true);
                            if (before) original = rendered;
                            else results.Add(new JObject { ["capture"] = name, ["maximumChange"] = Difference(original, rendered) });
                        }
                    }
            }
            finally
            {
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(obj);
            }
        }
    }

    private static Texture2D ReadRaw(string path, FilterMode mode)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(path));
        texture.filterMode = mode;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private static void SaveImage(Color32[] pixels, int width, int height, string path, bool flip = false)
    {
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            if (flip)
            {
                var flipped = new Color32[pixels.Length];
                for (int y = 0; y < height; y++) Array.Copy(pixels, y * width, flipped, (height - y - 1) * width, width);
                pixels = flipped;
            }
            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(image); }
    }

    private static Color32[] Render(Mesh mesh, Material material, Matrix4x4 matrix, Rect rect, int width, int height)
    {
        var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var commands = new CommandBuffer();
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(false, true, new Color(.08f, .08f, .08f, 1));
            commands.SetViewProjectionMatrices(Matrix4x4.identity,
                GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(rect.xMin, rect.xMax, rect.yMin, rect.yMax, -1, 1), true));
            commands.DrawMesh(mesh, matrix, material);
            Graphics.ExecuteCommandBuffer(commands);
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            return image.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            commands.Release();
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }

    private static int Difference(Color32[] first, Color32[] second)
    {
        int maximum = 0;
        for (int i = 0; i < first.Length; i++)
            maximum = Mathf.Max(maximum, Mathf.Abs(first[i].r - second[i].r), Mathf.Abs(first[i].g - second[i].g), Mathf.Abs(first[i].b - second[i].b));
        return maximum;
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
