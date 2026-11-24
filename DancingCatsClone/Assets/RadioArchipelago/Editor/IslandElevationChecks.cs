using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Pure route/profile math; no scene, chart, audio or preference changes.</summary>
    public static class IslandElevationChecks
    {
        [Serializable] public sealed class Result
        {
            public string name, evidence;
            public bool pass;
        }

        [Serializable] private sealed class Report
        {
            public bool pass;
            public string scope = "Optional elevation over unchanged planar musical distance; no gameplay or rendering test.";
            public Result[] checks;
        }

        private static Vector3 _allocationSink;

        [MenuItem("Tools/Radio Archipelago/Check island elevation")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Check island elevation in Edit Mode.");
            var results = new List<Result>(CheckMath());
            Check(results, "json_height_profile_contract", () =>
            {
                IslandElevationProfile profile = IslandElevationProfile.FromJson(
                    "{\"length\":40,\"closed\":true,\"keys\":[{\"distance\":0,\"height\":0},{\"distance\":20,\"height\":2},{\"distance\":40,\"height\":0}]}");
                Require(profile.Closed && profile.Length == 40, "JSON period and closure");
                Near(profile.Evaluate(10), 1, .00001f, "JSON keys drive height");
                Near(profile.Grade(10), .1875f, .00001f, "JSON keys drive grade");
                Throws<ArgumentException>(() => IslandElevationProfile.FromJson(null));
                Throws<ArgumentException>(() => IslandElevationProfile.FromJson("{}"));
                return "Unity JSON fields deserialize into the same validated immutable profile; absent/missing data is rejected.";
            });
            Result[] checks = results.ToArray();
            bool passed = true;
            foreach (Result check in checks) passed &= check.pass;
            string folder = Path.GetFullPath("Library/RadioArchipelago/IslandElevation");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "elevation-checks.json"),
                JsonUtility.ToJson(new Report { pass = passed, checks = checks }, true));
            if (!passed)
            {
                var failures = new List<string>();
                foreach (Result check in checks) if (!check.pass) failures.Add(check.name + ": " + check.evidence);
                throw new InvalidOperationException("ISLAND_ELEVATION FAIL: " + string.Join("; ", failures));
            }
            Debug.Log("ISLAND_ELEVATION PASS: " + checks.Length + " profile, timing, frame and allocation checks.");
        }

        /// <summary>Reusable without a running Unity Editor; uses no native JSON calls.</summary>
        public static Result[] CheckMath()
        {
            var checks = new List<Result>();
            Check(checks, "hills_valleys_and_analytic_grade", () =>
            {
                IslandElevationProfile profile = Hills();
                Near(profile.Evaluate(0), 0, 0, "start height");
                Near(profile.Evaluate(5), 2, .00001f, "halfway uphill height");
                Near(profile.Evaluate(10), 4, 0, "hill crest");
                Near(profile.Evaluate(20), -2, 0, "valley bottom");
                Near(profile.Evaluate(30), 3, 0, "second hill crest");
                Near(profile.Grade(5), .75f, .00001f, "dh/ds at uphill midpoint");
                Near(profile.Grade(15), -1.125f, .00001f, "dh/ds at descent midpoint");
                Require(profile.Grade(25) > 0 && profile.Grade(35) < 0, "hill/descent signs");
                return "40m loop: 4m hill, -2m valley, 3m hill; midpoint grades +0.75 and -1.125.";
            });
            Check(checks, "c2_keys_and_closed_seam", () =>
            {
                IslandElevationProfile profile = Hills();
                const double epsilon = .001;
                for (int key = 0; key <= 4; key++)
                {
                    double distance = key * 10;
                    Near(profile.Grade(distance), 0, .000001f, "key grade");
                    Near(profile.Grade(distance - epsilon), profile.Grade(distance + epsilon), .000002f,
                        "grade continuity at key/seam");
                    Require(Math.Abs((profile.Grade(distance + epsilon) - profile.Grade(distance)) / epsilon) < .001 &&
                        Math.Abs((profile.Grade(distance) - profile.Grade(distance - epsilon)) / epsilon) < .001,
                        "curvature must approach zero from both sides at keys/seam");
                }
                Near(profile.Evaluate(-epsilon), profile.Evaluate(epsilon), .000001f, "closed seam height");
                Near(profile.Evaluate(40), profile.Evaluate(0), 0, "one lap closes");
                Near(profile.Evaluate(40000005d), 2, .00001f, "one million laps retain height phase");
                Near(profile.Evaluate(-39999995d), 2, .00001f, "reverse laps retain height phase");
                return "Height, grade and zero key curvature remain continuous, including the periodic seam and signed laps.";
            });
            Check(checks, "dense_linear_slope_keeps_speed_and_periodic_nonzero_grade", () =>
            {
                var keys = new List<IslandElevationProfile.Key>();
                for (int i = 0; i <= 10; i++) keys.Add(K(i, i * .5f));
                keys.Add(K(20, 0));
                var dense = new IslandElevationProfile(20, true, keys);
                for (int i = 0; i <= 240; i++)
                {
                    double distance = 2 + i * .025;
                    Near(dense.Evaluate(distance), (float)(distance * .5), .00001f, "dense uphill stays linear");
                    Near(dense.Grade(distance), .5f, .00001f, "dense uphill must not stop at each key");
                }
                var crossing = new IslandElevationProfile(40, true,
                    new[] { K(0, 2), K(10, 4), K(20, 2), K(30, 0), K(40, 2) });
                Near(crossing.Grade(0), .2f, .00001f, "seam on a slope retains its grade");
                Near(crossing.Grade(-.001), crossing.Grade(.001), .00001f, "nonzero grade across periodic seam");
                return "241 dense uphill samples retain +0.5 grade; a sloping seam retains continuous +0.2 grade.";
            });
            Check(checks, "finite_monotone_intervals_and_clamped_open_profile", () =>
            {
                IslandElevationProfile profile = Hills();
                float[] heights = { 0, 4, -2, 3, 0 };
                for (int segment = 0; segment < 4; segment++)
                    for (int i = 0; i <= 128; i++)
                    {
                        double distance = segment * 10 + i * (10d / 128);
                        float height = profile.Evaluate(distance), grade = profile.Grade(distance);
                        Require(Finite(height) && Finite(grade), "finite profile sample");
                        Require(height >= Math.Min(heights[segment], heights[segment + 1]) - .00001f &&
                            height <= Math.Max(heights[segment], heights[segment + 1]) + .00001f,
                            "profile overshot an authored hill/valley");
                    }
                var open = new IslandElevationProfile(10, false, new[] { K(0, 1), K(10, 3) });
                Near(open.Evaluate(-20), 1, 0, "open start clamps");
                Near(open.Evaluate(20), 3, 0, "open end clamps");
                Near(open.Grade(-20), 0, 0, "clamped open start has zero grade");
                Near(open.Grade(20), 0, 0, "clamped open end has zero grade");
                return "516 finite samples preserve authored extrema; standalone open profiles clamp safely.";
            });
            Check(checks, "uneven_keys_do_not_reverse_a_shallow_uphill", () =>
            {
                var profile = new IslandElevationProfile(10.2f, false,
                    new[] { K(0, 0), K(.1f, 10), K(10.1f, 10.1f), K(10.2f, 20.1f) });
                for (int i = 0; i <= 1000; i++)
                {
                    double distance = .1 + i * .01;
                    Require(profile.Grade(distance) >= -.000001f,
                        "limited shared slopes must not reverse a shallow uphill between steep shoulders");
                    Require(profile.Evaluate(distance) >= 10 - .00001f && profile.Evaluate(distance) <= 10.1f + .00001f,
                        "uneven spacing must not overshoot the shallow span");
                }
                return "1,001 samples keep a 10m shallow uphill monotone between two short steep rises.";
            });
            Check(checks, "planar_length_timing_lateral_and_yaw_unchanged", () =>
            {
                Vector3[] points = Square();
                var flat = new IslandRoute(points);
                IslandElevationProfile profile = Hills();
                var elevated = new IslandRoute(points, profile);
                Require(flat.Length == elevated.Length && flat.ExactLength == elevated.ExactLength,
                    "elevation changed the musical XZ perimeter");
                for (int i = -160; i <= 320; i++)
                {
                    double distance = i * .125;
                    IslandRoute.Frame a = flat.Evaluate(distance), b = elevated.Evaluate(distance);
                    Require(a.Position.x == b.Position.x && a.Position.z == b.Position.z,
                        "same musical distance moved horizontally");
                    Near(b.Position.y, profile.Evaluate(distance), .00001f, "profile height over XZ arc");
                    Near(b.Tangent, a.Tangent, 0, "yaw-only tangent changed");
                    Near(b.Right, a.Right, 0, "lateral basis changed");
                    Require(a.Rotation.Equals(b.Rotation), "disc/input yaw changed");
                }
                Vector3 mapped = elevated.Map(new Vector3(1.25f, 2, 5));
                Vector3 baseline = flat.Map(new Vector3(1.25f, 2, 5));
                Near(mapped, baseline + Vector3.up * 2, .00001f, "chart jump remains above elevated baseline");
                Throws<ArgumentException>(() => new IslandRoute(new[] { new Vector3(0, 1, 0), P(0, 10), P(10, 0) }));
                return "40m perimeter and 481 XZ/yaw samples unchanged; chart height and lateral input remain independent.";
            });
            Check(checks, "surface_basis_tilts_without_tilting_input", () =>
            {
                var elevated = new IslandRoute(Square(), Hills());
                var flat = new IslandRoute(Square());
                for (int i = 0; i < 640; i++)
                {
                    IslandRoute.Frame frame = elevated.Evaluate(i * .0625);
                    Near(frame.SurfaceTangent.magnitude, 1, .00001f, "unit surface tangent");
                    Near(frame.SurfaceUp.magnitude, 1, .00001f, "unit surface up");
                    Near(Vector3.Dot(frame.SurfaceTangent, frame.SurfaceUp), 0, .00001f, "surface orthogonality");
                    Near(Vector3.Dot(frame.SurfaceTangent, frame.Right), 0, .00001f, "surface/right orthogonality");
                    Near(Vector3.Cross(frame.SurfaceTangent, frame.Right), frame.SurfaceUp, .00001f, "surface handedness");
                    Near(frame.SurfaceTangent.y / Vector3.Dot(frame.SurfaceTangent, frame.Tangent), frame.Grade,
                        .00001f, "surface tilt agrees with dh/ds");
                    Require(frame.SurfaceUp.y > 0 && frame.Tangent.y == 0 && frame.Right.y == 0,
                        "surface up and yaw-only movement convention");
                }
                Near(flat.Evaluate(5).SurfaceTangent, flat.Evaluate(5).Tangent, 0, "flat surface forward");
                Near(flat.Evaluate(5).SurfaceUp, Vector3.up, 0, "flat surface up");
                Near(flat.Evaluate(5).Grade, 0, 0, "flat grade");
                return "640 unit, perpendicular surface frames; pitch affects FX basis while input/disc basis stays yaw-only.";
            });
            Check(checks, "invalid_profiles_and_nonfinite_queries_rejected", () =>
            {
                Throws<ArgumentException>(() => new IslandElevationProfile(0, true, new[] { K(0, 0), K(1, 0) }));
                Throws<ArgumentException>(() => new IslandElevationProfile(float.PositiveInfinity, true, new[] { K(0, 0), K(1, 0) }));
                Throws<ArgumentNullException>(() => new IslandElevationProfile(40, true, null));
                Throws<ArgumentException>(() => new IslandElevationProfile(40, true, new[] { K(0, 0) }));
                Throws<ArgumentException>(() => new IslandElevationProfile(40, true, new[] { K(1, 0), K(40, 0) }));
                Throws<ArgumentException>(() => new IslandElevationProfile(40, true, new[] { K(0, 0), K(39, 0) }));
                Throws<ArgumentException>(() => new IslandElevationProfile(40, true, new[] { K(0, 0), K(40, 1) }));
                Throws<ArgumentException>(() => new IslandElevationProfile(40, true, new[] { K(0, 0), K(10, 1), K(10, 2), K(40, 0) }));
                Throws<ArgumentException>(() => new IslandElevationProfile(40, true, new[] { K(0, 0), K(10, float.NaN), K(40, 0) }));
                Throws<ArgumentException>(() => new IslandElevationProfile(1, true, new[] { K(0, 0), K(float.Epsilon, float.MaxValue), K(1, 0) }));
                Throws<ArgumentException>(() => new IslandRoute(Square(), new IslandElevationProfile(20, true, new[] { K(0, 0), K(20, 0) })));
                Throws<ArgumentException>(() => new IslandRoute(Square(), new IslandElevationProfile(40, false, new[] { K(0, 0), K(40, 0) })));
                var profile = Hills();
                Throws<ArgumentOutOfRangeException>(() => profile.Evaluate(double.NaN));
                Throws<ArgumentOutOfRangeException>(() => profile.Grade(double.NegativeInfinity));
                return "Malformed, nonfinite, discontinuous, unordered, incompatible and unrepresentable profiles fail at the boundary.";
            });
            Check(checks, "defensive_keys_and_optional_flat_default", () =>
            {
                var keys = new[] { K(0, 1), K(40, 1) };
                var profile = new IslandElevationProfile(40, true, keys);
                keys[0] = K(0, 9);
                Near(profile.Evaluate(0), 1, 0, "profile must copy caller keys");
                var explicitFlat = new IslandRoute(Square(), null);
                Near(explicitFlat.Evaluate(5).Position, P(0, 5), 0, "null elevation preserves existing flat route");
                return "Caller key edits cannot mutate a loaded profile; omitted/null elevation preserves the flat pilot.";
            });
            Check(checks, "profile_route_and_map_allocate_zero_after_warmup", () =>
            {
                IslandElevationProfile profile = Hills();
                var route = new IslandRoute(Square(), profile);
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < 2000; i++)
                {
                    sum += route.Map(new Vector3(.5f, 1, i * .125f), 11);
                    sum.y += profile.Evaluate(i * .125d) + profile.Grade(i * .125d);
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 20000; i++)
                {
                    IslandRoute.Frame frame = route.Evaluate(i * .125d);
                    sum += frame.Position + frame.SurfaceUp + frame.SurfaceTangent;
                    sum += route.Map(new Vector3(-.5f, 2, i * .125f), -3);
                    sum.y += profile.Evaluate(i * .125d) + profile.Grade(i * .125d);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                _allocationSink = sum;
                Require(_allocationSink.sqrMagnitude > 0, "sample results consumed");
                Require(allocated == 0, "hot path allocated " + allocated + " managed bytes");
                return "80,000 warmed profile/route/map calls allocated " + allocated.ToString(CultureInfo.InvariantCulture) + " managed bytes.";
            });
            return checks.ToArray();
        }

        private static IslandElevationProfile Hills() => new IslandElevationProfile(40, true,
            new[] { K(0, 0), K(10, 4), K(20, -2), K(30, 3), K(40, 0) });
        private static IslandElevationProfile.Key K(float distance, float height) => new IslandElevationProfile.Key(distance, height);
        private static Vector3[] Square() => new[] { P(0, 0), P(0, 10), P(10, 10), P(10, 0) };
        private static Vector3 P(float x, float z) => new Vector3(x, 0, z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Near(float actual, float expected, float tolerance, string message)
            => Require(Math.Abs(actual - expected) <= tolerance, message + ": actual=" + actual + ", expected=" + expected);
        private static void Near(Vector3 actual, Vector3 expected, float tolerance, string message)
            => Require(Vector3.Distance(actual, expected) <= tolerance, message);
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }
        private static void Check(List<Result> results, string name, Func<string> action)
        {
            try { results.Add(new Result { name = name, pass = true, evidence = action() }); }
            catch (Exception error) { results.Add(new Result { name = name, pass = false, evidence = error.ToString() }); }
        }
    }
}
