using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    public static class IslandAbyssChartAuthoringChecks
    {
        [Serializable] public sealed class Result { public string name, evidence; public bool pass; }

        [MenuItem("Radio Archipelago/Floating archipelago/Check hold grades and moving rounds")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run Abyss chart checks in Edit Mode.");
            Result[] results = CheckMath();
            foreach (Result result in results)
                if (!result.pass) throw new InvalidOperationException(result.name + ": " + result.evidence);
            Debug.Log("ISLAND_ABYSS_CHART_MATH_PASS checks=" + results.Length);
        }

        public static Result[] CheckMath()
        {
            var results = new List<Result>();
            Check(results, "hold_is_a_single_vertical_chord_and_landmarks_survive", () =>
            {
                var map = Spans(new Vector2(6, 17));
                var report = new IslandAbyssChartAuthoring.Report();
                var document = IslandAbyssChartAuthoring.FlattenSpans(map, Hills(), 0, 1, report);
                var profile = new IslandElevationProfile(document.length, document.closed, document.keys);
                var source = Hills();
                var old = new IslandElevationProfile(source.length, source.closed, source.keys);
                float a = profile.Evaluate(6), b = profile.Evaluate(17);
                for (int i = 0; i <= 220; i++)
                {
                    double s = 6 + i * .05;
                    Near(profile.Evaluate(s), Mathf.Lerp(a, b, (float)((s - 6) / 11)), .00005f, "hold chord");
                }
                Near(profile.Evaluate(40), 8, .00001f, "hill landmark outside feather");
                Near(profile.Evaluate(60), 12, .00001f, "sky landmark outside feather");
                Require(report.maxHoldChordError < .0001f && report.maxHoldGradeVariation < .002f,
                    "runtime profile must stay straight throughout the hold");
                for (double s = 0; s < source.length; s += .025)
                    Require(profile.Evaluate(s) >= old.Evaluate(s) - .001f, "held chord or outer feather entered the original landscape");
                Require(report.maxLift > 0 && report.minimumProfileDelta >= -.001f, "crest clearance must lift a chord instead of cutting the hill");
                return "221 chord samples above the original hill; 8m/12m landmarks survive outside joins; all-loop clearance passes.";
            });
            Check(results, "overlapping_laps_merge_before_a_seam_crossing_fit", () =>
            {
                var map = Spans(new Vector2(76, 86), new Vector2(82, 89), new Vector2(158, 167));
                var report = new IslandAbyssChartAuthoring.Report();
                var document = IslandAbyssChartAuthoring.FlattenSpans(map, Hills(), 0, 1, report);
                var profile = new IslandElevationProfile(document.length, document.closed, document.keys);
                Require(report.mergedHoldIntervals == 1, "later laps and the periodic seam must form one union");
                Require(!report.fullLoopFlattened, "partial coverage must preserve unheld terrain");
                Near(profile.Evaluate(0), profile.Evaluate(80), 0, "periodic height");
                Near(profile.Grade(-.00001), profile.Grade(.00001), .00001f, "periodic grade");
                Require(report.maxHoldChordError < .0001f && report.maxHoldGradeVariation < .002f,
                    "overlap cannot introduce a second grade patch inside a hold");
                return "Three lap aliases combine into one continuous 13m seam-crossing chord.";
            });
            Check(results, "full_loop_coverage_is_explicitly_reported", () =>
            {
                var report = new IslandAbyssChartAuthoring.Report();
                var document = IslandAbyssChartAuthoring.FlattenSpans(Spans(new Vector2(0, 170)), Hills(), 0, 1, report);
                var profile = new IslandElevationProfile(document.length, document.closed, document.keys);
                Require(report.fullLoopFlattened && report.holdCoverage >= .9999f, "whole-loop flattening must be reported");
                Near(profile.Grade(11), 0, 0, "a closed whole-loop chord must be level");
                return "A hold covering more than one lap produces a level periodic profile and an explicit full-loop flag.";
            });
            Check(results, "nearly_touching_full_cap_coverage_cannot_squeeze_a_steep_c2_feather", () =>
            {
                var floor = new IslandElevationProfile.Document { length = 80, closed = true,
                    keys = new[] { K(0, 0), K(5, .5f), K(6, .6f), K(7, .72f), K(8, .8f), K(9, .9f), K(10, 1), K(15, 1.5f), K(20, 2), K(30, 3),
                        K(40, 2), K(50, 1), K(60, 0), K(70, -1), K(80, 0) } };
                var report = new IslandAbyssChartAuthoring.Report();
                var result = IslandAbyssChartAuthoring.FlattenSpans(Spans(new Vector2(6, 8), new Vector2(9.4f, 11.4f)),
                    floor, 0, 1, report, .67f);
                Require(report.maxProfileGrade <= .75f,
                    "a sub-key-spacing cap seam produced a steep feather on a gentle source floor: " + report.maxProfileGrade);
                Require(!report.fullLoopFlattened, "a local cap seam repair must preserve unheld route relief");
                Require(report.minimumProfileDelta >= -.001f && report.maxHoldChordError < .0002f && report.maxHoldGradeVariation < .002f,
                    "closing the tiny cap seam must preserve the source floor and constant Hold grades");
                Near(new IslandElevationProfile(result.length, true, result.keys).Evaluate(40), 2, .00001f,
                    "a remote unheld landmark must survive local cap-coverage repair");
                return "Two cap intervals with a .06m free seam share one linear patch; the .75 grade limit, floor clearance and remote landmark survive.";
            });
            Check(results, "moving_rounds_are_reachable_varied_and_acoustic_data_is_immutable", () =>
            {
                MapTimeline before = Rounds(), map = Rounds();
                var report = new IslandAbyssChartAuthoring.Report();
                IslandAbyssChartAuthoring.PromoteMovingRounds(map, Circle(), 0, 1, report);
                Require(report.movingShare >= .35f && report.movingShare <= .45f, "moving-round share must reach 35–45%");
                Require(report.reachable && report.noOverlaps && report.minimumRadius >= .33f, "reachable, separated moving supports");
                bool left = false, right = false;
                for (int i = 0; i < map.elements.Length; i++)
                {
                    MapElement a = before.elements[i], b = map.elements[i];
                    Require(a.id == b.id && a.kind == b.kind && a.pos.y == b.pos.y && a.pos.z == b.pos.z &&
                        a.audioTime == b.audioTime && a.gameTime == b.gameTime && a.jumpDistance == b.jumpDistance,
                        "native acoustic or jump data changed");
                    if (b.updateType == 3) { left |= b.msLeft != 0; right |= b.msLeft == 0; }
                    MapSegment segment = map.segments[i];
                    Require(segment.x == b.pos.x && segment.motion.updateType == b.updateType && segment.motion.msLeft == b.msLeft,
                        "motion metadata must agree with the native element");
                }
                Require(left && right, "both native moving directions are required");
                Require(map.elements[0].updateType == 0 && map.elements[map.elements.Length - 1].updateType == 0,
                    "start/finish must remain stable");
                return report.movingRounds + "/" + report.eligibleRounds + " eligible rounds move; both directions, zero acoustic changes.";
            });
            Check(results, "held_endpoints_and_follow_neighbors_stay_fixed", () =>
            {
                MapTimeline map = Rounds();
                map.elements[25].kind = map.segments[25].kind = "Hold"; map.elements[25].id = 2;
                map.elements[40].kind = map.segments[40].kind = "Follow"; map.elements[40].id = 4;
                var report = new IslandAbyssChartAuthoring.Report();
                IslandAbyssChartAuthoring.PromoteMovingRounds(map, Circle(), 0, 1, report);
                foreach (int index in new[] { 24, 25, 26, 39, 40, 41 })
                    Require(map.elements[index].updateType == 0, "held/follow entry or adjacent exit support was converted into a mover");
                var second = new IslandAbyssChartAuthoring.Report();
                IslandAbyssChartAuthoring.PromoteMovingRounds(map, Circle(), 0, 1, second);
                Require(second.addedMovingRounds == 0 && second.movingRounds == report.movingRounds,
                    "rerunning challenge authoring must not keep adding movers");
                return "Hold/Follow and their adjacent round supports remain fixed; repeated promotion adds no further movers.";
            });
            Check(results, "uniform_rounds_keep_native_size_when_safe_mover_density_is_low", () =>
            {
                MapTimeline map = Rounds();
                float z = 0;
                for (int i = 0; i < map.elements.Length; i++)
                {
                    if (i > 0) z += i < 6 || i == map.elements.Length - 1 ? 3 : 1.2f;
                    MapElement e = map.elements[i]; MapSegment s = map.segments[i];
                    float x = i == 0 || i == map.elements.Length - 1 ? 0 : i % 2 == 0 ? -.45f : .45f;
                    e.pos = new Vector3(x, 0, z); e.audioTime = z / 6.5; e.gameTime = e.audioTime + .6;
                    s.x = x; s.z = s.endZ = z; s.audioTime = s.endAudioTime = e.audioTime; s.gameTime = s.endGameTime = e.gameTime;
                }
                // This inherited sine crosses a close stationary neighbour.
                var inherited = map.elements[30];
                inherited.pos.x = Mathf.Asin(inherited.pos.x) * 5 / (2 * Mathf.PI);
                inherited.updateType = map.segments[30].motion.updateType = 3;
                var report = new IslandAbyssChartAuthoring.Report();
                IslandAbyssChartAuthoring.PromoteMovingRounds(map, Circle(), 0, 1, report, 1, true);
                Require(report.uniformRoundTiles && report.noOverlaps && report.minimumRadius == IslandSupportSpacing.NativeJumpRadius,
                    "uniform authoring must preserve every complete native round body");
                Require(report.movingShare < .35f, "full-size clearance must take priority over the legacy minimum mover density");
                for (int i = 1; i <= 5; i++) Require(map.elements[i].updateType == 0, "five opening scoring supports must remain stationary");
                return "Unsafe inherited motion is rejected; full .65m supports remain valid even below 35% movers.";
            });
            Check(results, "uniform_rounds_fail_authoring_instead_of_shrinking_unplayable_subdivisions", () =>
            {
                MapTimeline map = Rounds();
                map.elements[31].pos.z = map.elements[30].pos.z + .5f;
                map.elements[31].audioTime = map.elements[31].pos.z / 6.5;
                map.segments[31].z = map.segments[31].endZ = map.elements[31].pos.z;
                map.segments[31].audioTime = map.segments[31].endAudioTime = map.elements[31].audioTime;
                try
                {
                    IslandAbyssChartAuthoring.PromoteMovingRounds(map, Circle(), 0, 1,
                        new IslandAbyssChartAuthoring.Report(), 1, true);
                }
                catch (InvalidDataException error)
                {
                    Require(error.Message.Contains("minimumRadius="), "the rejected chart must identify the failed spacing contract");
                    return "An overlapping stationary subdivision is rejected at generation; no tiny visual support can hide it.";
                }
                throw new InvalidOperationException("Uniform authoring silently accepted an undersized stationary pair.");
            });
            Check(results, "uniform_rounds_repair_feasible_stationary_spacing_inside_existing_lane_budgets", () =>
            {
                MapTimeline map = Rounds();
                map.elements[31].pos.z = map.elements[30].pos.z + 1.2f;
                map.elements[31].audioTime = map.elements[31].pos.z / 6.5;
                map.segments[31].z = map.segments[31].endZ = map.elements[31].pos.z;
                map.segments[31].audioTime = map.segments[31].endAudioTime = map.elements[31].audioTime;
                // Snapshot native values directly: Unity JSON can round a
                // double by one ULP, which would falsify this immutability test.
                var before = new MapElement[map.elements.Length];
                for (int i = 0; i < before.Length; i++)
                {
                    MapElement e = map.elements[i];
                    before[i] = new MapElement { pos = e.pos, audioTime = e.audioTime, gameTime = e.gameTime,
                        id = e.id, kind = e.kind, jumpDistance = e.jumpDistance };
                }
                var report = new IslandAbyssChartAuthoring.Report();
                IslandAbyssChartAuthoring.PromoteMovingRounds(map, Circle(), 0, 1, report, 1, true);
                Require(report.noOverlaps && report.reachable && report.minimumRadius == IslandSupportSpacing.NativeJumpRadius,
                    "a feasible stationary spacing repair must preserve normal native bodies");
                for (int i = 0; i < map.elements.Length; i++)
                {
                    MapElement a = before[i], b = map.elements[i];
                    Require(a.pos.y == b.pos.y && a.pos.z == b.pos.z && a.audioTime == b.audioTime && a.gameTime == b.gameTime &&
                        a.id == b.id && a.kind == b.kind && a.jumpDistance == b.jumpDistance,
                        "spatial repair changed exact native data at " + i + ": y=" + a.pos.y.ToString("R") + "/" + b.pos.y.ToString("R") +
                        ", z=" + a.pos.z.ToString("R") + "/" + b.pos.z.ToString("R") + ", audio=" + a.audioTime.ToString("R") + "/" + b.audioTime.ToString("R") +
                        ", game=" + a.gameTime.ToString("R") + "/" + b.gameTime.ToString("R") + ", jump=" + a.jumpDistance.ToString("R") + "/" + b.jumpDistance.ToString("R"));
                    if (i >= 1 && i <= 5) Require(a.pos.x == b.pos.x && b.updateType == 0, "the five opening lanes must be locked");
                    Require(Math.Abs(ChartRouteComposer.LandingX(b)) <= IslandChartAuthoring.LaneLimit,
                        "repair escaped the existing island lane bounds");
                }
                Require(Math.Abs(map.elements[30].pos.x - before[30].pos.x) > .1f ||
                    Math.Abs(map.elements[31].pos.x - before[31].pos.x) > .1f, "the overlapping stationary pair was never repaired");
                return "A stationary pair receives the nearest safe lateral correction; timing, bodies, five opening lanes and all transition budgets survive.";
            });
            Check(results, "full_hold_caps_are_validated_independently_of_round_radius_fitting", () =>
            {
                var route = Circle(); var settings = new IslandDynamicSupportSpacing.Settings(0, 1, 35, 4, 6.5f);
                MapTimeline close = CapsulePair(13);
                var fitted = new float[2];
                IslandDynamicSupportSpacing.Resolve(route, settings, close.elements, new[] { .65f, .65f }, fitted);
                Require(fitted[0] == .65f && fitted[1] == .65f, "fixture must expose the round-only gate's excluded Hold bodies");
                var overlap = IslandSpanSupportSpacing.Audit(close, route, settings);
                Require(!overlap.passed && overlap.violations.Length == 1 && overlap.violations[0].planarGap == 0,
                    "the actual curved .70m ceramic caps must reject their intersection");
                Require(IslandSpanSupportSpacing.Audit(CapsulePair(14), route, settings).passed,
                    "full-size capsules separated by a real gap must remain valid");
                MapTimeline laps = CapsulePair(10 + route.Length);
                Require(IslandSpanSupportSpacing.Audit(laps, route, settings).passed,
                    "different unwrapped musical laps must not reject identical geometric Hold positions");
                return "Actual all-layer Hold cap projections catch an intersection ignored by round fitting; separated caps and later laps pass.";
            });
            Check(results, "full_hold_body_catches_containment_and_native_motion_across_its_lifetime", () =>
            {
                var route = Circle(); var settings = new IslandDynamicSupportSpacing.Settings(0, 1, 35, 4, 6.5f);
                MapTimeline contained = CapsulePair(11);
                contained.elements[1].id = 0; contained.elements[1].kind = contained.segments[1].kind = "Jump";
                Require(!IslandSpanSupportSpacing.Audit(contained, route, settings).passed,
                    "a round fully inside a long body's projected interior must be rejected without relying on edge crossings");
                MapTimeline moving = CapsulePair(13.1f);
                moving.elements[1].id = 0; moving.elements[1].kind = moving.segments[1].kind = "Jump";
                moving.elements[1].updateType = 3; moving.elements[1].pos.x = Mathf.Asin(.9f) * 5 / (2 * Mathf.PI);
                Require(!IslandSpanSupportSpacing.Audit(moving, route, settings).passed,
                    "a native sine crossing a Hold cap before contact must be included in the lifetime gate");
                moving.elements[1].id = 4; moving.elements[1].kind = moving.segments[1].kind = "Follow";
                moving.elements[1].updateType = 2; moving.elements[1].mfendPos = 2;
                Require(!IslandSpanSupportSpacing.Audit(moving, route, settings).passed,
                    "an actor-driven Follow must reserve its full swept lane beside a Hold");
                return "Containment, approach-time native sine motion and actor-driven Follow lanes cannot hide an intersecting full Hold body.";
            });
            return results.ToArray();
        }

        private static MapTimeline CapsulePair(float secondStart)
        {
            return new MapTimeline
            {
                forwardSpeed = 6.5f,
                elements = new[] { new MapElement { id = 2, kind = "Hold", sourceIndex = 0, pos = new Vector3(0, 0, 10) },
                    new MapElement { id = 2, kind = "Hold", sourceIndex = 1, pos = new Vector3(0, 0, secondStart) } },
                segments = new[] { new MapSegment { id = 2, kind = "Hold", sourceIndex = 0, z = 10, endZ = 12 },
                    new MapSegment { id = 2, kind = "Hold", sourceIndex = 1, z = secondStart, endZ = secondStart + 2 } }
            };
        }

        private static MapTimeline Spans(params Vector2[] spans)
        {
            var segments = new MapSegment[spans.Length];
            for (int i = 0; i < spans.Length; i++)
                segments[i] = new MapSegment { kind = i % 2 == 0 ? "Hold" : "Follow", z = spans[i].x, endZ = spans[i].y };
            return new MapTimeline { segments = segments };
        }

        private static IslandElevationProfile.Document Hills() => new IslandElevationProfile.Document
        {
            length = 80, closed = true,
            keys = new[] { K(0, 0), K(10, 4), K(20, 0), K(40, 8), K(50, 0), K(60, 12), K(70, 12), K(80, 0) }
        };
        private static IslandElevationProfile.Key K(float s, float h) => new IslandElevationProfile.Key(s, h);

        private static IslandRoute Circle()
        {
            var points = new Vector3[128];
            for (int i = 0; i < points.Length; i++)
            {
                double a = i * Math.PI * 2 / points.Length;
                points[i] = new Vector3((float)(90 * Math.Cos(a)), 0, (float)(90 * Math.Sin(a)));
            }
            return new IslandRoute(points);
        }

        private static MapTimeline Rounds()
        {
            var elements = new MapElement[82]; var segments = new MapSegment[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                string kind = i == 0 ? "Start" : i == elements.Length - 1 ? "FinishJump" : "Jump";
                float z = i * 3, x = (i % 5 - 2) * .15f;
                var e = elements[i] = new MapElement { kind = kind, id = kind == "FinishJump" ? 1 : 0,
                    sourceIndex = i, pos = new Vector3(x, 0, z), jumpDistance = 3, audioTime = z / 6.5, gameTime = z / 6.5 + .6 };
                segments[i] = new MapSegment { kind = kind, sourceIndex = i, id = e.id, x = x, z = z, endZ = z,
                    audioTime = e.audioTime, endAudioTime = e.audioTime, gameTime = e.gameTime, endGameTime = e.gameTime,
                    elementIndices = new[] { i }, motion = new MotionParameters { jumpDistance = 3 } };
            }
            return new MapTimeline { elements = elements, segments = segments, forwardSpeed = 6.5f, delayTime = .6f };
        }

        private static void Near(float a, float b, float tolerance, string message) => Require(Math.Abs(a - b) <= tolerance, message + ": " + a + " vs " + b);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Check(List<Result> results, string name, Func<string> check)
        {
            try { results.Add(new Result { name = name, pass = true, evidence = check() }); }
            catch (Exception e) { results.Add(new Result { name = name, pass = false, evidence = e.ToString() }); }
        }
    }
}
