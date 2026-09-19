using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class VanillaMixCompanion
{
    private enum State { Default, PreRaise, Raise, Ready, Lower, HairBlow, HairFall, HairKnife, HairFallKnife }
    private readonly List<VanillaWeek2Graphic> graphics = new List<VanillaWeek2Graphic>();
    private readonly VanillaWeek2Graphic[] bars = new VanillaWeek2Graphic[7];
    private readonly VanillaABotAnalyzer analyzer = new VanillaABotAnalyzer();
    private Song song;
    private string id;
    private Func<string, int, VanillaWeek2Graphic> create;
    private Action<string> play;
    private Func<VanillaWeek2Graphic> current;
    private VanillaWeek2Graphic body;
    private VanillaWeek2Graphic pupils;
    private VanillaWeek2Graphic speaker;
    private VanillaWeek2Graphic muzzle;
    private VanillaWeek2Graphic heart;
    private VanillaWeek2Graphic darkBody;
    private SpriteRenderer eyes;
    private State state;
    private bool pixel;
    private bool otis;
    private bool train;
    private int blink;
    private int lastFocus;
    private int pupilDirection;
    private float pupilAge;
    private int order;
    private string lastAnimation;
    private int lastAnimationFrame;

    public bool BlocksDance => otis || state != State.Default;
    public IEnumerable<VanillaWeek2Graphic> Graphics => graphics;
    public VanillaWeek2Graphic Body => body;

    public void Initialize(Song owner, string characterId, int zIndex,
        Func<string, int, VanillaWeek2Graphic> factory, Action<string> protectedPlay,
        Func<VanillaWeek2Graphic> activeGraphic, Action<Renderer, int> sort = null)
    {
        song = owner;
        id = characterId;
        order = zIndex;
        create = factory;
        play = protectedPlay;
        current = activeGraphic;
        pixel = id == "nene-pixel";
        otis = id == "otis-speaker";
        var actor = current();
        Vector3 offset = Vector3.Scale(actor.GlobalOffset, new Vector3(
            Mathf.Abs(actor.transform.localScale.x), actor.transform.localScale.y, 1));
        Vector3 origin = actor.Position + offset;
        if (pixel)
        {
            origin += Point(296, 430);
            body = Add("aBotPixelBody", origin, -10, 6);
            var back = Add("aBotPixelBack", origin + Point(-55, 0), -12, 6);
            back.transform.localScale = new Vector3(6.1f, 6, 1);
            back.Position += Vector3.left * (back.FrameSize.x * .1f / 200);
            speaker = Add("aBotPixelSpeaker", origin + Point(-78, 9), -13, 6);
            pupils = Add("abotHead", origin + Point(-325, 72), -14, 6);
            AddBars("aBotVizPixel", origin + Point(-162, 16), 6,
                new[] { 0f, 42, 90, 144, 204, 240, 282 },
                new[] { 0f, -12, -18, -18, -18, -12, 0 });
        }
        else
        {
            origin += otis ? Point(170, 455) : Point(-95, 384);
            body = Add("abotSystem", origin, -10);
            Add("stereoBG", origin + Point(150, 30), -18);
            pupils = Add("systemEyes", origin + Point(50, 238), -15);
            var obj = new GameObject("A-Bot Eye Whites");
            obj.transform.SetParent(actor.transform.parent, false);
            eyes = obj.AddComponent<SpriteRenderer>();
            eyes.sprite = FunkinHudAssets.Solid;
            eyes.sharedMaterial = FunkinNoteSkin.NoteMaterial;
            obj.transform.localPosition = origin + Point(40, 250);
            obj.transform.localScale = new Vector3(160, 60, 1);
            if (sort != null) sort(eyes, order - 20);
            else eyes.sortingOrder = order - 20;
            AddBars("visualizer", origin + Point(207, 84), 1,
                new[] { 0f, 59, 115, 181, 235, 287, 338 },
                new[] { 0f, -8, -11.5f, -11.9f, -11.4f, -6.7f, .3f });
        }
        if (otis) muzzle = Add("otisMuzzle", actor.Position, -1);
        if (id == "nene-tankmen") heart = Add("comboHeart", actor.Position + Point(174.5f, -66), 1);
        Reset();
    }

    private VanillaWeek2Graphic Add(string name, Vector3 position, int layer, float scale = 1)
    {
        var graphic = create("effects/" + name, order + layer);
        graphic.Position = position;
        graphic.transform.localScale = new Vector3(scale, scale, 1);
        if (pixel)
        {
            Vector2 pivot = graphic.FrameSize / 2;
            if (name == "aBotPixelBody" || name == "aBotPixelSpeaker")
                pivot = new Vector2(Mathf.Floor(pivot.x + .5f), Mathf.Floor(pivot.y + .5f));
            graphic.Position += new Vector3(-pivot.x, pivot.y) * ((scale - 1) / 100);
        }
        graphics.Add(graphic);
        return graphic;
    }

    public void ApplyLighting(string root, int week)
    {
        if (id == "nene-dark")
        {
            darkBody = Add("abotSystem", body.Position, -9);
            darkBody.ReplaceTexture(Path.Combine(root, "effects/abotSystem/dark.png"));
            foreach (var graphic in graphics)
                if (graphic.name == "stereoBG") graphic.Tint = new Color32(97, 103, 133, 255);
            foreach (var bar in bars) bar.ColorAdjustment = new Vector4(-26, -45, -12, 0);
        }
        if (week == 7)
        {
            foreach (var graphic in graphics) graphic.ColorAdjustment = new Vector4(-40, -20, -40, -25);
            foreach (var bar in bars) bar.ColorAdjustment = new Vector4(-30, -10, -12, 0);
            if (muzzle != null) muzzle.ColorAdjustment = Vector4.zero;
        }
        if (pixel)
        {
            foreach (var graphic in graphics) graphic.ColorAdjustment = new Vector4(-10, -23, -66, 24);
            speaker.SetRim(Path.Combine(root, "effects/aBotPixelSpeaker_mask.png"), 5, 1, new Vector4(-10, -23, -66, 24), null, 90, 0);
        }
    }

    private static void Restart(VanillaWeek2Graphic graphic, string animation)
    {
        graphic.FrozenFrame = -1;
        graphic.Play(animation);
    }

    private void AddBars(string asset, Vector3 position, float scale, float[] x, float[] y)
    {
        for (int index = 0; index < bars.Length; index++)
        {
            bars[index] = Add(asset, position + Point(x[index], y[index]), -11, scale);
            Restart(bars[index], (index + 1).ToString());
        }
    }

    public void Reset()
    {
        analyzer.Reset();
        state = State.Default;
        train = false;
        blink = 3;
        lastFocus = -1;
        pupilDirection = 0;
        pupilAge = 1;
        lastAnimation = null;
        lastAnimationFrame = -1;
        foreach (var graphic in graphics)
        {
            graphic.gameObject.SetActive(true);
            graphic.FrozenFrame = -1;
        }
        if (pixel)
        {
            Restart(body, "danceLeft");
            Restart(speaker, "danceLeft");
            Restart(pupils, "toright");
        }
        else
        {
            Restart(body, body.Animation);
            if (darkBody != null) Restart(darkBody, darkBody.Animation);
            Restart(pupils, pupils.Animation);
        }
        foreach (var bar in bars) bar.Alpha = 0;
        if (muzzle != null) muzzle.gameObject.SetActive(false);
        if (heart != null) heart.gameObject.SetActive(false);
        if (eyes != null) eyes.gameObject.SetActive(true);
    }

    public void Train(bool passing) => train = passing;

    public void Beat()
    {
        var actor = current();
        if (pixel)
        {
            Restart(speaker, "danceLeft");
            if (state == State.Default && body.Has(actor.Animation)) Restart(body, actor.Animation);
        }
        else
        {
            Restart(body, body.Animation);
            body.Advance(1f / 24, song.mainCamera.transform.position, 0);
        }
        if (otis) return;
        if (state == State.PreRaise)
        {
            if (actor.Animation != "danceLeft" || actor.Finished) play("danceLeft");
        }
        else if (state == State.Ready)
        {
            if (blink == 0)
            {
                play(pixel ? "idleKnifeBlink" : "idleKnife");
                blink = UnityEngine.Random.Range(3, 8);
            }
            else blink--;
        }
        else if (state == State.Lower && !pixel && actor.Animation != "lowerKnife") play("lowerKnife");
    }

    public void Shoot(int direction)
    {
        if (!otis || direction < 0 || direction > 3) return;
        string animation = "shoot" + (direction + 1);
        play(animation);
        muzzle.Position = current().Position + Point(direction < 2 ? 950 : -350,
            direction == 0 ? 0 : direction == 3 ? -100 : -50);
        muzzle.gameObject.SetActive(true);
        muzzle.Additive = true;
        Restart(muzzle, animation);
    }

    private void Change(State next, string animation = null)
    {
        state = next;
        if (animation != null) play(animation);
    }

    private void AdvanceState()
    {
        if (otis || song.vanillaPlayback.PlayerId == "pico-blazin") return;
        var actor = current();
        bool low = song.health <= 50;
        switch (state)
        {
            case State.Default:
                if (low) Change(State.PreRaise);
                break;
            case State.PreRaise:
                if (!low) Change(State.Default);
                else if (actor.Animation == "danceLeft" && actor.AnimationFrame >= 13) Change(State.Raise, "raiseKnife");
                break;
            case State.Raise:
                if (actor.Finished) Change(State.Ready);
                break;
            case State.Ready:
                if (!low) Change(State.Lower, pixel ? "lowerKnife" : null);
                else if (pixel && actor.Finished) play("idleKnife");
                break;
            case State.Lower:
                if (actor.Animation == "lowerKnife" && actor.Finished) Change(State.Default);
                break;
            case State.HairBlow:
                if (!train) Change(State.HairFall, "hairFallNormal");
                else if (actor.Finished) play("hairBlowNormal");
                break;
            case State.HairFall:
                if (actor.Finished) Change(State.Default);
                break;
            case State.HairKnife:
                if (!train) Change(State.HairFallKnife, "hairFallKnife");
                else if (actor.Finished) play("hairBlowKnife");
                break;
            case State.HairFallKnife:
                if (actor.Finished) Change(State.Ready);
                break;
        }
        if (!pixel && train && (int)state <= (int)State.Lower)
        {
            bool raised = state == State.Raise || state == State.Ready;
            Change(raised ? State.HairKnife : State.HairBlow, raised ? "hairBlowKnife" : "hairBlowNormal");
        }
    }

    public void Advance(float delta, Vector3 camera, float clock)
    {
        AdvanceState();
        var actor = current();
        int focus = song.vanillaPlayback.FocusCharacter;
        if (focus >= 0 && focus < 2 && focus != lastFocus)
        {
            lastFocus = focus;
            pupilDirection = focus;
            pupilAge = 0;
            if (pixel) Restart(pupils, focus == 0 ? "toright" : "toleft");
        }
        pupilAge += delta;
        if (!pixel) pupils.SetAnimationFrame(pupilDirection == 0
            ? 17 + Mathf.Min(13, (int)(pupilAge * 24)) : Mathf.Min(16, (int)(pupilAge * 24)));
        bool visible = actor.gameObject.activeSelf && !song.isDead;
        if (eyes != null) eyes.gameObject.SetActive(visible);
        foreach (var graphic in graphics)
        {
            if (graphic == muzzle || graphic == heart) continue;
            graphic.gameObject.SetActive(visible);
            if (id != "nene-dark" && id != "nene-tankmen" && !otis && !pixel)
            {
                graphic.Tint = actor.Tint;
                graphic.ColorAdjustment = actor.ColorAdjustment;
                graphic.PhillyColor = actor.PhillyColor;
            }
        }
        if (darkBody != null)
        {
            darkBody.SetAnimationFrame(body.AnimationFrame);
            darkBody.Alpha = actor.Alpha;
            eyes.color = Color.Lerp(Color.white, new Color32(111, 150, 206, 255), actor.Alpha);
        }
        if (heart != null)
        {
            bool start = actor.Animation == "combo50" &&
                (lastAnimation != "combo50" || actor.AnimationFrame < lastAnimationFrame);
            if (start)
            {
                heart.gameObject.SetActive(visible);
                Restart(heart, "idle");
            }
            else if (heart.Finished || !visible) heart.gameObject.SetActive(false);
        }
        lastAnimation = actor.Animation;
        lastAnimationFrame = actor.AnimationFrame;
        UpdateVisualizer();
        foreach (var graphic in graphics)
            if (graphic.gameObject.activeSelf) graphic.Advance(delta, camera, clock);
        if (muzzle != null)
        {
            muzzle.Additive = muzzle.AnimationFrame < 2;
            if (muzzle.Finished || !visible) muzzle.gameObject.SetActive(false);
        }
    }

    private void UpdateVisualizer()
    {
        bool audible = song.songStarted && song.musicSources[0].isPlaying && !song.musicSources[0].mute
            && song.musicSources[0].volume > 0 && AudioListener.volume > 0;
        bool available = audible && analyzer.Read(song.musicSources[0]);
        for (int index = 0; index < bars.Length; index++)
        {
            int frame = available ? Mathf.RoundToInt(analyzer.Levels[index] * 6) : 0;
            bars[index].Alpha = frame > 0 ? 1 : 0;
            bars[index].SetAnimationFrame(5 - Mathf.Clamp(frame - 1, 0, 5));
        }
    }

    public VanillaWeek2Graphic DeathKnife()
    {
        if (otis) return null;
        var actor = current();
        var knife = create("effects/knife", -5);
        knife.gameObject.layer = song.deadBoyfriend.layer;
        knife.Position = actor.Position + (pixel ? Point(280, 170)
            + Vector3.Scale(actor.GlobalOffset, new Vector3(6, 6, 1))
            : id == "nene-christmas" ? Point(0, -40) : Point(120, 0));
        knife.transform.localScale = Vector3.one * (pixel ? 6 : 1);
        if (pixel) knife.Position += new Vector3(-knife.FrameSize.x, knife.FrameSize.y) * .025f;
        Restart(knife, "throw");
        return knife;
    }

    private static Vector3 Point(float x, float y) => new Vector3(x / 100, -y / 100, 0);
}
