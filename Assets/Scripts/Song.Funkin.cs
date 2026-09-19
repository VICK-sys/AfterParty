using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public partial class Song
{
    public AudioSource OpponentVocals { get; private set; }
    public string SplitPlayerVocalsPath { get; private set; }

    private IEnumerator LoadSplitVocals()
    {
        string player = Path.Combine(selectedSongDir, Path.GetFileName(selectedVocalsPath).Replace("Voices", "Voices-player"));
        string opponent = Path.Combine(selectedSongDir, Path.GetFileName(selectedVocalsPath).Replace("Voices", "Voices-opponent"));
        SplitPlayerVocalsPath = null;
        if (OpponentVocals != null) OpponentVocals.Stop();
        if (!File.Exists(player) || !File.Exists(opponent))
        {
            if (OpponentVocals != null)
            {
                musicSources = musicSources.Where(source => source != OpponentVocals).ToArray();
                if (OpponentVocals.clip != null) Destroy(OpponentVocals.clip);
                Destroy(OpponentVocals);
                OpponentVocals = null;
            }
            yield break;
        }
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(opponent).AbsoluteUri, AudioType.OGGVORBIS))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
            if (OpponentVocals == null)
            {
                OpponentVocals = gameObject.AddComponent<AudioSource>();
                OpponentVocals.playOnAwake = false;
                OpponentVocals.outputAudioMixerGroup = vocalSource.outputAudioMixerGroup;
                musicSources = musicSources.Concat(new[] { OpponentVocals }).ToArray();
            }
            else if (OpponentVocals.clip != null) Destroy(OpponentVocals.clip);
            OpponentVocals.clip = DownloadHandlerAudioClip.GetContent(request);
            SplitPlayerVocalsPath = player;
        }
    }

    private void SetFunkinVocalMuted(int side, bool muted)
    {
        if (side == 1 && vanillaPlayback?.Week3Stage?.OpponentExploded == true) muted = true;
        if (OpponentVocals != null && SplitPlayerVocalsPath != null)
            (side == 0 ? vocalSource : OpponentVocals).mute = muted;
        else if (hasVoiceLoaded) vocalSource.mute = muted;
    }
    private readonly double[] funkinScores = new double[2];
    private double countdownEnd;
    private double? countdownPausedAt;
    public bool IsCountingDown { get; private set; }
    public bool FreeplayAborted { get; set; }
    public double SongPosition => IsCountingDown ? ((countdownPausedAt ?? Time.realtimeSinceStartupAsDouble) - countdownEnd) * 1000 + Pause.GlobalOffset :
        stopwatch == null ? 0 : stopwatch.Elapsed.TotalMilliseconds + Pause.GlobalOffset;
    public float ChartScrollSpeed { get; private set; } = 1;
    public float FunkinScrollSpeed => Math.Max(0.01f, ChartScrollSpeed - speedDifference * 100);
    public static float ReadChartScrollSpeed(string path)
    {
        return (float?)JObject.Parse(File.ReadAllText(path))["song"]?["speed"] ?? 1;
    }
    public double FunkinRenderDistance => 720 / FunkinRules.PixelsPerMillisecond / Math.Min(FunkinScrollSpeed, 1);
    private readonly FunkinStrumEffect[,] strumEffects = new FunkinStrumEffect[2, 4];
    public FunkinHud FunkinHud { get; private set; }
    public float HudReferenceOrthographicSize => _defaultZoom;
    public float FunkinWorldPixelSize => (vanillaPlayback != null && vanillaPlayback.UsesSourceCamera
        ? HudReferenceOrthographicSize : uiCamera.orthographicSize) * 2 / 720;

    private void InitializeFunkinHud()
    {
        if (FunkinHud == null)
        {
            var root = new GameObject("Funkin HUD");
            root.transform.SetParent(transform, false);
            FunkinHud = root.AddComponent<FunkinHud>();
        }
        FunkinHud.Initialize(this, _song.Player2);
    }

    public void ResetFunkinScore()
    {
        FreeplayAborted = false;
        funkinScores[0] = funkinScores[1] = 0;
        IsCountingDown = false;
        countdownPausedAt = null;
    }

    public void BeginFunkinCountdown(double seconds)
    {
        countdownEnd = Time.realtimeSinceStartupAsDouble + seconds;
        IsCountingDown = true;
    }

    public void SetCountdownPaused(bool paused)
    {
        if (!IsCountingDown) return;
        if (paused && countdownPausedAt == null) countdownPausedAt = Time.realtimeSinceStartupAsDouble;
        else if (!paused && countdownPausedAt != null)
        {
            countdownEnd += Time.realtimeSinceStartupAsDouble - countdownPausedAt.Value;
            countdownPausedAt = null;
        }
    }

    public void InitializeFunkinStrums()
    {
        FunkinNoteSkin.WarmGameplay();
        if (GetComponent<FunkinStrumlineBackground>() == null) gameObject.AddComponent<FunkinStrumlineBackground>().Initialize(this);
        for (int side = 0; side < 2; side++)
        {
            var sprites = side == 0 ? player1NoteSprites : player2NoteSprites;
            var animators = side == 0 ? player1NotesAnimators : player2NotesAnimators;
            var line = Player.instance.Strumlines[side];
            line.RenderDistance = FunkinRenderDistance;
            for (int direction = 0; direction < 4; direction++)
            {
                animators[direction].enabled = false;
                sprites[direction].transform.rotation = Quaternion.identity;
                sprites[direction].transform.localScale = Vector3.one;
                sprites[direction].color = Color.white;
                sprites[direction].sprite = FunkinNoteSkin.Receptor(direction, FunkinStrumline.Animation.Static, 0);
                line.ConfirmDurations[direction] = FunkinNoteSkin.ConfirmDuration(direction);
                if (strumEffects[side, direction] == null)
                    strumEffects[side, direction] = sprites[direction].gameObject.AddComponent<FunkinStrumEffect>();
                strumEffects[side, direction].Initialize(direction, side, strumEffects[side, 0]);
            }
        }
        RefreshFunkinStrums();
    }

    public void RefreshFunkinStrums()
    {
        if (Player.instance == null) return;
        for (int side = 0; side < 2; side++)
        {
            var sprites = side == 0 ? player1NoteSprites : player2NoteSprites;
            var line = Player.instance.Strumlines[side];
            for (int direction = 0; direction < 4; direction++)
            {
                SpriteRenderer sprite = sprites[direction];
                sprite.sprite = FunkinNoteSkin.Receptor(direction, line.Animations[direction], line.AnimationTimes[direction]);
                float worldScale = FunkinWorldPixelSize;
                FunkinNoteSkin.WorldScale(sprite.transform, 100 * worldScale * FunkinNoteSkin.Scale);
                float x = (side == 0 ? 688 : 48) + 112 * direction + 52;
                if (OptionsV2.Middlescroll || vanillaPlayback?.SongId == "blazin") x = 420 + 112 * direction + 52;
                float y = OptionsV2.Downscroll ? 720 - 24 - 82.6f : 24 + 82.6f;
                Vector3 point = new Vector3(uiCamera.transform.position.x + (x - 640) * worldScale,
                    uiCamera.transform.position.y + (360 - y) * worldScale, sprite.transform.position.z);
                sprite.transform.position = point;
                if ((vanillaPlayback?.SongId == "blazin" || vanillaPlayback?.IsSpaghetti == true) && side == 1) sprite.enabled = false;
            }
        }
    }

    public void ApplyFunkinHit(NoteObject note, double timing, bool automatic)
    {
        if (note == null) return;
        int side = note.mustHit ? 0 : 1;
        if (vanillaPlayback?.CampaignStage?.Week == 8) vanillaPlayback.CampaignStage.WeekendJudgement = automatic ? FunkinRules.Judgement.Sick : FunkinRules.Judge(timing);
        if (vanillaPlayback?.CharacterStage != null) vanillaPlayback.CharacterStage.Hit(side, note.type, note.State.Time);
        else PlayFunkinCharacter(side, note.type, false);
        SetFunkinVocalMuted(side, false);
        if (CameraMovement.instance != null) CameraMovement.instance.focusOnPlayerOne = note.layer == 1;
        RemoveFunkinHead(note);
        if (note.State.Length > 0) strumEffects[side, note.type]?.Cover(note.State);
        if (automatic || !note.State.Scoreable) return;
        var judgement = FunkinRules.Judge(timing);
        PlayerStat stats = side == 0 ? playerOneStats : playerTwoStats;
        stats.totalNoteHits++;
        stats.hitNotes++;
        switch (judgement)
        {
            case FunkinRules.Judgement.Sick: stats.totalSicks++; break;
            case FunkinRules.Judgement.Good: stats.totalGoods++; break;
            case FunkinRules.Judgement.Bad: stats.totalBads++; break;
            case FunkinRules.Judgement.Shit: stats.totalShits++; break;
            case FunkinRules.Judgement.Miss: stats.missedHits++; break;
        }
        if (FunkinRules.BreaksCombo(judgement)) BreakFunkinCombo(side);
        else stats.currentCombo++;
        if (side == 0) vanillaPlayback?.CharacterStage?.Combo(stats.currentCombo, false);
        stats.highestCombo = Math.Max(stats.highestCombo, stats.currentCombo);
        AddFunkinScore(side, FunkinRules.Score(timing), FunkinRules.Health(judgement));
        FunkinHud?.ShowRating(judgement);
        if (stats.currentCombo >= 10) FunkinHud?.ShowCombo(stats.currentCombo);
        if (judgement == FunkinRules.Judgement.Sick) strumEffects[side, note.type]?.Splash();
    }

    public void ApplyFunkinMiss(NoteObject note)
    {
        int side = note.mustHit ? 0 : 1;
        RemoveFunkinHead(note);
        if (!note.State.Scoreable) return;
        PlayerStat stats = side == 0 ? playerOneStats : playerTwoStats;
        stats.totalNoteHits++;
        stats.missedHits++;
        BreakFunkinCombo(side);
        AddFunkinScore(side, -100, -8);
        if (vanillaPlayback?.CampaignStage?.Week == 8 || vanillaPlayback?.IsSpaghetti == true) vanillaPlayback.CampaignStage.Miss(side, note.type, note.State.Time);
        else PlayFunkinCharacter(side, note.type, true);
        PlayFunkinMissSound(side, 0.5f, 0.6f);
    }

    public void ApplyFunkinGhost(int side, int direction)
    {
        AddFunkinScore(side, -10, -8);
        PlayFunkinCharacter(side, direction, true);
        PlayFunkinMissSound(side, 0.1f, 0.2f);
    }

    public void ApplyFunkinHold(int side, double elapsed)
    {
        AddFunkinScore(side, 250 * elapsed, 12 * elapsed);
        KeepFunkinHoldPose(side);
    }

    public void KeepFunkinHoldPose(int side)
    {
        vanillaPlayback?.CharacterStage?.Hold(side);
        if (side == 0) _currentBoyfriendIdleTimer = boyfriendIdleTimer;
        else _currentEnemyIdleTimer = enemyIdleTimer;
    }

    public void ApplyFunkinDrop(int side, double penalty)
    {
        BreakFunkinCombo(side);
        AddFunkinScore(side, penalty, 0);
        PlayFunkinMissSound(side, 0.5f, 0.6f);
    }

    private void AddFunkinScore(int side, double score, double healthChange)
    {
        funkinScores[side] += score;
        (side == 0 ? playerOneStats : playerTwoStats).currentScore = (int)funkinScores[side];
        health += (float)(side == 0 ? healthChange : -healthChange);
        UpdateScoringInfo();
    }

    private void BreakFunkinCombo(int side)
    {
        PlayerStat stats = side == 0 ? playerOneStats : playerTwoStats;
        if (side == 0) vanillaPlayback?.CharacterStage?.Combo(stats.currentCombo, true);
        if (stats.currentCombo >= 10) FunkinHud?.ShowCombo(0);
        stats.currentCombo = 0;
    }

    private void PlayFunkinCharacter(int side, int direction, bool miss)
    {
        if (vanillaPlayback?.CharacterStage != null)
        {
            vanillaPlayback.CharacterStage.Sing(side, direction, miss);
            return;
        }
        string[] directions = { "Left", "Down", "Up", "Right" };
        if (side == 0) BoyfriendPlayAnimation("Sing " + directions[direction] + (miss ? " Miss" : ""));
        else EnemyPlayAnimation("Sing " + directions[direction]);
    }

    private void PlayFunkinMissSound(int side, float min, float max)
    {
        SetFunkinVocalMuted(side, true);
        if (noteMissClip.Length > 0) oopsSource.PlayOneShot(noteMissClip[UnityEngine.Random.Range(0, noteMissClip.Length)], UnityEngine.Random.Range(min, max));
    }

    private void RemoveFunkinHead(NoteObject note)
    {
        (note.mustHit ? player1NotesObjects : player2NotesObjects)[note.type].Remove(note);
    }

    public void ReleaseFunkinNote(NoteObject note)
    {
        RemoveFunkinHead(note);
        Player.instance?.Strumlines[note.mustHit ? 0 : 1].Notes.Remove(note.State);
        var pool = note.type == 0 ? leftNotesPool : note.type == 1 ? downNotesPool : note.type == 2 ? upNotesPool : rightNotesPool;
        pool.Release(note.gameObject);
    }
}
