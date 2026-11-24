using System;
using System.IO;
using System.Linq;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DancingCats.Clone.Editor
{
    [InitializeOnLoad]
    public static class IslandLightingDiagnostics
    {
        [Serializable] private class NormalReport { public bool roadNormalsUp; public int samples; public float minimumDot, averageDot; public ColorRecord[] colors; }
        [Serializable] private class ColorRecord { public string mesh; public int vertices; public Color minimum, maximum, average; }
        [Serializable] private class LightRecord
        { public string name, type; public bool enabled; public float intensity; public uint layers; public Vector3 position, direction; }
        [Serializable] private class RuntimeReport
        { public bool sunEnabled, lightLayers; public string sun, pipeline; public LightRecord[] lights; public uint[] worldLayers; }
        private static bool captured;
        static IslandLightingDiagnostics()
        {
            EditorApplication.playModeStateChanged += mode => { if (mode == PlayModeStateChange.EnteredPlayMode) captured = false; };
            EditorApplication.update += CaptureRuntime;
        }

        private static void CaptureRuntime()
        {
            if (captured || !EditorApplication.isPlaying || Time.frameCount < 20) return;
            var stage = UnityEngine.Object.FindAnyObjectByType<IslandStagePresentation>();
            if (stage == null || stage.Route == null) return;
            var report = new RuntimeReport
            {
                sun = RenderSettings.sun != null ? RenderSettings.sun.name : "none",
                sunEnabled = RenderSettings.sun != null && RenderSettings.sun.enabled && RenderSettings.sun.gameObject.activeInHierarchy,
                lightLayers = Shader.IsKeywordEnabled("_LIGHT_LAYERS"),
                pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "none",
                lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Select(l => new LightRecord
                { name = l.name, type = l.type.ToString(), enabled = l.enabled, intensity = l.intensity, layers = (uint)l.renderingLayerMask, position = l.transform.position, direction = -l.transform.forward }).ToArray(),
                worldLayers = stage.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.renderingLayerMask).Distinct().ToArray()
            };
            Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
            File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "lighting-runtime.json"), JsonUtility.ToJson(report, true)); captured = true;
        }

        public static void InspectAssets()
        {
            string path = IslandPilotJobs.ImportPath + "/emerald_island.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool previous = importer.isReadable;
            try
            {
                importer.isReadable = true; importer.SaveAndReimport();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IslandPilotJobs.ContentPath + "/EmeraldIsland.prefab");
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    int count = 0; double sum = 0; float min = 1;
                    foreach (var mesh in instance.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.StartsWith("RA_Road", StringComparison.Ordinal)))
                    {
                        var vertices = mesh.sharedMesh.vertices; var normals = mesh.sharedMesh.normals; var triangles = mesh.sharedMesh.triangles;
                        var matrix = mesh.transform.localToWorldMatrix; var normalMatrix = matrix.inverse.transpose;
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            var a = matrix.MultiplyPoint3x4(vertices[triangles[i]]); var b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]); var c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                            if (Mathf.Abs(a.y) > .002f || Mathf.Abs(b.y) > .002f || Mathf.Abs(c.y) > .002f) continue;
                            if (Mathf.Abs(Vector3.Cross(b - a, c - a).normalized.y) < .99f) continue;
                            for (int j = 0; j < 3; j++)
                            { float dot = Vector3.Dot(normalMatrix.MultiplyVector(normals[triangles[i + j]]).normalized, Vector3.up); min = Mathf.Min(min, dot); sum += dot; count++; }
                        }
                    }
                    var colors = instance.GetComponentsInChildren<MeshFilter>(true).GroupBy(m => m.name.Split('_').ElementAtOrDefault(1)).Select(g =>
                    {
                        var mesh = g.First(); var data = mesh.sharedMesh.colors; Color lo = Color.white, hi = Color.black, total = Color.clear;
                        foreach (var c in data) { lo = new Color(Mathf.Min(lo.r,c.r),Mathf.Min(lo.g,c.g),Mathf.Min(lo.b,c.b),1); hi = new Color(Mathf.Max(hi.r,c.r),Mathf.Max(hi.g,c.g),Mathf.Max(hi.b,c.b),1); total += c; }
                        return new ColorRecord { mesh = mesh.name, vertices = data.Length, minimum = lo, maximum = hi, average = data.Length > 0 ? total / data.Length : Color.clear };
                    }).ToArray();
                    var report = new NormalReport { samples = count, minimumDot = min, averageDot = count > 0 ? (float)(sum / count) : 0, roadNormalsUp = count > 0 && min > .98f, colors = colors };
                    Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
                    File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "road-normals.json"), JsonUtility.ToJson(report, true));
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            finally { importer.isReadable = previous; importer.SaveAndReimport(); }
        }
    }
}
