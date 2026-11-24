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

namespace RadioArchipelago.Editor
{
    /// <summary>Floating scenery with a configured musical chart and terrain-safe flight profile.</summary>
    public static class IslandAbyssJobs
    {
        public const string ScenePath = "Assets/RadioArchipelago/Scenes/EmeraldAbyssPilot.unity";
        public const string ContentPath = "Assets/RadioArchipelago/Content/Islands/EmeraldAbyss";
        public const string ImportPath = "Assets/RadioArchipelago/Imported/Islands/EmeraldAbyss";
        public const string DefinitionPath = ContentPath + "/EmeraldAbyss.asset";
        public const float CourseLateralScale = 1.8f;
        private const string SurfaceShader = "RadioArchipelago/Island Surface";
        private const string WaterShader = "RadioArchipelago/Coral Water";
        private const string SkyShader = "RadioArchipelago/Archipelago Sky";
        private const string GrassShader = "RadioArchipelago/Island Grass";
        private const string TreeLeavesShader = "RadioArchipelago/Island Tree Leaves";
        private const int PreviewLayer = 29;

        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class Report
        {
            public bool passed, reliefChartPreserved, reliefFlightPreserved, flatPilotPreserved, noPaving;
            public string scene = ScenePath;
            public int renderers, materials, cliffMaterials, vineMaterials;
            public long triangles;
            public float routeLength, maxHeight;
            public bool closeCamera, unifiedHolds, livingNature;
            public bool groundFinish, signalCurrency;
            public int movingRounds, grassMaterials, treeLeafMaterials, treeBarkMaterials;
            public int treeLeafBatches;
            public bool treeLeafVertexData;
            public float holdChordError, maxProfileGrade, cameraDistance, jumpHeightScale, lateralScale, playableWidth, cameraLateralFollow;
        }

        [MenuItem("Radio Archipelago/Floating archipelago/Prepare scene")]
        public static void Create()
        {
            IslandReliefJobs.RequireEditMode();
            var qualitySettings = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            try { PrepareScene(); }
            finally { RestoreQualitySettingsFile(qualitySettings); }
        }

        private static void PrepareScene()
        {
            foreach (string file in new[] { "emerald_abyss_island.fbx", "emerald_abyss_route.json", "ground_elevation.json", "play_elevation.json" })
                if (!File.Exists(ImportPath + "/" + file)) throw new FileNotFoundException("Abyss export is not ready", file);
            Directory.CreateDirectory(ContentPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Emerald floating archipelago"); SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                IslandReliefJobs.Attach(AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath + "/emerald_abyss_island.fbx"),
                    root.transform, "Floating island basis", MaterialFor);
                PrefabUtility.SaveAsPrefabAsset(root, ContentPath + "/EmeraldAbyss.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            if (!File.Exists(DefinitionPath) && !AssetDatabase.CopyAsset(IslandReliefJobs.DefinitionPath, DefinitionPath))
                throw new IOException("Could not preserve a separate archipelago definition");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            definition.LateralScale = CourseLateralScale;
            definition.UniformRoundTiles = definition.SongId != AudioChartGenerator.AlwaysRememberId;
            definition.CameraLateralFollow = .5f;
            definition.EnableGroundFinish = true;
            definition.FinishCurrencyPrefab = SignalSparkAuthoring.EnsureAssets();
            definition.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContentPath + "/EmeraldAbyss.prefab");
            RebuildMusicChart(definition);
            definition.RouteJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/emerald_abyss_route.json");
            definition.UseCloseCamera = definition.UnifiedHoldTiles = definition.LivingNatureEnabled = true;
            definition.JumpHeightScale = 1.65f;
            definition.CameraHeight = 8; definition.CameraBackDistance = 9.5f;
            definition.CameraLookAhead = 3.8f; definition.CameraFieldOfView = 82;
            definition.CameraFocusHeight = 3.6f; definition.CameraHeadingLead = 3.2f;
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(IslandReliefJobs.ScenePath, ScenePath))
                throw new IOException("Could not preserve a separate archipelago scene");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Opening a Single scene unloads unreferenced assets from the previous scene.
            definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            var game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            var stage = UnityEngine.Object.FindFirstObjectByType<IslandStagePresentation>();
            if (game == null || stage == null) throw new InvalidOperationException("Copied pilot game/stage is missing");
            stage.Configure(game, definition); game.SetIslandStage(stage);
            ConfigureSky();
            EditorUtility.SetDirty(stage); EditorUtility.SetDirty(game);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            RunChecks();
            CaptureViews();
            Debug.Log("ISLAND_ABYSS_READY " + ScenePath + " — enter Play and press ИГРАТЬ.");
        }

