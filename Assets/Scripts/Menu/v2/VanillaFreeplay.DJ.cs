using UnityEngine;

public sealed partial class VanillaFreeplay
{
    private float djIdleTime;
    private bool djSeenEasterEgg;
    private int djIdleLoops;
    private int cartoonFrame;
    private int cartoonBlink;
    private int cartoonLoop;
    private int cartoonChangeChannel;
    private int cartoonRemoteClick;
    private int cartoonTvSound;
    private AudioSource cartoon;
    private AudioSource tvSound;
    private AudioClip[] cartoonClips;
    private bool cartoonPending;
    private bool cartoonPlaying;
    private float cartoonFade = -1;
    private float cartoonPreviewVolume = 1;

    private void InitializeDJ()
    {
        if (!IsPico)
        {
        const string symbol = "Boyfriend DJ watchin tv OG";
        cartoonBlink = dj.GetSymbolFrameLabel(symbol, "BLINK");
        cartoonLoop = dj.GetSymbolFrameLabel(symbol, "LOOP");
        cartoonChangeChannel = dj.GetSymbolFrameLabel(symbol, "CHANGE_CHANNEL");
        cartoonRemoteClick = dj.GetSymbolFrameLabel(symbol, "REMOTE_CLICK");
        cartoonTvSound = dj.GetSymbolFrameLabel(symbol, "TV_SOUND");
        }
        cartoon = gameObject.AddComponent<AudioSource>();
        tvSound = gameObject.AddComponent<AudioSource>();
        cartoon.playOnAwake = tvSound.playOnAwake = false;
    }

    private void DJPlayerAction()
    {
        djIdleTime = 0;
        djSeenEasterEgg = false;
    }

    private void UpdateDJ(float delta)
    {
        if (cartoonFade >= 0)
        {
            cartoonFade += delta;
            cartoon.volume = OptionsV2.miscVolume * Mathf.Clamp01(1 - cartoonFade / 0.25f);
            if (cartoonFade >= 0.25f) StopCartoon(false);
        }
        else cartoon.volume = OptionsV2.miscVolume;
        tvSound.volume = OptionsV2.miscVolume;
        cartoonPreviewVolume = Mathf.MoveTowards(cartoonPreviewVolume, cartoonPlaying || cartoon.clip != null ? 0.15f : 1, delta * 0.85f);
        if (!ready || closing || RankAnimationPlaying || rankDjReaction)
        {
            djIdleTime = 0;
            djIdleLoops = dj.CompletedLoops;
            return;
        }
        switch (dj.CurrentLabel)
        {
            case "Idle":
                djIdleTime += delta;
                if (dj.CompletedLoops == djIdleLoops) return;
                djIdleLoops = dj.CompletedLoops;
                if (!djSeenEasterEgg && djIdleTime >= 60)
                {
                    djSeenEasterEgg = true;
                    djIdleTime = 0;
                    dj.Play("AFK", false);
                }
                else if (!IsPico && djIdleTime >= 120)
                {
                    djIdleTime = 0;
                    PlayCartoon(0);
                }
                break;
            case "AFK":
                if (dj.Finished)
                {
                    dj.Play("Idle", true);
                    djIdleLoops = 0;
                }
                break;
            case "Watching TV":
                UpdateCartoon();
                break;
        }
    }

    private void PlayCartoon(int start)
    {
        dj.PlayRange("Watching TV", start, -1, false);
        cartoonFrame = start - 1;
    }

    private void UpdateCartoon()
    {
        if (cartoonPending && !tvSound.isPlaying)
        {
            cartoonPending = false;
            LoadCartoon();
        }
        if (cartoonPlaying && !cartoon.isPlaying && !cartoonPending)
        {
            cartoonPlaying = false;
            PlayCartoon(cartoonChangeChannel);
        }
        int current = dj.LabelFrame;
        if (cartoonFrame < cartoonRemoteClick && current >= cartoonRemoteClick)
            tvSound.PlayOneShot(Resources.Load<AudioClip>("VanillaFreeplay/audio/remote_click"));
        if (cartoonFrame < cartoonTvSound && current >= cartoonTvSound)
        {
            tvSound.clip = Resources.Load<AudioClip>("VanillaFreeplay/audio/" + (cartoon.clip == null ? "tv_on" : "channel_switch"));
            tvSound.Play();
            cartoonPending = true;
        }
        cartoonFrame = current;
        if (!dj.Finished) return;
        int start = Random.value < 0.33f ? cartoonBlink : cartoonLoop;
        if (Random.value < 0.1f) start = cartoonChangeChannel;
        PlayCartoon(start);
    }

    private void LoadCartoon()
    {
        if (cartoonClips == null) cartoonClips = Resources.LoadAll<AudioClip>("VanillaFreeplay/audio/cartoons");
        if (cartoonClips.Length == 0) return;
        cartoon.Stop();
        cartoon.clip = cartoonClips[Random.Range(0, cartoonClips.Length)];
        cartoon.time = Random.Range(0, Mathf.Max(0, cartoon.clip.length - 5));
        cartoon.volume = OptionsV2.miscVolume;
        cartoon.Play();
        cartoonPlaying = true;
    }

    private void StopCartoon(bool fade)
    {
        cartoonPending = false;
        cartoonPlaying = false;
        if (tvSound != null) tvSound.Stop();
        cartoonFade = fade ? 0 : -1;
        if (fade || cartoon == null) return;
        cartoon.Stop();
        cartoon.clip = null;
    }
}
