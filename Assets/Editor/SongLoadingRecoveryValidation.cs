using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SongLoadingRecoveryValidation
{
    private static double deadline;
    private static int phase;
    private static LoadingTransition loading;
    private static bool sawFailure;

    static SongLoadingRecoveryValidation()
    {
        if (!SessionState.GetBool("SongLoadingRecoveryValidation.Active", false)) return;
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run recovery validation in a batch editor.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("SongLoadingRecoveryValidation.Active", true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (message.StartsWith("Song loading failed:")) sawFailure = true;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Song recovery exceeded its deadline.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (phase == 0)
            {
                loading = new GameObject("Loading Probe").AddComponent<LoadingTransition>();
                var host = new GameObject("Song Probe");
                host.SetActive(false);
                var song = host.AddComponent<Song>();
                song.musicSources = Array.Empty<AudioSource>();
                song.selectedSongDir = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
                song.selectedInstrumentalPath = Path.Combine(song.selectedSongDir, "Inst.ogg");
                song.selectedVocalsPath = Path.Combine(song.selectedSongDir, "Voices.ogg");
                var setup = (IEnumerator)typeof(Song).GetMethod("SetupSong", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(song, null);
                loading.StartCoroutine(setup);
                phase++;
            }
            else if (phase == 1)
            {
                var button = loading.GetComponentInChildren<Button>();
                if (button == null) return;
                if (!sawFailure || !loading.toggled) throw new Exception("Missing audio did not enter the visible failure state.");
                var message = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (message == null || !message.text.Contains("Could not load this song.")) throw new Exception("Failure message is missing.");
                button.onClick.Invoke();
                phase++;
            }
            else if (SceneManager.GetActiveScene().name == "Title" && VanillaFreeplay.Active != null && !loading.toggled)
            {
                if (VanillaStoryCampaign.Running) throw new Exception("Failed campaign remains active.");
                Debug.Log("SONG RECOVERY VALIDATION PASSED: missing instrumental, visible failure, return button, Freeplay active, loading overlay dismissed.");
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool("SongLoadingRecoveryValidation.Active", false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(code);
    }
}
