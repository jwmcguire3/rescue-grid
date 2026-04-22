#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildScripts
{
    private const string DefaultOutputRoot = "Build";
    private const string DefaultBaseIdentifier = "com.defaultcompany.rescuegrid.dev";
    private const string DefaultAndroidFormat = "apk";

    public static void BuildIos()
    {
        string outputRoot = GetOutputRoot();
        string outputDirectory = Path.Combine(outputRoot, "iOS", "XcodeProject");
        string bundleIdentifier = GetEnvironmentVariable(
            "RESCUE_IOS_BUNDLE_IDENTIFIER",
            GetEnvironmentVariable("RESCUE_BUILD_APP_IDENTIFIER", DefaultBaseIdentifier));

        EnsureRequiredBuildInputs();
        PrepareDirectory(outputDirectory);

        BuildPlayerOptions options = CreatePlayerOptions(BuildTargetGroup.iOS, BuildTarget.iOS, outputDirectory);

        WithTemporarySetting(
            () => PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS),
            value => PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, value),
            bundleIdentifier,
            () =>
            {
                WithTemporarySetting(
                    () => PlayerSettings.iOS.appleEnableAutomaticSigning,
                    value => PlayerSettings.iOS.appleEnableAutomaticSigning = value,
                    false,
                    () => ExecuteBuild(options));
            });
    }

    public static void BuildAndroid()
    {
        string outputRoot = GetOutputRoot();
        string bundleIdentifier = GetEnvironmentVariable(
            "RESCUE_ANDROID_APPLICATION_IDENTIFIER",
            GetEnvironmentVariable("RESCUE_BUILD_APP_IDENTIFIER", DefaultBaseIdentifier));
        string format = GetEnvironmentVariable("RESCUE_ANDROID_FORMAT", DefaultAndroidFormat).Trim().ToLowerInvariant();
        string fileExtension = format switch
        {
            "apk" => "apk",
            "aab" => "aab",
            _ => throw new InvalidOperationException(
                "RESCUE_ANDROID_FORMAT must be either 'apk' or 'aab'.")
        };
        string fileName = GetEnvironmentVariable(
            "RESCUE_ANDROID_OUTPUT_NAME",
            $"rescue-grid-android-dev.{fileExtension}");
        string outputPath = Path.Combine(outputRoot, "Android", fileName);

        EnsureRequiredBuildInputs();
        PrepareFile(outputPath);

        BuildPlayerOptions options = CreatePlayerOptions(BuildTargetGroup.Android, BuildTarget.Android, outputPath);

        WithTemporarySetting(
            () => PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
            value => PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, value),
            bundleIdentifier,
            () =>
            {
                WithTemporarySetting(
                    () => EditorUserBuildSettings.buildAppBundle,
                    value => EditorUserBuildSettings.buildAppBundle = value,
                    format == "aab",
                    () =>
                    {
                        WithTemporarySetting(
                            () => PlayerSettings.Android.targetArchitectures,
                            value => PlayerSettings.Android.targetArchitectures = value,
                            AndroidArchitecture.ARM64,
                            () => ExecuteBuild(options));
                    });
            });
    }

    public static void BuildWeb()
    {
        string outputRoot = GetOutputRoot();
        string outputDirectory = Path.Combine(outputRoot, "WebGL");

        EnsureRequiredBuildInputs();
        PrepareDirectory(outputDirectory);

        BuildPlayerOptions options = CreatePlayerOptions(BuildTargetGroup.WebGL, BuildTarget.WebGL, outputDirectory);
        ExecuteBuild(options);
    }

    private static BuildPlayerOptions CreatePlayerOptions(
        BuildTargetGroup targetGroup,
        BuildTarget target,
        string locationPathName)
    {
        string[] scenes = GetEnabledScenes();
        BuildOptions buildOptions = BuildOptions.StrictMode;

        if (GetFlag("RESCUE_DEVELOPMENT_BUILD", true))
        {
            buildOptions |= BuildOptions.Development;
        }

        if (GetFlag("RESCUE_ALLOW_DEBUGGING", true))
        {
            buildOptions |= BuildOptions.AllowDebugging;
        }

        if (GetFlag("RESCUE_CONNECT_PROFILER", false))
        {
            buildOptions |= BuildOptions.ConnectWithProfiler;
        }

        return new BuildPlayerOptions
        {
            scenes = scenes,
            targetGroup = targetGroup,
            target = target,
            locationPathName = locationPathName,
            options = buildOptions,
        };
    }

    private static void ExecuteBuild(BuildPlayerOptions options)
    {
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Build failed for {options.target}. Result: {report.summary.result}. " +
                $"See the Unity batch log for details.");
        }

        Console.WriteLine(
            $"Build succeeded for {options.target} at '{options.locationPathName}'. " +
            $"Total size: {report.summary.totalSize} bytes.");
    }

    private static void EnsureRequiredBuildInputs()
    {
        string[] scenes = GetEnabledScenes();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException(
                "No enabled scenes were found in Build Settings. " +
                "Add at least one scene before running a build.");
        }
    }

    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    private static string GetOutputRoot()
    {
        return GetEnvironmentVariable("RESCUE_BUILD_OUTPUT_ROOT", DefaultOutputRoot);
    }

    private static string GetEnvironmentVariable(string name, string fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static bool GetFlag(string name, bool fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "true" => true,
            "yes" => true,
            "on" => true,
            "0" => false,
            "false" => false,
            "no" => false,
            "off" => false,
            _ => throw new InvalidOperationException(
                $"{name} must be one of: 1, 0, true, false, yes, no, on, off.")
        };
    }

    private static void PrepareDirectory(string directory)
    {
        string fullPath = Path.GetFullPath(directory);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        Directory.CreateDirectory(fullPath);
    }

    private static void PrepareFile(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Could not determine output directory for '{filePath}'.");
        }

        Directory.CreateDirectory(directory);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static void WithTemporarySetting<T>(
        Func<T> getter,
        Action<T> setter,
        T temporaryValue,
        Action action)
    {
        T original = getter();
        setter(temporaryValue);

        try
        {
            action();
        }
        finally
        {
            setter(original);
        }
    }
}
#endif
