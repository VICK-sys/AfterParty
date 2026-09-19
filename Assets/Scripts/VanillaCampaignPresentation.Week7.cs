using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed partial class VanillaCampaignPresentation
{
    public bool VideoActive => video != null && video.isPrepared && Busy;
    public VideoPlayer Video => video;
    public float VideoTime => sound == null ? 0 : sound.time;
    private VideoPlayer video;
    private RenderTexture videoTexture;
    private bool skipVideo;
    private string videoError;
    private Image videoHandoffCover;

    private IEnumerator Week7Video(string id, int week = 7, float hideFor = 0, bool keepCovered = false)
    {
        Busy = true;
        skipVideo = false;
        videoError = null;
        bool hud = IntroHud;
        bool battle = IntroBattle;
        song.uiCamera.enabled = false;
        song.battleCanvas.enabled = false;
        var black = TakeIntroCover(Color.black);
        var image = Rect("Week 7 Video", viewport, 0, 0, 1280, 720).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
        videoTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
        videoTexture.Create();
        image.texture = videoTexture;
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.source = VideoSource.Url;
        string censored = Path.Combine(Application.streamingAssetsPath,"VanillaOptions",id+"-censored");
        bool useCensored = !VanillaPreferences.Naughtyness && File.Exists(censored+".mp4");
        video.url = new Uri(useCensored ? censored+".mp4" : Path.Combine(Root(week), "video", id + ".mp4")).AbsoluteUri;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = videoTexture;
        video.audioOutputMode = VideoAudioOutputMode.None;
        video.waitForFirstFrame = true;
        video.skipOnDrop = true;
        video.timeReference = VideoTimeReference.ExternalTime;
        video.errorReceived += (_, message) => videoError = message;
        var subtitle = Rect("Cutscene Subtitles", viewport, 40, 624, 1200, 85).gameObject.AddComponent<Text>();
        subtitle.font = Resources.Load<Font>("FunkinHud/Countdown/vcr");
        subtitle.fontSize = 30;
        subtitle.alignment = TextAnchor.LowerCenter;
        subtitle.color = Color.white;
        subtitle.raycastTarget = false;
        subtitle.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
        string subtitlePath = useCensored ? censored+".srt" : Path.Combine(Root(week), "video", id + ".srt");
        var text = new TextAsset(File.Exists(subtitlePath) ? File.ReadAllText(subtitlePath) : "");
        var subtitles = SRTParser.Load(text);
        Destroy(text);
        if (useCensored)
        {
            using (var request = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(new Uri(censored+".wav").AbsoluteUri,AudioType.WAV))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success) throw new IOException(request.error);
                sound.clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);
                clips.Add(sound.clip);
            }
        }
        else yield return LoadAudio(week, id + "Cutscene", clip => sound.clip = clip, UnityEngine.AudioType.WAV);
        video.Prepare();
        float deadline = Time.realtimeSinceStartup + 30;
        while (!video.isPrepared && videoError == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (!video.isPrepared)
            throw new InvalidOperationException("Cannot prepare Week 7 cutscene: " + (videoError ?? id));
        while (Pause.instance != null && Pause.instance.IsPaused) yield return null;
        video.Play();
        sound.Play();
        while (!skipVideo && (sound.isPlaying || Pause.instance != null && Pause.instance.IsPaused))
        {
            if (videoError != null) throw new InvalidOperationException(videoError);
            video.externalReferenceTime = sound.time;
            image.enabled = video.frame >= 0 && sound.time >= hideFor;
            black.enabled = sound.time >= hideFor;
            if (hideFor > 0) WeekendOutroPose(sound.time);
            var line = subtitles.FirstOrDefault(item => sound.time >= item.From && sound.time < item.To);
            subtitle.text = VanillaPreferences.Subtitles ? Regex.Replace(line?.Text ?? "", "<[^>]+>", "").Trim() : "";
            yield return null;
        }
        sound.Stop();
        video.Stop();
        Destroy(subtitle.gameObject);
        Destroy(image.gameObject);
        song.uiCamera.enabled = hud;
        song.battleCanvas.enabled = battle;
        if (keepCovered)
        {
            black.enabled = true;
            black.color = Color.black;
            videoHandoffCover = black;
            ReleaseVideo();
            yield break;
        }
        for (float elapsed = 0; elapsed < .5f; elapsed += Time.deltaTime)
        {
            float t = elapsed / .5f;
            float ease = t < .5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
            black.color = new Color(0, 0, 0, 1 - ease);
            yield return null;
        }
        Destroy(black.gameObject);
        ReleaseVideo();
        song.uiCamera.enabled = hud;
        song.battleCanvas.enabled = battle;
        Busy = false;
    }

    private IEnumerator RevealVideoGameplay(Image cover)
    {
        double start = song.SongPosition;
        while (song.SongPosition - start < 500)
        {
            float t = Mathf.Clamp01((float)((song.SongPosition - start) / 500));
            float ease = t < .5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
            cover.color = new Color(0, 0, 0, 1 - ease);
            yield return null;
        }
        Destroy(cover.gameObject);
    }

    public void PauseVideo(bool paused)
    {
        if (video == null || !video.isPrepared) return;
        if (paused) video.Pause();
        else video.Play();
    }

    public void SkipVideo() => skipVideo = true;

    public void RestartVideo()
    {
        if (video == null || !video.isPrepared) return;
        sound.time = 0;
        video.time = 0;
        video.externalReferenceTime = 0;
        weekendOutroStep = 0;
        weekendOutroBeat = -1;
        weekendNeneAlternate = false;
    }

    private void ReleaseVideo()
    {
        if (video != null)
        {
            video.Stop();
            video.targetTexture = null;
            Destroy(video);
            video = null;
        }
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
            videoTexture = null;
        }
    }
}
