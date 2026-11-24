using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Second-island route evidence, without first-island locations or scene mutations.</summary>
    public static class CoralOasisRouteChecks
    {
        private const string DefinitionPath = "Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.asset";
        private const string ContentPath = "Assets/RadioArchipelago/Content/Islands/CoralOasis/";
        private const string ImportPath = "Assets/RadioArchipelago/Imported/Islands/CoralOasis/";
        private const string OutputPath = "Library/RadioArchipelago/CoralOasis/route-checks.json";

        [Serializable] private sealed class RouteDocument
        {
            public bool closed;
            public Vector3[] points;
        }

        [Serializable] public sealed class Report
        {
            public bool passed, isolatedAssets, closed, nonIntersecting, windingBothWays, gentleOpening, finalClearSegment;
            public string definition, sourceRoute, sourceProfile, finalProfile, chart;
            public string scope = "Current Coral Oasis definition and exported closed XZ route. Dense final-height and heading samples, non-neighbour segment intersections and clearance, native finale frame, camera envelope. Imported solid-triangle clearance is reported separately by CoralOasisTerrainChecks.";
            public int routePoints, headingSamples, crossIntersections, nonlocalNearPairs;
            public float length, spanX, spanZ, sourceMinimumHeight, sourceMaximumHeight, finalMinimumHeight, finalMaximumHeight;
            public float sourceMaximumGrade, finalMaximumGrade, maximumHeadingDegreesPerMetre, estimatedMinimumBendRadius;
            public float accumulatedLeftDegrees, accumulatedRightDegrees, minimumNonlocalSeparation = float.PositiveInfinity;
            public float seamPositionError, seamHeadingError, seamGradeError;
            public float finishTakeoffDistance, finishLandingDistance, finishEndDistance, finishExitYaw;
            public float finishMaximumGrade, finishHeadingVariation, finalSurfaceHeight;
            public IslandCloseCameraChecks.Report camera;
        }

        [MenuItem("Radio Archipelago/Coral Oasis/Check route and camera")]
        public static void RunChecks()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Coral Oasis route checks require stable Edit Mode.");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(DefinitionPath);
            Require(definition != null && definition.RouteJson != null && definition.ElevationJson != null &&
                definition.Chart != null && definition.Prefab != null, "The Coral Oasis definition is incomplete.");
            var report = new Report
            {
                definition = DefinitionPath,
                sourceRoute = AssetDatabase.GetAssetPath(definition.RouteJson),
                finalProfile = AssetDatabase.GetAssetPath(definition.ElevationJson),
                chart = AssetDatabase.GetAssetPath(definition.Chart),
                sourceProfile = ImportPath + "play_elevation.json"
            };
            report.isolatedAssets = report.sourceRoute.StartsWith(ImportPath, StringComparison.Ordinal) &&
                report.chart.StartsWith(ContentPath, StringComparison.Ordinal) &&
                report.finalProfile.StartsWith(ContentPath, StringComparison.Ordinal) &&
                AssetDatabase.GetAssetPath(definition.Prefab).StartsWith(ContentPath, StringComparison.Ordinal);
            Require(report.isolatedAssets, "The second island must own its scenery, route, derived chart and elevation assets.");
            var document = JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text);
            Require(document != null && document.closed && document.points != null && document.points.Length >= 100,
                "Coral Oasis needs a sampled closed route, not an open or coarse control polygon.");
            report.closed = document.closed;
            report.routePoints = document.points.Length;
            var source = IslandElevationProfile.FromJson(File.ReadAllText(report.sourceProfile));
            var final = IslandElevationProfile.FromJson(definition.ElevationJson.text);
            var route = new IslandRoute(document.points, final);
            report.length = route.Length;
            Require(route.Length >= 640 && route.Length <= 760, "The second island route must fit the planned musical journey.");
            Require(Mathf.Abs(source.Length - route.Length) < .01f && source.Closed && final.Closed,
                "Source and final elevations must agree with the same closed planar perimeter.");
            report.sourceMinimumHeight = report.finalMinimumHeight = float.PositiveInfinity;
            report.sourceMaximumHeight = report.finalMaximumHeight = float.NegativeInfinity;
            var min = new Vector3(float.PositiveInfinity, 0, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, 0, float.NegativeInfinity);
            for (float s = 0; s < route.Length; s += .25f)
            {
                float sourceHeight = source.Evaluate(s), sourceGrade = source.Grade(s);
                float finalHeight = final.Evaluate(s), finalGrade = final.Grade(s);
                report.sourceMinimumHeight = Mathf.Min(report.sourceMinimumHeight, sourceHeight);
                report.sourceMaximumHeight = Mathf.Max(report.sourceMaximumHeight, sourceHeight);
                report.finalMinimumHeight = Mathf.Min(report.finalMinimumHeight, finalHeight);
                report.finalMaximumHeight = Mathf.Max(report.finalMaximumHeight, finalHeight);
                report.sourceMaximumGrade = Mathf.Max(report.sourceMaximumGrade, Mathf.Abs(sourceGrade));
                report.finalMaximumGrade = Mathf.Max(report.finalMaximumGrade, Mathf.Abs(finalGrade));
                var frame = route.Evaluate(s);
                min = Vector3.Min(min, frame.Position); max = Vector3.Max(max, frame.Position);
                float turn = Vector3.SignedAngle(route.Evaluate(s - .25f).Tangent,
                    route.Evaluate(s + .25f).Tangent, Vector3.up) * 2;
                report.maximumHeadingDegreesPerMetre = Mathf.Max(report.maximumHeadingDegreesPerMetre, Mathf.Abs(turn));
                if (turn > 0) report.accumulatedRightDegrees += turn * .25f;
                else report.accumulatedLeftDegrees -= turn * .25f;
                report.headingSamples++;
            }
            report.spanX = max.x - min.x; report.spanZ = max.z - min.z;
            report.estimatedMinimumBendRadius = 180 / Mathf.PI / Mathf.Max(.000001f, report.maximumHeadingDegreesPerMetre);
            report.windingBothWays = report.accumulatedLeftDegrees >= 30 && report.accumulatedRightDegrees >= 30;
            Require(report.windingBothWays, "The journey needs broad bends in both directions, not only a perimeter circle.");
            Require(report.maximumHeadingDegreesPerMetre <= 3.25f,
                "A route bend is too abrupt for the fixed close camera: estimated radius=" + report.estimatedMinimumBendRadius);
            Require(report.spanX > 70 && report.spanZ > 70, "The planned island needs a substantial varied footprint.");
            Require(report.sourceMaximumGrade <= .5f && report.finalMaximumGrade <= .7501f &&
                report.sourceMaximumHeight - report.sourceMinimumHeight >= 8 &&
                report.finalMaximumHeight - report.finalMinimumHeight >= 6,
                "The island requires substantial but gentle climbs and descents after Hold-grade protection.");
            var before = route.Evaluate(route.Length - .001f); var after = route.Evaluate(.001f);
            report.seamPositionError = Vector3.Distance(before.Position, after.Position);
            report.seamHeadingError = Vector3.Angle(before.Tangent, after.Tangent);
            report.seamGradeError = Mathf.Abs(before.Grade - after.Grade);
            Require(report.seamPositionError < .005f && report.seamHeadingError < .05f && report.seamGradeError < .01f,
                "Repeating the route must not introduce a position, direction or height-grade seam.");
            CheckPlanarSeparation(document.points, report);
            var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
            CheckOpeningAndFinish(map, definition, route, report);
            report.camera = IslandCloseCameraChecks.CheckMath(route, definition.CameraFieldOfView,
                definition.CameraBackDistance, definition.CameraHeight, definition.CameraLookAhead,
                definition.CameraFocusHeight, definition.CameraHeadingLead, definition.CameraSmoothing,
                definition.LateralScale, definition.CameraLateralFollow, NativeRhythmRules.HorizontalLimit);
            Require(report.camera.pass, "The current fixed camera envelope does not fit the new route: " +
                "required FOV=" + report.camera.requiredFixedFieldOfView + ", actual=" + definition.CameraFieldOfView);
            report.passed = true;
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, JsonUtility.ToJson(report, true));
            Debug.Log("CORAL_OASIS_ROUTE PASS: " + report.length + "m, elevation " + report.finalMinimumHeight + ".." +
                report.finalMaximumHeight + "m, minimum bend radius " + report.estimatedMinimumBendRadius + "m; closed winding course, native finale and fixed camera verified.");
        }

        private static void CheckOpeningAndFinish(MapTimeline map, IslandStageDefinition definition, IslandRoute route, Report report)
        {
            Require(map?.elements != null && map.id == definition.SongId, "The derived song map and island definition disagree.");
            int opening = 0; MapElement finish = null;
            foreach (var element in map.elements)
            {
                if (element.kind == "FinishJump") finish = element;
                if (element.kind == "Start" || element.id != 0 && element.id != 1 && element.id != 2 &&
                    element.id != 3 && element.id != 4 && element.id != 6 && element.id != 9) continue;
                if (opening++ < 3) Require(Mathf.Abs(element.pos.x) < .0001f, "The initial three landings must retain the approved gentle opening.");
            }
            report.gentleOpening = opening >= 3;
            Require(report.gentleOpening && finish != null && definition.EnableGroundFinish, "The genuine final flight and reward meadow must be present.");
            float landingZ = finish.pos.z + finish.jumpDistance;
            var path = new IslandGroundFinishPath(route, definition.StartDistance, definition.DistanceScale,
                definition.TileElevation, finish.pos.z, landingZ, landingZ + 29, definition.FinishExitYaw);
            report.finishTakeoffDistance = definition.StartDistance + finish.pos.z * definition.DistanceScale;
            report.finishLandingDistance = definition.StartDistance + landingZ * definition.DistanceScale;
            report.finishEndDistance = definition.StartDistance + (landingZ + 29) * definition.DistanceScale;
            report.finishExitYaw = definition.FinishExitYaw;
            report.finalSurfaceHeight = path.GroundHeight;
            Require(report.finishTakeoffDistance > 600 && report.finishEndDistance < route.Length - 2,
                "The final flight and reward passage must fit within their reserved segment without a lap wrap.");
            var direction = route.Evaluate(report.finishTakeoffDistance).Tangent;
            for (float s = report.finishTakeoffDistance - 12; s <= report.finishEndDistance; s += .25f)
            {
                var frame = route.Evaluate(s);
                report.finishMaximumGrade = Mathf.Max(report.finishMaximumGrade, Mathf.Abs(frame.Grade));
                report.finishHeadingVariation = Mathf.Max(report.finishHeadingVariation, Vector3.Angle(direction, frame.Tangent));
            }
            report.finalClearSegment = report.finishMaximumGrade < .01f && report.finishHeadingVariation < 1;
            Require(report.finalClearSegment, "The final reward meadow needs its reserved straight, flat exit.");
        }

        private static void CheckPlanarSeparation(Vector3[] points, Report report)
        {
            int count = points.Length;
            var arc = new double[count + 1];
            for (int i = 0; i < count; i++) arc[i + 1] = arc[i] + Vector3.Distance(points[i], points[(i + 1) % count]);
            for (int i = 0; i < count; i++)
                for (int j = i + 2; j < count; j++)
                {
                    if (i == 0 && j == count - 1) continue;
                    Vector3 a = points[i], b = points[(i + 1) % count], c = points[j], d = points[(j + 1) % count];
                    if (Intersects(a, b, c, d)) report.crossIntersections++;
                    double separation = arc[j] - arc[i]; separation = Math.Min(separation, arc[count] - separation);
                    if (separation < 35) continue;
                    float gap = Mathf.Min(PointSegment(a, c, d), Mathf.Min(PointSegment(b, c, d),
                        Mathf.Min(PointSegment(c, a, b), PointSegment(d, a, b))));
                    report.minimumNonlocalSeparation = Mathf.Min(report.minimumNonlocalSeparation, gap);
                    if (gap < 8.5f) report.nonlocalNearPairs++;
                }
            report.nonIntersecting = report.crossIntersections == 0 && report.nonlocalNearPairs == 0;
            Require(report.nonIntersecting, "Unrelated route sections cross or leave insufficient space for full-width moving platforms.");
        }

        private static bool Intersects(Vector3 a, Vector3 b, Vector3 c, Vector3 d) =>
            Cross(b - a, c - a) * Cross(b - a, d - a) < 0 && Cross(d - c, a - c) * Cross(d - c, b - c) < 0;
        private static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
        private static float PointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 v = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, v) / Mathf.Max(.000001f, v.sqrMagnitude));
            return Vector3.Distance(p, a + v * t);
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
