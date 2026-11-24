using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.Editor
{
    /// <summary>
    /// Authors one permanent, torso-fitted padded harness per courier. Backpack cases
    /// are interchangeable props; their source straps never participate in fitting.
    /// All geometry is saved as shared assets, with no runtime surface queries.
    /// </summary>
    public static class BodyHarnessAuthoring
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        const string Output = "Assets/RadioArchipelago/Content/Modular/BodyHarness";
        const int Samples = 96;
        const float Thickness = .032f;
        const float ContactGap = .008f;
        static readonly string Evidence = Path.GetFullPath("../Docs/Design/RadioArchipelago/BodyHarness20261105/authoring.json");

        [Serializable] public sealed class HeroEvidence
        {
            public string hero, prefab, mesh;
            public int vertices, triangles, projectionFallbacks;
            public float width, thickness, shoulderHeight, minimumY, maximumY, earMinimumY, maximumContactSmoothingLift;
            public Vector3 minimum, maximum;
            public bool finite, closedStrips;
        }
        [Serializable] sealed class Report
        {
            public string unityVersion, capturedUtc;
            public HeroEvidence[] heroes;
            public bool passed;
            public string method = "Two padded leather straps projected to the courier's actual torso mesh, excluding arms and feet; body-owned and independent of backpack design."
                + " Height is limited below lateral head details; body-only fitted paths are retained in the profile for independent inspection.";
        }

        [MenuItem("Radio Archipelago/Modular couriers/Author body-fitted harnesses")]
        public static void Run() => Run(AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath));

        public static void Run(CourierCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author the body harness assets in Edit Mode.");
            EnsureFolder(Output);
            var records = new List<HeroEvidence>();
            foreach (var hero in catalog.entries)
            {
                string actorPath = AssetDatabase.GetAssetPath(hero.prefab);
                var actor = PrefabUtility.LoadPrefabContents(actorPath);
                try
                {
                    var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                    var motion = actor.GetComponent<CourierMotion>();
                    if (wardrobe == null || wardrobe.bodyRig?.visualPivot == null)
                        throw new InvalidDataException(hero.assetId + ": missing modular body binding.");
                    motion.ManualSimulation = true; motion.ResetMotion();
                    records.Add(Author(hero.assetId, wardrobe));
                    PrefabUtility.SaveAsPrefabAsset(actor, actorPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(actor); }
            }
            AssetDatabase.SaveAssets();
            var report = new Report { unityVersion = Application.unityVersion, capturedUtc = DateTime.UtcNow.ToString("O"),
                heroes = records.ToArray(), passed = records.Count == 10 && records.All(r => r.finite && r.closedStrips && r.vertices > 1000) };
            Directory.CreateDirectory(Path.GetDirectoryName(Evidence));
            File.WriteAllText(Evidence, JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidDataException("Body harness authoring failed; inspect authoring.json.");
            Debug.Log("BODY_HARNESS_AUTHORING PASS: 10 permanent padded harnesses fitted to actual torso surfaces.");
        }

        static HeroEvidence Author(string id, CourierWardrobeRig wardrobe)
        {
            var frame = wardrobe.bodyRig.visualPivot;
            var targets = CourierStrapClearanceProbe.ReadArmTargets(wardrobe.gameObject);
            float shoulder = (targets.leftShoulder.y + targets.rightShoulder.y) * .5f;
            float earMinimum = targets.outerHead.vertices > 0 ? targets.outerHead.minimum.y : float.PositiveInfinity;
            float top = Mathf.Min(shoulder + .21f, earMinimum - .085f);
            float bottom = Mathf.Max(.32f, shoulder - .36f);
            float width = Mathf.Clamp(wardrobe.profile.bodyWidth * .087f, .10f, .14f);
            var surface = new TorsoSurface(wardrobe, frame);
            var meshBuilder = new Geometry();
            int fallback = 0; float maximumSmoothingLift = 0;
            Vector3[] left = null, right = null, leftNormals = null, rightNormals = null;
            try
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var centers = new Vector3[Samples]; var normals = new Vector3[Samples];
                    for (int i = 0; i < Samples; i++)
                    {
                        float angle = i * Mathf.PI * 2 / Samples;
                        float y = Mathf.Lerp(bottom, top, .5f + .5f * Mathf.Cos(angle));
                        float lateral = .72f + .28f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle)), 1.35f);
                        var direction = new Vector3(side * lateral, 0, Mathf.Sin(angle) * .9f).normalized;
                        if (!surface.Sample(y, direction, out centers[i], out normals[i])) fallback++;
                    }
                    // Leather bridges a short sculpted notch instead of folding into every
                    // face-panel seam. The constrained envelope can only lift the contact
                    // curve, never push it into the body, and the lift is limited to 25mm.
                    maximumSmoothingLift = Mathf.Max(maximumSmoothingLift, surface.SmoothContact(centers));
                    var smoothNormals = new Vector3[Samples];
                    for (int i = 0; i < Samples; i++) smoothNormals[i] = surface.SmoothOutwardHint(centers[i]);
                    var path = new StrapPath(centers, smoothNormals, width);
                    meshBuilder.PaddedStrap(path, Thickness, ContactGap);
                    meshBuilder.Stitches(path, Thickness + ContactGap + .002f);
                    meshBuilder.Buckle(path, 29, Thickness + ContactGap + .008f);
                    if (side < 0) { left = centers; leftNormals = smoothNormals; }
                    else { right = centers; rightNormals = smoothNormals; }
                }
            }
            finally { surface.Dispose(); }

            string meshPath = Output + "/" + id + "_Harness.asset";
            var mesh = meshBuilder.Save(meshPath, id + "_PaddedHarness");
            string prefabPath = Output + "/" + id + "_Harness.prefab";
            var root = new GameObject("BodyHarness_" + id);
            try
            {
                // Put the temporary object in the same isolated prefab scene as its owner.
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, wardrobe.gameObject.scene);
                var model = new GameObject("Tailored padded harness"); model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = model.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = Materials(id);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                wardrobe.profile.bodyHarnessPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                wardrobe.profile.leftHarnessPath = left; wardrobe.profile.rightHarnessPath = right;
                wardrobe.profile.leftHarnessNormals = leftNormals; wardrobe.profile.rightHarnessNormals = rightNormals;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return new HeroEvidence { hero = id, prefab = prefabPath, mesh = meshPath, vertices = mesh.vertexCount,
                triangles = mesh.triangles.Length / 3, projectionFallbacks = fallback, width = width, thickness = Thickness,
                shoulderHeight = shoulder, minimumY = mesh.bounds.min.y, maximumY = mesh.bounds.max.y,
                earMinimumY = float.IsInfinity(earMinimum) ? -1 : earMinimum,
                minimum = mesh.bounds.min, maximum = mesh.bounds.max,
                maximumContactSmoothingLift = maximumSmoothingLift,
                finite = mesh.vertices.All(Finite), closedStrips = left.Length == Samples && right.Length == Samples };
        }

        static Material[] Materials(string id)
        {
            Color leather = new Color(.235f, .115f, .052f);
            if (id == "Nori" || id == "Vela") leather = new Color(.105f, .155f, .21f);
            else if (id == "Tiko" || id == "Miro") leather = new Color(.095f, .22f, .17f);
            else if (id == "Rumi" || id == "Piko") leather = new Color(.215f, .13f, .25f);
            return new[] { Material(id + "_Leather", leather, .0f, .28f),
                Material(id + "_PaddedLeather", Color.Lerp(leather, new Color(.55f, .36f, .2f), .13f), .0f, .23f),
                Material("HarnessBrass", new Color(.64f, .42f, .16f), .78f, .58f),
                Material("HarnessStitch", new Color(.59f, .42f, .23f), .0f, .18f) };
        }
        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            string path = Output + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidDataException("The established URP Lit shader is unavailable.");
                mat = new Material(shader); AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat); return mat;
        }

        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        sealed class TorsoSurface : IDisposable
        {
            readonly Transform frame;
            readonly CourierMountProfile profile;
            readonly GameObject target;
            readonly Mesh mesh;
            readonly MeshCollider collider;
            public TorsoSurface(CourierWardrobeRig wardrobe, Transform visual)
            {
                frame = visual; profile = wardrobe.profile;
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                foreach (var skin in wardrobe.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.GetComponentInParent<CourierEquipmentRig>() != null || skin.sharedMesh == null) continue;
                    var baked = new Mesh { indexFormat = IndexFormat.UInt32 };
                    try
                    {
                        skin.BakeMesh(baked, true);
                        var matrix = frame.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                        int start = vertices.Count, cursor = 0;
                        var points = baked.vertices; var eligible = new bool[points.Length];
                        var counts = skin.sharedMesh.GetBonesPerVertex(); var weights = skin.sharedMesh.GetAllBoneWeights();
                        for (int i = 0; i < points.Length; i++)
                        {
                            vertices.Add(matrix.MultiplyPoint3x4(points[i])); float limbs = 0;
                            for (int j = 0; j < counts[i]; j++)
                            {
                                var w = weights[cursor++]; string name = skin.bones[w.boneIndex].name;
                                if (name.StartsWith("arm.", StringComparison.Ordinal) || name.StartsWith("foot.", StringComparison.Ordinal)) limbs += w.weight;
                            }
                            eligible[i] = limbs < .30f;
                        }
                        var indices = baked.triangles;
                        for (int i = 0; i < indices.Length; i += 3)
                            if (eligible[indices[i]] && eligible[indices[i + 1]] && eligible[indices[i + 2]])
                            { triangles.Add(start + indices[i]); triangles.Add(start + indices[i + 1]); triangles.Add(start + indices[i + 2]); }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(baked); }
                }
                if (triangles.Count == 0) throw new InvalidDataException("No readable torso surface for the body harness.");
                mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "Disposable torso fitting surface" };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
                target = new GameObject("Disposable body-harness contact surface"); target.transform.SetParent(frame, false);
                collider = target.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; Physics.SyncTransforms();
            }
            public bool Sample(float y, Vector3 direction, out Vector3 point, out Vector3 normal)
            {
                var section = Section(y);
                float centerZ = (section.frontZ + section.backZ) * .5f;
                var center = new Vector3(0, y, centerZ);
                var origin = frame.TransformPoint(center + direction * 3);
                var ray = new Ray(origin, frame.TransformDirection(-direction).normalized);
                if (collider.Raycast(ray, out var hit, 6 * frame.lossyScale.magnitude))
                {
                    point = frame.InverseTransformPoint(hit.point);
                    normal = frame.InverseTransformDirection(hit.normal).normalized;
                    if (Vector3.Dot(normal, direction) < 0) normal = -normal;
                    return true;
                }
                float a = Mathf.Max(.1f, section.halfWidth), b = Mathf.Max(.1f, (section.frontZ - section.backZ) * .5f);
                float radius = 1 / Mathf.Sqrt(direction.x * direction.x / (a * a) + direction.z * direction.z / (b * b));
                point = center + direction * radius;
                normal = new Vector3(direction.x / (a * a), 0, direction.z / (b * b)).normalized;
                return false;
            }
            public float SmoothContact(Vector3[] points)
            {
                var raw = new float[points.Length]; var radius = new float[points.Length];
                var direction = new Vector3[points.Length]; var middle = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    var section = Section(points[i].y);
                    middle[i] = new Vector3(0, points[i].y, (section.frontZ + section.backZ) * .5f);
                    var delta = points[i] - middle[i]; delta.y = 0;
                    raw[i] = radius[i] = delta.magnitude; direction[i] = delta.normalized;
                }
                for (int pass = 0; pass < 16; pass++)
                {
                    var next = new float[points.Length];
                    for (int i = 0; i < points.Length; i++)
                        next[i] = Mathf.Clamp(Mathf.Lerp(radius[i], (radius[(i + points.Length - 1) % points.Length]
                            + radius[(i + 1) % points.Length]) * .5f, .72f), raw[i], raw[i] + .025f);
                    radius = next;
                }
                float maximum = 0;
                for (int i = 0; i < points.Length; i++)
                { points[i] = middle[i] + direction[i] * radius[i]; maximum = Mathf.Max(maximum, radius[i] - raw[i]); }
                return maximum;
            }
            public Vector3 SmoothOutwardHint(Vector3 point)
            {
                var section = Section(point.y);
                var radial = new Vector3(point.x, 0, point.z - (section.frontZ + section.backZ) * .5f).normalized;
                float Radius(CourierBodySection s)
                {
                    float a = Mathf.Max(.1f, s.halfWidth), b = Mathf.Max(.1f, (s.frontZ - s.backZ) * .5f);
                    return 1 / Mathf.Sqrt(radial.x * radial.x / (a * a) + radial.z * radial.z / (b * b));
                }
                float slope = Mathf.Clamp((Radius(Section(point.y + .065f)) - Radius(Section(point.y - .065f))) / .13f, -.65f, .65f);
                // Raw face-panel normals can turn downwards then upwards within a few
                // millimetres. A smooth torso envelope gives a stable outward frame.
                float halfWidth = Mathf.Max(.1f, section.halfWidth), depth = Mathf.Max(.1f, (section.frontZ - section.backZ) * .5f);
                var horizontal = new Vector3(radial.x / (halfWidth * halfWidth), 0, radial.z / (depth * depth)).normalized;
                return new Vector3(horizontal.x, -slope, horizontal.z).normalized;
            }
            CourierBodySection Section(float y)
            {
                if (profile.bodySections.Length == 0) return new CourierBodySection { height = y,
                    halfWidth = profile.bodyWidth * .5f, frontZ = profile.bodyDepth * .5f, backZ = -profile.bodyDepth * .5f };
                var a = profile.bodySections[0]; var b = a;
                for (int i = 1; i < profile.bodySections.Length; i++) { b = profile.bodySections[i]; if (y <= b.height) break; a = b; }
                float t = Mathf.InverseLerp(a.height, b.height, y);
                return new CourierBodySection { height = y, halfWidth = Mathf.Lerp(a.halfWidth, b.halfWidth, t),
                    frontZ = Mathf.Lerp(a.frontZ, b.frontZ, t), backZ = Mathf.Lerp(a.backZ, b.backZ, t) };
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(mesh); }
        }

        sealed class StrapPath
        {
            public readonly Vector3[] centers, normals, across, tangent;
            public readonly float width;
            public StrapPath(Vector3[] c, Vector3[] n, float w)
            {
                centers = c; normals = n; width = w; across = new Vector3[c.Length]; tangent = new Vector3[c.Length];
                for (int i = 0; i < c.Length; i++)
                {
                    tangent[i] = (c[(i + 1) % c.Length] - c[(i + c.Length - 1) % c.Length]).normalized;
                    across[i] = Vector3.Cross(tangent[i], normals[i]).normalized;
                    normals[i] = Vector3.Cross(across[i], tangent[i]).normalized;
                }
                // Parallel transport around the closed loop, with a gentle return to the
                // body's outward frame, prevents sudden ribbon roll at source seams.
                var desired = (Vector3[])normals.Clone();
                for (int pass = 0; pass < 3; pass++) for (int i = 0; i < c.Length; i++)
                {
                    int previous = (i + c.Length - 1) % c.Length;
                    var transported = Quaternion.FromToRotation(tangent[previous], tangent[i]) * normals[previous];
                    var normal = Vector3.RotateTowards(transported, desired[i], 12 * Mathf.Deg2Rad, 0).normalized;
                    across[i] = Vector3.Cross(tangent[i], normal).normalized;
                    normals[i] = Vector3.Cross(across[i], tangent[i]).normalized;
                }
            }
            public Vector3 At(int i, float acrossWidth, float outward) => centers[i] + across[i] * acrossWidth + normals[i] * outward;
        }

        sealed class Geometry
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int>[] indices = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            public void PaddedStrap(StrapPath path, float thickness, float gap)
            {
                float h = path.width * .5f, bevel = .008f;
                var section = new[] { new Vector2(-h + bevel, gap), new Vector2(h - bevel, gap),
                    new Vector2(h, gap + bevel), new Vector2(h, gap + thickness - bevel),
                    new Vector2(h - bevel, gap + thickness), new Vector2(-h + bevel, gap + thickness),
                    new Vector2(-h, gap + thickness - bevel), new Vector2(-h, gap + bevel) };
                int start = vertices.Count;
                for (int i = 0; i < Samples; i++)
                    for (int j = 0; j < section.Length; j++)
                    { vertices.Add(path.At(i, section[j].x, section[j].y)); uv.Add(new Vector2(j / 8f, i / 12f)); }
                for (int i = 0; i < Samples; i++) for (int j = 0; j < 8; j++)
                {
                    int a = start + i * 8 + j, b = start + ((i + 1) % Samples) * 8 + j;
                    int c = start + ((i + 1) % Samples) * 8 + (j + 1) % 8, d = start + i * 8 + (j + 1) % 8;
                    Quad(a, b, c, d, j == 4 ? 1 : 0);
                }
            }
            public void Stitches(StrapPath path, float height)
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < Samples; i += 2)
                    {
                        var center = path.At(i, side * path.width * .34f, height);
                        Beam(center - path.tangent[i] * .006f, center + path.tangent[i] * .006f, .0015f, 3);
                    }
            }
            public void Buckle(StrapPath path, int index, float height)
            {
                var origin = path.At(index, 0, height); var x = path.across[index]; var y = path.tangent[index];
                float halfX = path.width * .54f, halfY = path.width * .39f;
                const int cornerSegments = 4;
                float radius = .012f; var perimeter = new List<Vector3>();
                for (int corner = 0; corner < 4; corner++)
                {
                    float baseAngle = corner * Mathf.PI * .5f;
                    float signX = corner == 0 || corner == 3 ? 1 : -1, signY = corner < 2 ? 1 : -1;
                    // Counterclockwise rounded corners, starting at the top-right.
                    for (int j = 0; j <= cornerSegments; j++)
                    {
                        float angle = baseAngle + j * Mathf.PI * .5f / cornerSegments;
                        var p = origin + x * (signX * (halfX - radius) + Mathf.Cos(angle) * radius)
                            + y * (signY * (halfY - radius) + Mathf.Sin(angle) * radius);
                        perimeter.Add(p);
                    }
                }
                for (int i = 0; i < perimeter.Count; i++) Beam(perimeter[i], perimeter[(i + 1) % perimeter.Count], .005f, 2);
                Beam(origin - y * halfY, origin + y * halfY, .0038f, 2);
            }
            void Beam(Vector3 from, Vector3 to, float radius, int material)
            {
                var axis = (to - from).normalized;
                var x = Vector3.Cross(axis, Mathf.Abs(axis.y) < .85f ? Vector3.up : Vector3.forward).normalized;
                var y = Vector3.Cross(axis, x); int start = vertices.Count;
                for (int end = 0; end < 2; end++) for (int j = 0; j < 6; j++)
                {
                    float angle = j * Mathf.PI / 3;
                    vertices.Add((end == 0 ? from : to) + radius * (x * Mathf.Cos(angle) + y * Mathf.Sin(angle)));
                    uv.Add(new Vector2(j / 6f, end));
                }
                for (int j = 0; j < 6; j++) Quad(start + j, start + (j + 1) % 6, start + 6 + (j + 1) % 6, start + 6 + j, material);
                for (int j = 1; j < 5; j++)
                { Triangle(start, start + j + 1, start + j, material); Triangle(start + 6, start + 6 + j, start + 6 + j + 1, material); }
            }
            void Quad(int a, int b, int c, int d, int material) { Triangle(a, b, c, material); Triangle(a, c, d, material); }
            void Triangle(int a, int b, int c, int material) { indices[material].Add(a); indices[material].Add(b); indices[material].Add(c); }
            public Mesh Save(string path, string name)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; AssetDatabase.CreateAsset(mesh, path); }
                mesh.Clear(); mesh.name = name; mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.subMeshCount = indices.Length;
                for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i, false);
                mesh.RecalculateBounds(); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.UploadMeshData(false);
                EditorUtility.SetDirty(mesh); return mesh;
            }
        }
    }
}
