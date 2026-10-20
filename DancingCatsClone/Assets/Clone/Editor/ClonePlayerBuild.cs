using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class ClonePlayerBuild
    {
        public static void Verify()
        {
            NativeRulesValidation.Run();
            OriginalRoleMotionValidation.Run();
            ProgressionValidation.Run();
            OriginalTileVisualValidation.Run();
            OriginalThemeVisualValidation.Run();
            OriginalAssetLibraryValidation.Run();
            RecoveredDataValidation.Run();
        }

        public static void Windows()
        {
            string output = Environment.GetEnvironmentVariable("DANCING_CATS_BUILD_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Set DANCING_CATS_BUILD_OUTPUT to the desired deliverable folder.");
            Directory.CreateDirectory(output);
            Build(CloneSceneBuilder.ScenePath, Path.Combine(output, "Dancing Cats Offline.exe"), BuildTarget.StandaloneWindows64);
        }

        public static void Android()
        {
            string output = Environment.GetEnvironmentVariable("DANCING_CATS_BUILD_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Set DANCING_CATS_BUILD_OUTPUT to the desired deliverable folder.");
            Directory.CreateDirectory(output);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            // Unity 6000.5 no longer ships an Android x86_64 player.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Build(CloneSceneBuilder.ScenePath, Path.Combine(output, "DancingCatsOffline.apk"), BuildTarget.Android);
        }

        private static void Build(string scene, string path, BuildTarget target)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene }, locationPathName = path, target = target, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("CLONE_BUILD FAIL: " + report.summary.result + ", errors=" + report.summary.totalErrors);
            Debug.Log("CLONE_BUILD PASS: " + path + ", bytes=" + report.summary.totalSize + ", warnings=" + report.summary.totalWarnings);
        }
    }
}
