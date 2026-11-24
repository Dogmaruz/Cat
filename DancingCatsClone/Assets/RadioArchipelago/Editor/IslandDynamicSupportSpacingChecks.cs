using System;
using System.Collections.Generic;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    public static class IslandDynamicSupportSpacingChecks
    {
        [Serializable] public sealed class Result { public string name, evidence; public bool pass; }

        [MenuItem("Radio Archipelago/Floating archipelago/Check dynamic round spacing")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run dynamic spacing checks in Edit Mode.");
            Result[] checks = CheckMath();
            foreach (Result check in checks) if (!check.pass) throw new InvalidOperationException(check.name + ": " + check.evidence);
            Debug.Log("ISLAND_DYNAMIC_SPACING_PASS checks=" + checks.Length);
        }

        public static Result[] CheckMath()
        {
            var checks = new List<Result>();
            Check(checks, "opposing_cycles_fit_for_approach_instead_of_contact_only", () =>
            {
                var route = Route(); var settings = Settings();
                var elements = new[] { E(10, -1.25f, 3), E(11, 1.25f, 3) };
                var radii = Fit(route, settings, elements);
                Require(radii[0] < .5f && radii[1] < .5f, "opposing movers need smaller radii despite separated contact centers");
                float minimumGap = float.MaxValue;
                for (double z = -24; z <= 14.38; z += .001)
                {
                    Vector3 a = Center(route, elements[0], (float)z), b = Center(route, elements[1], (float)z);
                    minimumGap = Math.Min(minimumGap, Vector3.Distance(a, b) - radii[0] - radii[1]);
                }
                Require(minimumGap >= IslandSupportSpacing.MinimumGap + .018f, "between-sample motion reserve must retain the .08m gap");
                var contact = new[] { Center(route, elements[0], 10), Center(route, elements[1], 11) };
                var legacy = new float[2];
                IslandSupportSpacing.ResolveRadii(contact, new[] { 10f, 11f }, 39, new[] { .65f, .65f }, legacy);
                Require(legacy[0] == .65f && legacy[1] == .65f, "fixture must expose a gap missed by the original contact-only policy");
                return "38,381 native-cycle samples retain the gap; legacy contact fitting leaves the crossing discs at .65m.";
            });
            Check(checks, "native_linear_motion_and_spring_height_are_covered", () =>
            {
                var route = Route(); var moving = E(10, 1, 1); moving.begin = 30; moving.end = 0;
                moving.mstartPos = new Vector3(-1, .2f, 10);
                var other = E(10.8f, -1, 0);
                float[] radii = Fit(route, Settings(), new[] { moving, other });
                for (double z = -20; z < 14; z += .002)
                {
                    Vector3 a = Center(route, moving, (float)z), b = Center(route, other, (float)z);
                    float dy = Math.Max(0, Math.Abs(a.y - b.y) - IslandDynamicSupportSpacing.SpringDifference);
                    a.y = b.y = 0;
                    double d = Math.Sqrt((a - b).sqrMagnitude + dy * dy);
                    Require(d >= radii[0] + radii[1] + IslandSupportSpacing.MinimumGap, "linear motion or spring compression caused an overlap");
                }
                Require(radii[0] < .4f, "linear approach must be considered instead of its far contact lane");
                return "17,000 linear-motion samples include native MovePercent and the full .21m relative spring range.";
            });
            Check(checks, "hit_tail_coexistence_and_later_lap_aliases", () =>
            {
                var route = Route();
                var shortWindow = new IslandDynamicSupportSpacing.Settings(0, 1, .2f, 0, 6.5f);
                float[] tail = Fit(route, shortWindow, new[] { E(10, 0, 0), E(10.9f, 0, 0) });
                Require(tail[0] < .5f, "the .52-second hit tail coexists beyond the ordinary visible-behind window");
                float[] laps = Fit(route, Settings(), new[] { E(10, 0, 3), E(10 + route.Length, 0, 3) });
                Require(laps[0] == .65f && laps[1] == .65f, "unwrapped musical laps must not shrink one another");
                return "Hit-tail visibility is included; identical geometric positions on later musical laps remain independent.";
            });
            Check(checks, "follow_disc_uses_its_live_forward_extent_and_lane_envelope", () =>
            {
                var route = Route(); var follow = E(10, 0, 2); follow.id = 4; follow.mfendPos = 5;
                float[] crossing = Fit(route, Settings(), new[] { follow, E(14, 1, 0) });
                Require(crossing[0] <= .001f && crossing[1] <= .001f, "a disc inside a ridden Follow's swept lane cannot retain an overlapping radius");
                float[] separated = Fit(route, Settings(), new[] { follow, E(17, 1, 0) });
                Require(separated[0] == .65f && separated[1] == .65f, "a round safely beyond the Follow end should retain its radius");
                return "Actor-driven Follow discs cover their native Z motion and full lateral lane; separated exit approaches remain full size.";
            });
            Check(checks, "wide_lane_centers_keep_disc_size_and_cover_sine_motion", () =>
            {
                var route = Route();
                var settings = new IslandDynamicSupportSpacing.Settings(0, 1, 35, 4, 6.5f, 2.1f);
                var fixedPair = new[] { E(10, -.4f, 0), E(10, .4f, 0) };
                var narrow = Fit(route, Settings(), fixedPair);
                var wide = Fit(route, settings, fixedPair);
                Require(narrow[0] < .4f && wide[0] == .65f && wide[1] == .65f,
                    "Widening centers should free normal disc radii, not enlarge their geometry.");
                var moving = new[] { E(10, -1.25f, 3), E(11, 1.25f, 3) };
                var radii = Fit(route, settings, moving);
                for (double z = -24; z <= 14.38; z += .002)
                    Require(Vector3.Distance(Center(route, moving[0], (float)z, 2.1f), Center(route, moving[1], (float)z, 2.1f)) >=
                        radii[0] + radii[1] + IslandSupportSpacing.MinimumGap + .018f,
                        "Expanded sine motion escaped its fitted radius reserve.");
                return "World lanes widen by2.1 while .65m discs remain unchanged;19,191 moving-pair samples retain their gap.";
            });
            Check(checks, "full_size_authoring_gate_uses_the_runtime_motion_and_lifetime_contract", () =>
            {
                var route = Route();
                var crossing = new[] { E(10, -1.25f, 3), E(11, 1.25f, 3) };
                Require(!IslandDynamicSupportSpacing.CanKeepRadius(route, Settings(), crossing, 0, .65f),
                    "a crossing mover must be rejected before its native body would need shrinking");
                var separated = new[] { E(10, 0, 0), E(12, 0, 3) };
                Require(IslandDynamicSupportSpacing.CanKeepRadius(route, Settings(), separated, 1, .65f),
                    "a safely separated moving support must retain its normal body and motion");
                var laterLap = new[] { E(10, 0, 3), E(10 + route.Length, 0, 3) };
                Require(IslandDynamicSupportSpacing.CanKeepRadius(route, Settings(), laterLap, 0, .65f),
                    "separate musical laps must remain independent in authoring too");
                return "The full-size candidate gate rejects true dynamic crossings, keeps safe motion, and ignores non-coexisting laps.";
            });
            return checks.ToArray();
        }

        private static IslandRoute Route() => new IslandRoute(new[]
        {
            P(0, 0), P(0, 4), P(0, 8), P(0, 20), P(0, 24), P(0, 50), P(20, 50), P(20, 0)
        });
        private static Vector3 P(float x, float z) => new Vector3(x, 0, z);
        private static MapElement E(float z, float x, int type) => new MapElement { id = 0, pos = new Vector3(x, 0, z), updateType = type };
        private static IslandDynamicSupportSpacing.Settings Settings() => new IslandDynamicSupportSpacing.Settings(0, 1, 35, 4, 6.5f);
        private static float[] Fit(IslandRoute route, IslandDynamicSupportSpacing.Settings settings, MapElement[] elements)
        {
            var radii = new float[elements.Length]; var preferred = new float[elements.Length];
            for (int i = 0; i < preferred.Length; i++) preferred[i] = .65f;
            IslandDynamicSupportSpacing.Resolve(route, settings, elements, preferred, radii);
            return radii;
        }
        private static Vector3 Center(IslandRoute route, MapElement e, float playerZ, float lateralScale = 1)
        {
            float x = e.pos.x, y = e.pos.y;
            if (e.updateType == 3) x = NativeRhythmRules.MoveCycleX(e.pos.x, e.pos.z - playerZ, e.msLeft != 0);
            else if (e.updateType == 1)
            {
                float t = NativeRhythmRules.MovePercent(e.pos.z - playerZ, e.begin, e.end);
                x = Mathf.Lerp(e.mstartPos.x, e.pos.x, t); y = Mathf.Lerp(e.mstartPos.y, e.pos.y, t);
            }
            var frame = route.Evaluate(e.pos.z);
            return frame.Position + frame.Right * (x * lateralScale) + Vector3.up * y;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Check(List<Result> checks, string name, Func<string> action)
        {
            try { checks.Add(new Result { name = name, pass = true, evidence = action() }); }
            catch (Exception e) { checks.Add(new Result { name = name, pass = false, evidence = e.ToString() }); }
        }
    }
}