        internal static void RebuildMusicChart(IslandStageDefinition definition)
        {
            string stem = definition.SongId == AudioChartGenerator.AlwaysRememberId ? "AlwaysRememberMeowAbyss" : definition.SongId + "Abyss";
            string chartPath = ContentPath + "/" + stem + ".timeline.json";
            string heightPath = ContentPath + "/" + (definition.SongId == AudioChartGenerator.AlwaysRememberId ? "AbyssHoldGrades" : definition.SongId + "HoldGrades") + ".json";
            var authored = IslandAbyssChartAuthoring.Build(SourceChartJson(definition), File.ReadAllText(ImportPath + "/play_elevation.json"),
                File.ReadAllText(ImportPath + "/emerald_abyss_route.json"), definition.StartDistance, definition.DistanceScale, definition.LateralScale,
                definition.Prefab, definition.TileElevation, definition.TileThickness, definition.UniformRoundTiles);
            File.WriteAllText(chartPath, authored.ChartJson);
            File.WriteAllText(heightPath, authored.ElevationJson);
            AssetDatabase.ImportAsset(chartPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(heightPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            definition.Chart = AssetDatabase.LoadAssetAtPath<TextAsset>(chartPath);
            definition.ElevationJson = AssetDatabase.LoadAssetAtPath<TextAsset>(heightPath);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
            File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "living-chart-checks.json"), JsonUtility.ToJson(authored.Report, true));
        }

