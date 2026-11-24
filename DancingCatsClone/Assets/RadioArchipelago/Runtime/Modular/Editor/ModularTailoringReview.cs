#if UNITY_EDITOR
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
    /// <summary>Native, fixed-camera evidence for source-strap tailoring and hat placement.</summary>
    public static class ModularTailoringReview
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        static readonly string Root = Path.GetFullPath("../Docs/Design/RadioArchipelago/AccessoryTailoring20261104");
        [Serializable] public sealed class MeshRecord
        {
            public string node, mesh, meshAsset;
            public int vertices, triangles, uvCount;
            public bool active;
        }
        [Serializable] public sealed class PairRecord
        {
            public string hero, item, kind;
            public Vector3 equipmentPosition, equipmentScale, caseMinimum, caseMaximum, hatMinimum, hatMaximum;
            public float caseHeight, caseMeshHeight, contactPlaneRearOffsetAtCenter;
            public int artificialHarnessNodes, activeSourceHarnessMeshes;
            public bool sourceHarnessUVPreserved, sourceHarnessTopologyPreserved, sourceHarnessMaterialsPreserved, sourceHarnessShapeUnchanged;
            public bool hatClearancePass;
            public float hatEyeClearance, hatRearClearance, hatHeadSeatingOverlap;
            public MeshRecord[] harnessMeshes;
            public DeformationRecord[] strapDeformation;
            public string[] images;
        }
        [Serializable] public sealed class Distribution
        {
            public float minimum, p05, median, p95, maximum;
        }
        [Serializable] public sealed class DeformationRecord
        {
            public string node;
            public int triangles, sourceDegenerateTriangles, newlyCollapsedTriangles, sourceEdgesTooSmall, newlyCollapsedEdges;
            public Distribution edgeLengthRatio, triangleAreaRatio;
        }
        [Serializable] public sealed class Report
        {
            public string phase, unityVersion, capturedUtc;
            public bool fixedCamera = true, numericalCaseSizePass, allSourceHarnessPreserved, allSourceHarnessShapeUnchanged, allSourceHarnessActive, noArtificialHarness, allHatClearancePass;
            public int backpackPairs, hatPairs, images;
            public float caseHeightMinimum, caseHeightMaximum;
            public PairRecord[] pairs;
            public string limitation = "Case contact offset samples the body back section at contact-center height; it is not a collision or artistic-quality pass. Source mesh and UV counts are evidence, not proof of seamless tailoring.";
        }

        public static void Baseline() { Capture(true); }
        public static void Run() { Capture(false); }
        public static void Quick() { Capture(false, true); }
        public static void Minimal() { Capture(false, true, true); }
        public static void StrapReachBefore() { Capture(true, true, true, true); }
        public static void StrapReachAfter() { Capture(false, true, true, true); }
        public static void StrapReachOwners() { Capture(false, false, false, true, true); }
        public static void StrapCorrectionBefore() { Capture(true, true, true, true, false, true); }
        public static void StrapCorrectionAfter() { Capture(false, true, true, true, false, true); }
        public static void StrapCorrectionOwners() { Capture(false, false, false, true, true, true); }

        static void Capture(bool baseline, bool quick = false, bool minimal = false, bool strapReach = false, bool owners = false, bool correction = false)
        {
            bool subset = baseline || quick;
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("The modular courier catalog is missing.");
            string folder = strapReach
                ? Path.GetFullPath("../Docs/Design/RadioArchipelago/"+(correction?"StrapFitCorrection20261105/":"StrapReach20261105/")+(baseline?"Before":"After"))
                : Path.Combine(Root, baseline ? "Before" : minimal ? "OriginalFit" : "After");
            Directory.CreateDirectory(folder);
            var records = new List<PairRecord>();
            var report = new Report { phase = baseline ? "baseline" : minimal ? "original-affine" : "fitted", unityVersion = Application.unityVersion, capturedUtc = DateTime.UtcNow.ToString("O") };
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Disposable fixed tailoring camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 2.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.94f, .925f, .875f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 50;
            var rt = RenderTexture.GetTemporary(640, 800, 24, RenderTextureFormat.ARGB32);
            var previousTarget = RenderTexture.active;
            camera.targetTexture = rt;
            var pixels = new Texture2D(640, 800, TextureFormat.RGBA32, false);
            var ambient = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(.47f, .47f, .47f);
            AddLight(scene, "Warm key", new Vector3(35, 145, 0), 1.4f, new Color(1, .94f, .83f));
            AddLight(scene, "Cool rim", new Vector3(25, -25, 0), .9f, new Color(.75f, .89f, 1));
            int imageCount = 0;
            try
            {
                foreach (var hero in catalog.entries)
                {
                    if (subset && hero.assetId != "Rumi" && hero.assetId != "Tiko" && hero.assetId != "Nori" && hero.assetId != "Lumen") continue;
                    var actor = UnityEngine.Object.Instantiate(hero.prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                        var motion = actor.GetComponent<CourierMotion>(); motion.ManualSimulation = true;
                        var ownerPack = catalog.FindEquipment(hero.defaultBackpackId, CourierEquipmentKind.Backpack);
                        var allPacks = catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Backpack);
                        foreach (var pack in allPacks)
                        {
                            if (owners && !pack.id.StartsWith("backpack_"+hero.assetId,StringComparison.Ordinal)) continue;
                            if (minimal && !(pack == ownerPack || (hero.assetId == "Rumi" && pack.id == "backpack_Rumi_Orbit"))) continue;
                            if (!minimal && subset && hero.assetId != "Rumi" && pack != ownerPack) continue;
                            wardrobe.Apply(pack, null, null); motion.ResetMotion();
                            var pair = Observe(hero.assetId, pack, wardrobe);
                            pair.images = RenderViews(hero.assetId + "__" + pack.id, actor, owners || minimal || (subset && hero.assetId == "Rumi"));
                            if(correction && hero.assetId=="Rumi" && pack.id=="backpack_Rumi_Orbit")
                            {
                                var bodies=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.GetComponentInParent<CourierEquipmentRig>()==null).ToArray();
                                foreach(var body in bodies)body.enabled=false;
                                try{RenderViews(hero.assetId+"__"+pack.id+"_no-body",actor,false,false);}
                                finally{foreach(var body in bodies)body.enabled=true;}
                                motion.PlayIdle();motion.SimulateSprings(.12f);
                                RenderViews(hero.assetId+"__"+pack.id+"_idle",actor,true,false);
                                motion.PlayJump(1.3f);
                                for(int step=0;step<25;step++){motion.SetFlightProgress(step/50f);motion.SimulateSprings(1/60f);}
                                RenderViews(hero.assetId+"__"+pack.id+"_flight",actor,true,false);
                                motion.ResetMotion();
                            }
                            records.Add(pair);
                            report.backpackPairs++;
                        }
                        foreach (var hat in catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Hat))
                        {
                            if (owners) continue;
                            if (subset && (hero.assetId != "Rumi" || (hat.id != "hat_AmberBeret" && hat.id != "hat_SignalDiadem"))) continue;
                            wardrobe.Apply(ownerPack, hat, null); motion.ResetMotion();
                            var pair = Observe(hero.assetId, hat, wardrobe);
                            var clearance = ModularHatFitRepair.Analyze(hero, hat, catalog);
                            pair.hatClearancePass = clearance.passed;
                            pair.hatEyeClearance = clearance.eyeClearance;
                            pair.hatRearClearance = clearance.rearClearance;
                            pair.hatHeadSeatingOverlap = clearance.headSeatingOverlap;
                            pair.images = RenderViews(hero.assetId + "__" + hat.id, actor, true, subset);
                            records.Add(pair);
                            report.hatPairs++;
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
                var heights = records.Where(p => p.kind == "Backpack").Select(p => p.caseHeight).ToArray();
                report.caseHeightMinimum = heights.Min(); report.caseHeightMaximum = heights.Max();
                report.numericalCaseSizePass = heights.All(h => Mathf.Abs(h - 1.18f) < .0001f);
                var packRecords = records.Where(p => p.kind == "Backpack").ToArray();
                report.allSourceHarnessPreserved = packRecords.All(p => p.sourceHarnessUVPreserved && p.sourceHarnessTopologyPreserved && p.sourceHarnessMaterialsPreserved);
                report.allSourceHarnessShapeUnchanged = packRecords.All(p => p.sourceHarnessShapeUnchanged);
                report.allSourceHarnessActive = packRecords.All(p => p.activeSourceHarnessMeshes > 0);
                report.noArtificialHarness = packRecords.All(p => p.artificialHarnessNodes == 0);
                report.allHatClearancePass = records.Where(p => p.kind == "Hat").All(p => p.hatClearancePass);
                report.images = imageCount; report.pairs = records.ToArray();
                File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report, true));
                Debug.Log("RADIO_TAILORING_CAPTURE " + (baseline ? "BASELINE" : "FINAL") + ": " + report.backpackPairs + " backpack pairs, " + report.hatPairs + " hat pairs, " + imageCount + " native screenshots; equal-case-size=" + report.numericalCaseSizePass + ".");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousTarget; RenderTexture.ReleaseTemporary(rt);
                RenderSettings.ambientLight = ambient;
                UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            string[] RenderViews(string name, GameObject actor, bool front, bool back = true)
            {
                var files = new List<string>();
                // Position and target are fixed for every hero/item and before/after capture.
                View("side", new Vector3(6, 2, 0));
                if (back) View("back", new Vector3(0, 2, -6));
                if (front) View("front", new Vector3(0, 2, 6));
                return files.ToArray();
                void View(string suffix, Vector3 eye)
                {
                    camera.transform.position = eye; camera.transform.LookAt(new Vector3(0, 2, 0));
                    using (var baked = new CourierPreviewSkinBake(actor)) { baked.Refresh(); camera.Render(); }
                    RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0, 0, 640, 800), 0, 0); pixels.Apply();
                    string file = name + "__" + suffix + ".png";
                    File.WriteAllBytes(Path.Combine(folder, file), pixels.EncodeToPNG()); files.Add(file); imageCount++;
                }
            }
        }

        static PairRecord Observe(string hero, CourierEquipmentDefinition item, CourierWardrobeRig wardrobe)
        {
            var frame = wardrobe.bodyRig.visualPivot;
            var gear = wardrobe.Equipped(item.kind);
            var pack = wardrobe.Equipped(CourierEquipmentKind.Backpack);
            var record = new PairRecord { hero = hero, item = item.id, kind = item.kind.ToString(), equipmentPosition = frame.InverseTransformPoint(gear.transform.position), equipmentScale = gear.transform.lossyScale };
            if (pack != null)
            {
                var rig = pack.GetComponent<CourierEquipmentRig>();
                var casePoints = new List<Vector3>();
                foreach (var mf in pack.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name == "Case" && mf.sharedMesh != null)
                        foreach (var point in mf.sharedMesh.vertices)
                            casePoints.Add(frame.InverseTransformPoint(mf.transform.TransformPoint(point)));
                if (casePoints.Count > 0)
                {
                    record.caseMinimum = Minimum(casePoints); record.caseMaximum = Maximum(casePoints);
                    record.caseMeshHeight = record.caseMaximum.y - record.caseMinimum.y;
                }
                // The Case mesh also includes the stationary antenna spindle on some designs.
                // The preserved equal-size contract is the authored case body, excluding that spindle.
                record.caseHeight = frame.InverseTransformVector(pack.transform.TransformVector(Vector3.up * (rig.caseBoundsMax.y - rig.caseBoundsMin.y))).magnitude;
                var contact = frame.InverseTransformPoint(pack.transform.TransformPoint(new Vector3(0, (rig.caseBoundsMin.y + rig.caseBoundsMax.y) * .5f, rig.caseContactZ)));
                var section = wardrobe.profile.bodySections.OrderBy(s => Mathf.Abs(s.height - contact.y)).FirstOrDefault();
                if (section != null) record.contactPlaneRearOffsetAtCenter = contact.z - section.backZ;
                record.artificialHarnessNodes = pack.GetComponentsInChildren<Transform>(true).Count(t => t.name == "FittedHarness" || t.name.StartsWith("FittedStrap", StringComparison.Ordinal) || t.name.StartsWith("FittedBuckle", StringComparison.Ordinal));
                record.harnessMeshes = pack.GetComponentsInChildren<MeshFilter>(true)
                    .Where(m => m.sharedMesh != null && (m.name.IndexOf("Harness", StringComparison.OrdinalIgnoreCase) >= 0 || m.name.IndexOf("Strap", StringComparison.OrdinalIgnoreCase) >= 0 || m.sharedMesh.name.IndexOf("Harness", StringComparison.OrdinalIgnoreCase) >= 0))
                    .Select(m => new MeshRecord { node = m.name, mesh = m.sharedMesh.name, meshAsset = AssetDatabase.GetAssetPath(m.sharedMesh), vertices = m.sharedMesh.vertexCount, triangles = m.sharedMesh.triangles.Length / 3, uvCount = m.sharedMesh.uv.Length, active = m.gameObject.activeInHierarchy && m.GetComponents<Renderer>().Any(r=>r.enabled) }).ToArray();
                record.activeSourceHarnessMeshes = record.harnessMeshes.Count(m => m.active && !m.node.StartsWith("FittedStrap", StringComparison.Ordinal));
                if (item.kind == CourierEquipmentKind.Backpack)
                {
                    var sourceRig = item.prefab.GetComponent<CourierEquipmentRig>();
                    var original = SourceHarnessFilters(sourceRig);
                    var fitted = SourceHarnessFilters(rig);
                    record.activeSourceHarnessMeshes = fitted.Count(m => m.gameObject.activeInHierarchy && m.GetComponents<Renderer>().Any(r=>r.enabled));
                    bool sameCount = original.Length > 0 && original.Length == fitted.Length;
                    record.sourceHarnessUVPreserved = sameCount;
                    record.sourceHarnessTopologyPreserved = sameCount;
                    record.sourceHarnessMaterialsPreserved = sameCount;
                    record.sourceHarnessShapeUnchanged = sameCount;
                    var deformation = new List<DeformationRecord>();
                    for (int index = 0; index < original.Length && index < fitted.Length; index++)
                    {
                        Mesh a = original[index].sharedMesh, b = fitted[index].sharedMesh;
                        record.sourceHarnessUVPreserved &= a.uv.SequenceEqual(b.uv);
                        record.sourceHarnessShapeUnchanged &= a.vertices.SequenceEqual(b.vertices);
                        record.sourceHarnessTopologyPreserved &= a.vertexCount == b.vertexCount && a.subMeshCount == b.subMeshCount && a.triangles.SequenceEqual(b.triangles);
                        var ar = original[index].GetComponent<Renderer>(); var br = fitted[index].GetComponent<Renderer>();
                        record.sourceHarnessMaterialsPreserved &= ar != null && br != null && ar.sharedMaterials.SequenceEqual(br.sharedMaterials);
                        if (a.vertexCount == b.vertexCount && a.triangles.SequenceEqual(b.triangles))
                            deformation.Add(Deformation(original[index], fitted[index], sourceRig.transform, pack.transform, frame));
                    }
                    record.strapDeformation = deformation.ToArray();
                }
            }
            if (item.kind == CourierEquipmentKind.Hat)
            {
                var points = gear.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null)
                    .SelectMany(m => m.sharedMesh.vertices.Select(p => frame.InverseTransformPoint(m.transform.TransformPoint(p)))).ToArray();
                if (points.Length > 0) { record.hatMinimum = Minimum(points); record.hatMaximum = Maximum(points); }
            }
            return record;
        }
        static Vector3 Minimum(IEnumerable<Vector3> points) { return new Vector3(points.Min(p => p.x), points.Min(p => p.y), points.Min(p => p.z)); }
        static Vector3 Maximum(IEnumerable<Vector3> points) { return new Vector3(points.Max(p => p.x), points.Max(p => p.y), points.Max(p => p.z)); }
        static MeshFilter[] SourceHarnessFilters(CourierEquipmentRig rig)
        {
            return rig == null || rig.harness == null ? Array.Empty<MeshFilter>() : rig.harness.Where(t => t != null)
                .SelectMany(t => t.GetComponentsInChildren<MeshFilter>(true)).Where(m => m.sharedMesh != null).Distinct().ToArray();
        }
        static DeformationRecord Deformation(MeshFilter source, MeshFilter fitted, Transform sourceEquipment, Transform actualEquipment, Transform frame)
        {
            Vector3[] original = source.sharedMesh.vertices.Select(p => frame.InverseTransformPoint(actualEquipment.TransformPoint(sourceEquipment.InverseTransformPoint(source.transform.TransformPoint(p))))).ToArray();
            Vector3[] target = fitted.sharedMesh.vertices.Select(p => frame.InverseTransformPoint(fitted.transform.TransformPoint(p))).ToArray();
            var edges = new List<float>(); var areas = new List<float>();
            var report = new DeformationRecord { node = fitted.name };
            int[] triangles = source.sharedMesh.triangles;
            report.triangles = triangles.Length / 3;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                int a = triangles[index], b = triangles[index + 1], c = triangles[index + 2];
                Edge(a, b); Edge(b, c); Edge(c, a);
                float originalArea = Vector3.Cross(original[b] - original[a], original[c] - original[a]).magnitude * .5f;
                float targetArea = Vector3.Cross(target[b] - target[a], target[c] - target[a]).magnitude * .5f;
                if (originalArea < 1e-10f) { report.sourceDegenerateTriangles++; continue; }
                float ratio = targetArea / originalArea;
                areas.Add(ratio);
                if (targetArea < 1e-10f || ratio < .005f) report.newlyCollapsedTriangles++;
            }
            report.edgeLengthRatio = Quantiles(edges); report.triangleAreaRatio = Quantiles(areas);
            return report;
            void Edge(int a, int b)
            {
                float before = Vector3.Distance(original[a], original[b]), after = Vector3.Distance(target[a], target[b]);
                if (before < 1e-6f) { report.sourceEdgesTooSmall++; return; }
                float ratio = after / before;
                edges.Add(ratio);
                if (after < 1e-6f || ratio < .005f) report.newlyCollapsedEdges++;
            }
        }
        static Distribution Quantiles(List<float> values)
        {
            if (values.Count == 0) return new Distribution();
            values.Sort();
            return new Distribution { minimum = values[0], p05 = At(.05f), median = At(.5f), p95 = At(.95f), maximum = values[values.Count - 1] };
            float At(float t) => values[Mathf.Clamp(Mathf.RoundToInt((values.Count - 1) * t), 0, values.Count - 1)];
        }
        static void AddLight(Scene scene, string name, Vector3 euler, float intensity, Color color)
        {
            var obj = new GameObject(name, typeof(Light)); SceneManager.MoveGameObjectToScene(obj, scene);
            obj.transform.rotation = Quaternion.Euler(euler);
            var light = obj.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = intensity; light.color = color;
        }
    }
}
#endif
