using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Batchmode entry points for CI / macOS shell scripts.
    /// Example:
    /// Unity -batchmode -quit -projectPath . -executeMethod CatchIfYouCan.EditorTools.CatchIfYouCanBuildMenu.BuildIOSBatch
    /// </summary>
    public static class CatchIfYouCanBuildMenu
    {
        private const string AndroidDevPath = "Builds/Android/CatchIfYouCan_dev.apk";
        private const string AndroidReleasePath = "Builds/Android/CatchIfYouCan_release.apk";
        private const string IOSPath = "Builds/iOS";

        [MenuItem("Catch If You Can/Build Android Development")]
        public static void BuildAndroidDevelopment()
        {
            if (!ValidateScenes())
                return;

            EnsureOutputDirectory(Path.GetDirectoryName(AndroidDevPath));
            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenePaths(),
                locationPathName = AndroidDevPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            RunBuild(options, "Android Development");
        }

        [MenuItem("Catch If You Can/Build Android Release")]
        public static void BuildAndroidRelease()
        {
            if (!ValidateScenes())
                return;

            EnsureOutputDirectory(Path.GetDirectoryName(AndroidReleasePath));
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenePaths(),
                locationPathName = AndroidReleasePath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            RunBuild(options, "Android Release (IL2CPP)");
        }

        [MenuItem("Catch If You Can/Build iOS")]
        public static void BuildIOS()
        {
            BuildIOSInternal(false);
        }

        /// <summary>Headless entry for BuildIOS.sh / CI.</summary>
        public static void BuildIOSBatch()
        {
            ConfigureIOSPlayerSettings();
            CatchIfYouCanProjectSetup.SetupProjectSilent();
            if (!ValidateScenes())
            {
                EditorApplication.Exit(1);
                return;
            }

            bool ok = BuildIOSInternal(true);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool BuildIOSInternal(bool batch)
        {
            if (!ValidateScenes())
                return false;

            ConfigureIOSPlayerSettings();
            EnsureOutputDirectory(IOSPath);

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenePaths(),
                locationPathName = IOSPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            return RunBuild(options, "iOS (IL2CPP / Xcode)", batch);
        }

        public static void ConfigureIOSPlayerSettings()
        {
            PlayerSettings.companyName = "CatchIfYouCan";
            PlayerSettings.productName = "CATCH IF YOU CAN";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.catchifyoucan.game");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.muteOtherAudioSources = false;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.None;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;

            // No microphone / location / camera usage strings unless features exist.
            PlayerSettings.iOS.locationUsageDescription = string.Empty;
            PlayerSettings.iOS.cameraUsageDescription = string.Empty;

            Debug.Log("[CIYC] iOS PlayerSettings configured for Xcode deploy (ARM64 / IL2CPP / Landscape).");
        }

        private static bool ValidateScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogError("[CIYC] No scenes in Build Settings.");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Build Failed", "No scenes in Build Settings.", "OK");
                return false;
            }

            var missing = scenes.Where(s => s.enabled && !File.Exists(s.path)).Select(s => s.path).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError("[CIYC] Missing scenes:\n" + string.Join("\n", missing));
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Build Failed",
                        "Missing scene files:\n" + string.Join("\n", missing), "OK");
                return false;
            }

            return true;
        }

        private static string[] GetEnabledScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        private static void EnsureOutputDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static bool RunBuild(BuildPlayerOptions options, string label, bool batch = false)
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[{label}] Build succeeded: {options.locationPathName}");
                if (!batch && !Application.isBatchMode)
                    EditorUtility.RevealInFinder(options.locationPathName);
                return true;
            }

            Debug.LogError($"[{label}] Build failed: {report.summary.result}");
            if (!batch && !Application.isBatchMode)
                EditorUtility.DisplayDialog("Build Failed", $"{label} failed.\nSee Console for details.", "OK");
            return false;
        }
    }
}
