using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Imports the user's Blender-prepared art without regenerating its geometry or textures.</summary>
    public static class ImportedCourierAssetBuilder
    {
        private const string AssetRoot = "Assets/RadioArchipelago";
        private static readonly string SourceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtSource/RadioArchipelago/Processed"));
        private static readonly string[] Names = { "Tiko", "Lumen", "Nori" };
        private static readonly string[] Ids = { "signal", "sail", "orbit" };
        private static readonly string[] DisplayNames = { "Тико", "Люмен", "Нори" };
        private static readonly string[] Payload = { "Model.fbx", "BaseColor.png", "Normal.png", "MetallicSmoothness.png" };

        public static bool HasPreparedModels => Names.All(name => Payload.All(file => File.Exists(Path.Combine(SourceRoot, name, file))));

        [MenuItem("Radio Archipelago/Imported couriers/Import prepared Blender models")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import couriers in Edit Mode.");
            if (!HasPreparedModels) throw new FileNotFoundException("The three prepared FBX/texture payloads are not complete in " + SourceRoot);
            EnsureFolder(AssetRoot + "/Imported"); EnsureFolder(AssetRoot + "/Content/ImportedCouriers");
            EnsureFolder(AssetRoot + "/Content/CourierIcons"); EnsureFolder(AssetRoot + "/Content/Materials");
            var entries = new CourierDefinition[Names.Length];
            for (int i = 0; i < Names.Length; i++) entries[i] = ImportEntry(i);
            string catalogPath = AssetRoot + "/Content/CourierCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(catalogPath);
            if (catalog == null) throw new FileNotFoundException("The existing courier catalog is missing.", catalogPath);
            // Keep the catalog GUID, gameplay IDs and saved selection indices stable.
            catalog.entries = entries; EditorUtility.SetDirty(catalog);
            CourierLightingAssetBuilder.Ensure(catalog);
            AssetDatabase.SaveAssets();
            CourierAssetBuilder.Validate();
            Debug.Log("RADIO_IMPORTED_ASSETS PASS: supplied Blender models, explicit skinned controls, original UV/textures and stable catalog IDs");
        }

        // A single prepared courier can be checked before the whole catalog is switched.
        public static CourierDefinition ImportEntry(int i)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import couriers in Edit Mode.");
            if (i < 0 || i >= Names.Length) throw new ArgumentOutOfRangeException(nameof(i));
            if (!Payload.All(file => File.Exists(Path.Combine(SourceRoot, Names[i], file))))
                throw new FileNotFoundException("The prepared payload is incomplete: " + Names[i]);
            EnsureFolder(AssetRoot + "/Imported"); EnsureFolder(AssetRoot + "/Content/ImportedCouriers");
            EnsureFolder(AssetRoot + "/Content/CourierIcons"); EnsureFolder(AssetRoot + "/Content/Materials");
            string folder = AssetRoot + "/Imported/" + Names[i]; EnsureFolder(folder);
            foreach (string file in Payload)
                File.Copy(Path.Combine(SourceRoot, Names[i], file), Path.GetFullPath(folder + "/" + file), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var color = ImportTexture(folder + "/BaseColor.png", true, false);
            var normal = ImportTexture(folder + "/Normal.png", false, true);
            var metal = ImportTexture(folder + "/MetallicSmoothness.png", false, false);
            var material = MaterialFor(Names[i], color, normal, metal);
            string modelPath = folder + "/Model.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importBlendShapes = true;
            importer.isReadable = true; // Authoring validation checks real bind poses, UVs and weights.
            importer.importNormals = ModelImporterNormals.Import;
            // Blink stays on the original face surface. Keep its authored smooth shading;
            // recalculated morph normals crease the nearly collapsed painted eyelids.
            importer.importBlendShapeNormals = ModelImporterNormals.None;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.optimizeGameObjects = false;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidDataException("Prepared model could not be imported: " + modelPath);
            var definition = CreatePrefab(model, material, Ids[i], DisplayNames[i]);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static Texture2D ImportTexture(string path, bool srgb, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; importer.mipmapEnabled = true;
            importer.isReadable = false; importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material MaterialFor(string name, Texture2D color, Texture2D normal, Texture2D metal)
        {
            string path = AssetRoot + "/Content/Materials/Supplied_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The active project requires URP Lit for the supplied materials.");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.name = "Supplied_" + name;
            material.SetTexture("_BaseMap", color); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .7f);
            material.SetTexture("_MetallicGlossMap", metal); material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material); return material;
        }

        private static CourierDefinition CreatePrefab(GameObject modelAsset, Material material, string id, string displayName)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Courier_" + id); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var visual = new GameObject("VisualPivot").transform; visual.SetParent(root.transform, false);
                var model = UnityEngine.Object.Instantiate(modelAsset, visual, false); model.name = "SuppliedModel";
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0) throw new InvalidDataException(id + ": prepared model has no skinned geometry.");
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                Bounds bounds = BoundsOf(renderers);
                model.transform.localScale *= 2.55f / Mathf.Max(.01f, bounds.size.y);
                bounds = BoundsOf(renderers);
                var footL = Find(model, "foot.L", true); var footR = Find(model, "foot.R", true);
                var feet = (footL.position + footR.position) * .5f;
                model.transform.position += new Vector3(-feet.x, -bounds.min.y, -feet.z);
                var motion = root.AddComponent<CourierMotion>();
                var blink = skins.FirstOrDefault(skin => FindBlinkShape(skin) != null);
                if (blink == null) throw new InvalidDataException(id + ": prepared model has no authored Blink shape.");
                var binding = new CourierRigBinding
                {
                    visualPivot = visual, headPivot = Find(model, "head", true),
                    leftArm = Find(model, "arm.L", true), rightArm = Find(model, "arm.R", true),
                    leftFoot = footL, rightFoot = footR,
                    antennaYawPivot = Find(model, "antenna.yaw", true),
                    antennaChain = Chain(model, "antenna.").Where(node => node.name != "antenna.yaw").ToArray(), crestChain = Chain(model, "crest."),
                    scarfChain = Chain(model, "scarf."),
                    blinkEyes = new[] { Find(model, "eye.L", false), Find(model, "eye.R", false) }.Where(node => node != null).ToArray(),
                    blinkRenderer = blink, blinkShapeName = FindBlinkShape(blink)
                };
                motion.ConfigureRig(binding);
                motion.ResetMotion();
                FitAnimationBounds(motion, skins, binding);
                string path = AssetRoot + "/Content/ImportedCouriers/Courier_" + id + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                var portrait = CourierAssetBuilder.RenderThumbnail(root, id);
                return new CourierDefinition { id = id, displayName = displayName, prefab = prefab, icon = portrait };
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Transform Find(GameObject model, string name, bool required)
        {
            var node = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == name);
            if (node == null && required) throw new InvalidDataException(model.name + ": missing prepared rig bone " + name);
            return node;
        }
        private static string FindBlinkShape(SkinnedMeshRenderer renderer)
        {
            var mesh = renderer.sharedMesh;
            if (mesh == null) return null;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                if (name == "Blink" || name.EndsWith(".Blink", StringComparison.Ordinal)) return name;
            }
            return null;
        }
        private static void FitAnimationBounds(CourierMotion motion, SkinnedMeshRenderer[] skins, CourierRigBinding binding)
        {
            var bounds = new Bounds[skins.Length];
            var found = new bool[skins.Length];
            var vertices = new List<Vector3>();
            var sample = new Mesh { name = "Owned courier animation bounds sample" };
            bool manual = motion.ManualSimulation;
            void CaptureGeometry()
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    skins[i].BakeMesh(sample, true); sample.GetVertices(vertices);
                    var frame = skins[i].rootBone != null ? skins[i].rootBone : skins[i].transform;
                    foreach (var vertex in vertices)
                    {
                        // Skinned culling bounds follow rootBone, which can have different FBX axes.
                        var point = frame.InverseTransformPoint(skins[i].transform.TransformPoint(vertex));
                        if (!found[i]) { bounds[i] = new Bounds(point, Vector3.zero); found[i] = true; }
                        else bounds[i].Encapsulate(point);
                    }
                }
            }
            void Capture()
            {
                CaptureGeometry();
                var rotor = binding.antennaYawPivot;
                if (rotor == null) return;
                var rotation = rotor.rotation;
                try
                {
                    // The full rotor envelope is authored once; no offscreen skin updates are needed.
                    for (int angle = 30; angle < 360; angle += 30)
                    {
                        rotor.rotation = Quaternion.AngleAxis(angle, binding.visualPivot.up) * rotation;
                        CaptureGeometry();
                    }
                }
                finally { rotor.rotation = rotation; }
            }
            void Step(float seconds)
            {
                while (seconds > .000001f)
                {
                    float dt = Mathf.Min(seconds, 1f / 60); motion.SimulateSprings(dt); seconds -= dt;
                }
                Capture();
            }
            try
            {
                motion.ManualSimulation = true; motion.ResetMotion(); Capture();
                motion.PlayIdle(); Step(.35f); Step(3.65f); Step(.6f); Step(.6f);
                motion.PlayJump(2.4f);
                foreach (float phase in new[] { .15f, .5f, .85f }) { motion.SetFlightProgress(phase); Step(.4f); }
                // Four-Hz shoulder strokes need a full phase sweep: a few sparse flight
                // samples can repeatedly land on the same stroke and miss the raised paws.
                motion.SetFlightProgress(.5f);
                for (int frame = 0; frame < 30; frame++) Step(1f / 120);
                motion.PlayLanding(1.5f); Step(.04f); Step(.1f);
                motion.PlayHold(false, 2.6f, true); Step(.6f); Step(.8f);
                motion.PlayEnd(); Step(.5f); Step(.7f);
                for (int i = 0; i < skins.Length; i++)
                {
                    // Capture real bone/blend-shape extents once during authoring. Conservative
                    // extra room covers spring transients without forcing offscreen CPU skinning.
                    bounds[i].Expand(bounds[i].size * .15f + Vector3.one * .08f);
                    skins[i].localBounds = bounds[i];
                }
            }
            finally
            {
                motion.ResetMotion(); motion.ManualSimulation = manual;
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }
        private static Transform[] Chain(GameObject model, string prefix) => model.GetComponentsInChildren<Transform>(true)
            .Where(node => node.name.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(node => node.name, StringComparer.Ordinal).ToArray();
        private static Bounds BoundsOf(Renderer[] renderers)
        {
            return CourierVisualBounds.Measure(renderers);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
