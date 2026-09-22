using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

public static class SongLoadingMemoryValidation
{
    private static IEnumerator routine;
    private static AsyncOperation pending;
    private static double deadline;
    private static readonly JArray Results = new JArray();

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run memory validation in a batch editor.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CheckAtlasAllocation();
        VanillaAtlasSeamValidation.Run();
        routine = CheckMenuRelease();
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Memory validation timed out.");
            if (pending != null && !pending.isDone) return;
            pending = null;
            if (routine.MoveNext()) pending = routine.Current as AsyncOperation;
            else
            {
                string directory = Environment.GetEnvironmentVariable("UNITY_PARTY_ATLAS_SEAM_PATH");
                File.WriteAllText(Path.Combine(directory, "memory-results.json"), Results.ToString());
                Debug.Log("SONG MEMORY VALIDATION PASSED: atlas allocation, pixel parity, cache retention control, live texture preservation, unused texture release, and reload.");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }

    private static void CheckAtlasAllocation()
    {
        string directory = Environment.GetEnvironmentVariable("UNITY_PARTY_ATLAS_SEAM_PATH");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "allocation.png");
        var source = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        var original = new Color32[1024 * 1024];
        for (int i = 0; i < original.Length; i++) original[i] = new Color32(30, 100, 180, (byte)(i % 3 == 0 ? 0 : 255));
        source.SetPixels32(original);
        source.Apply();
        File.WriteAllBytes(path, source.EncodeToPNG());
        Object.DestroyImmediate(source);
        MethodInfo loader = typeof(VanillaWeek2Graphic).GetMethod("LoadTexture", BindingFlags.NonPublic | BindingFlags.Static);
        var warm = (Texture2D)loader.Invoke(null, new object[] { path, false });
        Object.DestroyImmediate(warm);
        long start = GC.GetTotalMemory(true);
        var loaded = (Texture2D)loader.Invoke(null, new object[] { path, false });
        long allocated = GC.GetTotalMemory(false) - start;
        Require(allocated < original.Length, "Atlas load allocated a full pixel array: " + allocated);
        Color32[] prepared = loaded.GetPixels32();
        Require(prepared.Length == original.Length, "Atlas dimensions changed.");
        for (int i = 0; i < original.Length; i++)
        {
            Require(prepared[i].a == original[i].a, "Atlas alpha changed.");
            if (original[i].a != 0) Require(prepared[i].Equals(original[i]), "Visible atlas pixels changed.");
        }
        start = GC.GetTotalMemory(true);
        Color32[] control = loaded.GetPixels32();
        long controlAllocated = GC.GetTotalMemory(false) - start;
        GC.KeepAlive(control);
        Require(controlAllocated >= original.Length * 4, "Full pixel array control did not allocate: " + controlAllocated);
        Results.Add(new JObject { ["atlasManagedBytes"] = allocated, ["copyControlBytes"] = controlAllocated });
        Object.DestroyImmediate(loaded);
    }

    private static IEnumerator CheckMenuRelease()
    {
        var active = new GameObject("Active animation", typeof(RectTransform)).AddComponent<VanillaFreeplayAnimate>();
        active.Initialize("charSelect/bfChill");
        var activeId = active.mainTexture.GetEntityId();
        var unused = new GameObject("Unused animation", typeof(RectTransform)).AddComponent<VanillaFreeplayAnimate>();
        unused.Initialize("charSelect/lockedChill");
        var unusedId = unused.mainTexture.GetEntityId();
        long unusedBytes = Profiler.GetRuntimeMemorySizeLong(unused.mainTexture);
        Object.DestroyImmediate(unused.gameObject);
        unused = null;
        yield return Resources.UnloadUnusedAssets();
        Require(HasTexture(unusedId), "Cache retention control failed.");
        var release = VanillaFreeplayAnimate.ReleaseCachedAssets();
        while (release.MoveNext()) yield return release.Current;
        Require(!HasTexture(unusedId), "Unused cached texture remained loaded.");
        Require(HasTexture(activeId) && active.mainTexture.GetEntityId() == activeId, "Cache release unloaded a live texture.");
        active.SetFrame(15);
        Canvas.ForceUpdateCanvases();
        var reloaded = new GameObject("Reloaded animation", typeof(RectTransform)).AddComponent<VanillaFreeplayAnimate>();
        reloaded.Initialize("charSelect/lockedChill");
        Require(reloaded.mainTexture != null && reloaded.mainTexture.width == 8192 && reloaded.TotalFrames > 0, "Animation did not reload after eviction.");
        Results.Add(new JObject { ["releasedTextureBytes"] = unusedBytes, ["liveTexturePreserved"] = true, ["reloadPassed"] = true });
        Object.DestroyImmediate(active.gameObject);
        Object.DestroyImmediate(reloaded.gameObject);
        release = VanillaFreeplayAnimate.ReleaseCachedAssets();
        while (release.MoveNext()) yield return release.Current;
    }

    private static bool HasTexture(EntityId id) => Resources.FindObjectsOfTypeAll<Texture2D>().Any(texture => texture.GetEntityId() == id);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
