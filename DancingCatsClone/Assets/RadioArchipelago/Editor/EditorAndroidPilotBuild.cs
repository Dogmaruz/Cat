using System;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DancingCats.Clone.Editor
{
    /// <summary>Builds only a marked disposable staging project, never the working project.</summary>
    public static class EditorAndroidPilotBuild
    {
        private const string Song = "AlwaysRememberMeowCat";
        private const string Marker = "island-android-staging.json";
        private const string Content = "Assets/RadioArchipelago/Content/Islands/Emerald";
        private static readonly string[] RequiredShaders = {
            "Assets/RadioArchipelago/Art/IslandSurface.shader",
            "Assets/RadioArchipelago/Art/NativeHoldIvory.shader"
        };
        [Serializable] private sealed class StageMarker
        {
            public string schema, sourceProject, stagingProject, songId, scene, outputApk;
            public bool sourceUnchanged, copiedAssetsVerified;
            public string[] excludedMusicNames;
        }
        [Serializable] private sealed class BuildDiagnostic
        {
            public string result, apk, applicationId, graphicsApi, scene, song, unityVersion, versionName;
            public long bytes, uncompressedBuildReportedBytes;
            public int errors, warnings, versionCode;
            public double seconds;
            public string[] audioAssets, forbiddenMusic, shaderAssets, missingShaders;
        }
        [Serializable] private sealed class PackingDiagnostic
        {
            public string[] shaderAssets, missingShaders;
            public int packedSourceCount;
            public string inspectedBuild;
        }

        public static void Build()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string markerPath = Path.Combine(root, Marker);
            if (!File.Exists(markerPath)) throw new BuildFailedException("Refusing build outside a marked disposable staging project.");
            var marker = JsonUtility.FromJson<StageMarker>(File.ReadAllText(markerPath));
            if (marker == null || marker.schema != "radio_archipelago_android_pilot/1" || marker.songId != Song ||
                marker.scene != IslandPilotJobs.ScenePath || !marker.sourceUnchanged || !marker.copiedAssetsVerified ||
                !SamePath(root, marker.stagingProject) || SamePath(root, marker.sourceProject))
                throw new BuildFailedException("Staging marker is inconsistent; create a fresh staging project.");
            string output = Environment.GetEnvironmentVariable("RADIO_ISLAND_APK_PATH");
            if (string.IsNullOrWhiteSpace(output)) throw new BuildFailedException("Set RADIO_ISLAND_APK_PATH to an absolute .apk output path.");
            output = Path.GetFullPath(output);
            if (!output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || !SamePath(output, marker.outputApk))
                throw new BuildFailedException("APK output must match the reviewed staging manifest.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Launch Unity with -buildTarget Android before Build(), so URP collects mobile shader variants.");
            ValidateSingleSong();
            ConfigureMobileRenderer();
            PlayerSettings.companyName = "Radio Archipelago";
            PlayerSettings.productName = "Радиоархипелаг — первый остров";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.radioarchipelago.emeraldislandpilot");
            PlayerSettings.bundleVersion = "0.1.1";
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { IslandPilotJobs.ScenePath }, locationPathName = output,
                target = BuildTarget.Android, options = BuildOptions.DetailedBuildReport
            });
            var packedSources = PackedSources(report);
            var audio = packedSources.Where(IsAudio).ToArray();
            var shaders = packedSources.Where(p => p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)).ToArray();
            var missingShaders = RequiredShaders.Except(shaders, StringComparer.OrdinalIgnoreCase).ToArray();
            var forbidden = audio.Where(p => (p.IndexOf("/OriginalAudio/Music/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.IndexOf("/OriginalAudio/Previews/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.IndexOf("/CustomAudio/Music/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                !p.EndsWith("/" + Song + ".mp3", StringComparison.OrdinalIgnoreCase) ||
                (marker.excludedMusicNames ?? Array.Empty<string>()).Contains(Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase))).ToArray();
            var diagnostic = new BuildDiagnostic
            {
                result = report.summary.result.ToString(), apk = output, scene = IslandPilotJobs.ScenePath, song = Song,
                applicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), graphicsApi = "OpenGLES3 / URP Forward",
                unityVersion = Application.unityVersion, bytes = File.Exists(output) ? new FileInfo(output).Length : 0,
                uncompressedBuildReportedBytes = (long)report.summary.totalSize,
                versionName = PlayerSettings.bundleVersion, versionCode = PlayerSettings.Android.bundleVersionCode,
                errors = (int)report.summary.totalErrors, warnings = (int)report.summary.totalWarnings,
                seconds = report.summary.totalTime.TotalSeconds, audioAssets = audio, forbiddenMusic = forbidden,
                shaderAssets = shaders, missingShaders = missingShaders
            };
            File.WriteAllText(output + ".report.json", JsonUtility.ToJson(diagnostic, true));
            if (report.summary.result != BuildResult.Succeeded || forbidden.Length != 0 || missingShaders.Length != 0)
                throw new BuildFailedException("ISLAND_ANDROID_BUILD FAIL: " + diagnostic.result + "; errors=" + diagnostic.errors + "; forbiddenMusic=" + forbidden.Length + "; missingPackedShaders=" + string.Join(",", missingShaders));
            Debug.Log("ISLAND_ANDROID_BUILD PASS: " + output + "; bytes=" + diagnostic.bytes + "; audio=" + audio.Length);
        }

        // Useful for checking the actual BuildReport format without building again.
        public static void InspectLatestPacking()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!File.Exists(Path.Combine(root, Marker))) throw new BuildFailedException("Packing inspection requires a staging project.");
            var report = BuildReport.GetLatestReport();
            if (report == null) throw new BuildFailedException("No previous BuildReport is available.");
            var sources = PackedSources(report);
            var shaders = sources.Where(p => p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)).ToArray();
            var diagnostic = new PackingDiagnostic { shaderAssets = shaders,
                missingShaders = RequiredShaders.Except(shaders, StringComparer.OrdinalIgnoreCase).ToArray(),
                packedSourceCount = sources.Length, inspectedBuild = report.summary.outputPath };
            File.WriteAllText(Path.Combine(root, "latest-packing-inspection.json"), JsonUtility.ToJson(diagnostic, true));
            Debug.Log("ISLAND_PACKING_INSPECTED sources=" + sources.Length + "; shaders=" + shaders.Length + "; missing=" + string.Join(",", diagnostic.missingShaders));
        }

        private static string[] PackedSources(BuildReport report) => report.packedAssets.SelectMany(p => p.contents)
            .Select(p => p.sourceAssetPath).Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToArray();

        private static bool SamePath(string a, string b) => !string.IsNullOrWhiteSpace(b) &&
            string.Equals(Path.GetFullPath(a).TrimEnd('/', '\\'), Path.GetFullPath(b).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);
        private static bool IsAudio(string path) => new[] { ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac" }
            .Contains(Path.GetExtension(path).ToLowerInvariant());

        private static void ValidateSingleSong()
        {
            var original = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText("Assets/Resources/OriginalData/songs.json"));
            var custom = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText("Assets/Resources/CustomData/songs.json"));
            if (original.Length != 1 || original[0].id != Song || custom.Length != 0 ||
                original[0].audioResource != "CustomAudio/Music/" + Song ||
                !File.Exists("Assets/Resources/CustomAudio/Music/" + Song + ".mp3") ||
                !File.Exists("Assets/Resources/CustomData/Maps/" + Song + ".timeline.json") ||
                Directory.Exists("Assets/Resources/OriginalAudio/Music") || Directory.Exists("Assets/Resources/OriginalAudio/Previews"))
                throw new BuildFailedException("Staging resource whitelist does not contain exactly the approved song.");
        }

        private static void ConfigureMobileRenderer()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Content + "/IslandPilotQuality.asset");
            if (pipeline == null) throw new BuildFailedException("The prepared island render asset is missing.");
            var pipelineData = new SerializedObject(pipeline);
            var list = pipelineData.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) throw new BuildFailedException("The island renderer list is empty.");
            var rendererSource = list.GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
            if (rendererSource == null) throw new BuildFailedException("The island renderer is not UniversalRendererData.");
            const string mobilePath = Content + "/PilotMobileForwardRenderer.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(mobilePath) == null &&
                !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(rendererSource), mobilePath))
                throw new BuildFailedException("Could not make the staging-only Forward renderer copy.");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(mobilePath);
            var rendererData = new SerializedObject(renderer);
            var mode = rendererData.FindProperty("m_RenderingMode");
            if (mode == null) throw new BuildFailedException("This URP version has no expected renderer mode field.");
            mode.intValue = 0; rendererData.ApplyModifiedPropertiesWithoutUndo();
            list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            pipelineData.FindProperty("m_DefaultRendererIndex").intValue = 0;
            pipelineData.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int initialQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; }
            QualitySettings.SetQualityLevel(initialQuality, false);
            var scene = EditorSceneManager.OpenScene(IslandPilotJobs.ScenePath, OpenSceneMode.Single);
            var environment = UnityEngine.Object.FindAnyObjectByType<IslandPilotEnvironment>();
            if (environment == null) throw new BuildFailedException("Pilot scene has no environment controller.");
            var environmentData = new SerializedObject(environment);
            environmentData.FindProperty("renderPipeline").objectReferenceValue = pipeline;
            environmentData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(pipeline);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("ISLAND_ANDROID_RENDERER Forward / OpenGLES3; default, all quality levels and scene environment use staged IslandPilotQuality.");
        }
    }
}
