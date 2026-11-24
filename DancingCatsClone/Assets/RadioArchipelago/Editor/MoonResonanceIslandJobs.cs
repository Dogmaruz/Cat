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
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RadioArchipelago.Editor
{
    /// <summary>Owns the third island; the explicit Coral cloud action changes only its opt-in cloud presentation.</summary>
    public static class MoonResonanceIslandJobs
    {
        public const string ScenePath = "Assets/RadioArchipelago/Scenes/MoonResonancePilot.unity";
        public const string ContentPath = "Assets/RadioArchipelago/Content/Islands/MoonResonance";
        public const string ImportPath = "Assets/RadioArchipelago/Imported/Islands/MoonResonance";
        public const string DefinitionPath = ContentPath + "/MoonResonance.asset";
        public const string ReviewFolder = "Library/RadioArchipelago/MoonResonance";
        public const string TerrainReportPath = ReviewFolder + "/terrain-clearance.json";
        private const string ChartPath = ContentPath + "/NoriNoMoon.timeline.json";
        private const string HeightPath = ContentPath + "/NoriNoMoonHoldGrades.json";
        private const int PreviewLayer = 29;
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class Report
        {
            public bool passed, approvedMusic, fullSizeTiles, flowingWater, earlierIslandsPreserved;
            public string scene, definition, song, sourceAudio;
            public int renderers, materials, movingTiles, holds;
            public long triangles;
            public float routeLength, minimumHeight, maximumHeight;
        }

        public static IslandStageDefinition LoadDefinition() => AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);

        [MenuItem("Radio Archipelago/Moon Resonance/Create third island")]
        public static void Prepare()
        {
            IslandReliefJobs.RequireEditMode();
            foreach (string name in new[] { "moon_resonance_island.fbx", "moon_resonance_route.json", "play_elevation.json" })
                if (!File.Exists(ImportPath + "/" + name)) throw new FileNotFoundException("The separate Moon Resonance export is not ready.", name);
            var quality = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            try
            {
                Directory.CreateDirectory(ContentPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (Shader.Find("RadioArchipelago/Coral Water") == null) throw new InvalidOperationException("The new water shader has not compiled.");
                var preview = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("Moon Resonance / sculpted floating silver crags");
                SceneManager.MoveGameObjectToScene(root, preview);
                try
                {
                    IslandReliefJobs.Attach(AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath + "/moon_resonance_island.fbx"),
                        root.transform, "Moon Resonance basis", MaterialFor);
                    PrefabUtility.SaveAsPrefabAsset(root, ContentPath + "/MoonResonance.prefab");
                }
                finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
                if (!File.Exists(DefinitionPath) && !AssetDatabase.CopyAsset(CoralOasisIslandJobs.DefinitionPath, DefinitionPath))
                    throw new IOException("Could not create a separate third-island definition.");
                var definition = LoadDefinition();
                definition.SongId = NoriNoChartAuthoring.SongId;
                definition.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContentPath + "/MoonResonance.prefab");
                definition.RouteJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/moon_resonance_route.json");
                definition.StartDistance = 0; definition.DistanceScale = 1.20f; definition.LateralScale = 2.4f;
                definition.UniformRoundTiles = definition.UnifiedHoldTiles = definition.UseCloseCamera = true;
                definition.LivingNatureEnabled = true;
                definition.SparseCloudSteps = true;
                // Actual air gaps from the source mesh audit; stone visits remain uncovered.
                definition.CloudStepRanges = new[] { new Vector2(445.75f, 458.25f), new Vector2(471.75f, 479.25f),
                    new Vector2(499.25f, 509.50f), new Vector2(523.50f, 546.75f) };
                definition.EnableGroundFinish = true; definition.FinishExitYaw = 0;
                definition.FinishTheme = IslandFinishTheme.MoonCrystalGarden;
                definition.TileElevation = 1; definition.GlowEmission = 0;
                definition.CameraLateralFollow = .5f; definition.CameraHeight = 8;
                definition.CameraBackDistance = 9.5f; definition.CameraFocusHeight = 3.6f;
                definition.CameraLookAhead = 3.8f; definition.CameraHeadingLead = 3.2f; definition.CameraFieldOfView = 82;
                definition.JumpHeightScale = 1.65f;
                RebuildChart(definition);
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(CoralOasisIslandJobs.ScenePath, ScenePath))
                    throw new IOException("Could not create the third island's independent scene.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                definition = LoadDefinition();
                var game = Object.FindAnyObjectByType<OriginalPlayController>();
                var stage = Object.FindAnyObjectByType<IslandStagePresentation>();
                if (game == null || stage == null) throw new InvalidOperationException("The copied gameplay entry points are missing.");
                stage.Configure(game, definition); game.SetIslandStage(stage);
                ConfigureLight(scene);
                EditorUtility.SetDirty(stage); EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                RunChecks(); CaptureViews();
                if (scene.isDirty) EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("MOON_RESONANCE_READY: independent island scene, approved Nori-No recording, no Player build.");
            }
            finally { RestoreQuality(quality); }
        }

        private static IslandAbyssChartAuthoring.Result Build(IslandStageDefinition definition)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(NoriNoChartAuthoring.BaseChartPath);
            if (source == null) throw new FileNotFoundException("The approved Nori-No generated chart is unavailable.");
            var map = JsonUtility.FromJson<MapTimeline>(source.text);
            if (map.id != NoriNoChartAuthoring.SongId || map.audio == null || map.audio.rawSha256 != NoriNoChartAuthoring.SourceSha256)
                throw new InvalidDataException("Moon Resonance requires the chart of the approved Nori-No recording.");
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

        public static void DiagnoseHeldProfile()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = LoadDefinition();
            var chart = IslandChartAuthoring.Create(File.ReadAllText(NoriNoChartAuthoring.BaseChartPath), true);
            var source = JsonUtility.FromJson<IslandElevationProfile.Document>(File.ReadAllText(ImportPath + "/play_elevation.json"));
            string routeJson = File.ReadAllText(ImportPath + "/moon_resonance_route.json");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var type = typeof(IslandAbyssChartAuthoring);
            var report = new IslandAbyssChartAuthoring.Report();
            var first = (IslandElevationProfile.Document)type.GetMethod("FlattenSpans", flags).Invoke(null,
                new object[] { chart, source, 0f, 1.20f, report, 0f });
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(routeJson).points,
                new IslandElevationProfile(first.length, first.closed, first.keys));
            type.GetMethod("PromoteMovingRounds", flags).Invoke(null,
                new object[] { chart, route, 0f, 1.20f, report, 1.8f, true });
            var floor = IslandTerrainClearanceAuthoring.BuildFloor(
                AssetDatabase.LoadAssetAtPath<GameObject>(ContentPath + "/MoonResonance.prefab"), routeJson, first, chart,
                0f, 1.20f, 1.8f, definition.TileElevation, definition.TileThickness);
            Directory.CreateDirectory(ReviewFolder);
            File.WriteAllText(ReviewFolder + "/diagnostic-first-elevation.json", JsonUtility.ToJson(first, true));
            File.WriteAllText(ReviewFolder + "/diagnostic-floor.json", JsonUtility.ToJson(floor, true));
            File.WriteAllText(ReviewFolder + "/diagnostic-chart.json", JsonUtility.ToJson(chart, true));
            Debug.Log("MOON_HELD_PROFILE_DIAGNOSTICS_READY: fresh actual authoring inputs; no scene or assets changed.");
        }

        [MenuItem("Radio Archipelago/Moon Resonance/Install approved Nori-No")]
        public static void InstallNoriNo()
        {
            IslandReliefJobs.RequireEditMode();
            NoriNoChartAuthoring.LoadSource();
            var definition = LoadDefinition();
            if (definition == null) throw new InvalidOperationException("Create the third island before changing its music.");
            // A small forward-spacing reserve keeps complete ceramic bodies
            // clear on bends without shrinking tiles or changing music times.
            definition.DistanceScale = 1.20f;
            RebuildChart(definition);
            definition.SongId = NoriNoChartAuthoring.SongId;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            RunChecks();
            Debug.Log("MOON_RESONANCE_NORI_NO_READY: new music and independently generated tiles; scenery and earlier islands preserved.");
        }

        public static void ApplyCoralSparseClouds()
        {
            IslandReliefJobs.RequireEditMode();
            if (!VerifyEarlierIslands()) throw new InvalidOperationException("Preservation baseline failed before the authorized cloud change.");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(CoralOasisIslandJobs.DefinitionPath);
            definition.SparseCloudSteps = true;
            definition.CloudStepRanges = Array.Empty<Vector2>();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            if (!VerifyEarlierIslands()) throw new InvalidOperationException("The Coral cloud change modified other protected settings.");
            Directory.CreateDirectory(ReviewFolder);
            File.WriteAllText(ReviewFolder + "/coral-cloud-change.txt",
                "PASS: user-authorized sparse cloud presentation; only SparseCloudSteps:1 and CloudStepRanges:[] added. Original Tiko-Ta chart, audio, route, scenery, scene and camera preserved.\n");
        }

        private static Material MaterialFor(string sourceName)
        {
            string name = Regex.Replace(sourceName.Replace(" (Instance)", ""), @"\.\d{3}$", "");
            bool water = name.Contains("Water") || name.Contains("Foam"), grass = name.Contains("Grass");
            bool crystal = name.Contains("Crystal"), mist = name.Contains("Mist");
            string shaderName = water ? "RadioArchipelago/Coral Water" : mist ? "RadioArchipelago/Moon Mist" :
                crystal ? "RadioArchipelago/Moon Crystal" : grass ? "RadioArchipelago/Island Grass" : "RadioArchipelago/Island Surface";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing island shader: " + shaderName);
            string path = ContentPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.name = name; material.enableInstancing = true;
            material.SetColor("_BaseColor", mist ? new Color(1,1,1,.65f) : Color.white);
            if (name.Contains("Waterfall"))
            {
                IslandWaterfallRolloutJobs.ConfigureMaterial(material,true);
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
                material.SetFloat("_FoamLayer", name.Contains("Foam") ? 1 : 0);
                if (!name.Contains("Foam"))
                    material.SetTexture("_RippleNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RadioArchipelago/Art/WaterRippleNormal.png"));
                if (name.Contains("Waterfall"))
                {
                    material.SetFloat("_ZWrite", 0);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                material.SetFloat("_Fall", name.Contains("Waterfall") || name.Contains("Foam") ? 1 : 0);
                material.SetFloat("_FlowSpeed", name.Contains("Waterfall") ? 1.3f : .55f);
                material.SetFloat("_FoamStrength", name.Contains("Foam") ? .72f : .38f);
            }
            else
            {
                material.SetFloat("_Cull", name.Contains("Foliage") || grass ? 0 : 2);
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
            string path = ContentPath + "/MoonResonanceSky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("RadioArchipelago/Moon Resonance Sky")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_ZenithColor", new Color(.36f, .39f, .65f));
            material.SetColor("_HorizonColor", new Color(.77f, .73f, .88f));
            material.SetColor("_AbyssColor", new Color(.66f, .64f, .80f));
            material.SetColor("_CloudColor", new Color(.98f, .90f, .92f));
            material.SetFloat("_CloudStrength", .16f); EditorUtility.SetDirty(material); return material;
        }

        private static void ConfigureLight(Scene scene)
        {
            RenderSettings.skybox = Sky(); RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .64f, .84f);
            RenderSettings.ambientEquatorColor = new Color(.60f, .55f, .67f);
            RenderSettings.ambientGroundColor = new Color(.32f, .29f, .40f);
            RenderSettings.fogColor = new Color(.75f, .73f, .85f);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional)
                    {
                        light.color = new Color(1, .88f, .84f); light.intensity = 1.35f;
                        light.transform.rotation = Quaternion.Euler(44, -34, 0); light.shadows = LightShadows.Soft;
                        EditorUtility.SetDirty(light); RenderSettings.sun = light;
                    }
        }

        [MenuItem("Radio Archipelago/Moon Resonance/Check third island")]
        public static void RunChecks()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = LoadDefinition();
            if (definition == null || definition.Prefab == null || definition.RouteJson == null || definition.ElevationJson == null || definition.Chart == null)
                throw new InvalidOperationException("The third island is not completely configured.");
            if (AssetDatabase.GetAssetPath(definition.Chart) != ChartPath || AssetDatabase.GetAssetPath(definition.Prefab) != ContentPath + "/MoonResonance.prefab")
                throw new InvalidOperationException("Moon Resonance must own its chart and scenery assets.");
            if (!definition.SparseCloudSteps || definition.CloudStepRanges == null || definition.CloudStepRanges.Length != 4)
                throw new InvalidOperationException("Moon Resonance requires detached cloud steps in the four audited gaps between islets.");
            var report = new Report { scene = ScenePath, definition = DefinitionPath, song = definition.SongId,
                approvedMusic = definition.SongId == NoriNoChartAuthoring.SongId, fullSizeTiles = definition.UniformRoundTiles,
                sourceAudio = NoriNoChartAuthoring.SourceSha256, minimumHeight = float.MaxValue };
            NoriNoChartAuthoring.LoadSource();
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
            report.flowingWater = materials.Any(material => material != null && material.shader.name == IslandWaterfallRolloutJobs.ShaderName);
            if (!report.approvedMusic || !report.fullSizeTiles || !report.flowingWater || report.movingTiles == 0 || report.holds == 0 || report.maximumHeight - report.minimumHeight < 6)
                throw new InvalidOperationException("Moon Resonance lacks its approved music, full-size route, movement, water or elevation journey.");
            foreach (var material in materials)
                if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("An island material is missing or has shader errors.");
            MoonResonanceRouteChecks.RunChecks();
            MoonResonanceTerrainChecks.RunChecks();
            report.earlierIslandsPreserved = VerifyEarlierIslands();
            if (!report.earlierIslandsPreserved) throw new InvalidOperationException("A protected earlier-island asset changed.");
            report.passed = true; Directory.CreateDirectory(ReviewFolder);
            File.WriteAllText(ReviewFolder + "/island-checks.json", JsonUtility.ToJson(report, true));
            Debug.Log("MOON_RESONANCE_CHECKS PASS: independent scene, terrain, route, native chart and materials.");
        }

        public static bool VerifyEarlierIslands()
        {
            var first = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            if (first == null || Mathf.Abs(first.FinishExitYaw - IslandGroundFinishPath.MeadowExitYaw) > .00001f) return false;
            string path = Path.GetFullPath("../Docs/Design/RadioArchipelago/Island03MoonResonance/Implementation20261101/Before/preserved-islands-hashes.json");
            if (!File.Exists(path)) throw new FileNotFoundException("The earlier-island preservation receipt is required.", path);
            var records = RecoveredCatalog.FromJsonArray<Preserved>(File.ReadAllText(path));
            foreach (var record in records)
            {
                if (Path.GetFileName(record.Path) == "songs.json")
                {
                    // Require Nori-No and allow the explicitly known fourth song.
                    // Every original record, including nested fields, stays exact.
                    string beforePath = Path.Combine(Path.GetDirectoryName(path), "songs.json");
                    var before = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(beforePath));
                    var after = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(record.Path));
                    int fourthIslandSongs = after.Count(song => song.id == RadioTrioChartAuthoring.SongId);
                    if (fourthIslandSongs > 1 || after.Length != before.Length + 1 + fourthIslandSongs ||
                        after.Count(song => song.id == NoriNoChartAuthoring.SongId) != 1)
                        return false;
                    foreach (var song in before)
                    {
                        var matches = after.Where(candidate => candidate.id == song.id).ToArray();
                        if (matches.Length != 1 || JsonUtility.ToJson(matches[0]) != JsonUtility.ToJson(song)) return false;
                    }
                    continue;
                }
                using (var stream = File.OpenRead(record.Path))
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != ExpectedPreservedHash(record) &&
                        !IsAuthorizedCoralCloudChange(record, Path.GetDirectoryName(path))) return false;
            }
            return records.Length > 20;
        }
        [Serializable] private sealed class Preserved { public string Path, Hash; }

        private static string ExpectedPreservedHash(Preserved record)
        {
            return IslandPreservationRevisions.ExpectedHash(record.Path, record.Hash);
        }

        private static bool IsAuthorizedCoralCloudChange(Preserved record, string baselineFolder)
        {
            if (!string.Equals(Path.GetFullPath(record.Path), Path.GetFullPath(CoralOasisIslandJobs.DefinitionPath), StringComparison.OrdinalIgnoreCase))
                return false;
            byte[] baseline = File.ReadAllBytes(Path.Combine(baselineFolder, "CoralOasis.asset"));
            using (var sha = System.Security.Cryptography.SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(baseline)).Replace("-", "") != record.Hash) return false;
            string after = File.ReadAllText(record.Path);
            const string flag = @"^  SparseCloudSteps: 1\r?\n", ranges = @"^  CloudStepRanges: \[\]\r?\n";
            if (Regex.Matches(after, flag, RegexOptions.Multiline).Count != 1 || Regex.Matches(after, ranges, RegexOptions.Multiline).Count != 1)
                return false;
            after = Regex.Replace(Regex.Replace(after, flag, "", RegexOptions.Multiline), ranges, "", RegexOptions.Multiline);
            return new System.Text.UTF8Encoding(false).GetBytes(after).SequenceEqual(baseline);
        }

        [MenuItem("Radio Archipelago/Moon Resonance/Open third island")]
        public static void Open()
        {
            IslandReliefJobs.RequireEditMode();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("Radio Archipelago/Moon Resonance/Capture environment")]
        public static void CaptureViews()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = LoadDefinition();
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            var qualityFile = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            var previousPipeline = QualitySettings.renderPipeline;
            var previousTarget = RenderTexture.active;
            var previousClear = Shader.GetGlobalVector("_RA_ClearViewCamera");
            bool previousFog = RenderSettings.fog;
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture texture = null; Texture2D pixels = null;
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(IslandPilotJobs.ContentPath + "/IslandPilotQuality.asset");
                RenderSettings.fog = false;
                var root = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, scene);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = PreviewLayer;
                var cameraNode = new GameObject("Moon Resonance review camera"); SceneManager.MoveGameObjectToScene(cameraNode, scene);
                var camera = cameraNode.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.cullingMask = 1 << PreviewLayer; camera.fieldOfView = 58; camera.farClipPlane = 600; camera.nearClipPlane = .08f; camera.aspect = 1.5f;
                camera.clearFlags = CameraClearFlags.Skybox; cameraNode.AddComponent<Skybox>().material = Sky();
                var data = cameraNode.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = true; data.allowXRRendering = false;
                var lightNode = new GameObject("Moon Resonance review sun"); SceneManager.MoveGameObjectToScene(lightNode, scene);
                var sun = lightNode.AddComponent<Light>(); sun.type = LightType.Directional; sun.cullingMask = 1 << PreviewLayer;
                sun.color = new Color(1, .88f, .84f); sun.intensity = 1.35f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(44, -34, 0);
                Shader.SetGlobalVector("_RA_ClearViewCamera", Vector4.zero);
                texture = new RenderTexture(1536, 1024, 24); texture.Create(); pixels = new Texture2D(1536, 1024, TextureFormat.RGBA32, false);
                string folder = ReviewFolder + "/Scenery"; Directory.CreateDirectory(folder);
                for (int i = 0; i <= 6; i++)
                {
                    Vector3 eye, target;
                    if (i == 0)
                    {
                        Bounds bounds = root.GetComponentsInChildren<Renderer>().Aggregate(new Bounds(Vector3.zero, Vector3.zero), (a, renderer) => { a.Encapsulate(renderer.bounds); return a; });
                        target = bounds.center + Vector3.up * 10;
                        float extent = Mathf.Max(bounds.extents.x, bounds.extents.z);
                        eye = target + new Vector3(extent * .94f, extent * .86f, extent * 1.02f);
                    }
                    else
                    {
                        var frame = route.Evaluate(route.Length * (i - .5f) / 6);
                        eye = frame.Position - frame.Tangent * 18 + frame.Right * 10 + Vector3.up * 10;
                        target = route.Evaluate(route.Length * (i - .5f) / 6 + 15).Position + Vector3.up;
                    }
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                    RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(false, false);
                    File.WriteAllBytes(folder + "/" + (i == 0 ? "overview" : "route-" + i) + ".png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previousTarget; Shader.SetGlobalVector("_RA_ClearViewCamera", previousClear);
                RenderSettings.fog = previousFog;
                QualitySettings.renderPipeline = previousPipeline;
                if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene); RestoreQuality(qualityFile);
            }
        }

        private static void RestoreQuality(byte[] bytes)
        { const string path = "ProjectSettings/QualitySettings.asset"; if (!File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes); }
    }
}
