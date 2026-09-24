using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildAutomation
{
    public static string OutputOverride
    {
        get
        {
            string output = Environment.GetEnvironmentVariable("FRIDAY_FIGHT_FUNKIN_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output)) output = Environment.GetEnvironmentVariable("AFTERPARTY_BUILD_PATH");
            return string.IsNullOrWhiteSpace(output) ? Environment.GetEnvironmentVariable("UNITY_PARTY_BUILD_PATH") : output;
        }
    }

    [MenuItem("Build/Windows 64-bit")]
    public static void BuildWindows()
    {
        string output = OutputOverride;
        if (string.IsNullOrWhiteSpace(output))
            output = "Builds/Windows/Funkin.exe";

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Windows build failed: " + report.summary.result);

        Debug.Log("Windows build succeeded: " + report.summary.outputPath);
    }

    [MenuItem("Build/Xbox UWP x64")]
    public static void BuildXboxUwp()
    {
        string output = OutputOverride;
        if (string.IsNullOrWhiteSpace(output)) output = "Builds/XboxUWP";
        EditorUserBuildSettings.wsaArchitecture = "x64";
        EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WindowsStoreApps, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WSAPlayer, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WSAPlayer, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        Directory.CreateDirectory(Path.GetFullPath(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.WSAPlayer,
            extraScriptingDefines = new[] { "DISABLE_DISCORD" },
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Xbox UWP export failed: " + report.summary.result);
        Debug.Log("Xbox UWP export succeeded: " + report.summary.outputPath);
    }
}
