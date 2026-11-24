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
            public float anchorZ, frontZ, minimumStrapY, maximumStrapY, upperGuideStartY, upperGuideFullY;
            public bool sourcePositionsPreserved, uvPreserved, topologyPreserved, sourceNormalsPreserved, boxWeightsFixed;
            public int rigidMetalGroups, pinnedMetalGroups, rigidMetalVertices;
            public float maximumMetalWeightRangeBefore, maximumMetalWeightRangeAfter;
            public float maximumMovingWeightRangeAfter, maximumUpperWeightRangeAfter;
            public int semanticMetalGroups;
            public float maximumMetalSignatureRangeAfter;
            public bool metalWeightsRigid;
            public MetalProtection[] metalProtection;
        }

        [Serializable] sealed class MetalProtection
        {
            public int metalVertices, affectedVertices, movingBone;
            public float minimumBefore, maximumBefore, uniformWeight, weightRangeAfter;
            public float minimumUpperAlphaBefore, maximumUpperAlphaBefore, uniformUpperAlpha, upperWeightRangeAfter;
            public bool rearPinned;
            public Vector3 sourceMinimum, sourceMaximum;
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
                    Transform upperLeft = Control(rig.transform, "StrapUpper.Left");
                    Transform upperRight = Control(rig.transform, "StrapUpper.Right");
                    rig.harnessUpperControls = new[] { upperLeft, upperRight };
                    var bones = new[] { rig.transform, left, right, upperLeft, upperRight };
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
                    float minimumY = all.Min(p => p.y), maximumY = all.Max(p => p.y);
                    if (front - anchor < .025f) throw new InvalidDataException(item.id + ": source straps have no usable forward span.");
                    foreach (var filter in filters)
                        records.Add(Author(item.id, filter, rig.transform, bones, anchor, front, minimumY, maximumY,
                            integralMiro && filter.name == "Case"));
                    PrefabUtility.SaveAsPrefabAsset(equipment, prefabPath); backpacks++;
                }
                finally { PrefabUtility.UnloadPrefabContents(equipment); }
            }
            AssetDatabase.SaveAssets();
            var report = new Report { backpacks = backpacks, sharedSkinMeshes = records.Count, parts = records.ToArray(),
                passed = backpacks == 20 && records.Count == 41 && records.All(p => p.sourcePositionsPreserved && p.uvPreserved && p.topologyPreserved && p.sourceNormalsPreserved && p.boxWeightsFixed && p.metalWeightsRigid) };
            Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath));
            File.WriteAllText(EvidencePath, JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidDataException("The original strap skin collection did not retain its source mesh contracts.");
            Debug.Log("ORIGINAL_STRAP_SKINS PASS: 20 backpacks, 41 shared source-position skins; fixed rear roots and rigid front buckles.");
        }

        static PartEvidence Author(string item, MeshFilter filter, Transform frame, Transform[] bones, float anchor, float front,
            float minimumY, float maximumY, bool integralCase)
        {
            Mesh source = filter.sharedMesh;
            var vertices = source.vertices;
            var weights = new BoneWeight[vertices.Length];
            var upperAlpha = new float[vertices.Length];
            Matrix4x4 toFrame = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var report = new PartEvidence { backpack = item, part = filter.name, vertices = vertices.Length,
                sourceAsset = AssetDatabase.GetAssetPath(source), anchorZ = anchor, frontZ = front, boxWeightsFixed = true,
                minimumStrapY = minimumY, maximumStrapY = maximumY,
                upperGuideStartY = Mathf.Lerp(minimumY, maximumY, .2f), upperGuideFullY = Mathf.Lerp(minimumY, maximumY, .8f) };
            // The two supplied loop objects can have misleading L/R names after
            // coordinate conversion. Actual equipment-frame position chooses the side.
            float side = vertices.Select(p => toFrame.MultiplyPoint3x4(p).x).Average();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = toFrame.MultiplyPoint3x4(vertices[i]);
                float progress = Mathf.Clamp01((p.z - anchor) / (front - anchor));
                float influence = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / FrontPlateau));
                upperAlpha[i] = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(report.upperGuideStartY, report.upperGuideFullY, p.y));
                int control = (integralCase ? p.x : side) < 0 ? 1 : 2;
                weights[i] = GuideWeights(influence, upperAlpha[i], control);
            }
            ProtectMetal(item, source, vertices, toFrame, weights, upperAlpha, anchor, report);
            // Counters describe final protected weights, rather than the initial ramp.
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = toFrame.MultiplyPoint3x4(vertices[i]);
                float influence = weights[i].weight1 + weights[i].weight2;
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
            skin.quality = SkinQuality.Bone4;
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
            Vector3 yPadding = fromFrame.MultiplyVector(Vector3.up * .75f);
            Vector3 zPadding = fromFrame.MultiplyVector(Vector3.forward * 1.5f);
            bounds.Expand(new Vector3(Mathf.Abs(xPadding.x) + Mathf.Abs(yPadding.x) + Mathf.Abs(zPadding.x),
                Mathf.Abs(xPadding.y) + Mathf.Abs(yPadding.y) + Mathf.Abs(zPadding.y),
                Mathf.Abs(xPadding.z) + Mathf.Abs(yPadding.z) + Mathf.Abs(zPadding.z)) * 2);
            skin.localBounds = bounds;
            if (originalRenderer != null && originalRenderer != skin) originalRenderer.enabled = false;
            return report;
        }

        static void ProtectMetal(string item, Mesh source, Vector3[] vertices, Matrix4x4 toFrame, BoneWeight[] weights, float[] upperAlpha,
            float anchor, PartEvidence report)
        {
            string id = item.StartsWith("backpack_", StringComparison.Ordinal) ? item.Substring("backpack_".Length) : item;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/RadioArchipelago/Content/Modular/Materials/Backpacks_" + id + ".mat");
            if (material == null) throw new InvalidDataException(item + ": the supplied material is missing.");
            // Use the same read-only semantic definition as the independent pose
            // checker: triangles, metallic atlas and MESH-LOCAL UV seam positions.
            // No importer settings or source mesh buffers are changed by that reader.
            int[][] semanticGroups = CourierStrapClearanceProbe.FindMetalVertexGroups(source, material);
            report.semanticMetalGroups = semanticGroups.Length;
            var points = vertices.Select(p => toFrame.MultiplyPoint3x4(p)).ToArray();
            var initialWeights = weights.ToArray();
            var initialUpper = upperAlpha.ToArray();
            var parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            void Union(int a, int b) { parent[Find(a)] = Find(b); }
            var semanticMetal = new bool[vertices.Length];
            foreach (var group in semanticGroups)
                foreach (int vertex in group) { semanticMetal[vertex] = true; Union(group[0], vertex); }

            // Build the COMPLETE closure before assigning any weights. A leather UV
            // copy at a metal seam can connect two semantic groups; assigning groups
            // sequentially would overwrite that seam and break the earlier group.
            var localAliases = new Dictionary<Vector3Int, int>();
            var equipmentAliases = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000), Mathf.RoundToInt(p.y * 100000), Mathf.RoundToInt(p.z * 100000));
                if (localAliases.TryGetValue(key, out int other)) Union(i, other); else localAliases[key] = i;
                p = points[i];
                key = new Vector3Int(Mathf.RoundToInt(p.x * 100000), Mathf.RoundToInt(p.y * 100000), Mathf.RoundToInt(p.z * 100000));
                if (equipmentAliases.TryGetValue(key, out other)) Union(i, other); else equipmentAliases[key] = i;
            }
            var constrainedRoots = new HashSet<int>(semanticGroups.Select(g => Find(g[0])));
            var components = Enumerable.Range(0, vertices.Length).GroupBy(Find)
                .Where(g => constrainedRoots.Contains(g.Key)).Select(g => g.ToArray()).ToArray();
            var evidence = new List<MetalProtection>();
            foreach (int[] affected in components)
            {
                int[] ids = affected.Where(i => semanticMetal[i]).ToArray();
                float[] before = ids.Select(i => initialWeights[i].weight1 + initialWeights[i].weight2).OrderBy(w => w).ToArray();
                float[] upperBefore = ids.Select(i => initialUpper[i]).OrderBy(a => a).ToArray();
                bool pinned = affected.Any(i => points[i].z <= anchor + .00002f);
                float uniform = pinned ? 0 : before[before.Length / 2];
                float uniformUpper = pinned ? 0 : upperBefore[upperBefore.Length / 2];
                if (before[0] >= .999999f && !pinned) uniform = 1;
                int bone = ids.GroupBy(i => initialWeights[i].boneIndex1).OrderByDescending(g => g.Count()).First().Key;
                foreach (int index in affected)
                {
                    weights[index] = GuideWeights(uniform, uniformUpper, bone);
                    upperAlpha[index] = uniformUpper;
                }
                evidence.Add(new MetalProtection { metalVertices = ids.Length, affectedVertices = affected.Length, movingBone = bone,
                    minimumBefore = before[0], maximumBefore = before[before.Length - 1], uniformWeight = uniform, rearPinned = pinned,
                    minimumUpperAlphaBefore = upperBefore[0], maximumUpperAlphaBefore = upperBefore[upperBefore.Length - 1],
                    uniformUpperAlpha = uniformUpper,
                    sourceMinimum = new Vector3(ids.Min(i => points[i].x), ids.Min(i => points[i].y), ids.Min(i => points[i].z)),
                    sourceMaximum = new Vector3(ids.Max(i => points[i].x), ids.Max(i => points[i].y), ids.Max(i => points[i].z)) });
            }
            // Verify FINAL data after every assignment, including full side/upper
            // bone signatures. Immediate per-write range=0 did not prove that a later
            // alias propagation had left the earlier component unchanged.
            for (int component = 0; component < components.Length; component++)
            {
                int[] ids = components[component];
                evidence[component].weightRangeAfter = ids.Max(i => weights[i].weight1 + weights[i].weight2) - ids.Min(i => weights[i].weight1 + weights[i].weight2);
                evidence[component].upperWeightRangeAfter = ids.Max(i => weights[i].weight2) - ids.Min(i => weights[i].weight2);
            }
            float signatureRange = 0;
            foreach (var group in semanticGroups)
                foreach (int vertex in group)
                    for (int bone = 0; bone < 5; bone++)
                        signatureRange = Mathf.Max(signatureRange, Mathf.Abs(WeightForBone(weights[vertex], bone) - WeightForBone(weights[group[0]], bone)));
            report.maximumMetalSignatureRangeAfter = signatureRange;
            report.metalProtection = evidence.ToArray();
            report.rigidMetalGroups = evidence.Count;
            report.pinnedMetalGroups = evidence.Count(g => g.rearPinned);
            report.rigidMetalVertices = evidence.Sum(g => g.affectedVertices);
            report.maximumMetalWeightRangeBefore = evidence.Select(g => g.maximumBefore - g.minimumBefore).DefaultIfEmpty(0).Max();
            report.maximumMetalWeightRangeAfter = evidence.Select(g => g.weightRangeAfter).DefaultIfEmpty(0).Max();
            report.maximumMovingWeightRangeAfter = report.maximumMetalWeightRangeAfter;
            report.maximumUpperWeightRangeAfter = evidence.Select(g => g.upperWeightRangeAfter).DefaultIfEmpty(0).Max();
            report.metalWeightsRigid = report.maximumMovingWeightRangeAfter <= .000001f && report.maximumUpperWeightRangeAfter <= .000001f &&
                report.maximumMetalSignatureRangeAfter <= .000001f;
            if (!report.metalWeightsRigid) throw new InvalidDataException(item + ": final metallic component weights are inconsistent.");
        }

        static float WeightForBone(BoneWeight weights, int bone) =>
            (weights.boneIndex0 == bone ? weights.weight0 : 0) + (weights.boneIndex1 == bone ? weights.weight1 : 0) +
            (weights.boneIndex2 == bone ? weights.weight2 : 0) + (weights.boneIndex3 == bone ? weights.weight3 : 0);

        static BoneWeight GuideWeights(float moving, float upperAlpha, int frontBone)
        {
            return new BoneWeight { boneIndex0 = 0, weight0 = 1 - moving,
                boneIndex1 = frontBone, weight1 = moving * (1 - upperAlpha),
                boneIndex2 = frontBone + 2, weight2 = moving * upperAlpha };
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
