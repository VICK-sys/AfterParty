using UnityEngine;

public static class VanillaMenuTiming
{
    public static float Delta => Clamp(Time.unscaledDeltaTime);
    public static float Clamp(float delta) => Mathf.Clamp(delta, 0, 1f / 30);
}
