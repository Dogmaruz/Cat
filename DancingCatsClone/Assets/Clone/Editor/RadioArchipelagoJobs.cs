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
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Open the game menu in Play Mode first.");
            var game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            if (game == null || game.Phase != ClonePhase.Menu)
                throw new InvalidOperationException("The song menu is not ready.");
            var song = game.Songs.Single(record => record.id == "CanCanPianoGeneratorTestV9");
            if (!game.SelectSong(song.catalogId)) throw new InvalidOperationException("The Can Can v9 test cannot be selected.");
            game.Endless = false;
            Debug.Log("CHART_V9_PREVIEW PASS: CanCanPianoGeneratorTestV9 selected in the menu.");
        }

        private static void RequireSavedEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before authoring Radio Archipelago assets.");
        }
    }
}
