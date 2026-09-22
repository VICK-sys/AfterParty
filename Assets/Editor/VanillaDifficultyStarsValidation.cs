using System;
using System.Collections.Generic;
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

public static class VanillaDifficultyStarsValidation
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static string Geometry(VanillaFreeplayAnimate graphic)
    {
        using (var vertices = new VertexHelper())
        {
            typeof(VanillaFreeplayAnimate).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
                .Invoke(graphic, new object[] { vertices });
            var mesh = new Mesh();
            vertices.FillMesh(mesh);
            string result = string.Join("|", mesh.vertices.Select(v => v.ToString("R")))
                + string.Join("|", mesh.uv.Select(v => v.ToString("R")));
            Object.DestroyImmediate(mesh);
            return result;
        }
    }

    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("UNITY_PARTY_STARS_TEST_PATH");
        Directory.CreateDirectory(output);
        var host = new GameObject("Stars canvas", typeof(RectTransform), typeof(Canvas));
        var canvas = host.GetComponent<Canvas>();
        var cameraHost = new GameObject("Capture", typeof(Camera));
        var camera = cameraHost.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 360;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.farClipPlane = 2000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .12f, .12f, 1);
        camera.cullingMask = 1 << 31;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1000;
        canvas.scaleFactor = 1;
        var group = new GameObject("Difficulty Stars", typeof(RectTransform), typeof(VanillaFreeplayDifficultyStars));
        var rect = group.GetComponent<RectTransform>();
        rect.SetParent(host.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(450, -309);
        rect.sizeDelta = new Vector2(1280, 720);
        var controller = group.GetComponent<VanillaFreeplayDifficultyStars>();
        controller.Initialize();
        controller.enabled = false;
        var flames = controller.GetComponentsInChildren<VanillaFreeplaySprite>(true);
        var initial = new string[16];
        foreach (var child in host.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var features = AssetDatabase.FindAssets("t:UniversalRendererData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data => data.rendererFeatures).Where(feature => feature != null && feature.isActive).Distinct().ToArray();
        foreach (var feature in features) feature.SetActive(false);
        try
        {
            for (int rating = 0; rating <= 15; rating++)
            {
                controller.SetRating(rating);
                initial[rating] = Geometry(controller.Stars);
                bool moved = false;
                for (int tick = 0; tick < 240; tick++)
                {
                    controller.Tick(1f / 24);
                    int frame = controller.Stars.CurrentFrame;
                    Require(rating == 0 ? frame == 1500 : frame >= (rating - 1) * 100 && frame < rating * 100,
                        "Star playback changed rating " + rating);
                    moved |= Geometry(controller.Stars) != initial[rating];
                }
                Require(rating == 0 ? !moved : moved, "Star mesh did not animate at rating " + rating);
                Require(flames.Count(f => f.gameObject.activeSelf) == Math.Max(0, rating - 10), "Flame count differs from rating.");
            }
            foreach (int rating in new[] { 5, 12, 3, 15, 0, 1, 15, 5 })
            {
                controller.SetRating(rating);
                Require(Geometry(controller.Stars) == initial[rating], "Song changes retained another rating's stars.");
                controller.Tick(2);
                Require(flames.Count(f => f.gameObject.activeSelf) == Math.Max(0, rating - 10), "Song changes retained stale flames.");
            }
            controller.SetRating(5);
            var resting = Capture(camera, target, output, "stars-rest");
            controller.Tick(43f / 24);
            var bouncing = Capture(camera, target, output, "stars-bounce");
            Require(Different(resting, bouncing), "Rendered stars stayed static.");
            Require(!Different(bouncing, Capture(camera, target, output, "frozen-control")), "Frozen control changed pixels.");
            controller.SetRating(0);
            controller.SetRating(15);
            Require(flames.Count(f => f.gameObject.activeSelf) == 1, "Flames did not ignite in sequence.");
            controller.Tick(.24f);
            Require(flames.Count(f => f.gameObject.activeSelf) == 1, "Second flame ignited early.");
            controller.Tick(.02f);
            Require(flames.Count(f => f.gameObject.activeSelf) == 2, "Second flame did not ignite.");
            controller.SetRating(3);
            controller.Tick(2);
            Require(flames.All(f => !f.gameObject.activeSelf), "A canceled ignition returned after a song change.");
            controller.SetRating(15);
            controller.Tick(1.1f);
            var burning = Capture(camera, target, output, "blue-stars-fire");
            controller.Tick(.15f);
            Require(Different(burning, Capture(camera, target, output, "blue-stars-fire-next")), "Fire did not animate.");
            foreach (var flame in flames) flame.gameObject.SetActive(false);
            Require(Different(burning, Capture(camera, target, output, "no-fire-control")), "Missing fire control passed.");
            controller.SetRating(0);
            controller.SetRating(11);
            controller.Tick((flames[0].FrameCount + .1f) / flames[0].fps);
            Require(flames[0].FrameIndex == 2, "Fire loop replayed its ignition frames.");
            CheckSelection(controller, initial);
            Debug.Log("DIFFICULTY STARS PASSED: 16 ratings, 240 ticks each, eight rating changes, Freeplay song and difficulty selection, rendered bounce and flames, staggered ignition, canceled ignition, loop frame 2, frozen and missing-fire controls.");
        }
        finally
        {
            foreach (var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(cameraHost);
            Object.DestroyImmediate(target);
        }
    }

    private static void CheckSelection(VanillaFreeplayDifficultyStars controller, string[] initial)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("Selection probe", typeof(RectTransform));
        var freeplay = host.AddComponent<VanillaFreeplay>();
        freeplay.enabled = false;
        var albumHost = new GameObject("Album", typeof(RectTransform));
        albumHost.transform.SetParent(host.transform, false);
        var album = albumHost.AddComponent<VanillaFreeplayAnimate>();
        album.Initialize("freeplay/albumRoll/freeplayAlbum", false);
        var titleHost = new GameObject("Title", typeof(RectTransform));
        titleHost.transform.SetParent(albumHost.transform, false);
        var title = titleHost.AddComponent<VanillaFreeplaySprite>();
        var songs = new List<VanillaFreeplaySong>();
        foreach (int rating in new[] { 5, 12, 3 })
        {
            var song = new VanillaFreeplaySong();
            foreach (string difficulty in new[] { "Normal", "Nightmare" })
                song.details[difficulty] = new JObject { ["playData"] = new JObject
                {
                    ["album"] = "volume1",
                    ["ratings"] = new JObject { [difficulty.ToLowerInvariant()] = difficulty == "Normal" ? rating : 15 }
                } };
            songs.Add(song);
        }
        typeof(VanillaFreeplay).GetField("filtered", flags).SetValue(freeplay, songs);
        typeof(VanillaFreeplay).GetField("albumRoot", flags).SetValue(freeplay, albumHost.GetComponent<RectTransform>());
        typeof(VanillaFreeplay).GetField("album", flags).SetValue(freeplay, album);
        typeof(VanillaFreeplay).GetField("albumTitle", flags).SetValue(freeplay, title);
        typeof(VanillaFreeplay).GetField("stars", flags).SetValue(freeplay, controller);
        try
        {
            foreach (string difficulty in new[] { "Normal", "Nightmare", "Normal" })
                foreach (int selection in new[] { 1, 2, 3, 1, 0 })
                {
                    typeof(VanillaFreeplay).GetProperty("SelectedIndex").SetValue(freeplay, selection);
                    typeof(VanillaFreeplay).GetProperty("Difficulty").SetValue(freeplay, difficulty);
                    typeof(VanillaFreeplay).GetMethod("RefreshAlbum", flags).Invoke(freeplay, null);
                    int expected = selection == 0 ? 0 : songs[selection - 1].Rating(difficulty);
                    Require(controller.Rating == expected && Geometry(controller.Stars) == initial[expected],
                        "Freeplay selection did not replace the previous song rating.");
                    Require(albumHost.activeSelf == (selection != 0), "Random selection retained album stars.");
                    controller.Tick(7);
                }
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static Color32[] Capture(Camera camera, RenderTexture target, string output, string name)
    {
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
        Color32[] pixels = image.GetPixels32();
        Object.DestroyImmediate(image);
        return pixels;
    }

    private static bool Different(Color32[] first, Color32[] second) => first.Where((pixel, i) => !pixel.Equals(second[i])).Take(21).Count() > 20;
}
