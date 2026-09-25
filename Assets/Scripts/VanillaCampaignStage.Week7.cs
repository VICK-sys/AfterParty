using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public sealed partial class VanillaCampaignStage
{
    public int SpeakerShots { get; private set; }
    public int RunnerSpawns { get; private set; }
    public int SpecialNoteHits { get; private set; }
    public int ActiveRunners => runners.Count(runner => runner.graphic.gameObject.activeSelf);
    public float TankAngle => tankAngle;
    public float DeathDuration => actors[0].id == "pico-blazin" ? 1.25f : explosionDeath ? 3 : actors[0].id.StartsWith("pico") && actors[0].id != "pico-holding-nene" ? 35f / 24 : death == null ? 2.417f : death.Duration;
    private float tankAngle;
    private float tankSpeed;
    private VanillaWeek2Graphic clouds;
    private JToken[] speakerNotes = Array.Empty<JToken>();
    private readonly Queue<JToken> runnerNotes = new Queue<JToken>();
    private readonly List<Runner> runners = new List<Runner>();
    private int nextRunner;
    private AudioClip deathQuote;
    private bool deathLoopStarted;
    private bool deathConfirmed;
    private bool quotePlayed;
    private float quoteFade;

    private sealed class Runner
    {
        public VanillaWeek2Graphic graphic;
        public double time;
        public bool right;
        public float endingOffset;
        public float speed;
        public float shotAge;
        public int shot;
        public Vector2 origin;
        public Vector2 initialOffset;
        public bool fresh;
    }

    private void LoadWeek7()
    {
        tankAngle = UnityEngine.Random.Range(-90, 46);
        tankSpeed = UnityEngine.Random.Range(5f, 7f);
        if (StageId == "tankmanBattlefield")
        {
            clouds = Graphic(Path.Combine(root, "effects/clouds"), "Moving Clouds", 12);
            clouds.Scroll = Vector2.one * .25f;
            clouds.Position = new Vector3(-11, -.2f, 0);
        }
        else
        {
            PlaceBackgroundTankman("sniper", new Vector2(109.7f, 511.25f));
            PlaceBackgroundTankman("guy", new Vector2(1435.6f, 560.4f));
            for (int index = 0; index < actors.Length; index++)
            {
                string mask = actors[index].id == "gf-tankmen" ? Path.Combine(root, "effects/gfTankmen_mask.png")
                    : actors[index].id == "nene-tankmen" ? Path.Combine(root, "effects/neneTankmen_mask.png") : null;
                float angle = index == 1 ? 25 : 90;
                foreach (var graphic in new[] { actors[index].graphic, actors[index].censor }.Concat(actors[index].alternates).Where(item => item != null))
                    graphic.SetRim(mask, 15, index == 1 ? .3f : .1f, new Vector4(-38, -20, -46, -25),
                        new Color32(223, 239, 60, 255), angle, .4f, mask == null || actors[index].id == "nene-tankmen");
            }
        }
        if ((string)chart["song"] == "stress")
        {
            speakerNotes = JArray.Parse(File.ReadAllText(Path.Combine(root, (string)chart["variation"] == "pico" ? "speaker-chart-pico.json" : "speaker-chart.json"))).OrderBy(note => (double)note["t"]).ToArray();
            PrepareRunners();
        }
        if (actors[1].id == "tankman-bloody")
        {
            foreach (var graphic in new[] { actors[1].censor }.Concat(actors[1].alternates).Where(item => item != null && item.Has("idle-bloody")))
            {
                graphic.PrepareRimMask(Path.Combine(root, "effects/tankmanCaptainBloody_mask.png"));
                graphic.WarmFrames();
                graphic.Advance(0, song.mainCamera.transform.position, clock);
            }
            FunkinHudAssets.Icon("tankman-bloody");
        }
    }

    private void PlaceBackgroundTankman(string name, Vector2 stageTranslation)
    {
        var graphic = props[name];
        float scale = graphic.transform.localScale.x;
        graphic.GlobalOffset = new Vector3(stageTranslation.x, -stageTranslation.y, 0) * ((1 - scale) / (100 * scale));
    }

    private void ResetWeek7()
    {
        if (actors[1].id == "tankman-bloody") actors[1].censor?.ClearRimMask();
        SpeakerShots = RunnerSpawns = SpecialNoteHits = 0;
        runnerNotes.Clear();
        foreach (JToken note in speakerNotes)
            if (UnityEngine.Random.value < 1f / 16) runnerNotes.Enqueue(note);
        foreach (var runner in runners) runner.graphic.gameObject.SetActive(false);
        if (clouds != null) clouds.Position = new Vector3(-11, -.2f, 0);
        if (StageId == "tankmanBattlefieldErect") props["tankBricks"].Position = new Vector3(4.45f, -7.74f, 0);
        deathLoopStarted = deathConfirmed = quotePlayed = false;
        quoteFade = 0;
        if (deathQuote != null) Destroy(deathQuote);
        deathQuote = null;
        AdvanceTank(0);
    }

    public void AdvanceTank(float delta)
    {
        if (!props.TryGetValue("tankRolling", out var tank)) return;
        tankAngle += delta * tankSpeed;
        float radians = (tankAngle + 180) * Mathf.Deg2Rad;
        tank.Position = new Vector3(4 + Mathf.Cos(radians) * 15, -(13 + Mathf.Sin(radians) * 11), 0);
        tank.Angle = tankAngle - 75;
    }

    public void AdvanceWeek7(float delta, double time)
    {
        AdvanceTank(delta);
        if (clouds != null) clouds.Position += Vector3.right * (.08f * delta);
        while (SpeakerShots < speakerNotes.Length && (double)speakerNotes[SpeakerShots]["t"] <= time)
        {
            int direction = (int)speakerNotes[SpeakerShots++]["d"];
            if (actors[2].id == "otis-speaker") companion.Shoot(direction);
            else
            {
                direction += direction == 3 ? -UnityEngine.Random.Range(0, 2) : UnityEngine.Random.Range(0, 2);
                actors[2].Play("shoot" + (direction + 1), true);
            }
        }
        while (runnerNotes.Count > 0 && (double)runnerNotes.Peek()["t"] <= time + 3000)
        {
            JToken note = runnerNotes.Dequeue();
            SpawnRunner((double)note["t"], (int)note["d"] < 2);
        }
        foreach (Runner runner in runners.Where(item => item.graphic.gameObject.activeSelf))
        {
            var graphic = runner.graphic;
            if (graphic.Animation == "run")
            {
                if (time >= runner.time)
                {
                    graphic.Play("shot" + runner.shot);
                    runner.shotAge = 0;
                }
                else
                {
                    float factor = (float)(time - runner.time) * runner.speed;
                    float x = runner.right ? 1280 * .74f + runner.endingOffset - factor : 1280 * .02f - runner.endingOffset + factor;
                    graphic.Position = new Vector3(x / 100, graphic.Position.y, 0);
                }
            }
            else
            {
                runner.shotAge += delta;
                float flicker = runner.shotAge - 10f / 24;
                if (flicker >= .6f) graphic.gameObject.SetActive(false);
                else graphic.Alpha = flicker < 0 ? 1 : Mathf.FloorToInt(flicker / (flicker < .3f ? .1f : .05f)) % 2;
            }
        }
    }

    public void StressPicoOutroBeat(bool begin)
    {
        if (actors[2].id != "otis-speaker") return;
        actors[2].Dance(begin);
        companion?.Beat();
    }

    private void PrepareRunners()
    {
        for (int index = runners.Count; index < 4; index++)
        {
            var runner = new Runner { graphic = Graphic(Path.Combine(root, "effects/runner"), "Running Tankman " + index, 30) };
            runner.graphic.Play("run");
            runner.origin = runner.graphic.FrameSize * .5f;
            float initialScale = Mathf.Floor(runner.graphic.Size.x * .4f) / runner.graphic.FrameSize.x;
            runner.initialOffset = runner.origin * (1 - initialScale);
            runner.graphic.ApplyAnimationOffsets = false;
            runners.Add(runner);
            if ((string)chart["variation"] == "pico")
            {
                runner.graphic.SetRim(null, 15, .1f, new Vector4(-38, -20, -46, -25), new Color32(223, 239, 60, 255), 135, .4f, true);
            }
            runner.graphic.WarmFrames();
            runner.graphic.Advance(0, song.mainCamera.transform.position, clock);
            runner.graphic.gameObject.SetActive(false);
        }
    }

    public void SpawnRunner(double time, bool right)
    {
        bool fresh = RunnerSpawns < 4;
        int index = fresh ? RunnerSpawns : nextRunner;
        if (!fresh) nextRunner = (nextRunner + 1) % 4;
        Runner runner = runners[index];
        runner.fresh = fresh;
        if (fresh) runner.shot = UnityEngine.Random.Range(1, 3);
        runner.time = time;
        runner.right = right;
        runner.endingOffset = UnityEngine.Random.Range(50f, 200f);
        runner.speed = UnityEngine.Random.Range(.6f, 1f);
        runner.shotAge = 0;
        runner.graphic.transform.localScale = Vector3.one * ((string)chart["variation"] == "pico" ? 1.1f : 1);
        runner.graphic.Position = new Vector3(99.99f, -((string)chart["variation"] == "pico" ? 350 : 200 + UnityEngine.Random.Range(50, 101)) / 100f, 0);
        runner.graphic.FlipX = !right;
        runner.graphic.Alpha = 1;
        runner.graphic.Play("run");
        RenderRunner(runner, UnityEngine.Random.Range(0f, runner.graphic.Duration), song.mainCamera.transform.position);
        runner.graphic.gameObject.SetActive(true);
        RunnerSpawns++;
    }

    private void RenderWeek7(float delta, Vector3 camera)
    {
        if (clouds != null) clouds.Advance(delta, camera, clock);
        foreach (Runner runner in runners)
            if (runner.graphic.gameObject.activeSelf) RenderRunner(runner, delta, camera);
    }

    private void RenderRunner(Runner runner, float delta, Vector3 camera)
    {
        var graphic = runner.graphic;
        float scale = Mathf.Abs(graphic.transform.localScale.x);
        Vector2 animationOffset = graphic.Animation == "run" ? (runner.fresh ? runner.initialOffset : Vector2.zero) : new Vector2(300, 200);
        Vector2 offset = runner.origin * (1 - scale) - animationOffset;
        graphic.GlobalOffset = new Vector3(offset.x, -offset.y, 0) / (100 * scale);
        graphic.Advance(delta, camera, clock);
    }

    private IEnumerator LoadDeathQuote()
    {
        bool pico = actors[0].id.StartsWith("pico");
        string path = Path.Combine(root, pico ? "audio/jeffGameover-pico/jeffGameover-" + UnityEngine.Random.Range(1, 11) + ".ogg"
            : "audio/jeffGameover-" + UnityEngine.Random.Range(1, 26) + ".ogg");
        using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
            deathQuote = DownloadHandlerAudioClip.GetContent(request);
        }
    }

    private void Week7Death(string animation)
    {
        if (animation == "deathLoop") deathLoopStarted = true;
        if (animation == "deathConfirm")
        {
            deathConfirmed = true;
            sound.Stop();
            song.musicSources[0].volume = OptionsV2.instVolume;
        }
    }

    public void EnableTankmanMask()
    {
        if (actors[1].id != "tankman-bloody") return;
        foreach (var graphic in new[] { actors[1].censor }.Concat(actors[1].alternates).Where(item => item != null && item.Has("idle-bloody")))
            graphic.SetRim(Path.Combine(root, "effects/tankmanCaptainBloody_mask.png"), 15, .3f,
                new Vector4(-38, -20, -46, -25), new Color32(223, 239, 60, 255), 25, 1);
    }

    private void AdvanceDeathQuote(float delta)
    {
        if (!deathLoopStarted || deathConfirmed) return;
        if (!quotePlayed && deathQuote != null)
        {
            sound.clip = deathQuote;
            sound.Play();
            quotePlayed = true;
        }
        if (quotePlayed && !sound.isPlaying) quoteFade += delta;
        song.musicSources[0].volume = OptionsV2.instVolume * Mathf.Lerp(.2f, 1, quoteFade / 4);
    }
}
