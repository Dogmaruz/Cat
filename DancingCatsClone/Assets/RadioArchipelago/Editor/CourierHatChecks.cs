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
    /// <summary>Read-only support/eye-occlusion evidence, independent of the hat fitter's pass predicate.</summary>
    public static class CourierHatChecks
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        const string MetaRoot = "../ArtSource/RadioArchipelago/ProcessedModular/Characters";
        [Serializable] sealed class Eye { public float[] center; public float radiusX, radiusZ; }
        [Serializable] sealed class Blink { public Eye[] eyes; }
        [Serializable] sealed class BodyMeta { public Blink blink; }
        [Serializable] public sealed class Distribution { public float minimum, p10, median, p90, maximum; }
        [Serializable] public sealed class Observation
        {
            public string hero, hat;
            public bool structuralPassed;
            public float scale, hatWidth, headWidthAtForehead, widthRatio;
            public Vector3 position, euler;
            public int sampledAnnulusPoints, pairedHeadHatPoints, nearOrInsideHatPoints, headAboveHatRoofPoints;
            public int eyeSamples, eyeOccludedSamples, nonfiniteVertices;
            public Distribution verticalUndersideGap;
            public string[] diagnostics;
        }
        [Serializable] sealed class Report
        {
            public string capturedUtc, unityVersion, phase;
            public bool structuralPassed, activeScenePreserved;
            public int pairs;
            public Observation[] observations;
            public string method = "Double-sided CPU triangle heightfields sample an annulus inside the hat footprint, avoiding the exact head center/crest. Positive underside gap means air below the hat at that sample; negative means the head enters the estimated hat volume. Eye occlusion samples compare actual frontmost hat and body surfaces at metadata eye ellipses.";
            public string limitation = "Annulus is a generic inner footprint, not a semantic stitched rim. Brims, diadems and hanging earflaps can produce legitimate gap/penetration samples. Diagnostics never substitute for native front/side review, and structuralPassed is not an artistic-fit pass.";
        }

        public static void Baseline() => Measure("Before");
        public static void Run() => Measure("After");

        static void Measure(string phase)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Measure hat fits in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("The courier catalog is missing.");
            var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
            var scene = EditorSceneManager.NewPreviewScene(); var observations = new List<Observation>();
            try
            {
                foreach (var hero in catalog.entries)
                {
                    var actor = UnityEngine.Object.Instantiate(hero.prefab); SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                        var motion = actor.GetComponent<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                        var frame = wardrobe.bodyRig.visualPivot;
                        var body = Surface.Body(actor, frame);
                        var bodyY = new HeightField(body, 1); var bodyZ = new HeightField(body, 2);
                        var meta = JsonUtility.FromJson<BodyMeta>(File.ReadAllText(Path.Combine(MetaRoot, hero.assetId, "Meta.json")));
                        float eyeTop = meta.blink.eyes.Max(e => e.center[2] + e.radiusZ);
                        var band = body.vertices.Where(p => Mathf.Abs(p.y - (eyeTop + .08f)) < .045f).ToArray();
                        float headWidth = wardrobe.profile.headWidth;
                        if (band.Length > 8)
                        {
                            float zCut = Mathf.Lerp(band.Min(p => p.z), band.Max(p => p.z), .55f);
                            var front = band.Where(p => p.z >= zCut).ToArray();
                            headWidth = front.Max(p => p.x) - front.Min(p => p.x);
                        }
                        foreach (var hat in catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Hat))
                        {
                            wardrobe.Apply(null, hat, null); motion.ResetMotion();
                            var gear = wardrobe.Equipped(CourierEquipmentKind.Hat);
                            var source = Surface.Equipment(gear, frame);
                            var hatY = new HeightField(source, 1); var hatZ = new HeightField(source, 2);
                            var fit = wardrobe.profile.FindFit(hat.id);
                            var item = new Observation { hero = hero.assetId, hat = hat.id, scale = fit.scale,
                                position = fit.position, euler = fit.euler, headWidthAtForehead = headWidth,
                                hatWidth = source.maximum.x - source.minimum.x, nonfiniteVertices = source.vertices.Count(v => !Finite(v)) };
                            item.widthRatio = item.hatWidth / Mathf.Max(.01f, headWidth);
                            var gaps = new List<float>(); var notes = new List<string>();
                            Vector3 center = (source.minimum + source.maximum) * .5f;
                            // A broad annulus, not a highest-point test: a cap balanced on
                            // Lumen's narrow crest leaves most of these off-center samples in air.
                            foreach (float radius in new[] { .4f, .7f, 1f })
                                for (int i = 0; i < 16; i++)
                                {
                                    float angle = i * Mathf.PI * 2 / 16;
                                    float x = center.x + Mathf.Cos(angle) * radius * (source.maximum.x - source.minimum.x) * .325f;
                                    float z = center.z + Mathf.Sin(angle) * radius * (source.maximum.z - source.minimum.z) * .325f;
                                    item.sampledAnnulusPoints++;
                                    if (!bodyY.Query(x, z, out _, out float head) || !hatY.Query(x, z, out float low, out float roof)) continue;
                                    if (head < eyeTop + .025f) continue;
                                    item.pairedHeadHatPoints++; float gap = low - head; gaps.Add(gap);
                                    if (gap <= .025f) item.nearOrInsideHatPoints++;
                                    if (head > roof + .025f) item.headAboveHatRoofPoints++;
                                }
                            item.verticalUndersideGap = Quantiles(gaps);
                            foreach (var eye in meta.blink.eyes)
                                foreach (var offset in new[] { Vector2.zero, new Vector2(-.5f, 0), new Vector2(.5f, 0), new Vector2(0, -.5f), new Vector2(0, .5f) })
                                {
                                    float x = eye.center[0] + eye.radiusX * offset.x, y = eye.center[2] + eye.radiusZ * offset.y;
                                    item.eyeSamples++;
                                    if (bodyZ.Query(x, y, out _, out float face) && hatZ.Query(x, y, out _, out float cap) && cap > face + .005f)
                                        item.eyeOccludedSamples++;
                                }
                            if (item.widthRatio < .8f) notes.Add("Hat is narrower than 80% of the forward forehead band; inspect scale.");
                            if (gaps.Count < 8) notes.Add("Few off-center hat/head samples overlap; inspect alignment or special hat shape.");
                            if (gaps.Count >= 8 && item.nearOrInsideHatPoints < gaps.Count * .35f) notes.Add("Most inner-footprint samples have air underneath; inspect hovering.");
                            if (item.headAboveHatRoofPoints > 4) notes.Add("Several head samples protrude above hat roof; inspect crest/cap overlap.");
                            if (item.eyeOccludedSamples > 0) notes.Add("Hat geometry is in front of an eye sample.");
                            item.diagnostics = notes.ToArray();
                            item.structuralPassed = item.nonfiniteVertices == 0 && source.vertices.Length > 0 &&
                                Finite(fit.position) && Finite(fit.euler) && fit.scale > .1f && fit.scale < 5 &&
                                gear.GetComponentsInChildren<Collider>(true).Length == 0;
                            observations.Add(item);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var report = new Report { phase = phase, capturedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                pairs = observations.Count, observations = observations.ToArray(), activeScenePreserved = active == SceneManager.GetActiveScene() && active.isDirty == dirty };
            int expectedPairs = catalog.entries.Length * catalog.equipment.Count(e => e.kind == CourierEquipmentKind.Hat);
            report.structuralPassed = observations.Count == expectedPairs && observations.All(o => o.structuralPassed) && report.activeScenePreserved;
            Directory.CreateDirectory(CourierHatReview.EvidenceFolder);
            File.WriteAllText(Path.Combine(CourierHatReview.EvidenceFolder, phase + "-checks.json"), JsonUtility.ToJson(report, true));
            if (!report.structuralPassed) throw new InvalidDataException("Hat structural checks failed; inspect " + phase + "-checks.json.");
            Debug.Log("COURIER_HAT_MEASURED: " + observations.Count + " independent fit diagnostics; structural checks passed. Native visual review is still required.");
        }

        static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));
        static Distribution Quantiles(List<float> values)
        {
            values.Sort(); if (values.Count == 0) return new Distribution();
            float Q(float t) => values[Mathf.Clamp(Mathf.RoundToInt(t * (values.Count - 1)), 0, values.Count - 1)];
            return new Distribution { minimum = values[0], p10 = Q(.1f), median = Q(.5f), p90 = Q(.9f), maximum = values[values.Count - 1] };
        }
        sealed class Surface
        {
            public Vector3[] vertices; public int[] indices; public Vector3 minimum, maximum;
            static Surface Finish(List<Vector3> vertices, List<int> indices)
            {
                if (vertices.Count == 0) throw new InvalidDataException("A hat/body has no readable geometry.");
                var bounds = new Bounds(vertices[0], Vector3.zero); foreach (var v in vertices) bounds.Encapsulate(v);
                return new Surface { vertices = vertices.ToArray(), indices = indices.ToArray(), minimum = bounds.min, maximum = bounds.max };
            }
            public static Surface Body(GameObject actor, Transform frame)
            {
                var vertices = new List<Vector3>(); var indices = new List<int>();
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null || skin.GetComponentInParent<CourierEquipmentRig>() != null) continue;
                    var mesh = new Mesh { indexFormat = skin.sharedMesh.indexFormat };
                    try { skin.BakeMesh(mesh, true); Add(mesh, frame.worldToLocalMatrix * skin.transform.localToWorldMatrix, vertices, indices); }
                    finally { UnityEngine.Object.DestroyImmediate(mesh); }
                }
                return Finish(vertices, indices);
            }
            public static Surface Equipment(GameObject gear, Transform frame)
            {
                var vertices = new List<Vector3>(); var indices = new List<int>();
                foreach (var filter in gear.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) Add(filter.sharedMesh, frame.worldToLocalMatrix * filter.transform.localToWorldMatrix, vertices, indices);
                return Finish(vertices, indices);
            }
            static void Add(Mesh mesh, Matrix4x4 matrix, List<Vector3> vertices, List<int> indices)
            {
                int start = vertices.Count; foreach (var p in mesh.vertices) vertices.Add(matrix.MultiplyPoint3x4(p));
                foreach (var index in mesh.triangles) indices.Add(start + index);
            }
        }
        sealed class HeightField
        {
            const float Cell = .06f;
            readonly Dictionary<long, List<int>> bins = new Dictionary<long, List<int>>();
            readonly Vector3[] triangles;
            public HeightField(Surface source, int heightAxis)
            {
                triangles = new Vector3[source.indices.Length];
                for (int i = 0; i < triangles.Length; i++)
                {
                    var p = source.vertices[source.indices[i]];
                    triangles[i] = heightAxis == 1 ? new Vector3(p.x, p.z, p.y) : p;
                }
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = triangles[i]; var b = triangles[i + 1]; var c = triangles[i + 2];
                    int loX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / Cell), hiX = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / Cell);
                    int loY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) / Cell), hiY = Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) / Cell);
                    for (int x = loX; x <= hiX; x++) for (int y = loY; y <= hiY; y++)
                    {
                        long key = Key(x, y); if (!bins.TryGetValue(key, out var list)) { list = new List<int>(); bins.Add(key, list); }
                        list.Add(i);
                    }
                }
            }
            public bool Query(float x, float y, out float low, out float high)
            {
                low = float.PositiveInfinity; high = float.NegativeInfinity;
                if (!bins.TryGetValue(Key(Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(y / Cell)), out var list)) return false;
                foreach (int i in list)
                {
                    var a = triangles[i]; var b = triangles[i + 1]; var c = triangles[i + 2];
                    float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(den) < 1e-10f) continue;
                    float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / den;
                    float v = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / den;
                    float w = 1 - u - v;
                    if (u < -.0001f || v < -.0001f || w < -.0001f) continue;
                    float height = a.z * u + b.z * v + c.z * w; low = Mathf.Min(low, height); high = Mathf.Max(high, height);
                }
                return !float.IsInfinity(low);
            }
            static long Key(int x, int y) => ((long)x << 32) | (uint)y;
        }
    }
}
