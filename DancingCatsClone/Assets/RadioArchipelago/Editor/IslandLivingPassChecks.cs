using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    public static class IslandLivingPassChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int meshSamples, boostedSamples;
            public float maxHoldGradeError, extraJumpAtApex;
        }

        public static void RunChecks()
        {
            IslandReliefJobs.RequireEditMode();
            IslandAbyssJobs.RunChecks();
            IslandAbyssChartAuthoringChecks.RunChecks();
            IslandDynamicSupportSpacingChecks.RunChecks();
            IslandCloseCameraChecks.RunChecks();
            IslandLivingNatureChecks.RunChecks();
            var report = new Report();
            var definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
            Mesh mesh = null;
            try
            {
                definition.DistanceScale = 1;
                var points = new[] { Vector3.zero, new Vector3(0, 0, 30), new Vector3(30, 0, 30), new Vector3(30, 0, 0) };
                var planar = new IslandRoute(points);
                float length = planar.Length;
                var elevation = new IslandElevationProfile(length, true, new[] {
                    new IslandElevationProfile.Key(0, 0), new IslandElevationProfile.Key(length * .25f, 6),
                    new IslandElevationProfile.Key(length * .5f, 0), new IslandElevationProfile.Key(length * .75f, 3),
                    new IslandElevationProfile.Key(length, 0) });
                var route = new IslandRoute(points, elevation);
                var surface = new IslandHoldSurface(route, definition, 5, 24, .25f, 0, .65f);
                mesh = surface.Mesh;
                CheckSurface(surface, route, 5, 24, definition.TileElevation, report);
                foreach (float progress in new[] { 5.1f, 12f, 20f, 23.98f })
                { surface.Advance(progress); CheckSurface(surface, route, 5, 24, definition.TileElevation, report); }
                for (int i = 0; i <= 1000; i++)
                {
                    float t = i / 1000f;
                    float boost = IslandFlightElevation.Boost(8, 3, 3 + 8 * t, 1.65f);
                    Require(boost >= 0 && !float.IsNaN(boost), "Higher jump produced invalid height");
                    if (i == 0 || i == 1000) Require(boost == 0, "Higher jump changed a contact endpoint");
                    Require(IslandFlightElevation.Boost(8, 3, 3 + 8 * t, 1) == 0, "Default legacy jump changed");
                    report.boostedSamples++;
                }
                report.extraJumpAtApex = IslandFlightElevation.Boost(8, 3, 7, 1.65f);
                Require(report.extraJumpAtApex > .5f, "Higher jump is not visibly higher");
                report.passed = true;
                Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
                File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "living-pass-checks.json"), JsonUtility.ToJson(report, true));
                Debug.Log("ISLAND_LIVING_PASS_CHECKS_PASS higher jump/closed glazed capsule/native nature/fixed camera/derived chart");
            }
            finally
            {
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static void CheckSurface(IslandHoldSurface surface, IslandRoute route, float start, float end, float tileHeight, Report report)
        {
            var mesh = surface.Mesh; var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
            var edges = new Dictionary<ulong, int>();
            Require(mesh.subMeshCount == 3, "Glaze/body/brass submeshes are missing");
            for (int part = 0; part < 3; part++)
            {
                var triangles = mesh.GetTriangles(part); Require(triangles.Length > 0, "A tile style material has no geometry");
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    AddEdge(edges, triangles[i], triangles[i + 1]); AddEdge(edges, triangles[i + 1], triangles[i + 2]);
                    AddEdge(edges, triangles[i + 2], triangles[i]);
                    var normal = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                    Require(normal.sqrMagnitude > 1e-14f, "Capsule has a zero-area triangle");
                    if (part == 1) Require(normal.y > 0, "Glazed top faces inward");
                }
            }
            foreach (int count in edges.Values) Require(count == 2, "Glazed capsule has an open edge or T-junction");
            foreach (var normal in normals) Require(!float.IsNaN(normal.x) && normal.sqrMagnitude > .9f, "Invalid capsule normal");
            int contour = (vertices.Length - 4) / 6;
            float a = route.Evaluate(start).Position.y, b = route.Evaluate(end).Position.y;
            for (int i = contour * 5; i < contour * 6; i++)
            {
                float expected = Mathf.LerpUnclamped(a, b, uv[i].y) + tileHeight;
                report.maxHoldGradeError = Mathf.Max(report.maxHoldGradeError, Mathf.Abs(vertices[i].y + surface.Pivot.y - expected));
            }
            Require(report.maxHoldGradeError < .001f, "A long support bends vertically");
            report.meshSamples++;
        }

        private static void AddEdge(Dictionary<ulong, int> edges, int a, int b)
        {
            ulong key = ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
            edges.TryGetValue(key, out int count); edges[key] = count + 1;
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
