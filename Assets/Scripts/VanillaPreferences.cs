using System;
using Newtonsoft.Json;
using UnityEngine;

public static class VanillaPreferences
{
    private static int DefaultVSync => DesktopOptionsAvailable ? 0 : 1;

    public static bool DesktopOptionsAvailable
    {
        get
        {
#if UNITY_WSA || UNITY_XBOXONE || UNITY_GAMECORE
            return false;
#else
            return true;
#endif
        }
    }

    public sealed class Preference
    {
        public string id, label, description;
        public int initial, min, max, step;
        public string[] choices;
        public bool checkbox, percentage;
        public bool Available => DesktopOptionsAvailable || id == "Naughtyness" || id == "Downscroll" ||
            id == "StrumlineBackground" || id == "FlashingLights" || id == "CameraZooms" ||
            id == "Subtitles" || id == "DebugDisplay" || id == "DebugDisplayBG" ||
            id == "FPS" || id == "UnlockedFramerate";
        public int Value => Get(id, initial);
        public string Display => choices != null ? choices[Mathf.Clamp(Value, 0, choices.Length-1)] : Value + (percentage ? "%" : "");
    }

    public static readonly Preference[] Items =
    {
        Toggle("Naughtyness", "Naughtyness", "When enabled, raunchy content (such as swearing, etc.) is displayed.", true),
        Toggle("Downscroll", "Downscroll", "When enabled, notes move downwards toward the strumline at the bottom of the screen.", false),
        Number("StrumlineBackground", "Strumline Background", "Show a semi-transparent background behind the strumline.", 0, 0, 100, 10, true),
        Toggle("FlashingLights", "Flashing Lights", "When disabled, flashing effects are dampened. Useful for people with photosensitive epilepsy.", true),
        Toggle("CameraZooms", "Camera Zooms", "When enabled, the camera bounces during songs.", true),
        Toggle("Subtitles", "Subtitles", "When enabled, subtitles appear during some songs and cutscenes.", true),
        Choice("DebugDisplay", "Debug Display", "When enabled, FPS and other debug stats are displayed.", 2, "Advanced", "Simple", "Off"),
        Number("DebugDisplayBG", "Debug Display BG", "Adjust the debug display's background opacity.", 50, 0, 100, 10, true),
        Toggle("AutoPause", "Pause on Unfocus", "When enabled, the game automatically pauses when losing focus.", true),
        Toggle("AutoFullscreen", "Launch in Fullscreen", "When enabled, the game automatically starts up in fullscreen mode.", false),
        Choice("VSync", "VSync", "When enabled, the game attempts to match the framerate with your monitor's refresh rate.", DefaultVSync, "Off", "On", "Adaptive"),
        Toggle("UnlockedFramerate", "Unlocked Framerate", "When enabled, the framerate is unlocked.\nThis setting is mutually exclusive with FPS.", false),
        Number("FPS", "FPS", "The maximum framerate that the game targets.\nThis setting is mutually exclusive with Unlocked Framerate.", 60, 30, 500, 5),
        Toggle("HideMouse", "Hide Mouse", "When enabled, the mouse is hidden while taking a screenshot.", true),
        Toggle("FancyPreview", "Fancy Preview", "When enabled, a preview is shown after taking a screenshot.", true),
        Toggle("PreviewOnSave", "Preview on Save", "When enabled, the preview is only shown after a screenshot is saved.", true),
        Toggle("DiscordRPC", "Discord RPC", "Toggles Discord RPC.", true)
    };

    private static Preference Toggle(string id, string label, string description, bool initial) => new Preference
        { id=id, label=label, description=description, initial=initial?1:0, min=0, max=1, step=1, checkbox=true };
    private static Preference Number(string id, string label, string description, int initial, int min, int max, int step, bool percent=false) => new Preference
        { id=id, label=label, description=description, initial=initial, min=min, max=max, step=step, percentage=percent };
    private static Preference Choice(string id, string label, string description, int initial, params string[] choices) => new Preference
        { id=id, label=label, description=description, initial=initial, min=0, max=choices.Length-1, step=1, choices=choices };

    public static int Get(string id, int initial=0) => PlayerPrefs.GetInt("Funkin.Options."+id, initial);
    public static bool Naughtyness => Get("Naughtyness", 1) != 0;
    public static bool FlashingLights => Get("FlashingLights", 1) != 0;
    public static bool CameraZooms => Get("CameraZooms", 1) != 0;
    public static bool Subtitles => Get("Subtitles", 1) != 0;
    public static bool AutoPause => Get("AutoPause", 1) != 0;

    public static void Set(Preference preference, int value)
    {
        value = Mathf.Clamp(value, preference.min, preference.max);
        PlayerPrefs.SetInt("Funkin.Options."+preference.id, value);
        if (preference.id == "Downscroll")
        {
            OptionsV2.Downscroll = value != 0;
            MiscOptions legacy;
            try { legacy = JsonConvert.DeserializeObject<MiscOptions>(PlayerPrefs.GetString("MiscOptions", "{}")) ?? new MiscOptions(); }
            catch { legacy = new MiscOptions(); }
            legacy.enableDownscroll = OptionsV2.Downscroll;
            PlayerPrefs.SetString("MiscOptions", JsonConvert.SerializeObject(legacy));
        }
        Apply();
        PlayerPrefs.Save();
    }

    public static void Apply()
    {
        QualitySettings.vSyncCount = Get("VSync", DefaultVSync) != 0 ? 1 : 0;
        Application.targetFrameRate = Get("UnlockedFramerate") != 0 ? -1 : Mathf.Clamp(Get("FPS", 60), 30, 500);
        Application.runInBackground = true;
        if (PlayerPrefs.HasKey("Funkin.Options.Downscroll")) OptionsV2.Downscroll = Get("Downscroll") != 0;
        if (DiscordManager.current != null)
        {
            bool enabled = DesktopOptionsAvailable && Get("DiscordRPC", 1) != 0;
            if (!enabled) DiscordManager.current.client?.ClearPresence();
            DiscordManager.current.enabled = enabled;
        }
    }

    public static void Migrate()
    {
        if (!PlayerPrefs.HasKey("Funkin.Options.Downscroll"))
        {
            try
            {
                var legacy = JsonConvert.DeserializeObject<MiscOptions>(PlayerPrefs.GetString("MiscOptions", "{}"));
                PlayerPrefs.SetInt("Funkin.Options.Downscroll", legacy?.enableDownscroll == true ? 1 : 0);
            }
            catch { PlayerPrefs.SetInt("Funkin.Options.Downscroll", 0); }
        }
        Apply();
    }
}
