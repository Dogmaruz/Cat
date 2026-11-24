using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using RadioArchipelago.Editor;
using RadioArchipelago.World;
using RhythmGame.Composition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DancingCats.Clone.Editor
{
    public static class IslandPilotJobs
    {
        public const string ScenePath = "Assets/Editor/SceneTemplates/Prototypes/EmeraldIslandPilot.unity";
        public const string ContentPath = "Assets/RadioArchipelago/Content/Islands/Emerald";
        public const string ImportPath = "Assets/RadioArchipelago/Imported/Islands/Emerald";
        public const string OriginalMap = "Assets/Resources/CustomData/Maps/AlwaysRememberMeowCat.timeline.json";
        public const string DefinitionPath = ContentPath + "/EmeraldIsland.asset";
        public static string ReviewPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/IslandPilotReview"));

        [MenuItem("Radio Archipelago/Island pilot/Prepare first island")]
        public static void Create()
        {
            RequireCleanEditor();
            string export = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtSource/RadioArchipelago/Islands/V4/UnityExport/Output"));
            foreach (string file in new[] { "emerald_island.fbx", "emerald_route.json", "rock_albedo.png", "export-report.json" })
                if (!File.Exists(Path.Combine(export, file))) throw new FileNotFoundException("Island export is incomplete", file);
            Directory.CreateDirectory(ImportPath); Directory.CreateDirectory(ContentPath);
            Directory.CreateDirectory("Assets/RadioArchipelago/Scenes"); Directory.CreateDirectory(ReviewPath);
            foreach (string file in new[] { "emerald_island.fbx", "emerald_route.json", "rock_albedo.png", "export-report.json" })
                File.Copy(Path.Combine(export, file), Path.Combine(ImportPath, file), true);
            if (File.Exists(Path.Combine(export, "emerald_island_clean.fbx")))
            {
                File.Copy(Path.Combine(export, "emerald_island_clean.fbx"), Path.Combine(ImportPath, "emerald_island.fbx"), true);
                if (File.Exists(Path.Combine(export, "clean-export-report.json")))
                    File.Copy(Path.Combine(export, "clean-export-report.json"), Path.Combine(ImportPath, "clean-report.json"), true);
            }
            foreach (string file in new[] { "emerald_cobbles.fbx", "emerald_cobbles_report.json" })
                if (File.Exists(Path.Combine(export, file))) File.Copy(Path.Combine(export, file), Path.Combine(ImportPath, file), true);
            if (File.Exists(Path.Combine(export, "emerald_cobbles_v2.fbx")))
            {
                File.Copy(Path.Combine(export, "emerald_cobbles_v2.fbx"), Path.Combine(ImportPath, "emerald_cobbles.fbx"), true);
                File.Copy(Path.Combine(export, "emerald_cobbles_v2_report.json"), Path.Combine(ImportPath, "emerald_cobbles_report.json"), true);
            }
            var sourceMap = File.ReadAllText(OriginalMap);
            var pilot = IslandChartAuthoring.Create(sourceMap);
            string chartPath = ContentPath + "/AlwaysRememberMeowIsland.timeline.json";
            BackupPilotInputs(chartPath);
            File.WriteAllText(chartPath, JsonUtility.ToJson(pilot, true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ImportPath + "/emerald_island.fbx");
            importer.globalScale = 1; importer.useFileScale = true;
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
            importer.generateSecondaryUV = false; importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
            string cobblePath = ImportPath + "/emerald_cobbles.fbx";
            if (File.Exists(cobblePath))
            {
                var cobbleImporter = (ModelImporter)AssetImporter.GetAtPath(cobblePath);
                cobbleImporter.globalScale = 1; cobbleImporter.useFileScale = true;
                cobbleImporter.importCameras = false; cobbleImporter.importLights = false; cobbleImporter.importAnimation = false;
                cobbleImporter.generateSecondaryUV = false; cobbleImporter.isReadable = false;
                cobbleImporter.importNormals = ModelImporterNormals.Import; cobbleImporter.SaveAndReimport();
            }
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(ImportPath + "/rock_albedo.png");
            textureImporter.wrapMode = TextureWrapMode.Repeat; textureImporter.sRGBTexture = true;
            textureImporter.maxTextureSize = 2048; textureImporter.anisoLevel = 4;
            textureImporter.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath + "/emerald_island.fbx");
            if (model == null) throw new InvalidOperationException("FBX import did not produce a model");
            var wrapper = new GameObject("Emerald ravine — runtime island");
            try
            {
                AttachModel(model, wrapper.transform, "Island model basis");
                var cobbles = AssetDatabase.LoadAssetAtPath<GameObject>(cobblePath);
                if (cobbles != null) AttachModel(cobbles, wrapper.transform, "Cobblestone road basis");
                PrefabUtility.SaveAsPrefabAsset(wrapper, ContentPath + "/EmeraldIsland.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
            var definition = LoadOrCreate<IslandStageDefinition>(DefinitionPath);
            definition.SongId = "AlwaysRememberMeowCat";
            definition.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContentPath + "/EmeraldIsland.prefab");
            definition.RouteJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/emerald_route.json");
            definition.Chart = AssetDatabase.LoadAssetAtPath<TextAsset>(chartPath);
            definition.HoldBodyShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/RadioArchipelago/Art/NativeHoldIvory.shader");
            definition.HoldTracerShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Clone/Shaders/Recovered/Recovered_Cats_Ecolor_A.shader");
            definition.HoldBandOpacity = .24f; definition.HoldLineOpacity = 1f;
            CreateReadableEffectMeshes(definition);
            definition.DistanceScale = IslandChartAuthoring.WorldDistanceScale; definition.TileElevation = 1f; definition.TileThickness = .30f; definition.RoadWidth = 6;
            definition.GlowEmission = 0; definition.CourierScale = .64f;
            definition.LateralLimit = IslandChartAuthoring.LaneLimit; definition.VisibleAhead = 34; definition.VisibleBehind = 4;
            definition.CameraHeight = 7.8f; definition.CameraBackDistance = 11.5f;
            definition.CameraLookAhead = 8.5f; definition.CameraFieldOfView = 48; definition.CameraSmoothing = 7;
            EditorUtility.SetDirty(definition);
            var pipeline = CreateQuality();
            AssetDatabase.SaveAssets();
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(CloneSceneBuilder.ScenePath, ScenePath))
                throw new IOException("Could not copy the existing game scene");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Opening a scene unloads otherwise unreferenced assets; resolve the
            // persistent definitions again before attaching them to the new scene.
            definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ContentPath + "/IslandPilotQuality.asset");
            var game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            if (game == null) throw new InvalidOperationException("Pilot scene has no game controller");
            foreach (var old in scene.GetRootGameObjects().Where(o => o.name == "Island pilot environment").ToArray()) UnityEngine.Object.DestroyImmediate(old);
            var environment = new GameObject("Island pilot environment");
            var stage = environment.AddComponent<IslandStagePresentation>();
            stage.Configure(game, definition); game.SetIslandStage(stage);
            environment.AddComponent<IslandPilotEnvironment>().Configure(pipeline, game);
            ConfigureLighting(environment, scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            RunChecks();
            Debug.Log("ISLAND_PILOT_READY " + ScenePath + " — press Play, then ИГРАТЬ. No player build created.");
        }

        private static void BackupPilotInputs(string chartPath)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtSource/RadioArchipelago/IslandPilot/BeforePhoneSpacing", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            foreach (string asset in new[] { chartPath, DefinitionPath, ScenePath })
                if (File.Exists(asset))
                {
                    Directory.CreateDirectory(folder);
                    File.Copy(asset, Path.Combine(folder, Path.GetFileName(asset)));
                    if (File.Exists(asset + ".meta")) File.Copy(asset + ".meta", Path.Combine(folder, Path.GetFileName(asset) + ".meta"));
                }
        }

        private static Material SurfaceMaterial(string source)
        {
            string category = source.Replace(" (Instance)", "");
            string path = ContentPath + "/" + category + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("RadioArchipelago/Island Surface");
                if (shader == null) throw new InvalidOperationException("Island surface shader did not compile");
                mat = new Material(shader) { name = category }; AssetDatabase.CreateAsset(mat, path);
            }
            string key = source.ToLowerInvariant();
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_DetailMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ImportPath + "/rock_albedo.png"));
            mat.SetFloat("_DetailStrength", key.Contains("rock") ? .65f : key.Contains("terrain") ? .32f : key.Contains("wood") ? .08f : 0);
            mat.SetFloat("_DetailScale", .35f);
            mat.SetFloat("_Road", 0); // Paving relief is actual stone geometry.
            mat.SetFloat("_Cull", key.Contains("foliage") ? 0 : 2);
            mat.SetFloat("_Smoothness", key.Contains("metal") ? .58f : key.Contains("water") ? .8f : .22f);
            mat.SetFloat("_Metallic", key.Contains("metal") ? .65f : 0);
            mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
        }

        private static void CreateReadableEffectMeshes(IslandStageDefinition definition)
        {
            string folder = ContentPath + "/ReadableEffects";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(ContentPath, "ReadableEffects");
            var library = AssetDatabase.LoadAssetAtPath<OriginalAssets>("Assets/Resources/OriginalAssets.asset");
            if (library == null) throw new InvalidOperationException("Native tile library is missing");
            var originals = library.prefabs.Where(p => p.asset != null &&
                (p.name.StartsWith("Jump", StringComparison.Ordinal) || p.name.StartsWith("Hold", StringComparison.Ordinal) ||
                 p.name.StartsWith("Follow", StringComparison.Ordinal) || p.name.StartsWith("MidShot", StringComparison.Ordinal) || p.name.StartsWith("EndPlatform", StringComparison.Ordinal)))
                .SelectMany(p => p.asset.GetComponentsInChildren<MeshFilter>(true)).Select(f => f.sharedMesh)
                .Where(m => m != null && !m.isReadable).Distinct().ToArray();
            var copies = new List<Mesh>(originals.Length);
            foreach (var original in originals)
            {
                string sourcePath = AssetDatabase.GetAssetPath(original);
                if (Path.GetExtension(sourcePath) != ".asset") throw new InvalidOperationException("Readable FX derivative needs a native mesh asset: " + sourcePath);
                string destination = folder + "/" + AssetDatabase.AssetPathToGUID(sourcePath) + ".asset";
                if (!File.Exists(destination) && !AssetDatabase.CopyAsset(sourcePath, destination))
                    throw new IOException("Cannot copy native effect mesh " + sourcePath);
                var copy = AssetDatabase.LoadAssetAtPath<Mesh>(destination);
                var serialized = new SerializedObject(copy); var readable = serialized.FindProperty("m_IsReadable");
                if (readable == null) throw new InvalidOperationException("Native mesh has no readability property: " + sourcePath);
                if (!readable.boolValue)
                {
                    readable.boolValue = true; serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(copy); AssetDatabase.SaveAssetIfDirty(copy);
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    copy = AssetDatabase.LoadAssetAtPath<Mesh>(destination);
                }
                if (copy == null || !copy.isReadable || copy.vertexCount != original.vertexCount || copy.vertices.Length != copy.vertexCount)
                    throw new InvalidDataException("Readable effect copy is invalid: " + sourcePath);
                copies.Add(copy);
            }
            definition.FxSourceMeshes = originals; definition.FxReadableMeshes = copies.ToArray();
            Debug.Log("ISLAND_READABLE_FX copies=" + copies.Count + "; native source assets preserved");
        }

        public static void PrepareReadableEffects()
        {
            RequireCleanEditor();
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            if (definition == null) throw new InvalidOperationException("Create the island definition first");
            CreateReadableEffectMeshes(definition); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
        }

        private static UniversalRenderPipelineAsset CreateQuality()
        {
            string path = ContentPath + "/IslandPilotQuality.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null)
            {
                var original = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (original == null) throw new InvalidOperationException("URP is required for the island pilot");
                pipeline = UnityEngine.Object.Instantiate(original); pipeline.name = "Island pilot quality";
                AssetDatabase.CreateAsset(pipeline, path);
            }
            var pipelineData = new SerializedObject(pipeline);
            var renderers = pipelineData.FindProperty("m_RendererDataList");
            var sourceRenderer = renderers.GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
            string rendererPath = ContentPath + "/IslandPilotRenderer.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath) == null &&
                !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceRenderer), rendererPath))
                throw new IOException("Cannot create the pilot-owned renderer");
            var pilotRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            foreach (var feature in pilotRenderer.rendererFeatures)
                if (feature != null && feature.GetType().Name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0)
                { feature.SetActive(false); EditorUtility.SetDirty(feature); }
            renderers.arraySize = 1; renderers.GetArrayElementAtIndex(0).objectReferenceValue = pilotRenderer;
            pipelineData.FindProperty("m_DefaultRendererIndex").intValue = 0;
            pipelineData.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pilotRenderer);
            pipeline.renderScale = 1; pipeline.msaaSampleCount = 4;
            var quality = new SerializedObject(pipeline);
            quality.FindProperty("m_SoftShadowsSupported").boolValue = true;
            quality.ApplyModifiedPropertiesWithoutUndo();
            pipeline.mainLightShadowmapResolution = 2048; pipeline.shadowDistance = 80;
            pipeline.shadowCascadeCount = 4; pipeline.cascade4Split = new Vector3(.1f, .28f, .58f);
            EditorUtility.SetDirty(pipeline); return pipeline;
        }

        private static void ConfigureLighting(GameObject owner, Scene scene)
        {
            foreach (var light in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Light>(true))) light.enabled = false;
            foreach (var volume in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Volume>(true))) volume.enabled = false;
            var sunNode = new GameObject("Late morning sun"); sunNode.transform.SetParent(owner.transform, false);
            sunNode.transform.rotation = Quaternion.Euler(48, -35, 0);
            var sun = sunNode.AddComponent<Light>(); sun.type = LightType.Directional;
            sun.color = new Color(1, .94f, .82f); sun.intensity = 2.3f; sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .85f; sun.shadowBias = .035f; sun.shadowNormalBias = .22f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f, .7f, .85f);
            RenderSettings.ambientEquatorColor = new Color(.35f, .44f, .48f);
            RenderSettings.ambientGroundColor = new Color(.15f, .19f, .15f);
            RenderSettings.reflectionIntensity = .7f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.72f, .83f, .88f); RenderSettings.fogStartDistance = 80; RenderSettings.fogEndDistance = 240;
            string skyPath = ContentPath + "/IslandSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetColor("_SkyTint", new Color(.58f, .69f, .82f)); sky.SetFloat("_Exposure", 1.1f); sky.SetFloat("_AtmosphereThickness", 1);
            RenderSettings.skybox = sky; EditorUtility.SetDirty(sky);
            var profile = LoadOrCreate<VolumeProfile>(ContentPath + "/IslandAtmosphere.asset");
            var tone = profile.TryGet<Tonemapping>(out var oldTone) ? oldTone : profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var color = profile.TryGet<ColorAdjustments>(out var oldColor) ? oldColor : profile.Add<ColorAdjustments>();
            color.postExposure.Override(.2f); color.contrast.Override(5); color.saturation.Override(3);
            var bloom = profile.TryGet<Bloom>(out var oldBloom) ? oldBloom : profile.Add<Bloom>(); bloom.intensity.Override(0); bloom.active = false;
            var volumeNode = new GameObject("Island atmosphere"); volumeNode.transform.SetParent(owner.transform, false);
            var activeVolume = volumeNode.AddComponent<Volume>(); activeVolume.isGlobal = true; activeVolume.priority = 20; activeVolume.sharedProfile = profile;
            owner.GetComponent<IslandPilotEnvironment>().ConfigureAtmosphere(sun, activeVolume);
            var scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
            var serialized = new SerializedObject(scope); var array = serialized.FindProperty("volumes"); array.arraySize = 1; array.GetArrayElementAtIndex(0).objectReferenceValue = activeVolume; serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        [MenuItem("Radio Archipelago/Island pilot/Checks")]
        public static void RunChecks()
        {
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            if (definition == null || definition.Prefab == null || definition.Chart == null || definition.RouteJson == null)
                throw new InvalidOperationException("Pilot assets or bindings are missing");
            if (definition.HoldBodyShader == null || definition.HoldBodyShader.name != "RadioArchipelago/Native Hold Ivory")
                throw new InvalidOperationException("The pilot Hold shader must have an explicit serialized Player dependency.");
            if (definition.HoldTracerShader == null || definition.HoldTracerShader.renderQueue < 3000)
                throw new InvalidOperationException("The pilot Hold tracer must explicitly reference its transparent Player shader.");
            IslandChartAuthoring.Verify(JsonUtility.FromJson<MapTimeline>(File.ReadAllText(OriginalMap)), JsonUtility.FromJson<MapTimeline>(definition.Chart.text));
            IslandRouteChecks.RunChecks();
            IslandSteeringChecks.RunChecks();
            IslandCameraChecks.RunChecks();
            IslandCameraStabilityChecks.RunChecks();
            IslandProjectionChecks.RunChecks();
            IslandOcclusionChecks.RunChecks();
            RadioArchipelago.Editor.IslandHoldFxProbe.RunChecks();
            IslandSpacingChecks.RunChecks();
            var pilotRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ContentPath + "/IslandPilotRenderer.asset");
            if (pilotRenderer == null || pilotRenderer.rendererFeatures.Any(f => f != null && f.isActive && f.GetType().Name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("Pilot ambient occlusion must be disabled.");
            var meshes = definition.Prefab.GetComponentsInChildren<MeshFilter>(true);
            if (meshes.Length == 0 || meshes.Any(m => m.sharedMesh == null)) throw new InvalidOperationException("Island mesh reference missing");
            var all = definition.Prefab.GetComponentsInChildren<Renderer>(true);
            if (all.Any(r => r.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name != "RadioArchipelago/Island Surface")))
                throw new InvalidOperationException("Island material reference missing");
            long triangles = meshes.Sum(m => (long)m.sharedMesh.GetIndexCount(0) / 3);
            File.WriteAllText(Path.Combine(ReviewPath, "asset-checks.json"), "{\"passed\":true,\"renderers\":" + all.Length + ",\"triangles\":" + triangles + ",\"chartTimingPreserved\":true,\"buildCreated\":false}");
            Debug.Log("ISLAND_PILOT_ASSETS_PASS renderers=" + all.Length + " triangles=" + triangles);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        private static void AttachModel(GameObject model, Transform parent, string name)
        {
            var basis = new GameObject(name).transform; basis.SetParent(parent, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model); instance.transform.SetParent(basis, false);
            var anchors = instance.GetComponentsInChildren<Transform>(true);
            var north = anchors.First(t => t.name == "RA_North10"); var origin = anchors.First(t => t.name == "RA_Origin");
            float yaw = Vector3.SignedAngle(north.position - origin.position, Vector3.back, Vector3.up);
            instance.transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * instance.transform.localRotation;
            if (anchors.First(t => t.name == "RA_X10").position.x < 0) basis.localScale = new Vector3(-1, 1, 1);
            CheckAnchor(instance, "RA_Origin", Vector3.zero); CheckAnchor(instance, "RA_X10", new Vector3(10, 0, 0)); CheckAnchor(instance, "RA_North10", new Vector3(0, 0, -10));
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => SurfaceMaterial(m != null ? m.name : renderer.name)).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void CheckAnchor(GameObject model, string name, Vector3 expected)
        {
            var anchor = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (anchor == null || Vector3.Distance(anchor.position, expected) > .002f)
                throw new InvalidDataException("FBX coordinate check failed for " + name + ": actual " + (anchor != null ? anchor.position.ToString("F4") : "missing") + ", expected " + expected);
        }

        private static void RequireCleanEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring the pilot");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before creating the pilot");
        }
    }
}
