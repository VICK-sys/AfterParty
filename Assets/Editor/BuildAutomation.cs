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
            string output = Environment.GetEnvironmentVariable("AFTERPARTY_BUILD_PATH");
            return string.IsNullOrWhiteSpace(output) ? Environment.GetEnvironmentVariable("UNITY_PARTY_BUILD_PATH") : output;
        }
    }

    [MenuItem("Build/Windows 64-bit")]
    public static void BuildWindows()
    {
        string output = OutputOverride;
        if (string.IsNullOrWhiteSpace(output))
            output = "Builds/Windows/AfterParty.exe";

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
}
