using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class VanillaPhillyBackgroundValidation
{
    public static void CheckStage(Song song)
    {
        string output = Environment.GetEnvironmentVariable("UNITY_PARTY_PHILLY_BACKGROUND_PATH");
        if (string.IsNullOrEmpty(output)) return;
        var stage = song.vanillaPlayback.CampaignStage;
        if (stage == null || !stage.StageId.StartsWith("phillyStreets")) return;
        Directory.CreateDirectory(output);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var render = typeof(VanillaCampaignStage).GetMethod("Render", flags);
        var clock = typeof(VanillaCampaignStage).GetField("clock", flags);
        float savedClock = stage.Clock;
        Vector3 savedPosition = song.mainCamera.transform.position;
        float savedSize = song.mainCamera.orthographicSize;
        var graphics = stage.GetComponentsInChildren<VanillaWeek2Graphic>(true);
        var active = graphics.Select(item => item.gameObject.activeSelf).ToArray();
        var sprites = stage.GetComponentsInChildren<SpriteRenderer>(true);
        var spriteEnabled = sprites.Select(item => item.enabled).ToArray();
        var sky = graphics.Single(item => item.name == (stage.StageId == "phillyStreetsErect" ? "phillySkybox" : "sky"));
        var scenarios = new[] { new Vector4(2050, 900, .77f, 0), new Vector4(1450, 850, .85f, 37), new Vector4(1800, 1000, .65f, 95) };
        var metadata = new JArray();
        try
        {
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].gameObject.SetActive(graphics[i] == sky || graphics[i].name.StartsWith("mist")
                    || graphics[i].name.StartsWith("philly") && !graphics[i].name.StartsWith("phillyCars")
                    || graphics[i].name == "grey1" || graphics[i].name == "grey2");
            foreach (var sprite in sprites) sprite.enabled = sprite.name == "solid";
            for (int index = 0; index < scenarios.Length; index++)
            {
                Vector4 scenario = scenarios[index];
                song.mainCamera.transform.position = new Vector3(scenario.x / 100, -scenario.y / 100, -10);
                song.mainCamera.orthographicSize = 3.6f / scenario.z;
                clock.SetValue(stage, scenario.w);
                foreach (var graphic in graphics) graphic.SetAnimationFrame(0);
                render.Invoke(stage, new object[] { 0f });
                string prefix = stage.StageId + "-" + index;
                VanillaSongValidation.CaptureStage(song, Path.Combine(output, prefix + ".png"));
                var visible = graphics.Where(item => item.gameObject.activeSelf).ToArray();
                foreach (var graphic in visible) graphic.gameObject.SetActive(graphic == sky);
                foreach (var sprite in sprites) sprite.enabled = false;
                VanillaSongValidation.CaptureStage(song, Path.Combine(output, prefix + "-sky.png"));
                sky.transform.localScale = Vector3.one * .65f;
                VanillaSongValidation.CaptureStage(song, Path.Combine(output, prefix + "-scaled-control.png"));
                sky.transform.localScale = Vector3.one;
                foreach (var graphic in visible) graphic.gameObject.SetActive(true);
                foreach (var sprite in sprites) sprite.enabled = sprite.name == "solid";
                metadata.Add(new JObject { ["stage"] = stage.StageId, ["index"] = index,
                    ["cameraX"] = scenario.x, ["cameraY"] = scenario.y, ["zoom"] = scenario.z, ["time"] = scenario.w });
            }
            File.WriteAllText(Path.Combine(output, stage.StageId + "-scenarios.json"), metadata.ToString());
        }
        finally
        {
            sky.transform.localScale = Vector3.one;
            for (int i = 0; i < graphics.Length; i++) graphics[i].gameObject.SetActive(active[i]);
            for (int i = 0; i < sprites.Length; i++) sprites[i].enabled = spriteEnabled[i];
            clock.SetValue(stage, savedClock);
            song.mainCamera.transform.position = savedPosition;
            song.mainCamera.orthographicSize = savedSize;
            render.Invoke(stage, new object[] { 0f });
        }
    }
}
