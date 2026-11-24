using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Isolated, reversible relief experiment on the first musical island.</summary>
    public static class IslandReliefJobs
    {
        public const string ScenePath = "Assets/Editor/SceneTemplates/Prototypes/EmeraldReliefPilot.unity";
        public const string ContentPath = "Assets/RadioArchipelago/Content/Islands/EmeraldRelief";
        public const string ImportPath = "Assets/RadioArchipelago/Imported/Islands/EmeraldRelief";
        public const string DefinitionPath = ContentPath + "/EmeraldRelief.asset";
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class Report
        {
            public bool passed, flatPilotPreserved, musicTimingPreserved;
            public string scene = ScenePath;
            public float routeLength, groundMax, playableMax, maxGrade, maxSkyGap;
            public int renderers, protectedGroundMaterials;
            public long triangles;
        }

        [MenuItem("Radio Archipelago/Relief experiment/Prepare scene")]
        public static void Create()
        {
            RequireEditMode();
            foreach (string file in new[] { "emerald_relief_island.fbx", "emerald_relief_cobbles.fbx", "emerald_relief_route.json", "ground_elevation.json", "play_elevation.json" })
                if (!File.Exists(ImportPath + "/" + file)) throw new FileNotFoundException("Relief export is not ready", file);
            Directory.CreateDirectory(ContentPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Emerald relief prototype"); SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                Attach(AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath + "/emerald_relief_island.fbx"), root.transform, "Relief island basis");
                Attach(AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath + "/emerald_relief_cobbles.fbx"), root.transform, "Relief cobblestone basis");
                PrefabUtility.SaveAsPrefabAsset(root, ContentPath + "/EmeraldRelief.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            if (!File.Exists(DefinitionPath) && !AssetDatabase.CopyAsset(IslandPilotJobs.DefinitionPath, DefinitionPath))
                throw new IOException("Could not copy the flat pilot definition");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            string chartPath = ContentPath + "/AlwaysRememberMeowRelief.timeline.json";
            if (!File.Exists(chartPath) && !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(definition.Chart), chartPath))
                throw new IOException("Could not preserve a separate relief chart");
            definition.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContentPath + "/EmeraldRelief.prefab");
            definition.RouteJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/emerald_relief_route.json");
            definition.ElevationJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/play_elevation.json");
            definition.Chart = AssetDatabase.LoadAssetAtPath<TextAsset>(chartPath);
            definition.CameraHeight = 10.5f;
            definition.CameraBackDistance = 12.5f;
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(IslandPilotJobs.ScenePath, ScenePath))
                throw new IOException("Could not preserve a separate relief scene");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            var game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            var stage = UnityEngine.Object.FindFirstObjectByType<IslandStagePresentation>();
            if (game == null || stage == null) throw new InvalidOperationException("Copied pilot game/stage is missing");
            stage.Configure(game, definition); game.SetIslandStage(stage);
            EditorUtility.SetDirty(stage); EditorUtility.SetDirty(game);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            RunChecks();
            Debug.Log("ISLAND_RELIEF_READY " + ScenePath + " — enter Play and press ИГРАТЬ.");
        }

        internal static void Attach(GameObject model, Transform parent, string name, Func<string, Material> materialFor = null)
        {
            if (model == null) throw new InvalidOperationException("Relief FBX failed to import");
            var basis = new GameObject(name).transform; basis.SetParent(parent, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent.gameObject.scene); instance.transform.SetParent(basis, false);
            var anchors = instance.GetComponentsInChildren<Transform>(true);
            Transform origin = anchors.First(t => t.name == "RA_Origin"), north = anchors.First(t => t.name == "RA_North10");
            float yaw = Vector3.SignedAngle(north.position - origin.position, Vector3.back, Vector3.up);
            instance.transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * instance.transform.localRotation;
            if (anchors.First(t => t.name == "RA_X10").position.x < 0) basis.localScale = new Vector3(-1, 1, 1);
            foreach (var anchor in new[] { ("RA_Origin", Vector3.zero), ("RA_X10", new Vector3(10, 0, 0)), ("RA_North10", new Vector3(0, 0, -10)) })
                if (Vector3.Distance(anchors.First(t => t.name == anchor.Item1).position, anchor.Item2) > .002f)
                    throw new InvalidOperationException("Relief coordinate anchor mismatch: " + anchor.Item1);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => (materialFor ?? MaterialFor)(m.name)).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static Material MaterialFor(string sourceName)
        {
            string name = Regex.Replace(sourceName.Replace(" (Instance)", ""), @"\.\d{3}$", "");
            string path = ContentPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                string source = name.Contains("StepTreads") ? "RA_Emerald_Wood" : name.Contains("BridgeDeck") ? "RA_Emerald_Rocks" : name;
                var original = AssetDatabase.LoadAssetAtPath<Material>(IslandPilotJobs.ContentPath + "/" + source + ".mat");
                material = original != null ? new Material(original) : new Material(Shader.Find("RadioArchipelago/Island Surface"));
                if (original == null)
                {
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Cull", name.Contains("Foliage") ? 0 : 2);
                    material.SetTexture("_DetailMap", AssetDatabase.LoadAssetAtPath<Texture2D>(IslandPilotJobs.ImportPath + "/rock_albedo.png"));
                    material.SetFloat("_DetailStrength", name.Contains("Rocks") ? .65f : name.Contains("Terrain") ? .32f : 0);
                    material.SetFloat("_DetailScale", .35f);
                }
                material.name = name; AssetDatabase.CreateAsset(material, path);
            }
            bool ground = name.Contains("Terrain") || name.Contains("Road") || name.Contains("Bridge") || name.Contains("StepTreads");
            material.SetFloat("_ProtectGameplaySurface", ground ? 1 : 0);
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("Radio Archipelago/Relief experiment/Checks")]
        public static void RunChecks()
        {
            IslandPilotJobs.RunChecks();
            IslandElevationChecks.RunChecks(); IslandFlightElevationChecks.RunChecks();
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            if (definition == null || definition.ElevationJson == null || definition.Prefab == null)
                throw new InvalidOperationException("Relief assets/bindings are missing");
            var flat = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandPilotJobs.DefinitionPath);
            if (flat.ElevationJson != null || flat.Prefab == definition.Prefab || flat.RouteJson.text != definition.RouteJson.text)
                throw new InvalidOperationException("The original flat pilot must remain separate and its XZ route unchanged");
            if (flat.Chart.text != definition.Chart.text) throw new InvalidOperationException("Relief must preserve the approved musical chart");
            var elevation = IslandElevationProfile.FromJson(definition.ElevationJson.text);
            var ground = IslandElevationProfile.FromJson(File.ReadAllText(ImportPath + "/ground_elevation.json"));
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points, elevation);
            var report = new Report { routeLength = route.Length, flatPilotPreserved = true, musicTimingPreserved = true };
            for (float s = 0; s < route.Length; s += .1f)
            {
                report.groundMax = Mathf.Max(report.groundMax, ground.Evaluate(s));
                report.playableMax = Mathf.Max(report.playableMax, elevation.Evaluate(s));
                report.maxGrade = Mathf.Max(report.maxGrade, Mathf.Abs(elevation.Grade(s)));
                report.maxSkyGap = Mathf.Max(report.maxSkyGap, elevation.Evaluate(s) - ground.Evaluate(s));
            }
            var renderers = definition.Prefab.GetComponentsInChildren<MeshRenderer>(true); report.renderers = renderers.Length;
            var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            if (materials.Any(m => m == null || m.shader == null || m.shader.name != "RadioArchipelago/Island Surface"))
                throw new InvalidOperationException("Relief material is missing or incompatible");
            report.protectedGroundMaterials = materials.Count(m => m.GetFloat("_ProtectGameplaySurface") > .5f);
            if (report.protectedGroundMaterials < 4 || report.groundMax < 7 || report.maxSkyGap < 11)
                throw new InvalidOperationException("Hill, canopy route or opaque walkable surfaces are missing");
            report.triangles = definition.Prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => (long)f.sharedMesh.GetIndexCount(0) / 3);
            report.passed = true; Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
            File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "relief-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("ISLAND_RELIEF_CHECKS_PASS triangles=" + report.triangles + " height=" + report.playableMax);
        }

        internal static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play Mode and wait for compilation before relief authoring");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before relief authoring");
        }
    }
}
