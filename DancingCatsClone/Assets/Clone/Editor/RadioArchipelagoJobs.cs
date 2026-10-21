using System;
using System.IO;
using RadioArchipelago.Editor;
using UnityEditor;
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

        private static void RequireSavedEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before authoring Radio Archipelago assets.");
        }
    }
}
