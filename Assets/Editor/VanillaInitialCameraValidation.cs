using System;
using UnityEngine;

public static class VanillaInitialCameraValidation
{
    private static Song observedSong;
    private static int samples;
    private static bool restarted;
    private static bool complete;

    public static bool Tick()
    {
        if (Environment.GetEnvironmentVariable("UNITY_PARTY_INITIAL_CAMERA_TEST") != "1") return false;
        Song song = Song.instance;
        if (song == null || song.vanillaPlayback?.CampaignStage == null) return false;
        if (song != observedSong)
        {
            observedSong = song;
            samples = 0;
            restarted = false;
            complete = false;
        }
        if (complete) return false;
        if (OptionsV2.LiteMode || OptionsV2.Middlescroll) throw new Exception("Initial camera probe requires standard camera options.");
        var targets = song.vanillaPlayback.CampaignStage.CameraTargets;
        if (Vector3.Distance(targets[1], targets[2]) < 1) throw new Exception("Initial camera control cannot distinguish opponent and girlfriend.");
        if (song.IsCountingDown && !song.vanillaPlayback.Presentation.OwnsCamera)
        {
            Vector3 actual = song.mainCamera.transform.position;
            if (Vector3.Distance(actual, targets[1]) > .01f)
                throw new Exception("Initial camera differs from opponent: actual=" + actual + ", expected=" + targets[1] + ", retry=" + restarted);
            samples++;
        }
        if (!song.songStarted) return false;
        if (samples < 2) throw new Exception("Initial camera countdown was not observed.");
        Debug.Log("INITIAL CAMERA VERIFIED: song=" + song.vanillaPlayback.SongId + ", retry=" + restarted + ", samples=" + samples);
        if (!restarted)
        {
            song.RestartInPlace();
            restarted = true;
            samples = 0;
            return true;
        }
        complete = true;
        return false;
    }
}
