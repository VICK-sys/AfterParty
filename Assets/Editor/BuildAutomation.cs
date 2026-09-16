using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildAutomation
{
    [MenuItem("Build/Windows 64-bit")]
    public static void BuildWindows()
    {
        string output = Environment.GetEnvironmentVariable("UNITY_PARTY_BUILD_PATH");
        if (string.IsNullOrWhiteSpace(output))
            output = "Builds/Windows/Unity Party.exe";

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