        private static string SourceChartJson(IslandStageDefinition definition)
        {
            var relief = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandReliefJobs.DefinitionPath);
            if (definition.SongId == relief.SongId) return relief.Chart.text;
            string path = "Assets/Resources/CustomData/Maps/" + definition.SongId + ".timeline.json";
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (source == null) throw new FileNotFoundException("The configured island song needs its generated musical chart", path);
            return JsonUtility.ToJson(IslandChartAuthoring.Create(source.text, true), true);
        }

        private static Material MaterialFor(string sourceName)
        {
            string name = Regex.Replace(sourceName.Replace(" (Instance)", ""), @"\.\d{3}$", "");
            string path = ContentPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                string source = name.Contains("Cliffs") || name.Contains("BridgeDeck") ? "RA_Emerald_Rocks" :
                    name.Contains("TreeBark") ? "RA_Emerald_Wood" :
                    name.Contains("Vines") ? "RA_Emerald_Wood" : name;
                var original = AssetDatabase.LoadAssetAtPath<Material>(IslandReliefJobs.ContentPath + "/" + source + ".mat") ??
                    AssetDatabase.LoadAssetAtPath<Material>(IslandPilotJobs.ContentPath + "/" + source + ".mat");
                string specializedShader = name.Contains("TreeLeaves") ? TreeLeavesShader : name.Contains("Grass") ? GrassShader : null;
                if (specializedShader != null && Shader.Find(specializedShader) == null)
                    throw new InvalidOperationException("The island material shader has not compiled: " + specializedShader);
                material = specializedShader != null ? new Material(Shader.Find(specializedShader)) :
                    original != null ? new Material(original) : new Material(Shader.Find(SurfaceShader));
                if (original == null)
                {
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Cull", name.Contains("Foliage") || name.Contains("Grass") || name.Contains("TreeLeaves") ? 0 : 2);
                    material.SetFloat("_Smoothness", name.Contains("TreeLeaves") ? .18f : .25f);
                }
                material.name = name; AssetDatabase.CreateAsset(material, path);
            }
            // Never cut open the sealed terrain. Decorative foreground rocks/foliage can still clear the view.
            bool solid = name.Contains("Terrain") || name.Contains("Cliffs") || name.Contains("Bridge");
            material.SetFloat("_ProtectGameplaySurface", solid ? 1 : 0);
            if (name.Contains("Grass")) material.shader = Shader.Find(GrassShader);
            if (name.Contains("TreeLeaves")) material.shader = Shader.Find(TreeLeavesShader);
            if (name.Contains("Water"))
            {
                material.shader = Shader.Find(WaterShader);
                material.SetFloat("_Fall", 0);
                material.SetFloat("_FlowSpeed", .65f);
                material.SetFloat("_FoamStrength", .35f);
                material.SetFloat("_FoamLayer", 0);
                material.SetFloat("_VertexFoam", 1);
                material.SetTexture("_RippleNormal", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RadioArchipelago/Art/WaterRippleNormal.png"));
            }
            if (material.HasProperty("_Road")) material.SetFloat("_Road", 0);
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureSky()
        {
            var shader = Shader.Find(SkyShader);
            if (shader == null) throw new InvalidOperationException("The archipelago sky shader has not compiled");
            string path = ContentPath + "/ArchipelagoSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null) { sky = new Material(shader); AssetDatabase.CreateAsset(sky, path); }
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f, .72f, .88f);
            RenderSettings.ambientEquatorColor = new Color(.39f, .49f, .54f);
            RenderSettings.ambientGroundColor = new Color(.25f, .32f, .35f);
            RenderSettings.fogColor = new Color(.72f, .83f, .9f);
            EditorUtility.SetDirty(sky);
        }

        [MenuItem("Radio Archipelago/Floating archipelago/Checks")]
        public static void RunChecks()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            var relief = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandReliefJobs.DefinitionPath);
            var flat = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandPilotJobs.DefinitionPath);
            if (definition == null || definition.Prefab == null || definition.ElevationJson == null || definition.RouteJson == null)
                throw new InvalidOperationException("Archipelago bindings are missing");
            var report = new Report
            {
                reliefChartPreserved = definition.Chart != relief.Chart,
                reliefFlightPreserved = definition.ElevationJson != relief.ElevationJson &&
                    definition.RouteJson.text == relief.RouteJson.text,
                flatPilotPreserved = flat.ElevationJson == null && flat.Prefab != definition.Prefab && relief.Prefab != definition.Prefab
            };
            if (!report.reliefChartPreserved || !report.reliefFlightPreserved || !report.flatPilotPreserved)
                throw new InvalidOperationException("The scenery fork must preserve music, route, flight and original variants");
            var authored = IslandAbyssChartAuthoring.Build(SourceChartJson(definition), File.ReadAllText(ImportPath + "/play_elevation.json"),
                definition.RouteJson.text, definition.StartDistance, definition.DistanceScale, definition.LateralScale,
                definition.Prefab, definition.TileElevation, definition.TileThickness, definition.UniformRoundTiles);
            if (authored.ChartJson != definition.Chart.text || authored.ElevationJson != definition.ElevationJson.text || !authored.Report.musicalTimingPreserved)
                throw new InvalidOperationException("The derivative moving chart or straight Hold grades are stale");
            report.movingRounds = authored.Report.movingRounds; report.holdChordError = authored.Report.maxHoldChordError;
            report.maxProfileGrade = authored.Report.maxProfileGrade;
            report.closeCamera = definition.UseCloseCamera; report.unifiedHolds = definition.UnifiedHoldTiles;
            report.livingNature = definition.LivingNatureEnabled; report.jumpHeightScale = definition.JumpHeightScale;
            report.cameraDistance = Mathf.Sqrt(definition.CameraHeight * definition.CameraHeight + definition.CameraBackDistance * definition.CameraBackDistance);
            report.lateralScale = definition.LateralScale;
            report.playableWidth = 2 * NativeRhythmRules.HorizontalLimit * definition.LateralScale;
            report.cameraLateralFollow = definition.CameraLateralFollow;
            report.groundFinish = definition.EnableGroundFinish;
            report.signalCurrency = definition.FinishCurrencyPrefab != null;
            if (!report.groundFinish || !report.signalCurrency)
                throw new InvalidOperationException("The current island requires its grounded finale and Signal Spark currency prefab");
            if (Mathf.Abs(report.lateralScale - CourseLateralScale) > .001f || Mathf.Abs(report.cameraLateralFollow - .5f) > .001f)
                throw new InvalidOperationException("The comfortable island width and balanced fixed camera are not configured");
            if (!report.closeCamera || !report.unifiedHolds || !report.livingNature || report.jumpHeightScale < 1.5f)
                throw new InvalidOperationException("Living-pass presentation settings are missing");
            var elevation = IslandElevationProfile.FromJson(definition.ElevationJson.text);
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points, elevation);
            report.routeLength = route.Length;
            for (float s = 0; s < route.Length; s += .2f) report.maxHeight = Mathf.Max(report.maxHeight, elevation.Evaluate(s));
            var renderers = definition.Prefab.GetComponentsInChildren<MeshRenderer>(true);
            var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            report.renderers = renderers.Length; report.materials = materials.Length;
            if (materials.Any(m => m == null || m.shader == null ||
                m.shader.name != SurfaceShader && m.shader.name != GrassShader && m.shader.name != TreeLeavesShader && m.shader.name != WaterShader))
                throw new InvalidOperationException("Archipelago material is missing or incompatible");
            report.noPaving = !materials.Any(m => m.name.Contains("Road") || m.name.Contains("StepTreads"));
            if (!report.noPaving) throw new InvalidOperationException("The no-road version still contains paving or wooden stair treads");
            report.cliffMaterials = materials.Count(m => m.name.Contains("Cliffs"));
            report.vineMaterials = materials.Count(m => m.name.Contains("Vines"));
            report.grassMaterials = materials.Count(m => m.shader.name == GrassShader);
            report.treeLeafMaterials = materials.Count(m => m.shader.name == TreeLeavesShader);
            report.treeBarkMaterials = materials.Count(m => m.name.Contains("TreeBark"));
            report.treeLeafVertexData = true;
            foreach (var renderer in renderers.Where(r => r.sharedMaterials.Any(m => m.shader.name == TreeLeavesShader)))
            {
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                report.treeLeafBatches++;
                report.treeLeafVertexData &= mesh != null && mesh.vertexCount > 0 &&
                    mesh.HasVertexAttribute(VertexAttribute.Color) && mesh.HasVertexAttribute(VertexAttribute.TexCoord0) &&
                    mesh.HasVertexAttribute(VertexAttribute.Normal);
            }
            if (report.treeLeafMaterials == 0 || report.treeBarkMaterials == 0 || !report.treeLeafVertexData ||
                materials.Where(m => m.shader.name == TreeLeavesShader).Any(m => m.GetFloat("_Cull") > .5f))
                throw new InvalidOperationException("Natural tree bark, two-sided leaves or imported leaf UV/color/normal data is missing");
            if (report.cliffMaterials == 0 || report.vineMaterials == 0 || report.maxHeight < 11)
                throw new InvalidOperationException("Structural cliffs, hanging vines or sky passage is missing");
            if (materials.Where(m => m.name.Contains("Cliffs") || m.name.Contains("Terrain") || m.name.Contains("Bridge"))
                .Any(m => m.GetFloat("_ProtectGameplaySurface") < .5f))
                throw new InvalidOperationException("A solid cliff can still be cut open by view clearing");
            report.triangles = definition.Prefab.GetComponentsInChildren<MeshFilter>(true)
                .Sum(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(i => (long)f.sharedMesh.GetIndexCount(i) / 3));
            if (report.grassMaterials == 0 || report.renderers > 350 || report.triangles > 7000000)
                throw new InvalidOperationException("The scenery fork exceeds its prototype geometry budget");
            if (AssetDatabase.LoadAssetAtPath<Material>(ContentPath + "/ArchipelagoSky.mat")?.shader.name != SkyShader)
                throw new InvalidOperationException("The abyss needs its own airy sky");
            if (materials.Any(m => ShaderUtil.ShaderHasError(m.shader)) || ShaderUtil.ShaderHasError(Shader.Find(SkyShader)))
                throw new InvalidOperationException("An archipelago shader has compiler errors");
            report.passed = true; Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
            File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "living-chart-checks.json"), JsonUtility.ToJson(authored.Report, true));
            File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "abyss-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("ISLAND_ABYSS_CHECKS_PASS triangles=" + report.triangles + " renderers=" + report.renderers);
        }

        [MenuItem("Radio Archipelago/Floating archipelago/Capture scenery without Play Mode")]
        public static void CaptureViews()
        {
            IslandReliefJobs.RequireEditMode();
            var qualitySettings = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            var scene = EditorSceneManager.NewPreviewScene();
            var previousTarget = RenderTexture.active;
            var previousClear = Shader.GetGlobalVector("_RA_ClearViewCamera");
            var previousPipeline = QualitySettings.renderPipeline;
            RenderTexture target = null; Texture2D pixels = null;
            try
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(IslandPilotJobs.ContentPath + "/IslandPilotQuality.asset");
                if (pipeline == null) throw new InvalidOperationException("The scene-owned pilot renderer is missing");
                QualitySettings.renderPipeline = pipeline;
                var root = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, scene);
                var nature = root.AddComponent<IslandLivingNature>(); nature.Configure(definition, route);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PreviewLayer;
                var cameraNode = new GameObject("Archipelago scenery camera"); SceneManager.MoveGameObjectToScene(cameraNode, scene);
                var camera = cameraNode.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.cullingMask = 1 << PreviewLayer; camera.fieldOfView = definition.CameraFieldOfView;
                camera.nearClipPlane = .08f; camera.farClipPlane = 350; camera.aspect = 1.5f;
                camera.clearFlags = CameraClearFlags.Skybox;
                cameraNode.AddComponent<Skybox>().material = AssetDatabase.LoadAssetAtPath<Material>(ContentPath + "/ArchipelagoSky.mat");
                var cameraData = cameraNode.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = false; cameraData.renderShadows = true; cameraData.volumeLayerMask = 0;
                cameraData.allowXRRendering = false;
                var lightNode = new GameObject("Archipelago scenery sunlight"); SceneManager.MoveGameObjectToScene(lightNode, scene);
                var sun = lightNode.AddComponent<Light>(); sun.type = LightType.Directional; sun.cullingMask = 1 << PreviewLayer;
                sun.transform.rotation = RenderSettings.sun != null ? RenderSettings.sun.transform.rotation : Quaternion.Euler(43, -32, 0);
                sun.color = RenderSettings.sun != null ? RenderSettings.sun.color : new Color(1, .9f, .74f);
                sun.intensity = RenderSettings.sun != null ? RenderSettings.sun.intensity : 1.55f;
                sun.shadows = LightShadows.Soft;
                Shader.SetGlobalVector("_RA_ClearViewCamera", Vector4.zero);
                target = new RenderTexture(1296, 864, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                target.Create(); pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                string folder = Path.GetFullPath(Path.Combine(IslandPilotJobs.ReviewPath, "AbyssScenery")); Directory.CreateDirectory(folder);
                foreach (var view in new[] { ("stone-bridge", 90.5f), ("ravine", 142f), ("ascent", 413f), ("sky-islands", 440f), ("descent", 474f), ("island-bodies", 445f), ("islet-garden", 420f) })
                {
                    nature.UpdatePresentation(view.Item2, 1.5f);
                    var frame = route.Evaluate(view.Item2);
                    var origin = frame.Position - frame.Tangent * definition.CameraBackDistance + Vector3.up * definition.CameraHeight;
                    var look = route.Evaluate(view.Item2 + definition.CameraLookAhead).Position + Vector3.up;
                    if (view.Item1 == "island-bodies")
                    {
                        origin = frame.Position + frame.Right * 52 - frame.Tangent * 22 + Vector3.up * 5;
                        look = frame.Position - Vector3.up * 10;
                    }
                    else if (view.Item1 == "islet-garden")
                    {
                        origin = frame.Position + frame.Right * 18 - frame.Tangent * 14 + Vector3.up * 7;
                        look = frame.Position - Vector3.up * 3;
                    }
                    camera.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(look - origin, Vector3.up));
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(folder, view.Item1 + ".png"), pixels.EncodeToPNG());
                }
            }
            finally
            {
                Shader.SetGlobalVector("_RA_ClearViewCamera", previousClear); RenderTexture.active = previousTarget;
                QualitySettings.renderPipeline = previousPipeline;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
                RestoreQualitySettingsFile(qualitySettings);
            }
            Debug.Log("ISLAND_ABYSS_SCENERY_CAPTURED — Edit Mode only; no gameplay run started.");
        }

        private static void RestoreQualitySettingsFile(byte[] original)
        {
            const string path = "ProjectSettings/QualitySettings.asset";
            // Setting a temporary pipeline also reserializes every quality tier
            // on this Editor version. Preserve the user's exact disk settings.
            if (!File.ReadAllBytes(path).SequenceEqual(original)) File.WriteAllBytes(path, original);
        }
    }
}
