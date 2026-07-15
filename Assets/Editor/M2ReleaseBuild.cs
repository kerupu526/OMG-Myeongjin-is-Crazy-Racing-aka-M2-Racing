using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Reproducible Windows release-candidate build entry point.
///
/// The deployed game must boot directly into the shared NetworkRace scene: it contains the
/// room UI, Relay connection flow, race bootstrap, and the local-race path.  Keeping that
/// requirement here prevents a future accidental build from launching a sample scene instead.
/// </summary>
public static class M2ReleaseBuild
{
    public const string EntryScenePath = "Assets/Scenes/NetworkRace.unity";
    public const string WindowsOutputPath = "Builds/Windows/M2Racing.exe";

    [MenuItem("M2/Build Windows Release Candidate")]
    public static void BuildWindowsRelease()
    {
        BuildWindowsReleaseInternal(exitEditorWhenFinished: false);
    }

    /// <summary>Batch-mode entry point for CI and release verification.</summary>
    public static void BuildWindowsReleaseFromBatchMode()
    {
        try
        {
            BuildWindowsReleaseInternal(exitEditorWhenFinished: true);
        }
        catch (Exception exception)
        {
            Debug.LogError($"=== M2_WINDOWS_RELEASE_BUILD_FAIL ===\n{exception}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Batch-mode configuration check that does not create a player build.</summary>
    public static void ValidateReleaseConfigurationFromBatchMode()
    {
        try
        {
            ValidateReleaseConfiguration();
            Debug.Log("=== M2_RELEASE_CONFIGURATION_OK ===");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"=== M2_RELEASE_CONFIGURATION_FAIL ===\n{exception.Message}");
            EditorApplication.Exit(1);
        }
    }

    public static void ValidateReleaseConfiguration()
    {
        EditorBuildSettingsScene[] configuredScenes = EditorBuildSettings.scenes;
        var enabledScenes = new List<string>();
        var seenScenes = new HashSet<string>(StringComparer.Ordinal);

        foreach (EditorBuildSettingsScene scene in configuredScenes)
        {
            if (!scene.enabled) continue;
            if (string.IsNullOrWhiteSpace(scene.path) || !File.Exists(scene.path))
            {
                throw new BuildFailedException($"Build scene is missing: '{scene.path}'.");
            }
            if (!seenScenes.Add(scene.path))
            {
                throw new BuildFailedException($"Build scene is configured more than once: '{scene.path}'.");
            }
            enabledScenes.Add(scene.path);
        }

        if (enabledScenes.Count == 0)
        {
            throw new BuildFailedException("No enabled scenes are configured for the player build.");
        }
        if (!string.Equals(enabledScenes[0], EntryScenePath, StringComparison.Ordinal))
        {
            throw new BuildFailedException(
                $"The first build scene must be {EntryScenePath}; current entry is {enabledScenes[0]}.");
        }
        if (PlayerSettings.runInBackground == false)
        {
            throw new BuildFailedException("PlayerSettings.runInBackground must stay enabled for multiplayer hosts.");
        }
        if (string.IsNullOrWhiteSpace(PlayerSettings.companyName) ||
            string.Equals(PlayerSettings.companyName, "DefaultCompany", StringComparison.Ordinal))
        {
            throw new BuildFailedException("Set a non-default company name before creating a release candidate.");
        }
    }

    static void BuildWindowsReleaseInternal(bool exitEditorWhenFinished)
    {
        ValidateReleaseConfiguration();

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
        {
            throw new BuildFailedException("Unable to switch the active build target to Windows x64.");
        }

        string outputPath = Path.GetFullPath(WindowsOutputPath);
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new BuildFailedException($"Invalid Windows build output path: {outputPath}");
        }
        Directory.CreateDirectory(outputDirectory);

        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled) scenes.Add(scene.path);
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
        };

        // Unity 6.3's Windows post-processor can crash natively in
        // AddIconToWindowsExecutable when it patches the same imported texture into every
        // standalone icon slot.  The crash aborts a fully valid build after all player data has
        // been generated, leaving no usable release artifact. Keep the project's configured
        // icon untouched, but omit it only while producing this Windows candidate. A generic
        // executable icon is preferable to a non-buildable deployment pipeline.
        Texture2D[] configuredIcons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
        bool suppressIconPatch = configuredIcons != null && configuredIcons.Length > 0;
        if (suppressIconPatch)
        {
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, Array.Empty<Texture2D>());
        }

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            if (suppressIconPatch)
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, configuredIcons);
            }
        }
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Windows release build failed: {report.summary.result} ({report.summary.totalErrors} errors).");
        }

        Debug.Log($"=== M2_WINDOWS_RELEASE_BUILD_OK === {outputPath}");
        if (exitEditorWhenFinished) EditorApplication.Exit(0);
    }
}
