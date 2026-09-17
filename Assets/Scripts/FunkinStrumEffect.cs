using System.Collections.Generic;
using UnityEngine;

public sealed class FunkinStrumEffect : MonoBehaviour
{
    private sealed class Effect
    {
        public SpriteRenderer Sprite;
        public Sprite[] Frames;
        public float Time;
        public int Rate;
        public FunkinNoteState Hold;
        public int Phase;
        public bool Splash;
        public FunkinStrumEffect Owner;
    }

    private readonly List<Effect> effects = new List<Effect>();
    private readonly List<Effect> splashes = new List<Effect>();
    private FunkinStrumEffect poolOwner;
    private int direction;
    private int side;
    private SpriteRenderer receptor;

    public void Initialize(int lane, int player, FunkinStrumEffect pool)
    {
        direction = lane;
        side = player;
        poolOwner = pool;
        receptor = GetComponent<SpriteRenderer>();
        foreach (Effect effect in effects) effect.Sprite.enabled = false;
    }

    private Effect NewEffect(bool splash)
    {
        Effect effect = null;
        if (splash)
        {
            var pool = poolOwner.splashes;
            if (pool.Count >= 6)
                effect = pool.Find(entry => !entry.Sprite.enabled) ?? pool[Random.Range(0, pool.Count)];
        }
        else
            effect = effects.Find(entry => !entry.Splash && !entry.Sprite.enabled);
        if (effect == null)
        {
            var child = new GameObject("Note Effect");
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            effect = new Effect { Sprite = child.AddComponent<SpriteRenderer>(), Splash = splash, Owner = this };
            effect.Sprite.sharedMaterial = receptor.sharedMaterial;
            effects.Add(effect);
            if (splash) poolOwner.splashes.Add(effect);
        }
        else if (effect.Owner != this)
        {
            effect.Owner.effects.Remove(effect);
            effect.Owner = this;
            effects.Add(effect);
            effect.Sprite.transform.SetParent(transform, false);
        }
        effect.Sprite.enabled = true;
        effect.Sprite.sortingLayerID = receptor.sortingLayerID;
        effect.Sprite.sortingOrder = splash ? 50 : 40;
        effect.Sprite.color = new Color(1, 1, 1, splash ? 0.8f : 1);
        effect.Time = 0;
        effect.Phase = 0;
        effect.Hold = null;
        return effect;
    }

    public void Splash()
    {
        Effect effect = NewEffect(true);
        int variant = Random.Range(1, 3);
        string color = FunkinNoteSkin.Colors[direction].ToLowerInvariant();
        string prefix = "note impact " + variant + " " + (direction == 1 && variant == 1 ? " " : "") + color + "0";
        effect.Frames = FunkinNoteSkin.Frames("noteSplashes", prefix);
        effect.Rate = Random.Range(22, 27);
    }

    public void Cover(FunkinNoteState note)
    {
        Effect effect = NewEffect(false);
        effect.Hold = note;
        effect.Frames = CoverFrames("Start");
        effect.Rate = 24;
    }

    private Sprite[] CoverFrames(string phase)
    {
        string color = FunkinNoteSkin.Colors[direction];
        return FunkinNoteSkin.Frames("holdCover" + color, "holdCover" + phase + color + "0");
    }

    private void LateUpdate()
    {
        Song song = Song.instance;
        if (song == null || !song.songSetupDone) return;
        bool running = song.IsCountingDown || song.stopwatch != null && song.stopwatch.IsRunning;
        foreach (Effect effect in effects)
        {
            if (!effect.Sprite.enabled) continue;
            if (running) effect.Time += Time.deltaTime;
            if (effect.Hold != null)
            {
                if (effect.Hold.HoldDropped || !receptor.enabled)
                {
                    effect.Sprite.enabled = false;
                    continue;
                }
                if (effect.Hold.HoldFinished && effect.Phase < 2)
                {
                    if (!Player.instance.Strumlines[side].Controlled || Player.demoMode)
                    {
                        effect.Sprite.enabled = false;
                        continue;
                    }
                    effect.Phase = 2;
                    effect.Time = 0;
                    effect.Frames = CoverFrames("End");
                }
            }
            int index = (int)(effect.Time * effect.Rate);
            if (index >= effect.Frames.Length)
            {
                if (effect.Hold != null && effect.Phase < 2)
                {
                    effect.Phase = 1;
                    effect.Time = 0;
                    effect.Frames = CoverFrames("");
                    index = 0;
                }
                else
                {
                    effect.Sprite.enabled = false;
                    continue;
                }
            }
            FunkinNoteSkin.ApplyFrame(effect.Sprite, effect.Frames[index]);
            float pixel = song.FunkinWorldPixelSize;
            Vector3 offset = effect.Hold == null ? new Vector3(-4.2f, -0.6f) : new Vector3(-12.6f, -44.8f);
            effect.Sprite.transform.position = transform.position + offset * pixel;
            FunkinNoteSkin.WorldScale(effect.Sprite.transform, pixel * 100);
        }
    }
}
