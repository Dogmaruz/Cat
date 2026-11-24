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
    /// <summary>Read-only equipment-switch and fit evidence for the body-owned harness.</summary>
    public static class BodyHarnessChecks
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        public static string EvidenceFolder => Path.GetFullPath("../Docs/Design/RadioArchipelago/BodyHarness20261105");
        const float PoseTolerance = .0001f;

        [Serializable] public sealed class Distribution
        {
            public float minimum, median, p95, maximum;
        }
        [Serializable] public sealed class FitEvidence
        {
            public string hero;
            public int harnessVertices, harnessTriangles, harnessSubmeshes, routeSamples, surfaceHits, surfaceMisses;
            public int nonfiniteVertices, degenerateTriangles, openEdges, nonManifoldEdges;
            public int outerHeadBoundingBoxCandidates;
            public float maximumHarnessHeight, outerHeadMinimumHeight;
            public Distribution routeToTorsoSurface;
            public bool noItemHidesHarness;
        }
        [Serializable] public sealed class PairEvidence
        {
            public string hero, backpack;
            public bool passed, harnessObjectPersisted, harnessGeometryUnchanged, harnessVisualPoseUnchanged;
            public float caseHeight, caseWidth, maximumHarnessPoseDelta, sweptTopMinimum;
            public int oldStrapNodes, sourceColliders, equippedColliders;
            public string[] failures;
        }
        [Serializable] public sealed class Report
        {
            public string capturedUtc, unityVersion;
            public bool passed, activeScenePreserved;
            public int heroes, backpacks, pairs;
            public FitEvidence[] fits;
            public PairEvidence[] observations;
            public string limitation = "Automatic checks prove stable body-owned geometry, strapless named source hierarchy, case scale and valid mechanisms. Route-to-torso distances and lateral head bounding-box candidates are diagnostic geometry samples, not a signed full-body collision or artistic-quality certification. Case-only images must also be reviewed for integrated remnants.";
        }
        sealed class Snapshot
        {
            public GameObject root;
            public Mesh[] meshes;
            public Material[][] materials;
            public Matrix4x4[] poses;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Body-harness checks run only in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("The courier catalog is missing.");
            var active = SceneManager.GetActiveScene();
            bool wasDirty = active.isDirty;
            var scene = EditorSceneManager.NewPreviewScene();
            var pairs = new List<PairEvidence>();
            var fits = new List<FitEvidence>();
            var packs = catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Backpack).ToArray();
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
                        motion.ResetMotion();
                        Snapshot reference = null;
                        foreach (var pack in packs)
                        {
                            var pair = new PairEvidence { hero = hero.assetId, backpack = pack.id };
                            var failures = new List<string>();
                            try
                            {
                                motion.ResetMotion(); wardrobe.Apply(pack, null, null); motion.ResetMotion();
                                var gear = wardrobe.Equipped(CourierEquipmentKind.Backpack);
                                var harness = wardrobe.BodyHarness;
                                var rig = gear != null ? gear.GetComponent<CourierEquipmentRig>() : null;
                                Require(wardrobe.profile.bodyHarnessPrefab != null && harness != null && harness.activeInHierarchy,
                                    "The equipped backpack must use this hero's separate body harness.", failures);
                                Require(gear != null && rig != null, "Backpack rig is missing.", failures);
                                if (gear == null || rig == null || harness == null) throw new InvalidDataException("Incomplete equipment instance.");
                                var frame = wardrobe.bodyRig.visualPivot;
                                pair.oldStrapNodes = OldStrapNodes(pack.prefab) + OldStrapNodes(gear);
                                Require(pair.oldStrapNodes == 0, "Original backpack strap or strap-control nodes remain.", failures);
                                Require(!harness.transform.IsChildOf(gear.transform), "The fixed harness is still owned by the selected backpack.", failures);
                                Require(harness.GetComponentsInChildren<Collider>(true).Length == 0,
                                    "The visual-only body harness contains a collider.", failures);
                                Require(harness.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0,
                                    "The body harness must not depend on the removed per-backpack strap skeleton.", failures);
                                var now = ReadSnapshot(harness, frame);
                                Require(now.meshes.Length > 0 && now.meshes.All(m => m != null && m.vertexCount > 0),
                                    "Body harness has no authored mesh.", failures);
                                if (reference == null) reference = now;
                                pair.harnessObjectPersisted = now.root == reference.root;
                                pair.harnessGeometryUnchanged = now.meshes.SequenceEqual(reference.meshes) &&
                                    now.materials.Length == reference.materials.Length &&
                                    now.materials.Select((m, i) => m.SequenceEqual(reference.materials[i])).All(equal => equal);
                                pair.maximumHarnessPoseDelta = MaximumDelta(reference.poses, now.poses);
                                pair.harnessVisualPoseUnchanged = pair.maximumHarnessPoseDelta < PoseTolerance;
                                Require(pair.harnessGeometryUnchanged && pair.harnessVisualPoseUnchanged,
                                    "Changing backpack changed the hero's harness geometry, materials or placement.", failures);
                                var equipmentToVisual = frame.worldToLocalMatrix * gear.transform.localToWorldMatrix;
                                pair.caseHeight = equipmentToVisual.MultiplyVector(Vector3.up * (rig.caseBoundsMax.y - rig.caseBoundsMin.y)).magnitude;
                                pair.caseWidth = equipmentToVisual.MultiplyVector(Vector3.right * (rig.caseBoundsMax.x - rig.caseBoundsMin.x)).magnitude;
                                bool reduced = pack.id == "backpack_Boro" || pack.id == "backpack_Miro" || pack.id == "backpack_Miro_Garden" ||
                                    pack.id == "backpack_Piko_Voyager" || pack.id == "backpack_Vela_Cloud";
                                Require(reduced ? Mathf.Abs(pair.caseWidth - 1.22f) < .0002f : Mathf.Abs(pair.caseHeight - 1.18f) < .0002f,
                                    reduced ? "A selected wide case does not match its 1.22-unit target width." : "An untouched backpack lost its 1.18-unit case height.", failures);
                                pair.sourceColliders = pack.prefab.GetComponentsInChildren<Collider>(true).Length;
                                pair.equippedColliders = gear.GetComponentsInChildren<Collider>(true).Length;
                                Require(pair.sourceColliders == 0 && pair.equippedColliders == 0, "Visual equipment contains a collider.", failures);
                                Require(rig.topPivot != null && rig.topRotor != null && rig.topRotor.IsChildOf(rig.topPivot),
                                    "The backpack's separate movable top mechanism is missing.", failures);
                                Require(wardrobe.bodyRig.antennaYawPivot == rig.topPivot && wardrobe.bodyRig.antennaChain != null &&
                                    wardrobe.bodyRig.antennaChain.Length > 0 && wardrobe.bodyRig.antennaChain.All(t => t != null),
                                    "The original character-motion system is not bound to the equipped mechanism.", failures);
                                Require(Finite(rig.spinAxis) && rig.spinAxis.sqrMagnitude > .001f, "The mechanism has no valid rotation axis.", failures);
                                pair.sweptTopMinimum = rig.SweptTopMinimum;
                                Require(Finite(pair.sweptTopMinimum) && pair.sweptTopMinimum >= wardrobe.profile.bodyHeight + .115f,
                                    "The mechanism rotation sweep crosses the authored head-top clearance.", failures);
                            }
                            catch (Exception error) { failures.Add(error.GetType().Name + ": " + error.Message); }
                            pair.failures = failures.ToArray(); pair.passed = failures.Count == 0; pairs.Add(pair);
                        }
                        var fit = MeasureFit(actor, hero.assetId, scene);
                        wardrobe.Apply(null, null, null);
                        fit.noItemHidesHarness = wardrobe.BodyHarness == null || !wardrobe.BodyHarness.activeInHierarchy;
                        fits.Add(fit);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var report = new Report
            {
                capturedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                activeScenePreserved = active == SceneManager.GetActiveScene() && active.isDirty == wasDirty,
                heroes = catalog.entries.Length, backpacks = packs.Length, pairs = pairs.Count,
                fits = fits.ToArray(), observations = pairs.ToArray()
            };
            report.passed = report.heroes == 10 && report.backpacks == 20 && report.pairs == 200 &&
                pairs.All(p => p.passed) && fits.All(f => f.noItemHidesHarness && f.nonfiniteVertices == 0 &&
                    f.degenerateTriangles == 0 && f.openEdges == 0 && f.nonManifoldEdges == 0) && report.activeScenePreserved;
            Directory.CreateDirectory(EvidenceFolder);
            File.WriteAllText(Path.Combine(EvidenceFolder, "checks.json"), JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidDataException("Body-harness checks failed; inspect " + Path.Combine(EvidenceFolder, "checks.json"));
            Debug.Log("BODY_HARNESS_CHECKS PASS: 200 backpack switches, 10 fixed body harnesses, strapless case hierarchy, unchanged case size and movable top bindings; active scene preserved.");
        }

        static FitEvidence MeasureFit(GameObject actor, string hero, Scene scene)
        {
            var wardrobe = actor.GetComponent<CourierWardrobeRig>();
            var frame = wardrobe.bodyRig.visualPivot;
            var harness = wardrobe.BodyHarness;
            var result = new FitEvidence { hero = hero, maximumHarnessHeight = float.NegativeInfinity, outerHeadMinimumHeight = -1 };
            if (harness == null) return result;
            var targets = CourierStrapClearanceProbe.ReadArmTargets(actor);
            if (targets.outerHead.vertices > 0) result.outerHeadMinimumHeight = targets.outerHead.minimum.y;
            foreach (var filter in harness.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh; if (mesh == null) continue;
                result.harnessVertices += mesh.vertexCount; result.harnessTriangles += mesh.triangles.Length / 3;
                result.harnessSubmeshes += mesh.subMeshCount;
                var matrix = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices; var triangles = mesh.triangles;
                var edges = new Dictionary<ulong, int>();
                foreach (var vertex in vertices)
                {
                    var point = matrix.MultiplyPoint3x4(vertex);
                    if (!Finite(point)) result.nonfiniteVertices++;
                    result.maximumHarnessHeight = Mathf.Max(result.maximumHarnessHeight, point.y);
                    if (targets.outerHead.vertices > 0 && Mathf.Abs(point.x) >= Mathf.Min(Mathf.Abs(targets.outerHead.minimum.x), Mathf.Abs(targets.outerHead.maximum.x)) &&
                        point.y >= targets.outerHead.minimum.y && point.y <= targets.outerHead.maximum.y &&
                        point.z >= targets.outerHead.minimum.z && point.z <= targets.outerHead.maximum.z)
                        result.outerHeadBoundingBoxCandidates++;
                }
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude < 1e-18f)
                        result.degenerateTriangles++;
                    Edge(a, b); Edge(b, c); Edge(c, a);
                }
                result.openEdges += edges.Values.Count(count => count == 1);
                result.nonManifoldEdges += edges.Values.Count(count => count > 2);
                void Edge(int a, int b)
                {
                    uint lo = (uint)Mathf.Min(a, b), hi = (uint)Mathf.Max(a, b);
                    ulong key = ((ulong)lo << 32) | hi;
                    edges.TryGetValue(key, out int count); edges[key] = count + 1;
                }
            }
            var torso = ReadTorsoSurface(actor, frame);
            var node = new GameObject("Disposable harness fit surface");
            SceneManager.MoveGameObjectToScene(node, scene); node.transform.SetParent(frame, false);
            var collider = node.AddComponent<MeshCollider>(); collider.sharedMesh = torso;
            var distances = new List<float>();
            try
            {
                Physics.SyncTransforms();
                Sample(wardrobe.profile.leftHarnessPath, wardrobe.profile.leftHarnessNormals);
                Sample(wardrobe.profile.rightHarnessPath, wardrobe.profile.rightHarnessNormals);
                result.routeToTorsoSurface = Quantiles(distances);
            }
            finally { UnityEngine.Object.DestroyImmediate(node); UnityEngine.Object.DestroyImmediate(torso); }
            return result;
            void Sample(Vector3[] path, Vector3[] normals)
            {
                if (path == null || normals == null) return;
                for (int i = 0; i < Mathf.Min(path.Length, normals.Length); i++)
                {
                    result.routeSamples++;
                    var n = normals[i].normalized;
                    var start = frame.TransformPoint(path[i] + n * .15f);
                    var end = frame.TransformPoint(path[i] - n * .25f);
                    var direction = end - start;
                    if (collider.Raycast(new Ray(start, direction.normalized), out var hit, direction.magnitude))
                    {
                        result.surfaceHits++;
                        distances.Add(Vector3.Dot(path[i] - frame.InverseTransformPoint(hit.point), n));
                    }
                    else result.surfaceMisses++;
                }
            }
        }

        static Mesh ReadTorsoSurface(GameObject actor, Transform frame)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null || skin.GetComponentInParent<CourierEquipmentRig>() != null) continue;
                var scratch = new Mesh { indexFormat = skin.sharedMesh.indexFormat };
                try
                {
                    skin.BakeMesh(scratch, true); var points = scratch.vertices;
                    var counts = skin.sharedMesh.GetBonesPerVertex(); var weights = skin.sharedMesh.GetAllBoneWeights();
                    var excluded = new bool[points.Length]; int cursor = 0;
                    for (int i = 0; i < points.Length; i++)
                    {
                        float arm = 0;
                        for (int j = 0; j < counts[i]; j++)
                        {
                            var w = weights[cursor++]; var bone = skin.bones[w.boneIndex];
                            if (bone != null && (bone.name == "arm.L" || bone.name == "arm.R")) arm += w.weight;
                        }
                        excluded[i] = arm > .35f;
                    }
                    int offset = vertices.Count; var matrix = frame.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                    foreach (var point in points) vertices.Add(matrix.MultiplyPoint3x4(point));
                    var source = scratch.triangles;
                    for (int i = 0; i < source.Length; i += 3)
                        if (!excluded[source[i]] && !excluded[source[i + 1]] && !excluded[source[i + 2]])
                        { triangles.Add(offset + source[i]); triangles.Add(offset + source[i + 1]); triangles.Add(offset + source[i + 2]); }
                }
                finally { UnityEngine.Object.DestroyImmediate(scratch); }
            }
            var mesh = new Mesh { name = "Disposable torso-only fit surface", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }
        static Snapshot ReadSnapshot(GameObject harness, Transform frame)
        {
            var filters = harness.GetComponentsInChildren<MeshFilter>(true);
            return new Snapshot
            {
                root = harness, meshes = filters.Select(m => m.sharedMesh).ToArray(),
                materials = filters.Select(m => m.GetComponent<Renderer>()?.sharedMaterials ?? Array.Empty<Material>()).ToArray(),
                poses = filters.Select(m => frame.worldToLocalMatrix * m.transform.localToWorldMatrix).ToArray()
            };
        }
        static int OldStrapNodes(GameObject root) => root == null ? 0 : root.GetComponentsInChildren<Transform>(true).Count(t =>
            t.name.StartsWith("Harness", StringComparison.OrdinalIgnoreCase) || t.name.StartsWith("StrapFront", StringComparison.OrdinalIgnoreCase) ||
            t.name.StartsWith("StrapUpper", StringComparison.OrdinalIgnoreCase) || t.name.StartsWith("OriginalHarness", StringComparison.OrdinalIgnoreCase));
        static float MaximumDelta(Matrix4x4[] left, Matrix4x4[] right)
        {
            if (left.Length != right.Length) return float.PositiveInfinity;
            float maximum = 0;
            for (int i = 0; i < left.Length; i++) for (int element = 0; element < 16; element++)
                maximum = Mathf.Max(maximum, Mathf.Abs(left[i][element] - right[i][element]));
            return maximum;
        }
        static Distribution Quantiles(List<float> values)
        {
            if (values.Count == 0) return new Distribution();
            values.Sort();
            return new Distribution { minimum = values[0], median = At(.5f), p95 = At(.95f), maximum = values[values.Count - 1] };
            float At(float t) => values[Mathf.Clamp(Mathf.RoundToInt((values.Count - 1) * t), 0, values.Count - 1)];
        }
        static void Require(bool condition, string message, List<string> failures) { if (!condition) failures.Add(message); }
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
