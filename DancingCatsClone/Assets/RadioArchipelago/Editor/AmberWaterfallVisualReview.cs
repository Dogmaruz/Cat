using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Full-scenery, native-clock visual evidence at the unchanged gameplay camera.
    /// A changing-pixel count is deliberately not an artistic acceptance criterion:
    /// inspect the resulting time-stamped sequence for a continuous lip and readable fall.
    /// This isolated scene contains no actor, chart, music or profile owner.
    /// </summary>
    [InitializeOnLoad]
    public static class AmberWaterfallVisualReview
    {
        private const string Key = "RadioArchipelago.AmberWaterfallVisual";
        private const int Width = 540, Height = 960, FrameCount = 24;
        private static float[] stations;
        private static string[] targetNodes;
        private static int[] selectedCoverage;
        private static CameraPose[] artPoses;
        private static List<SolidSurface> solidSurfaces;
        private struct CameraPose { public Vector3 position; public Quaternion rotation; }
        private sealed class SolidSurface { public Bounds bounds; public Vector3[] points,normals; public int[] indices; }
        private static GameObject owner;
        private static Camera camera;
        private static RenderTexture target;
        private static Texture2D pixels;
        private static IslandStageDefinition definition;
        private static IslandRoute route;
        private static Renderer[] water;
        private static Material[] materials;
        private static string[] materialBefore;
        private static Evidence report;
        private static int viewIndex, frameIndex, warmupFrames;
        private static float firstTime, lastTime;
        private static double started;

        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class StationDocument { public float[] values; }
        [Serializable] private sealed class Lighting
        {
            public string sky;
            public AmbientMode ambientMode;
            public Color skyColor,equatorColor,groundColor,ambientLight,fogColor,sunColor;
            public bool fog;
            public float fogDensity,sunIntensity;
            public FogMode fogMode;
            public Vector3 sunEuler;
        }
        [Serializable] private sealed class Frame
        {
            public string file;
            public float nativeTime;
            public int unityFrame;
            public double editorTime;
        }
        [Serializable] private sealed class ViewEvidence
        {
            public string targetNode;
            public int selectedVisibleWaterPixels;
            public bool actualRouteCamera = true;
            public float station;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float fieldOfView;
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Evidence
        {
            public string state,runId,label,unityVersion,error,originalScene,prefabDependencyHash;
            public string scope = "Actual native Play Mode clock; complete imported scenery, published route/elevation and unchanged fixed portrait gameplay camera. No actor/UI, song/chart/profile initialization, material clones, shader time overrides or temporal interpolation. Human visual review required; completion alone is not visual acceptance.";
            public bool nativeClock,savedMaterialsUnchanged,originalSceneRestored,shaderErrors;
            public int width = Width,height = Height;
            public int uncapturedRendererWarmupFramesPerView = 2;
            public List<ViewEvidence> views = new List<ViewEvidence>();
            public string[] shaders;
        }
        private static string Folder => SessionState.GetString(Key + ".folder","");

        static AmberWaterfallVisualReview()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StateChanged;
        }

        public static void Begin(string label = "live") => BeginDefinition(AmberWindsIslandJobs.DefinitionPath,label,new[] {185f,210f});

        public static void BeginIsland(string definitionPath,string label) => BeginDefinition(definitionPath,label,null);

        public static void BeginIslandArt(string definitionPath,string label) => BeginDefinition(definitionPath,label,null,true);

        public static void BeginIslandLakes(string definitionPath,string label) => BeginDefinition(definitionPath,label,null,true,true);

        public static void BeginReference(string definitionPath,string label,string referenceReport) => BeginDefinition(definitionPath,label,null,true,true,referenceReport);

        [MenuItem("Tools/Radio Archipelago/Preview joined Coral lakes and falls")]
        public static void CoralArt() => BeginIslandArt(CoralOasisIslandJobs.DefinitionPath,"coral-art");
        [MenuItem("Tools/Radio Archipelago/Preview joined Moon lakes and falls")]
        public static void MoonArt() => BeginIslandArt(MoonResonanceIslandJobs.DefinitionPath,"moon-art");
        [MenuItem("Tools/Radio Archipelago/Preview joined Amber lakes and falls")]
        public static void AmberArt() => BeginIslandArt(AmberWindsIslandJobs.DefinitionPath,"amber-art");

        private static void BeginDefinition(string definitionPath,string label,float[] fixedStations,bool art = false,bool lakes = false,string referenceReport = null)
        {
            IslandReliefJobs.RequireEditMode();
            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty || string.IsNullOrEmpty(scene.path) || SessionState.GetBool(Key,false))
                throw new InvalidOperationException("Waterfall visual review needs one saved clean scene and no active review.");
            if (EditorSceneManager.sceneCount != 1)
                throw new InvalidOperationException("Preserve additional open scenes before starting this isolated review.");
            string runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            string folder = "Library/RadioArchipelago/WaterfallVisual/" + label + "-" + runId;
            Directory.CreateDirectory(folder);
            var sun = RenderSettings.sun ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            var lighting = new Lighting {sky = AssetDatabase.GetAssetPath(RenderSettings.skybox),ambientMode = RenderSettings.ambientMode,
                skyColor = RenderSettings.ambientSkyColor,equatorColor = RenderSettings.ambientEquatorColor,groundColor = RenderSettings.ambientGroundColor,
                ambientLight = RenderSettings.ambientLight,fog = RenderSettings.fog,fogColor = RenderSettings.fogColor,fogDensity = RenderSettings.fogDensity,fogMode = RenderSettings.fogMode,
                sunColor = sun ? sun.color : new Color(1,.90f,.73f),sunIntensity = sun ? sun.intensity : 1.65f,sunEuler = sun ? sun.transform.eulerAngles : new Vector3(38,-34,0)};
            SessionState.SetString(Key + ".scene",scene.path);
            SessionState.SetString(Key + ".folder",folder);
            SessionState.SetString(Key + ".lighting",JsonUtility.ToJson(lighting));
            SessionState.SetString(Key + ".definition",definitionPath);
            SessionState.SetString(Key + ".stations",JsonUtility.ToJson(new StationDocument {values=fixedStations}));
            SessionState.SetBool(Key + ".asyncCompilation",ShaderUtil.allowAsyncCompilation);
            SessionState.SetBool(Key+".art",art);
            SessionState.SetBool(Key+".lakes",lakes);
            SessionState.SetString(Key+".reference",referenceReport ?? "");
            SessionState.SetBool(Key,true);
            var pending = new Evidence {state = "starting",runId = runId,label = label,unityVersion = Application.unityVersion,originalScene = scene.path};
            File.WriteAllText(folder + "/report.json",JsonUtility.ToJson(pending,true));
            File.WriteAllText("Library/RadioArchipelago/WaterfallVisual/latest.json",JsonUtility.ToJson(new Location {folder = folder},true));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        [Serializable] private sealed class Location { public string folder; }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                report = JsonUtility.FromJson<Evidence>(File.ReadAllText(Folder + "/report.json"));
                viewIndex = frameIndex = 0; firstTime = lastTime = -1;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (report != null && report.state != "complete" && report.state != "failed")
                { report.state = "failed"; report.error = "Play Mode was interrupted before all native frames were captured."; Save(); }
                Dispose();
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            string folder = Folder;
            string previous = SessionState.GetString(Key + ".scene","");
            SessionState.SetBool(Key,false);
            ShaderUtil.allowAsyncCompilation=SessionState.GetBool(Key+".asyncCompilation",true);
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous,OpenSceneMode.Single);
            var restored = JsonUtility.FromJson<Evidence>(File.ReadAllText(folder + "/report.json"));
            restored.originalSceneRestored = SceneManager.GetActiveScene().path == previous && !SceneManager.GetActiveScene().isDirty;
            File.WriteAllText(folder + "/report.json",JsonUtility.ToJson(restored,true));
            SessionState.EraseString(Key + ".lighting"); SessionState.EraseString(Key + ".scene"); SessionState.EraseString(Key + ".folder");
            SessionState.EraseString(Key + ".definition"); SessionState.EraseString(Key + ".stations"); SessionState.EraseBool(Key+".asyncCompilation");
            SessionState.EraseBool(Key+".art");
            SessionState.EraseBool(Key+".lakes");
            SessionState.EraseString(Key+".reference");
        }

        private static void Initialize()
        {
            definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(SessionState.GetString(Key+".definition",AmberWindsIslandJobs.DefinitionPath));
            if (!definition || !definition.Prefab || !definition.RouteJson || !definition.ElevationJson)
                throw new InvalidOperationException("Published Amber prefab, route and conditioned height profile are required.");
            route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,IslandElevationProfile.FromJson(definition.ElevationJson.text));
            owner = new GameObject("Isolated native waterfall visual review");
            var scenery = Object.Instantiate(definition.Prefab,owner.transform);
            foreach (var t in scenery.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 29;
            water = scenery.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Any(IsWater)).ToArray();
            materials = water.SelectMany(r => r.sharedMaterials).Where(IsWater).Distinct().ToArray();
            if (materials.Length == 0) throw new InvalidOperationException("The actual prefab has no supported flowing water.");
            report.shaderErrors = materials.Any(m => ShaderUtil.ShaderHasError(m.shader));
            if (report.shaderErrors) throw new InvalidOperationException("The actual water shader has compilation errors.");
            if (materials.Any(m => m.HasProperty("_WaterPreviewTime") && m.GetFloat("_WaterPreviewTime") >= 0))
                throw new InvalidOperationException("A real saved water material overrides the native clock.");
            materialBefore = materials.Select(material => EditorJsonUtility.ToJson(material)).ToArray();
            // Native visual evidence must wait for the real pass, including a
            // cold shader variant. Restore this Editor-only preference on exit.
            ShaderUtil.allowAsyncCompilation=false;
            report.shaders = materials.Select(m => m.shader.name).Distinct().ToArray();
            report.nativeClock = true;
            report.prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(definition.Prefab)).ToString();
            var lighting = JsonUtility.FromJson<Lighting>(SessionState.GetString(Key + ".lighting",""));
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(lighting.sky);
            RenderSettings.ambientMode = lighting.ambientMode; RenderSettings.ambientSkyColor = lighting.skyColor;
            RenderSettings.ambientEquatorColor = lighting.equatorColor; RenderSettings.ambientGroundColor = lighting.groundColor;
            RenderSettings.ambientLight = lighting.ambientLight; RenderSettings.fog = lighting.fog; RenderSettings.fogColor = lighting.fogColor;
            RenderSettings.fogDensity = lighting.fogDensity; RenderSettings.fogMode = lighting.fogMode;
            var light = new GameObject("Copied island daylight").AddComponent<Light>(); light.transform.SetParent(owner.transform,false);
            light.type = LightType.Directional; light.color = lighting.sunColor; light.intensity = lighting.sunIntensity;
            light.transform.rotation = Quaternion.Euler(lighting.sunEuler); light.shadows = LightShadows.Soft; light.cullingMask = 1 << 29;
            RenderSettings.sun = light;
            camera = new GameObject("Unchanged route portrait camera").AddComponent<Camera>(); camera.transform.SetParent(owner.transform,false);
            camera.enabled = false; camera.clearFlags = CameraClearFlags.Skybox; camera.backgroundColor = definition.SkyColor;
            camera.nearClipPlane = .08f; camera.farClipPlane = 300; camera.aspect = (float)Width/Height; camera.fieldOfView = definition.CameraFieldOfView; camera.cullingMask = 1 << 29;
            var data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.allowXRRendering = false;
            target = new RenderTexture(Width,Height,24); target.Create();
            pixels = new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            stations=JsonUtility.FromJson<StationDocument>(SessionState.GetString(Key+".stations","{}")).values;
            targetNodes=null;
            selectedCoverage=null;
            artPoses=null;
            if (SessionState.GetBool(Key+".art",false))
            {
                bool lakes=SessionState.GetBool(Key+".lakes",false);
                report.scope="Actual native Play Mode clock; full imported scenery and saved materials. Explicit "+(lakes ? "elevated lake/outlet" : "exterior falling-body")+" art cameras, not normal gameplay framing; route/profile/music unchanged. No shader time override or interpolated frames.";
                string reference=SessionState.GetString(Key+".reference","");
                if (string.IsNullOrEmpty(reference)) SelectArtViews(lakes);
                else
                {
                    var previous=JsonUtility.FromJson<Evidence>(File.ReadAllText(reference));
                    if(previous.state!="complete" || !previous.originalSceneRestored || previous.views.Count==0)
                        throw new InvalidOperationException("Reference views must come from a completed restored native capture.");
                    foreach(var v in previous.views)
                        if(!water.Any(r=>r.name==v.targetNode)) throw new InvalidOperationException("Reference water node changed: "+v.targetNode);
                    artPoses=previous.views.Select(v=>new CameraPose {position=v.cameraPosition,rotation=v.cameraRotation}).ToArray();
                    stations=previous.views.Select(v=>v.station).ToArray();
                    targetNodes=previous.views.Select(v=>v.targetNode).ToArray();
                    selectedCoverage=null;
                    camera.fieldOfView=previous.views[0].fieldOfView;
                    report.scope="Actual native Play Mode clock; full imported scenery and saved materials. Exact reference camera poses/FOV replayed from "+reference+". Art views, not gameplay framing. No shader time override or interpolated frames.";
                }
            }
            else if (stations==null || stations.Length==0) SelectWaterfallStations();
            StartView();
        }

        private static bool IsWater(Material material) => material && material.shader &&
            (material.shader.name == "RadioArchipelago/Coral Water" || material.shader.name == "RadioArchipelago/Amber Flow Water");

        private static void StartView()
        {
            if (artPoses!=null) camera.transform.SetPositionAndRotation(artPoses[viewIndex].position,artPoses[viewIndex].rotation);
            else
            {
                var pose = new IslandCloseCameraRig().Step(route,stations[viewIndex],0,definition.CameraBackDistance,definition.CameraHeight,
                    definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,definition.CameraSmoothing,0,true);
                camera.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
            }
            report.views.Add(new ViewEvidence {station = stations[viewIndex],actualRouteCamera=artPoses==null,targetNode=targetNodes==null ? "" : targetNodes[viewIndex],selectedVisibleWaterPixels=selectedCoverage==null ? 0 : selectedCoverage[viewIndex],cameraPosition = camera.transform.position,cameraRotation = camera.transform.rotation,fieldOfView = camera.fieldOfView});
            frameIndex = 0; firstTime = lastTime = -1;
            warmupFrames = 2;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException("Native waterfall visual capture timed out.");
                if (Time.timeSinceLevelLoad < .4f) return;
                if (!owner) Initialize();
                if (lastTime >= 0 && Time.time - lastTime < .10f) return;
                float now = Time.time;
                if (firstTime < 0) firstTime = now;
                string name = "view-"+viewIndex+"-station-" + stations[viewIndex].ToString("000",System.Globalization.CultureInfo.InvariantCulture) + "-" + frameIndex.ToString("000") + ".png";
                var previous = RenderTexture.active;
                try
                {
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination = target});
                    // A new material's first asynchronous Editor shader draw
                    // can be blank. Prime the actual renderer before evidence.
                    if (warmupFrames > 0) { warmupFrames--; lastTime = now; return; }
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,Width,Height),0,0); pixels.Apply(false,false);
                    File.WriteAllBytes(Folder + "/" + name,pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; }
                report.views[viewIndex].frames.Add(new Frame {file = name,nativeTime = now,unityFrame = Time.frameCount,editorTime = EditorApplication.timeSinceStartup});
                lastTime = now; frameIndex++;
                if (frameIndex < FrameCount) return;
                if (++viewIndex < stations.Length) { StartView(); return; }
                report.savedMaterialsUnchanged = materialBefore.SequenceEqual(materials.Select(material => EditorJsonUtility.ToJson(material)));
                if (!report.savedMaterialsUnchanged) throw new InvalidOperationException("Saved water material data changed during native visual capture.");
                if (report.views.Any(v => v.frames.Count != FrameCount || v.frames.Last().nativeTime - v.frames[0].nativeTime < 2.3f || v.frames.Select(f => f.unityFrame).Distinct().Count() != FrameCount))
                    throw new InvalidOperationException("Every view needs24 distinct native frames spanning at least2.3seconds.");
                report.state = "complete"; Save(); EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                if (report == null) report = JsonUtility.FromJson<Evidence>(File.ReadAllText(Folder + "/report.json"));
                report.state = "failed"; report.error = error.ToString(); Save();
                EditorApplication.ExitPlaymode(); Debug.LogException(error);
            }
        }

        private static void Save() => File.WriteAllText(Folder + "/report.json",JsonUtility.ToJson(report,true));

        private static void SelectWaterfallStations()
        {
            var falls=water.Where(r=>r.sharedMaterials.Any(m=>m && m.shader.name=="RadioArchipelago/Amber Flow Water")).OrderBy(r=>r.name).ToArray();
            if (falls.Length!=2) throw new InvalidOperationException("The migrated island must expose exactly two continuous waterfall renderers.");
            stations=new float[falls.Length]; targetNodes=new string[falls.Length]; selectedCoverage=new int[falls.Length];
            for (int index=0;index<falls.Length;index++)
            {
                var fall=falls[index]; var filter=fall.GetComponent<MeshFilter>(); var mesh=filter.sharedMesh;
                Vector3[] positions=mesh.vertices,normals=mesh.normals; bool hasBody=false; var bounds=new Bounds();
                for (int i=0;i<positions.Length;i++)
                {
                    if (Mathf.Abs(filter.transform.TransformDirection(normals[i]).normalized.y)>.35f) continue;
                    Vector3 p=filter.transform.TransformPoint(positions[i]);
                    if (!hasBody) {bounds=new Bounds(p,Vector3.zero);hasBody=true;} else bounds.Encapsulate(p);
                }
                if (!hasBody) throw new InvalidOperationException("No actual vertical waterfall surface: "+fall.name);
                Vector3 focus=bounds.center; focus.y=bounds.max.y-Mathf.Min(6,bounds.size.y*.2f);
                var candidates=new List<(float score,float station)>();
                for (float s=0;s<route.Length;s+=2)
                {
                    if (index>0 && stations.Take(index).Any(previous=>Mathf.Abs(previous-s)<8)) continue;
                    var pose=new IslandCloseCameraRig().Step(route,s,0,definition.CameraBackDistance,definition.CameraHeight,
                        definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,definition.CameraSmoothing,0,true);
                    camera.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
                    Vector3 p=camera.WorldToViewportPoint(focus);
                    if (p.z<8 || p.z>90 || p.x<.04f || p.x>.96f || p.y<.12f || p.y>.91f) continue;
                    candidates.Add((Mathf.Abs(p.x-.5f)*1.2f+Mathf.Abs(p.y-.55f)+p.z*.006f,s));
                }
                int coverage=0; float chosen=-1;
                foreach (var candidate in candidates.OrderBy(item=>item.score).Take(12))
                {
                    var pose=new IslandCloseCameraRig().Step(route,candidate.station,0,definition.CameraBackDistance,definition.CameraHeight,
                        definition.CameraLookAhead,definition.CameraFocusHeight,definition.CameraHeadingLead,definition.CameraSmoothing,0,true);
                    camera.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
                    Color32[] on=ReadFrame(); Color32[] off;
                    bool enabled=fall.enabled;
                    try { fall.enabled=false;off=ReadFrame(); } finally {fall.enabled=enabled;}
                    int visible=0;
                    for (int i=0;i<on.Length;i++)
                        if (Math.Abs(on[i].r-off[i].r)+Math.Abs(on[i].g-off[i].g)+Math.Abs(on[i].b-off[i].b)>20) visible++;
                    if (visible>coverage) {coverage=visible;chosen=candidate.station;}
                }
                if (chosen<0 || coverage<200) throw new InvalidOperationException("Waterfall is not visible through actual full scenery from the unchanged route camera: "+fall.name);
                stations[index]=chosen; targetNodes[index]=fall.name; selectedCoverage[index]=coverage;
            }
        }

        private static void SelectArtViews(bool lakes)
        {
            solidSurfaces=new List<SolidSurface>();
            foreach (var renderer in owner.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.sharedMaterials.Any(m=>m && (m.name.Contains("_Terrain") || m.name.Contains("_Cliffs") || m.name.Contains("_Rocks")))) continue;
                var f=renderer.GetComponent<MeshFilter>(); if(!f || !f.sharedMesh) continue;
                var meshData=f.sharedMesh;var normalMatrix=f.transform.localToWorldMatrix.inverse.transpose;
                solidSurfaces.Add(new SolidSurface {bounds=renderer.bounds,points=meshData.vertices.Select(f.transform.TransformPoint).ToArray(),
                    normals=meshData.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray(),indices=meshData.triangles});
            }
            var falls=water.Where(r=>r.sharedMaterials.Any(m=>m && m.shader.name=="RadioArchipelago/Amber Flow Water")).OrderBy(r=>r.name).ToArray();
            if (falls.Length<1) throw new InvalidOperationException("No continuous lake/fall surface for native art views.");
            artPoses=new CameraPose[falls.Length]; stations=new float[falls.Length];
            targetNodes=new string[falls.Length]; selectedCoverage=new int[falls.Length];
            for (int index=0;index<falls.Length;index++)
            {
                var fall=falls[index]; var filter=fall.GetComponent<MeshFilter>();
                var normals=filter.sharedMesh.normals; var points=filter.sharedMesh.vertices;
                Vector3 facing=Vector3.zero;
                for (int i=0;i<points.Length;i++)
                {
                    Vector3 normal=filter.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
                    if (Mathf.Abs(normal.y)<.25f) facing+=normal;
                }
                facing.y=0;
                if (facing.sqrMagnitude<.01f) facing=Vector3.forward;
                facing.Normalize();
                var bounds=fall.bounds; Vector3 focus=bounds.center+Vector3.up*bounds.size.y*.10f;
                float distance=Mathf.Max(18,bounds.size.y*.9f,Mathf.Max(bounds.size.x,bounds.size.z)*1.2f);
                int most=0; CameraPose selected=new CameraPose();
                // Rank the falling body, not the broad lake: a rear view of a
                // large pool can otherwise win while the cliff hides the fall.
                Mesh full=filter.sharedMesh;
                var body=new Mesh {name="Temporary actual waterfall triangle mask",indexFormat=full.indexFormat};
                body.vertices=full.vertices;body.normals=full.normals;body.colors=full.colors;body.uv=full.uv;body.uv2=full.uv2;
                var indices=new List<int>(); var all=full.triangles;
                for (int i=0;i<all.Length;i+=3)
                {
                    Vector3 a=filter.transform.TransformPoint(points[all[i]]),b=filter.transform.TransformPoint(points[all[i+1]]),c=filter.transform.TransformPoint(points[all[i+2]]);
                    Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                    if(lakes ? Mathf.Abs(n.y)<.55f : Mathf.Abs(n.y)>.55f) continue;
                    indices.Add(all[i]);indices.Add(all[i+1]);indices.Add(all[i+2]);
                }
                body.SetTriangles(indices,0);body.RecalculateBounds();
                if(lakes)
                {
                    bounds=new Bounds(filter.transform.TransformPoint(body.bounds.center),Vector3.zero);
                    foreach(int vertex in indices) bounds.Encapsulate(filter.transform.TransformPoint(points[vertex]));
                    focus=bounds.center;
                    distance=Mathf.Max(16,Mathf.Max(bounds.size.x,bounds.size.z)*1.3f);
                }
                try
                {
                    filter.sharedMesh=body;
                    foreach (float angle in new[] {0f,45f,-45f,90f,-90f,180f})
                    foreach (float eyeY in lakes ? new[] {bounds.max.y+distance*.9f,bounds.max.y+distance*1.3f} : new[] {bounds.max.y+Mathf.Max(8,bounds.size.y*.18f),focus.y+bounds.size.y*.12f})
                    {
                        Vector3 direction=Quaternion.Euler(0,angle,0)*facing;
                        Vector3 eye=new Vector3(focus.x,eyeY,focus.z)+direction*distance;
                        if (InsideSolid(eye)) continue;
                        camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));
                        Color32[] on=ReadFrame(),off;bool enabled=fall.enabled;
                        try {fall.enabled=false;off=ReadFrame();} finally {fall.enabled=enabled;}
                        int visible=0;
                        for (int p=0;p<on.Length;p++)
                            if (Math.Abs(on[p].r-off[p].r)+Math.Abs(on[p].g-off[p].g)+Math.Abs(on[p].b-off[p].b)>20) visible++;
                        if (visible>most) {most=visible;selected=new CameraPose {position=eye,rotation=camera.transform.rotation};}
                    }
                }
                finally {filter.sharedMesh=full;Object.DestroyImmediate(body);}
                if (most<200) throw new InvalidOperationException("No visible actual "+(lakes ? "lake/outlet" : "falling body")+" through full scenery: "+fall.name+" visiblePixels="+most);
                stations[index]=-index-1; targetNodes[index]=fall.name; selectedCoverage[index]=most; artPoses[index]=selected;
            }
        }

        private static bool InsideSolid(Vector3 eye)
        {
            var hits=new List<(float y,int sign)>();
            foreach(var surface in solidSurfaces)
            {
                Bounds b=surface.bounds;
                if(eye.x<b.min.x || eye.x>b.max.x || eye.z<b.min.z || eye.z>b.max.z || eye.y>b.max.y) continue;
                for(int i=0;i<surface.indices.Length;i+=3)
                {
                    int ia=surface.indices[i],ib=surface.indices[i+1],ic=surface.indices[i+2];
                    Vector3 a=surface.points[ia],p=surface.points[ib],c=surface.points[ic];
                    float determinant=(p.z-c.z)*(a.x-c.x)+(c.x-p.x)*(a.z-c.z);
                    if(Mathf.Abs(determinant)<1e-8f) continue;
                    float u=((p.z-c.z)*(eye.x-c.x)+(c.x-p.x)*(eye.z-c.z))/determinant;
                    float v=((c.z-a.z)*(eye.x-c.x)+(a.x-c.x)*(eye.z-c.z))/determinant;
                    if(u<-.000001f || v<-.000001f || u+v>1.000001f) continue;
                    float height=u*a.y+v*p.y+(1-u-v)*c.y;
                    if(height<=eye.y+.001f) continue;
                    float normalY=(surface.normals[ia]+surface.normals[ib]+surface.normals[ic]).y;
                    if(Mathf.Abs(normalY)<1e-5f) continue;
                    hits.Add((height,normalY>0 ? 1 : -1));
                }
            }
            int winding=0;
            var sorted=hits.OrderBy(h=>h.y).ToArray();
            for(int i=0;i<sorted.Length;)
            {
                float height=sorted[i].y;int sign=0;
                do {sign+=sorted[i++].sign;} while(i<sorted.Length && Mathf.Abs(sorted[i].y-height)<.001f);
                winding+=Math.Sign(sign);
            }
            return winding>0;
        }

        private static Color32[] ReadFrame()
        {
            var previous=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply(false,false);
                return pixels.GetPixels32();
            }
            finally {RenderTexture.active=previous;}
        }

        private static void Dispose()
        {
            if (target) { target.Release(); Object.DestroyImmediate(target); }
            if (pixels) Object.DestroyImmediate(pixels);
            if (owner) Object.DestroyImmediate(owner);
            ShaderUtil.allowAsyncCompilation=SessionState.GetBool(Key+".asyncCompilation",true);
            owner = null; camera = null; target = null; pixels = null; definition = null; route = null; water = null; materials = null; materialBefore = null;
            solidSurfaces=null;artPoses=null;
        }
    }
}
