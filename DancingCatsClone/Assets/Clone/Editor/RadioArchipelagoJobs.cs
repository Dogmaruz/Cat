using System;
using System.IO;
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

        public static void RigRender()
        {
            RequireSavedEditor();
            ImportedCourierValidation.RenderReview();
        }

        public static void OpenAnimationPreview()
        {
            RequireSavedEditor();
            CourierAnimationPreviewWindow.Open();
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

        private static void RequireSavedEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before authoring Radio Archipelago assets.");
        }
    }
}
