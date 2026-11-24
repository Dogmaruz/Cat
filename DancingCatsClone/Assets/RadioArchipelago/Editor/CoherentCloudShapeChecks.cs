using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Independent single-shell topology, shading, sharing and variation proof.</summary>
    public static class CoherentCloudShapeChecks
    {
        private const int FormCount = 5;
        private const string ShapePrefix = "Island coherent billow shared ";
        private static readonly float[] SizeScales = { .72f, .88f, 1.04f, 1.22f, 1.44f };
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class FaceEvidence
        {
            public int triangle, a, b, c;
            public float areaSquared, unityNormalizedDot, exactNormalizedDot;
            public Vector3 p0, p1, p2, n0, n1, n2;
        }
        [Serializable] private sealed class ShapeReport
        {
            public string name;
            public int vertices, triangles, connectedComponents, boundaryEdges, nonmanifoldEdges, eulerCharacteristic, crownPeaks;
            public float minimumNormalDot = 1, radialVariation, signedVolume, smoothEdgeFraction, minimumTriangleAreaSquared = float.MaxValue;
            public float minimumFaceNormalDot = 1;
            public int normalizationEpsilonFaces;
            public FaceEvidence epsilonFace, worstFace;
        }
        [Serializable] private sealed class Report
        {
            public bool pass, sameFiveSharedShapes, opaqueSharedMaterial, cleanupComplete, distinctNormalizedForms, distinctAspectRatios;
            public int cloudMeshes, cloudMaterials;
            public float minimumVariantDifference = float.MaxValue;
            public float minimumAspectDifference = float.MaxValue;
            public PoolReport sky, steps;
            public List<ShapeReport> shapes = new List<ShapeReport>();
        }

        [Serializable] private sealed class PoolReport
        {
            public int count, distinctForms, distinctSizeClasses;
            public bool uniformScales, metadataMatchesGeometry, adjacentVariety, allFormsAndSizes;
            public int[] shapeIndices, sizeClasses;
        }

        [Serializable] private sealed class PreviewReport
        {
            public string unityVersion, capturedUtc;
            public bool qualityFileUnchanged;
            public string scope = "Actual runtime-generated shared meshes and their original opaque material, isolated preview scene, neutral key/fill/ambient lighting. Front, side, top and three-quarter views of every variant plus a shared-scale comparison. No assets, prefabs, saved scenes or settings files written.";
            public List<string> files = new List<string>();
        }

        [MenuItem("Tools/Radio Archipelago/Check coherent cloud shapes")]
        public static void RunChecks()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Coherent cloud geometry checks require stable Edit Mode.");
            var owner = new GameObject("Isolated coherent cloud shapes") { hideFlags = HideFlags.HideAndDontSave };
            var stage = ScriptableObject.CreateInstance<IslandStageDefinition>();
            var report = new Report();
            var shapes = new HashSet<Mesh>(); var materials = new HashSet<Material>();
            try
            {
                stage.SparseCloudSteps = true;
                var route = new IslandRoute(new[] { Vector3.zero, new Vector3(0, 0, 600), new Vector3(200, 0, 600), new Vector3(200, 0, 0) });
                var nature = owner.AddComponent<IslandLivingNature>(); nature.Configure(stage, route);
                foreach (var filter in owner.GetComponentsInChildren<MeshFilter>(true))
                {
                    string parent = filter.transform.parent.name;
                    if (!parent.StartsWith("Drifting sky cloud ", StringComparison.Ordinal) && !parent.StartsWith("High sky cloud bank ", StringComparison.Ordinal)) continue;
                    shapes.Add(filter.sharedMesh); materials.Add(filter.GetComponent<MeshRenderer>().sharedMaterial);
                }
                report.cloudMeshes = shapes.Count; report.cloudMaterials = materials.Count;
                Require(shapes.Count == FormCount, "FIVE_CLOUD_FORMS RED: sky and bank must share exactly five distinct coherent cloud shells.");
                var orderedShapes = new List<Mesh>(shapes);
                orderedShapes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (Mesh shape in orderedShapes) report.shapes.Add(CheckShape(shape));
                var vertices = new List<Vector3[]>();
                foreach (Mesh shape in orderedShapes)
                {
                    float[] heights = SurfaceHeights(shape);
                    var signature = new Vector3[heights.Length];
                    for (int i = 0; i < heights.Length; i++) signature[i] = new Vector3(0, float.IsNaN(heights[i]) ? -1.2f : heights[i], 0);
                    vertices.Add(signature);
                }
                for (int a = 0; a < vertices.Count; a++) for (int b = a + 1; b < vertices.Count; b++)
                {
                    Require(vertices[a].Length == vertices[b].Length, "Cloud shape comparisons need equal spatial sample coverage.");
                    double difference = 0;
                    for (int i = 0; i < vertices[a].Length; i++) difference += (vertices[a][i] - vertices[b][i]).sqrMagnitude;
                    float rms = Mathf.Sqrt((float)(difference / vertices[a].Length));
                    report.minimumVariantDifference = Mathf.Min(report.minimumVariantDifference, rms);
                    Require(rms > .075f, "Each cloud form needs a distinct normalized crown/outline, beyond a change of size: " +
                        orderedShapes[a].name + " / " + orderedShapes[b].name + ", RMS=" + rms);
                    Vector3 first = orderedShapes[a].bounds.size, second = orderedShapes[b].bounds.size;
                    float aspectDifference = Vector2.Distance(new Vector2(first.x / first.y, first.z / first.y),
                        new Vector2(second.x / second.y, second.z / second.y));
                    report.minimumAspectDifference = Mathf.Min(report.minimumAspectDifference, aspectDifference);
                    Require(aspectDifference > .075f, "Each cloud recipe needs distinct intrinsic proportions without anisotropic pool scaling: " +
                        orderedShapes[a].name + " / " + orderedShapes[b].name + ", aspect difference=" + aspectDifference);
                }
                report.sameFiveSharedShapes = report.distinctNormalizedForms = report.distinctAspectRatios = true;
                report.sky = CheckPool(nature, "clouds", 20);
                report.steps = CheckPool(nature, "bank", 5);
                Require(materials.Count == 1, "All sky and stepping clouds must share one material.");
                foreach (Material material in materials)
                    Require(material.shader.name == "RadioArchipelago/Island Surface" && material.renderQueue < 2500 &&
                        material.GetColor("_BaseColor").a == 1 && material.enableInstancing,
                        "Cloud softness must use shared opaque surface shading, without transparency or material instances.");
                report.opaqueSharedMaterial = true;
                nature.Dispose();
                report.cleanupComplete = owner.transform.childCount == 0;
                foreach (Mesh shape in shapes) report.cleanupComplete &= shape == null;
                foreach (Material material in materials) report.cleanupComplete &= material == null;
                Require(report.cleanupComplete, "Cloud shape resources survived Dispose.");
                report.pass = true;
                string folder = Path.GetFullPath("Library/RadioArchipelago/SparseCloudSteps"); Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "coherent-cloud-shapes.json"), JsonUtility.ToJson(report, true));
                Debug.Log("COHERENT_CLOUD_SHAPES PASS: " + JsonUtility.ToJson(report));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(stage); }
        }

        [MenuItem("Tools/Radio Archipelago/Preview coherent cloud shapes")]
        public static void CapturePreview()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating,
                "Capture coherent cloud shapes in stable Edit Mode.");
            const int layer = 30;
            const string qualityPath = "ProjectSettings/QualitySettings.asset";
            byte[] qualityBefore = File.ReadAllBytes(qualityPath);
            var previousPipeline = QualitySettings.renderPipeline;
            RenderTexture previousTarget = RenderTexture.active;
            Vector4 previousClear = Shader.GetGlobalVector("_RA_ClearViewCamera");
            bool previousFog = RenderSettings.fog;
            AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            Color previousAmbient = RenderSettings.ambientLight;
            float previousAmbientIntensity = RenderSettings.ambientIntensity, previousReflection = RenderSettings.reflectionIntensity;
            Light previousSun = RenderSettings.sun;
            var scene = EditorSceneManager.NewPreviewScene();
            var definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
            IslandLivingNature nature = null;
            RenderTexture target = null; Texture2D pixels = null;
            var report = new PreviewReport { unityVersion = Application.unityVersion, capturedUtc = DateTime.UtcNow.ToString("O") };
            string folder = Path.GetFullPath("Library/RadioArchipelago/CoherentClouds"); Directory.CreateDirectory(folder);
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(IslandPilotJobs.ContentPath + "/IslandPilotQuality.asset");
                RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.43f, .49f, .57f); RenderSettings.ambientIntensity = 1;
                RenderSettings.reflectionIntensity = 0;
                Shader.SetGlobalVector("_RA_ClearViewCamera", Vector4.zero);
                var source = new GameObject("Cloud preview source pool"); SceneManager.MoveGameObjectToScene(source, scene);
                definition.SparseCloudSteps = true;
                nature = source.AddComponent<IslandLivingNature>();
                nature.Configure(definition, new IslandRoute(new[] { Vector3.zero, new Vector3(0, 0, 600), new Vector3(200, 0, 600), new Vector3(200, 0, 0) }));
                var meshes = new List<Mesh>(); Material material = null;
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.sharedMesh.name.StartsWith(ShapePrefix, StringComparison.Ordinal)) continue;
                    if (!meshes.Contains(filter.sharedMesh)) meshes.Add(filter.sharedMesh);
                    material = filter.GetComponent<MeshRenderer>().sharedMaterial;
                }
                Require(meshes.Count == FormCount && material != null, "Generate all five coherent cloud forms before capturing their preview.");
                meshes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                source.SetActive(false);
                var models = new GameObject[meshes.Count];
                for (int i = 0; i < models.Length; i++)
                {
                    models[i] = new GameObject("Actual shared cloud variant " + i) { layer = layer };
                    SceneManager.MoveGameObjectToScene(models[i], scene);
                    models[i].AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    var renderer = models[i].AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                var cameraNode = new GameObject("Cloud proof camera"); SceneManager.MoveGameObjectToScene(cameraNode, scene);
                var camera = cameraNode.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.aspect = 1.5f; camera.nearClipPlane = .05f; camera.farClipPlane = 40;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.34f, .43f, .54f);
                camera.cullingMask = 1 << layer; camera.allowHDR = false; camera.allowMSAA = true;
                var data = cameraNode.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false; data.renderShadows = false; data.allowXRRendering = false;
                var keyNode = new GameObject("Neutral cloud preview key"); SceneManager.MoveGameObjectToScene(keyNode, scene);
                var key = keyNode.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.15f;
                key.color = new Color(1, .985f, .96f); key.cullingMask = 1 << layer; key.shadows = LightShadows.None;
                key.transform.rotation = Quaternion.Euler(38, -32, 0); RenderSettings.sun = key;
                var fillNode = new GameObject("Neutral cloud preview fill"); SceneManager.MoveGameObjectToScene(fillNode, scene);
                var fill = fillNode.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .32f;
                fill.color = new Color(.82f, .91f, 1); fill.cullingMask = 1 << layer; fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.Euler(12, 140, 0);
                target = new RenderTexture(1536, 1024, 24) { antiAliasing = 4 }; target.Create();
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                string[] names = { "front", "side", "top", "three-quarter" };
                Vector3[] eyes = { new Vector3(0, .1f, 6), new Vector3(6, .1f, 0), new Vector3(0, 6, .001f), new Vector3(4.5f, 3.2f, 5) };
                for (int i = 0; i < models.Length; i++)
                {
                    for (int m = 0; m < models.Length; m++) models[m].SetActive(m == i);
                    Vector3 center = meshes[i].bounds.center;
                    for (int view = 0; view < eyes.Length; view++)
                    {
                        Vector3 extent = meshes[i].bounds.extents;
                        camera.orthographicSize = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z)) * 1.25f;
                        camera.transform.SetPositionAndRotation(center + eyes[view], Quaternion.LookRotation(-eyes[view], view == 2 ? Vector3.forward : Vector3.up));
                        string filename = "variant-" + i + "-" + names[view] + ".png";
                        Capture(camera, target, pixels, Path.Combine(folder, filename)); report.files.Add(filename);
                    }
                }
                float spacing = 0;
                foreach (Mesh mesh in meshes) spacing = Mathf.Max(spacing, mesh.bounds.size.x + .9f);
                Bounds comparisonBounds = new Bounds(Vector3.zero, Vector3.zero);
                for (int i = 0; i < models.Length; i++)
                {
                    models[i].SetActive(true);
                    models[i].transform.position = new Vector3((i - (models.Length - 1) * .5f) * spacing, 0, 0);
                    comparisonBounds.Encapsulate(new Bounds(models[i].transform.position + meshes[i].bounds.center, meshes[i].bounds.size));
                }
                camera.orthographicSize = Mathf.Max(comparisonBounds.extents.x / camera.aspect, comparisonBounds.extents.y + comparisonBounds.extents.z) * 1.15f;
                Vector3 comparisonEye = comparisonBounds.center + new Vector3(0, 3.8f, 12);
                camera.transform.SetPositionAndRotation(comparisonEye, Quaternion.LookRotation(comparisonBounds.center - comparisonEye, Vector3.up));
                Capture(camera, target, pixels, Path.Combine(folder, "five-variants.png")); report.files.Add("five-variants.png");
            }
            finally
            {
                RenderTexture.active = previousTarget; Shader.SetGlobalVector("_RA_ClearViewCamera", previousClear);
                RenderSettings.fog = previousFog; RenderSettings.ambientMode = previousAmbientMode; RenderSettings.ambientLight = previousAmbient;
                RenderSettings.ambientIntensity = previousAmbientIntensity; RenderSettings.reflectionIntensity = previousReflection; RenderSettings.sun = previousSun;
                QualitySettings.renderPipeline = previousPipeline;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (nature != null) nature.Dispose();
                EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(definition);
            }
            byte[] qualityAfter = File.ReadAllBytes(qualityPath);
            report.qualityFileUnchanged = qualityBefore.Length == qualityAfter.Length;
            for (int i = 0; i < qualityBefore.Length && report.qualityFileUnchanged; i++) report.qualityFileUnchanged &= qualityBefore[i] == qualityAfter[i];
            Require(report.qualityFileUnchanged, "Cloud preview must leave the on-disk quality settings unchanged.");
            File.WriteAllText(Path.Combine(folder, "preview.json"), JsonUtility.ToJson(report, true));
            Debug.Log("COHERENT_CLOUD_PREVIEW PASS: " + folder);
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D pixels, string path)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(false, false);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }

        private static PoolReport CheckPool(IslandLivingNature nature, string fieldName, float scaleMultiplier)
        {
            FieldInfo poolField = typeof(IslandLivingNature).GetField(fieldName, Hidden);
            Require(poolField != null, "The actual cloud pool is missing: " + fieldName);
            var entries = (Array)poolField.GetValue(nature);
            Require(entries != null && entries.Length > 0, "The actual cloud pool is empty: " + fieldName);
            var report = new PoolReport { count = entries.Length, shapeIndices = new int[entries.Length], sizeClasses = new int[entries.Length] };
            var forms = new HashSet<int>(); var sizes = new HashSet<int>();
            var formCounts = new int[FormCount]; var sizeCounts = new int[SizeScales.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                object item = entries.GetValue(i);
                int shape = PoolValue<int>(item, "shapeIndex"), size = PoolValue<int>(item, "sizeClass");
                Require(shape >= 0 && shape < FormCount && size >= 0 && size < SizeScales.Length,
                    "Cloud metadata must address one of five forms and five size classes: " + fieldName);
                Transform root = PoolValue<Transform>(item, "root");
                var filter = root.GetComponentInChildren<MeshFilter>(true);
                string expectedName = ShapePrefix + shape;
                Require(filter != null && filter.sharedMesh != null &&
                    (filter.sharedMesh.name == expectedName || filter.sharedMesh.name.StartsWith(expectedName + " ", StringComparison.Ordinal)),
                    "Cloud form metadata disagrees with its actual shared mesh: " + fieldName + " / " + i);
                Vector3 expectedScale = Vector3.one * (SizeScales[size] * scaleMultiplier);
                Require((root.localScale - expectedScale).sqrMagnitude < .00000001f &&
                    (filter.transform.localScale - Vector3.one).sqrMagnitude < .00000001f,
                    "Clouds must use their discrete uniform size class, with no stretched child mesh: " + fieldName + " / " + i);
                if (i > 0) Require(shape != report.shapeIndices[i - 1] || size != report.sizeClasses[i - 1],
                    "Adjacent clouds must not repeat the same form and size class.");
                report.shapeIndices[i] = shape; report.sizeClasses[i] = size;
                forms.Add(shape); sizes.Add(size); formCounts[shape]++; sizeCounts[size]++;
            }
            report.distinctForms = forms.Count; report.distinctSizeClasses = sizes.Count;
            if (entries.Length >= FormCount)
            {
                Require(forms.Count == FormCount && sizes.Count == SizeScales.Length,
                    "A pool of at least five clouds must expose all five actual forms and size classes: " + fieldName);
                int minimumForms = int.MaxValue, maximumForms = 0, minimumSizes = int.MaxValue, maximumSizes = 0;
                for (int i = 0; i < FormCount; i++)
                {
                    minimumForms = Mathf.Min(minimumForms, formCounts[i]); maximumForms = Mathf.Max(maximumForms, formCounts[i]);
                    minimumSizes = Mathf.Min(minimumSizes, sizeCounts[i]); maximumSizes = Mathf.Max(maximumSizes, sizeCounts[i]);
                }
                Require(maximumForms - minimumForms <= 1 && maximumSizes - minimumSizes <= 1,
                    "Cloud form and size classes must be distributed evenly across the fixed pool: " + fieldName);
                report.allFormsAndSizes = true;
            }
            report.uniformScales = report.metadataMatchesGeometry = report.adjacentVariety = true;
            return report;
        }

        private static T PoolValue<T>(object item, string name)
        {
            FieldInfo field = item.GetType().GetField(name, Hidden);
            Require(field != null, "Cloud proof metadata is missing: " + name);
            return (T)field.GetValue(item);
        }

        private static ShapeReport CheckShape(Mesh mesh)
        {
            var report = new ShapeReport { name = mesh.name, vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3 };
            Require(report.triangles > 1000 && report.triangles <= 16000, "Cloud shell needs a bounded smooth triangle budget.");
            Vector3[] positions = mesh.vertices, normals = mesh.normals;
            int[] triangles = mesh.triangles, parent = new int[positions.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            var edges = new Dictionary<ulong, int>();
            double volume = 0, radiusSum = 0, radiusSquared = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 face = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                report.minimumTriangleAreaSquared = Mathf.Min(report.minimumTriangleAreaSquared, face.sqrMagnitude);
                Require(face.sqrMagnitude > 1e-20f, "Cloud shell contains a collapsed triangle.");
                Vector3 average = (normals[a] + normals[b] + normals[c]).normalized;
                // Unity's Vector3.normalized returns zero below its 1e-5
                // magnitude epsilon. A tiny valid marching-tetra face needs
                // explicit normalization; its area was checked independently.
                float exactDot = Vector3.Dot(face / Mathf.Sqrt(face.sqrMagnitude), average);
                if (exactDot < report.minimumFaceNormalDot)
                {
                    report.minimumFaceNormalDot = exactDot;
                    report.worstFace = Face(i / 3, a, b, c, positions, normals, face.sqrMagnitude,
                        Vector3.Dot(face.normalized, average), exactDot);
                }
                if (face.sqrMagnitude < .0000000001f)
                {
                    report.normalizationEpsilonFaces++;
                    if (report.epsilonFace == null) report.epsilonFace = Face(i / 3, a, b, c, positions, normals, face.sqrMagnitude,
                        Vector3.Dot(face.normalized, average), exactDot);
                }
                Edge(edges, a, b); Edge(edges, b, c); Edge(edges, c, a);
                Join(parent, a, b); Join(parent, b, c);
                volume += Vector3.Dot(positions[a], Vector3.Cross(positions[b], positions[c])) / 6.0;
            }
            int smoothEdges = 0;
            foreach (var edge in edges)
            {
                if (edge.Value == 1) report.boundaryEdges++;
                if (edge.Value != 2) report.nonmanifoldEdges++;
                int a = (int)(edge.Key >> 32), b = (int)(edge.Key & uint.MaxValue);
                float normalDot = Vector3.Dot(normals[a], normals[b]);
                report.minimumNormalDot = Mathf.Min(report.minimumNormalDot, normalDot);
                if (normalDot > .7f) smoothEdges++;
            }
            var components = new HashSet<int>();
            for (int i = 0; i < positions.Length; i++)
            {
                components.Add(Root(parent, i));
                Require(Finite(positions[i].x) && Finite(positions[i].y) && Finite(positions[i].z) &&
                    Finite(normals[i].x) && Finite(normals[i].y) && Finite(normals[i].z) && Mathf.Abs(normals[i].sqrMagnitude - 1) < .001f,
                    "Cloud geometry and shading normals must remain finite and normalized.");
                Vector3 local = positions[i] - mesh.bounds.center;
                float radius = new Vector3(local.x / mesh.bounds.extents.x, local.y / mesh.bounds.extents.y, local.z / mesh.bounds.extents.z).magnitude;
                radiusSum += radius; radiusSquared += radius * radius;
            }
            report.connectedComponents = components.Count;
            report.smoothEdgeFraction = (float)smoothEdges / edges.Count;
            report.eulerCharacteristic = positions.Length - edges.Count + report.triangles;
            report.signedVolume = (float)volume;
            report.radialVariation = Mathf.Sqrt(Mathf.Max(0, (float)(radiusSquared / positions.Length - Math.Pow(radiusSum / positions.Length, 2))));
            report.crownPeaks = CrownPeaks(SurfaceHeights(mesh));
            string diagnosticFolder = Path.GetFullPath("Library/RadioArchipelago/CoherentClouds"); Directory.CreateDirectory(diagnosticFolder);
            File.WriteAllText(Path.Combine(diagnosticFolder, "face-diagnostic-" + mesh.name[mesh.name.Length - 1] + ".json"), JsonUtility.ToJson(report, true));
            Require(report.minimumFaceNormalDot > .25f,
                "Cloud normals disagree with the visible surface winding: " + JsonUtility.ToJson(report.worstFace));
            Require(report.connectedComponents == 1 && report.boundaryEdges == 0 && report.nonmanifoldEdges == 0,
                "Cloud must be one closed continuous shell, with no separate intersecting sphere surfaces.");
            Require(report.signedVolume > 0 && report.minimumNormalDot > -.3f && report.smoothEdgeFraction > .94f,
                "Cloud volume must face outward and shade smoothly across rolling billows.");
            Require(report.radialVariation > .025f, "Cloud outline must be irregular rather than a plain smooth ellipsoid.");
            return report;
        }
        private static FaceEvidence Face(int triangle, int a, int b, int c, Vector3[] positions, Vector3[] normals,
            float areaSquared, float unityDot, float exactDot) => new FaceEvidence {
                triangle = triangle, a = a, b = b, c = c, areaSquared = areaSquared,
                unityNormalizedDot = unityDot, exactNormalizedDot = exactDot,
                p0 = positions[a], p1 = positions[b], p2 = positions[c], n0 = normals[a], n1 = normals[b], n2 = normals[c] };
        private const int HeightSamples = 41;
        private static float[] SurfaceHeights(Mesh mesh)
        {
            var heights = new float[HeightSamples * HeightSamples];
            for (int i = 0; i < heights.Length; i++) heights[i] = float.NaN;
            Vector3[] positions = mesh.vertices; Bounds bounds = mesh.bounds;
            for (int i = 0; i < positions.Length; i++)
                positions[i] = new Vector3((positions[i].x - bounds.center.x) / bounds.extents.x,
                    (positions[i].y - bounds.center.y) / bounds.extents.y, (positions[i].z - bounds.center.z) / bounds.extents.z);
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = positions[triangles[i]], b = positions[triangles[i + 1]], c = positions[triangles[i + 2]];
                float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < .00000001f) continue;
                int lowX = Mathf.Clamp(Mathf.CeilToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) + 1) * .5f * (HeightSamples - 1)), 0, HeightSamples - 1);
                int highX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1) * .5f * (HeightSamples - 1)), 0, HeightSamples - 1);
                int lowZ = Mathf.Clamp(Mathf.CeilToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) + 1) * .5f * (HeightSamples - 1)), 0, HeightSamples - 1);
                int highZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) + 1) * .5f * (HeightSamples - 1)), 0, HeightSamples - 1);
                for (int z = lowZ; z <= highZ; z++) for (int x = lowX; x <= highX; x++)
                {
                    float px = -1 + 2f * x / (HeightSamples - 1), pz = -1 + 2f * z / (HeightSamples - 1);
                    float u = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / denominator;
                    float v = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / denominator;
                    if (u < -.00001f || v < -.00001f || u + v > 1.00001f) continue;
                    float height = u * a.y + v * b.y + (1 - u - v) * c.y;
                    int index = x + z * HeightSamples;
                    if (float.IsNaN(heights[index]) || height > heights[index]) heights[index] = height;
                }
            }
            return heights;
        }
        private static int CrownPeaks(float[] heights)
        {
            int peaks = 0;
            for (int z = 1; z < HeightSamples - 1; z++) for (int x = 1; x < HeightSamples - 1; x++)
            {
                float height = heights[x + z * HeightSamples];
                if (float.IsNaN(height) || height < -.05f) continue;
                bool peak = true;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    float other = heights[x + dx + (z + dz) * HeightSamples];
                    if (!float.IsNaN(other) && other >= height - .0001f) peak = false;
                }
                if (peak) peaks++;
            }
            return peaks;
        }
        private static void Edge(Dictionary<ulong, int> edges, int a, int b)
        {
            if (a > b) { int swap = a; a = b; b = swap; }
            ulong key = ((ulong)(uint)a << 32) | (uint)b;
            edges.TryGetValue(key, out int count); edges[key] = count + 1;
        }
        private static int Root(int[] parent, int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        private static void Join(int[] parent, int a, int b) { a = Root(parent, a); b = Root(parent, b); if (a != b) parent[b] = a; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
