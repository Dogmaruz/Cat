using System;
using System.Collections.Generic;
using System.IO;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RadioArchipelago.Editor
{
    /// <summary>Owns generated currency materials/prefab; never edits imported or original reward prefabs.</summary>
    public static class SignalSparkAuthoring
    {
        public const string Folder = "Assets/RadioArchipelago/Content/Collectibles/SignalSpark";
        public const string PrefabPath = Folder + "/SignalSpark.prefab";
        public const string ModelPath = "Assets/RadioArchipelago/Imported/Collectibles/SignalSpark/signal_spark.fbx";
        private const int PreviewLayer = 31;
        private const float Diameter = .98f;

        [Serializable] private sealed class Report
        {
            public bool passed, transparentGlass, emissiveCore, twoGoldCrescents, noLightsOrColliders;
            public bool warmFilament, localFilamentGlow, warmHalo;
            public string prefab, model;
            public int renderers, triangles, sharedMaterials, simulationSamples;
            public Vector3 size;
            public float minGroundClearance, maxGroundClearance;
        }

        [MenuItem("Radio Archipelago/Signal sparks/Create currency assets")]
        public static void CreateAssets() => EnsureAssets();

        public static SignalSparkVisual EnsureAssets()
        {
            Require(!EditorApplication.isPlaying, "Create signal currency assets in Edit Mode.");
            EnsureFolder(Folder);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Require(source != null, "The authored Signal Spark FBX has not been imported: " + ModelPath);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var glassShader = Shader.Find("RadioArchipelago/Signal Spark Glass");
            var glowShader = Shader.Find("RadioArchipelago/Signal Spark Glow");
            var filamentShader = Shader.Find("RadioArchipelago/Signal Spark Filament Aura");
            Require(lit != null && glassShader != null && glowShader != null && filamentShader != null, "Signal Spark URP shaders are missing.");
            var palette = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase)
            {
                // Keep source material IDs stable: these names belong to the
                // preserved FBX. Their current palette is warm tungsten light.
                { "GlassMint", MaterialAsset("GlassMint", glassShader, new Color(.98f,.88f,.66f,.022f), 0, .96f) },
                { "GoldBrass", MaterialAsset("GoldBrass", lit, new Color(.63f,.36f,.085f), .86f, .73f) },
                { "GoldHighlight", MaterialAsset("GoldHighlight", lit, new Color(.94f,.68f,.24f), .84f, .83f) },
                { "TealEnamel", MaterialAsset("TealEnamel", lit, new Color(.105f,.068f,.028f), .46f, .66f) },
                { "SignalMint", MaterialAsset("SignalMint", lit, new Color(1,.88f,.42f), 0, .38f, new Color(1,.62f,.105f)*3.4f) },
                { "CoreGold", MaterialAsset("CoreGold", lit, new Color(.98f,.66f,.20f), .35f, .65f, new Color(1,.43f,.025f)*.78f) },
                { "MoteMint", MaterialAsset("MoteMint", lit, new Color(1,.79f,.29f), .10f, .67f, new Color(1,.49f,.045f)*1.65f) },
                { "OrbitMint", MaterialAsset("OrbitMint", lit, new Color(.70f,.36f,.075f), .38f, .5f, new Color(1,.40f,.025f)*.55f) }
            };
            var glass = palette["GlassMint"];
            glass.SetColor("_RimColor", new Color(1,.80f,.40f));
            glass.SetFloat("_RimOpacity", .29f); EditorUtility.SetDirty(glass);
            var haloMaterial = MaterialAsset("LocalGlow", glowShader, new Color(1,.58f,.12f,.58f), 0, 0);
            haloMaterial.SetFloat("_Intensity", .95f); EditorUtility.SetDirty(haloMaterial);
            var filamentAura = MaterialAsset("FilamentAura", filamentShader, new Color(1,.48f,.065f,.40f), 0, 0);
            filamentAura.SetFloat("_GlowWidth", .012f); EditorUtility.SetDirty(filamentAura);
            Mesh haloMesh = HaloMesh();
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = Node(scene, "Signal Spark");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                model.name = "Authored glass and brass"; model.transform.SetParent(root.transform, false);
                // Blender's decorated -Y face exports toward Unity +Z. The
                // trailing gameplay camera views -Z; show the enamel/front there.
                model.transform.localRotation = Quaternion.Euler(0,180,0) * model.transform.localRotation;
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                Require(renderers.Length >= 6, "Signal Spark export is missing authored parts.");
                foreach (var renderer in renderers)
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        Require(slots[i] != null, "Imported spark has an unassigned material.");
                        string id = slots[i].name.Replace(" (Instance)", "");
                        if (!palette.TryGetValue(id, out var material)) throw new InvalidOperationException("Unknown Signal Spark material: " + id);
                        slots[i] = material;
                    }
                    renderer.sharedMaterials = slots;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                }
                Bounds bounds = Measure(renderers, root.transform);
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                Require(longest > .001f, "Signal Spark export has no visible volume.");
                model.transform.localScale *= Diameter / longest;
                bounds = Measure(renderers, root.transform);
                model.transform.localPosition -= bounds.center;
                bounds = Measure(renderers, root.transform);

                var halo = Node(scene, "Soft local glow"); halo.transform.SetParent(root.transform, false);
                halo.transform.localScale = Vector3.one * Diameter * 1.75f;
                halo.AddComponent<MeshFilter>().sharedMesh = haloMesh;
                var haloRenderer = halo.AddComponent<MeshRenderer>(); haloRenderer.sharedMaterial = haloMaterial;
                haloRenderer.shadowCastingMode = ShadowCastingMode.Off; haloRenderer.receiveShadows = false;
                haloRenderer.lightProbeUsage = LightProbeUsage.Off; haloRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var visual = root.AddComponent<SignalSparkVisual>();
                var spiral = Find(model.transform, "SignalSpiral");
                var core = Find(model.transform, "SignalCore");
                var orbit = Find(model.transform, "OrbitMotes");
                Require(spiral != null && core != null && orbit != null, "Signal Spark export lost its animation nodes.");
                // A small expanded copy softens the hot helix silhouette in the
                // object itself. This works with bloom/HDR disabled on phones.
                var filamentGlow = Node(scene, "Warm filament aura");
                filamentGlow.transform.SetParent(spiral, false);
                filamentGlow.AddComponent<MeshFilter>().sharedMesh = spiral.GetComponent<MeshFilter>().sharedMesh;
                var filamentGlowRenderer = filamentGlow.AddComponent<MeshRenderer>();
                filamentGlowRenderer.sharedMaterial = filamentAura;
                filamentGlowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                filamentGlowRenderer.receiveShadows = false;
                filamentGlowRenderer.lightProbeUsage = LightProbeUsage.Off;
                filamentGlowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                // Reserve the complete circular orbit, independent of the initial mote phase.
                Bounds orbitBounds = Measure(orbit.GetComponentsInChildren<Renderer>(true), root.transform);
                float orbitRadius = Mathf.Sqrt(Mathf.Pow(Mathf.Max(Mathf.Abs(orbitBounds.min.x), Mathf.Abs(orbitBounds.max.x)), 2) +
                    Mathf.Pow(Mathf.Max(Mathf.Abs(orbitBounds.min.z), Mathf.Abs(orbitBounds.max.z)), 2));
                bounds.Encapsulate(new Vector3(-orbitRadius, orbitBounds.min.y, -orbitRadius));
                bounds.Encapsulate(new Vector3(orbitRadius, orbitBounds.max.y, orbitRadius));
                visual.Configure(spiral, core, orbit, halo.transform, haloRenderer, renderers, bounds);
                foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 0;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return LoadPrefab();
            }
            finally { if (root != null) Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        [MenuItem("Radio Archipelago/Signal sparks/Validate currency assets")]
        public static void RunChecks()
        {
            var prefab = LoadPrefab();
            Require(prefab != null, "Create the Signal Spark prefab before validation.");
            var report = new Report { prefab = PrefabPath, model = ModelPath, minGroundClearance = float.PositiveInfinity };
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            try
            {
                instance = Object.Instantiate(prefab.gameObject); SceneManager.MoveGameObjectToScene(instance, scene);
                var visual = instance.GetComponent<SignalSparkVisual>();
                var unique = new HashSet<Material>();
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Require(material != null && material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), "Invalid Signal Spark shader/material.");
                        unique.Add(material);
                    }
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                    for (int part = 0; part < filter.sharedMesh.subMeshCount; part++)
                        report.triangles += (int)(filter.sharedMesh.GetIndexCount(part) / 3);
                report.renderers = renderers.Length; report.sharedMaterials = unique.Count;
                report.size = visual.LocalVisualBounds.size;
                report.transparentGlass = Find(instance.transform, "GlassShell")?.GetComponent<Renderer>()?.sharedMaterial.shader.name == "RadioArchipelago/Signal Spark Glass";
                var spiral = Find(instance.transform, "SignalSpiral");
                report.emissiveCore = spiral != null && spiral.GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("_EMISSION");
                var gold = Find(instance.transform, "GoldCrescents");
                report.twoGoldCrescents = gold != null && gold.GetComponent<MeshFilter>().sharedMesh.subMeshCount >= 2;
                report.noLightsOrColliders = instance.GetComponentsInChildren<Light>(true).Length == 0 && instance.GetComponentsInChildren<Collider>(true).Length == 0;
                Require(report.transparentGlass && report.emissiveCore && report.twoGoldCrescents && report.noLightsOrColliders, "Missing glass, emissive spiral, two-part brass trim, or visual-only ownership.");
                var filamentMaterial = spiral != null ? spiral.GetComponent<Renderer>().sharedMaterial : null;
                Color emission = filamentMaterial != null ? filamentMaterial.GetColor("_EmissionColor") : Color.black;
                report.warmFilament = emission.r > 3 && emission.g > 1.7f && emission.g < emission.r && emission.b < emission.g * .25f;
                var localAura = Find(instance.transform, "Warm filament aura");
                report.localFilamentGlow = localAura != null && localAura.GetComponent<Renderer>().sharedMaterial.shader.name == "RadioArchipelago/Signal Spark Filament Aura";
                Color haloColor = Find(instance.transform, "Soft local glow").GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
                report.warmHalo = haloColor.r > haloColor.g && haloColor.g > haloColor.b && haloColor.a >= .45f;
                Require(report.warmFilament && report.localFilamentGlow && report.warmHalo, "Signal Spark lost its warm glowing filament or local yellow halo.");
                Require(report.triangles <= 31000 && report.renderers <= 9 && report.sharedMaterials <= 10, "Signal Spark exceeded its mobile asset budget.");
                Require(report.size.y > .92f && report.size.y < 1.04f, "Signal Spark currency scale is wrong.");
                instance.transform.position = Vector3.up * (.1f - visual.LocalVisualBounds.min.y);
                Quaternion rootRotation = instance.transform.localRotation;
                for (int i = 0; i <= 180; i++)
                {
                    visual.Tick(i * .13f, .5f);
                    float clearance = visual.WorldBottomY;
                    report.minGroundClearance = Mathf.Min(report.minGroundClearance, clearance);
                    report.maxGroundClearance = Mathf.Max(report.maxGroundClearance, clearance);
                    Require(clearance >= -.05f && clearance <= .2f, "Animated signal currency intersects or floats too far above ground.");
                    Require(Quaternion.Angle(rootRotation, instance.transform.localRotation) < .001f, "Signal Spark inherited the old coin root spin.");
                    report.simulationSamples++;
                }
                report.passed = true;
                string path = Path.GetFullPath("Library/RadioArchipelago/SignalSpark"); Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "signal-spark-checks.json"), JsonUtility.ToJson(report, true));
                Debug.Log("SIGNAL_SPARK_CHECKS_PASS glass/helix/orbit/shared assets/ground/no lights/no colliders");
            }
            finally { if (instance != null) Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(scene); }
        }

        [MenuItem("Radio Archipelago/Signal sparks/Capture Unity preview")]
        public static void CapturePreview()
        {
            var prefab = LoadPrefab();
            Require(prefab != null, "Create Signal Spark assets before capture.");
            string path = Path.GetFullPath("Library/RadioArchipelago/SignalSpark"); Directory.CreateDirectory(path);
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            RenderTexture target = null; Texture2D pixels = null;
            try
            {
                var camera = Node(scene, "Currency proof camera").AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << PreviewLayer;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.065f,.078f);
                camera.orthographic = true; camera.orthographicSize = .69f; camera.aspect = 1;
                camera.nearClipPlane = .02f; camera.farClipPlane = 10; camera.allowHDR = false;
                camera.transform.position = new Vector3(.23f,.12f,-2); camera.transform.LookAt(Vector3.zero);
                var actor = Object.Instantiate(prefab.gameObject); SceneManager.MoveGameObjectToScene(actor, scene);
                foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PreviewLayer;
                var visual = actor.GetComponent<SignalSparkVisual>(); visual.SetCamera(camera); visual.Tick(1.6f, .3f);
                Lamp(scene, "Warm key", new Vector3(35,145,0), new Color(1,.91f,.74f), 2.3f);
                Lamp(scene, "Cool fill", new Vector3(25,-25,0), new Color(.63f,.85f,1), 1.0f);
                target = new RenderTexture(1200,1200,24,RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                target.Create(); camera.targetTexture = target;
                pixels = new Texture2D(1200,1200,TextureFormat.RGB24,false);
                // URP needs one render to initialize this new preview camera's
                // opaque draw list before the first transparent-glass capture.
                camera.Render();
                foreach (var view in new[] { "front", "three-quarter" })
                {
                    actor.transform.rotation = Quaternion.Euler(0, view == "front" ? 0 : -32, 0);
                    visual.Tick(1.6f, .3f); camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0,0,1200,1200),0,0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(path, "signal-spark-unity-" + view + ".png"), pixels.EncodeToPNG());
                }
                Debug.Log("SIGNAL_SPARK_PREVIEW_PASS " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Material MaterialAsset(string name, Shader shader, Color color, float metallic, float smoothness, Color emission = default)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = "SignalSpark_" + name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            if (emission.maxColorComponent > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission); }
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material); return material;
        }

        private static Mesh HaloMesh()
        {
            string path = Folder + "/LocalGlow.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = "SignalSpark_LocalGlow" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0,2,1,0,3,2 }; mesh.RecalculateBounds(); mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static Bounds Measure(Renderer[] renderers, Transform root)
        {
            Bounds result = default; bool first = true;
            foreach (var renderer in renderers)
            {
                Bounds b = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    p = root.InverseTransformPoint(renderer.transform.TransformPoint(p));
                    if (first) { result = new Bounds(p,Vector3.zero); first = false; } else result.Encapsulate(p);
                }
            }
            return result;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root) { var found = Find(child,name); if (found != null) return found; }
            return null;
        }
        private static SignalSparkVisual LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return prefab != null ? prefab.GetComponent<SignalSparkVisual>() : null;
        }
        private static GameObject Node(Scene scene, string name) { var node = new GameObject(name); SceneManager.MoveGameObjectToScene(node,scene); node.layer = PreviewLayer; return node; }
        private static void Lamp(Scene scene, string name, Vector3 angles, Color color, float intensity)
        { var light = Node(scene,name).AddComponent<Light>(); light.type = LightType.Directional; light.color = color; light.intensity = intensity; light.cullingMask = 1 << PreviewLayer; light.transform.rotation = Quaternion.Euler(angles); }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; int cut = path.LastIndexOf('/'); EnsureFolder(path.Substring(0,cut)); AssetDatabase.CreateFolder(path.Substring(0,cut),path.Substring(cut+1)); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
