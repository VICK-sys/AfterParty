using System.Collections;
using System.Collections.Generic;
using System.IO;
using FridayNightFunkin;
using UnityEngine;

public partial class Song
{
    private readonly List<(NoteObject note, Vector3 origin)> outgoingRestartNotes = new List<(NoteObject, Vector3)>();
    private int deathLoopTween = -1;
    public float RestartDelayRemaining { get; private set; }
    public int RestartCount { get; private set; }

    public void RestartInPlace()
    {
        bool fromDeath = isDead;
        StopAllCoroutines();
        if (deathLoopTween >= 0) LeanTween.cancel(deathLoopTween);
        deathLoopTween = -1;
        ReleaseRestartNotes();
        foreach (NoteObject note in FindObjectsByType<NoteObject>(FindObjectsSortMode.None))
        {
            if (note.dummyNote || note.State == null) continue;
            if (fromDeath) ReleaseFunkinNote(note);
            else
            {
                note.BeginRestartOutgoing();
                outgoingRestartNotes.Add((note, note.transform.position));
            }
        }
        foreach (AudioSource source in musicSources) source.Stop();
        vocalSource.Stop();
        soundSource.Stop();
        oopsSource.Stop();
        subtitleDisplayer.StopSubtitles();
        subtitleDisplayer.paused = false;
        stopwatch.Reset();
        beatStopwatch?.Reset();
        songStarted = isDead = respawning = false;
        ResetFunkinScore();
        Player.instance.ResetSong();
        playerOneStats = new PlayerStat();
        playerTwoStats = new PlayerStat();
        health = MAXHealth / 2;
        currentBeat = _currentRatingLayer = 0;
        beat = altDance = false;
        _currentInterval = _currentBoyfriendIdleTimer = _currentEnemyIdleTimer = 0;
        foreach (var lane in player1NotesObjects) lane.Clear();
        foreach (var lane in player2NotesObjects) lane.Clear();
        jsonDir = Path.Combine(selectedSongDir, "Chart-" + difficulty.ToLowerInvariant() + ".json");
        _song = new FNFSong(jsonDir);
        ChartScrollSpeed = ReadChartScrollSpeed(jsonDir);
        beatsPerSecond = 60 / (float)_song.Bpm;
        stepCrochet = beatsPerSecond * 250;
        _noteBehaviours.Clear();
        foreach (var section in _song.Sections)
            foreach (var data in section.Notes)
                _noteBehaviours.Add(new NoteBehaviour(section, data));
        _noteSchedule.Reset(_noteBehaviours);
        musicSources[0].clip = musicClip;
        musicSources[0].loop = false;
        musicSources[0].volume = OptionsV2.instVolume;
        vocalSource.clip = vocalClip;
        vocalSource.volume = OptionsV2.voicesVolume;
        vocalSource.mute = false;
        if (OpponentVocals != null)
        {
            OpponentVocals.volume = OptionsV2.voicesVolume;
            OpponentVocals.mute = false;
        }
        LeanTween.cancel(mainCamera.gameObject);
        LeanTween.cancel(uiCamera.gameObject);
        LeanTween.cancel(deadCamera.gameObject);
        LeanTween.cancel(deathBlackout.gameObject);
        deathBlackout.color = Color.clear;
        deadCamera.enabled = false;
        mainCamera.enabled = uiCamera.enabled = battleCanvas.enabled = true;
        vanillaPlayback.Presentation.ResetForRetry(this);
        vanillaPlayback.Restart();
        InitializeFunkinStrums();
        InitializeFunkinHud();
        UpdateScoringInfo();
        startSongTooltip.SetActive(false);
        generatingSongMsg.SetActive(false);
        RestartDelayRemaining = .5f;
        BeginFunkinCountdown(beatsPerSecond * 5 + .5f);
        RestartCount++;
        if (fromDeath) vanillaPlayback.Presentation.RevealRetry();
        StartCoroutine(RestartCountdown());
    }

    private IEnumerator RestartCountdown()
    {
        while (RestartDelayRemaining > 0)
        {
            yield return null;
            RestartDelayRemaining = Mathf.Max(0, RestartDelayRemaining - Time.deltaTime);
            float offset = NoteObject.RestartOffset(.5f - RestartDelayRemaining, false, OptionsV2.Downscroll) * FunkinWorldPixelSize;
            foreach (var entry in outgoingRestartNotes)
                if (entry.note != null) entry.note.transform.position = entry.origin + Vector3.up * offset;
        }
        ReleaseRestartNotes();
        yield return vanillaPlayback.Presentation.Countdown(this, true);
        StartSongAudio();
    }

    private void ReleaseRestartNotes()
    {
        foreach (var entry in outgoingRestartNotes)
            if (entry.note != null) ReleaseFunkinNote(entry.note);
        outgoingRestartNotes.Clear();
    }

    public void ConfirmRetry()
    {
        if (!isDead || respawning) return;
        respawning = true;
        musicSources[0].Stop();
        if (deadBoyfriendAnimator.runtimeAnimatorController != null) deadBoyfriendAnimator.Play("Dead Confirm");
        vanillaPlayback?.CharacterStage?.PlayDeath("deathConfirm");
        musicSources[0].PlayOneShot(deadConfirm);
        StartCoroutine(ConfirmRetryRoutine());
    }

    private IEnumerator ConfirmRetryRoutine()
    {
        yield return new WaitForSeconds(deadConfirm != null ? deadConfirm.length / 7 : 0);
        if (vanillaPlayback != null)
        {
            yield return vanillaPlayback.Presentation.RetryFade(0, 1, 2);
            Pause.instance.RestartSong();
        }
        else
        {
            deathBlackout.rectTransform.LeanAlpha(1, 2).setOnComplete(() => Pause.instance.RestartSong());
        }
    }
}
