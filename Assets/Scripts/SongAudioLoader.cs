using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class SongAudioLoader
{
    public sealed class Result
    {
        public AudioClip Clip;
        public string Error;
    }

    public static IEnumerator Load(string path, Result result, float timeoutSeconds = 30)
    {
        SongLoadingDiagnostics.Record("audio begin: " + path);
        result.Clip = null;
        result.Error = null;
        UnityWebRequest request = null;
        UnityWebRequestAsyncOperation operation = null;
        try
        {
            string uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS);
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
            operation = request.SendWebRequest();
        }
        catch (Exception exception)
        {
            result.Error = exception.Message;
        }

        using (request)
        {
            if (result.Error != null) yield break;
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    request.Abort();
                    result.Error = "Audio request timed out.";
                    yield break;
                }
                yield return null;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                result.Error = request.error;
                yield break;
            }
            try
            {
                result.Clip = DownloadHandlerAudioClip.GetContent(request);
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                yield break;
            }
            if (result.Clip == null)
            {
                result.Error = "Audio decoder returned no clip.";
                yield break;
            }
            while (result.Clip.loadState != AudioDataLoadState.Loaded)
            {
                if (result.Clip.loadState == AudioDataLoadState.Failed || Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    result.Error = result.Clip.loadState == AudioDataLoadState.Failed
                        ? "Audio decoding failed." : "Audio decoding timed out.";
                    UnityEngine.Object.Destroy(result.Clip);
                    result.Clip = null;
                    yield break;
                }
                yield return null;
            }
            SongLoadingDiagnostics.Record("audio ready: " + path);
        }
    }
}
