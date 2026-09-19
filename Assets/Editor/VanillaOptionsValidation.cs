using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VanillaOptionsValidation
{
    private static int phase, assertions, errors;
    private static double started, changed;
    private static bool finishing;
    private static MenuV2 menu;
    private static VanillaOptionsMenu options;
    private static Gamepad pad;
    private static InputSettings.BackgroundBehavior backgroundBehavior;
    private static InputSettings.EditorInputBehaviorInPlayMode editorInputBehavior;
    private static readonly Dictionary<string,string> strings = new Dictionary<string,string>();
    private static readonly Dictionary<string,int?> integers = new Dictionary<string,int?>();
    private static string Output => Environment.GetEnvironmentVariable("UNITY_PARTY_OPTIONS_TEST_PATH") ?? Path.GetFullPath("Builds/OptionsValidation");

    static VanillaOptionsValidation()
    {
        if (!SessionState.GetBool("VanillaOptionsValidation.Active",false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run options validation in an isolated batch editor.");
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        SessionState.SetBool("VanillaOptionsValidation.Active",true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message,string stack,LogType type)
    {
        if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    private static void Require(bool condition,string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
    private static void Next() { phase++; changed=EditorApplication.timeSinceStartup; }

    private static void Backup()
    {
        backgroundBehavior=InputSystem.settings.backgroundBehavior;
        editorInputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach (string key in new[]{"Funkin.Controls","Saved Keybinds","MiscOptions"}) strings[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetString(key):null;
        foreach (var pref in VanillaPreferences.Items)
        {
            string key="Funkin.Options."+pref.id;
            integers[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):(int?)null;
        }
        integers["Funkin.GlobalOffset"]=PlayerPrefs.HasKey("Funkin.GlobalOffset")?Pause.GlobalOffset:(int?)null;
        File.WriteAllText(Path.Combine(Output,"settings-backup.json"),JsonConvert.SerializeObject(new { strings,integers },Formatting.Indented));
        foreach (var pref in VanillaPreferences.Items) VanillaPreferences.Set(pref,pref.initial);
        PlayerPrefs.DeleteKey("Funkin.Controls");
        PlayerPrefs.DeleteKey("Saved Keybinds");
        VanillaControls.Reload();
    }

    private static void CheckVolumeTray()
    {
        var runtime = Object.FindAnyObjectByType<VanillaOptionsRuntime>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var volume = typeof(VanillaOptionsRuntime).GetField("masterVolume", flags);
        var mute = typeof(VanillaOptionsRuntime).GetField("masterMuted", flags);
        object originalVolume = volume.GetValue(runtime), originalMute = mute.GetValue(runtime);
        float listener = AudioListener.volume, scale = Time.timeScale;
        float? savedVolume = PlayerPrefs.HasKey("Funkin.MasterVolume") ? PlayerPrefs.GetFloat("Funkin.MasterVolume") : (float?)null;
        int? savedMute = PlayerPrefs.HasKey("Funkin.MasterMuted") ? PlayerPrefs.GetInt("Funkin.MasterMuted") : (int?)null;
        try
        {
            var bars = (RawImage[])typeof(VanillaOptionsRuntime).GetField("volumeBars", flags).GetValue(runtime);
            var tray = (RectTransform)typeof(VanillaOptionsRuntime).GetField("volumeTray", flags).GetValue(runtime);
            var tick = typeof(VanillaOptionsRuntime).GetMethod("UpdateVolumeTray", flags);
            for (int i = 0; i < 11; i++) runtime.ChangeMasterVolume(1);
            Require(AudioListener.volume == 1 && bars.All(bar => bar.enabled), "Maximum volume or ten-bar display failed.");
            runtime.ChangeMasterVolume(-1);
            Require(Mathf.Approximately(AudioListener.volume, .9f) && bars.Count(bar => bar.enabled) == 9, "Volume decrement failed.");
            runtime.ChangeMasterVolume(0);
            Time.timeScale = 0;
            tick.Invoke(runtime, new object[] { 2f });
            Require(AudioListener.volume == 0 && bars.All(bar => !bar.enabled) && tray.gameObject.activeSelf && tray.anchoredPosition.y < 0,
                "Muted tray did not remain visible while paused.");
            runtime.ChangeMasterVolume(0);
            Require(Mathf.Approximately(AudioListener.volume, .9f), "Unmute lost the previous volume.");
            tick.Invoke(runtime, new object[] { 2f });
            Require(!tray.gameObject.activeSelf, "Audible tray did not hide after its timeout.");
            for (int i = 0; i < 11; i++) runtime.ChangeMasterVolume(-1);
            Require(AudioListener.volume == 0 && PlayerPrefs.GetFloat("Funkin.MasterVolume") == 0, "Minimum volume was not clamped and saved.");
        }
        finally
        {
            volume.SetValue(runtime, originalVolume);
            mute.SetValue(runtime, originalMute);
            typeof(VanillaOptionsRuntime).GetMethod("ApplyMasterVolume", flags).Invoke(runtime, null);
            AudioListener.volume = listener;
            Time.timeScale = scale;
            if (savedVolume.HasValue) PlayerPrefs.SetFloat("Funkin.MasterVolume", savedVolume.Value);
            else PlayerPrefs.DeleteKey("Funkin.MasterVolume");
            if (savedMute.HasValue) PlayerPrefs.SetInt("Funkin.MasterMuted", savedMute.Value);
            else PlayerPrefs.DeleteKey("Funkin.MasterMuted");
            PlayerPrefs.Save();
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || finishing) return;
        if (started==0) started=changed=EditorApplication.timeSinceStartup;
        double wait=EditorApplication.timeSinceStartup-changed;
        try
        {
            if (EditorApplication.timeSinceStartup-started>120) throw new InvalidOperationException("Options validation timed out.");
            switch (phase)
            {
                case 0:
                    if (wait<2 || VanillaTitleScreen.Active==null) return;
                    Backup();
                    CheckVolumeTray();
                    menu=MenuV2.Instance;
                    VanillaTitleScreen.Active.Accept();
                    VanillaTitleScreen.Active.Accept();
                    VanillaTitleScreen.Active.Accept();
                    Next();
                    break;
                case 1:
                    if (VanillaTitleScreen.Active!=null || VanillaTitleTransition.BlocksInput) return;
                    menu.vanillaMenu.MoveSelection(3-menu.vanillaMenu.SelectedIndex);
                    menu.vanillaMenu.ConfirmSelection();
                    Next();
                    break;
                case 2:
                    options=VanillaOptionsMenu.Active;
                    if (options==null || wait<2) return;
                    Require(!menu.mainScreen.gameObject.activeSelf && !menu.optionsScreen.gameObject.activeSelf,"Options left a legacy menu visible.");
                    Require(options.Labels.SequenceEqual(new[]{"PREFERENCES","CONTROLS","LAG ADJUSTMENT","CLEAR SAVE DATA","EXIT"}),"Root page order differs.");
                    Require(options.GetComponentsInChildren<VanillaOptionsText>().All(text=>text.GetComponent<CanvasRenderer>()!=null),"An options label lacks a canvas renderer.");
                    Capture("root");
                    Capture("blank-control",true);
                    options.Confirm();
                    Require(options.Busy,"Root confirmation skipped the flicker delay.");
                    Next();
                    break;
                case 3:
                    if (options.CurrentPage!=VanillaOptionsMenu.Page.Preferences || options.Busy || wait<2) return;
                    Require(options.Labels.Length==17,"Desktop preferences are missing.");
                    Capture("preferences-top");
                    options.MoveSelection(1);
                    options.Confirm();
                    Next();
                    break;
                case 4:
                    if (options.Busy) return;
                    Require(OptionsV2.Downscroll && PlayerPrefs.GetInt("Funkin.Options.Downscroll")==1,"Downscroll did not persist or apply.");
                    options.MoveSelection(1);
                    options.Adjust(1);
                    Require(VanillaPreferences.Get("StrumlineBackground")==10,"Percentage increment differs.");
                    options.Adjust(-1);
                    options.Adjust(-1);
                    Require(VanillaPreferences.Get("StrumlineBackground")==0,"Percentage went below zero.");
                    options.MoveSelection(10);
                    options.Adjust(1);
                    Require(Application.targetFrameRate==65,"FPS did not apply.");
                    Next();
                    break;
                case 5:
                    if (wait<1) return;
                    Require(options.CameraScroll>700,"Preference camera did not follow the selection.");
                    Capture("preferences-scrolled");
                    options.Back();
                    options.MoveSelection(1-options.SelectedIndex);
                    options.Confirm();
                    Next();
                    break;
                case 6:
                    if (options.CurrentPage!=VanillaOptionsMenu.Page.Controls || options.Busy) return;
                    Capture("controls-keyboard");
                    options.OpenRebindPrompt();
                    Capture("rebind-prompt");
                    Require(options.Rebinding && VanillaControls.Capturing,"Rebinding did not block global shortcuts.");
                    options.ClosePrompt();
                    Require(VanillaControls.Rebind(0,0,(int)KeyCode.S,false),"Binding swap was rejected.");
                    Require(VanillaControls.Bindings[0].keys[0]==(int)KeyCode.S && VanillaControls.Bindings[1].keys[0]==(int)KeyCode.A,"Note collision did not swap the old input.");
                    Require(VanillaControls.Bindings[5].keys[0]==(int)KeyCode.S,"Note binding changed a different control group.");
                    VanillaControls.Reload();
                    Require(Player.primaryKeyCodes[0]==KeyCode.S && VanillaControls.Bindings[1].keys[0]==(int)KeyCode.A,"Bindings did not survive a reload.");
                    Require(VanillaControls.Rebind(4,1,-1,false),"Could not remove a secondary UI binding.");
                    Require(!VanillaControls.Rebind(4,0,-1,false),"Last UI binding could be removed.");
                    Require(VanillaControls.Rebind(0,1,-1,false) && VanillaControls.Rebind(0,0,-1,false),"Notes incorrectly used the UI unbind restriction.");
                    pad=InputSystem.AddDevice<Gamepad>();
                    InputSystem.EnableDevice(pad);
                    options.ShowPage(VanillaOptionsMenu.Page.Options);
                    options.ShowPage(VanillaOptionsMenu.Page.Controls);
                    options.Adjust(1);
                    options.Confirm();
                    Capture("controls-gamepad");
                    Require(VanillaControls.Rebind(0,0,3,true),"Gamepad rebind failed.");
                    Require(VanillaControls.Bindings[2].buttons.Contains(6),"Gamepad collision did not swap.");
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.North));
                    InputSystem.Update();
                    Require(VanillaControls.Held("NOTE_LEFT") && !VanillaControls.Held("NOTE_UP"),"Mapped gamepad input did not follow the swapped note binding.");
                    InputSystem.QueueStateEvent(pad,new GamepadState());
                    InputSystem.Update();
                    Require(!VanillaControls.Held("NOTE_LEFT"),"Gamepad release left a note held.");
                    options.Back(); options.Back();
                    options.MoveSelection(3-options.SelectedIndex);
                    options.Confirm();
                    Next();
                    break;
                case 7:
                    if (!options.PromptOpen || options.Busy) return;
                    Capture("clear-save-prompt");
                    options.ClosePrompt();
                    Require(PlayerPrefs.HasKey("Funkin.Controls"),"Canceling the save prompt deleted data.");
                    options.MoveSelection(2-options.SelectedIndex);
                    options.Confirm();
                    Next();
                    break;
                case 8:
                    if (options.CurrentPage!=VanillaOptionsMenu.Page.Offsets || options.Busy || wait<4) return;
                    Require(menu.musicSource.clip.name=="offsetsLoop" && menu.musicSource.isPlaying,"Offset music did not start.");
                    Capture("offsets");
                    options.SetOffset(72);
                    options.BeginCalibration(true);
                    Require(options.CalibrationCount==0 && options.TemporaryOffset==0,"Calibration reused old samples.");
                    for(int i=0;i<12;i++) options.AddCalibrationTap(-35);
                    Require(options.TemporaryOffset==-35 && options.CalibrationCount==12,"Calibration average or four-tap update differs.");
                    Require(Pause.GlobalOffset==72,"Incomplete calibration overwrote the saved offset.");
                    Next();
                    break;
                case 9:
                    if (wait<2.2) return;
                    Capture("calibration");
                    options.ExitCalibration(true);
                    Require(Pause.GlobalOffset==72,"Cancel calibration did not preserve the offset.");
                    Next();
                    break;
                case 10:
                    if(wait<3.2) return;
                    options.BeginCalibration(true);
                    for(int i=0;i<30;i++) options.AddCalibrationTap(-35);
                    Require(Pause.GlobalOffset==-35 && !options.Calibrating,"Thirty taps did not complete and save calibration.");
                    Next();
                    break;
                case 11:
                    if(wait<3.2) return;
                    options.BeginCalibration(true);
                    for(int i=0;i<10;i++) options.AddCalibrationTap(i%2==0?-90:90);
                    Require(options.CalibrationCount==0 && options.TemporaryOffset==0,"Inconsistent calibration did not reset.");
                    options.ExitCalibration(true);
                    Next();
                    break;
                case 12:
                    if(wait<3.2) return;
                    options.BeginCalibration(false);
                    Next();
                    break;
                case 13:
                    if(wait<5) return;
                    Require(options.TestingOffset && options.TestNoteCount>0,"Test mode did not create playable notes.");
                    Capture("offset-test");
                    options.ExitCalibration(true);
                    Next();
                    break;
                case 14:
                    if(wait<3.2) return;
                    options.Back();
                    Next();
                    break;
                case 15:
                    if(wait<1.2 || options.CurrentPage!=VanillaOptionsMenu.Page.Options) return;
                    Require(menu.musicSource.clip==menu.menuClip && menu.musicSource.isPlaying,"Leaving offsets did not restore menu music.");
                    options.Close();
                    Require(menu.mainScreen.gameObject.activeSelf && VanillaOptionsMenu.Active==null,"Options did not return to the main menu.");
                    Require(errors==0,"Runtime errors were logged.");
                    Finish(true);
                    break;
            }
        }
        catch(Exception exception) { Debug.LogException(exception); Finish(false); }
    }

    private static void Capture(string name,bool blank=false)
    {
        var canvas=options.GetComponent<Canvas>();
        var scaler=canvas.GetComponent<CanvasScaler>();
        var transforms=canvas.GetComponentsInChildren<Transform>(true);
        int[] layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        var host=new GameObject("Options Capture",typeof(Camera));
        var camera=host.GetComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=360;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
        camera.cullingMask=blank?0:1<<31;
        camera.transform.position=new Vector3(0,0,-1000); camera.farClipPlane=2000;
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        var target=new RenderTexture(1280,720,24);
        var features=AssetDatabase.FindAssets("t:UniversalRendererData").Select(id=>AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(data=>data.rendererFeatures).Where(feature=>feature!=null && feature.isActive).Distinct().ToArray();
        try
        {
            foreach(var child in transforms) child.gameObject.layer=31;
            foreach(var feature in features) feature.SetActive(false);
            camera.targetTexture=target;
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1000;
            scaler.enabled=false; canvas.scaleFactor=1;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
            var previous=RenderTexture.active; RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply(); RenderTexture.active=previous;
            float light=image.GetPixels().Average(c=>c.r+c.g+c.b);
            File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
            Require(blank?light<.001f:light>.02f,"Render or blank control failed: "+name);
            if(name=="root") Require(image.GetPixels().Count(c=>c.r>.9f && c.g>.9f && c.b>.9f)>5000,"Root labels did not render over the background.");
            Object.DestroyImmediate(image);
        }
        finally
        {
            canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; scaler.enabled=true;
            for(int i=0;i<transforms.Length;i++) transforms[i].gameObject.layer=layers[i];
            foreach(var feature in features) feature.SetActive(true);
            Object.DestroyImmediate(host); Object.DestroyImmediate(target);
        }
    }

    private static void Finish(bool passed)
    {
        if(finishing) return;
        finishing=true;
        foreach(var entry in strings) { if(entry.Value==null) PlayerPrefs.DeleteKey(entry.Key); else PlayerPrefs.SetString(entry.Key,entry.Value); }
        foreach(var entry in integers) { if(entry.Value==null) PlayerPrefs.DeleteKey(entry.Key); else PlayerPrefs.SetInt(entry.Key,entry.Value.Value); }
        PlayerPrefs.Save(); VanillaPreferences.Apply(); VanillaControls.Reload();
        if(pad!=null) InputSystem.RemoveDevice(pad);
        InputSystem.settings.backgroundBehavior=backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode=editorInputBehavior;
        SessionState.SetBool("VanillaOptionsValidation.Active",false);
        string result="OPTIONS VALIDATION: passed="+passed+", errors="+errors+", assertions="+assertions+", phase="+phase;
        File.WriteAllText(Path.Combine(Output,"result.txt"),result+"\n");
        Debug.Log(result);
        EditorApplication.Exit(passed?0:1);
    }
}
