using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Opt-in Editor choreography repair against actual full-size ceramic support meshes.</summary>
    public static class IslandFullSupportLaneRepair
    {
        private const float SearchStep = .0025f;
        [Serializable] public sealed class Change
        {
            public int sourceIndex;
            public float originalLane, repairedLane, reachableMinimum, reachableMaximum;
        }
        [Serializable] public sealed class Report
        {
            public int repairedRounds, violationsBefore, violationsAfter;
            public float maximumLateralChange, searchStep = SearchStep;
            public bool musicalTimingPreserved, fullSizeClearance;
            public Change[] changes;
        }

        /// <summary>Changes only static-round X and matching segment X, and commits only a fully passing result.</summary>
        public static Report Repair(MapTimeline map, IslandRoute route, IslandDynamicSupportSpacing.Settings settings,
            float tileThickness = .30f, float tileElevation = 1)
        {
            if (map?.elements == null || map.segments == null || route == null)
                throw new ArgumentException("A complete authored chart and actual final route are required.");
            var working = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(map));
            var report = RepairInPlace(working, route, settings, tileThickness, tileElevation);
            IslandChartAuthoring.Verify(map, working);
            for (int i = 0; i < map.elements.Length; i++)
            {
                var position = map.elements[i].pos; position.x = working.elements[i].pos.x; map.elements[i].pos = position;
            }
            for (int i = 0; i < map.segments.Length; i++) map.segments[i].x = working.segments[i].x;
            report.musicalTimingPreserved = true;
            return report;
        }

        private static Report RepairInPlace(MapTimeline map, IslandRoute route, IslandDynamicSupportSpacing.Settings settings,
            float tileThickness, float tileElevation)
        {
            var nodes = map.segments.Where(s => s != null && s.kind != "Theme").OrderBy(s => s.audioTime).ToArray();
            var opening = new HashSet<int>(nodes.Where(Round).Take(5).Select(s => s.sourceIndex));
            var changes = new List<Change>();
            var audit = IslandSpanSupportSpacing.Audit(map, route, settings, tileThickness, tileElevation);
            var report = new Report { violationsBefore = audit.violations.Length };
            for (int pass = 0; !audit.passed && pass < nodes.Length; pass++)
            {
                var targets = new HashSet<int>(audit.violations.SelectMany(v => new[] { v.first, v.second }));
                int best = -1; float bestLane = 0, bestChange = float.PositiveInfinity, bestLow = 0, bestHigh = 0;
                for (int i = 1; i + 1 < nodes.Length; i++)
                {
                    MapSegment node = nodes[i]; MapElement element = map.elements[node.sourceIndex];
                    if (!targets.Contains(node.sourceIndex) || !Round(node) || element.id != 0 && element.id != 1 ||
                        element.updateType != 0 || opening.Contains(node.sourceIndex)) continue;
                    float incoming = ChartRouteComposer.MaxLateralShift(node.audioTime - nodes[i - 1].endAudioTime);
                    float outgoing = ChartRouteComposer.MaxLateralShift(nodes[i + 1].audioTime - node.endAudioTime);
                    float previous = ExitX(map, nodes[i - 1]);
                    float next = ChartRouteComposer.LandingX(map.elements[nodes[i + 1].sourceIndex]);
                    float limit = Math.Min(IslandChartAuthoring.LaneLimit, NativeRhythmRules.HorizontalLimit);
                    float low = Math.Max(-limit, Math.Max(previous - incoming, next - outgoing));
                    float high = Math.Min(limit, Math.Min(previous + incoming, next + outgoing));
                    if (low > high || !FindLane(map, nodes, node, route, settings, tileThickness, tileElevation, low, high, out float lane)) continue;
                    float delta = Math.Abs(lane - element.pos.x);
                    if (delta >= bestChange) continue;
                    best = i; bestLane = lane; bestChange = delta; bestLow = low; bestHigh = high;
                }
                if (best < 0)
                    throw new InvalidDataException("No reachable static-round lane can clear the full-size support meshes: " + JsonUtility.ToJson(audit));
                MapSegment selected = nodes[best]; MapElement target = map.elements[selected.sourceIndex];
                float original = target.pos.x;
                var at = target.pos; at.x = bestLane; target.pos = at; selected.x = bestLane;
                changes.Add(new Change { sourceIndex = selected.sourceIndex, originalLane = original, repairedLane = bestLane,
                    reachableMinimum = bestLow, reachableMaximum = bestHigh });
                report.maximumLateralChange = Math.Max(report.maximumLateralChange, bestChange);
                report.repairedRounds++;
                var nextAudit = IslandSpanSupportSpacing.Audit(map, route, settings, tileThickness, tileElevation);
                if (nextAudit.violations.Length >= audit.violations.Length)
                    throw new InvalidDataException("Full-support lane repair did not reduce the independently audited mesh violations.");
                audit = nextAudit;
            }
            if (!audit.passed) throw new InvalidDataException("Full-support lane repair did not converge: " + JsonUtility.ToJson(audit));
            for (int i = 0; i < map.elements.Length; i++)
            {
                MapElement element = map.elements[i];
                if (element.id != 0 && element.id != 1 && element.id != 4) continue;
                if (!IslandDynamicSupportSpacing.CanKeepRadius(route, settings, map.elements, i, IslandSupportSpacing.NativeJumpRadius))
                    throw new InvalidDataException("Full-support lane repair would invalidate a full-size native round: " + i);
            }
            report.violationsAfter = audit.violations.Length;
            report.fullSizeClearance = true; report.changes = changes.ToArray();
            return report;
        }

        private static bool FindLane(MapTimeline map, MapSegment[] nodes, MapSegment node, IslandRoute route,
            IslandDynamicSupportSpacing.Settings settings, float thickness, float elevation, float low, float high, out float lane)
        {
            MapElement target = map.elements[node.sourceIndex]; Vector3 original = target.pos;
            var probe = new MapTimeline { elements = map.elements,
                segments = nodes.Where(s => s == node || map.elements[s.sourceIndex].id == 2 || map.elements[s.sourceIndex].id == 3).ToArray() };
            bool Safe(float candidate)
            {
                var at = original; at.x = candidate; target.pos = at;
                return IslandDynamicSupportSpacing.CanKeepRadius(route, settings, map.elements, node.sourceIndex, IslandSupportSpacing.NativeJumpRadius) &&
                    IslandSpanSupportSpacing.Audit(probe, route, settings, thickness, elevation).passed;
            }
            lane = original.x;
            var candidates = new List<float> { low, high };
            int steps = (int)Math.Ceiling(Math.Max(Math.Abs(low - original.x), Math.Abs(high - original.x)) / SearchStep);
            for (int i = 1; i <= steps; i++)
            {
                float left = original.x - i * SearchStep, right = original.x + i * SearchStep;
                if (left >= low && left <= high) candidates.Add(left);
                if (right >= low && right <= high) candidates.Add(right);
            }
            try
            {
                foreach (float candidate in candidates.OrderBy(x => Math.Abs(x - original.x)).ThenBy(x => x))
                {
                    if (!Safe(candidate)) continue;
                    float safe = candidate, unsafeLane = original.x;
                    for (int iteration = 0; iteration < 18; iteration++)
                    {
                        float middle = (safe + unsafeLane) * .5f;
                        if (Safe(middle)) safe = middle; else unsafeLane = middle;
                    }
                    lane = safe; return true;
                }
                return false;
            }
            finally { target.pos = original; }
        }

        private static bool Round(MapSegment segment) => segment.kind == "Jump" || segment.kind == "BigJump";
        private static float ExitX(MapTimeline map, MapSegment segment) => ChartRouteComposer.LandingX(map.elements[
            segment.kind == "Hold" ? segment.sourceIndex : segment.elementIndices[segment.elementIndices.Length - 1]]);
    }
}
