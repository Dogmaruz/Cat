using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Actual mesh rejection and repair proof; never writes a chart or scenery asset.</summary>
    public static class IslandFullSupportLaneRepairChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed, optInDefaultOff, narrowCourseRejected, wideCourseRepaired;
            public bool musicalDataPreserved, openingPreserved, failedInputUnchanged;
            public string checkedUtc;
            public IslandFullSupportLaneRepair.Report repair;
        }

        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Full-support lane repair checks require stable Edit Mode.");
            var option = typeof(IslandAbyssChartAuthoring).GetMethod("Build").GetParameters().Last();
            Require(option.Name == "repairFullSupportLanes" && option.ParameterType == typeof(bool) &&
                option.IsOptional && !(bool)option.DefaultValue, "Full-support lane repair must be explicitly opt-in and default off.");
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O"), optInDefaultOff = true };
            // Match the dense production route contract. A four-corner polygon
            // interpolates its corner headings across each entire edge, so its
            // lateral basis is not forward/right even far along a straight XZ edge.
            var points = new List<Vector3>();
            for (int i = 0; i < 300; i++) points.Add(new Vector3(0, 0, i));
            for (int i = 0; i < 100; i++) points.Add(new Vector3(i, 0, 300));
            for (int i = 0; i < 300; i++) points.Add(new Vector3(100, 0, 300 - i));
            for (int i = 0; i < 100; i++) points.Add(new Vector3(100 - i, 0, 0));
            var route = new IslandRoute(points);
            Require(Vector3.Angle(route.Evaluate(24).Tangent, Vector3.forward) < .001f,
                "The local fixture must actually have a straight, aligned frame.");
            var narrow = Fixture();
            string original = JsonUtility.ToJson(narrow);
            var narrowSettings = new IslandDynamicSupportSpacing.Settings(0, 1.20f, 35, 4, 6.5f, 1.8f);
            Require(!IslandSpanSupportSpacing.Audit(narrow, route, narrowSettings).passed,
                "The real .804916m exit-gap fixture must reject overlapping full-size Hold and round bodies.");
            foreach (float extreme in new[] { -.5f, .5f })
            {
                var position = narrow.elements[7].pos; position.x = extreme; narrow.elements[7].pos = position;
                Require(!IslandSpanSupportSpacing.Audit(narrow, route, narrowSettings).passed,
                    "Even the full short-flight steering extrema must retain a narrow-course mesh overlap.");
            }
            narrow = Fixture();
            try { IslandFullSupportLaneRepair.Repair(narrow, route, narrowSettings); }
            catch (InvalidDataException error)
            {
                Require(error.Message.Contains("reachable static-round"),
                    "The narrow fixture must fail for geometric feasibility. Actual exception: " + error);
                report.narrowCourseRejected = true;
            }
            Require(report.narrowCourseRejected, "At1.8 world lane scale no .5-native shift can clear both full-size caps.");
            Require(JsonUtility.ToJson(narrow) == original, "An infeasible repair must not partially mutate its input.");
            report.failedInputUnchanged = true;

            var wide = Fixture();
            var before = Fixture();
            var wideSettings = new IslandDynamicSupportSpacing.Settings(0, 1.20f, 35, 4, 6.5f, 2.4f);
            Require(!IslandSpanSupportSpacing.Audit(wide, route, wideSettings).passed,
                "Widening alone must not conceal the original overlapping lane layout.");
            report.repair = IslandFullSupportLaneRepair.Repair(wide, route, wideSettings);
            Require(report.repair.repairedRounds == 1 && report.repair.violationsBefore == 1 && report.repair.violationsAfter == 0 &&
                IslandSpanSupportSpacing.Audit(wide, route, wideSettings).passed,
                "The widened course must produce one geometrically safe minimal lane repair.");
            Require(IslandDynamicSupportSpacing.CanKeepRadius(route, wideSettings, wide.elements, 7, .65f),
                "The repaired round must retain its full native radius against every coexisting round/mover.");
            Require(Mathf.Abs(wide.elements[7].pos.x - wide.elements[6].pos.x) <=
                DancingCats.Clone.Editor.ChartRouteComposer.MaxLateralShift(wide.segments[7].audioTime - wide.segments[6].endAudioTime) + .0001f,
                "Hold release steering must remain within the unchanged short-flight reachability budget.");
            IslandChartAuthoring.Verify(before, wide);
            for (int i = 0; i < wide.elements.Length; i++)
            {
                Require(before.elements[i].updateType == wide.elements[i].updateType &&
                    before.elements[i].msLeft == wide.elements[i].msLeft && before.elements[i].mstartPos == wide.elements[i].mstartPos,
                    "The repair must preserve native movement types and phases.");
                if (i <= 5) Require(before.elements[i].pos == wide.elements[i].pos, "Opening supports must remain exact.");
            }
            report.openingPreserved = report.musicalDataPreserved = report.wideCourseRepaired = report.passed = true;
            Directory.CreateDirectory(MoonResonanceIslandJobs.ReviewFolder);
            File.WriteAllText(MoonResonanceIslandJobs.ReviewFolder + "/full-support-lane-repair-checks.json", JsonUtility.ToJson(report, true));
            Debug.Log("ISLAND_FULL_SUPPORT_LANE_REPAIR_CHECKS PASS: unchanged short-flight envelope,1.8 RED/2.4 GREEN, actual capsule/round mesh clearance and musical preservation.");
        }

        private static MapTimeline Fixture()
        {
            float[] z = { 0, 3, 6, 9, 12, 15, 20, 23.079916f, 27, 31 };
            var elements = new MapElement[z.Length]; var segments = new MapSegment[z.Length];
            for (int i = 0; i < z.Length; i++)
            {
                string kind = i == 0 ? "Start" : i == 6 ? "Hold" : i == z.Length - 1 ? "FinishJump" : "Jump";
                float end = i == 6 ? 22.275f : z[i];
                float jump = i + 1 < z.Length ? z[i + 1] - end : 5;
                var element = elements[i] = new MapElement { kind = kind, id = i == 6 ? 2 : 0, sourceIndex = i,
                    pos = new Vector3(i == 7 ? .25f : 0, 0, z[i]), jumpDistance = jump,
                    audioTime = z[i] / 6.5, gameTime = z[i] / 6.5 + .6 };
                segments[i] = new MapSegment { kind = kind, id = element.id, sourceIndex = i, x = element.pos.x,
                    z = z[i], endZ = end, audioTime = element.audioTime, gameTime = element.gameTime,
                    endAudioTime = end / 6.5, endGameTime = end / 6.5 + .6, elementIndices = new[] { i },
                    motion = new MotionParameters { jumpDistance = jump } };
            }
            // Compiler-produced charts are canonical JSON. Normalize synthetic
            // null arrays once so the transactional copy tests geometry rather
            // than null-versus-empty serialization defaults.
            return JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(new MapTimeline {
                elements = elements, segments = segments, forwardSpeed = 6.5f, delayTime = .6f }));
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
