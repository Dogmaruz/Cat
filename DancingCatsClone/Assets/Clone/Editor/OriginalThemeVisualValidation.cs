using System;
using System.Collections.Generic;
using System.Reflection;
using RhythmGame.Services;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class OriginalThemeVisualValidation
    {
        [Serializable] private sealed class Manifest { public string[] materials; public EnvironmentBinding[] environments; public PrefabBinding[] prefabs; }
        [Serializable] private sealed class EnvironmentBinding { public int environmentIndex; public string[] allThemes, allThemesTarget; public Color[] themeColor; }
        [Serializable] private sealed class PrefabBinding { public string prefab; public RendererBinding[] bindings; }
        [Serializable] private sealed class RendererBinding { public string rendererPath; public int[] materials; }

        private sealed class CountingResources : IGameResources
        {
            private readonly IGameResources resources = new UnityGameResources();
            public int LoadCount { get; private set; }
            public T Load<T>(string path) where T : UnityEngine.Object
            {
                LoadCount++;
                return resources.Load<T>(path);
            }
        }

        public static void Run()
        {
            var type = typeof(RecoveredCatalog).Assembly.GetType("DancingCats.Clone.OriginalThemeVisuals");
            Require(type != null, "original theme visuals are not implemented");
            var data = Resources.Load<TextAsset>("OriginalData/theme-visual-bindings");
            Require(data != null, "original theme binding manifest exists");
            var manifest = JsonUtility.FromJson<Manifest>(data.text);
            var resources = new CountingResources();
            var catalog = new ThemeCatalog(resources);
            Require(catalog.MaterialCount == manifest.materials.Length && catalog.MaterialCount == manifest.materials.Length,
                "injected theme catalog preserves material ordering and reuses its manifest");
            Require(resources.LoadCount == 1, "theme manifest is loaded once per application scope");
            var otherResources = new CountingResources();
            Require(new ThemeCatalog(otherResources).MaterialCount == manifest.materials.Length && otherResources.LoadCount == 1,
                "a separate application scope owns its own theme manifest");
            var library = AssetDatabase.LoadAssetAtPath<OriginalAssets>("Assets/Resources/OriginalAssets.asset");
            Require(library != null && library.themeMaterials.Length == manifest.materials.Length,
                "builder preserves every original palette material in the runtime library");
            for (int i = 0; i < manifest.materials.Length; i++)
                Require(library.themeMaterials[i] == AssetDatabase.LoadAssetAtPath<Material>(manifest.materials[i]),
                    "palette material order matches its original asset pointer");
            foreach (var prefab in manifest.prefabs)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/" + prefab.prefab + ".prefab");
                Require(asset != null, "original theme prefab exists: " + prefab.prefab);
                foreach (var binding in prefab.bindings)
                {
                    var target = string.IsNullOrEmpty(binding.rendererPath) ? asset.transform : asset.transform.Find(binding.rendererPath);
                    Require(target != null && target.GetComponent<Renderer>() != null,
                        "original palette resolves its exact renderer: " + prefab.prefab + "/" + binding.rendererPath);
                }
            }
            foreach (var environmentBinding in manifest.environments)
            {
                int number = environmentBinding.environmentIndex;
                Require(number >= 1 && number <= 7, "theme environment numbers match gameplay scenes1..7");
                var environment = UnityEngine.Object.Instantiate(library.Prefab("Environment" + number));
                var owner = new GameObject("OriginalThemeVisual validation");
                try
                {
                    string prefabName = "Jump1" + (number == 1 ? "" : "_" + (number - 2));
                    var binding = Array.Find(manifest.prefabs, item => item.prefab == prefabName).bindings[0];
                    var tiles = new List<OriginalTile>();
                    foreach (float z in new[] { 0f, 10f })
                    {
                        var instance = UnityEngine.Object.Instantiate(library.Prefab(prefabName), owner.transform);
                        instance.transform.position = Vector3.forward * z;
                        var tile = instance.AddComponent<OriginalTile>();
                        tile.element = new MapElement { id = 0, pos = instance.transform.position };
                        tile.initialZ = z;
                        tiles.Add(tile);
                    }
                    var component = owner.AddComponent(type);
                    Invoke(type, component, "Bind", environment, tiles, library.themeMaterials, number, catalog);
                    Require((int)type.GetProperty("Index").GetValue(component) == 0, "original palette starts at0");
                    Invoke(type, component, "Advance", 6f);
                    Require((int)type.GetProperty("Index").GetValue(component) == 1, "Theme tile advances the original palette");
                    Require(!environment.transform.Find(environmentBinding.allThemes[0]).gameObject.activeSelf &&
                        environment.transform.Find(environmentBinding.allThemes[1]).gameObject.activeSelf,
                        "original background activates the corresponding authored group");
                    var ahead = tiles[1].transform.Find(binding.rendererPath).GetComponent<Renderer>();
                    var behind = tiles[0].transform.Find(binding.rendererPath).GetComponent<Renderer>();
                    Require(ahead.sharedMaterial == library.themeMaterials[binding.materials[1]], "ahead tile adopts exact palette1 material");
                    Require(behind.sharedMaterial == library.themeMaterials[binding.materials[0]], "tiles beyond native rear margin retain previous palette");
                    Invoke(type, component, "Advance", 6f);
                    Invoke(type, component, "Advance", 6f);
                    Require((int)type.GetProperty("Index").GetValue(component) == 0, "palette wraps after all authored scenery groups");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(environment);
                    UnityEngine.Object.DestroyImmediate(owner);
                }
            }
            Require(resources.LoadCount == 1, "all environment bindings reuse their injected theme manifest");
            Debug.Log("ORIGINAL_THEME_VISUALS PASS: seven environments,114 materials,223 exact renderer bindings, native palette cycling and scoped manifest caching");
        }

        private static object Invoke(Type type, Component component, string name, params object[] values)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Require(method != null, "theme interface exists: " + name);
            return method.Invoke(component, values);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("ORIGINAL_THEME_VISUALS RED/FAIL: " + message);
        }
    }
}
