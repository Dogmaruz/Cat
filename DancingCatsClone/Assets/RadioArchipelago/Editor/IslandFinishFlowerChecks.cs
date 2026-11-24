using System;
using System.IO;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RadioArchipelago.Editor
{
    /// <summary>Checks the generated finale meadow's real meshes and safe collection corridor.</summary>
    public static class IslandFinishFlowerChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int cornflowers, daisies, dandelions, lowCorridorFlowers;
            public int renderers, triangles, vertices, flatSamples;
            public float maxCorridorPlantHeight, maxGroundError;
            public bool noColliders, completeDisposal;
            public string scope = "Actual generated flower meshes in a rotated preview scene. Ground top triangles and existing planar collection corridor remain unchanged; plants under the hover trail stay below 0.30 m. No per-flower objects, transparent cards, lights or colliders.";
        }

        [MenuItem("Radio Archipelago/Floating archipelago/Check finish flower meadow")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Flower meadow checks need stable Edit Mode.");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            if (definition == null || definition.Prefab == null)
                throw new InvalidOperationException("The current island definition is unavailable.");
            var scene = EditorSceneManager.NewPreviewScene();
            var node = new GameObject("Finish flower meadow validation");
            SceneManager.MoveGameObjectToScene(node, scene);
            var report = new Report();
            try
            {
                var glade = node.AddComponent<IslandFinishGlade>();
                Vector3 anchor = new Vector3(21, 0, -8);
                Vector3 forward = Quaternion.Euler(0, 37, 0) * Vector3.forward;
                const float ground = 5.2f;
                glade.Configure(anchor, forward, ground, 34, 14,
                    Borrow(definition.Prefab, "Terrain"), Borrow(definition.Prefab, "Cliffs"),
                    Borrow(definition.Prefab, "Grass"), Borrow(definition.Prefab, "Foliage"), 71237);
                report.cornflowers = glade.CornflowerCount;
                report.daisies = glade.DaisyCount;
                report.dandelions = glade.DandelionCount;
                report.lowCorridorFlowers = glade.LowCorridorFlowerCount;
                report.maxCorridorPlantHeight = glade.MaxCorridorPlantHeight;
                report.renderers = glade.GeneratedRendererCount;
                report.triangles = glade.GeneratedTriangleCount;
                report.noColliders = node.GetComponentsInChildren<Collider>(true).Length == 0;
                Require(report.cornflowers >= 35 && report.daisies >= 35 && report.dandelions >= 35,
                    "The finale needs all three readable flower species, including visible side beds.");
                Require(report.lowCorridorFlowers >= 50 && report.maxCorridorPlantHeight <= .30f,
                    "Low lawn flowers must remain under the planned hovering collection clearance.");
                Require(report.renderers == 4 && report.triangles < 180000 && report.noColliders,
                    "The meadow must keep four shared renderers, no colliders and a bounded geometry cost.");
                foreach (Mesh mesh in glade.OwnedMeshes)
                {
                    report.vertices += mesh.vertexCount;
                    foreach (Vector3 vertex in mesh.vertices)
                        Require(Finite(vertex), "A flower or meadow mesh contains a nonfinite vertex.");
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        int[] indices = mesh.GetIndices(submesh);
                        for (int i = 0; i < indices.Length; i++)
                            Require(indices[i] >= 0 && indices[i] < mesh.vertexCount,
                                "The generated mesh contains an invalid triangle index.");
                    }
                }
                for (int zi = 0; zi <= 38; zi++)
                    for (int xi = -8; xi <= 8; xi++)
                    {
                        var point = glade.GeneratedRoot.TransformPoint(new Vector3(xi * .5f, 0, zi - 2));
                        Require(glade.TrySampleGround(point, out float height),
                            "The complete collection corridor must have real top triangles beneath it.");
                        float error = Mathf.Abs(height - ground);
                        report.maxGroundError = Mathf.Max(report.maxGroundError, error);
                        Require(error < .0001f, "The safe central collection surface must stay exactly planar.");
                        report.flatSamples++;
                    }
                var owned = new Mesh[glade.OwnedMeshes.Count];
                for (int i = 0; i < owned.Length; i++) owned[i] = glade.OwnedMeshes[i];
                glade.Dispose();
                report.completeDisposal = glade.GeneratedRoot == null && glade.OwnedMeshes.Count == 0;
                foreach (Mesh mesh in owned) report.completeDisposal &= mesh == null;
                Require(report.completeDisposal, "All generated meadow meshes must be disposed on scene cleanup.");
                report.passed = true;
            }
            finally
            {
                Object.DestroyImmediate(node);
                EditorSceneManager.ClosePreviewScene(scene);
                Directory.CreateDirectory("Library/IslandPilotReview");
                File.WriteAllText("Library/IslandPilotReview/finish-flower-meadow.json", JsonUtility.ToJson(report, true));
            }
            Debug.Log("FINISH_FLOWER_MEADOW PASS: " + report.cornflowers + " cornflowers, " + report.daisies
                + " daisies, " + report.dandelions + " dandelions; four renderers; " + report.triangles
                + " triangles; " + report.flatSamples + " exact central ground samples; low centre and complete disposal.");
        }

        private static Material Borrow(GameObject prefab, string suffix)
        {
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && material.name.IndexOf("_" + suffix, StringComparison.Ordinal) >= 0)
                        return material;
            throw new InvalidOperationException("Missing borrowed meadow material: " + suffix);
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
