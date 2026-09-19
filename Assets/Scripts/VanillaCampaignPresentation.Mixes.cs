using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class VanillaCampaignPresentation
{
    private IEnumerator StressPicoOutro()
    {
        song.stopwatch.Stop();
        song.beatStopwatch.Stop();
        Player.instance?.ClearInput();
        Busy = weekendCamera = true;
        song.uiCamera.enabled = song.battleCanvas.enabled = false;
        var stage = song.vanillaPlayback.CampaignStage;
        Vector3 from = song.mainCamera.transform.position;
        float size = song.mainCamera.orthographicSize;
        Vector3 target = stage.CameraTargets[1] + new Vector3(3.2f, .7f);
        var fade = Overlay(Color.clear);
        var subtitle = CreateSkipText();
        subtitle.rectTransform.anchoredPosition = new Vector2(40, -624);
        subtitle.rectTransform.sizeDelta = new Vector2(1200, 85);
        subtitle.alignment = TextAnchor.LowerCenter;
        subtitle.fontSize = 30;
        subtitle.color = Color.white;
        var source = new TextAsset(File.ReadAllText(Path.Combine(Root(7), "video/stress-pico-ending.srt")));
        var lines = SRTParser.Load(source);
        Destroy(source);
        yield return LoadAudio(7, "erect/endCutscene", clip => sound.clip = clip);
        stage.PlayAnimation("dad", "stressPicoEnding");
        sound.Play();
        bool laughed = false;
        for (float time = 0; time < 320f / 24; time += Time.deltaTime)
        {
            float t = Mathf.Clamp01(time / 2.8f);
            float ease = t >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * t);
            song.mainCamera.transform.position = Vector3.Lerp(from, target, ease);
            song.mainCamera.orthographicSize = Mathf.Lerp(size, 3.6f / .65f, 1 - Mathf.Pow(2, -10 * Mathf.Clamp01(time / 2)));
            if (!laughed && time >= 176f / 24) { laughed = true; stage.PlayAnimation("bf", "laughEnd"); }
            if (time >= 270f / 24)
            {
                t = Mathf.Clamp01((time - 270f / 24) / 2);
                ease = t < .5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                song.mainCamera.transform.position = target + Vector3.up * (3 * ease);
                fade.color = new Color(0, 0, 0, t);
            }
            subtitle.text = lines.FirstOrDefault(line => sound.time >= line.From && sound.time < line.To)?.Text.Trim() ?? "";
            yield return null;
        }
        sound.Stop();
        Destroy(subtitle.gameObject);
        fade.color = Color.black;
        OutroFinished = true;
        Busy = weekendCamera = false;
    }

    private IEnumerator PicoDoppelgangerIntro()
    {
        Busy = weekendCamera = true;
        bool hud = IntroHud;
        bool battle = IntroBattle;
        song.uiCamera.enabled = song.battleCanvas.enabled = false;
        var stage = song.vanillaPlayback.Week3Stage;
        bool playerShoots = Random.value < .5f;
        bool explode = Random.value < .08f;
        var player = stage.CreateMixEffect("doppleganger", 301);
        var opponent = stage.CreateMixEffect("doppleganger", playerShoots ? 300 : 302);
        var blood = stage.CreateMixEffect("bloodPool", playerShoots ? 299 : 300);
        var cigarette = stage.CreateMixEffect("cigarette", 298);
        player.Position = stage.CharacterGraphic(0).Position + new Vector3(-4.02f, 1.44f);
        opponent.Position = stage.CharacterGraphic(1).Position + new Vector3(-3.33f, 1.445f);
        blood.Position = stage.CharacterGraphic(playerShoots ? 1 : 0).Position + new Vector3(playerShoots ? -.3f : 6.4f, -4.6f);
        cigarette.Position = stage.CharacterGraphic(0).Position + new Vector3(playerShoots ? -.1f : -3.5f, -2.6f);
        cigarette.FlipX = playerShoots;
        blood.gameObject.SetActive(false);
        cigarette.gameObject.SetActive(false);
        player.gameObject.SetActive(false);
        opponent.gameObject.SetActive(false);
        yield return LoadAudio(3, "cutscene/" + (explode ? "cutscene2" : "cutscene"), clip => music.clip = clip);
        var effects = new Dictionary<string, AudioClip>();
        foreach (string name in new[] { "picoGasp", "picoShoot", "picoSpin", "picoCigarette", "picoCigarette2", "picoExplode" })
            yield return LoadAudio(3, "cutscene/" + name, clip => effects[name] = clip);
        float[] times = { .3f, 3.7f, 6.29f, 8.75f, 10.33f };
        string[] names = { "picoGasp", explode ? "picoCigarette2" : "picoCigarette", "picoShoot", explode ? "picoExplode" : "", "picoSpin" };
        int cue = 0;
        int shooter = playerShoots ? 0 : 1;
        Vector3 midpoint = (stage.CameraTargets[0] + stage.CameraTargets[1]) / 2;
        stage.HideMixActor(0, true);
        stage.HideMixActor(1, true);
        player.gameObject.SetActive(true);
        opponent.gameObject.SetActive(true);
        player.Play((playerShoots ? "shoot" : explode ? "explode" : "cigarette") + "Player");
        opponent.Play((!playerShoots ? "shoot" : explode ? "explode" : "cigarette") + "Opponent");
        ReleaseIntroCover();
        music.loop = false;
        music.Play();
        int lastCutsceneBeat = -1;
        bool skipped = false;
        bool spat = false;
        float skipAt = -1;
        var skip = CreateSkipText();
        for (float time = 0; time < 13; time += Time.deltaTime)
        {
            int cutsceneBeat = Mathf.FloorToInt(music.time * 2.5f);
            if (cutsceneBeat != lastCutsceneBeat)
            {
                lastCutsceneBeat = cutsceneBeat;
                stage.MixIntroBeat();
            }
            while (cue < times.Length && time >= times[cue])
            {
                if (names[cue] != "") sound.PlayOneShot(effects[names[cue]]);
                cue++;
            }
            if ((Advance || Input.GetKeyDown(Player.pauseKey) || Player.ControllerPausePressed) && time < 8.75f)
            {
                if (skipAt >= 0 && time - skipAt >= .5f) { skipped = true; break; }
                if (skipAt < 0) skipAt = time;
            }
            skip.color = new Color(1, 1, 1, skipAt >= 0 && time < 8.75f ? Mathf.Clamp01((time - skipAt) / .5f) : 0);
            Vector3 target = time < 4 ? midpoint : time < 6.3f ? stage.CameraTargets[1 - shooter] : time < 8.75f ? stage.CameraTargets[shooter] : stage.CameraTargets[1 - shooter];
            song.mainCamera.transform.position = Vector3.Lerp(song.mainCamera.transform.position, target, 1 - Mathf.Pow(.96f, Time.deltaTime * 60));
            if (explode && time >= 8.75f && !spat) { spat = true; stage.PlayAnimation("gf", "drop70"); }
            if (explode && time >= 11.2f)
            {
                if (!blood.gameObject.activeSelf) { blood.gameObject.SetActive(true); blood.Play("poolAnim"); }
            }
            if (!explode && time >= 11.5f && !spat)
            {
                spat = true;
                cigarette.gameObject.SetActive(true);
                cigarette.Play("cigarette spit");
            }
            if (explode)
            {
                var victim = playerShoots ? opponent : player;
                string loop = "loop" + (playerShoots ? "Opponent" : "Player");
                if (victim.Finished && victim.Has(loop)) victim.Play(loop);
            }
            yield return null;
        }
        UnityEngine.UI.Image skipFade = null;
        if (skipped)
        {
            skipFade = Overlay(Color.clear);
            for (float time = 0; time < .5f; time += Time.deltaTime)
            {
                skipFade.color = new Color(0, 0, 0, time / .5f);
                music.volume = 1 - time / .5f;
                yield return null;
            }
        }
        music.Stop();
        music.volume = 1;
        sound.Stop();
        Destroy(skip.gameObject);
        if (skipped || !explode)
        {
            stage.HideMixActor(0, false);
            stage.HideMixActor(1, false);
            player.gameObject.SetActive(false);
            opponent.gameObject.SetActive(false);
            blood.gameObject.SetActive(false);
        }
        else
        {
            stage.OpponentExploded = playerShoots;
            if (playerShoots && song.OpponentVocals != null) song.OpponentVocals.mute = true;
            stage.HideMixActor(shooter, false);
            (playerShoots ? player : opponent).gameObject.SetActive(false);
            if (!playerShoots)
            {
                var fade = Overlay(Color.clear);
                for (float time = 0; time < 2; time += Time.deltaTime)
                {
                    fade.color = new Color(0, 0, 0, Mathf.Clamp01(time - 1));
                    yield return null;
                }
                song.CompleteScriptedSong();
                yield break;
            }
        }
        song.uiCamera.enabled = hud;
        song.battleCanvas.enabled = battle;
        Busy = weekendCamera = false;
        if (skipFade != null) StartCoroutine(FadeMixSkip(skipFade));
    }

    private IEnumerator FadeMixSkip(UnityEngine.UI.Image overlay)
    {
        for (float time = 0; time < .5f; time += Time.deltaTime)
        {
            overlay.color = new Color(0, 0, 0, 1 - time / .5f);
            yield return null;
        }
        Destroy(overlay.gameObject);
    }
}
