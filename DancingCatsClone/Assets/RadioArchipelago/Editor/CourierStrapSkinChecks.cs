using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Read-only checks of the original strap skins in their actual equipped poses.</summary>
    public static class CourierStrapSkinChecks
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        const float PositionTolerance = .00005f;
        static readonly Dictionary<Mesh, int[][]> MetalGroups = new Dictionary<Mesh, int[][]>();
        [Serializable] public sealed class SkinEvidence
        {
            public string part;
            public int vertices, triangles, fixedRearVertices, rigidFrontVertices, protectedBoxVertices, nonfiniteVertices, maximumInfluences;
            public int metallicGroups, metallicVertices;
            public float metallicWeightMaximumDifference, metallicDisplacementMaximumError;
            public bool originalPositionsPreserved, originalUVPreserved, originalTopologyPreserved, originalMaterialsPreserved;
            public float cpuToBakeMaximumError, expectedPoseMaximumError, fixedRearMaximumError, rigidFrontMaximumError, protectedBoxMaximumError;
        }
        [Serializable] public sealed class PairEvidence
        {
            public string hero, backpack;
            public bool passed;
            public float caseHeight, shoulderZ, posedFrontZ;
            public int sourceColliders, equippedColliders;
            public SkinEvidence[] skins;
            public string[] failures;
        }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string unityVersion, capturedUtc;
            public int pairs, skins, vertices, fixedRearVertices, rigidFrontVertices, protectedBoxVertices;
            public int metallicGroups, metallicVertices;
            public float metallicWeightMaximumDifference, metallicDisplacementMaximumError;
            public string rigidityMeaning = "rigidFrontVertices is retained as a legacy count of fully moving vertices; metal group rigidity is measured independently by uniform full weight signatures and equal displacement.";
            public float cpuToBakeMaximumError, expectedPoseMaximumError, fixedRearMaximumError, rigidFrontMaximumError, protectedBoxMaximumError;
            public PairEvidence[] observations;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run the read-only original strap checks in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("Missing courier catalog.");
            MetalGroups.Clear();
            var observations = new List<PairEvidence>();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var hero in catalog.entries)
                {
                    var actor = UnityEngine.Object.Instantiate(hero.prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                        var motion = actor.GetComponent<CourierMotion>();
                        motion.ManualSimulation = true;
                        foreach (var item in catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Backpack))
                        {
                            var pair = new PairEvidence { hero = hero.assetId, backpack = item.id };
                            var failures = new List<string>();
                            var parts = new List<SkinEvidence>();
                            try
                            {
                                motion.ResetMotion();
                                wardrobe.Apply(item, null, null);
                                var gear = wardrobe.Equipped(CourierEquipmentKind.Backpack);
                                var rig = gear.GetComponent<CourierEquipmentRig>();
                                var fit = wardrobe.profile.FindFit(item.id);
                                Require(rig != null && fit != null, "Missing equipment rig or fit.", failures);
                                Require(rig.harnessFrontControls.Length == 2 && rig.harnessFrontControls.All(t => t != null),
                                    "The supplied loops need two actual front controls.", failures);
                                Require(rig.harnessFrontControls.All(t => t.parent == rig.transform),
                                    "Front controls must use the equipment coordinate frame.", failures);
                                Require(rig.harnessUpperControls.Length == 2 && rig.harnessUpperControls.All(t => t != null && t.parent == rig.transform),
                                    "The upper shoulder controls must use the equipment coordinate frame.", failures);
                                var visual = wardrobe.bodyRig.visualPivot;
                                var equipmentToVisual = visual.worldToLocalMatrix * gear.transform.localToWorldMatrix;
                                pair.caseHeight = equipmentToVisual.MultiplyVector(Vector3.up * (rig.caseBoundsMax.y - rig.caseBoundsMin.y)).magnitude;
                                Require(Mathf.Abs(pair.caseHeight - 1.18f) < .0001f, "The Nori-reference case height changed.", failures);
                                pair.sourceColliders = item.prefab.GetComponentsInChildren<Collider>(true).Length;
                                pair.equippedColliders = gear.GetComponentsInChildren<Collider>(true).Length;
                                Require(pair.sourceColliders == pair.equippedColliders, "Fitting added a collider.", failures);
                                pair.shoulderZ = (visual.InverseTransformPoint(wardrobe.bodyRig.leftArm.position).z +
                                    visual.InverseTransformPoint(wardrobe.bodyRig.rightArm.position).z) * .5f;
                                pair.posedFrontZ = float.NegativeInfinity;
                                var skins = gear.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                                Require(skins.Length >= 2, "No skinned original shoulder loops were installed.", failures);
                                foreach (var skin in skins)
                                    parts.Add(CheckSkin(skin, rig, visual, item, item.id == "backpack_Miro" && skin.name == "Case", pair, failures));
                                Require(parts.Sum(p => p.fixedRearVertices) > 0, "No rear mounting vertices remained pinned.", failures);
                                Require(parts.Sum(p => p.rigidFrontVertices) > 0, "No rigid front region exists for the buckles.", failures);
                                Require(pair.posedFrontZ >= pair.shoulderZ + .04f,
                                    "The actual baked strap front still falls behind the upper arm.", failures);
                            }
                            catch (Exception error)
                            {
                                failures.Add(error.GetType().Name + ": " + error.Message);
                            }
                            pair.skins = parts.ToArray(); pair.failures = failures.ToArray(); pair.passed = failures.Count == 0;
                            observations.Add(pair);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); MetalGroups.Clear(); }
            var all = observations.SelectMany(p => p.skins).ToArray();
            var report = new Report
            {
                unityVersion = Application.unityVersion, capturedUtc = DateTime.UtcNow.ToString("O"),
                pairs = observations.Count, skins = all.Length, vertices = all.Sum(p => p.vertices),
                fixedRearVertices = all.Sum(p => p.fixedRearVertices), rigidFrontVertices = all.Sum(p => p.rigidFrontVertices),
                protectedBoxVertices = all.Sum(p => p.protectedBoxVertices),
                metallicGroups = all.Sum(p => p.metallicGroups), metallicVertices = all.Sum(p => p.metallicVertices),
                metallicWeightMaximumDifference = Maximum(all, p => p.metallicWeightMaximumDifference),
                metallicDisplacementMaximumError = Maximum(all, p => p.metallicDisplacementMaximumError),
                cpuToBakeMaximumError = Maximum(all, p => p.cpuToBakeMaximumError),
                expectedPoseMaximumError = Maximum(all, p => p.expectedPoseMaximumError),
                fixedRearMaximumError = Maximum(all, p => p.fixedRearMaximumError),
                rigidFrontMaximumError = Maximum(all, p => p.rigidFrontMaximumError),
                protectedBoxMaximumError = Maximum(all, p => p.protectedBoxMaximumError),
                observations = observations.ToArray(), passed = observations.Count == 200 && observations.All(p => p.passed)
            };
            string folder = Path.GetFullPath("../Docs/Design/RadioArchipelago/StrapReach20261105");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "skin-checks.json"), JsonUtility.ToJson(report, true));
            if (!report.passed)
                throw new InvalidDataException("Original strap skin checks failed for " + observations.Count(p => !p.passed) + " pairs. Inspect skin-checks.json.");
            Debug.Log("COURIER_STRAP_SKIN_CHECKS PASS: 200 equipped pairs; five-bone source skins, fixed rear mounts, rigid metal components, independent CPU/BakeMesh agreement, case sizes and colliders.");
        }

        static SkinEvidence CheckSkin(SkinnedMeshRenderer skin, CourierEquipmentRig rig, Transform visual,
            CourierEquipmentDefinition item, bool protectedMiroBox, PairEvidence pair, List<string> failures)
        {
            var filter = skin.GetComponent<MeshFilter>();
            var source = filter != null ? filter.sharedMesh : null;
            var mesh = skin.sharedMesh;
            var result = new SkinEvidence { part = skin.name };
            Require(source != null && mesh != null, skin.name + ": missing untouched source mesh.", failures);
            if (source == null || mesh == null) return result;
            var points = mesh.vertices; var originalPoints = source.vertices;
            result.vertices = points.Length; result.triangles = mesh.triangles.Length / 3;
            result.originalPositionsPreserved = points.SequenceEqual(originalPoints);
            result.originalUVPreserved = mesh.uv.SequenceEqual(source.uv);
            result.originalTopologyPreserved = mesh.subMeshCount == source.subMeshCount && mesh.triangles.SequenceEqual(source.triangles);
            var originalRenderer = skin.GetComponent<MeshRenderer>();
            var expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/RadioArchipelago/Content/Modular/Materials/Backpacks_" + item.id.Substring("backpack_".Length) + ".mat");
            result.originalMaterialsPreserved = expectedMaterial != null && skin.sharedMaterials.Length > 0 && skin.sharedMaterials.All(m => m == expectedMaterial);
            Require(result.originalPositionsPreserved && result.originalUVPreserved && result.originalTopologyPreserved && result.originalMaterialsPreserved,
                skin.name + ": source positions, topology, UV or materials changed.", failures);
            Require(skin.enabled && (originalRenderer == null || !originalRenderer.enabled), skin.name + ": duplicate or disabled visible renderer.", failures);
            var bones = skin.bones; var bindposes = mesh.bindposes; var weights = mesh.boneWeights;
            Require(bones.Length == 5 && bindposes.Length == 5 && weights.Length == points.Length,
                skin.name + ": invalid original strap skeleton.", failures);
            if (bones.Length != 5 || bindposes.Length != 5 || weights.Length != points.Length) return result;
            Require(bones[0] == rig.transform && bones[1] == rig.harnessFrontControls[0] && bones[2] == rig.harnessFrontControls[1] &&
                bones[3] == rig.harnessUpperControls[0] && bones[4] == rig.harnessUpperControls[1],
                skin.name + ": skin uses a different root/front/upper control order.", failures);
            Require(bones.Skip(1).All(b => b.parent == rig.transform && Quaternion.Angle(b.localRotation, Quaternion.identity) < .0001f &&
                Vector3.Distance(b.localScale, Vector3.one) < .000001f), skin.name + ": expected translation-only front and upper controls.", failures);
            var matrices = bones.Select((b, i) => b.localToWorldMatrix * bindposes[i]).ToArray();
            var boneOffsets = bones.Select((b, i) => i == 0 ? Vector3.zero : b.localPosition).ToArray();
            var toEquipment = rig.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            var worldToEquipment = rig.transform.worldToLocalMatrix;
            var toVisual = visual.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            var baked = new Mesh { indexFormat = mesh.indexFormat };
            try
            {
                skin.BakeMesh(baked, true); var actual = baked.vertices;
                Require(actual.Length == points.Length, skin.name + ": BakeMesh changed the vertex count.", failures);
                if (actual.Length != points.Length) return result;
                for (int i = 0; i < points.Length; i++)
                {
                    var w = weights[i];
                    float sum = 0, rootWeight = 0; int influenceCount = 0; bool valid = true;
                    Vector3 cpuWorld = Vector3.zero, expectedOffset = Vector3.zero;
                    for (int channel = 0; channel < 4; channel++)
                    {
                        float weight = Weight(w, channel); int bone = Index(w, channel);
                        if (weight < -.000001f || float.IsNaN(weight) || float.IsInfinity(weight)) valid = false;
                        if (weight <= 0) continue;
                        if (bone < 0 || bone >= bones.Length) { valid = false; continue; }
                        sum += weight; influenceCount++;
                        cpuWorld += matrices[bone].MultiplyPoint3x4(points[i]) * weight;
                        expectedOffset += boneOffsets[bone] * weight;
                        if (bone == 0) rootWeight += weight;
                    }
                    result.maximumInfluences = Mathf.Max(result.maximumInfluences, influenceCount);
                    if (!valid || Mathf.Abs(sum - 1) > .00001f || influenceCount > 3)
                    { failures.Add(skin.name + ": weights are not normalized root/front/upper influences."); break; }
                    var basePoint = toEquipment.MultiplyPoint3x4(points[i]);
                    var posed = toEquipment.MultiplyPoint3x4(actual[i]);
                    if (!Finite(posed)) { result.nonfiniteVertices++; continue; }
                    var cpu = worldToEquipment.MultiplyPoint3x4(cpuWorld);
                    result.cpuToBakeMaximumError = Mathf.Max(result.cpuToBakeMaximumError, Vector3.Distance(cpu, posed));
                    result.expectedPoseMaximumError = Mathf.Max(result.expectedPoseMaximumError, Vector3.Distance(basePoint + expectedOffset, posed));
                    if (rootWeight >= .999999f)
                    {
                        result.fixedRearVertices++;
                        result.fixedRearMaximumError = Mathf.Max(result.fixedRearMaximumError, Vector3.Distance(basePoint, posed));
                    }
                    if (rootWeight <= .000001f)
                    {
                        result.rigidFrontVertices++;
                        // This is the fully moving region; it can include an upper
                        // blend. Actual metal component rigidity is checked below.
                        result.rigidFrontMaximumError = Mathf.Max(result.rigidFrontMaximumError,
                            Vector3.Distance(posed - basePoint, expectedOffset));
                    }
                    if (protectedMiroBox && basePoint.z <= -.015f + .000001f)
                    {
                        result.protectedBoxVertices++;
                        result.protectedBoxMaximumError = Mathf.Max(result.protectedBoxMaximumError, Vector3.Distance(basePoint, posed));
                        if (rootWeight < .999999f) failures.Add("Miro box vertex was assigned a moving strap bone.");
                    }
                    pair.posedFrontZ = Mathf.Max(pair.posedFrontZ, toVisual.MultiplyPoint3x4(actual[i]).z);
                }
                if (!MetalGroups.TryGetValue(source, out var groups))
                    MetalGroups[source] = groups = CourierStrapClearanceProbe.FindMetalVertexGroups(source, expectedMaterial);
                foreach (var group in groups)
                {
                    if (group.Length == 0) continue;
                    result.metallicGroups++; result.metallicVertices += group.Length;
                    int first = group[0]; var signature = weights[first];
                    var referenceDelta = toEquipment.MultiplyPoint3x4(actual[first]) - toEquipment.MultiplyPoint3x4(points[first]);
                    foreach (int vertex in group)
                    {
                        float difference = 0;
                        for (int bone = 0; bone < bones.Length; bone++)
                            difference += Mathf.Abs(WeightForBone(weights[vertex], bone) - WeightForBone(signature, bone));
                        result.metallicWeightMaximumDifference = Mathf.Max(result.metallicWeightMaximumDifference, difference);
                        var delta = toEquipment.MultiplyPoint3x4(actual[vertex]) - toEquipment.MultiplyPoint3x4(points[vertex]);
                        result.metallicDisplacementMaximumError = Mathf.Max(result.metallicDisplacementMaximumError, Vector3.Distance(delta, referenceDelta));
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
            Require(result.nonfiniteVertices == 0, skin.name + ": nonfinite skinned positions.", failures);
            Require(result.cpuToBakeMaximumError <= PositionTolerance && result.expectedPoseMaximumError <= PositionTolerance,
                skin.name + ": pose/bindpose differs from independent CPU skinning (including imported scale).", failures);
            Require(result.fixedRearMaximumError <= PositionTolerance && result.protectedBoxMaximumError <= PositionTolerance,
                skin.name + ": a rear mount or fixed case vertex moved.", failures);
            Require(result.rigidFrontMaximumError <= PositionTolerance, skin.name + ": front buckle region changed shape.", failures);
            Require(result.maximumInfluences <= 2 || skin.quality == SkinQuality.Bone4,
                skin.name + ": three influences need Bone4 GPU skin quality to prevent upper-control truncation.", failures);
            Require(result.metallicWeightMaximumDifference <= .00001f && result.metallicDisplacementMaximumError <= PositionTolerance,
                skin.name + ": metallic component weights are not uniform, so its original shape can distort.", failures);
            return result;
        }

        static int Index(BoneWeight w, int channel) => channel == 0 ? w.boneIndex0 : channel == 1 ? w.boneIndex1 : channel == 2 ? w.boneIndex2 : w.boneIndex3;
        static float Weight(BoneWeight w, int channel) => channel == 0 ? w.weight0 : channel == 1 ? w.weight1 : channel == 2 ? w.weight2 : w.weight3;
        static float WeightForBone(BoneWeight w, int bone) => (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) +
            (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);

        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
            !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
        static void Require(bool condition, string message, List<string> failures) { if (!condition) failures.Add(message); }
        static float Maximum(SkinEvidence[] parts, Func<SkinEvidence, float> select) => parts.Length > 0 ? parts.Max(select) : 0;
    }
}
