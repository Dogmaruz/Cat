using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Read-only cloud proof against the actual three island definitions and derived route heights.</summary>
    public static class IslandCloudStageChecks
    {
        private const string CoralPath = "Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.asset";
        private const string MoonPath = "Assets/RadioArchipelago/Content/Islands/MoonResonance/MoonResonance.asset";
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly float[] CloudSizes = { .72f, .88f, 1.04f, 1.22f, 1.44f };
        private static readonly Vector2[] MoonAir = {
            new Vector2(445.75f, 458.25f), new Vector2(471.75f, 479.25f),
            new Vector2(499.25f, 509.50f), new Vector2(523.50f, 546.75f) };
        private static readonly Vector2[] MoonStone = {
            new Vector2(458.5f, 471.5f), new Vector2(479.5f, 499), new Vector2(509.75f, 523.25f) };

        [Serializable] private sealed class RouteDocument { public bool closed; public Vector3[] points; }
        [Serializable] private sealed class CloudEvidence
        {
            public float station, footprintStart, footprintEnd, height, width, depth;
            public int shapeIndex, sizeClass;
            public Vector3 worldPosition;
            public Vector3 rootScale, meshScale;
            public string sharedMesh;
            public bool chartLandingAnchor;
        }
        [Serializable] private sealed class StageReport
        {
            public string definition, route, elevation, chart, countNote;
            public bool sparse, expectedCountReached, idempotent, cleanupComplete, noGameplayComponents;
            public bool pauseFreezesPose, realGeometryPassed, cloudFreeStoneVisits;
            public bool uniformSizeClasses, adjacentFormSizeVariety, allFiveFormsAndSizes;
            public int expectedCloudCount, actualCloudCount, sharedMeshes, sharedMaterials, chartLandingAnchors;
            public int distinctForms, distinctSizeClasses;
            public int[] cloudsPerAirWindow;
            public float minimumGap, minimumClearance, sizeRatio, minimumStoneVisitGap;
            public long warmedTickAllocatedBytes;
            public CloudEvidence[] clouds;
        }
        [Serializable] private sealed class Report
        {
            public bool pass, assetsUnchanged, allEmeraldDefaultsPreserved;
            public string checkedUtc;
            public string scope = "Actual imported stage definitions, closed planar routes and derived elevation profiles. Isolated scene-owned visual pools only; transformed puff vertices plus full drift reserve, route-height clearance, actual Moon air windows and dense physical centreline checks through the three stone visits. No scene, definition, timeline or imported asset is written.";
            public int checkedAssetFiles;
            public StageReport emerald, coral, moon;
        }

        [MenuItem("Tools/Radio Archipelago/Check real island cloud stages")]
        public static void RunChecks()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating,
                "Island cloud stage checks require stable Edit Mode.");
            var emerald = Load(IslandAbyssJobs.DefinitionPath);
            var coral = Load(CoralPath);
            var moon = Load(MoonPath);
            var snapshots = new Dictionary<string, string>();
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O") };
            foreach (string path in new[] { IslandPilotJobs.DefinitionPath, IslandReliefJobs.DefinitionPath, IslandAbyssJobs.DefinitionPath })
            {
                var earlier = Load(path);
                Require(!earlier.SparseCloudSteps && (earlier.CloudStepRanges == null || earlier.CloudStepRanges.Length == 0),
                    "Preserved Emerald definitions must retain the original cloud defaults: " + path);
                Snapshot(earlier, snapshots);
            }
            report.allEmeraldDefaultsPreserved = true;
            Snapshot(coral, snapshots); Snapshot(moon, snapshots);
            Require(coral.SparseCloudSteps && moon.SparseCloudSteps, "Coral Oasis and Moon Resonance must explicitly opt into detached cloud steps.");
            Require(coral.CloudStepRanges != null && coral.CloudStepRanges.Length == 0,
                "Coral Oasis must retain its existing 432–460 crossing window.");
            Require(moon.CloudStepRanges != null && moon.CloudStepRanges.Length == MoonAir.Length,
                "Moon Resonance needs the four cloud windows between its three small islands.");
            for (int i = 0; i < MoonAir.Length; i++)
                Require((moon.CloudStepRanges[i] - MoonAir[i]).sqrMagnitude < .000001f,
                    "Moon cloud window differs from the independently audited actual centreline air interval.");

            report.emerald = CheckStage(emerald, IslandAbyssJobs.DefinitionPath, 2, new[] { new Vector2(432, 460) });
            report.coral = CheckStage(coral, CoralPath, 2, new[] { new Vector2(432, 460) });
            report.moon = CheckStage(moon, MoonPath, 5, MoonAir);
            foreach (var snapshot in snapshots)
                Require(snapshot.Value == Hash(snapshot.Key), "Read-only stage check changed an asset: " + snapshot.Key);
            report.assetsUnchanged = true; report.checkedAssetFiles = snapshots.Count; report.pass = true;
            string folder = Path.GetFullPath("Library/RadioArchipelago/SparseCloudSteps"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "island-cloud-stages.json"), JsonUtility.ToJson(report, true));
            Debug.Log("ISLAND_CLOUD_STAGES PASS: " + JsonUtility.ToJson(report));
        }

        private static StageReport CheckStage(IslandStageDefinition definition, string path, int expected, Vector2[] windows)
        {
            Require(definition.ElevationJson != null, "The actual playable stage needs its derived height profile: " + path);
            var document = JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text);
            Require(document != null && document.closed && document.points != null, "Actual stage route must be closed: " + path);
            var route = new IslandRoute(document.points, IslandElevationProfile.FromJson(definition.ElevationJson.text));
            string beforeDefinition = EditorJsonUtility.ToJson(definition);
            bool beforeDirty = EditorUtility.IsDirty(definition);
            var owner = new GameObject("Isolated actual island cloud check") { hideFlags = HideFlags.HideAndDontSave };
            var report = new StageReport {
                definition = path, route = AssetDatabase.GetAssetPath(definition.RouteJson),
                elevation = AssetDatabase.GetAssetPath(definition.ElevationJson), chart = AssetDatabase.GetAssetPath(definition.Chart),
                sparse = definition.SparseCloudSteps, expectedCloudCount = expected };
            try
            {
                var nature = owner.AddComponent<IslandLivingNature>(); nature.Configure(definition, route);
                Array entries = Entries(nature);
                report.actualCloudCount = entries.Length; report.expectedCountReached = entries.Length == expected;
                Require(entries.Length > 0 && entries.Length <= 32, "Actual stage cloud pool is empty or unbounded: " + path);
                Require(report.expectedCountReached, "Actual stage cloud count differs from its large-cloud spacing policy: " + path);
                report.countNote = "Full-size clouds retained in every authored air window, with wider spacing.";
                report.noGameplayComponents = owner.GetComponentsInChildren<Collider>(true).Length == 0 &&
                    owner.GetComponentsInChildren<Rigidbody>(true).Length == 0 && owner.GetComponentsInChildren<AudioSource>(true).Length == 0;
                Require(report.noGameplayComponents, "Decorative cloud configuration must never add gameplay components.");

                // Reuse the original independent triangle/gap/clearance proof;
                // only this new file uses reflection into its private report.
                Type proofType = typeof(SparseCloudStepChecks).GetNestedType("Report", BindingFlags.NonPublic);
                Require(proofType != null, "The isolated geometry proof report is missing.");
                object proof = Activator.CreateInstance(proofType, true);
                MethodInfo check = typeof(SparseCloudStepChecks).GetMethod("CheckSparse", BindingFlags.Static | BindingFlags.NonPublic);
                Require(check != null, "The isolated cloud geometry proof method is missing.");
                try
                {
                    check.Invoke(null, new object[] { nature, definition, route, windows, proof });
                }
                catch (TargetInvocationException error)
                {
                    throw new InvalidOperationException(path + " actual cloud geometry: " + error.InnerException?.Message, error.InnerException ?? error);
                }
                report.realGeometryPassed = true;
                report.minimumGap = Metric<float>(proof, "minimumGap");
                report.minimumClearance = Metric<float>(proof, "minimumClearance");
                report.sizeRatio = Metric<float>(proof, "sizeRatio");
                var meshes = new HashSet<Mesh>(); var materials = new HashSet<Material>();
                foreach (var filter in owner.GetComponentsInChildren<MeshFilter>(true)) meshes.Add(filter.sharedMesh);
                foreach (var renderer in owner.GetComponentsInChildren<MeshRenderer>(true)) materials.Add(renderer.sharedMaterial);
                var bankMeshes = new HashSet<Mesh>(); var bankMaterials = new HashSet<Material>();
                var forms = new HashSet<int>(); var sizes = new HashSet<int>();
                var formCounts = new int[5]; var sizeCounts = new int[CloudSizes.Length];
                report.clouds = new CloudEvidence[entries.Length];
                report.cloudsPerAirWindow = windows == null ? Array.Empty<int>() : new int[windows.Length];
                var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
                for (int i = 0; i < entries.Length; i++)
                {
                    object item = entries.GetValue(i); float station = Value<float>(item, "distance");
                    Transform root = Value<Transform>(item, "root");
                    MeshFilter filter = root.GetComponentInChildren<MeshFilter>(true);
                    bankMeshes.Add(filter.sharedMesh); bankMaterials.Add(root.GetComponentInChildren<MeshRenderer>(true).sharedMaterial);
                    var evidence = Footprint(filter, route, station);
                    evidence.shapeIndex = Value<int>(item, "shapeIndex"); evidence.sizeClass = Value<int>(item, "sizeClass");
                    evidence.rootScale = root.localScale; evidence.meshScale = filter.transform.localScale;
                    Require(evidence.shapeIndex >= 0 && evidence.shapeIndex < 5 && evidence.sizeClass >= 0 && evidence.sizeClass < CloudSizes.Length,
                        "An actual bank cloud must address one of five forms and size classes: " + path);
                    string expectedName = "Island coherent billow shared " + evidence.shapeIndex;
                    Require(filter.sharedMesh.name == expectedName || filter.sharedMesh.name.StartsWith(expectedName + " ", StringComparison.Ordinal),
                        "Actual bank cloud form metadata differs from the mesh being drawn: " + path);
                    Require((root.localScale - Vector3.one * (5 * CloudSizes[evidence.sizeClass])).sqrMagnitude < .00000001f &&
                        (filter.transform.localScale - Vector3.one).sqrMagnitude < .00000001f,
                        "Every actual bank cloud must retain its uniform five-times-larger size without shrinking its child mesh: " + path);
                    if (i > 0) Require(evidence.shapeIndex != report.clouds[i - 1].shapeIndex || evidence.sizeClass != report.clouds[i - 1].sizeClass,
                        "Adjacent actual bank clouds repeat an identical form and size class: " + path);
                    forms.Add(evidence.shapeIndex); sizes.Add(evidence.sizeClass);
                    formCounts[evidence.shapeIndex]++; sizeCounts[evidence.sizeClass]++;
                    evidence.chartLandingAnchor = MatchesLanding(map, definition, route, root.position, station);
                    if (evidence.chartLandingAnchor) report.chartLandingAnchors++;
                    report.clouds[i] = evidence;
                    if (windows == null) continue;
                    int containing = -1;
                    for (int w = 0; w < windows.Length; w++)
                        if (evidence.footprintStart > windows[w].x && evidence.footprintEnd < windows[w].y) containing = w;
                    Require(containing >= 0, "Actual cloud footprint and drift must lie strictly inside one authored air window: " + path);
                    report.cloudsPerAirWindow[containing]++;
                    if (path == MoonPath)
                    {
                        for (int v = 0; v < MoonStone.Length; v++)
                            Require(evidence.footprintEnd < MoonStone[v].x || evidence.footprintStart > MoonStone[v].y,
                                "An actual Moon cloud extends into a small-island centreline visit.");
                    }
                }
                report.sharedMeshes = bankMeshes.Count; report.sharedMaterials = bankMaterials.Count;
                Require(report.sharedMaterials == 1 && report.sharedMeshes <= 5, "Actual cloud banks must share at most five meshes and their single cloud material.");
                report.distinctForms = forms.Count; report.distinctSizeClasses = sizes.Count;
                report.uniformSizeClasses = report.adjacentFormSizeVariety = true;
                if (path == MoonPath || entries.Length >= 5)
                {
                    Require(forms.Count == 5 && sizes.Count == CloudSizes.Length,
                        "An actual bank with at least five clouds must show all five forms and all five size classes: " + path);
                    int minForms = int.MaxValue, maxForms = 0, minSizes = int.MaxValue, maxSizes = 0;
                    for (int i = 0; i < formCounts.Length; i++)
                    {
                        minForms = Mathf.Min(minForms, formCounts[i]); maxForms = Mathf.Max(maxForms, formCounts[i]);
                        minSizes = Mathf.Min(minSizes, sizeCounts[i]); maxSizes = Mathf.Max(maxSizes, sizeCounts[i]);
                    }
                    Require(maxForms - minForms <= 1 && maxSizes - minSizes <= 1,
                        "The five actual bank forms and size classes must remain evenly distributed: " + path);
                    report.allFiveFormsAndSizes = true;
                }
                if (windows != null)
                    for (int w = 0; w < windows.Length; w++) Require(report.cloudsPerAirWindow[w] >= 1, "An authored actual air gap has no cloud: " + path);
                if (path == MoonPath)
                {
                    report.minimumStoneVisitGap = float.MaxValue;
                    foreach (Vector2 visit in MoonStone)
                    {
                        int samples = Mathf.CeilToInt((visit.y - visit.x) / .25f);
                        for (int sample = 0; sample <= samples; sample++)
                        {
                            Vector3 point = route.Evaluate(Mathf.Lerp(visit.x, visit.y, (float)sample / samples)).Position;
                            for (int i = 0; i < entries.Length; i++)
                            {
                                Transform cloudRoot = Value<Transform>(entries.GetValue(i), "root");
                                float gap = HorizontalPointGap(cloudRoot.GetComponentInChildren<MeshFilter>(true), point);
                                report.minimumStoneVisitGap = Mathf.Min(report.minimumStoneVisitGap, gap);
                                Require(gap > 0, "A transformed Moon cloud plus drift covers an actual stone-visit centreline sample: cloud=" + i +
                                    ", cloudStation=" + Value<float>(entries.GetValue(i), "distance") + ", visit=" + visit +
                                    ", sample=" + Mathf.Lerp(visit.x, visit.y, (float)sample / samples) + ", point=" + point + ", gap=" + gap);
                            }
                        }
                    }
                    report.cloudFreeStoneVisits = true;
                }
                int transformCount = owner.GetComponentsInChildren<Transform>(true).Length;
                Transform firstCloud = Value<Transform>(entries.GetValue(0), "root");
                nature.Configure(definition, route);
                report.idempotent = transformCount == owner.GetComponentsInChildren<Transform>(true).Length &&
                    firstCloud == Value<Transform>(Entries(nature).GetValue(0), "root");
                Require(report.idempotent, "Actual stage repeated Configure rebuilt the pool.");
                nature.UpdatePresentation(Value<float>(entries.GetValue(0), "distance"), .016f);
                Vector3 pausePosition = firstCloud.position;
                nature.UpdatePresentation(Value<float>(entries.GetValue(0), "distance"), 0);
                report.pauseFreezesPose = (firstCloud.position - pausePosition).sqrMagnitude < .00000001f;
                Require(report.pauseFreezesPose, "Actual cloud drift must freeze with the presentation clock.");
                for (int i = 0; i < 20; i++) nature.UpdatePresentation(450 + i, .016f);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 2000; i++) nature.UpdatePresentation(430 + i % 130, .016f);
                report.warmedTickAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(report.warmedTickAllocatedBytes == 0, "Actual stage cloud ticks allocate after warmup.");
                nature.Dispose(); nature.Dispose();
                report.cleanupComplete = owner.transform.childCount == 0 && !nature.IsConfigured && nature.CloudBankCount == 0;
                foreach (Mesh mesh in meshes) report.cleanupComplete &= mesh == null;
                foreach (Material material in materials) report.cleanupComplete &= material == null;
                Require(report.cleanupComplete, "Actual stage Dispose left owned clouds, meshes or materials alive.");
                Require(beforeDefinition == EditorJsonUtility.ToJson(definition) && beforeDirty == EditorUtility.IsDirty(definition),
                    "Cloud checking mutated the actual stage definition or its dirty state.");
                return report;
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        private static CloudEvidence Footprint(MeshFilter filter, IslandRoute route, float station)
        {
            IslandRoute.Frame frame = route.Evaluate(station);
            float low = float.MaxValue, high = float.MinValue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                float projected = station + Vector3.Dot(filter.transform.TransformPoint(vertex) - frame.Position, frame.Tangent);
                low = Mathf.Min(low, projected); high = Mathf.Max(high, projected);
            }
            Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.lossyScale);
            return new CloudEvidence { station = station, footprintStart = low - .12f, footprintEnd = high + .12f,
                worldPosition = filter.transform.position, sharedMesh = filter.sharedMesh.name, width = size.x, height = size.y, depth = size.z };
        }

        private static float HorizontalPointGap(MeshFilter filter, Vector3 point)
        {
            Vector3 axis = point - filter.transform.position; axis.y = 0;
            if (axis.sqrMagnitude < .000001f) return -1;
            float distance = axis.magnitude; axis /= distance;
            float extent = float.MinValue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
                extent = Mathf.Max(extent, Vector3.Dot(filter.transform.TransformPoint(vertex) - filter.transform.position, axis));
            return distance - extent - .12f;
        }

        private static bool MatchesLanding(MapTimeline map, IslandStageDefinition definition, IslandRoute route, Vector3 position, float station)
        {
            if (map?.elements == null) return false;
            IslandRoute.Frame frame = route.Evaluate(station);
            float lateral = Vector3.Dot(position - frame.Position, frame.Right);
            foreach (MapElement element in map.elements)
            {
                if (element == null || element.updateType != 0 || (element.id != 0 && element.id != 1 && element.id != 4)) continue;
                float distance = definition.StartDistance + element.pos.z * definition.DistanceScale;
                if (Mathf.Abs(station - distance) < .001f && Mathf.Abs(lateral - element.pos.x * definition.LateralScale) < .001f) return true;
            }
            return false;
        }

        private static IslandStageDefinition Load(string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(path);
            Require(definition != null && definition.RouteJson != null && definition.Chart != null,
                "Build the actual stage definition before cloud validation: " + path);
            return definition;
        }
        private static void Snapshot(IslandStageDefinition definition, Dictionary<string, string> snapshots)
        {
            foreach (UnityEngine.Object asset in new UnityEngine.Object[] { definition, definition.RouteJson, definition.ElevationJson, definition.Chart })
            {
                if (asset == null) continue;
                string path = AssetDatabase.GetAssetPath(asset);
                if (!snapshots.ContainsKey(path)) snapshots.Add(path, Hash(path));
            }
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static T Metric<T>(object report, string name) => (T)report.GetType().GetField(name).GetValue(report);
        private static Array Entries(IslandLivingNature nature) => (Array)typeof(IslandLivingNature).GetField("bank", Hidden).GetValue(nature);
        private static T Value<T>(object item, string name)
        {
            FieldInfo field = item.GetType().GetField(name, Hidden);
            Require(field != null, "Actual cloud proof metadata is missing: " + name);
            return (T)field.GetValue(item);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
