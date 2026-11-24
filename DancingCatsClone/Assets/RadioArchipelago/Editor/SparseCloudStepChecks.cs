using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Isolated proof of large cloud geometry, spacing, placement and lifecycle.</summary>
    public static class SparseCloudStepChecks
    {
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Report
        {
            public bool pass, defaultLargeLayout, narrowWindows, invalidWindows, chartLandingAnchors, cleanupComplete;
            public int legacyCount, sparseCount, sharedSparseMeshes;
            public float minimumGap = float.MaxValue, minimumClearance = float.MaxValue, sizeRatio;
            public long warmedTickAllocatedBytes;
        }

        [MenuItem("Tools/Radio Archipelago/Check sparse cloud steps")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Sparse cloud checks require stable Edit Mode.");
            var flag = typeof(IslandStageDefinition).GetField("SparseCloudSteps");
            var ranges = typeof(IslandStageDefinition).GetField("CloudStepRanges");
            Require(flag != null && flag.FieldType == typeof(bool), "SPARSE_CLOUD_STEPS RED: missing opt-in SparseCloudSteps bool.");
            Require(ranges != null && ranges.FieldType == typeof(Vector2[]), "Missing authored CloudStepRanges windows.");
            var owner = new GameObject("Isolated sparse cloud steps check") { hideFlags = HideFlags.HideAndDontSave };
            var stage = ScriptableObject.CreateInstance<IslandStageDefinition>();
            var report = new Report();
            TextAsset chart = null;
            try
            {
                Require(!(bool)flag.GetValue(stage), "New stages must retain the serialized default flag.");
                Require(((Vector2[])ranges.GetValue(stage)).Length == 0, "CloudStepRanges must default to an empty array.");
                var points = new[] { Vector3.zero, new Vector3(0, 0, 600), new Vector3(200, 0, 600), new Vector3(200, 0, 0) };
                var flat = new IslandRoute(points);
                var nature = owner.AddComponent<IslandLivingNature>();
                nature.Configure(stage, flat);
                CheckLegacy(nature, report);
                CheckSparse(nature, stage, flat, new[] { new Vector2(432, 460) }, report);
                nature.Dispose();
                flag.SetValue(stage, true);
                nature.Configure(stage, flat);
                report.sparseCount = nature.CloudBankCount;
                Require(report.sparseCount == 2, "A 28m crossing should use two substantial detached clouds.");
                CheckSparse(nature, stage, flat, new[] { new Vector2(432, 460) }, report);
                var rootsBefore = owner.GetComponentsInChildren<Transform>(true);
                nature.Configure(stage, flat);
                Require(rootsBefore.Length == owner.GetComponentsInChildren<Transform>(true).Length,
                    "Repeated Configure must keep the same pool.");
                for (int i = 0; i < 20; i++) nature.UpdatePresentation(445, .016f);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 2000; i++) nature.UpdatePresentation(442 + i % 12, .016f);
                report.warmedTickAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(report.warmedTickAllocatedBytes == 0, "Sparse animation must allocate zero bytes after warmup.");
                Require(owner.GetComponentsInChildren<Collider>(true).Length == 0 &&
                    owner.GetComponentsInChildren<Rigidbody>(true).Length == 0 &&
                    owner.GetComponentsInChildren<AudioSource>(true).Length == 0, "Clouds must remain visual only.");
                nature.Dispose();
                var windows = new[] { new Vector2(445.75f, 458.25f), new Vector2(471.75f, 479.25f),
                    new Vector2(499.25f, 509.5f), new Vector2(523.5f, 546.75f) };
                ranges.SetValue(stage, windows);
                var elevated = new IslandRoute(points, new IslandElevationProfile(flat.Length, true, new[] {
                    new IslandElevationProfile.Key(0, 0), new IslandElevationProfile.Key(420, 4),
                    new IslandElevationProfile.Key(465, 12), new IslandElevationProfile.Key(550, 7),
                    new IslandElevationProfile.Key(600, 7), new IslandElevationProfile.Key(flat.Length, 0) }));
                nature.Configure(stage, elevated);
                CheckSparse(nature, stage, elevated, windows, report);
                Require(CountInside(nature, windows[0]) == 1 && CountInside(nature, windows[1]) == 1 && CountInside(nature, windows[2]) == 1,
                    "Each short crossing should hold one cloud with its full five-times-larger size.");
                Require(CountInside(nature, windows[3]) == 2 && nature.CloudBankCount == 5,
                    "The longer crossing should hold two separated clouds, exposing all five forms and sizes.");
                report.narrowWindows = true;
                nature.Dispose();

                // Real immutable chart coordinates guide an otherwise decorative pool.
                chart = new TextAsset(JsonUtility.ToJson(new MapTimeline { elements = new[] {
                    new MapElement { id = 0, pos = new Vector3(-.8f, 0, 439) },
                    new MapElement { id = 1, pos = new Vector3(.9f, 0, 453) } } }));
                stage.Chart = chart; stage.DistanceScale = 1; stage.LateralScale = 2;
                ranges.SetValue(stage, Array.Empty<Vector2>());
                nature.Configure(stage, flat);
                Require(nature.CloudBankCount == 2, "Chart alignment must retain the two separated large volumes.");
                var entries = Entries(nature);
                for (int i = 0; i < entries.Length; i++)
                {
                    float distance = Value<float>(entries.GetValue(i), "distance");
                    Transform root = Value<Transform>(entries.GetValue(i), "root");
                    var element = JsonUtility.FromJson<MapTimeline>(chart.text).elements[i];
                    Require(Mathf.Abs(distance - element.pos.z) < .001f, "Cloud must anchor to the actual ordinary landing.");
                    IslandRoute.Frame frame = flat.Evaluate(distance);
                    float lateral = Vector3.Dot(root.position - frame.Position, frame.Right);
                    Require(Mathf.Abs(lateral - element.pos.x * stage.LateralScale) < .001f,
                        "Cloud must follow the landing lane rather than a centre ribbon.");
                }
                report.chartLandingAnchors = true;
                nature.Dispose(); stage.Chart = null;
                ranges.SetValue(stage, new[] { new Vector2(float.NaN, 2), new Vector2(8, 3), new Vector2(-5, 2),
                    new Vector2(10, float.PositiveInfinity), new Vector2(flat.Length - 1, flat.Length + 20) });
                nature.Configure(stage, flat);
                Require(nature.CloudBankCount == 0, "Invalid authored windows must not create invalid geometry or silently use another range.");
                nature.Dispose();
                ranges.SetValue(stage, new[] { new Vector2(432, 460), new Vector2(440, 465) });
                nature.Configure(stage, flat);
                CheckSparse(nature, stage, flat, new[] { new Vector2(432, 465) }, report);
                nature.Dispose();
                report.invalidWindows = true;
                report.cleanupComplete = !nature.IsConfigured && owner.transform.childCount == 0 && nature.CloudBankCount == 0;
                Require(report.cleanupComplete, "Dispose must remove every cloud resource and permit reconfiguration.");
                report.pass = true;
                string folder = Path.GetFullPath("Library/RadioArchipelago/SparseCloudSteps"); Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "sparse-cloud-checks.json"), JsonUtility.ToJson(report, true));
                Debug.Log("SPARSE_CLOUD_STEPS PASS: " + JsonUtility.ToJson(report));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(stage);
                if (chart != null) UnityEngine.Object.DestroyImmediate(chart);
            }
        }

        private static void CheckLegacy(IslandLivingNature nature, Report report)
        {
            var entries = Entries(nature); report.legacyCount = entries.Length;
            Require(entries.Length == 2, "The default 28m crossing must use two substantial separated clouds.");
            float[] expectedScales = { 6.1f, 4.4f };
            for (int i = 0; i < entries.Length; i++)
            {
                object item = entries.GetValue(i); Transform root = Value<Transform>(item, "root");
                Require((root.localScale - Vector3.one * expectedScales[i]).sqrMagnitude < .00000001f,
                    "Default clouds must retain their full uniform five-times-larger scale.");
                var filter = root.GetComponentInChildren<MeshFilter>(true);
                var mesh = filter.sharedMesh;
                Require(mesh.vertexCount > 1000 && mesh.triangles.Length / 3 <= 16000, "Refreshed cloud shell must have a bounded smooth topology.");
                Require(filter.transform.localPosition == Vector3.zero && filter.transform.localScale == Vector3.one,
                    "A child transform must not shrink the visible cloud back into the old small envelope.");
                Require(mesh.bounds.size.x * root.localScale.x >= 9 && mesh.bounds.size.y * root.localScale.y >= 2.5f,
                    "Default crossing clouds must visibly read as substantial volumes.");
            }
            report.defaultLargeLayout = true;
        }

        private static void CheckSparse(IslandLivingNature nature, IslandStageDefinition stage, IslandRoute route, Vector2[] ranges, Report report)
        {
            Array entries = Entries(nature); Require(entries.Length > 0 && entries.Length <= 32, "Sparse pool must be bounded and nonempty.");
            var shapes = new HashSet<Mesh>(); var materialSet = new HashSet<Material>();
            float smallest = float.MaxValue, largest = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                object item = entries.GetValue(i); Transform root = Value<Transform>(item, "root");
                var filter = root.GetComponentInChildren<MeshFilter>(true);
                var mesh = filter.sharedMesh; shapes.Add(mesh);
                materialSet.Add(root.GetComponentInChildren<MeshRenderer>(true).sharedMaterial);
                Vector3[] vertices = mesh.vertices;
                float distance = Value<float>(item, "distance"); var frame = route.Evaluate(distance);
                float low = float.MaxValue, high = float.MinValue, top = float.MinValue, bottom = float.MaxValue;
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 world = filter.transform.TransformPoint(vertices[v]);
                    float station = distance + Vector3.Dot(world - frame.Position, frame.Tangent);
                    low = Mathf.Min(low, station); high = Mathf.Max(high, station); top = Mathf.Max(top, world.y); bottom = Mathf.Min(bottom, world.y);
                }
                // Lateral drift can project onto another frame on a curve; reserve its full horizontal magnitude.
                low -= .12f; high += .12f;
                Require(top - bottom > .40f && (top - bottom) / (high - low) > .38f, "Cloud must remain a visible 3D volume.");
                float volume = mesh.bounds.size.x * root.localScale.x * mesh.bounds.size.y * root.localScale.y * mesh.bounds.size.z * root.localScale.z;
                smallest = Mathf.Min(smallest, volume); largest = Mathf.Max(largest, volume);
                bool inside = false;
                for (int r = 0; r < ranges.Length; r++) inside |= low >= ranges[r].x - .001f && high <= ranges[r].y + .001f;
                Require(inside, "Actual transformed puff plus drift leaves its authored window.");
                for (int sample = 0; sample <= 80; sample++)
                {
                    float floor = route.Evaluate(Mathf.Lerp(low, high, sample / 80f)).Position.y + stage.TileElevation;
                    float clearance = floor - top - .035f;
                    report.minimumClearance = Mathf.Min(report.minimumClearance, clearance);
                    Require(clearance >= .649f, "Cloud top reaches an existing contact on a slope/drift extreme.");
                }
                for (int j = 0; j < i; j++)
                {
                    object other = entries.GetValue(j); Transform otherRoot = Value<Transform>(other, "root");
                    // A separating horizontal axis proves that all actual triangles remain apart throughout drift.
                    Vector3 axis = root.position - otherRoot.position; axis.y = 0; axis.Normalize();
                    float aLow = float.MaxValue, bHigh = float.MinValue;
                    for (int v = 0; v < vertices.Length; v++) aLow = Mathf.Min(aLow, Vector3.Dot(filter.transform.TransformPoint(vertices[v]), axis));
                    var otherFilter = otherRoot.GetComponentInChildren<MeshFilter>(true);
                    foreach (var vertex in otherFilter.sharedMesh.vertices)
                        bHigh = Mathf.Max(bHigh, Vector3.Dot(otherFilter.transform.TransformPoint(vertex), axis));
                    float gap = aLow - bHigh - .24f;
                    report.minimumGap = Mathf.Min(report.minimumGap, gap);
                    Require(gap >= 1.499f, "Separate clouds need at least 1.5m of actual air including both drift envelopes.");
                }
            }
            report.sharedSparseMeshes = Mathf.Max(report.sharedSparseMeshes, shapes.Count);
            Require(shapes.Count <= 5 && materialSet.Count == 1, "Cloud steps must share at most five meshes and one material.");
            if (entries.Length >= 4)
            {
                report.sizeRatio = largest / smallest;
                Require(report.sizeRatio >= 2.2f, "Large and small clouds must have visibly different volumes.");
                Require(shapes.Count >= 2, "Sparse clusters must expose more than one shared puff silhouette.");
            }
        }

        private static int CountInside(IslandLivingNature nature, Vector2 range)
        {
            int count = 0; foreach (object item in Entries(nature))
            { float d = Value<float>(item, "distance"); if (d >= range.x && d <= range.y) count++; }
            return count;
        }
        private static Array Entries(IslandLivingNature nature) => (Array)typeof(IslandLivingNature).GetField("bank", Hidden).GetValue(nature);
        private static T Value<T>(object item, string name) => (T)item.GetType().GetField(name, Hidden).GetValue(item);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
