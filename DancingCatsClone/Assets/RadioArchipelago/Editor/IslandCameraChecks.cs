using System;
using System.IO;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    public static class IslandCameraChecks
    {
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int views, points;
            public float minX = 1, maxX, minY = 1, maxY, maxRetreat;
        }

        [MenuItem("Radio Archipelago/Island pilot/Check camera safe framing")]
        public static void RunChecks()
        {
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandPilotJobs.DefinitionPath);
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points);
            var node = new GameObject("Camera framing checks") { hideFlags = HideFlags.HideAndDontSave };
            var camera = node.AddComponent<Camera>(); camera.enabled = false;
            var report = new Report();
            var points = new Vector3[24];
            var safe = new Rect(.10f, .12f, .80f, .72f);
            try
            {
                camera.fieldOfView = definition.CameraFieldOfView;
                camera.nearClipPlane = .08f;
                foreach (float aspect in new[] { .45f, 590f / 1280, 9f / 16, .75f, 16f / 9 })
                {
                    camera.aspect = aspect;
                    for (int step = 0; step < 2048; step++)
                    {
                        float distance = route.Length * step / 2048;
                        var frame = route.Evaluate(distance);
                        var target = route.Evaluate(distance + definition.CameraLookAhead).Position + Vector3.up;
                        Vector3 origin = frame.Position - frame.Tangent * definition.CameraBackDistance + Vector3.up * definition.CameraHeight;
                        Quaternion rotation = Quaternion.LookRotation(target - origin, Vector3.up);
                        // Extremes of both lanes and the maximum authored flight.
                        Vector3 actor = frame.Position + frame.Right * (step % 2 == 0 ? -1.5f : 1.5f) + Vector3.up * (definition.TileElevation + 2.25f);
                        int count = 0;
                        for (int side = -1; side <= 1; side += 2)
                            for (int fore = -1; fore <= 1; fore += 2)
                                for (int top = 0; top <= 1; top++)
                                    points[count++] = actor + frame.Right * (side * .85f) + frame.Tangent * (fore * .65f) + Vector3.up * (top == 0 ? -.1f : 2.6f);
                        for (int sample = 0; sample <= 7; sample++)
                        {
                            var path = route.Evaluate(distance + 12.5f * sample / 7);
                            for (int side = -1; side <= 1; side += 2)
                                points[count++] = path.Position + path.Right * (side * 2.2f) + Vector3.up * definition.TileElevation;
                        }
                        camera.transform.SetPositionAndRotation(origin, rotation);
                        IslandCameraFraming.Fit(camera, points, count, safe);
                        if (Quaternion.Angle(rotation, camera.transform.rotation) > .001f || camera.fieldOfView != definition.CameraFieldOfView)
                            throw new InvalidOperationException("Safe framing must preserve heading and FOV.");
                        report.maxRetreat = Mathf.Max(report.maxRetreat, Vector3.Dot(origin - camera.transform.position, camera.transform.forward));
                        report.views++;
                        for (int p = 0; p < count; p++)
                        {
                            Vector3 v = camera.WorldToViewportPoint(points[p]); report.points++;
                            report.minX = Mathf.Min(report.minX, v.x); report.maxX = Mathf.Max(report.maxX, v.x);
                            report.minY = Mathf.Min(report.minY, v.y); report.maxY = Mathf.Max(report.maxY, v.y);
                            if (v.z <= camera.nearClipPlane || v.x < safe.xMin - .0001f || v.x > safe.xMax + .0001f ||
                                v.y < safe.yMin - .0001f || v.y > safe.yMax + .0001f)
                                throw new InvalidOperationException("Camera clipped gameplay envelope at route " + distance + ", aspect " + aspect + ": " + v);
                        }
                    }
                }
                report.passed = true;
                Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
                File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "camera-checks.json"), JsonUtility.ToJson(report, true));
                Debug.Log("ISLAND_CAMERA_CHECKS_PASS views=" + report.views + " points=" + report.points);
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }
    }
}
