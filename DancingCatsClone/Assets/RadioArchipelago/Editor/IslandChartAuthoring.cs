using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Spatial challenge for the island pilot. Musical evidence and timings are immutable.</summary>
    public static class IslandChartAuthoring
    {
        public const float LateralAmplitude = 1.35f;
        public const float LaneLimit = 1.30f;
        public const float WorldDistanceScale = 1.18f;
        private sealed class Support
        {
            public MapSegment segment;
            public MapElement first, last;
            public bool fixedLane, opening;
            public float desired, lower, upper;
            public float Exit => fixedLane ? ChartRouteComposer.LandingX(last) : desired;
        }

        public static MapTimeline Create(string sourceJson) => Create(sourceJson, false);

        /// <summary>Opt in for three centered landings and a short stationary transition into normal phrasing.</summary>
        public static MapTimeline Create(string sourceJson, bool approachableOpening)
        {
            var map = JsonUtility.FromJson<MapTimeline>(sourceJson);
            ChartRouteComposer.Apply(map);
            var supports = map.segments.Where(s => s.kind != "Theme").OrderBy(s => s.audioTime).Select(s =>
            {
                var first = map.elements[s.sourceIndex];
                var last = map.elements[s.elementIndices.Last()];
                bool fixedLane = s.kind == "Follow" || s.kind == "Start" || s.kind == "FinishJump";
                float entry = ChartRouteComposer.LandingX(first);
                // Sine-moving supports can only reach [-1,1]; their stored X is
                // phase, unlike the direct lane coordinate of stationary tiles.
                float limit = first.updateType == 3 ? .95f : LaneLimit;
                return new Support { segment = s, first = first, last = last, fixedLane = fixedLane,
                    desired = fixedLane ? entry : Mathf.Clamp(entry * LateralAmplitude, -limit, limit),
                    lower = fixedLane ? entry : -limit, upper = fixedLane ? entry : limit };
            }).ToArray();

            if (approachableOpening)
            {
                int scoring = 0;
                foreach (var support in supports)
                {
                    if (support.segment.kind == "Start" || support.segment.kind == "FinishJump") continue;
                    float limit = scoring < 3 ? 0 : scoring == 3 ? .25f : .5f;
                    if (scoring++ >= 5) break;
                    // A native Follow remains authoritative. Reject an incompatible
                    // opt-in instead of bending its authored path toward the center.
                    if (support.fixedLane)
                    {
                        if (Mathf.Abs(support.desired) > limit + .0001f)
                            throw new InvalidDataException("A fixed path is incompatible with the centered island opening.");
                        continue;
                    }
                    support.opening = true;
                    support.lower = -limit; support.upper = limit;
                    support.desired = Mathf.Clamp(support.desired, -limit, limit);
                    // Stored moving X is phase; desired is already a contact lane.
                    // The opening must stay still both in elements and segment data.
                    support.first.updateType = 0; support.first.msLeft = 0;
                    support.first.mstartPos = Vector3.zero;
                    if (support.segment.motion == null) support.segment.motion = new MotionParameters();
                    support.segment.motion.updateType = 0; support.segment.motion.msLeft = 0;
                    support.segment.motion.mstartPos = Vector3.zero;
                }
            }

            for (int i = supports.Length - 2; i >= 0; --i)
            {
                var a = supports[i]; var b = supports[i + 1];
                float shift = ChartRouteComposer.MaxLateralShift(b.segment.audioTime - a.segment.endAudioTime);
                if (a.fixedLane) continue;
                a.lower = Mathf.Max(a.lower, b.lower - shift);
                a.upper = Mathf.Min(a.upper, b.upper + shift);
                if (a.lower > a.upper + .0001f) throw new InvalidDataException("No reachable island lane.");
            }
            for (int i = 0; i < supports.Length; ++i)
            {
                var s = supports[i];
                if (!s.fixedLane)
                {
                    float lo = s.lower, hi = s.upper;
                    if (i > 0)
                    {
                        float shift = ChartRouteComposer.MaxLateralShift(s.segment.audioTime - supports[i - 1].segment.endAudioTime);
                        lo = Mathf.Max(lo, supports[i - 1].Exit - shift);
                        hi = Mathf.Min(hi, supports[i - 1].Exit + shift);
                    }
                    if (lo > hi + .0001f) throw new InvalidDataException("Unreachable island support.");
                    s.desired = Mathf.Clamp(s.desired, lo, hi);
                    if (!s.opening && i > 0 && IsJump(s.first) && IsJump(supports[i - 1].first))
                    {
                        // Give rapid subdivisions a reachable side-to-side
                        // reply instead of stacking repeated notes in one lane.
                        float previous = supports[i - 1].Exit;
                        float gap = (s.segment.z - supports[i - 1].segment.endZ) * WorldDistanceScale;
                        float target = 1.38f;
                        if (gap > 0 && gap < target)
                        {
                            float dx = Mathf.Sqrt(Mathf.Max(0, target * target - gap * gap));
                            float sign = Mathf.Abs(s.desired - previous) > .05f ? Mathf.Sign(s.desired - previous) : (i % 2 == 0 ? 1 : -1);
                            float first = Mathf.Clamp(previous + sign * dx, lo, hi);
                            float second = Mathf.Clamp(previous - sign * dx, lo, hi);
                            if (Mathf.Abs(second - previous) > Mathf.Abs(first - previous)) first = second;
                            if (Mathf.Abs(first - previous) > Mathf.Abs(s.desired - previous)) s.desired = first;
                        }
                    }
                    var pos = s.first.pos;
                    pos.x = s.first.updateType == 3 ? Mathf.Asin(Mathf.Clamp(s.desired, -.99f, .99f)) * 5 / (2 * Mathf.PI) : s.desired;
                    s.first.pos = pos; s.segment.x = pos.x;
                }
            }
            map.normalizationNotes = (map.normalizationNotes ?? Array.Empty<string>()).Concat(new[] {
                "Island pilot: phrase-route-v1, 35% wider reachable lateral phrasing with rapid alternating replies; music, timings, holds, glides and flights preserved."
            }).ToArray();
            if (approachableOpening)
                map.normalizationNotes = map.normalizationNotes.Concat(new[] {
                    "Island opening: first three scoring supports centered and stationary; next two stationary supports ease into normal reachable lateral phrasing."
                }).ToArray();
            Verify(JsonUtility.FromJson<MapTimeline>(sourceJson), map);
            return map;
        }

        public static void Verify(MapTimeline original, MapTimeline pilot)
        {
            Require(original.elements.Length == pilot.elements.Length && original.segments.Length == pilot.segments.Length, "Chart structure changed");
            Require(original.forwardSpeed == pilot.forwardSpeed && original.delayTime == pilot.delayTime, "Musical clock changed");
            for (int i = 0; i < original.elements.Length; i++)
            {
                var a = original.elements[i]; var b = pilot.elements[i];
                Require(a.kind == b.kind && a.id == b.id && a.sourceIndex == b.sourceIndex && a.pos.z == b.pos.z &&
                    a.pos.y == b.pos.y && a.jumpDistance == b.jumpDistance && a.gameTime == b.gameTime && a.audioTime == b.audioTime &&
                    a.mfendPos == b.mfendPos && EqualArray(a.holdTicks, b.holdTicks), "Musical element changed at " + i);
                if (a.kind == "Follow" || a.kind == "FollowMid" || a.kind == "FollowEnd")
                    Require(JsonUtility.ToJson(a) == JsonUtility.ToJson(b), "Authored glide changed at " + i);
            }
            for (int i = 0; i < original.segments.Length; i++)
            {
                var a = original.segments[i]; var b = pilot.segments[i];
                Require(a.z == b.z && a.endZ == b.endZ && a.audioTime == b.audioTime && a.endAudioTime == b.endAudioTime &&
                    a.gameTime == b.gameTime && a.endGameTime == b.endGameTime, "Support timing changed at " + i);
                Require(EqualArray(a.path, b.path), "Glide path changed at " + i);
            }
            Require(EqualArray(original.events, pilot.events), "Rhythm events changed");
            var nodes = pilot.segments.Where(s => s.kind != "Theme").OrderBy(s => s.audioTime).ToArray();
            for (int i = 1; i < nodes.Length; i++)
            {
                var before = nodes[i - 1]; var after = nodes[i];
                float exit = ChartRouteComposer.LandingX(pilot.elements[before.elementIndices.Last()]);
                // A Hold's exit is created at its first element's lane by BuildTrack.
                if (before.kind == "Hold") exit = ChartRouteComposer.LandingX(pilot.elements[before.sourceIndex]);
                float entry = ChartRouteComposer.LandingX(pilot.elements[after.sourceIndex]);
                float allowed = ChartRouteComposer.MaxLateralShift(after.audioTime - before.endAudioTime);
                Require(Mathf.Abs(exit - entry) <= allowed + .001f, "Island lane transition is too wide at " + i);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); }

        private static bool IsJump(MapElement element) => element.id == 0 || element.id == 1;

        private static bool EqualArray<T>(T[] a, T[] b)
        {
            if (a == null || b == null) return a == null && b == null;
            return a.Length == b.Length && a.Select(item => JsonUtility.ToJson(item))
                .SequenceEqual(b.Select(item => JsonUtility.ToJson(item)));
        }
    }
}
