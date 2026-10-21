using System;
using System.IO;
using System.Linq;
using RhythmGame.Composition;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VContainer;
using RadioArchipelago;
using RadioArchipelago.UI;

namespace DancingCats.Clone.Editor
{
    public static class DependencyInjectionValidation
    {
        public static void RunChecks()
        {
            var scopeType = typeof(OriginalPlayController).Assembly.GetType("RhythmGame.Composition.GameLifetimeScope");
            if (scopeType == null) throw new InvalidOperationException("DI scope is missing: the game cannot compose injected dependencies.");
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ContainerBuilder).Assembly);
            Require(package != null && package.version == "1.19.0", "Pinned VContainer package must be installed");
            var scene = SceneManager.GetActiveScene();
            Require(scene.path == CloneSceneBuilder.ScenePath && !scene.isDirty, "Validate the saved gameplay scene");
            var scopes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameLifetimeScope>(true)).ToArray();
            Require(scopes.Length == 1, "Gameplay scene must have one composition root");
            var serialized = new SerializedObject(scopes[0]);
            foreach (string name in new[] { "controller", "presenter", "eventSystem" })
                Require(serialized.FindProperty(name).objectReferenceValue != null, "Missing composition binding: " + name);
            Require(serialized.FindProperty("volumes").arraySize > 0, "Post-processing targets must be wired once during authoring");
            DependencyInjectionBehaviorChecks.Run();
            ProgressionValidation.Run();
            ClonePlayerBuild.Verify(); // Static/content validations only; this method never builds a player.
            MeowLevelValidation.RunChecks();
            Debug.Log("DI_VALIDATION PASS: package, scene composition, isolated services, run lifecycle and existing content checks");
        }

        public static void MigrateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            Require(scene.path == CloneSceneBuilder.ScenePath && !scene.isDirty, "DI migration requires the saved gameplay scene");
            string backup = Path.GetFullPath("Library/DependencyInjectionValidation/DancingCats.before-di.unity");
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(CloneSceneBuilder.ScenePath, backup);
            var controllers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OriginalPlayController>(true)).ToArray();
            Require(controllers.Length == 1, "Expected one gameplay controller");
            ConfigureScene(controllers[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save the composed gameplay scene");
            AssetDatabase.SaveAssets();
            Debug.Log("DI_SCENE PASS: composition bindings saved; original assets and scene GUID preserved");
        }

        public static void ConfigureScene(OriginalPlayController controller)
        {
            var scene = controller.gameObject.scene;
            var legacy = controller.GetComponent<OriginalUIPresenter>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
            var presenter = controller.GetComponent<RadioMenuPresenter>() ?? controller.gameObject.AddComponent<RadioMenuPresenter>();
            var systems = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).ToArray();
            Require(systems.Length <= 1, "Scene has multiple input event systems");
            EventSystem events;
            if (systems.Length == 0)
            {
                var input = new GameObject("Game UI Input", typeof(EventSystem));
                input.transform.SetParent(controller.transform, false);
                events = input.GetComponent<EventSystem>();
            }
            else events = systems[0];
            if (events.name == "Game UI Input" && events.TryGetComponent<InputSystemUIInputModule>(out var generatedModule))
                UnityEngine.Object.DestroyImmediate(generatedModule);
            var scope = controller.GetComponent<GameLifetimeScope>() ?? controller.gameObject.AddComponent<GameLifetimeScope>();
            var volumes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true)).ToArray();
            var art = AssetDatabase.LoadAssetAtPath<RadioArtAssets>("Assets/RadioArchipelago/Content/RadioArtAssets.asset");
            var couriers = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            Require(art != null && couriers != null, "Create Radio Archipelago art and courier assets before composing the scene");
            scope.SetBindings(controller, presenter, events, volumes, art, couriers);
            EditorUtility.SetDirty(scope);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
