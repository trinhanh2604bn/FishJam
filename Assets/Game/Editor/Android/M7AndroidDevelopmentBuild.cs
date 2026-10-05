using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace FishPuzzle.EditorTools
{
    /// <summary>
    /// Builds the M7 Android development APK and can run the existing test suites afterward.
    /// A request file at Builds/Android/m7-request.txt starts the job from the open editor.
    /// </summary>
    public static class M7AndroidDevelopmentBuild
    {
        public const string ApkRelativePath = "Builds/Android/FishPuzzle-M7.apk";
        public const string RequestRelativePath = "Builds/Android/m7-request.txt";
        public const string ResultRelativePath = "Builds/Android/m7-result.txt";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string RequestPath => Path.Combine(ProjectRoot, "Builds", "Android", "m7-request.txt");

        private static string ResultPath => Path.Combine(ProjectRoot, "Builds", "Android", "m7-result.txt");

        private static string ApkPath => Path.Combine(ProjectRoot, "Builds", "Android", "FishPuzzle-M7.apk");

        private const string BundleId = "com.DefaultCompany.FishJam";
        private const string RequestToken = "build-and-test";
        private const string SdkPath = @"C:\Users\Trinhthingocanh\AppData\Local\Android\Sdk";
        private const string NdkPath = @"D:\FishJam\Builds\Android\tools\ndk";
        private const string JdkPath = @"D:\FishJam\Builds\Android\tools\jdk";

        [InitializeOnLoadMethod]
        private static void WatchRequest()
        {
            EditorApplication.update += TickRequest;
        }

        private static bool _switchRequested;

        private static void TickRequest()
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            if (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode
                || BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            var mode = File.ReadAllText(RequestPath).Trim();
            if (string.IsNullOrEmpty(mode))
            {
                mode = "build-and-test";
            }

            ApplyAndroidSettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    File.Delete(RequestPath);
                    WriteResult("BLOCKED Android Build Support module is not installed for this editor.");
                    return;
                }

                if (_switchRequested)
                {
                    return;
                }

                _switchRequested = true;
                EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android, BuildTarget.Android);
                return;
            }

            File.Delete(RequestPath);
            var runTests = mode.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0;
            EditorApplication.delayCall += () => Run(runTests);
        }

        [MenuItem("Tools/Fish Puzzle/Build M7 Android APK")]
        public static void BuildFromMenu()
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Builds", "Android"));
            File.WriteAllText(RequestPath, RequestToken);
        }

        public static void BuildDevelopmentApk()
        {
            Run(false);
        }

        private static void Run(bool runTests)
        {
            ApplyAndroidSettings();
            var report = new StringBuilder();
            report.AppendLine("started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("activeTarget " + EditorUserBuildSettings.activeBuildTarget);
            report.AppendLine("orientation " + PlayerSettings.defaultInterfaceOrientation);
            report.AppendLine("architectures " + PlayerSettings.Android.targetArchitectures);
            report.AppendLine("scriptingBackend " + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
            report.AppendLine("bundleId " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            report.AppendLine("keystore custom " + PlayerSettings.Android.useCustomKeystore);
            var scenes = EnabledScenes();
            report.AppendLine("scenes " + string.Join(", ", scenes));

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                report.AppendLine("RESULT BLOCKED Android Build Support module is not installed.");
                WriteResult(report.ToString());
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath) ?? Path.Combine(ProjectRoot, "Builds", "Android"));
            if (File.Exists(ApkPath))
            {
                File.Delete(ApkPath);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };

            var buildReport = BuildPipeline.BuildPlayer(options);
            report.AppendLine("buildResult " + buildReport.summary.result);
            report.AppendLine("buildSize " + (File.Exists(ApkPath) ? new FileInfo(ApkPath).Length.ToString() : "0"));
            report.AppendLine("totalErrors " + buildReport.summary.totalErrors);
            report.AppendLine("totalWarnings " + buildReport.summary.totalWarnings);
            if (buildReport.summary.result != BuildResult.Succeeded)
            {
                report.AppendLine("RESULT BUILD_FAILED");
                WriteResult(report.ToString());
                return;
            }

            report.AppendLine("RESULT BUILD_SUCCEEDED");
            WriteResult(report.ToString());
            if (runTests)
            {
                RunSuite(TestMode.EditMode, Path.Combine(ProjectRoot, "Builds", "Android", "m7-editmode.txt"), () =>
                {
                    RunSuite(TestMode.PlayMode, Path.Combine(ProjectRoot, "Builds", "Android", "m7-playmode.txt"), null);
                });
            }
        }

        private static void RunSuite(TestMode mode, string outputPath, Action next)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new SuiteCallbacks(mode, outputPath, next);
            api.RegisterCallbacks(callbacks);
            api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
        }

        private static void ApplyAndroidSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            if (Directory.Exists(SdkPath))
            {
                AndroidExternalToolsSettings.sdkRootPath = SdkPath;
            }

            if (Directory.Exists(NdkPath))
            {
                AndroidExternalToolsSettings.ndkRootPath = NdkPath;
            }

            if (Directory.Exists(JdkPath))
            {
                AndroidExternalToolsSettings.jdkRootPath = JdkPath;
            }

            Debug.Log("[M7] JDK " + AndroidExternalToolsSettings.jdkRootPath
                + " NDK " + AndroidExternalToolsSettings.ndkRootPath
                + " SDK " + AndroidExternalToolsSettings.sdkRootPath);

            AssetDatabase.SaveAssets();
        }

        private static string[] EnabledScenes()
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }

        private static void WriteResult(string text)
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Builds", "Android"));
            File.WriteAllText(ResultPath, text);
            Debug.Log("[M7] " + text);
        }

        private sealed class SuiteCallbacks : ICallbacks
        {
            private readonly TestMode _mode;
            private readonly string _outputPath;
            private readonly Action _next;

            public SuiteCallbacks(TestMode mode, string outputPath, Action next)
            {
                _mode = mode;
                _outputPath = outputPath;
                _next = next;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var failures = new List<string>();
                CollectFailures(result, failures);
                var report = new StringBuilder();
                report.AppendLine("mode " + _mode);
                report.AppendLine("passed " + result.PassCount);
                report.AppendLine("failed " + result.FailCount);
                report.AppendLine("skipped " + result.SkipCount);
                report.AppendLine("inconclusive " + result.InconclusiveCount);
                report.AppendLine("status " + result.TestStatus);
                for (var i = 0; i < failures.Count; i++)
                {
                    report.AppendLine("FAIL " + failures[i]);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_outputPath) ?? Path.Combine(ProjectRoot, "Builds", "Android"));
                File.WriteAllText(_outputPath, report.ToString());
                Debug.Log("[M7] " + _mode + " passed " + result.PassCount + " failed " + result.FailCount);
                if (_next != null)
                {
                    EditorApplication.delayCall += () => _next();
                }
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            private static void CollectFailures(ITestResultAdaptor result, List<string> failures)
            {
                if (result == null)
                {
                    return;
                }

                if (result.TestStatus == TestStatus.Failed && !result.HasChildren)
                {
                    failures.Add(result.FullName + " :: " + result.Message);
                }

                if (!result.HasChildren)
                {
                    return;
                }

                foreach (var child in result.Children)
                {
                    CollectFailures(child, failures);
                }
            }
        }
    }
}
