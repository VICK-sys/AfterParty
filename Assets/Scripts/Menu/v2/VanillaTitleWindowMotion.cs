using UnityEngine;

public sealed class VanillaTitleWindowMotion : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WindowRect { public int left, top, right, bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(System.IntPtr window, out WindowRect rectangle);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(System.IntPtr window, System.IntPtr after, int x, int y, int width, int height, uint flags);

    private System.IntPtr window;
    private WindowRect origin;
    private bool moving;
    private float age;

    private void Update()
    {
        if (Screen.fullScreen) return;
        if (Input.GetKeyDown(KeyCode.Y))
        {
            window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            moving = window != System.IntPtr.Zero && GetWindowRect(window, out origin);
            age = 0;
        }
        if (!moving) return;
        age += VanillaMenuTiming.Delta;
        float x = age < 0.35f ? 0 : Ease(Mathf.PingPong((age - 0.35f) / 1.4f, 1)) * 300;
        float y = Ease(Mathf.PingPong(age / 0.7f, 1)) * 100;
        SetWindowPos(window, System.IntPtr.Zero, origin.left + Mathf.RoundToInt(x), origin.top + Mathf.RoundToInt(y), 0, 0, 0x15);
    }

    private static float Ease(float value)
    {
        return value < 0.5f ? 2 * value * value : 1 - Mathf.Pow(-2 * value + 2, 2) / 2;
    }
#endif
}
