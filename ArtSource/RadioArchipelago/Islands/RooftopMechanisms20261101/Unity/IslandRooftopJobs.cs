using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static class IslandRooftopJobs
    {
        private const string Evidence = "../Docs/Design/RadioArchipelago/RooftopMechanisms20261101";
        private static readonly string[] Labels = { "Emerald", "Coral", "Moon" };
        private static readonly string[] DefinitionPaths = {
            IslandAbyssJobs.DefinitionPath, CoralOasisIslandJobs.DefinitionPath, MoonResonanceIslandJobs.DefinitionPath };
        private static readonly string[] Folders = {
            "Assets/RadioArchipelago/Content/Islands/EmeraldAbyss",
            "Assets/RadioArchipelago/Content/Islands/CoralOasis",
            "Assets/RadioArchipelago/Content/Islands/MoonResonance" };
        [Serializable] private sealed class Dish { public Vector3 pivot; public float scale; }
        [Serializable] private sealed class PreviewDocument { public Dish[] dishes; }
        [Serializable] private sealed class EmptyBinding { public string island, nodeName, guid; public long rendererFileId; }
        [Serializable] private sealed class EmptyBindings { public List<EmptyBinding> entries = new List<EmptyBinding>(); }

        public static void CaptureEmptyBindings()
        {
            IslandReliefJobs.RequireEditMode();
            var result = new EmptyBindings();
            for (int i = 0; i < Labels.Length; i++)
                foreach (var renderer in Load(i).Prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount != 0) continue;
                    var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
                    Require(source != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id), "Missing source identifier for empty remainder.");
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out guid, out id);
                    result.entries.Add(new EmptyBinding { island = Labels[i], nodeName = renderer.name, guid = guid, rendererFileId = id });
                }
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "/obsolete-renderer-bindings.json", JsonUtility.ToJson(result,true));
        }
        [Serializable] private sealed class StageReport
        {
            public string island;
            public int rotors, newRenderers, oldRenderers, materialSlots;
            public bool staticBindingsPreserved, sharedMaterials, staticFlagsClear, allPivotsStable, pauseFreezes,
                allHeadsMove, cleanupRestores, noPhysicsAdded;
            public long warmedTickBytes;
            public float minimumAngularChange, maxPivotMovement;
            public List<string> images = new List<string>();
        }
        [Serializable] private sealed class Report { public bool pass; public string checkedUtc; public List<StageReport> stages = new List<StageReport>(); }
        private static IslandStageDefinition Load(int i) => AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPaths[i]);

        public static void CaptureBefore()
        {
            IslandReliefJobs.RequireEditMode();
            const string staging = "../ArtSource/RadioArchipelago/Islands/RooftopMechanisms20261101/STAGING/Coral/RooftopMotion.json";
            var document = JsonUtility.FromJson<PreviewDocument>(File.ReadAllText(staging));
            var previous = QualitySettings.renderPipeline; var oldTarget = RenderTexture.active;
            var clear = Shader.GetGlobalVector("_RA_ClearViewCamera"); bool fog = RenderSettings.fog;
            var scene = EditorSceneManager.NewPreviewScene(); RenderTexture target = null; Texture2D pixels = null;
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(IslandPilotJobs.ContentPath + "/IslandPilotQuality.asset");
                RenderSettings.fog = false; Shader.SetGlobalVector("_RA_ClearViewCamera", Vector4.zero);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(Load(1).Prefab, scene);
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 28;
                var cameraObject = new GameObject("Before roof camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.cullingMask = 1 << 28; camera.fieldOfView = 50; camera.aspect = 1.5f; camera.nearClipPlane = .05f; camera.farClipPlane = 600;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Load(1).SkyColor;
                var data = cameraObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = true;
                Vector3 p = document.dishes[0].pivot + Vector3.up * .7f; Vector3 eye = p + new Vector3(4,3,5);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(p-eye,Vector3.up));
                var lightObject = new GameObject("Before roof key"); SceneManager.MoveGameObjectToScene(lightObject,scene);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.color = new Color(1,.93f,.84f); light.cullingMask = 1 << 28;
                light.transform.rotation = Quaternion.Euler(38,-30,0); light.shadows = LightShadows.Soft;
                target = new RenderTexture(1200,800,24); target.Create(); pixels = new Texture2D(1200,800,TextureFormat.RGBA32,false);
                Directory.CreateDirectory(Evidence); Capture(camera,target,pixels,Evidence + "/Coral-before-dish.png");
            }
            finally
            {
                RenderTexture.active = oldTarget; QualitySettings.renderPipeline = previous; RenderSettings.fog = fog; Shader.SetGlobalVector("_RA_ClearViewCamera",clear);
                if(target!=null){target.Release();Object.DestroyImmediate(target);} if(pixels!=null)Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [MenuItem("Radio Archipelago/Rooftops/Install authored motion")]
        public static void Install()
        {
            IslandReliefJobs.RequireEditMode();
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O") };
            for (int i = 0; i < Labels.Length; i++)
            {
                var definition = Load(i);
                Require(definition != null && definition.Prefab != null, "Missing rooftop island definition.");
                string manifestPath = Folders[i] + "/RooftopMotion.json";
                var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath);
                Require(manifest != null, "Import rooftop manifest first: " + manifestPath);
                var document = JsonUtility.FromJson<IslandRooftopMotion.Document>(manifest.text);
                string prefabPath = AssetDatabase.GetAssetPath(definition.Prefab);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var nodes = root.GetComponentsInChildren<Transform>(true).GroupBy(n => n.name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                    var names = new HashSet<string>(document.mechanisms.Select(m => m.nodeName), StringComparer.Ordinal);
                    var staticMaterials = root.GetComponentsInChildren<MeshRenderer>(true)
                        .Where(r => !names.Contains(r.name)).ToDictionary(r => r, r => r.sharedMaterials);
                    var stage = new StageReport { island = Labels[i], oldRenderers = staticMaterials.Count };
                    foreach (var entry in document.mechanisms)
                    {
                        Require(nodes.TryGetValue(entry.nodeName, out Transform node), "Missing imported rooftop node: " + entry.nodeName);
                        var renderer = node.GetComponent<MeshRenderer>();
                        Require(renderer != null && entry.materialSources != null, "Invalid rooftop renderer/material binding.");
                        var materials = new Material[entry.materialSources.Length];
                        for (int slot = 0; slot < materials.Length; slot++)
                        {
                            var binding = entry.materialSources[slot];
                            Require(nodes.TryGetValue(binding.nodeName, out Transform source), "Missing shared material source: " + binding.nodeName);
                            var sourceRenderer = source.GetComponent<MeshRenderer>();
                            Require(sourceRenderer != null && binding.slot < sourceRenderer.sharedMaterials.Length, "Missing source material slot.");
                            materials[slot] = sourceRenderer.sharedMaterials[binding.slot];
                            Require(materials[slot] != null, "Null shared rooftop material.");
                        }
                        renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                        var flags = GameObjectUtility.GetStaticEditorFlags(node.gameObject);
                        GameObjectUtility.SetStaticEditorFlags(node.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                        stage.materialSlots += materials.Length; stage.newRenderers++;
                    }
                    Require(staticMaterials.All(pair => pair.Key.sharedMaterials.SequenceEqual(pair.Value)), "Existing static material binding changed.");
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    stage.staticBindingsPreserved = stage.sharedMaterials = stage.staticFlagsClear = true;
                    stage.rotors = document.mechanisms.Length;
                    report.stages.Add(stage);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                definition.RooftopMotionJson = manifest;
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            }
            report.pass = true;
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "/installation.json", JsonUtility.ToJson(report, true));
            Debug.Log("ROOFTOPS_INSTALLED: 12 rigid heads; existing static bindings and shared materials preserved.");
        }

        [MenuItem("Radio Archipelago/Rooftops/Review imported motion")]
        public static void Review()
        {
            IslandReliefJobs.RequireEditMode();
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O") };
            var qualityBefore = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            var pipelineBefore = QualitySettings.renderPipeline;
            var oldTarget = RenderTexture.active;
            bool fogBefore = RenderSettings.fog;
            var clearBefore = Shader.GetGlobalVector("_RA_ClearViewCamera");
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(IslandPilotJobs.ContentPath + "/IslandPilotQuality.asset");
                RenderSettings.fog = false; Shader.SetGlobalVector("_RA_ClearViewCamera", Vector4.zero);
                Directory.CreateDirectory(Evidence);
                for (int i = 0; i < Labels.Length; i++) report.stages.Add(ReviewStage(i));
            }
            finally
            {
                RenderTexture.active = oldTarget; QualitySettings.renderPipeline = pipelineBefore;
                RenderSettings.fog = fogBefore; Shader.SetGlobalVector("_RA_ClearViewCamera", clearBefore);
                if (!File.ReadAllBytes("ProjectSettings/QualitySettings.asset").SequenceEqual(qualityBefore))
                    File.WriteAllBytes("ProjectSettings/QualitySettings.asset", qualityBefore);
            }
            report.pass = true;
            File.WriteAllText(Evidence + "/native-motion.json", JsonUtility.ToJson(report, true));
            Debug.Log("ROOFTOPS_REVIEW PASS: actual imported pivots, shared materials, motion, freeze, cleanup and three pose captures per island.");
        }

        private static StageReport ReviewStage(int index)
        {
            var definition = Load(index); Require(definition.RooftopMotionJson != null, "Install the rooftop manifest first.");
            var document = JsonUtility.FromJson<IslandRooftopMotion.Document>(definition.RooftopMotionJson.text);
            var scene = EditorSceneManager.NewPreviewScene();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, scene);
            var owner = new GameObject("Canonical rooftop coordinate space"); SceneManager.MoveGameObjectToScene(owner, scene);
            var motion = new IslandRooftopMotion();
            RenderTexture target = null; Texture2D pixels = null;
            var report = new StageReport { island = Labels[index], minimumAngularChange = 360 };
            try
            {
                var nodes = root.GetComponentsInChildren<Transform>(true).GroupBy(n => n.name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                foreach (Transform node in nodes.Values) node.gameObject.layer = 28;
                var rotorNodes = document.mechanisms.Select(m => nodes[m.nodeName]).ToArray();
                var rest = rotorNodes.Select(n => n.localRotation).ToArray();
                var pivots = rotorNodes.Select(n => n.position).ToArray();
                var oldRenderers = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => !r.name.StartsWith("RA_Rooftop_", StringComparison.Ordinal)).ToArray();
                var staticPoses = oldRenderers.Select(r => r.transform.localToWorldMatrix).ToArray();
                foreach (var node in rotorNodes)
                {
                    var renderer = node.GetComponent<MeshRenderer>();
                    Require(renderer != null && renderer.sharedMaterials.All(m => m != null && !ShaderUtil.ShaderHasError(m.shader) && m.shader.name == "RadioArchipelago/Island Surface"), "Missing/unsupported rooftop shared material.");
                    Require((GameObjectUtility.GetStaticEditorFlags(node.gameObject) & StaticEditorFlags.BatchingStatic) == 0, "A moving rooftop is batching-static.");
                }
                foreach (var entry in document.mechanisms)
                {
                    var materials = nodes[entry.nodeName].GetComponent<MeshRenderer>().sharedMaterials;
                    for (int slot = 0; slot < entry.materialSources.Length; slot++)
                    {
                        var source = entry.materialSources[slot];
                        Require(nodes.TryGetValue(source.nodeName, out Transform sourceNode) && sourceNode.GetComponent<MeshRenderer>() != null &&
                            sourceNode.GetComponent<MeshRenderer>().sharedMaterials[source.slot] == materials[slot], "Rooftop shared material source is no longer valid: " + source.nodeName);
                    }
                }
                motion.Configure(root.transform, owner.transform, definition.RooftopMotionJson);
                Require(motion.Count == 4, "Each authored island must have four distinct moving mechanisms.");
                report.rotors = motion.Count; report.sharedMaterials = report.staticFlagsClear = true;
                report.noPhysicsAdded = rotorNodes.All(n => n.GetComponentsInChildren<Collider>(true).Length == 0 && n.GetComponentsInChildren<Rigidbody>(true).Length == 0);
                Require(report.noPhysicsAdded, "Rooftop motion must not add physics.");
                var cameraObject = new GameObject("Rooftop evidence camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.fieldOfView = 50; camera.aspect = 1.5f; camera.nearClipPlane = .05f; camera.farClipPlane = 600;
                camera.cullingMask = 1 << 28; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = definition.SkyColor;
                var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>(); cameraData.renderPostProcessing = false; cameraData.renderShadows = true;
                var lightObject = new GameObject("Rooftop evidence key"); SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.color = new Color(1, .93f, .84f); light.cullingMask = 1 << 28;
                light.transform.rotation = Quaternion.Euler(38, -30, 0); light.shadows = LightShadows.Soft;
                target = new RenderTexture(1200, 800, 24); target.Create(); pixels = new Texture2D(1200, 800, TextureFormat.RGBA32, false);
                var focus = rotorNodes[0]; var bounds = focus.GetComponent<MeshRenderer>().bounds;
                Vector3 look = bounds.center;
                float extent = Mathf.Max(2.5f, bounds.extents.magnitude);
                camera.transform.SetPositionAndRotation(look + new Vector3(1, .7f, 1).normalized * extent * 3.5f,
                    Quaternion.LookRotation(-new Vector3(1, .7f, 1).normalized, Vector3.up));
                Capture(camera, target, pixels, Evidence + "/" + Labels[index] + "-pose-0.png"); report.images.Add(Labels[index] + "-pose-0.png");
                for (int step = 0; step < 6; step++) motion.Tick(.1f);
                Capture(camera, target, pixels, Evidence + "/" + Labels[index] + "-pose-0.6.png"); report.images.Add(Labels[index] + "-pose-0.6.png");
                var frozen = rotorNodes.Select(n => n.localRotation).ToArray(); motion.Tick(0);
                report.pauseFreezes = rotorNodes.Select((n,j) => Quaternion.Angle(n.localRotation, frozen[j])).All(a => a < .0001f);
                Require(report.pauseFreezes, "Pause changed a rooftop pose.");
                for (int step = 0; step < 6; step++) motion.Tick(.1f);
                Capture(camera, target, pixels, Evidence + "/" + Labels[index] + "-pose-1.2.png"); report.images.Add(Labels[index] + "-pose-1.2.png");
                for (int n = 0; n < rotorNodes.Length; n++)
                {
                    report.minimumAngularChange = Mathf.Min(report.minimumAngularChange, Quaternion.Angle(rest[n], rotorNodes[n].localRotation));
                    report.maxPivotMovement = Mathf.Max(report.maxPivotMovement, Vector3.Distance(pivots[n], rotorNodes[n].position));
                }
                report.allHeadsMove = report.minimumAngularChange > 40;
                report.allPivotsStable = report.maxPivotMovement < .0001f;
                Require(report.allHeadsMove && report.allPivotsStable, "Roof heads did not rotate about their fixed pivots.");
                report.staticBindingsPreserved = oldRenderers.Select((r,j) => r.transform.localToWorldMatrix == staticPoses[j]).All(v => v);
                Require(report.staticBindingsPreserved, "A static roof/building moved.");
                for (int warm = 0; warm < 20; warm++) motion.Tick(.016f);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < 2000; frame++) motion.Tick(.016f);
                report.warmedTickBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(report.warmedTickBytes == 0, "Rooftop ticks allocate managed memory.");
                motion.Dispose();
                report.cleanupRestores = motion.Count == 0 && rotorNodes.Select((n,j) => Quaternion.Angle(n.localRotation,rest[j])).All(a => a < .0001f);
                Require(report.cleanupRestores, "Rooftop cleanup did not restore authored poses.");
                var preview = JsonUtility.FromJson<PreviewDocument>(definition.RooftopMotionJson.text);
                if (preview.dishes != null && preview.dishes.Length > 0)
                {
                    Vector3 p = preview.dishes[0].pivot + Vector3.up * .7f;
                    Vector3 eye = p + new Vector3(4, 3, 5);
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(p - eye, Vector3.up));
                    Capture(camera, target, pixels, Evidence + "/Coral-remounted-dish.png"); report.images.Add("Coral-remounted-dish.png");
                }
                return report;
            }
            finally
            {
                motion.Dispose();
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(root); Object.DestroyImmediate(owner);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D pixels, string path)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(false,false);
            File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}

