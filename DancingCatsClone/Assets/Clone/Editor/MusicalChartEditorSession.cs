using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DancingCats.Clone.Editor
{
    // A local, allowlisted authoring job queue for an already open Editor.
    // No network endpoint, arbitrary code evaluation or player build commands.
    // Package setup is restricted to the explicit, pinned DI installation action.
    [InitializeOnLoad]
    public static class MusicalChartEditorSession
    {
        [Serializable] private sealed class Request { public string action; }
        [Serializable] private sealed class Response
        {
            public string action, message, scenePath, selectedSong, phase;
            public bool success, playing, paused, compiling, sceneDirty;
            public double clock;
        }

        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MusicalChartReview"));
        private static double nextPoll;

        static MusicalChartEditorSession()
        {
            EditorApplication.update += Poll;
            EditorApplication.delayCall += () => WriteResponse("inspect", true, "Editor authoring session available");
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            string path = Path.Combine(Folder, "request.json");
            if (!File.Exists(path)) return;
            string action = "unknown";
            try
            {
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
                File.Delete(path);
                action = request?.action;
                switch (action)
                {
                    case "inspect": break;
                    case "radio-hop-baseline": Invoke("RadioArchipelagoJobs", "HopBaseline"); break;
                    case "radio-hop-candidate": Invoke("RadioArchipelagoJobs", "HopCandidate"); break;
                    case "radio-hop-checks": Invoke("RadioArchipelagoJobs", "HopChecks"); break;
                    case "refresh":
                        RequireEditMode();
                        AssetDatabase.Refresh();
                        break;
                    case "stop-play":
                        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                        break;
                    case "generate":
                        RequireEditMode();
                        Invoke("MeowLevelValidation", "GenerateAndVerify");
                        break;
                    case "validate":
                        RequireEditMode();
                        Invoke("MeowLevelValidation", "RunChecks");
                        break;
                    case "playtest":
                        RequireEditMode();
                        Invoke("IntegratedPlayValidation", "RunCustomSongInOpenEditor");
                        break;
                    case "install-di":
                        RequireEditMode();
                        Invoke("DependencyInjectionPackageSetup", "Install");
                        break;
                    case "di-migrate":
                        RequireEditMode();
                        Invoke("DependencyInjectionValidation", "MigrateScene");
                        break;
                    case "di-checks":
                        RequireEditMode();
                        Invoke("DependencyInjectionValidation", "RunChecks");
                        break;
                    case "di-menus":
                        RequireEditMode();
                        Invoke("IntegratedPlayValidation", "RunDependencyInjectionMenusInOpenEditor");
                        break;
                    case "radio-assets":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "CreateAssets");
                        break;
                    case "radio-migrate":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "MigrateScene");
                        break;
                    case "radio-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RunChecks");
                        break;
                    case "radio-playtest":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "Playtest");
                        break;
                    case "radio-menu-preview-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "MenuPreviewChecks");
                        break;
                    case "radio-menu-bake-probe":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "MenuBakeProbe");
                        break;
                    case "radio-lighting-assets":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "LightingAssets");
                        break;
                    case "radio-lighting-reflections":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RefreshLightingReflections");
                        break;
                    case "radio-motion-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "MotionChecks");
                        break;
                    case "radio-import-couriers":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "ImportCouriers");
                        break;
                    case "radio-rig-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RigChecks");
                        break;
                    case "radio-rigid-paw-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RigidPawChecks");
                        break;
                    case "radio-surface-baseline":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "SurfaceBaseline");
                        break;
                    case "radio-surface-candidate":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "SurfaceCandidate");
                        break;
                    case "radio-rounded-paw-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RoundedPawChecks");
                        break;
                    case "radio-joint-baseline":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "JointBaseline");
                        break;
                    case "radio-joint-candidate":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "JointCandidate");
                        break;
                    case "radio-shoulder-trials":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "ShoulderTrials");
                        break;
                    case "radio-accessory-checks":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "AccessoryChecks");
                        break;
                    case "radio-original-paw-study":
                        RequireEditMode();
                        Invoke("OriginalPawMotionStudy", "Run");
                        break;
                    case "chart-original-baseline":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "RunBaseline");
                        break;
                    case "chart-original-checks":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "RunChecks");
                        break;
                    case "chart-original-measure":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "RunCandidate");
                        break;
                    case "chart-original-export-pcm":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "ExportPcmForAnalysis");
                        break;
                    case "chart-custom-export-pcm":
                        RequireEditMode();
                        Invoke("AudioChartGenerator", "ExportAlwaysRememberPcm");
                        break;
                    case "chart-custom-publish":
                        RequireEditMode();
                        Invoke("AudioChartGenerator", "RegenerateAlwaysRememberFromAudio");
                        break;
                    case "chart-flight-checks":
                        RequireEditMode();
                        Invoke("PcmFlightAnalysisValidation", "RunChecks");
                        break;
                    case "chart-sparse-checks":
                        RequireEditMode();
                        Invoke("PcmSparsePhraseAnalysisValidation", "RunChecks");
                        break;
                    case "chart-custom-spectral":
                        RequireEditMode();
                        Invoke("AudioChartGenerator", "ExportAlwaysRememberSpectralEvidence");
                        break;
                    case "chart-benchmark-export-calibration":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "ExportCalibrationPcm");
                        break;
                    case "chart-benchmark-export-validation":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "ExportValidationPcm");
                        break;
                    case "chart-benchmark-baseline":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "RunCalibrationBaseline");
                        break;
                    case "chart-evidence-checks":
                        RequireEditMode();
                        Invoke("PcmVocalEvidenceValidation", "RunChecks");
                        break;
                    case "chart-foreground-checks":
                        RequireEditMode();
                        Invoke("PcmForegroundAnalysisValidation", "RunChecks");
                        break;
                    case "chart-rapid-checks":
                        RequireEditMode();
                        Invoke("PcmRapidArticulationAnalysisValidation", "RunChecks");
                        break;
                    case "chart-benchmark-candidate":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "RunCalibrationCandidate");
                        break;
                    case "chart-validation-baseline":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "RunFrozenBaseline");
                        break;
                    case "chart-validation-candidate":
                        RequireEditMode();
                        Invoke("PcmChartBenchmark", "RunFrozenCandidate");
                        break;
                    case "chart-original-publish":
                        RequireEditMode();
                        Invoke("OriginalChartCalibration", "GenerateUnstoppableComparison");
                        break;
                    case "chart-route-checks":
                        RequireEditMode();
                        Invoke("ChartRouteComposerValidation", "RunChecks");
                        break;
                    case "chart-v9-export-calibration":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "ExportCalibrationPcm");
                        break;
                    case "chart-v9-export-frozen":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "ExportFrozenPcm");
                        break;
                    case "chart-v9-baseline-calibration":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunCalibrationBaseline");
                        break;
                    case "chart-v9-baseline-frozen":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunFrozenBaseline");
                        break;
                    case "chart-v9-baseline-regression":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunRegressionBaseline");
                        break;
                    case "chart-v9-calibration":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunCalibrationCandidate");
                        break;
                    case "chart-v9-frozen":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunFrozenCandidate");
                        break;
                    case "chart-v9-regression":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunRegressionCandidate");
                        break;
                    case "chart-v9-metric-checks":
                        RequireEditMode();
                        Invoke("PcmChartStudyV9", "RunMetricChecks");
                        break;
                    case "chart-short-native-checks":
                        RequireEditMode();
                        Invoke("ShortNativeSupportValidation", "RunChecks");
                        break;
                    case "chart-instrument-evidence-checks":
                        RequireEditMode();
                        Invoke("PcmInstrumentalEvidenceValidation", "RunChecks");
                        break;
                    case "chart-melodic-follow-checks":
                        RequireEditMode();
                        Invoke("PcmMelodicFollowValidation", "RunChecks");
                        break;
                    case "chart-short-support-checks":
                        RequireEditMode();
                        Invoke("PcmShortSupportAnalysisValidation", "RunChecks");
                        break;
                    case "chart-instrument-integration":
                        RequireEditMode();
                        Invoke("InstrumentalChartIntegrationValidation", "RunChecks");
                        break;
                    case "chart-v9-publication-guards":
                        RequireEditMode();
                        Invoke("ChartTestPublicationV9", "RunGuardChecks");
                        break;
                    case "chart-v9-publish-tests":
                        RequireEditMode();
                        Invoke("ChartTestPublicationV9", "PublishTests");
                        break;
                    case "chart-v9-play-disconnect":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "V9DisconnectPlaytest");
                        break;
                    case "chart-v9-play-cancan":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "V9CanCanPlaytest");
                        break;
                    case "chart-v9-play-dance":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "V9DancePlaytest");
                        break;
                    case "chart-v9-play-clouds":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "V9CloudsPlaytest");
                        break;
                    case "chart-route-integration":
                        RequireEditMode();
                        Invoke("ChartRouteIntegrationValidation", "RunChecks");
                        break;
                    case "chart-route-publish":
                        RequireEditMode();
                        Invoke("ChartRouteJobs", "ApplyAllCustomLevels");
                        break;
                    case "chart-route-real-audio":
                        RequireEditMode();
                        Invoke("ChartRouteIntegrationValidation", "CheckPublishedGeneration");
                        break;
                    case "chart-native-moving-checks":
                        RequireEditMode();
                        Invoke("NativeMovingTileValidation", "RunChecks");
                        break;
                    case "chart-original-playtest":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "ComparisonPlaytest");
                        break;
                    case "radio-rig-render":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "RigRender");
                        break;
                    case "radio-animation-preview":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "OpenAnimationPreview");
                        break;
                    case "radio-animation-reel":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "AnimationReel");
                        break;
                    case "radio-first-courier":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "FirstCourierProbe");
                        break;
                    case "radio-open-scene":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "OpenGameScene");
                        break;
                    case "radio-show-menu":
                        RequireEditMode();
                        Invoke("RadioArchipelagoJobs", "ShowMainMenu");
                        break;
                    case "radio-select-custom-song":
                        Invoke("RadioArchipelagoJobs", "SelectCustomSongPreview");
                        break;
                    case "chart-v9-preview-cancan":
                        Invoke("RadioArchipelagoJobs", "SelectV9CanCanPreview");
                        break;
                    case "chart-v10-export": Invoke("PcmChartStudyV10", "ExportNewPcm"); break;
                    case "chart-v10-freeze-checks": Invoke("PcmChartStudyV10", "RunFreezeChecks"); break;
                    case "chart-v10-baseline-known": Invoke("PcmChartStudyV10", "BaselineKnown"); break;
                    case "chart-v10-baseline-heldout": Invoke("PcmChartStudyV10", "BaselineHeldout"); break;
                    case "chart-v10-known": Invoke("PcmChartStudyV10", "CandidateKnown"); break;
                    case "chart-v10-heldout": Invoke("PcmChartStudyV10", "CandidateHeldout"); break;
                    case "chart-v10-publication-guards": Invoke("ChartTestPublicationV10", "RunGuardChecks"); break;
                    case "chart-v10-publish-tests": Invoke("ChartTestPublicationV10", "PublishTests"); break;
                    case "chart-v10-play-disconnect": Invoke("RadioArchipelagoJobs", "V10DisconnectPlaytest"); break;
                    case "chart-v10-play-cancan": Invoke("RadioArchipelagoJobs", "V10CanCanPlaytest"); break;
                    case "chart-v10-play-dance": Invoke("RadioArchipelagoJobs", "V10DancePlaytest"); break;
                    case "chart-v10-play-moonlight": Invoke("RadioArchipelagoJobs", "V10MoonlightPlaytest"); break;
                    case "chart-v10-play-elise": Invoke("RadioArchipelagoJobs", "V10ElisePlaytest"); break;
                    case "chart-v10-play-jingle": Invoke("RadioArchipelagoJobs", "V10JinglePlaytest"); break;
                    case "chart-v10-preview-cancan": Invoke("RadioArchipelagoJobs", "SelectV10CanCanPreview"); break;
                    case "chart-study-export": Invoke("ChartGeneratorStudy", "ExportPcm"); break;
                    case "chart-study-baseline-known": Invoke("ChartGeneratorStudy", "BaselineKnown"); break;
                    case "chart-study-baseline-heldout": Invoke("ChartGeneratorStudy", "BaselineHeldout"); break;
                    case "chart-study-known": Invoke("ChartGeneratorStudy", "CandidateKnown"); break;
                    case "chart-study-heldout": Invoke("ChartGeneratorStudy", "CandidateHeldout"); break;
                    case "chart-study-publish": Invoke("ChartGeneratorStudy", "PublishTests"); break;
                    case "chart-study-checks": Invoke("ChartGeneratorStudy", "RunChecks"); break;
                    case "chart-study-precision": Invoke("ChartStudyPrecisionDiagnostics", "Run"); break;
                    case "rhythm-timing-inspect": Invoke("RhythmTimingDiagnostics", "InspectLiveTiming"); break;
                    case "rhythm-timing-original": Invoke("RhythmTimingDiagnostics", "BeginOriginalJingle"); break;
                    case "rhythm-timing-v11": Invoke("RhythmTimingDiagnostics", "BeginV11Jingle"); break;
                    case "rhythm-timing-stop": Invoke("RhythmTimingDiagnostics", "StopRecording"); break;
                    case "rhythm-timing-512": Invoke("RhythmTimingDiagnostics", "Apply512ForComparison"); break;
                    case "rhythm-timing-v11-512": Invoke("RhythmTimingDiagnostics", "BeginV11Jingle512"); break;
                    case "rhythm-timing-restore": Invoke("RhythmTimingDiagnostics", "RestoreBufferConfiguration"); break;
                    case "rhythm-timing-serialization": Invoke("RhythmTimingDiagnostics", "RunSerializationChecks"); break;
                    case "rhythm-timing-v11-512-pose": Invoke("RhythmTimingDiagnostics", "BeginV11Jingle512Pose"); break;
                    case "rhythm-timing-apply-project": Invoke("RhythmTimingDiagnostics", "ApplyProjectBufferSettings"); break;
                    case "chart-legacy-support-checks": Invoke("PcmLegacySupportValidation", "RunChecks"); break;
                    case "chart-voiced-response-checks": Invoke("PcmVoicedResponseValidation", "RunChecks"); break;
                    case "chart-short-motif-checks": Invoke("PcmShortMotifValidation", "RunChecks"); break;
                    case string studyAction when studyAction.StartsWith("chart-study-play:", StringComparison.Ordinal):
                        InvokeString("RadioArchipelagoJobs", "StudyPlaytest", studyAction.Substring("chart-study-play:".Length)); break;
                    case string studyAction when studyAction.StartsWith("chart-study-reference:", StringComparison.Ordinal):
                        InvokeString("RadioArchipelagoJobs", "StudyReferencePlaytest", studyAction.Substring("chart-study-reference:".Length)); break;
                    case string studyAction when studyAction.StartsWith("chart-study-preview:", StringComparison.Ordinal):
                        InvokeString("RadioArchipelagoJobs", "StudyPreview", studyAction.Substring("chart-study-preview:".Length)); break;
                    case "chart-v11-play-cancan": Invoke("RadioArchipelagoJobs", "V11CanCanPlaytest"); break;
                    case "chart-v11-play-elise": Invoke("RadioArchipelagoJobs", "V11ElisePlaytest"); break;
                    case "chart-v11-play-jingle": Invoke("RadioArchipelagoJobs", "V11JinglePlaytest"); break;
                    case "chart-v11-play-ode": Invoke("RadioArchipelagoJobs", "V11OdePlaytest"); break;
                    case "chart-v11-play-bajo": Invoke("RadioArchipelagoJobs", "V11BajoPlaytest"); break;
                    case "chart-v11-play-baby": Invoke("RadioArchipelagoJobs", "V11BabyPlaytest"); break;
                    case "chart-v11-preview-jingle": Invoke("RadioArchipelagoJobs", "SelectV11JinglePreview"); break;
                    default: throw new ArgumentException("Unknown musical authoring job: " + action);
                }
                WriteResponse(action, true, "Completed authoring job");
            }
            catch (Exception error)
            {
                WriteResponse(action, false, (error.InnerException ?? error).ToString());
                Debug.LogError("MUSICAL_CHART_EDITOR: " + (error.InnerException ?? error).Message);
            }
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before changing or validating the authored map.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; save it before running authoring jobs.");
        }

        private static void Invoke(string typeName, string methodName)
        {
            var type = typeof(MusicalChartEditorSession).Assembly.GetType("DancingCats.Clone.Editor." + typeName, true);
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null) throw new MissingMethodException(typeName, methodName);
            method.Invoke(null, null);
        }

        private static void InvokeString(string typeName, string methodName, string value)
        {
            var type = typeof(MusicalChartEditorSession).Assembly.GetType("DancingCats.Clone.Editor." + typeName, true);
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (method == null) throw new MissingMethodException(typeName, methodName);
            method.Invoke(null, new object[] { value });
        }

        private static void WriteResponse(string action, bool success, string message)
        {
            var scene = SceneManager.GetActiveScene();
            var controller = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            var response = new Response
            {
                action = action, success = success, message = message,
                playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused,
                compiling = EditorApplication.isCompiling, scenePath = scene.path, sceneDirty = scene.isDirty,
                selectedSong = controller != null ? controller.Selected?.id : "",
                phase = controller != null ? controller.Phase.ToString() : "", clock = controller != null ? controller.GameTime : 0
            };
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "response.json"), JsonUtility.ToJson(response, true));
        }
    }
}
