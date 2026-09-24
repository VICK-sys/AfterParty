using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public sealed class VanillaDebugDisplay : MonoBehaviour
{
    private readonly Queue<double> times = new Queue<double>();
    private RectTransform panel;
    private VanillaDebugPanel background;
    private VanillaDebugText info, fpsText, gcText, taskText;
    private VanillaDebugGraph fpsGraph, gcGraph, taskGraph;
    private int mode = -1, fps, fpsPeak;
    private double elapsed, gcMemory, gcPeak, taskMemory, taskPeak;
    private System.Diagnostics.Process process;
    private bool taskSupported;

    private void Awake()
    {
#if UNITY_WSA && ENABLE_WINMD_SUPPORT && !UNITY_EDITOR
        taskSupported = true;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        taskSupported = ReadWindowsMemory() > 0;
#elif UNITY_STANDALONE || UNITY_EDITOR
        try
        {
            process = System.Diagnostics.Process.GetCurrentProcess();
            taskSupported = process.WorkingSet64 > 0;
        }
        catch (Exception) { taskSupported = false; }
#endif
        panel = (RectTransform)transform;
        background = Rect("Background", transform, 0, 0, 240, 207).gameObject.AddComponent<VanillaDebugPanel>();
        background.raycastTarget = false;
        info = Label("Simple Stats", 8, 8);
        fpsText = Label("FPS Stats", 8, 8);
        gcText = Label("GC Memory", 8, 90);
        taskText = Label("Task Memory", 8, 145);
        fpsGraph = Graph("FPS Graph", 57);
        gcGraph = Graph("GC Memory Graph", 112);
        taskGraph = Graph("Task Memory Graph", 167);
    }

    public void Tick(int displayMode, float opacity, double now, float delta)
    {
        if (mode != displayMode)
        {
            mode = displayMode;
            bool advanced = mode == 0;
            gameObject.SetActive(mode != 2);
            info.gameObject.SetActive(!advanced);
            fpsText.gameObject.SetActive(advanced);
            gcText.gameObject.SetActive(advanced);
            taskText.gameObject.SetActive(advanced && taskSupported);
            fpsGraph.gameObject.SetActive(advanced);
            gcGraph.gameObject.SetActive(advanced);
            taskGraph.gameObject.SetActive(advanced && taskSupported);
            float height = 201 * (advanced ? (taskSupported ? 1 : .7f) : (taskSupported ? .3f : .2f));
            panel.sizeDelta = background.rectTransform.sizeDelta = new Vector2(240, height + 6);
            if (mode != 2)
            {
                fpsGraph.Clear();
                gcGraph.Clear();
                taskGraph.Clear();
                RefreshDisplay();
            }
        }
        background.color = new Color(1, 1, 1, Mathf.Clamp01(opacity));
        if (mode == 2) return;
        times.Enqueue(now);
        while (times.Count > 0 && times.Peek() < now - 1) times.Dequeue();
        if (elapsed < .1)
        {
            elapsed += delta;
            return;
        }
        elapsed = 0;
        fps = times.Count;
        fpsPeak = Math.Max(fpsPeak, fps);
        gcMemory = GC.GetTotalMemory(false);
        gcPeak = Math.Max(gcPeak, gcMemory);
#if UNITY_WSA && ENABLE_WINMD_SUPPORT && !UNITY_EDITOR
        taskMemory = Windows.System.MemoryManager.AppMemoryUsage;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (taskSupported) taskMemory = ReadWindowsMemory();
#elif UNITY_STANDALONE || UNITY_EDITOR
        if (taskSupported)
        {
            try
            {
                process.Refresh();
                taskMemory = process.WorkingSet64;
            }
            catch (Exception) { }
        }
#endif
        taskPeak = Math.Max(taskPeak, taskMemory);
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        string gc = "GC MEM: " + FormatBytes(gcMemory) + " / " + FormatBytes(gcPeak);
        string task = "TASK MEM: " + FormatBytes(taskMemory) + " / " + FormatBytes(taskPeak);
        if (mode == 0)
        {
            fpsGraph.Sample(fps, fpsPeak);
            gcGraph.Sample(gcMemory, gcPeak);
            if (taskSupported) taskGraph.Sample(taskMemory, taskPeak);
            fpsText.Text = "FPS: " + fps + "\nAVG FPS: " + Math.Floor(fpsGraph.Average) + "\n1% LOW FPS: " + Math.Floor(fpsGraph.Lowest);
            gcText.Text = gc;
            taskText.Text = task;
        }
        else info.Text = "FPS: " + fps + "\n" + gc + (taskSupported ? "\n" + task : "");
    }

    public static string FormatBytes(double bytes)
    {
        string[] units = { "bytes", "kb", "mb", "gb", "tb", "pb" };
        int unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }
        return (Math.Floor(bytes * 100 + .5) / 100).ToString("0.##", CultureInfo.InvariantCulture) + units[unit];
    }

    private VanillaDebugText Label(string label, float x, float y)
    {
        var text = Rect(label, transform, x, y, 500, 60).gameObject.AddComponent<VanillaDebugText>();
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private VanillaDebugGraph Graph(string label, float y)
    {
        var graph = Rect(label, transform, 8, y, 216, 25).gameObject.AddComponent<VanillaDebugGraph>();
        graph.raycastTarget = false;
        return graph;
    }

    private static RectTransform Rect(string label, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private void OnDestroy()
    {
        process?.Dispose();
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCounters
    {
        public uint size, pageFaultCount;
        public UIntPtr peakWorkingSet, workingSet, quotaPeakPagedPool, quotaPagedPool;
        public UIntPtr quotaPeakNonPagedPool, quotaNonPagedPool, pagefile, peakPagefile;
    }

    [DllImport("psapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr handle, out ProcessMemoryCounters counters, uint size);

    private static double ReadWindowsMemory()
    {
        return GetProcessMemoryInfo(new IntPtr(-1), out var counters, (uint)Marshal.SizeOf<ProcessMemoryCounters>())
            ? counters.workingSet.ToUInt64() : 0;
    }
#endif
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaDebugPanel : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float width = rectTransform.rect.width, height = rectTransform.rect.height;
        var border = new Color32(61, 63, 65, (byte)Mathf.FloorToInt(color.a * 255));
        var inner = new Color32(44, 47, 48, border.a);
        Quad(mesh, 0, 0, width, 3, border);
        Quad(mesh, 0, height - 3, width, 3, border);
        Quad(mesh, 0, 3, 3, height - 6, border);
        Quad(mesh, width - 3, 3, 3, height - 6, border);
        Quad(mesh, 3, 3, width - 6, height - 6, inner);
    }

    private static void Quad(VertexHelper mesh, float x, float y, float width, float height, Color32 color)
    {
        int index = mesh.currentVertCount;
        mesh.AddVert(new Vector3(x, -y), color, Vector2.zero);
        mesh.AddVert(new Vector3(x + width, -y), color, Vector2.zero);
        mesh.AddVert(new Vector3(x + width, -y - height), color, Vector2.zero);
        mesh.AddVert(new Vector3(x, -y - height), color, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class VanillaDebugGraph : MaskableGraphic
{
    private readonly List<double> history = new List<double>(100);
    private double peak;

    public double Average
    {
        get
        {
            double sum = 0;
            foreach (double value in history) sum += value;
            return history.Count == 0 ? 0 : sum / history.Count;
        }
    }

    public double Lowest
    {
        get
        {
            double lowest = double.MaxValue;
            foreach (double value in history) lowest = Math.Min(lowest, value);
            return history.Count == 0 ? 0 : lowest;
        }
    }

    public void Clear()
    {
        history.Clear();
        peak = 0;
        SetVerticesDirty();
    }

    public void Sample(double value, double maximum)
    {
        if (history.Count == 100) history.RemoveAt(0);
        history.Add(value);
        peak = Math.Max(maximum, value);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float width = rectTransform.rect.width, height = rectTransform.rect.height;
        double scale = peak > 0 ? height / peak : 0;
        if (history.Count > 0)
        {
            var previous = new Vector2(4, -(height - (float)(history[0] * scale) - 1));
            for (int i = 0; i < history.Count; i++)
            {
                var point = new Vector2(5 + i * (width - 2) / 99, -(height - (float)(history[i] * scale) - 1));
                Line(mesh, previous, point, Color.white);
                previous = point;
            }
        }
        var axisColor = new Color(1, 1, 1, .5f);
        Line(mesh, new Vector2(4, 0), new Vector2(4, -height), axisColor);
        Line(mesh, new Vector2(4, -height), new Vector2(4 + width, -height), axisColor);
    }

    private static void Line(VertexHelper mesh, Vector2 start, Vector2 end, Color color)
    {
        Vector2 direction = end - start;
        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * .5f;
        int index = mesh.currentVertCount;
        mesh.AddVert(start - normal, color, Vector2.zero);
        mesh.AddVert(start + normal, color, Vector2.zero);
        mesh.AddVert(end + normal, color, Vector2.zero);
        mesh.AddVert(end - normal, color, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
