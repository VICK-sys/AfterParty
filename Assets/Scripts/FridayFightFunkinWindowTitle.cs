using System.Collections;
using UnityEngine;

public sealed class FridayFightFunkinWindowTitle : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private delegate bool WindowCallback(System.IntPtr window, System.IntPtr data);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowCallback callback, System.IntPtr data);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(System.IntPtr window, out uint processId);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassName(System.IntPtr window, System.Text.StringBuilder name, int capacity);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SetWindowText(System.IntPtr window, string title);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        var host = new GameObject("Friday Fight Funkin' Window Title");
        DontDestroyOnLoad(host);
        host.AddComponent<FridayFightFunkinWindowTitle>();
    }

    private IEnumerator Start()
    {
        int processId;
        using (var process = System.Diagnostics.Process.GetCurrentProcess())
            processId = process.Id;
        while (true)
        {
            System.IntPtr window = FindWindow(processId);
            if (window != System.IntPtr.Zero && SetWindowText(window, "Friday Fight Funkin'"))
                break;
            yield return null;
        }
        Destroy(gameObject);
    }

    private static System.IntPtr FindWindow(int processId)
    {
        System.IntPtr result = System.IntPtr.Zero;
        EnumWindows((window, data) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != processId) return true;
            var name = new System.Text.StringBuilder(256);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "UnityWndClass") return true;
            result = window;
            return false;
        }, System.IntPtr.Zero);
        return result;
    }
#endif
}
