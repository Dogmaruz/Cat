using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Checks the imported paw faces themselves; moving controls alone cannot satisfy these checks.</summary>
    public static class CourierRigidPawChecks
    {
        private const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        private const float Dt = 1f / 120, WeightTolerance = .00001f;
        private static readonly string Output = Path.GetFullPath("Library/RadioRigging/RigidPawReview");

        [MenuItem("Radio Archipelago/Imported couriers/Check rigid paws")]
        public static void RunChecks()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Run rigid paw checks in Edit Mode after compilation.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            Require(catalog != null && catalog.entries != null && catalog.entries.Length == 3,
                "The imported courier catalog must contain all three characters.");
            var notes = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            Directory.CreateDirectory(Output);
            try
            {
                foreach (var entry in catalog.entries)
                {
                    Require(entry != null && entry.prefab != null, "The courier catalog contains an incomplete entry.");
                    var actor = UnityEngine.Object.Instantiate(entry.prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try { CheckCourier(entry.id, actor, notes); }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
                File.WriteAllText(Path.Combine(Output, "rigid-paw-checks.txt"), "PASS\n" + string.Join("\n", notes) + "\n");
                Debug.Log("RADIO_RIGID_PAWS PASS: imported faces, disconnected rigid weights, invariant real edges, signed mirrored four-Hz strokes and paw culling bounds. " + Output);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Output, "rigid-paw-checks.txt"), "FAIL\n" + string.Join("\n", notes) + "\n" + error);
                throw;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private sealed class Skin
        {
            public SkinnedMeshRenderer renderer;
            public Mesh scratch;
            public int[] owner; // 0: body/accessory, 1: exact left shoulder, 2: exact right shoulder.
            public bool[] body;
            public Vector3[] rest;
            public Edge[] edges;
            public int boundaryEdges;
            public readonly int[] vertices = new int[2], faces = new int[2];
        }

        private struct Edge
        {
            public int a, b;
            public float length;
        }

        private static void CheckCourier(string id, GameObject actor, List<string> notes)
        {
            var motion = actor.GetComponentInChildren<CourierMotion>(true);
            Require(motion != null, id + ": no courier motion.");
            var binding = typeof(CourierMotion).GetField("rig", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(motion) as CourierRigBinding;
            Require(binding != null && binding.visualPivot != null && binding.leftArm != null && binding.rightArm != null,
                id + ": missing imported shoulder bindings.");
            Require(binding.leftArm != binding.rightArm, id + ": both paws share one shoulder control.");
            var visual = binding.visualPivot;
            var shoulders = new[] { binding.leftArm, binding.rightArm };
            motion.ManualSimulation = true; motion.ResetMotion();
            var skins = new List<Skin>();
            try
            {
                foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var skin = new Skin { renderer = renderer, scratch = new Mesh { name = "Owned rigid paw geometry sample" } };
                    skins.Add(skin);
                    ReadTopology(id, skin, shoulders);
                    skin.rest = Bake(skin, visual, false);
                }
                Require(skins.Count > 0, id + ": actual imported skinned geometry is required.");
                foreach (var renderer in actor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    Require(mesh == null || IsImportedModel(mesh), id + ": a primitive or generated mesh proxy is present: " + renderer.name);
                }
                var bodyBounds = new Bounds(); bool bodyFound = false;
                foreach (var skin in skins)
                    for (int vertex = 0; vertex < skin.rest.Length; vertex++)
                        if (skin.body[vertex])
                        {
                            if (!bodyFound) { bodyBounds = new Bounds(skin.rest[vertex], Vector3.zero); bodyFound = true; }
                            else bodyBounds.Encapsulate(skin.rest[vertex]);
                        }
                float height = bodyBounds.size.y;
                Require(bodyFound && Finite(height) && height > .0001f, id + ": no finite feet-to-head geometry height.");
                foreach (var skin in skins)
                    for (int edge = 0; edge < skin.edges.Length; edge++)
                    {
                        var value = skin.edges[edge]; value.length = Vector3.Distance(skin.rest[value.a], skin.rest[value.b]);
                        skin.edges[edge] = value;
                    }
                for (int side = 0; side < 2; side++)
                    Require(skins.Sum(skin => skin.vertices[side]) >= 12 && skins.Sum(skin => skin.faces[side]) >= 8,
                        id + ": a shoulder has no meaningful imported paw surface: " + shoulders[side].name);

                var restCentroids = Centroids(skins, skins.Select(skin => skin.rest).ToArray());
                var shoulderPositions = shoulders.Select(shoulder => visual.InverseTransformPoint(shoulder.position)).ToArray();
                var sideSigns = shoulderPositions.Select(position => Mathf.Sign(position.x - bodyBounds.center.x)).ToArray();
                Require(sideSigns[0] * sideSigns[1] < 0, id + ": shoulder controls must be on opposite body sides.");
                var restElevation = new float[2];
                for (int side = 0; side < 2; side++)
                {
                    Vector3 reach = restCentroids[side] - shoulderPositions[side];
                    Require(new Vector2(reach.x, reach.y).magnitude > height * .005f,
                        id + ": paw centroid is too close to the shoulder to measure a real stroke.");
                    restElevation[side] = Elevation(reach, sideSigns[side]);
                }

                motion.PlayJump(2.4f); motion.SetFlightProgress(.5f);
                for (int frame = 0; frame < 60; frame++) motion.SimulateSprings(Dt);
                var minimum = new[] { float.PositiveInfinity, float.PositiveInfinity };
                var maximum = new[] { float.NegativeInfinity, float.NegativeInfinity };
                var low = new Vector3[2]; var high = new Vector3[2];
                var previous = new float[2]; var previousSlope = new float[2]; var peaks = new int[2];
                float maximumEdgeError = 0, maximumMirrorError = 0;
                // One complete second contains four complete strokes, including the lowered
                // and raised states. The visual frame removes global bob, turn and squash.
                for (int frame = 0; frame <= 120; frame++)
                {
                    motion.SimulateSprings(Dt);
                    var posed = skins.Select(skin => Bake(skin, visual, true)).ToArray();
                    var centroid = Centroids(skins, posed); var delta = new float[2];
                    for (int skinIndex = 0; skinIndex < skins.Count; skinIndex++)
                        foreach (var edge in skins[skinIndex].edges)
                        {
                            if (edge.length < height * .000001f) continue; // UV duplicates and zero-area cap edges.
                            float length = Vector3.Distance(posed[skinIndex][edge.a], posed[skinIndex][edge.b]);
                            float error = Mathf.Abs(length - edge.length);
                            maximumEdgeError = Mathf.Max(maximumEdgeError, error / edge.length);
                            Require(error <= edge.length * .002f + height * .000002f,
                                id + ": actual paw edge stretches under its shoulder: " + skins[skinIndex].renderer.name
                                + " vertices " + edge.a + "/" + edge.b + ", relative error=" + Number(error / edge.length));
                        }
                    for (int side = 0; side < 2; side++)
                    {
                        Vector3 shoulder = visual.InverseTransformPoint(shoulders[side].position);
                        Require(Vector3.Distance(shoulder, shoulderPositions[side]) < height * .00001f,
                            id + ": the shoulder attachment translates during a stroke: " + shoulders[side].name);
                        delta[side] = Mathf.DeltaAngle(restElevation[side], Elevation(centroid[side] - shoulder, sideSigns[side]));
                        if (delta[side] < minimum[side]) { minimum[side] = delta[side]; low[side] = centroid[side]; }
                        if (delta[side] > maximum[side]) { maximum[side] = delta[side]; high[side] = centroid[side]; }
                        float slope = delta[side] - previous[side];
                        if (frame > 1 && previousSlope[side] > 0 && slope <= 0 && previous[side] > 35) peaks[side]++;
                        previous[side] = delta[side]; previousSlope[side] = slope;
                    }
                    maximumMirrorError = Mathf.Max(maximumMirrorError, Mathf.Abs(delta[0] - delta[1]));
                    Require(Mathf.Abs(delta[0] - delta[1]) < 12,
                        id + ": actual paw strokes are not signed mirrors; elevations=" + Number(delta[0]) + "/" + Number(delta[1]));
                    Require(delta[0] * delta[1] >= 0 || Mathf.Max(Mathf.Abs(delta[0]), Mathf.Abs(delta[1])) < 6,
                        id + ": one real paw rises while the other falls.");
                }
                for (int side = 0; side < 2; side++)
                {
                    Require(peaks[side] == 4, id + ": actual paw geometry needs four raised strokes per second: " + shoulders[side].name + " peaks=" + peaks[side]);
                    Require(minimum[side] < -4 && maximum[side] > 42 && maximum[side] - minimum[side] > 45,
                        id + ": actual paw geometry does not cover the lowered and raised stroke: " + shoulders[side].name
                        + " range=" + Number(minimum[side]) + ".." + Number(maximum[side]));
                    Require(Vector3.Distance(low[side], high[side]) > height * .02f,
                        id + ": actual paw excursion is too small relative to body height: " + shoulders[side].name);
                }
                notes.Add("PASS " + id + ": rigid paw vertices=" + skins.Sum(skin => skin.vertices[0]) + "/" + skins.Sum(skin => skin.vertices[1])
                    + "; faces=" + skins.Sum(skin => skin.faces[0]) + "/" + skins.Sum(skin => skin.faces[1])
                    + "; exact single-shoulder weights; no triangle bridges; maximum edge relative error=" + Number(maximumEdgeError)
                    + "; signed elevation ranges=" + Number(minimum[0]) + ".." + Number(maximum[0]) + "/" + Number(minimum[1]) + ".." + Number(maximum[1])
                    + "; mirror error=" + Number(maximumMirrorError) + "deg; four actual strokes per side; culling PASS"
                    + "; raw boundary edges=" + skins.Sum(skin => skin.boundaryEdges)
                    + " (diagnostic only: UV/normal splits prevent a fair raw-index closedness assertion).");
            }
            finally { foreach (var skin in skins) if (skin.scratch != null) UnityEngine.Object.DestroyImmediate(skin.scratch); }
        }

        private static void ReadTopology(string id, Skin skin, Transform[] shoulders)
        {
            var mesh = skin.renderer.sharedMesh;
            Require(mesh != null && mesh.isReadable && IsImportedModel(mesh),
                id + ": paw checks need readable, supplied FBX geometry: " + skin.renderer.name);
            var uv = mesh.uv;
            Require(uv.Length == mesh.vertexCount && uv.All(value => Finite(value.x) && Finite(value.y)),
                id + ": imported source UVs are missing or invalid: " + skin.renderer.name);
            var bones = skin.renderer.bones;
            Require(bones != null && bones.Length > 0 && bones.All(bone => bone != null), id + ": invalid skin bones.");
            var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights();
            Require(counts.Length == mesh.vertexCount, id + ": incomplete imported weights.");
            skin.owner = new int[mesh.vertexCount]; skin.body = new bool[mesh.vertexCount];
            int cursor = 0;
            for (int vertex = 0; vertex < counts.Length; vertex++)
            {
                float sum = 0, body = 0; var arm = new float[2]; var exact = new float[2];
                for (int influence = 0; influence < counts[vertex]; influence++)
                {
                    Require(cursor < weights.Length, id + ": truncated imported weights.");
                    var weight = weights[cursor++];
                    Require(Finite(weight.weight) && weight.weight >= 0 && weight.boneIndex >= 0 && weight.boneIndex < bones.Length,
                        id + ": invalid imported influence.");
                    var bone = bones[weight.boneIndex]; sum += weight.weight;
                    if (BodyBone(bone.name)) body += weight.weight;
                    for (int side = 0; side < 2; side++)
                    {
                        if (bone == shoulders[side] || bone.IsChildOf(shoulders[side])) arm[side] += weight.weight;
                        if (bone == shoulders[side]) exact[side] += weight.weight;
                    }
                }
                Require(Mathf.Abs(sum - 1) <= .0001f, id + ": unnormalized imported vertex weights.");
                skin.body[vertex] = body >= .5f;
                for (int side = 0; side < 2; side++)
                    if (arm[side] > WeightTolerance)
                    {
                        Require(Mathf.Abs(exact[side] - 1) <= WeightTolerance && sum - exact[side] <= WeightTolerance,
                            id + ": paw vertex must have 100% weight on its exact shoulder, with no fractional body/other influence: "
                            + skin.renderer.name + " vertex=" + vertex + " shoulder=" + shoulders[side].name + " weight=" + Number(exact[side]));
                        skin.owner[vertex] = side + 1; skin.vertices[side]++;
                    }
            }
            Require(cursor == weights.Length, id + ": trailing imported weight records.");
            var edges = new Dictionary<ulong, int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                Require(mesh.GetTopology(sub) == MeshTopology.Triangles, id + ": expected imported triangle faces.");
                int[] triangles = mesh.GetTriangles(sub);
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    int a = triangles[triangle], b = triangles[triangle + 1], c = triangles[triangle + 2];
                    int owner = skin.owner[a];
                    Require(owner == skin.owner[b] && owner == skin.owner[c],
                        id + ": a triangle bridges the rigid paw and body/opposite paw: " + skin.renderer.name + " face=" + triangle / 3);
                    if (owner == 0) continue;
                    skin.faces[owner - 1]++;
                    AddEdge(edges, a, b); AddEdge(edges, b, c); AddEdge(edges, c, a);
                }
            }
            skin.boundaryEdges = edges.Count(pair => pair.Value == 1);
            skin.edges = edges.Keys.Select(value => new Edge { a = (int)(value >> 32), b = (int)(value & uint.MaxValue) }).ToArray();
        }

        private static void AddEdge(Dictionary<ulong, int> edges, int a, int b)
        {
            ulong key = ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            edges.TryGetValue(key, out int count); edges[key] = count + 1;
        }

        private static Vector3[] Bake(Skin skin, Transform visual, bool checkCulling)
        {
            // In this Editor version useScale=true compensates for renderer scale, so
            // TransformPoint applies it once and the visual inverse removes body squash.
            skin.renderer.BakeMesh(skin.scratch, true);
            var vertices = skin.scratch.vertices;
            Require(vertices.Length == skin.owner.Length, "A deformed mesh changed its imported topology.");
            var bounds = skin.renderer.bounds; bounds.Expand(.002f);
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                Vector3 world = skin.renderer.transform.TransformPoint(vertices[vertex]);
                if (checkCulling && skin.owner[vertex] != 0)
                    Require(bounds.Contains(world), skin.renderer.name + ": actual paw geometry leaves renderer culling bounds at a stroke.");
                vertices[vertex] = visual.InverseTransformPoint(world);
                Require(Finite(vertices[vertex].x) && Finite(vertices[vertex].y) && Finite(vertices[vertex].z), "A deformed vertex is not finite.");
            }
            return vertices;
        }

        private static Vector3[] Centroids(List<Skin> skins, Vector3[][] vertices)
        {
            var sum = new Vector3[2]; var count = new int[2];
            for (int skin = 0; skin < skins.Count; skin++)
                for (int vertex = 0; vertex < vertices[skin].Length; vertex++)
                {
                    int side = skins[skin].owner[vertex] - 1;
                    if (side < 0) continue;
                    sum[side] += vertices[skin][vertex]; count[side]++;
                }
            for (int side = 0; side < 2; side++)
            { Require(count[side] > 0, "Missing actual rigid paw vertices."); sum[side] /= count[side]; }
            return sum;
        }

        private static bool IsImportedModel(Mesh mesh)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            return path.StartsWith("Assets/RadioArchipelago/Imported/", StringComparison.Ordinal)
                && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }
        private static bool BodyBone(string name) => name == "root" || name == "body" || name == "head"
            || name == "arm.L" || name == "arm.R" || name == "foot.L" || name == "foot.R";
        private static float Elevation(Vector3 reach, float side) => Mathf.Atan2(reach.y, reach.x * side) * Mathf.Rad2Deg;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Number(float value) => value.ToString("F5", CultureInfo.InvariantCulture);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("RADIO_RIGID_PAWS FAIL: " + message); }
    }
}
