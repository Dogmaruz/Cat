using System;
using System.IO;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Fixed-radius camera geometry; no fitting, dolly, FOV animation or scene changes.</summary>
    public static class IslandCloseCameraChecks
    {
        [Serializable] private sealed class RouteDocument { public Vector3[] points = Array.Empty<Vector3>(); }
        [Serializable] public sealed class Range
        {
            public float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            public int points, clipped;
        }
        [Serializable] public sealed class Aspect
        {
            public float aspect;
            public Range actor = new Range(), landings = new Range();
        }
        [Serializable] public sealed class Report
        {
            public bool pass, defaultEnvelopeFits, fixedRadius, stationaryPoseStable, finishPath;
            public string scope = "2048 poses over the full elevated route at five aspects; both lane extremes, actor heights .9..8.2m/body half-width .9m, four landing rim corners at reachable gaps 1..10m.";
            public float back, height, lookAhead, focusHeight, headingLead, smoothing, fixedFieldOfView;
            public float lateralScale, cameraLateralFollow, nativeLateralLimit, worldLateralLimit, fullTravelWidth;
            public float radiusMinimum = float.MaxValue, radiusMaximum, radiusVariation, stationaryPositionDrift, stationaryRotationDrift;
            public float requiredFixedFieldOfView, suggestedFixedFieldOfView, parentWorldPositionDrift;
            public int poses;
            public Rect safeViewport;
            public Aspect[] aspects;
        }

        [MenuItem("Tools/Radio Archipelago/Check close camera geometry")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Check close camera geometry in Edit Mode.");
            const string folder = "Assets/RadioArchipelago/Imported/Islands/EmeraldAbyss/";
            var points = JsonUtility.FromJson<RouteDocument>(File.ReadAllText(folder + "emerald_abyss_route.json")).points;
            var elevation = IslandElevationProfile.FromJson(File.ReadAllText(folder + "play_elevation.json"));
            var route = new IslandRoute(points, elevation);
            // The wider Abyss course follows half of the courier's lateral travel.
            // Field of view remains one fixed value for the entire recording.
            Report report = CheckMath(route, 82, lateralScale: 2.1f, cameraLateralFollow: .5f, nativeLateralLimit: 1.5f);
            report.parentWorldPositionDrift = CheckParentApplication(route);
            report.pass &= report.parentWorldPositionDrift < .001f;
            string output = Path.GetFullPath("Library/RadioArchipelago/IslandCamera");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "close-camera-checks.json"), JsonUtility.ToJson(report, true));
            if (!report.pass) throw new InvalidOperationException("ISLAND_CLOSE_CAMERA FAIL: " + JsonUtility.ToJson(report));
            Debug.Log("ISLAND_CLOSE_CAMERA PASS geometry/state; default envelope fits=" + report.defaultEnvelopeFits +
                "; fixed FOV=" + report.fixedFieldOfView + "; required=" + report.requiredFixedFieldOfView +
                "; suggested=" + report.suggestedFixedFieldOfView + "; radius variation=" + report.radiusVariation);
        }

        /// <summary>Pure managed geometry reusable in a headless math harness.</summary>
        public static Report CheckMath(IslandRoute route, float fieldOfView = 58, float back = 9.5f, float height = 8,
            float lookAhead = 3.8f, float focusHeight = 3.6f, float headingLead = 3.2f, float smoothing = 7,
            float lateralScale = 1, float cameraLateralFollow = 1, float nativeLateralLimit = 1.3f,
            IslandGroundFinishPath groundFinish = null, float routeStartDistance = 0, float distanceScale = 1.18f)
        {
            var report = new Report { back = back, height = height, lookAhead = lookAhead, focusHeight = focusHeight,
                headingLead = headingLead, smoothing = smoothing, fixedFieldOfView = fieldOfView,
                lateralScale = lateralScale, cameraLateralFollow = cameraLateralFollow, nativeLateralLimit = nativeLateralLimit,
                worldLateralLimit = nativeLateralLimit * lateralScale, fullTravelWidth = 2 * nativeLateralLimit * lateralScale,
                finishPath = groundFinish != null,
                safeViewport = new Rect(.08f, .12f, .84f, .72f), aspects = new Aspect[5] };
            float sweepLength = groundFinish == null ? route.Length : (groundFinish.EndChartZ - groundFinish.TakeoffChartZ) * distanceScale + 30;
            float sweepStart = groundFinish == null ? route.Length : routeStartDistance + groundFinish.TakeoffChartZ * distanceScale - 25;
            if (groundFinish != null) report.scope = "2048 poses over final approach, unchanged native last jump and grounded coin run at five aspects; unchanged .9..8.2m actor envelope and full lane extremes.";
            float[] aspectValues = { .45f, 590f / 1280, 9f / 16, .75f, 16f / 9 };
            float delta = sweepLength / 2048 / 7.67f;
            float requiredTan = 0;
            for (int a = 0; a < report.aspects.Length; a++)
            {
                var aspect = new Aspect { aspect = aspectValues[a] };
                report.aspects[a] = aspect;
                var rig = new IslandCloseCameraRig();
                for (int step = 0; step < 4096; step++)
                {
                    float distance = sweepStart + sweepLength * (step - 2048) / 2048;
                    var frame = Sample(route, groundFinish, distance);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float nativeLateral = side * nativeLateralLimit, lateral = nativeLateral * lateralScale;
                        var pose = rig.Step(frame, Sample(route, groundFinish, distance + headingLead), lateral * cameraLateralFollow,
                            back, height, lookAhead, focusHeight, smoothing, side == -1 ? delta : 0, step == 0);
                        if (step < 2048) continue; // A complete lap settles heading before measurement.
                        report.poses++;
                        float radius = Vector3.Distance(pose.Position, pose.Anchor);
                        report.radiusMinimum = Mathf.Min(report.radiusMinimum, radius);
                        report.radiusMaximum = Mathf.Max(report.radiusMaximum, radius);
                        Quaternion inverse = Quaternion.Inverse(pose.Rotation);
                        for (int x = -1; x <= 1; x += 2)
                            for (int z = -1; z <= 1; z += 2)
                                for (int y = 0; y <= 1; y++)
                                {
                                    Vector3 actor = frame.Position + frame.Right * (lateral + x * .9f) + frame.Tangent * (z * .75f)
                                        + Vector3.up * (y == 0 ? .9f : 8.2f);
                                    AddPoint(actor, pose.Position, inverse, fieldOfView, aspect.aspect,
                                        report.safeViewport, aspect.actor, ref requiredTan);
                                }
                        for (int gap = 1; gap <= 10; gap++)
                        {
                            var next = Sample(route, groundFinish, distance + gap);
                            float shift = ChartRouteComposer.MaxLateralShift(gap / 7.67);
                            for (int lane = -1; lane <= 1; lane += 2)
                            {
                                float landingX = Mathf.Clamp(nativeLateral + lane * shift, -nativeLateralLimit, nativeLateralLimit) * lateralScale;
                                for (int edge = -1; edge <= 1; edge += 2)
                                    for (int fore = -1; fore <= 1; fore += 2)
                                {
                                    Vector3 landing = next.Position + next.Right * (landingX + edge * .7f)
                                        + next.Tangent * (fore * .7f) + Vector3.up * .9f;
                                    AddPoint(landing, pose.Position, inverse, fieldOfView, aspect.aspect,
                                        report.safeViewport, aspect.landings, ref requiredTan);
                                }
                            }
                        }
                    }
                }
            }
            report.radiusVariation = report.radiusMaximum - report.radiusMinimum;
            report.fixedRadius = report.radiusVariation < .001f;
            report.defaultEnvelopeFits = true;
            foreach (var aspect in report.aspects) report.defaultEnvelopeFits &= aspect.actor.clipped == 0 && aspect.landings.clipped == 0;
            report.requiredFixedFieldOfView = (float)(2 * Math.Atan(requiredTan) * 180 / Math.PI);
            report.suggestedFixedFieldOfView = Mathf.Ceil(report.requiredFixedFieldOfView + 1);
            var stationary = new IslandCloseCameraRig();
            float cameraLateral = nativeLateralLimit * lateralScale * cameraLateralFollow;
            var reference = stationary.Step(route, 137, cameraLateral, back, height, lookAhead, focusHeight, headingLead, smoothing, .016f, true);
            for (int i = 0; i < 360; i++)
            {
                var sample = stationary.Step(route, 137, cameraLateral, back, height, lookAhead, focusHeight, headingLead, smoothing, .016f, false);
                report.stationaryPositionDrift = Mathf.Max(report.stationaryPositionDrift, Vector3.Distance(reference.Position, sample.Position));
                report.stationaryRotationDrift = Mathf.Max(report.stationaryRotationDrift, Quaternion.Angle(reference.Rotation, sample.Rotation));
            }
            report.stationaryPoseStable = report.stationaryPositionDrift < .001f && report.stationaryRotationDrift < .01f;
            // An envelope miss fails this check and reports a fixed global FOV
            // recommendation. The runtime rig never retreats or changes FOV.
            report.pass = report.fixedRadius && report.stationaryPoseStable && report.defaultEnvelopeFits;
            return report;
        }

        public static Report CheckFinishMath(IslandRoute route, IslandGroundFinishPath path, float routeStartDistance, float distanceScale)
            => CheckMath(route, 82, lateralScale: 2.1f, cameraLateralFollow: .5f, nativeLateralLimit: 1.5f,
                groundFinish: path, routeStartDistance: routeStartDistance, distanceScale: distanceScale);

        private static IslandRoute.Frame Sample(IslandRoute route, IslandGroundFinishPath path, float distance)
            => path != null ? path.Evaluate(distance) : route.Evaluate(distance);

        private static void AddPoint(Vector3 world, Vector3 camera, Quaternion inverse, float fieldOfView, float aspect,
            Rect safe, Range range, ref float requiredTan)
        {
            Vector3 local = inverse * (world - camera);
            range.points++;
            if (local.z <= .08f) { range.clipped++; requiredTan = float.PositiveInfinity; return; }
            float x = local.x / local.z, y = local.y / local.z;
            float tan = Mathf.Tan(fieldOfView * Mathf.Deg2Rad * .5f);
            float vx = .5f + x / (2 * tan * aspect), vy = .5f + y / (2 * tan);
            range.minX = Mathf.Min(range.minX, vx); range.maxX = Mathf.Max(range.maxX, vx);
            range.minY = Mathf.Min(range.minY, vy); range.maxY = Mathf.Max(range.maxY, vy);
            if (vx < safe.xMin || vx > safe.xMax || vy < safe.yMin || vy > safe.yMax) range.clipped++;
            requiredTan = Mathf.Max(requiredTan, Mathf.Max(x >= 0 ? x / (2 * safe.xMax - 1) / aspect : -x / (1 - 2 * safe.xMin) / aspect,
                y >= 0 ? y / (2 * safe.yMax - 1) : -y / (1 - 2 * safe.yMin)));
        }

        private static float CheckParentApplication(IslandRoute route)
        {
            var carrier = new GameObject("Close camera isolated carrier") { hideFlags = HideFlags.HideAndDontSave };
            var camera = new GameObject("Close camera isolated pose");
            camera.transform.SetParent(carrier.transform, false);
            try
            {
                var rig = new IslandCloseCameraRig();
                var reference = rig.Step(route, 137, .8f, 9.5f, 8, 3.8f, 3.6f, 3.2f, 7, .016f, true);
                float drift = 0;
                for (int i = 0; i < 60; i++)
                {
                    carrier.transform.SetPositionAndRotation(new Vector3(i * .8f, i * .05f, -i * 1.2f), Quaternion.Euler(0, i * 4, 0));
                    var pose = rig.Step(route, 137, .8f, 9.5f, 8, 3.8f, 3.6f, 3.2f, 7, .016f, false);
                    camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                    drift = Mathf.Max(drift, Vector3.Distance(reference.Position, camera.transform.position));
                }
                return drift;
            }
            finally { UnityEngine.Object.DestroyImmediate(carrier); }
        }
    }
}
