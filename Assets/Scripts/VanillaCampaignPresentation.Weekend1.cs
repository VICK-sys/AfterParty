using System.Collections;
using UnityEngine;

public sealed partial class VanillaCampaignPresentation
{
    private bool weekendCamera;
    private int weekendOutroStep;
    private int weekendOutroBeat = -1;
    private bool weekendNeneAlternate;
    private Vector3 weekendFrom;
    private float weekendFromSize;
    private float weekendReturnAge = -1;

    private IEnumerator DarnellIntro()
    {
        Busy = weekendCamera = true;
        bool hud = IntroHud;
        bool battle = IntroBattle;
        song.uiCamera.enabled = false;
        song.battleCanvas.enabled = false;
        var stage = song.vanillaPlayback.CampaignStage;
        Vector3 pico = stage.CameraTargets[0];
        Vector3 darnell = stage.CameraTargets[1];
        stage.PlayAnimation("bf", "intro1");
        var black = videoHandoffCover != null ? videoHandoffCover : Overlay(Color.black);
        videoHandoffCover = null;
        yield return LoadAudio(8, "darnellCanCutscene", clip => music.clip = clip);
        int step = 0;
        var can = stage.CreateIntroCan();
        can.gameObject.SetActive(false);
        for (float time = 0; time < 10; time += Time.deltaTime)
        {
            if (time >= .7f && !music.isPlaying) music.Play();
            var nene = stage.CharacterGraphic(2);
            if (time >= .7f && nene.Finished)
                stage.PlayAnimation("gf", nene.Animation == "danceLeft" ? "danceRight" : "danceLeft");
            black.color = new Color(0,0,0,1-Mathf.Clamp01((time-1)/2));
            float t = Mathf.Clamp01((time-2)/2.5f);
            float ease = t < .5f ? 2*t*t : 1-Mathf.Pow(-2*t+2,2)/2;
            song.mainCamera.transform.position = Vector3.Lerp(pico + Vector3.right * 2.5f, darnell + Vector3.right, ease);
            song.mainCamera.orthographicSize = Mathf.Lerp(3.6f/1.3f,3.6f/.66f,ease);
            if (time >= 6)
            {
                float back = Mathf.Clamp01((time - 6) / .4f) - 1;
                float amount = 1 + 2.70158f * back * back * back + 1.70158f * back * back;
                song.mainCamera.transform.position = darnell + Vector3.right * Mathf.LerpUnclamped(1, 1.8f, amount);
            }
            if (time >= 5 && step == 0) { step++; stage.PlayAnimation("dad","lightCan"); stage.WeekendSound("Darnell_Lighter"); }
            if (time >= 6 && step == 1) { step++; stage.PlayAnimation("bf","cock"); stage.WeekendSound("Gun_Prep"); }
            if (time >= 6.4f && step == 2)
            {
                step++;
                stage.PlayAnimation("dad","kickCan");
                stage.WeekendSound("Kick_Can_UP");
                can.gameObject.SetActive(true);
                can.Play("up");
            }
            if (time >= 6.9f && step == 3) { step++; stage.PlayAnimation("dad","kneeCan"); stage.WeekendSound("Kick_Can_FORWARD"); can.Play("forward"); }
            if (time >= 7.1f && step == 4)
            {
                step++;
                stage.PlayAnimation("bf","intro2");
                stage.WeekendSound("shot"+Random.Range(1,5));
                can.gameObject.SetActive(false);
                stage.SpawnCan(false);
                stage.ShootCan();
            }
            if (time >= 7.9f && step == 5) { step++; stage.PlayAnimation("dad","laughCutscene"); stage.WeekendSound("darnell_laugh",.6f); }
            if (time >= 8.2f && step == 6) { step++; stage.PlayAnimation("gf","laughCutscene"); stage.WeekendSound("nene_laugh",.6f); }
            can.Advance(Time.deltaTime,song.mainCamera.transform.position,stage.Clock);
            yield return null;
        }
        music.Stop();
        Destroy(can.gameObject);
        Destroy(black.gameObject);
        song.uiCamera.enabled = hud;
        song.battleCanvas.enabled = battle;
        weekendFrom = song.mainCamera.transform.position;
        weekendFromSize = song.mainCamera.orthographicSize;
        weekendReturnAge = 0;
        Busy = false;
    }

    private void ReturnWeekendCamera()
    {
        if (weekendReturnAge < 0) return;
        weekendReturnAge += Time.deltaTime;
        float t = Mathf.Clamp01(weekendReturnAge / 2);
        float eased = (1-Mathf.Cos(t*Mathf.PI))/2;
        song.mainCamera.transform.position = weekendFrom;
        song.mainCamera.orthographicSize = Mathf.Lerp(weekendFromSize, 3.6f/.77f, eased);
        if (t == 1) { weekendReturnAge = -1; weekendCamera = false; }
    }

    private IEnumerator WeekendOutro()
    {
        song.stopwatch.Stop();
        song.beatStopwatch.Stop();
        weekendOutroStep = 0;
        weekendOutroBeat = -1;
        weekendNeneAlternate = false;
        weekendFrom = song.mainCamera.transform.position;
        weekendFromSize = song.mainCamera.orthographicSize;
        weekendCamera = true;
        string id = song.vanillaPlayback.SongId;
        yield return Week7Video(id, 8, id == "2hot" && song.vanillaPlayback.CampaignStage != null ? 6 : 0, keepCovered: true);
        weekendCamera = false;
        OutroFinished = true;
        Busy = false;
    }

    private void WeekendOutroPose(float time)
    {
        if (song.vanillaPlayback.SongId != "2hot") return;
        var stage = song.vanillaPlayback.CampaignStage;
        int beat = Mathf.FloorToInt(time * 86.5f / 60);
        if (beat != weekendOutroBeat)
        {
            if (weekendOutroBeat < 0 || stage.CharacterGraphic(2).Finished)
            {
                stage.PlayAnimation("gf", weekendNeneAlternate ? "danceRight" : "danceLeft");
                weekendNeneAlternate = !weekendNeneAlternate;
            }
            weekendOutroBeat = beat;
        }
        float t = Mathf.Clamp01((time-1)/2);
        float ease = t < .5f ? 2*t*t : 1-Mathf.Pow(-2*t+2,2)/2;
        song.mainCamera.transform.position = Vector3.Lerp(weekendFrom,new Vector3(15.39f,-8.335f,-10),ease);
        song.mainCamera.orthographicSize = Mathf.Lerp(weekendFromSize,3.6f/.69f,ease);
        if (time >= 2 && weekendOutroStep == 0) { weekendOutroStep++; song.vanillaPlayback.CampaignStage.PlayAnimation("bf","intro1"); }
        if (time >= 2.5f && weekendOutroStep == 1) { weekendOutroStep++; song.vanillaPlayback.CampaignStage.PlayAnimation("dad","pissed"); }
    }
}
