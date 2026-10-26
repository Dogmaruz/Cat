using System;
using System.IO;
using System.Linq;
using RadioArchipelago.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DancingCats.Clone.Editor
{
    public static class RadioArchipelagoJobs
    {
        public static void CreateAssets()
        {
            RequireSavedEditor();
            CourierAssetBuilder.Run();
            RadioArtAssetBuilder.Run();
        }

        public static void MigrateScene()
        {
            RequireSavedEditor();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != CloneSceneBuilder.ScenePath) throw new InvalidOperationException("Open the saved gameplay scene first.");
            string backup = Path.GetFullPath("Library/RadioArchipelagoReview/before-radio.unity");
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(scene.path, backup);
            DependencyInjectionValidation.MigrateScene();
            Debug.Log("RADIO_SCENE PASS: new menu and couriers wired to the existing rhythm session through DI");
        }

        public static void RunChecks()
        {
            RequireSavedEditor();
            CourierSelectionChecks.Run();
            RadioEditorValidation.RunChecks();
            DependencyInjectionValidation.RunChecks();
        }

        public static void Playtest()
        {
            RequireSavedEditor();
            RadioEditorValidation.RunPlaytest();
        }

        public static void ComparisonPlaytest()
        {
            RequireSavedEditor();
            RadioEditorValidation.RunComparisonPlaytest();
        }

        public static void V9DisconnectPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV9DisconnectPlaytest(); }
        public static void V9CanCanPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV9CanCanPlaytest(); }
        public static void V9DancePlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV9DancePlaytest(); }
        public static void V9CloudsPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV9CloudsPlaytest(); }
        public static void V10DisconnectPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV10DisconnectPlaytest(); }
        public static void V10CanCanPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV10CanCanPlaytest(); }
        public static void V10DancePlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV10DancePlaytest(); }
        public static void V10MoonlightPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV10MoonlightPlaytest(); }
        public static void V10ElisePlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV10ElisePlaytest(); }
        public static void V10JinglePlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV10JinglePlaytest(); }
        public static void V11CanCanPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV11CanCanPlaytest(); }
        public static void V11ElisePlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV11ElisePlaytest(); }
        public static void V11JinglePlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV11JinglePlaytest(); }
        public static void V11OdePlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV11OdePlaytest(); }
        public static void V11BajoPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV11BajoPlaytest(); }
        public static void V11BabyPlaytest() { RequireSavedEditor(); RadioEditorValidation.RunV11BabyPlaytest(); }

        public static void StudyPlaytest(string sourceId)
        {
            RequireSavedEditor();
            var manifest = ChartGeneratorStudy.LoadManifest();
            if (!manifest.publishSourceIds.Contains(sourceId))
                throw new ArgumentException("This source is not selected for test publication.", nameof(sourceId));
            RadioEditorValidation.RunNamedPlaytest(sourceId + manifest.testSuffix);
        }

        public static void StudyReferencePlaytest(string sourceId)
        {
            RequireSavedEditor();
            if (!ChartGeneratorStudy.LoadManifest().sources.Any(source => source.id == sourceId))
                throw new ArgumentException("This reference is not selected for the study.", nameof(sourceId));
            RadioEditorValidation.RunNamedPlaytest(sourceId);
        }

        public static void StudyPreview(string sourceId)
        {
            var manifest = ChartGeneratorStudy.LoadManifest();
            if (!manifest.publishSourceIds.Contains(sourceId))
                throw new ArgumentException("This source is not selected for test publication.", nameof(sourceId));
            SelectTestPreview(sourceId + manifest.testSuffix);
        }

        public static void MenuPreviewChecks()
        {
            RequireSavedEditor();
            RadioEditorValidation.RunMenuPreviewReview();
        }

        public static void MenuBakeProbe()
        {
            RequireSavedEditor();
            CourierMenuPreviewChecks.ProbeBakeSpaces();
        }

        public static void LightingAssets()
        {
            RequireSavedEditor();
            var catalog = AssetDatabase.LoadAssetAtPath<RadioArchipelago.CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            CourierLightingAssetBuilder.Ensure(catalog);
            Debug.Log("RADIO_COURIER_LIGHTING_ASSETS PASS: shared studio profile and static HDR reflections are assigned");
        }

        public static void RefreshLightingReflections()
        {
            RequireSavedEditor();
            CourierLightingAssetBuilder.RebuildReflections();
        }

        public static void MotionChecks()
        {
            RequireSavedEditor();
            CourierMotionChecks.Run();
        }

        public static void ImportCouriers()
        {
            RequireSavedEditor();
            ImportedCourierAssetBuilder.Run();
        }

        public static void RigChecks()
        {
            RequireSavedEditor();
            ImportedCourierValidation.RunChecks();
        }

        public static void AccessoryChecks()
        {
            RequireSavedEditor();
            CourierAccessoryChecks.RunChecks();
        }

        public static void RigidPawChecks()
        {
            RequireSavedEditor();
            CourierRigidPawChecks.RunChecks();
        }

        public static void SurfaceBaseline() { RequireSavedEditor(); CourierSurfaceReview.RenderBaseline(); }
        public static void SurfaceCandidate() { RequireSavedEditor(); CourierSurfaceReview.RenderCandidate(); }
        public static void RoundedPawChecks() { RequireSavedEditor(); CourierRoundedPawChecks.RunChecks(); }
        public static void JointBaseline() { RequireSavedEditor(); CourierJointReview.Baseline(); }
        public static void JointCandidate() { RequireSavedEditor(); CourierJointReview.Candidate(); }
        public static void ShoulderTrials() { RequireSavedEditor(); CourierJointReview.ShoulderTrials(); }

        public static void RigRender()
        {
            RequireSavedEditor();
            ImportedCourierValidation.RenderReview();
        }

        public static void OpenAnimationPreview()
        {
            RequireSavedEditor();
            CourierAnimationPreviewWindow.OpenFlight();
        }

        public static void AnimationReel()
        {
            RequireSavedEditor();
            CourierAnimationReel.Render();
        }

        public static void HopBaseline() { RequireSavedEditor(); CourierHopVisualReview.Baseline(); }
        public static void HopCandidate() { RequireSavedEditor(); CourierHopVisualReview.Candidate(); }
        public static void HopChecks() { RequireSavedEditor(); CourierHopChecks.Run(); }

        public static void FirstCourierProbe()
        {
            RequireSavedEditor();
            var entries = new[] { ImportedCourierAssetBuilder.ImportEntry(0) };
            ImportedCourierValidation.RunChecks(entries);
            ImportedCourierValidation.RenderReview(entries);
        }

        public static void OpenGameScene()
        {
            RequireSavedEditor();
            EditorSceneManager.OpenScene(CloneSceneBuilder.ScenePath);
        }

        public static void ShowMainMenu()
        {
            RequireSavedEditor();
            if (SceneManager.GetActiveScene().path != CloneSceneBuilder.ScenePath)
                EditorSceneManager.OpenScene(CloneSceneBuilder.ScenePath);
            EditorApplication.isPlaying = true;
        }

        public static void SelectCustomSongPreview()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Open the game menu in Play Mode first.");
            var game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            if (game == null || game.Phase != ClonePhase.Menu || !game.SelectSong(20001))
                throw new InvalidOperationException("The custom-song menu is not ready.");
        }

        public static void SelectV9CanCanPreview()
            => SelectTestPreview("CanCanPianoGeneratorTestV9");

        public static void SelectV10CanCanPreview()
            => SelectTestPreview("CanCanPianoGeneratorTestV10");

        public static void SelectV11JinglePreview()
            => SelectTestPreview("JingleBellsGeneratorTestV11");

        private static void SelectTestPreview(string id)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Open the game menu in Play Mode first.");
            var game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            if (game == null || game.Phase != ClonePhase.Menu)
                throw new InvalidOperationException("The song menu is not ready.");
            var song = game.Songs.Single(record => record.id == id);
            if (!game.SelectSong(song.catalogId)) throw new InvalidOperationException("The requested test cannot be selected.");
            game.Endless = false;
            Debug.Log("CHART_TEST_PREVIEW PASS: " + id + " selected in the menu.");
        }

        private static void RequireSavedEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before authoring Radio Archipelago assets.");
        }
    }
}
