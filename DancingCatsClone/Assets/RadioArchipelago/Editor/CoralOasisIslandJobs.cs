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
    /// <summary>Owns only the second island's assets and scene. The first island is read-only.</summary>
    public static class CoralOasisIslandJobs
    {
        public const string ScenePath = "Assets/RadioArchipelago/Scenes/CoralOasisPilot.unity";
        public const string ContentPath = "Assets/RadioArchipelago/Content/Islands/CoralOasis";
        public const string ImportPath = "Assets/RadioArchipelago/Imported/Islands/CoralOasis";
        public const string DefinitionPath = ContentPath + "/CoralOasis.asset";
        public const string ReviewFolder = "Library/RadioArchipelago/CoralOasis";
        public const string TerrainReportPath = ReviewFolder + "/terrain-clearance.json";
        private const string ChartPath = ContentPath + "/TikoTaOasis.timeline.json";
        private const string HeightPath = ContentPath + "/TikoTaOasisHoldGrades.json";
        private const int PreviewLayer = 29;
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class Report
        {
            public bool passed, approvedMusic, fullSizeTiles, flowingWater, firstIslandPreserved;
            public string scene, definition, song, sourceAudio;
            public int renderers, materials, movingTiles, holds;
            public long triangles;
            public float routeLength, minimumHeight, maximumHeight;
        }

        public static IslandStageDefinition LoadDefinition() => AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);

        [MenuItem("Radio Archipelago/Coral Oasis/Create second island")]
        public static void Prepare()
        {
            IslandReliefJobs.RequireEditMode();
            foreach (string name in new[] { "coral_oasis_island.fbx", "coral_oasis_route.json", "play_elevation.json" })
                if (!File.Exists(ImportPath + "/" + name)) throw new FileNotFoundException("The separate Coral Oasis export is not ready.", name);
            var quality = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            try
            {
                Directory.CreateDirectory(ContentPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (Shader.Find("RadioArchipelago/Coral Water") == null) throw new InvalidOperationException("The new water shader has not compiled.");
                var preview = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("Coral Oasis / sculpted floating sandstone");
                SceneManager.MoveGameObjectToScene(root, preview);
                try
                {
                    IslandReliefJobs.Attach(AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath + "/coral_oasis_island.fbx"),
                        root.transform, "Coral Oasis basis", MaterialFor);
                    PrefabUtility.SaveAsPrefabAsset(root, ContentPath + "/CoralOasis.prefab");
                }
                finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
                if (!File.Exists(DefinitionPath) && !AssetDatabase.CopyAsset(IslandAbyssJobs.DefinitionPath, DefinitionPath))
                    throw new IOException("Could not create a separate second-island definition.");
                var definition = LoadDefinition();
                definition.SongId = TikoTaChartAuthoring.SongId;
                definition.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContentPath + "/CoralOasis.prefab");
                definition.RouteJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/coral_oasis_route.json");
                definition.StartDistance = 0; definition.DistanceScale = 1.20f; definition.LateralScale = 1.8f;
                definition.UniformRoundTiles = definition.UnifiedHoldTiles = definition.UseCloseCamera = true;
                definition.LivingNatureEnabled = true;
                definition.EnableGroundFinish = true; definition.FinishExitYaw = 0;
                definition.FinishTheme = IslandFinishTheme.CoralShore;
                definition.TileElevation = 1; definition.GlowEmission = 0;
                definition.CameraLateralFollow = .5f; definition.CameraHeight = 8;
                definition.CameraBackDistance = 9.5f; definition.CameraFocusHeight = 3.6f;
                definition.CameraLookAhead = 3.8f; definition.CameraHeadingLead = 3.2f; definition.CameraFieldOfView = 82;
                definition.JumpHeightScale = 1.65f;
                RebuildChart(definition);
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(IslandAbyssJobs.ScenePath, ScenePath))
                    throw new IOException("Could not create the second island's independent scene.");
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
                Debug.Log("CORAL_OASIS_READY: independent island scene, approved Tiko-Ta recording, no Player build.");
            }
            finally { RestoreQuality(quality); }
        }

        private static IslandAbyssChartAuthoring.Result Build(IslandStageDefinition definition)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(TikoTaChartAuthoring.BaseChartPath);
            if (source == null) throw new FileNotFoundException("The approved Tiko-Ta generated chart is unavailable.");
            var map = JsonUtility.FromJson<MapTimeline>(source.text);
            if (map.id != TikoTaChartAuthoring.SongId || map.audio == null || map.audio.rawSha256 != TikoTaChartAuthoring.SourceSha256)
                throw new InvalidDataException("Coral Oasis requires the chart of the approved Tiko-Ta recording.");
            string chart = JsonUtility.ToJson(IslandChartAuthoring.Create(source.text, true), true);
            return IslandAbyssChartAuthoring.Build(chart, File.ReadAllText(ImportPath + "/play_elevation.json"),
                definition.RouteJson.text, definition.StartDistance, definition.DistanceScale, definition.LateralScale,
                definition.Prefab, definition.TileElevation, definition.TileThickness, definition.UniformRoundTiles);
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

        [MenuItem("Radio Archipelago/Coral Oasis/Install approved Tiko-Ta")]
        public static void InstallTikoTa()
        {
            IslandReliefJobs.RequireEditMode();
            TikoTaChartAuthoring.LoadSource();
            var definition = LoadDefinition();
            if (definition == null) throw new InvalidOperationException("Create the second island before changing its music.");
            // A small forward-spacing reserve keeps complete ceramic bodies
            // clear on bends without shrinking tiles or changing music times.
            definition.DistanceScale = 1.20f;
            RebuildChart(definition);
            definition.SongId = TikoTaChartAuthoring.SongId;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            RunChecks();
            Debug.Log("CORAL_OASIS_TIKO_TA_READY: new music and independently generated tiles; scenery and first island preserved.");
        }

        private static Material MaterialFor(string sourceName)
        {
            string name = Regex.Replace(sourceName.Replace(" (Instance)", ""), @"\.\d{3}$", "");
            bool water = name.Contains("Water") || name.Contains("Foam"), grass = name.Contains("Grass");
            string shaderName = water ? "RadioArchipelago/Coral Water" : grass ? "RadioArchipelago/Island Grass" : "RadioArchipelago/Island Surface";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing island shader: " + shaderName);
            string path = ContentPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.name = name; material.enableInstancing = true;
            material.SetColor("_BaseColor", Color.white);
            if (name.Contains("Waterfall"))
            {
                IslandWaterfallRolloutJobs.ConfigureMaterial(material,false);
                return material;
            }
            if (water)
            {
                material.SetFloat("_FoamLayer", name.Contains("Foam") ? 1 : 0);
                if (!name.Contains("Foam")) material.SetTexture("_RippleNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RadioArchipelago/Art/WaterRippleNormal.png"));
                if (name.Contains("Waterfall"))
                { material.SetFloat("_ZWrite", 0); material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10); material.renderQueue = 3000; }
                material.SetFloat("_Fall", name.Contains("Waterfall") || name.Contains("Foam") ? 1 : 0);
                material.SetFloat("_FlowSpeed", name.Contains("Waterfall") ? 1.3f : .55f);
                material.SetFloat("_FoamStrength", name.Contains("Foam") ? .72f : .38f);
            }
            else
            {
                material.SetFloat("_Cull", name.Contains("Foliage") || grass ? 0 : 2);
                material.SetFloat("_Smoothness", name.Contains("Metal") ? .65f : name.Contains("Glass") ? .72f : .28f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", name.Contains("Metal") ? .65f : 0);
                if (material.HasProperty("_Road")) material.SetFloat("_Road", 0);
                if (material.HasProperty("_ProtectGameplaySurface"))
                    material.SetFloat("_ProtectGameplaySurface", name.Contains("Terrain") || name.Contains("Cliffs") ? 1 : 0);
                if (material.HasProperty("_DetailMap"))
                {
                    // Reuse only the immutable neutral stone texture, never the
                    // first island's material or its palette/settings.
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
            string path = ContentPath + "/CoralOasisSky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("RadioArchipelago/Archipelago Sky")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_ZenithColor", new Color(.32f, .64f, .86f));
            material.SetColor("_HorizonColor", new Color(.72f, .86f, .93f));
            material.SetColor("_AbyssColor", new Color(.72f, .79f, .88f));
            material.SetColor("_CloudColor", new Color(1, .94f, .84f));
            material.SetFloat("_CloudStrength", .16f); EditorUtility.SetDirty(material); return material;
        }

        private static void ConfigureLight(Scene scene)
        {
            RenderSettings.skybox = Sky(); RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.60f, .73f, .86f);
            RenderSettings.ambientEquatorColor = new Color(.60f, .52f, .46f);
            RenderSettings.ambientGroundColor = new Color(.36f, .28f, .25f);
            RenderSettings.fogColor = new Color(.79f, .85f, .89f);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional)
                    {
                        light.color = new Color(1, .87f, .71f); light.intensity = 1.45f;
                        light.transform.rotation = Quaternion.Euler(44, -34, 0); light.shadows = LightShadows.Soft;
                        EditorUtility.SetDirty(light); RenderSettings.sun = light;
                    }
        }

        [MenuItem("Radio Archipelago/Coral Oasis/Check second island")]
        public static void RunChecks()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = LoadDefinition();
            if (definition == null || definition.Prefab == null || definition.RouteJson == null || definition.ElevationJson == null || definition.Chart == null)
                throw new InvalidOperationException("The second island is not completely configured.");
            if (AssetDatabase.GetAssetPath(definition.Chart) != ChartPath || AssetDatabase.GetAssetPath(definition.Prefab) != ContentPath + "/CoralOasis.prefab")
                throw new InvalidOperationException("Coral Oasis must own its chart and scenery assets.");
            var report = new Report { scene = ScenePath, definition = DefinitionPath, song = definition.SongId,
                approvedMusic = definition.SongId == TikoTaChartAuthoring.SongId, fullSizeTiles = definition.UniformRoundTiles,
                sourceAudio = TikoTaChartAuthoring.SourceSha256, minimumHeight = float.MaxValue };
            TikoTaChartAuthoring.LoadSource();
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
                throw new InvalidOperationException("Coral Oasis lacks its approved music, full-size route, movement, water or elevation journey.");
            foreach (var material in materials)
                if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("An island material is missing or has shader errors.");
            CoralOasisRouteChecks.RunChecks();
            CoralOasisTerrainChecks.RunChecks();
            report.firstIslandPreserved = VerifyFirstIsland();
            if (!report.firstIslandPreserved) throw new InvalidOperationException("A protected first-island asset changed.");
            report.passed = true; Directory.CreateDirectory(ReviewFolder);
            File.WriteAllText(ReviewFolder + "/island-checks.json", JsonUtility.ToJson(report, true));
            Debug.Log("CORAL_OASIS_CHECKS PASS: independent scene, terrain, route, native chart and materials.");
        }

        public static bool VerifyFirstIsland()
        {
            var first = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            if (first == null || Mathf.Abs(first.FinishExitYaw - IslandGroundFinishPath.MeadowExitYaw) > .00001f) return false;
            string path = Path.GetFullPath("../Docs/Design/RadioArchipelago/TikoTa20261101/Before/preserved-first-island-hashes.json");
            if (!File.Exists(path)) throw new FileNotFoundException("The first-island preservation receipt is required.", path);
            var records = RecoveredCatalog.FromJsonArray<Preserved>(File.ReadAllText(path));
            foreach (var record in records)
            {
                if (Path.GetFileName(record.Path) == "songs.json")
                {
                    // Preserve the original records while allowing the two
                    // explicitly known later island songs to coexist.
                    string beforePath = Path.Combine(Path.GetDirectoryName(path), "songs.json");
                    var before = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(beforePath));
                    var after = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(record.Path));
                    int thirdIslandSongs = after.Count(song => song.id == NoriNoChartAuthoring.SongId);
                    int fourthIslandSongs = after.Count(song => song.id == RadioTrioChartAuthoring.SongId);
                    if (thirdIslandSongs > 1 || fourthIslandSongs > 1 ||
                        after.Length != before.Length + 1 + thirdIslandSongs + fourthIslandSongs ||
                        after.Count(song => song.id == TikoTaChartAuthoring.SongId) != 1)
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
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") !=
                        IslandPreservationRevisions.ExpectedHash(record.Path, record.Hash)) return false;
            }
            return records.Length > 20;
        }
        [Serializable] private sealed class Preserved { public string Path, Hash; }

        [MenuItem("Radio Archipelago/Coral Oasis/Open second island")]
        public static void Open()
        {
            IslandReliefJobs.RequireEditMode();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("Radio Archipelago/Coral Oasis/Capture environment")]
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
                var cameraNode = new GameObject("Coral Oasis review camera"); SceneManager.MoveGameObjectToScene(cameraNode, scene);
                var camera = cameraNode.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.cullingMask = 1 << PreviewLayer; camera.fieldOfView = 58; camera.farClipPlane = 600; camera.nearClipPlane = .08f; camera.aspect = 1.5f;
                camera.clearFlags = CameraClearFlags.Skybox; cameraNode.AddComponent<Skybox>().material = Sky();
                var data = cameraNode.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = true; data.allowXRRendering = false;
                var lightNode = new GameObject("Coral Oasis review sun"); SceneManager.MoveGameObjectToScene(lightNode, scene);
                var sun = lightNode.AddComponent<Light>(); sun.type = LightType.Directional; sun.cullingMask = 1 << PreviewLayer;
                sun.color = new Color(1, .87f, .71f); sun.intensity = 1.45f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(44, -34, 0);
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
