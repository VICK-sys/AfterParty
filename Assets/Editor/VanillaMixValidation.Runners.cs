using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class VanillaMixValidation
{
    public static void CheckRunnerLoading(Song song)
    {
        var stage = song.vanillaPlayback.CampaignStage;
        var pool = Field<IList>(stage, "runners");
        Require(pool.Count == 4, "Stress did not prepare four runners during loading.");
        var graphics = pool.Cast<object>().Select(runner => (VanillaWeek2Graphic)ProbeGet(runner, "graphic")).ToArray();
        var meshes = graphics.Select(graphic => Field<Dictionary<int, Mesh>>(graphic, "meshes")).ToArray();
        var composites = graphics.Select(graphic => Field<RenderTexture>(graphic, "composite")).ToArray();
        var texture = Field<Texture2D>(graphics[0], "texture");
        bool pico = song.vanillaPlayback.Variation == "pico";
        for (int slot = 0; slot < graphics.Length; slot++)
        {
            var animations = (JObject)Field<JObject>(graphics[slot], "data")["animations"];
            int expected = animations.Properties().SelectMany(animation => animation.Value["frames"].Values<int>()).Distinct().Count();
            Require(meshes[slot].Count == expected, "Stress runner has unwarmed animation frames.");
            Require(Field<Texture2D>(graphics[slot], "texture") == texture, "Stress runners duplicated their atlas.");
            Require(!pico || composites[slot] != null && composites[slot].IsCreated(), "Stress Pico runner has no prepared composite.");
        }
        int textureLoads = VanillaWeek2Graphic.TextureLoads;
        int meshCount = meshes[0].Count;
        int graphicCount = stage.GetComponentsInChildren<VanillaWeek2Graphic>(true).Length;
        var random = UnityEngine.Random.state;
        int previousSlot = Field<int>(stage, "nextRunner");
        var timer = new System.Diagnostics.Stopwatch();
        double maximumSpawn = 0;
        try
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                stage.ResetStage();
                Require(stage.ActiveRunners == 0 && stage.RunnerSpawns == 0, "Runner reset retained active effects.");
                for (int spawn = 0; spawn < 9; spawn++)
                {
                    int slot = spawn < 4 ? spawn : Field<int>(stage, "nextRunner");
                    timer.Restart();
                    stage.SpawnRunner(1000000, spawn % 2 == 0);
                    timer.Stop();
                    maximumSpawn = Math.Max(maximumSpawn, timer.Elapsed.TotalMilliseconds);
                    Require(graphics[slot].gameObject.activeSelf, "Prepared runner pool selected the wrong slot.");
                    foreach (string animation in new[] { "run", "shot1", "shot2" })
                    {
                        graphics[slot].Play(animation);
                        for (int frame = 0; frame < 14; frame++)
                        {
                            graphics[slot].SetAnimationFrame(frame);
                            ProbeInvoke(stage, "RenderRunner", pool[slot], 0f, song.mainCamera.transform.position);
                        }
                    }
                    graphics[slot].FrozenFrame = -1;
                    foreach (var graphic in graphics) graphic.gameObject.SetActive(false);
                }
            }
            Require(VanillaWeek2Graphic.TextureLoads == textureLoads, "Runner spawning loaded a texture during gameplay.");
            Require(meshes.All(cache => cache.Count == meshCount), "Runner animation created meshes during gameplay.");
            Require(pool.Count == 4 && stage.GetComponentsInChildren<VanillaWeek2Graphic>(true).Length == graphicCount,
                "Runner spawning created graphics during gameplay.");
            Require(graphics.Select((graphic, slot) => Field<RenderTexture>(graphic, "composite") == composites[slot]).All(same => same),
                "Runner spawning created a composite during gameplay.");
            Debug.Log("STRESS RUNNER LOADING PASSED: 27 spawns across three runs, all animation frames, shared atlas, no new textures, meshes, graphics, or composites. Maximum spawn: " + maximumSpawn + " ms.");
        }
        finally
        {
            foreach (var graphic in graphics) graphic.FrozenFrame = -1;
            stage.ResetStage();
            ProbeSet(stage, "nextRunner", previousSlot);
            UnityEngine.Random.state = random;
        }
    }

    private static void CheckRunnerRendering(Song song)
    {
        string reference = Environment.GetEnvironmentVariable("UNITY_PARTY_RUNNER_REFERENCE_PATH");
        if (string.IsNullOrEmpty(reference)) return;
        var source = JArray.Parse(File.ReadAllText(reference));
        Require(source.Count == 304, "Runner reference is incomplete.");
        var stage = song.vanillaPlayback.CampaignStage;
        var chart = Field<JObject>(stage, "chart");
        JToken variation = chart["variation"].DeepClone();
        var random = UnityEngine.Random.state;
        int previousSlot = Field<int>(stage, "nextRunner");
        var results = new JArray();
        float maximumError = 0;
        bool rejectedOldPlacement = false;
        try
        {
            ProbeSet(stage, "nextRunner", 0);
            foreach (var group in source.GroupBy(row => new { scale = (float)row["scale"], fresh = (bool)row["fresh"], flip = (bool)row["flip"] }))
            {
                chart["variation"] = group.Key.scale > 1 ? "pico" : "";
                int firstReusedSlot = Field<int>(stage, "nextRunner");
                stage.ResetStage();
                Require(Field<int>(stage, "nextRunner") == firstReusedSlot, "Runner retry reset the source reuse cursor.");
                object runner = null;
                VanillaWeek2Graphic graphic = null;
                for (int spawn = 0; spawn < (group.Key.fresh ? 1 : 5); spawn++)
                {
                    if (graphic != null) graphic.gameObject.SetActive(false);
                    stage.SpawnRunner(1000000, !group.Key.flip);
                    var pool = Field<IList>(stage, "runners");
                    Require(pool.Count >= Math.Min(spawn + 1, 4), "Runner pool recycled before constructing its four source slots.");
                    runner = pool[spawn < 4 ? spawn : firstReusedSlot];
                    graphic = (VanillaWeek2Graphic)ProbeGet(runner, "graphic");
                    Require(graphic.gameObject.activeSelf, "Runner pool selected the wrong slot.");
                    float y = -graphic.Position.y * 100;
                    Require(group.Key.scale > 1 ? Mathf.Abs(y - 350) < .001f : y >= 250 && y <= 300, "Runner logical height changed.");
                }
                foreach (JToken expected in group)
                {
                    graphic.Position = new Vector3(10, -3.5f, 0);
                    graphic.Play((string)expected["animation"]);
                    graphic.SetAnimationFrame((int)expected["frame"]);
                    ProbeInvoke(stage, "RenderRunner", runner, 0f, new Vector3(6.4f, -3.6f, -10));
                    float[] actual = RunnerBounds(stage, graphic);
                    float[] bounds = expected["bounds"].Values<float>().ToArray();
                    float error = actual.Zip(bounds, (a, b) => Mathf.Abs(a - b)).Max();
                    maximumError = Mathf.Max(maximumError, error);
                    Require(error < .002f, "Runner rendered bounds differ from the reference: " + expected + ", actual=" + string.Join(",", actual));
                    results.Add(new JObject { ["scale"] = group.Key.scale, ["fresh"] = group.Key.fresh, ["flip"] = group.Key.flip,
                        ["animation"] = (string)expected["animation"], ["frame"] = (int)expected["frame"], ["bounds"] = new JArray(actual), ["maximumError"] = error });
                    if (!rejectedOldPlacement && group.Key.scale > 1 && !group.Key.fresh && (string)expected["animation"] == "run")
                    {
                        graphic.GlobalOffset = Vector3.zero;
                        graphic.ApplyAnimationOffsets = true;
                        graphic.Advance(0, new Vector3(6.4f, -3.6f, -10), 0);
                        Require(RunnerBounds(stage, graphic).Zip(bounds, (a, b) => Mathf.Abs(a - b)).Max() > 20, "Old runner placement passed the rejection control.");
                        graphic.ApplyAnimationOffsets = false;
                        rejectedOldPlacement = true;
                    }
                }
            }
            Require(rejectedOldPlacement, "Runner placement rejection control did not run.");
            var cycles = JArray.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(reference), "pool-reference.json")));
            Require(cycles.Count == 3, "Runner pool reference is incomplete.");
            ProbeSet(stage, "nextRunner", 0);
            foreach (JToken cycle in cycles)
            {
                stage.ResetStage();
                foreach (int slot in cycle["slots"].Values<int>())
                {
                    stage.SpawnRunner(1000000, true);
                    var pool = Field<IList>(stage, "runners");
                    var graphic = (VanillaWeek2Graphic)ProbeGet(pool[slot], "graphic");
                    Require(graphic.gameObject.activeSelf, "Runner pool differs from source after retry: " + slot);
                    foreach (object item in pool) ((VanillaWeek2Graphic)ProbeGet(item, "graphic")).gameObject.SetActive(false);
                }
            }
            string output = Path.Combine(Environment.GetEnvironmentVariable("UNITY_PARTY_SONG_TEST_PATH"), "runner-rendering.json");
            File.WriteAllText(output, new JObject { ["passed"] = true, ["cases"] = results.Count, ["maximumError"] = maximumError,
                ["oldPlacementRejected"] = rejectedOldPlacement, ["poolSequenceMatched"] = true, ["results"] = results }.ToString());
            Debug.Log("STRESS RUNNER RENDERING PASSED: " + results.Count + " source frames, maximum error " + maximumError + " stage pixels, old placement rejected.");
        }
        finally
        {
            chart["variation"] = variation;
            stage.ResetStage();
            ProbeSet(stage, "nextRunner", previousSlot);
            UnityEngine.Random.state = random;
        }
    }

    private static float[] RunnerBounds(VanillaCampaignStage stage, VanillaWeek2Graphic graphic)
    {
        var mesh = Field<Dictionary<int, Mesh>>(graphic, "meshes")[graphic.Frame];
        var vertices = mesh.vertices.Select(point => stage.transform.InverseTransformPoint(graphic.transform.TransformPoint(point))).ToArray();
        return new[] { vertices.Min(p => p.x) * 100, -vertices.Max(p => p.y) * 100, vertices.Max(p => p.x) * 100, -vertices.Min(p => p.y) * 100 };
    }
}
