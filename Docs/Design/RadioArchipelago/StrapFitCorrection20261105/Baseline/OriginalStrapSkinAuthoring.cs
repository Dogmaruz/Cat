using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>
    /// Adds fixed rear and rigid front controls to the supplied leather meshes.
    /// Source positions, UVs, topology and material assignments remain untouched.
    /// </summary>
    public static class OriginalStrapSkinAuthoring
    {
        const string Output = "Assets/RadioArchipelago/Content/Modular/StrapSkins";
        const float FrontPlateau = .7f;
        const float MiroAnchor = -.004f;
        const float MiroBoxFront = -.015f;
        static readonly string EvidencePath = Path.GetFullPath("../Docs/Design/RadioArchipelago/StrapReach20261105/skinning.json");

        [Serializable] sealed class PartEvidence
        {
            public string backpack, part, sourceAsset, skinAsset;
            public int vertices, rearFixedVertices, rigidFrontVertices, transitionVertices, protectedBoxVertices;
            public float anchorZ, frontZ;
            public bool sourcePositionsPreserved, uvPreserved, topologyPreserved, sourceNormalsPreserved, boxWeightsFixed;
        }

        [Serializable] sealed class Report
        {
            public bool passed;
            public int backpacks, sharedSkinMeshes;
            public PartEvidence[] parts;
        }

        public static void Run(CourierCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author the supplied strap controls in Edit Mode.");
            EnsureFolder(Output);
            var records = new List<PartEvidence>(); int backpacks = 0;
            foreach (var item in catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Backpack))
            {
                string prefabPath = AssetDatabase.GetAssetPath(item.prefab);
                var equipment = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var rig = equipment.GetComponent<CourierEquipmentRig>();
                    if (rig == null || rig.harness == null || rig.harness.Length != 2)
                        throw new InvalidDataException(item.id + ": two original Harness parts are required.");
                    Transform left = Control(rig.transform, "StrapFront.Left");
                    Transform right = Control(rig.transform, "StrapFront.Right");
                    rig.harnessFrontControls = new[] { left, right };
                    var bones = new[] { rig.transform, left, right };
                    var filters = rig.harness.SelectMany(t => t.GetComponentsInChildren<MeshFilter>(true)).Distinct().ToList();
                    if (filters.Count != 2) throw new InvalidDataException(item.id + ": each original Harness needs one MeshFilter.");
                    bool integralMiro = item.id == "backpack_Miro";
                    if (integralMiro)
                    {
                        var cases = equipment.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name == "Case").ToArray();
                        if (cases.Length != 1) throw new InvalidDataException("Classic Miro needs its original Case mesh.");
                        filters.Add(cases[0]);
                    }
                    var all = new List<Vector3>();
                    foreach (var filter in filters)
                    {
                        if (filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                            throw new InvalidDataException(item.id + "/" + filter.name + ": readable original mesh required.");
                        Matrix4x4 matrix = rig.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        foreach (var point in filter.sharedMesh.vertices) all.Add(matrix.MultiplyPoint3x4(point));
                    }
                    float anchor = integralMiro ? MiroAnchor : all.Min(p => p.z);
                    float front = all.Max(p => p.z);
                    if (front - anchor < .025f) throw new InvalidDataException(item.id + ": source straps have no usable forward span.");
                    foreach (var filter in filters)
                        records.Add(Author(item.id, filter, rig.transform, bones, anchor, front, integralMiro && filter.name == "Case"));
                    PrefabUtility.SaveAsPrefabAsset(equipment, prefabPath); backpacks++;
                }
                finally { PrefabUtility.UnloadPrefabContents(equipment); }
            }
            AssetDatabase.SaveAssets();
            var report = new Report { backpacks = backpacks, sharedSkinMeshes = records.Count, parts = records.ToArray(),
                passed = backpacks == 20 && records.Count == 41 && records.All(p => p.sourcePositionsPreserved && p.uvPreserved && p.topologyPreserved && p.sourceNormalsPreserved && p.boxWeightsFixed) };
            Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath));
            File.WriteAllText(EvidencePath, JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidDataException("The original strap skin collection did not retain its source mesh contracts.");
            Debug.Log("ORIGINAL_STRAP_SKINS PASS: 20 backpacks, 41 shared source-position skins; fixed rear roots and rigid front buckles.");
        }

        static PartEvidence Author(string item, MeshFilter filter, Transform frame, Transform[] bones, float anchor, float front, bool integralCase)
        {
            Mesh source = filter.sharedMesh;
            var vertices = source.vertices;
            var weights = new BoneWeight[vertices.Length];
            Matrix4x4 toFrame = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var report = new PartEvidence { backpack = item, part = filter.name, vertices = vertices.Length,
                sourceAsset = AssetDatabase.GetAssetPath(source), anchorZ = anchor, frontZ = front, boxWeightsFixed = true };
            // The two supplied loop objects can have misleading L/R names after
            // coordinate conversion. Actual equipment-frame position chooses the side.
            float side = vertices.Select(p => toFrame.MultiplyPoint3x4(p).x).Average();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = toFrame.MultiplyPoint3x4(vertices[i]);
                float progress = Mathf.Clamp01((p.z - anchor) / (front - anchor));
                float influence = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / FrontPlateau));
                int control = (integralCase ? p.x : side) < 0 ? 1 : 2;
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1 - influence, boneIndex1 = control, weight1 = influence };
                if (influence <= .000001f) report.rearFixedVertices++;
                else if (influence >= .999999f) report.rigidFrontVertices++;
                else report.transitionVertices++;
                if (integralCase && p.z <= MiroBoxFront + .000001f)
                {
                    report.protectedBoxVertices++;
                    report.boxWeightsFixed &= influence == 0;
                }
            }
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = item + "_" + filter.name + "_OriginalStrapSkin";
            mesh.boneWeights = weights;
            var bindposes = bones.Select(b => b.worldToLocalMatrix * filter.transform.localToWorldMatrix).ToArray();
            mesh.bindposes = bindposes;
            report.sourcePositionsPreserved = mesh.vertices.SequenceEqual(vertices);
            report.uvPreserved = mesh.uv.SequenceEqual(source.uv);
            report.topologyPreserved = mesh.subMeshCount == source.subMeshCount && mesh.triangles.SequenceEqual(source.triangles);
            report.sourceNormalsPreserved = mesh.normals.SequenceEqual(source.normals);
            if (!report.sourcePositionsPreserved || !report.uvPreserved || !report.topologyPreserved || !report.sourceNormalsPreserved || !report.boxWeightsFixed)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                throw new InvalidDataException(item + "/" + filter.name + ": authoring changed supplied geometry or fixed box weights.");
            }
            string path = Output + "/" + item + "_" + filter.name.Replace('.', '_') + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset == null) { AssetDatabase.CreateAsset(mesh, path); asset = mesh; }
            else
            {
                EditorUtility.CopySerialized(mesh, asset);
                asset.vertices = vertices;
                asset.boneWeights = weights;
                asset.bindposes = bindposes;
                asset.bounds = source.bounds;
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            asset.UploadMeshData(false); EditorUtility.SetDirty(asset);
            report.skinAsset = path;
            Renderer originalRenderer = filter.GetComponent<MeshRenderer>();
            if (originalRenderer == null) originalRenderer = filter.GetComponent<SkinnedMeshRenderer>();
            if (originalRenderer == null) throw new InvalidDataException(filter.name + ": original MeshRenderer missing.");
            // Unity replaces the MeshRenderer when a SkinnedMeshRenderer is added.
            // Capture its settings before that native component replacement.
            var materials = originalRenderer.sharedMaterials;
            var shadows = originalRenderer.shadowCastingMode;
            var receives = originalRenderer.receiveShadows;
            var probes = originalRenderer.lightProbeUsage;
            var reflections = originalRenderer.reflectionProbeUsage;
            var probeAnchor = originalRenderer.probeAnchor;
            var renderingLayer = originalRenderer.renderingLayerMask;
            var skin = filter.GetComponent<SkinnedMeshRenderer>();
            if (skin == null) skin = filter.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = asset;
            skin.bones = bones;
            skin.rootBone = frame;
            skin.quality = SkinQuality.Bone2;
            skin.sharedMaterials = materials;
            skin.shadowCastingMode = shadows;
            skin.receiveShadows = receives;
            skin.lightProbeUsage = probes;
            skin.reflectionProbeUsage = reflections;
            skin.probeAnchor = probeAnchor;
            skin.renderingLayerMask = renderingLayer;
            skin.enabled = true;
            // Culling must include the front controls' allowed fitting travel. Visual
            // framing measures BakeMesh vertices and therefore ignores this padding.
            var bounds = source.bounds;
            Matrix4x4 fromFrame = toFrame.inverse;
            Vector3 xPadding = fromFrame.MultiplyVector(Vector3.right * 1.0f);
            Vector3 zPadding = fromFrame.MultiplyVector(Vector3.forward * 1.5f);
            bounds.Expand(new Vector3(Mathf.Abs(xPadding.x) + Mathf.Abs(zPadding.x), Mathf.Abs(xPadding.y) + Mathf.Abs(zPadding.y),
                Mathf.Abs(xPadding.z) + Mathf.Abs(zPadding.z)) * 2);
            skin.localBounds = bounds;
            if (originalRenderer != null && originalRenderer != skin) originalRenderer.enabled = false;
            return report;
        }

        static Transform Control(Transform root, string name)
        {
            Transform control = null;
            for (int i = 0; i < root.childCount; i++) if (root.GetChild(i).name == name) { control = root.GetChild(i); break; }
            if (control == null) { control = new GameObject(name).transform; control.SetParent(root, false); }
            control.localPosition = Vector3.zero; control.localRotation = Quaternion.identity; control.localScale = Vector3.one;
            control.gameObject.layer = root.gameObject.layer; return control;
        }

        static void EnsureFolder(string path)
        {
            string[] pieces = path.Split('/'); string parent = pieces[0];
            for (int i = 1; i < pieces.Length; i++)
            {
                string child = parent + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent, pieces[i]);
                parent = child;
            }
        }
    }
}
