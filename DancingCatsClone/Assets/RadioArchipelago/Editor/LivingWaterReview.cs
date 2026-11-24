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
    /// <summary>Real island-water art previews and a short, isolated native-clock proof. No gameplay or profile writes.</summary>
    [InitializeOnLoad]
    public static class LivingWaterReview
    {
        private const string ClockKey = "RadioArchipelago.LivingWater.NativeClock";
        private const string SceneKey = ClockKey + ".PreviousScene";
        private const string AmberKey = ClockKey + ".AmberSourceScenery", PhaseKey = ClockKey + ".Phase", RunKey = ClockKey + ".RunId";
        private const int ExpectedLiveFrames = 14;
        private static bool AmberLive => SessionState.GetBool(AmberKey,false);
        private static string LiveFolder => AmberLive ? AmberWindsIslandJobs.ReviewFolder + "/WaterNative" : Folder;
        private static string LiveTag => AmberLive ? AmberPhaseTags[SessionState.GetInt(PhaseKey,0)] : "live";
        private static readonly string[] AmberPhaseTags = { "stream", "roll210", "side-roll", "waterfall" };
        private const string SideCascadePrefix = "RA_Amber_SideCascade_";
        private static readonly Vector3[] SideCascadeTargets = { new Vector3(47,28,-96),new Vector3(49,20,-96),new Vector3(49,12,-96) };
        private const string Folder = "Library/RadioArchipelago/LivingWater";
        private const string ShaderName = "RadioArchipelago/Coral Water";
        private static readonly string[] Definitions = {
            IslandAbyssJobs.DefinitionPath, CoralOasisIslandJobs.DefinitionPath, MoonResonanceIslandJobs.DefinitionPath,
            "Assets/RadioArchipelago/Content/Islands/AmberWinds/AmberWinds.asset" };
        private static readonly string[] Names = { "emerald", "coral", "moon", "amber" };
        private static readonly int PreviewTime = Shader.PropertyToID("_WaterPreviewTime");
        private const string RipplePath = "Assets/RadioArchipelago/Art/WaterRippleNormal.png";
        private static View live;
        private static Color32[] firstLive;
        private static float firstLiveTime;
        private static int liveFrame;
        private static double started;
        private static readonly List<float> LiveFrameTimes = new List<float>(16);

        [Serializable] private sealed class Feature
        {
            public string island, feature, definition;
            public int waterRenderers, movingPixels, coveredPixels, frames;
            public float changedWaterFraction;
            public Vector3 centre;
        }
        [Serializable] private sealed class Report { public bool pass; public string unityVersion, scope; public List<Feature> features = new List<Feature>(); }
        [Serializable] private sealed class FlowProperties { public string material; public float useFlowUv,currentSpeed,flowSpeed,fall,previewTime; }
        [Serializable] private sealed class LiveReport { public bool pass; public string state, scope, feature, runId; public float firstTime, lastTime, cameraStation; public int movingPixels, coveredPixels, frames; public bool defaultClock, originalSceneRestored; public FlowProperties[] materialProperties; public float[] frameTimes; public DirectionSample[] flowSamples; }
        [Serializable] private sealed class AmberLiveReport { public bool pass, sourceSceneryOnly, originalSceneRestored; public LiveReport[] features; }
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }

        private sealed class View : IDisposable
        {
            internal Scene scene;
            internal GameObject owner;
            internal Camera camera;
            internal RenderTexture target;
            internal Texture2D pixels;
            internal List<Material> clones = new List<Material>();
            internal Renderer[] waters;
            internal Vector3 centre;
            internal int width = 960, height = 640;
            internal float selectedStation;
            internal View(IslandStageDefinition definition, bool waterfall, bool preview, string measuredKind = null, string measuredNodePrefix = null)
            {
                scene = preview ? EditorSceneManager.NewPreviewScene() : SceneManager.GetActiveScene();
                try
                {
                owner = new GameObject("Isolated living water art review");
                SceneManager.MoveGameObjectToScene(owner, scene);
                var scenery = Object.Instantiate(definition.Prefab, owner.transform);
                foreach (var t in scenery.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 29;
                waters = scenery.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Any(m => m && m.shader && m.shader.name == ShaderName)).ToArray();
                if (!string.IsNullOrEmpty(measuredKind))
                    waters = waters.Where(r => r.sharedMaterials.Any(m => m && m.name.IndexOf(measuredKind,StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                if (!string.IsNullOrEmpty(measuredNodePrefix))
                    waters = waters.Where(r => r.name.StartsWith(measuredNodePrefix,StringComparison.Ordinal)).ToArray();
                if (waters.Length == 0) throw new InvalidOperationException("The actual island prefab has no living water: " + definition.name + " / " + measuredKind);
                if (waters.SelectMany(r => r.sharedMaterials).Any(m => m && m.shader && m.shader.name == ShaderName && ShaderUtil.ShaderHasError(m.shader)))
                    throw new InvalidOperationException("The water shader has compilation errors.");
                foreach (var renderer in waters)
                {
                    renderer.gameObject.layer = 30;
                    if (!preview) continue;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var clone = new Material(materials[i]) { hideFlags = HideFlags.HideAndDontSave };
                        clones.Add(clone); materials[i] = clone;
                    }
                    renderer.sharedMaterials = materials;
                }
                Vector3 facing; centre = FindSurface(waters, waterfall, out facing);
                var node = new GameObject("Native water review camera"); node.transform.SetParent(owner.transform, false);
                camera = node.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.57f,.73f,.78f);
                camera.fieldOfView = 50; camera.nearClipPlane = .08f; camera.farClipPlane = 160; camera.aspect = 1.5f;
                camera.cullingMask = (1 << 29) | (1 << 30);
                Vector3 eye = centre + (waterfall ? facing * 21 + Vector3.up * 5 : new Vector3(-9,23,-13));
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(centre - eye));
                var data = node.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.allowXRRendering = false;
                var light = new GameObject("Water review daylight").AddComponent<Light>(); light.transform.SetParent(owner.transform, false);
                light.type = LightType.Directional; light.color = new Color(1,.94f,.84f); light.intensity = 1.3f;
                light.shadows = LightShadows.Soft; light.cullingMask = camera.cullingMask; light.transform.rotation = Quaternion.Euler(48,-32,0);
                target = new RenderTexture(960,640,24); target.Create();
                pixels = new Texture2D(960,640,TextureFormat.RGBA32,false);
                }
                catch { Dispose(); throw; }
            }
            internal Color32[] Capture(string path, bool mask = false)
            {
                int culling = camera.cullingMask; Color clear = camera.backgroundColor;
                var previous = RenderTexture.active;
                try
                {
                    if (mask) { camera.cullingMask = 1 << 30; camera.backgroundColor = Color.black; }
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply(false,false);
                    if (path != null) File.WriteAllBytes(path,pixels.EncodeToPNG());
                    return pixels.GetPixels32();
                }
                finally { RenderTexture.active = previous; camera.cullingMask = culling; camera.backgroundColor = clear; }
            }
            internal Color32[] VisibleWaterMask()
            {
                var on = Capture(null);
                var states = waters.Select(r => r.enabled).ToArray();
                try
                {
                    foreach (var renderer in waters) renderer.enabled = false;
                    var off = Capture(null);
                    for (int i = 0; i < on.Length; i++)
                    {
                        int difference = Math.Abs(on[i].r-off[i].r)+Math.Abs(on[i].g-off[i].g)+Math.Abs(on[i].b-off[i].b);
                        on[i] = difference > 15 ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
                    }
                    return on;
                }
                finally { for (int i = 0; i < waters.Length; i++) waters[i].enabled = states[i]; }
            }
            internal void UseGameplayCamera(IslandStageDefinition definition, float fixedStation = -1)
            {
                if (definition.RouteJson == null || definition.ElevationJson == null)
                    throw new InvalidOperationException("Gameplay water view needs the real route and elevation.");
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                width = 540; height = 960;
                target = new RenderTexture(width,height,24); target.Create();
                pixels = new Texture2D(width,height,TextureFormat.RGBA32,false);
                camera.aspect = (float)width/height; camera.fieldOfView = definition.CameraFieldOfView;
                var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                    IslandElevationProfile.FromJson(definition.ElevationJson.text));
                var rig = new IslandCloseCameraRig();
                if (fixedStation >= 0)
                {
                    var fixedPose = rig.Step(route,fixedStation,0,definition.CameraBackDistance,definition.CameraHeight,
                        definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,definition.CameraSmoothing,0,true);
                    camera.transform.SetPositionAndRotation(fixedPose.Position,fixedPose.Rotation);
                    selectedStation = fixedStation;
                    return;
                }
                var candidates = new List<(float score, float station, IslandCloseCameraRig.Pose pose)>();
                for (float station = 0; station < route.Length; station += 2)
                {
                    var pose = rig.Step(route,station,0,definition.CameraBackDistance,definition.CameraHeight,
                        definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,
                        definition.CameraSmoothing,0,true);
                    camera.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
                    Vector3 point = camera.WorldToViewportPoint(centre);
                    if (point.z < 8 || point.z > 60 || point.x < .03f || point.x > .97f || point.y < .03f || point.y > .93f) continue;
                    float score = Mathf.Abs(point.x-.5f)*2+Mathf.Abs(point.y-.47f)+Mathf.Abs(point.z-25)*.008f;
                    candidates.Add((score,station,pose));
                }
                int best = -1, coverage = 0;
                var shortlist = candidates.OrderBy(c => c.score).Take(8).Select(c => (c.station,c.pose)).ToList();
                // A large pool's centre can be hidden while another bank or
                // cascade is visible from the course. Add broad route samples
                // rather than pretending a water-only mask proves visibility.
                for (int i = 0; i < 32; i++)
                    shortlist.Add((route.Length*i/32,rig.Step(route,route.Length*i/32,0,definition.CameraBackDistance,definition.CameraHeight,
                        definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,
                        definition.CameraSmoothing,0,true)));
                for (int i = 0; i < shortlist.Count; i++)
                {
                    var pose = shortlist[i].pose;
                    camera.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
                    int visible = VisibleWaterMask().Count(pixel => pixel.r > 0);
                    if (visible > coverage) { coverage = visible; best = i; }
                }
                if (best < 0 || coverage < 500)
                    throw new InvalidOperationException("Water is not visible from any sampled real gameplay camera: " + definition.name);
                var chosen = shortlist[best]; selectedStation = chosen.station;
                camera.transform.SetPositionAndRotation(chosen.pose.Position,chosen.pose.Rotation);
            }
            public void Dispose()
            {
                if (target) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels) Object.DestroyImmediate(pixels);
                if (owner) Object.DestroyImmediate(owner);
                foreach (var material in clones) Object.DestroyImmediate(material);
                if (scene.IsValid() && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static LivingWaterReview()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            EditorApplication.update += Tick;
            if (SessionState.GetBool(ClockKey,false) && EditorApplication.isPlaying) started = EditorApplication.timeSinceStartup;
        }
        [MenuItem("Tools/Radio Archipelago/Preview legacy Emerald water")]
        public static void Capture()
        {
            IslandReliefJobs.RequireEditMode(); EnsureRippleBindings(); Directory.CreateDirectory(Folder);
            var report = new Report { unityVersion = Application.unityVersion, scope = "Actual imported island prefabs/materials. Art views:10frames/1.8s. Real route/portrait camera-rig scenery views:16frames/1s, no actor/UI. Only cloned materials use a preview clock; saved assets/settings unchanged. Visible-water masks include terrain/foliage occlusion." };
            var pipeline = QualitySettings.renderPipeline;
            var qualityFile = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            bool fog = RenderSettings.fog; var ambient = RenderSettings.ambientMode; Color ambientLight = RenderSettings.ambientLight;
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(IslandPilotJobs.ContentPath + "/IslandPilotQuality.asset");
                RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.40f,.48f,.55f);
                // Emerald keeps its original low river. The other islands now
                // use genuine joined lake/fall surfaces and the separate native
                // visual menu entries; their shader has no fake preview clock.
                for (int island = 0; island < 1; island++)
                {
                    var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(Definitions[island]);
                    if (island == 3 && (definition == null || definition.Prefab == null)) continue;
                    foreach (bool fall in new[] { false,true })
                    // Continuous falls now have native-clock visual captures;
                    // this legacy preview clock still verifies the old pools.
                    if (!fall || definition.Prefab.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.Contains("Waterfall") && r.sharedMaterials.Any(m=>m && m.shader && m.shader.name==ShaderName)))
                    using (var view = new View(definition,fall,true))
                    {
                        string tag = Names[island] + (fall ? "-cascade" : "-pool");
                        var feature = new Feature { island = Names[island], definition = Definitions[island], feature = tag, waterRenderers = view.waters.Length, centre = view.centre, frames = 10 };
                        Color32[] first = null, last = null, mask = null;
                        for (int frame = 0; frame < 10; frame++)
                        {
                            foreach (var material in view.clones) if (material.HasProperty(PreviewTime)) material.SetFloat(PreviewTime, frame * .2f);
                            var current = view.Capture(Folder + "/" + tag + "-" + frame.ToString("00") + ".png");
                            if (frame == 0) { first = current; mask = view.Capture(null,true); }
                            if (frame == 9) last = current;
                        }
                        Changed(first,last,mask,out feature.movingPixels,out feature.coveredPixels);
                        feature.changedWaterFraction = (float)feature.movingPixels / Math.Max(1,feature.coveredPixels);
                        if (feature.coveredPixels < 1000 || feature.changedWaterFraction < .02f) throw new InvalidOperationException("No visible animated water in the native capture: " + tag);
                        if (fall)
                        {
                            var curtain = view.waters.Where(r => r.name.Contains("Waterfall")).OrderByDescending(r => r.bounds.size.y).FirstOrDefault();
                            if (curtain)
                            {
                                Vector3 direction = -view.camera.transform.forward; direction.y = 0; direction.Normalize();
                                Vector3 target = curtain.bounds.center;
                                Vector3 eye = target + direction * Mathf.Max(35,curtain.bounds.size.y*1.3f) + Vector3.up * 5;
                                view.camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
                                view.Capture(Folder + "/" + tag + "-full.png");
                            }
                        }
                        report.features.Add(feature);
                    }
                    // Reproduce the actual fixed camera rig and portrait FOV.
                    // This is scenery-only evidence, not a gameplay recording.
                    using (var view = new View(definition,false,true))
                    {
                        foreach (var material in view.clones) material.SetFloat(PreviewTime,0);
                        view.UseGameplayCamera(definition);
                        string tag = Names[island] + "-gameplay-water";
                        Color32[] first = null, last = null, mask = view.VisibleWaterMask();
                        for (int frame = 0; frame < 16; frame++)
                        {
                            foreach (var material in view.clones) material.SetFloat(PreviewTime,frame/15f);
                            var current = view.Capture(Folder + "/" + tag + "-" + frame.ToString("00") + ".png");
                            if (frame == 0) first = current;
                            if (frame == 15) last = current;
                        }
                        Changed(first,last,mask,out int moving,out int covered);
                        if (covered < 500 || moving < 100)
                            throw new InvalidOperationException("No perceptible water motion at a real route camera: " + tag);
                        report.features.Add(new Feature { island = Names[island],definition = Definitions[island],feature = tag,
                            waterRenderers = view.waters.Length,centre = view.centre,frames = 16,movingPixels = moving,
                            coveredPixels = covered,changedWaterFraction = (float)moving/Math.Max(1,covered) });
                    }
                }
                report.pass = true; File.WriteAllText(Folder + "/preview.json",JsonUtility.ToJson(report,true));
                Debug.Log("LIVING_WATER_PREVIEW PASS: " + JsonUtility.ToJson(report));
            }
            finally
            {
                QualitySettings.renderPipeline = pipeline; RenderSettings.fog = fog; RenderSettings.ambientMode = ambient; RenderSettings.ambientLight = ambientLight;
                if (!File.ReadAllBytes("ProjectSettings/QualitySettings.asset").SequenceEqual(qualityFile)) File.WriteAllBytes("ProjectSettings/QualitySettings.asset",qualityFile);
            }
        }

        private static void EnsureRippleBindings()
        {
            AssetDatabase.ImportAsset(RipplePath,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RipplePath);
            if (importer.sRGBTexture || importer.textureType != TextureImporterType.Default || importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled)
            {
                // Raw RGB encodes XYZ normals explicitly; do not repack to AG.
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear; importer.maxTextureSize = 256;
                importer.alphaIsTransparency = false; importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RipplePath);
            foreach (string definitionPath in Definitions)
            {
                var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(definitionPath);
                if (definition == null || definition.Prefab == null) continue;
                foreach (var material in definition.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
                {
                    if (!material || material.shader.name != ShaderName || material.GetFloat("_FoamLayer") > .5f) continue;
                    bool changed = material.GetTexture("_RippleNormal") != texture;
                    if (changed) material.SetTexture("_RippleNormal",texture);
                    if (material.name.Contains("Waterfall") && (material.GetFloat("_ZWrite") != 0 || material.renderQueue != 3000))
                    {
                        material.SetFloat("_ZWrite",0); material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10); material.renderQueue = 3000; changed = true;
                    }
                    if (changed) { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
                }
            }
        }

        private static Vector3 FindSurface(Renderer[] renderers, bool fall, out Vector3 facing)
        {
            float best = -1; Vector3 chosen = Vector3.zero; facing = Vector3.forward;
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials.All(m => m.GetFloat("_FoamLayer") > .5f)) continue;
                var filter = renderer.GetComponent<MeshFilter>(); if (!filter || !filter.sharedMesh) continue;
                var vertices = filter.sharedMesh.vertices; var triangles = filter.sharedMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = filter.transform.TransformPoint(vertices[triangles[i]]), b = filter.transform.TransformPoint(vertices[triangles[i+1]]), c = filter.transform.TransformPoint(vertices[triangles[i+2]]);
                    Vector3 cross = Vector3.Cross(b-a,c-a); float area = cross.magnitude;
                    if (area < .001f) continue;
                    Vector3 normal = cross / area;
                    if (fall ? Mathf.Abs(normal.y) > .35f : Mathf.Abs(normal.y) < .93f) continue;
                    if (fall && renderer.name.IndexOf("Waterfall",StringComparison.OrdinalIgnoreCase) >= 0) area *= 100;
                    if (area <= best) continue;
                    best = area; chosen = (a+b+c)/3; facing = new Vector3(normal.x,0,normal.z).normalized;
                    Vector3 away = chosen - renderer.bounds.center; away.y = 0;
                    if (Vector3.Dot(facing,away) < 0) facing = -facing;
                    if (fall) chosen.y = renderer.bounds.max.y - Mathf.Min(12,renderer.bounds.size.y*.3f);
                }
            }
            if (best < 0) throw new InvalidOperationException("The imported island has no " + (fall ? "cascade" : "pool") + " surface.");
            return chosen;
        }
        private static void Changed(Color32[] first, Color32[] last, Color32[] mask, out int moving, out int covered)
        {
            moving = covered = 0;
            for (int i = 0; i < first.Length; i++)
            {
                if (mask[i].r + mask[i].g + mask[i].b < 20) continue;
                covered++;
                if (Math.Abs(first[i].r-last[i].r)+Math.Abs(first[i].g-last[i].g)+Math.Abs(first[i].b-last[i].b) > 8) moving++;
            }
        }

        [Serializable] private sealed class SeamVertex
        {
            public string sourceNode;
            public Vector3 position,normal;
            public Vector2 uv;
            public Color color;
        }
        [Serializable] private sealed class SeamPair
        {
            public string joint;
            public float u,requestedU,positionError,normalDot,normalAngle,maximumPredictedAnimatedGap,colorError;
            public SeamVertex stream,fall;
        }
        [Serializable] private sealed class AlphaComparison
        {
            public string scope = "Actual imported three joins at five nearest existing U samples (actual and requested U recorded); matched normals/colors and frozen e5595 Vert displacement at ten real-valued sample times. R3 cloned-alpha comparison at UV V53.113525390625. Identical fixed210m portrait camera and cloned-material preview clock2s. B changes only cloned Fall SrcBlendOne/DstBlendZero; saved ZWrite0/queue3000 and all other properties stay unchanged. No source or real material writes.";
            public bool savedMaterialsUnchanged;
            public SeamPair[] seam;
        }
        public static void CaptureAmberAlphaComparison()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = AmberWindsIslandJobs.LoadDefinition();
            if (definition == null) throw new InvalidOperationException("The real fourth-island definition is required for this imported seam diagnosis.");
            string folder = AmberWindsIslandJobs.ReviewFolder + "/SeamDiagnosis"; Directory.CreateDirectory(folder);
            var saved = definition.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material && material.shader && material.shader.name == ShaderName).Distinct().ToArray();
            string[] before = saved.Select(material => EditorJsonUtility.ToJson(material)).ToArray();
            bool[] dirty = saved.Select(EditorUtility.IsDirty).ToArray();
            var report = new AlphaComparison();
            using (var view = new View(definition,false,true))
            {
                view.UseGameplayCamera(definition,210);
                view.camera.clearFlags = CameraClearFlags.Skybox;
                view.camera.gameObject.AddComponent<Skybox>().material = AssetDatabase.LoadAssetAtPath<Material>(AmberWindsIslandJobs.ContentPath + "/AmberWindsSky.mat");
                var sun = view.owner.GetComponentInChildren<Light>(); sun.color = new Color(1,.90f,.73f); sun.intensity = 1.65f;
                foreach (var material in view.clones) material.SetFloat(PreviewTime,2);
                var pairs = new List<SeamPair>();
                var joints = new[] {
                    (name:"lower",v:36.018428802490234f,stream:"Amber_DownstreamCreek_0",fall:"Amber_VoluminousFallingWater_0_0"),
                    (name:"R3",v:53.113525390625f,stream:"Amber_DownstreamCreek_1",fall:"Amber_VoluminousFallingWater_1_0"),
                    (name:"side",v:10.356194496154785f,stream:"RA_Amber_SideCascade_RaisedStreamAndRoll",fall:"RA_Amber_SideCascade_RaisedFall_Layer0") };
                foreach (var joint in joints) foreach (float u in new[] {0f,.25f,.5f,.75f,1f})
                {
                    var stream = FindImportedSeamVertex(view,"Stream",u,joint.v,null,joint.stream);
                    var fall = FindImportedSeamVertex(view,"Waterfall",u,joint.v,stream.position,joint.fall);
                    var pair = new SeamPair {joint = joint.name,u = stream.uv.x,requestedU = u,stream = stream,fall = fall,positionError = Vector3.Distance(stream.position,fall.position),
                        normalDot = Vector3.Dot(stream.normal,fall.normal),normalAngle = Vector3.Angle(stream.normal,fall.normal),
                        colorError = Mathf.Max(Mathf.Abs(stream.color.r-fall.color.r),Mathf.Max(Mathf.Abs(stream.color.g-fall.color.g),Mathf.Abs(stream.color.b-fall.color.b)))};
                    foreach (float time in new[] {0f,.1f,.25f,.5f,.75f,1f,1.5f,2f,3f,4.5f})
                        pair.maximumPredictedAnimatedGap = Mathf.Max(pair.maximumPredictedAnimatedGap,
                            Vector3.Distance(EvaluateFrozenVert(stream,time,0),EvaluateFrozenVert(fall,time,1)));
                    pairs.Add(pair);
                }
                report.seam = pairs.ToArray();
                view.Capture(folder + "/roll210-current-alpha.png");
                foreach (var material in view.clones.Where(material => material.name.Contains("Waterfall")))
                { material.SetFloat("_SrcBlend",1); material.SetFloat("_DstBlend",0); }
                view.Capture(folder + "/roll210-cloned-fall-opaque.png");
            }
            report.savedMaterialsUnchanged = before.SequenceEqual(saved.Select(material => EditorJsonUtility.ToJson(material))) && dirty.SequenceEqual(saved.Select(EditorUtility.IsDirty));
            File.WriteAllText(folder + "/comparison.json",JsonUtility.ToJson(report,true));
            if (!report.savedMaterialsUnchanged) throw new InvalidOperationException("The diagnostic must leave all saved material data and dirty flags unchanged.");
            Debug.Log("AMBER_ALPHA_SEAM_DIAGNOSIS READY: native imported endpoints and clone-only A/B; no real material/source writes.");
        }
        private static SeamVertex FindImportedSeamVertex(View view, string kind, float u, float v, Vector3? matchPosition, string nodeName)
        {
            float score = float.MaxValue; SeamVertex best = null;
            foreach (var renderer in view.waters.Where(renderer => renderer.name == nodeName && renderer.sharedMaterials.Any(material => material.name.Contains(kind))))
            {
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter.sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var colors = mesh.colors;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (Mathf.Abs(uv[i].y-v) > .003f || Mathf.Abs(uv[i].x-u) > .035f) continue;
                    Vector3 position = filter.transform.TransformPoint(vertices[i]);
                    float candidate = Mathf.Abs(uv[i].y-v)*100 + Mathf.Abs(uv[i].x-u)*10 + (matchPosition.HasValue ? Vector3.Distance(position,matchPosition.Value) : 0);
                    if (candidate >= score) continue;
                    score = candidate; best = new SeamVertex {sourceNode = renderer.name,position = position,uv = uv[i],
                        normal = filter.transform.worldToLocalMatrix.transpose.MultiplyVector(normals[i]).normalized,
                        color = colors.Length == vertices.Length ? colors[i] : Color.white};
                }
            }
            if (best == null) throw new InvalidOperationException("The imported seam endpoint is missing: " + kind + " U=" + u + " V=" + v);
            return best;
        }

        private static float HlslSmooth(float low, float high, float value)
        { float t = Mathf.Clamp01((value-low)/(high-low)); return t*t*(3-2*t); }
        private static Vector3 EvaluateFrozenVert(SeamVertex sample, float seconds, float fallFlag)
        {
            float time = seconds*.65f;
            Vector3 position = sample.position,normal = sample.normal;
            float wave = Mathf.Sin(position.x*.48f+position.z*.31f-time*1.5f)*.045f +
                Mathf.Sin(position.x*-.28f+position.z*.71f+time*1.1f)*.025f;
            float horizontal = HlslSmooth(.45f,.9f,Mathf.Abs(normal.y))*(1-fallFlag);
            position.y += wave*horizontal;
            float falling = Mathf.Max(fallFlag,1-HlslSmooth(.35f,.85f,Mathf.Abs(normal.y)));
            position += normal*Mathf.Sin(position.y*.8f+time*2.1f+sample.uv.x*5)*.065f*falling;
            return position;
        }

        [MenuItem("Tools/Radio Archipelago/Verify native water clock")]
        public static void StartLiveProof() => AmberWaterfallVisualReview.BeginIsland(CoralOasisIslandJobs.DefinitionPath,"coral");

        [MenuItem("Tools/Radio Archipelago/Verify Amber source-scenery water clock")]
        public static void StartAmberSceneryProof() => AmberWaterfallVisualReview.Begin("live");

        private static void BeginLiveProof(bool amber)
        {
            IslandReliefJobs.RequireEditMode();
            if (SceneManager.GetActiveScene().isDirty || SessionState.GetBool(ClockKey,false))
                throw new InvalidOperationException("Native water proof requires a clean current scene and no active water review.");
            SessionState.SetBool(AmberKey,amber); SessionState.SetInt(PhaseKey,0);
            SessionState.SetString(RunKey,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
            Directory.CreateDirectory(LiveFolder); SessionState.SetString(SceneKey,SceneManager.GetActiveScene().path);
            if (amber) File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new AmberLiveReport { sourceSceneryOnly = true,features = Array.Empty<LiveReport>() },true));
            SessionState.SetBool(ClockKey,true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ClockKey,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { started = EditorApplication.timeSinceStartup; firstLive = null; liveFrame = 0; }
            if (state == PlayModeStateChange.ExitingPlayMode && live != null) { live.Dispose(); live = null; firstLive = null; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                string folder = LiveFolder; bool amber = AmberLive;
                SessionState.SetBool(ClockKey,false);
                string previous = SessionState.GetString(SceneKey,"");
                if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous,OpenSceneMode.Single);
                string path = folder + "/live-clock.json";
                if (File.Exists(path))
                {
                    if (amber)
                    {
                        var report = JsonUtility.FromJson<AmberLiveReport>(File.ReadAllText(path));
                        report.originalSceneRestored = true; File.WriteAllText(path,JsonUtility.ToJson(report,true));
                    }
                    else
                    {
                        var report = JsonUtility.FromJson<LiveReport>(File.ReadAllText(path));
                        report.originalSceneRestored = true; File.WriteAllText(path,JsonUtility.ToJson(report,true));
                    }
                }
                SessionState.EraseBool(AmberKey); SessionState.EraseInt(PhaseKey); SessionState.EraseString(SceneKey); SessionState.EraseString(RunKey);
            }
        }
        [Serializable] private sealed class CascadeVisibility
        {
            public float station;
            public int visibleWaterPixels;
            public int[] topMidBaseVisiblePixels;
            public Vector3[] projectedTopMidBase;
            public bool allTargetsInsidePortrait;
        }
        [Serializable] private sealed class CascadeVisibilityReport
        {
            public string scope = "Explicit RA_Amber_SideCascade_ waterfall nodes. Actual fixed route camera at155/170/185m; full scenery remains enabled and the category-specific visible-water mask includes occlusion. Projected landmarks use artist-provided actual Unity top/mid/base, not water-only renders.";
            public CascadeVisibility[] stations;
        }
        private static void CaptureSideCascadeVisibility(IslandStageDefinition definition, View view)
        {
            float previous = view.selectedStation;
            Renderer[] measuredWater = view.waters;
            var proofs = new List<CascadeVisibility>();
            try
            {
                // Include the explicit spring/roll/curtain together for endpoint
                // visibility; temporal fall measurements retain their own target subset.
                view.waters = view.owner.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name.StartsWith(SideCascadePrefix,StringComparison.Ordinal) &&
                        renderer.sharedMaterials.Any(material => material && material.shader && material.shader.name == ShaderName)).ToArray();
                foreach (float station in new[] {155f,170f,185f})
                {
                    view.UseGameplayCamera(definition,station);
                    view.Capture(LiveFolder + "/side-cascade-route-" + station.ToString("000",System.Globalization.CultureInfo.InvariantCulture) + ".png");
                    var mask = view.VisibleWaterMask();
                    var projected = SideCascadeTargets.Select(point => view.camera.WorldToViewportPoint(point)).ToArray();
                    proofs.Add(new CascadeVisibility {station = station,visibleWaterPixels = mask.Count(pixel => pixel.r > 0),
                        projectedTopMidBase = projected,topMidBaseVisiblePixels = projected.Select(point => VisibleNear(mask,view.width,view.height,point)).ToArray(),
                        allTargetsInsidePortrait = projected.All(point => point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1)});
                }
            }
            finally { view.waters = measuredWater; view.UseGameplayCamera(definition,previous); }
            File.WriteAllText(LiveFolder + "/side-cascade-visibility.json",JsonUtility.ToJson(new CascadeVisibilityReport {stations = proofs.ToArray()},true));
            if (proofs.Any(proof => proof.visibleWaterPixels < 500 || !proof.allTargetsInsidePortrait || proof.topMidBaseVisiblePixels.Any(count => count < 5)))
                throw new InvalidOperationException("The explicit side waterfall top, middle and base must be visible through full scenery from all three representative normal route cameras.");
        }
        private static int VisibleNear(Color32[] mask, int width, int height, Vector3 viewport)
        {
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return 0;
            int x = Mathf.RoundToInt(viewport.x*(width-1)), y = Mathf.RoundToInt(viewport.y*(height-1)), count = 0;
            for (int dy = -10; dy <= 10; dy++) for (int dx = -10; dx <= 10; dx++)
            {
                int px = x+dx,py = y+dy;
                if (dx*dx+dy*dy <= 100 && px >= 0 && px < width && py >= 0 && py < height && mask[py*width+px].r > 0) count++;
            }
            return count;
        }

        [Serializable] private sealed class DirectionSample
        {
            public string label,sourceNode;
            public float x,y,tangentX,tangentY,expectedPixelsPerSecond,actualDownSlopeDegrees,wantedSurfaceDistance;
            public Vector3 worldPosition,worldDirection;
        }
        private static DirectionSample[] DirectionSamples(View view, string tag)
        {
            var targets = new List<(string label,Vector3 point,float v)>();
            if (tag == "roll210" || tag == "side-roll")
            {
                float radius = tag == "roll210" ? 3 : 1.5f;
                float joinV = tag == "roll210" ? 53.113525390625f : 10.356194496154785f;
                foreach (float degrees in new[] {35f,48f,65f})
                {
                    float angle = degrees*Mathf.Deg2Rad;
                    // The native imported physical V axis locates the real curved
                    // sheet; do not approximate a diagonal creek roll as world+X.
                    targets.Add(("roll-" + degrees.ToString("00",System.Globalization.CultureInfo.InvariantCulture) + "deg",
                        Vector3.zero,joinV-radius*(Mathf.PI*.5f-angle)));
                }
            }
            else if (tag == "waterfall")
            {
                targets.Add(("curtain-upper",new Vector3(48.6f,24,-96),float.NaN));
                targets.Add(("curtain-middle",new Vector3(49,20,-96),float.NaN));
                targets.Add(("curtain-lower",new Vector3(49,15,-96),float.NaN));
            }
            else targets.Add(("stream-centre",view.centre,float.NaN));
            return targets.Select(target => ImportedFlowSample(view,target.label,target.point,target.v)).ToArray();
        }
        private static DirectionSample ImportedFlowSample(View view, string label, Vector3 wanted, float wantedV)
        {
            float best = float.MaxValue; Vector3 position = Vector3.zero,direction = Vector3.zero; string node = "";
            foreach (var renderer in view.waters)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var mesh = filter.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles; var uv = mesh.uv;
                if (uv.Length != vertices.Length) continue;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int ia = triangles[i],ib = triangles[i+1],ic = triangles[i+2];
                    Vector3 a = filter.transform.TransformPoint(vertices[ia]),b = filter.transform.TransformPoint(vertices[ib]),c = filter.transform.TransformPoint(vertices[ic]);
                    Vector3 centre = (a+b+c)/3;
                    Vector2 centreUv = (uv[ia]+uv[ib]+uv[ic])/3;
                    float distance = float.IsNaN(wantedV) ? (centre-wanted).sqrMagnitude :
                        (centreUv.x-.5f)*(centreUv.x-.5f)*36+(centreUv.y-wantedV)*(centreUv.y-wantedV);
                    if (distance >= best) continue;
                    Vector2 ab = uv[ib]-uv[ia],ac = uv[ic]-uv[ia]; float determinant = ab.x*ac.y-ab.y*ac.x;
                    if (Mathf.Abs(determinant) < .000001f) continue;
                    Vector3 along = ((c-a)*ab.x-(b-a)*ac.x)/determinant;
                    if (along.sqrMagnitude < .000001f) continue;
                    best = distance; position = centre; direction = along.normalized; node = renderer.name;
                }
            }
            if (best == float.MaxValue) throw new InvalidOperationException("The native water direction sample lacks imported metric UV triangles: " + label);
            float slope = Mathf.Asin(Mathf.Clamp(-direction.y,-1,1))*Mathf.Rad2Deg;
            if (label.StartsWith("roll-",StringComparison.Ordinal))
            {
                float wantedSlope = float.Parse(label.Substring(5,2),System.Globalization.CultureInfo.InvariantCulture);
                if (Mathf.Sqrt(best) > 1 || Mathf.Abs(slope-wantedSlope) > 8)
                    throw new InvalidOperationException("The imported roll sample does not resolve to its requested physical slope: " + label + " / " + slope);
            }
            if (label.StartsWith("curtain-",StringComparison.Ordinal) && direction.y > -.95f)
                throw new InvalidOperationException("The imported curtain UV must advance physically downward: " + label);
            Vector3 point = view.camera.WorldToViewportPoint(position);
            Vector3 before = view.camera.WorldToViewportPoint(position-direction*.1f),after = view.camera.WorldToViewportPoint(position+direction*.1f);
            Vector2 tangent = new Vector2((after.x-before.x)*(view.width-1),-(after.y-before.y)*(view.height-1))/.2f;
            float speed = view.waters.SelectMany(renderer => renderer.sharedMaterials).First().GetFloat("_CurrentSpeed") *
                view.waters.SelectMany(renderer => renderer.sharedMaterials).First().GetFloat("_FlowSpeed");
            return new DirectionSample {label = label,sourceNode = node,x = point.x*(view.width-1),y = (1-point.y)*(view.height-1),
                tangentX = tangent.normalized.x,tangentY = tangent.normalized.y,expectedPixelsPerSecond = tangent.magnitude*speed,
                worldPosition = position,worldDirection = direction,actualDownSlopeDegrees = slope,wantedSurfaceDistance = Mathf.Sqrt(best)};
        }
        private static void SaveVisibleMask(View view, Color32[] mask, string path)
        {
            var texture = new Texture2D(view.width,view.height,TextureFormat.RGBA32,false);
            try { texture.SetPixels32(mask); texture.Apply(false,false); File.WriteAllBytes(path,texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ClockKey,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > (AmberLive ? 60 : 30)) throw new TimeoutException("Native water clock proof timed out.");
                if (Time.timeSinceLevelLoad < .25f) return;
                if (live == null)
                {
                    IslandStageDefinition definition = AmberLive ? AmberWindsIslandJobs.CreateSceneryDefinition() :
                        AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(CoralOasisIslandJobs.DefinitionPath);
                    try
                    {
                        if (AmberLive && (definition.Chart != null || !string.IsNullOrEmpty(definition.SongId)))
                            throw new InvalidOperationException("Amber water art proof must not bind a song or gameplay chart.");
                        int phase = SessionState.GetInt(PhaseKey,0);
                        bool fall = AmberLive && phase == AmberPhaseTags.Length-1;
                        live = new View(definition,fall,false,AmberLive ? (fall ? "Waterfall" : "Stream") : null,
                            AmberLive && phase >= 2 ? SideCascadePrefix : null);
                        if (AmberLive && phase == 1) live.UseGameplayCamera(definition,210);
                        else live.UseGameplayCamera(definition);
                        if (AmberLive && fall) CaptureSideCascadeVisibility(definition,live);
                        if (live.waters.SelectMany(r => r.sharedMaterials).Any(m => m.GetFloat(PreviewTime) >= 0))
                            throw new InvalidOperationException("A saved material overrides the native water clock.");
                        if (AmberLive)
                            foreach (var material in live.waters.SelectMany(r => r.sharedMaterials).Distinct())
                            {
                                if (Mathf.Abs(material.GetFloat("_UseFlowUV")-1) > .001f || Mathf.Abs(material.GetFloat("_CurrentSpeed")-4.2f) > .001f ||
                                    Mathf.Abs(material.GetFloat("_FlowSpeed")-.65f) > .001f || (fall && material.GetFloat("_Fall") < .5f))
                                    throw new InvalidOperationException("Amber native stream/fall flow configuration differs from its source UV contract.");
                            }
                    }
                    finally { if (AmberLive) Object.DestroyImmediate(definition); }
                    foreach (string stale in Directory.GetFiles(LiveFolder,LiveTag + "-flow-*.png",SearchOption.TopDirectoryOnly)) File.Delete(stale);
                    LiveFrameTimes.Clear();
                    firstLive = live.Capture(LiveFolder + "/" + LiveTag + "-clock-early.png"); firstLiveTime = Time.time;
                }
                if (Time.time-firstLiveTime >= liveFrame*.10f && liveFrame < ExpectedLiveFrames)
                {
                    live.Capture(LiveFolder + "/" + LiveTag + "-flow-" + liveFrame.ToString("00") + ".png"); LiveFrameTimes.Add(Time.time); liveFrame++;
                }
                if (liveFrame < ExpectedLiveFrames || Time.time - firstLiveTime < 1.3f) return;
                var last = live.Capture(LiveFolder + "/" + LiveTag + "-clock-late.png"); var mask = live.VisibleWaterMask();
                Changed(firstLive,last,mask,out int moving,out int covered);
                var report = new LiveReport { pass = moving > 100 && covered > 500 && liveFrame == ExpectedLiveFrames && LiveFrameTimes.Count == ExpectedLiveFrames,
                    state = "complete", feature = LiveTag,runId = SessionState.GetString(RunKey,""),frames = liveFrame,
                    firstTime = firstLiveTime, lastTime = Time.time, cameraStation = live.selectedStation, movingPixels = moving, coveredPixels = covered, defaultClock = true,
                    scope = "Real native Play Mode _Time, untouched saved materials, no preview override. Real route camera rig/FOV in portrait; isolated scenery-only scene, no game/profile/audio controllers. Visible-water mask measures only the selected water category and excludes terrain/foliage occlusion.",
                    frameTimes = LiveFrameTimes.ToArray(),flowSamples = AmberLive ? DirectionSamples(live,LiveTag) : Array.Empty<DirectionSample>(),
                    materialProperties = live.waters.SelectMany(r => r.sharedMaterials).Distinct().Select(m => new FlowProperties {
                        material = m.name,useFlowUv = m.GetFloat("_UseFlowUV"),currentSpeed = m.GetFloat("_CurrentSpeed"),
                        flowSpeed = m.GetFloat("_FlowSpeed"),fall = m.GetFloat("_Fall"),previewTime = m.GetFloat(PreviewTime) }).ToArray() };
                if (AmberLive) SaveVisibleMask(live,mask,LiveFolder + "/" + LiveTag + "-visible-mask.png");
                File.WriteAllText(LiveFolder + "/" + (AmberLive ? LiveTag + "-clock.json" : "live-clock.json"),JsonUtility.ToJson(report,true));
                live.Dispose(); live = null; firstLive = null;
                if (AmberLive && SessionState.GetInt(PhaseKey,0) < AmberPhaseTags.Length-1)
                {
                    SessionState.SetInt(PhaseKey,SessionState.GetInt(PhaseKey,0)+1); liveFrame = 0;
                    return;
                }
                if (AmberLive)
                {
                    var features = AmberPhaseTags.Select(tag => JsonUtility.FromJson<LiveReport>(File.ReadAllText(LiveFolder + "/" + tag + "-clock.json"))).ToArray();
                    File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new AmberLiveReport {
                        pass = features.All(feature => feature.pass && feature.frames == ExpectedLiveFrames && feature.runId == SessionState.GetString(RunKey,"")),sourceSceneryOnly = true,features = features },true));
                }
                EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                if (live != null) { live.Dispose(); live = null; }
                if (AmberLive)
                    File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new AmberLiveReport { sourceSceneryOnly = true,
                        features = new[] { new LiveReport { state = "failed",feature = LiveTag,scope = error.ToString() } } },true));
                else File.WriteAllText(LiveFolder + "/live-clock.json",JsonUtility.ToJson(new LiveReport { state = "failed", scope = error.ToString() },true));
                EditorApplication.ExitPlaymode(); Debug.LogException(error);
            }
        }
    }
}
