using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RadioArchipelago.Editor
{
    /// <summary>Independent authoring of the approved fourth autumn island.</summary>
    public static class AmberWindsIslandJobs
    {
        public const string ScenePath = "Assets/RadioArchipelago/Scenes/AmberWindsPilot.unity";
        public const string ContentPath = "Assets/RadioArchipelago/Content/Islands/AmberWinds";
        public const string ImportPath = "Assets/RadioArchipelago/Imported/Islands/AmberWinds";
        public const string DefinitionPath = ContentPath + "/AmberWinds.asset";
        public const string ReviewFolder = "Library/RadioArchipelago/AmberWinds";
        public const string TerrainReportPath = ReviewFolder + "/terrain-clearance.json";
        private const string ChartPath = ContentPath + "/RadioTrioAmber.timeline.json";
        private const string HeightPath = ContentPath + "/RadioTrioAmberHoldGrades.json";
        private const int PreviewLayer = 29;
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class Report
        {
            public bool passed, approvedMusic, fullSizeTiles, flowingWater, earlierIslandsPreserved, coralPreservationGuard, moonPreservationGuard;
            public string scene, definition, song, sourceAudio;
            public int renderers, materials, movingTiles, holds, clouds;
            public float cloudMinimumGap, cloudMinimumClearance, cloudVolumeRatio;
            public long triangles;
            public float routeLength, minimumHeight, maximumHeight;
        }

        [Serializable] private sealed class CloudDocument { public Vector2[] ranges; }
        [Serializable] private sealed class Preserved { public string Path, Hash; }

        public static IslandStageDefinition LoadDefinition() => AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);

        [MenuItem("Radio Archipelago/Amber Winds/Create fourth island")]
        public static void Prepare()
        {
            IslandReliefJobs.RequireEditMode();
            RadioTrioChartAuthoring.LoadSource();
            if (!File.Exists(RadioTrioChartAuthoring.BaseChartPath)) throw new InvalidOperationException("Publish the reviewed recording and generated base chart first.");
            RequireSourceExports();
            var quality = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            try
            {
                CreateSceneryAssets();
                if (!File.Exists(DefinitionPath) && !AssetDatabase.CopyAsset(MoonResonanceIslandJobs.DefinitionPath, DefinitionPath))
                    throw new IOException("Could not create a separate fourth-island definition.");
                var definition = LoadDefinition();
                definition.SongId = RadioTrioChartAuthoring.SongId;
                ConfigureWorldDefinition(definition);
                RebuildChart(definition);
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                foreach (var material in AssetDatabase.FindAssets("t:Material", new[] { ContentPath }).Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid))))
                    AssetDatabase.SaveAssetIfDirty(material);
                if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(MoonResonanceIslandJobs.ScenePath, ScenePath))
                    throw new IOException("Could not create the fourth island's independent scene.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                // Opening a different scene may unload an asset not referenced by that scene yet.
                definition = LoadDefinition();
                if (definition == null) throw new InvalidOperationException("Reload the saved fourth-island definition after changing scenes.");
                var game = Object.FindAnyObjectByType<OriginalPlayController>();
                var stage = Object.FindAnyObjectByType<IslandStagePresentation>();
                if (game == null || stage == null) throw new InvalidOperationException("The copied gameplay entry points are missing.");
                stage.Configure(game, definition); game.SetIslandStage(stage); ConfigureLight(scene);
                EditorUtility.SetDirty(stage); EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
                RadioIslandMenuJobs.Prepare();
                RunChecks(); CaptureViews();
                Debug.Log("AMBER_WINDS_READY: separate autumn island and reviewed Radio Trio song; no Player build.");
            }
            finally { RestoreQuality(quality); }
        }

        [Serializable] private sealed class SceneryMetrics
        {
            public int renderers,materials,clouds;
            public long triangles;
            public float routeLength,cloudMinimumGap,cloudMinimumClearance,cloudVolumeRatio;
        }

        [Serializable] private sealed class SceneryReport
        {
            public bool passed, noSongBinding, noGameplayChart, earlierAssetsPreserved;
            public string scope = "Scenery-only native preview using the actual source route/raw heights, imported triangles, source-authored cloud windows and rigid windmill nodes. No published song, gameplay chart, saved definition, menu entry or gameplay scene is required or authored.";
            public string sourceModel, sourceModelDependencyHash, prefab;
            public SceneryMetrics world;
        }

        private static void RequireSourceExports()
        {
            foreach (string name in new[] { "amber_winds_island.fbx", "amber_winds_route.json", "play_elevation.json", "ground_elevation.json", "RooftopMotion.json", "cloud_steps.json" })
                if (!File.Exists(ImportPath + "/" + name)) throw new FileNotFoundException("The separate frozen Amber Winds export is not ready.", name);
        }

        private static void CreateSceneryAssets()
        {
            Directory.CreateDirectory(ContentPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Amber Winds / autumn radio mills and cloud islands");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                IslandReliefJobs.Attach(AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath + "/amber_winds_island.fbx"), root.transform, "Amber Winds basis", MaterialFor);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (transform.name.StartsWith("RA_Rooftop_", StringComparison.Ordinal))
                        GameObjectUtility.SetStaticEditorFlags(transform.gameObject, GameObjectUtility.GetStaticEditorFlags(transform.gameObject) & ~StaticEditorFlags.BatchingStatic);
                AmberWaterfallSprayAuthoring.Attach(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, ContentPath + "/AmberWinds.prefab");
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            Sky();
            SaveOwnMaterials();
        }

        private static void SaveOwnMaterials()
        {
            foreach (var material in AssetDatabase.FindAssets("t:Material", new[] { ContentPath })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid))))
                AssetDatabase.SaveAssetIfDirty(material);
        }

        private static void ConfigureWorldDefinition(IslandStageDefinition definition)
        {
            definition.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContentPath + "/AmberWinds.prefab");
            definition.RouteJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/amber_winds_route.json");
            definition.RooftopMotionJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/RooftopMotion.json");
            // Ten metres of the authored straight lead-in keep the complete native finale on the straight return.
            definition.StartDistance = 10; definition.DistanceScale = 1.20f; definition.LateralScale = 1.8f;
            definition.UniformRoundTiles = definition.UnifiedHoldTiles = definition.UseCloseCamera = true;
            definition.LivingNatureEnabled = definition.SparseCloudSteps = true;
            definition.CloudStepRanges = CloudRanges(); definition.CloudBankScaleMultiplier = 1.6f;
            definition.EnableGroundFinish = true; definition.FinishExitYaw = 0;
            definition.FinishTheme = IslandFinishTheme.AmberAutumn;
            definition.TileElevation = 1; definition.GlowEmission = 0;
            definition.CameraLateralFollow = .5f; definition.CameraHeight = 8;
            definition.CameraBackDistance = 9.5f; definition.CameraFocusHeight = 3.6f;
            definition.CameraLookAhead = 3.8f; definition.CameraHeadingLead = 3.2f; definition.CameraFieldOfView = 82;
            definition.JumpHeightScale = 1.65f;
        }

        internal static IslandStageDefinition CreateSceneryDefinition()
        {
            RequireSourceExports();
            var definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
            definition.name = "Amber Winds source-scenery preview only";
            definition.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                ConfigureWorldDefinition(definition);
                definition.SongId = string.Empty;
                definition.Chart = null;
                definition.EnableGroundFinish = false;
                definition.ElevationJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/play_elevation.json");
                if (definition.Prefab == null || definition.RouteJson == null || definition.ElevationJson == null || definition.RooftopMotionJson == null)
                    throw new InvalidOperationException("Import the separate frozen scenery, route and raw heights before creating an art preview.");
                return definition;
            }
            catch { Object.DestroyImmediate(definition); throw; }
        }

        [MenuItem("Radio Archipelago/Amber Winds/Prepare scenery only")]
        public static void PrepareScenery()
        {
            IslandReliefJobs.RequireEditMode(); RequireSourceExports();
            var quality = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            try
            {
                CreateSceneryAssets();
                CaptureScenery();
                Debug.Log("AMBER_SCENERY_READY: independent prefab/materials and transient source-route preview; no song, catalog, gameplay chart, definition or scene published.");
            }
            finally { RestoreQuality(quality); }
        }

        [MenuItem("Radio Archipelago/Amber Winds/Capture scenery only")]
        public static void CaptureScenery()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = CreateSceneryDefinition();
            try
            {
                var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                    IslandElevationProfile.FromJson(definition.ElevationJson.text));
                var report = new SceneryReport { noSongBinding = definition.SongId == string.Empty,
                    noGameplayChart = definition.Chart == null, sourceModel = ImportPath + "/amber_winds_island.fbx",
                    sourceModelDependencyHash = AssetDatabase.GetAssetDependencyHash(ImportPath + "/amber_winds_island.fbx").ToString(),
                    prefab = AssetDatabase.GetAssetPath(definition.Prefab), world = new SceneryMetrics { routeLength = route.Length } };
                foreach (var material in definition.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
                    if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader))
                        throw new InvalidOperationException("Amber scenery has an invalid material or shader.");
                var renderers = definition.Prefab.GetComponentsInChildren<MeshRenderer>(true);
                report.world.renderers = renderers.Length;
                report.world.materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().Count();
                foreach (var filter in definition.Prefab.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        report.world.triangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
                AmberWindsTerrainChecks.RunSceneryChecks(definition);
                CheckRooftops(definition);
                var cloudReport = new Report(); CheckClouds(definition, route, cloudReport);
                report.world.clouds = cloudReport.clouds; report.world.cloudMinimumGap = cloudReport.cloudMinimumGap;
                report.world.cloudMinimumClearance = cloudReport.cloudMinimumClearance; report.world.cloudVolumeRatio = cloudReport.cloudVolumeRatio;
                CaptureDefinitionViews(definition, ReviewFolder + "/SceneryOnly");
                report.earlierAssetsPreserved = VerifyEarlierIslands(true);
                if (!report.noSongBinding || !report.noGameplayChart || !report.earlierAssetsPreserved)
                    throw new InvalidOperationException("Scenery preview must remain independent of song publication and preserve existing assets.");
                report.passed = true; Directory.CreateDirectory(ReviewFolder);
                File.WriteAllText(ReviewFolder + "/scenery-only-checks.json", JsonUtility.ToJson(report, true));
                Debug.Log("AMBER_SCENERY_CHECKS PASS: actual imported geography, source heights, separated volumetric clouds and rigid windmills; no gameplay chart.");
            }
            finally { Object.DestroyImmediate(definition); }
        }

        private static Vector2[] CloudRanges()
        {
            var document = JsonUtility.FromJson<CloudDocument>(File.ReadAllText(ImportPath + "/cloud_steps.json"));
            if (document?.ranges == null || document.ranges.Length == 0) throw new InvalidDataException("The real cloud/islet air windows are missing.");
            foreach (var range in document.ranges)
                if (float.IsNaN(range.x) || float.IsNaN(range.y) || float.IsInfinity(range.x) || float.IsInfinity(range.y) || range.x < 0 || range.y <= range.x)
                    throw new InvalidDataException("Invalid source-authored cloud interval.");
            return document.ranges;
        }

        private static IslandAbyssChartAuthoring.Result Build(IslandStageDefinition definition)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(RadioTrioChartAuthoring.BaseChartPath);
            if (source == null) throw new FileNotFoundException("The approved Radio Trio generated chart is unavailable.");
            var map = JsonUtility.FromJson<MapTimeline>(source.text);
            if (map.id != RadioTrioChartAuthoring.SongId || map.audio == null || map.audio.rawSha256 != RadioTrioChartAuthoring.SourceSha256)
                throw new InvalidDataException("Amber Winds requires the chart of the approved Radio Trio recording.");
            string chart = JsonUtility.ToJson(IslandChartAuthoring.Create(source.text, true), true);
            return IslandAbyssChartAuthoring.Build(chart, File.ReadAllText(ImportPath + "/play_elevation.json"),
                definition.RouteJson.text, definition.StartDistance, definition.DistanceScale, definition.LateralScale,
                definition.Prefab, definition.TileElevation, definition.TileThickness, definition.UniformRoundTiles,
                minimumStationSeparation: .005f, repairFullSupportLanes: true);
        }

        private static void RebuildChart(IslandStageDefinition definition)
        {
            var result = Build(definition);
            File.WriteAllText(ChartPath, result.ChartJson); File.WriteAllText(HeightPath, result.ElevationJson);
            AssetDatabase.ImportAsset(ChartPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(HeightPath, ImportAssetOptions.ForceSynchronousImport);
            definition.Chart = AssetDatabase.LoadAssetAtPath<TextAsset>(ChartPath);
            definition.ElevationJson = AssetDatabase.LoadAssetAtPath<TextAsset>(HeightPath);
            Directory.CreateDirectory(ReviewFolder);
            File.WriteAllText(ReviewFolder + "/chart-authoring.json", JsonUtility.ToJson(result.Report, true));
        }

        private static Material MaterialFor(string sourceName)
        {
            string name = Regex.Replace(sourceName.Replace(" (Instance)", ""), @"\.\d{3}$", "");
            bool water = name.Contains("Water") || name.Contains("Stream") || name.Contains("Foam"), grass = name.Contains("Grass");
            bool continuousWater = name.Contains("Waterfall") || name.Contains("Stream");
            bool crystal = name.Contains("Crystal"), mist = name.Contains("Mist");
            string shaderName = continuousWater ? "RadioArchipelago/Amber Flow Water" : water ? "RadioArchipelago/Coral Water" : mist ? "RadioArchipelago/Moon Mist" :
                crystal ? "RadioArchipelago/Moon Crystal" : grass ? "RadioArchipelago/Island Grass" : "RadioArchipelago/Island Surface";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing island shader: " + shaderName);
            string path = ContentPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.name = name; material.enableInstancing = true;
            material.SetColor("_BaseColor", mist ? new Color(1,1,1,.65f) : Color.white);
            if (continuousWater)
            {
                if (name.Contains("Waterfall"))
                {
                    IslandWaterfallRolloutJobs.ConfigureAmberMaterial(material);
                    return material;
                }
                material.SetColor("_BaseColor", new Color(.12f,.48f,.50f,1));
                material.SetColor("_FoamColor", new Color(.86f,.97f,.94f,1));
                material.SetFloat("_CurrentSpeed", 4.8f);
                material.SetFloat("_FoamStrength", .92f);
                material.SetTexture("_RippleNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RadioArchipelago/Art/WaterRippleNormal.png"));
                material.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(material);
                return material;
            }
            if (mist)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(material);
                return material;
            }
            if (water)
            {
                bool authoredFlow = name.Contains("Stream") || name.Contains("Waterfall");
                material.SetFloat("_UseFlowUV", authoredFlow ? 1 : 0);
                material.SetFloat("_CurrentSpeed", authoredFlow ? 4.2f : 2.1f);
                material.SetFloat("_FoamLayer", name.Contains("Foam") ? 1 : 0);
                if (!name.Contains("Foam"))
                    material.SetTexture("_RippleNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RadioArchipelago/Art/WaterRippleNormal.png"));
                if (name.Contains("Waterfall"))
                {
                    material.SetVector("_FallFadeRange", new Vector4(-90,-80,0,0));
                    material.SetFloat("_ZWrite", 0);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                material.SetFloat("_Fall", name.Contains("Waterfall") || name.Contains("Foam") ? 1 : 0);
                material.SetFloat("_FlowSpeed", authoredFlow ? .65f : .55f);
                material.SetFloat("_FoamStrength", name.Contains("Foam") ? .72f : .38f);
            }
            else
            {
                material.SetFloat("_Cull", name.Contains("Foliage") || name.Contains("Fabric") || grass ? 0 : 2);
                material.SetFloat("_Smoothness", name.Contains("Metal") ? .65f : crystal ? .78f : name.Contains("Glass") ? .72f : .28f);
                if (crystal) material.SetFloat("_EmissionStrength", .22f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", name.Contains("Metal") ? .65f : 0);
                if (material.HasProperty("_Road")) material.SetFloat("_Road", 0);
                if (material.HasProperty("_ProtectGameplaySurface"))
                    material.SetFloat("_ProtectGameplaySurface", name.Contains("Terrain") || name.Contains("Cliffs") ? 1 : 0);
                if (material.HasProperty("_DetailMap"))
                {
                    // Reuse only the immutable neutral stone texture, never the
                    // earlier islands's material or its palette/settings.
                    var stoneReference = AssetDatabase.LoadAssetAtPath<Material>(IslandAbyssJobs.ContentPath + "/RA_Emerald_Cliffs.mat");
                    var texture = stoneReference != null ? stoneReference.GetTexture("_DetailMap") : null;
                    if (texture != null) material.SetTexture("_DetailMap", texture);
                    material.SetFloat("_DetailScale", name.Contains("Terrain") ? .7f : .45f);
                    material.SetFloat("_DetailStrength", name.Contains("Cliffs") ? .52f : name.Contains("Rocks") ? .40f : name.Contains("Terrain") ? .17f : .05f);
                }
            }
            EditorUtility.SetDirty(material); return material;
        }

        private static Material Sky()
        {
            string path = ContentPath + "/AmberWindsSky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("RadioArchipelago/Archipelago Sky")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_ZenithColor", new Color(.30f,.61f,.76f));
            material.SetColor("_HorizonColor", new Color(.94f,.82f,.65f));
            material.SetColor("_AbyssColor", new Color(.72f,.80f,.83f));
            material.SetColor("_CloudColor", new Color(1,.96f,.85f));
            material.SetFloat("_CloudStrength", .16f); EditorUtility.SetDirty(material); return material;
        }

        private static void ConfigureLight(Scene scene)
        {
            RenderSettings.skybox = Sky(); RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.64f,.74f,.81f);
            RenderSettings.ambientEquatorColor = new Color(.61f,.54f,.43f);
            RenderSettings.ambientGroundColor = new Color(.31f,.34f,.31f);
            RenderSettings.fogColor = new Color(.80f,.84f,.82f);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional)
                    {
                        light.color = new Color(1,.90f,.73f); light.intensity = 1.65f;
                        light.transform.rotation = Quaternion.Euler(38, -34, 0); light.shadows = LightShadows.Soft;
                        EditorUtility.SetDirty(light); RenderSettings.sun = light;
                    }
        }

        [MenuItem("Radio Archipelago/Amber Winds/Check fourth island")]
        public static void RunChecks()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = LoadDefinition();
            if (definition == null || definition.Prefab == null || definition.RouteJson == null || definition.ElevationJson == null || definition.Chart == null)
                throw new InvalidOperationException("The fourth island is not completely configured.");
            if (AssetDatabase.GetAssetPath(definition.Chart) != ChartPath || AssetDatabase.GetAssetPath(definition.Prefab) != ContentPath + "/AmberWinds.prefab")
                throw new InvalidOperationException("Amber Winds must own its chart and scenery assets.");
            if (!definition.SparseCloudSteps || Mathf.Abs(definition.CloudBankScaleMultiplier - 1.6f) > .0001f || definition.CloudStepRanges == null || !definition.CloudStepRanges.SequenceEqual(CloudRanges()))
                throw new InvalidOperationException("Amber Winds requires source-authored air gaps between its small islands.");
            var report = new Report { scene = ScenePath, definition = DefinitionPath, song = definition.SongId,
                approvedMusic = definition.SongId == RadioTrioChartAuthoring.SongId, fullSizeTiles = definition.UniformRoundTiles,
                sourceAudio = RadioTrioChartAuthoring.SourceSha256, minimumHeight = float.MaxValue };
            RadioTrioChartAuthoring.LoadSource();
            var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
            AudioChartGeneratorValidation.ValidateMap(map); IslandOpeningChecks.VerifyOpening(map);
            var result = Build(definition);
            if (!result.Report.musicalTimingPreserved || result.ChartJson != definition.Chart.text || result.ElevationJson != definition.ElevationJson.text)
                throw new InvalidOperationException("The new island's derived chart/height profile is stale.");
            report.movingTiles = result.Report.movingRounds;
            report.holds = map.segments.Count(s => s.kind == "Hold");
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            report.routeLength = route.Length;
            for (float distance = 0; distance < route.Length; distance += .2f)
            { float y = route.Evaluate(distance).Position.y; report.minimumHeight = Mathf.Min(report.minimumHeight, y); report.maximumHeight = Mathf.Max(report.maximumHeight, y); }
            var renderers = definition.Prefab.GetComponentsInChildren<MeshRenderer>(true);
            var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
            report.renderers = renderers.Length; report.materials = materials.Length;
            foreach (var filter in definition.Prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++) report.triangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
            report.flowingWater = materials.Any(material => material != null && material.shader.name == "RadioArchipelago/Amber Flow Water");
            if (!report.approvedMusic || !report.fullSizeTiles || !report.flowingWater || report.movingTiles == 0 || report.holds == 0 || report.maximumHeight - report.minimumHeight < 6)
                throw new InvalidOperationException("Amber Winds lacks its approved music, full-size route, movement, water or elevation journey.");
            foreach (var material in materials)
                if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("An island material is missing or has shader errors.");
            AmberWindsRouteChecks.RunChecks();
            AmberWindsTerrainChecks.RunChecks();
            CheckRooftops(definition);
            CheckClouds(definition, route, report);
            report.coralPreservationGuard = CoralOasisIslandJobs.VerifyFirstIsland();
            report.moonPreservationGuard = MoonResonanceIslandJobs.VerifyEarlierIslands();
            report.earlierIslandsPreserved = VerifyEarlierIslands() && report.coralPreservationGuard && report.moonPreservationGuard;
            if (!report.earlierIslandsPreserved) throw new InvalidOperationException("A protected earlier-island asset changed.");
            report.passed = true; Directory.CreateDirectory(ReviewFolder);
            File.WriteAllText(ReviewFolder + "/island-checks.json", JsonUtility.ToJson(report, true));
            Debug.Log("AMBER_WINDS_CHECKS PASS: independent scene, terrain, route, native chart and materials.");
        }

        private static void CheckClouds(IslandStageDefinition definition, IslandRoute route, Report report)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var owner = new GameObject("Isolated Amber cloud geometry");
            SceneManager.MoveGameObjectToScene(owner, preview);
            IslandLivingNature nature = null;
            try
            {
                nature = owner.AddComponent<IslandLivingNature>(); nature.Configure(definition, route);
                var entries = (Array)typeof(IslandLivingNature).GetField("bank", Hidden).GetValue(nature);
                report.clouds = entries.Length;
                if (entries.Length != definition.CloudStepRanges.Length)
                    throw new InvalidOperationException("Amber requires one substantial detached cloud per real air gap.");
                // Reuse the independent transformed-triangle proof, including full drift,
                // minimum physical separation, actual height clearance and visible volume variation.
                var proofType = typeof(SparseCloudStepChecks).GetNestedType("Report", BindingFlags.NonPublic);
                var proof = Activator.CreateInstance(proofType, true);
                try
                {
                    typeof(SparseCloudStepChecks).GetMethod("CheckSparse", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { nature, definition, route, definition.CloudStepRanges, proof });
                }
                catch (TargetInvocationException error)
                { throw new InvalidOperationException("Amber cloud geometry: " + error.InnerException?.Message, error.InnerException ?? error); }
                report.cloudMinimumGap = (float)proofType.GetField("minimumGap").GetValue(proof);
                report.cloudMinimumClearance = (float)proofType.GetField("minimumClearance").GetValue(proof);
                report.cloudVolumeRatio = (float)proofType.GetField("sizeRatio").GetValue(proof);
            }
            finally
            {
                nature?.Dispose(); Object.DestroyImmediate(owner);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [Serializable] private sealed class RotorPreviewReport
        {
            public bool passed, zeroDeltaFreezes, disposeRestores;
            public string[] nodes;
            public float[] angleAfterOneSecond;
        }

        private static void CheckRooftops(IslandStageDefinition definition)
        {
            if (definition.RooftopMotionJson == null) throw new InvalidOperationException("The windmill motion manifest is missing.");
            var preview = EditorSceneManager.NewPreviewScene();
            var owner = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, preview);
            var motion = new IslandRooftopMotion();
            try
            {
                motion.Configure(owner.transform, owner.transform, definition.RooftopMotionJson);
                var document = JsonUtility.FromJson<IslandRooftopMotion.Document>(definition.RooftopMotionJson.text);
                var nodes = document.mechanisms.Select(entry => owner.GetComponentsInChildren<Transform>(true)
                    .Single(node => node.name == entry.nodeName)).ToArray();
                if (motion.Count < 3) throw new InvalidOperationException("Three prominent rotating windmills are required.");
                var rest = nodes.Select(node => node.localRotation).ToArray();
                for (int tick = 0; tick < 10; tick++) motion.Tick(.1f);
                var moved = nodes.Select(node => node.localRotation).ToArray();
                var report = new RotorPreviewReport { nodes = nodes.Select(node => node.name).ToArray(),
                    angleAfterOneSecond = nodes.Select((node,index) => Quaternion.Angle(rest[index],node.localRotation)).ToArray() };
                if (report.angleAfterOneSecond.Any(angle => angle < 20))
                    throw new InvalidOperationException("Authored windmill sails did not visibly rotate during their one-second native helper tick.");
                motion.Tick(0);
                report.zeroDeltaFreezes = nodes.Select((node,index) => Quaternion.Angle(node.localRotation,moved[index]) < .001f).All(value => value);
                motion.Dispose();
                report.disposeRestores = nodes.Select((node,index) => Quaternion.Angle(node.localRotation,rest[index]) < .001f).All(value => value);
                if (!report.zeroDeltaFreezes || !report.disposeRestores)
                    throw new InvalidOperationException("Windmill zero-delta pause or cleanup did not preserve its authored rest pose.");
                report.passed = true; Directory.CreateDirectory(ReviewFolder);
                File.WriteAllText(ReviewFolder + "/rooftop-motion-preview.json", JsonUtility.ToJson(report,true));
            }
            finally { motion.Dispose(); Object.DestroyImmediate(owner); EditorSceneManager.ClosePreviewScene(preview); }
        }

        public static bool VerifyEarlierIslands(bool allowUnpublishedSong = false)
        {
            const string before = "../Docs/Design/RadioArchipelago/Island04AmberWinds/Implementation20261102/Before/";
            var records = RecoveredCatalog.FromJsonArray<Preserved>(File.ReadAllText(before + "preserved-existing.json"));
            string project = Path.GetFullPath(".").Replace('\\','/');
            string[] permittedIntegration = {
                "/Assets/Resources/RadioArchipelago/IslandMenuCatalog.asset",
                "/Assets/RadioArchipelago/Runtime/UI/RadioIslandMenuPolicy.cs",
                "/Assets/RadioArchipelago/Runtime/UI/RadioIslandMenuCatalog.cs",
                "/Assets/RadioArchipelago/Runtime/UI/RadioMenuPresenter.cs",
                "/Assets/RadioArchipelago/Editor/RadioIslandMenuJobs.cs",
                "/Assets/RadioArchipelago/Editor/RadioIslandMenuLiveChecks.cs",
                "/Assets/RadioArchipelago/Editor/IslandPilotPlayReview.cs"
            };
            foreach (var record in records)
            {
                string path = Path.GetFullPath(record.Path).Replace('\\','/');
                if (path == project + "/Assets/Resources/CustomData/songs.json")
                {
                    if (!(allowUnpublishedSong && File.ReadAllBytes(before + "songs.json").SequenceEqual(File.ReadAllBytes(path))) &&
                        !RadioTrioChartAuthoring.PreservesCatalogSnapshot(File.ReadAllText(before + "songs.json"))) return false;
                    continue;
                }
                if (permittedIntegration.Any(suffix => path == project + suffix)) continue;
                using (var stream = File.OpenRead(record.Path)) using (var sha = System.Security.Cryptography.SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != IslandPreservationRevisions.ExpectedHash(record.Path, record.Hash)) return false;
            }
            return records.Length >= 405;
        }

        [MenuItem("Radio Archipelago/Amber Winds/Open fourth island")]
        public static void Open()
        {
            IslandReliefJobs.RequireEditMode();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("Radio Archipelago/Amber Winds/Capture environment")]
        public static void CaptureViews()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = LoadDefinition();
            if (definition == null) throw new InvalidOperationException("Create the fourth gameplay definition before capturing its conditioned route.");
            CaptureDefinitionViews(definition, ReviewFolder + "/Scenery");
        }

        private static void CaptureDefinitionViews(IslandStageDefinition definition, string folder)
        {
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            var qualityFile = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            var previousPipeline = QualitySettings.renderPipeline;
            var previousTarget = RenderTexture.active;
            var previousClear = Shader.GetGlobalVector("_RA_ClearViewCamera");
            bool previousFog = RenderSettings.fog;
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture texture = null; Texture2D pixels = null;
            IslandLivingNature nature = null; IslandRooftopMotion motion = null;
            var captureMilliseconds = new System.Collections.Generic.List<double>();
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(IslandPilotJobs.ContentPath + "/IslandPilotQuality.asset");
                RenderSettings.fog = false;
                var root = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, scene);
                Bounds sceneryBounds = root.GetComponentsInChildren<Renderer>().Aggregate(new Bounds(Vector3.zero, Vector3.zero),
                    (a, renderer) => { a.Encapsulate(renderer.bounds); return a; });
                nature = root.AddComponent<IslandLivingNature>(); nature.Configure(definition, route);
                nature.UpdatePresentation(route.Length * .66f, 0);
                motion = new IslandRooftopMotion(); motion.Configure(root.transform, root.transform, definition.RooftopMotionJson);
                for (int tick = 0; tick < 10; tick++) motion.Tick(.1f);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = PreviewLayer;
                var cameraNode = new GameObject("Amber Winds review camera"); SceneManager.MoveGameObjectToScene(cameraNode, scene);
                var camera = cameraNode.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.cullingMask = 1 << PreviewLayer; camera.fieldOfView = 58; camera.farClipPlane = 600; camera.nearClipPlane = .08f; camera.aspect = 1.5f;
                camera.clearFlags = CameraClearFlags.Skybox; cameraNode.AddComponent<Skybox>().material = Sky();
                var data = cameraNode.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = true; data.allowXRRendering = false;
                var lightNode = new GameObject("Amber Winds review sun"); SceneManager.MoveGameObjectToScene(lightNode, scene);
                var sun = lightNode.AddComponent<Light>(); sun.type = LightType.Directional; sun.cullingMask = 1 << PreviewLayer;
                sun.color = new Color(1,.90f,.73f); sun.intensity = 1.65f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(38,-34,0);
                Shader.SetGlobalVector("_RA_ClearViewCamera", Vector4.zero);
                texture = new RenderTexture(1536, 1024, 24); texture.Create(); pixels = new Texture2D(1536, 1024, TextureFormat.RGBA32, false);
                Directory.CreateDirectory(folder);
                for (int i = 0; i <= 6; i++)
                {
                    Vector3 eye, target;
                    if (i == 0)
                    {
                        target = sceneryBounds.center + Vector3.up * 10;
                        float extent = Mathf.Max(sceneryBounds.extents.x, sceneryBounds.extents.z);
                        eye = target + new Vector3(extent * .94f, extent * .86f, extent * 1.02f);
                    }
                    else
                    {
                        float station = route.Length * (i - .5f) / 6;
                        nature.UpdatePresentation(station, 0);
                        var frame = route.Evaluate(station);
                        eye = frame.Position - frame.Tangent * 18 + frame.Right * 10 + Vector3.up * 10;
                        target = route.Evaluate(route.Length * (i - .5f) / 6 + 15).Position + Vector3.up;
                    }
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
                    captureMilliseconds.Add(CaptureSceneryFrame(camera, texture, pixels,
                        folder + "/" + (i == 0 ? "overview" : "route-" + i) + ".png"));
                }
                // Art views from the actual fixed gameplay rig and portrait FOV,
                // without introducing an actor, clock, timeline or old song.
                if (RenderTexture.active == texture) RenderTexture.active = null;
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
                texture = new RenderTexture(720, 1280, 24); texture.Create();
                pixels = new Texture2D(720, 1280, TextureFormat.RGBA32, false);
                camera.aspect = 9f / 16; camera.fieldOfView = definition.CameraFieldOfView;
                var rig = new IslandCloseCameraRig();
                foreach (float station in new[] { 155f,170f,172f,185f,200f,210f,244f,272f,464f,506f,542f,580f,675f })
                {
                    nature.UpdatePresentation(station, 0);
                    var pose = rig.Step(route, station, 0, definition.CameraBackDistance, definition.CameraHeight,
                        definition.CameraLookAhead, definition.CameraFocusHeight, definition.CameraHeadingLead, definition.CameraSmoothing, 0, true);
                    camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                    captureMilliseconds.Add(CaptureSceneryFrame(camera, texture, pixels,
                        folder + "/portrait-route-" + station.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                }
                File.WriteAllText(folder + "/preview-timings.json", JsonUtility.ToJson(new PreviewTimingReport {
                    frames = captureMilliseconds.Count, minimumMilliseconds = captureMilliseconds.Min(),
                    meanMilliseconds = captureMilliseconds.Average(), maximumMilliseconds = captureMilliseconds.Max() }, true));
            }
            finally
            {
                motion?.Dispose(); nature?.Dispose();
                RenderTexture.active = previousTarget; Shader.SetGlobalVector("_RA_ClearViewCamera", previousClear);
                RenderSettings.fog = previousFog;
                QualitySettings.renderPipeline = previousPipeline;
                if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene); RestoreQuality(qualityFile);
            }
        }

        [Serializable] private sealed class PreviewTimingReport
        {
            public string scope = "Synchronous Editor offscreen scenery captures, mixed1536x1024/720x1280 resolutions. Includes render submission and GPU readback; excludes PNG encoding/file IO. No actor, chart, gameplay VFX or native frame pacing; this is an art-preview cost observation, not a runtime FPS or phone benchmark.";
            public int frames;
            public double minimumMilliseconds, meanMilliseconds, maximumMilliseconds;
        }

        private static double CaptureSceneryFrame(Camera camera, RenderTexture texture, Texture2D pixels, string path)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(false, false);
            elapsed.Stop();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            return elapsed.Elapsed.TotalMilliseconds;
        }

        private static void RestoreQuality(byte[] bytes)
        { const string path = "ProjectSettings/QualitySettings.asset"; if (!File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes); }
    }
}
