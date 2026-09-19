using UnityEngine;

public sealed class VanillaWeekendRain : MonoBehaviour
{
    public Material Material { get; private set; }

    public void Configure(float time, float intensity)
    {
        if (Material == null)
        {
            Material = new Material(Resources.Load<Shader>("VanillaSongs/WeekendRain"));
            Material.SetColor("_RainColor", new Color32(102, 128, 204, 255));
        }
        var camera = GetComponent<Camera>();
        Material.SetFloat("_RainTime", time + 1);
        Material.SetFloat("_Intensity", intensity);
        Material.SetVector("_View", new Vector4(camera.transform.position.x * 100, -camera.transform.position.y * 100,
            camera.orthographicSize * camera.aspect * 200, camera.orthographicSize * 200));
    }

    private void OnDestroy()
    {
        if (Material != null) Destroy(Material);
    }
}
