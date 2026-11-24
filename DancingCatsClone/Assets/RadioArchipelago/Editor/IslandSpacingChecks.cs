using System;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Checks actual closed-route footprints, including later lap aliases.</summary>
    public static class IslandSpacingChecks
    {
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class Report
        {
            public bool passed, futureLapsDoNotShrinkEarlierTiles, musicalTimesUnchanged;
            public int landingCount, baseRadiusOverlaps, adjustedRadiusOverlaps;
            public float minimumRadius, worldDistanceScale, meanLateralStep;
        }

        public static void RunChecks()
        {
            string originalJson = File.ReadAllText(IslandPilotJobs.OriginalMap);
            var map = IslandChartAuthoring.Create(originalJson);
            IslandChartAuthoring.Verify(JsonUtility.FromJson<MapTimeline>(originalJson), map);
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(File.ReadAllText(IslandPilotJobs.ImportPath + "/emerald_route.json")).points);
            var landings = map.elements.Where(e => e.id == 0 || e.id == 1 || e.id == 4).ToArray();
            var centers = new Vector3[landings.Length];
            var z = new float[landings.Length];
            var preferred = new float[landings.Length];
            var radii = new float[landings.Length];
            float lateral = 0;
            for (int i = 0; i < landings.Length; i++)
            {
                var e = landings[i];
                var frame = route.Evaluate(e.pos.z * IslandChartAuthoring.WorldDistanceScale);
                centers[i] = frame.Position + frame.Right * ChartRouteComposer.LandingX(e) + Vector3.up * e.pos.y;
                z[i] = e.pos.z; preferred[i] = IslandSupportSpacing.NativeJumpRadius;
                if (i > 0) lateral += Mathf.Abs(ChartRouteComposer.LandingX(e) - ChartRouteComposer.LandingX(landings[i - 1]));
            }
            float window = 38 / IslandChartAuthoring.WorldDistanceScale;
            IslandSupportSpacing.ResolveRadii(centers, z, window, preferred, radii);
            var report = new Report { musicalTimesUnchanged = true, landingCount = landings.Length,
                minimumRadius = radii.Min(), worldDistanceScale = IslandChartAuthoring.WorldDistanceScale,
                meanLateralStep = lateral / Mathf.Max(1, landings.Length - 1) };
            for (int i = 0; i < centers.Length; i++)
                for (int j = i + 1; j < centers.Length; j++)
                {
                    if (Mathf.Abs(z[i] - z[j]) > window) continue;
                    float distance = Vector3.Distance(centers[i], centers[j]);
                    if (distance < preferred[i] + preferred[j] + IslandSupportSpacing.MinimumGap - .0001f) report.baseRadiusOverlaps++;
                    if (distance < radii[i] + radii[j] + IslandSupportSpacing.MinimumGap - .0001f) report.adjustedRadiusOverlaps++;
                }
            var samePlace = new[] { Vector3.zero, Vector3.zero };
            var differentLaps = new[] { 0f, route.Length / IslandChartAuthoring.WorldDistanceScale };
            var wanted = new[] { .65f, .65f }; var actual = new float[2];
            IslandSupportSpacing.ResolveRadii(samePlace, differentLaps, window, wanted, actual);
            report.futureLapsDoNotShrinkEarlierTiles = actual[0] == .65f && actual[1] == .65f;
            report.passed = report.adjustedRadiusOverlaps == 0 && report.futureLapsDoNotShrinkEarlierTiles && report.minimumRadius >= .33f;
            File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "spacing-checks.json"), JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException("Island spacing check failed: " + JsonUtility.ToJson(report));
            Debug.Log("ISLAND_SPACING_PASS radiusMin=" + report.minimumRadius + "; fixedPairs=" + report.baseRadiusOverlaps + "; speed=" + report.worldDistanceScale);
        }
    }
}
