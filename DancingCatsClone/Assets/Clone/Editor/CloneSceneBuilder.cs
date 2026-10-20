using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DancingCats.Clone.Editor
{
    public static class CloneSceneBuilder
    {
        [Serializable] private sealed class ThemeBindings { public string[] materials; }
        public const string ScenePath = "Assets/Clone/Scenes/DancingCats.unity";

        [MenuItem("Clone/Build independent original-content scene")]
        public static void Run()
        {
            RecoveredAssetImporter.Repair();
            Directory.CreateDirectory("Assets/Clone/Scenes");
            Directory.CreateDirectory("Assets/Clone/Generated");
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();
            for (int theme = 0; theme < 8; theme++)
            {
                var original = EditorSceneManager.OpenScene("Assets/Recovered/Scenes/level" + theme + ".unity", OpenSceneMode.Single);
                var railway = original.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Railway", StringComparison.Ordinal));
                if (railway != null)
                {
                    // The original Camera components remain in each authored scene. Runtime activates only the selected environment.
                    PrefabUtility.SaveAsPrefabAsset(railway, "Assets/Clone/Generated/Environment" + theme + ".prefab");
                }
                var end = original.GetRootGameObjects().FirstOrDefault(g => g.name == "EndPlatform");
                if (end != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(end, "Assets/Clone/Generated/EndPlatform" + theme + ".prefab");
                }
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Retain the original Volume rather than bringing template lighting into the restored environments.
            var original5 = EditorSceneManager.OpenScene("Assets/Recovered/Scenes/level5.unity", OpenSceneMode.Additive);
            foreach (var root in original5.GetRootGameObjects().Where(g => g.name.StartsWith("GlobalVolume", StringComparison.Ordinal)))
            {
                var copy = UnityEngine.Object.Instantiate(root);
                copy.name = root.name;
                SceneManager.MoveGameObjectToScene(copy, scene);
            }
            EditorSceneManager.CloseScene(original5, true);
            SceneManager.SetActiveScene(scene);
            // Opening recovered scenes unloads unused assets. Resolve the persistent library only
            // after the last scene switch, then immediately attach it to the clone controller.
            var library = AssetDatabase.LoadAssetAtPath<OriginalAssets>("Assets/Resources/OriginalAssets.asset");
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<OriginalAssets>();
                AssetDatabase.CreateAsset(library, "Assets/Resources/OriginalAssets.asset");
            }
            library.prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Recovered", "Assets/Clone/Generated" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => new OriginalAssets.PrefabEntry { name = Path.GetFileNameWithoutExtension(p), asset = AssetDatabase.LoadAssetAtPath<GameObject>(p) })
                .Where(p => p.asset != null).OrderBy(p => p.name, StringComparer.Ordinal).ToArray();
            // Native Sprite .asset files are independent of TextureImporter subassets.
            // Both forms are present in the recovered UI, so discover both explicitly.
            library.sprites = CollectSprites();
            library.fonts = AssetDatabase.FindAssets("t:Font", new[] { "Assets/Recovered" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Font>).Where(f => f != null).ToArray();
            var themes = Resources.Load<TextAsset>("OriginalData/theme-visual-bindings");
            if (themes != null)
                library.themeMaterials = JsonUtility.FromJson<ThemeBindings>(themes.text).materials.Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
            EditorUtility.SetDirty(library);
            var controller = new GameObject("Dancing Cats offline reconstruction").AddComponent<OriginalPlayController>();
            controller.Assets = library;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Dogmaruz";
            PlayerSettings.productName = "Dancing Cats Offline Clone";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.dogmaruz.dancingcatsoffline");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.dogmaruz.dancingcatsoffline");
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            OriginalAssetLibraryValidation.Run();
            Debug.Log("CLONE_SCENE PASS: original library " + library.prefabs.Length + " prefabs, " + library.sprites.Length + " sprites, independent scene " + ScenePath);
        }

        public static void ValidateAndBuildScene()
        {
            NativeRulesValidation.Run();
            OriginalRoleMotionValidation.Run();
            ProgressionValidation.Run();
            OriginalTileVisualValidation.Run();
            Run();
        }

        private static Sprite[] CollectSprites() => AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Recovered" })
            .Concat(AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Recovered" }))
            .Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Sprite>().Distinct().ToArray();

        public static void RepairSpriteLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<OriginalAssets>("Assets/Resources/OriginalAssets.asset");
            if (library == null) throw new InvalidOperationException("Build the independent scene before repairing its library.");
            library.sprites = CollectSprites();
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            OriginalAssetLibraryValidation.Run();
        }

        public static void TileAndScene()
        {
            Run();
            OriginalTileVisualValidation.Run();
            var themeTest = typeof(CloneSceneBuilder).Assembly.GetType("DancingCats.Clone.Editor.OriginalThemeVisualValidation");
            themeTest?.GetMethod("Run")?.Invoke(null, null);
        }

        public static void ProgressionThemesAndTiles()
        {
            ProgressionValidation.Run();
            OriginalThemeVisualValidation.Run();
            OriginalTileVisualValidation.Run();
        }
    }
}
