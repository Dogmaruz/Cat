using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Deterministic route math only; no scene, collider, song or preference changes.</summary>
    public static class IslandRouteChecks
    {
        [Serializable] public sealed class Result
        {
            public string name, evidence;
            public bool pass;
        }

        [Serializable] private sealed class Report
        {
            public bool pass;
            public string scope = "Closed island route math; no gameplay or rendering test.";
            public Result[] checks;
        }

        private static Vector3 _allocationSink;

        [MenuItem("Tools/Radio Archipelago/Check island route")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Check island route math in Edit Mode.");
            var checks = CheckMath();
            bool passed = true;
            foreach (var check in checks) passed &= check.pass;
            string folder = Path.GetFullPath("Library/RadioArchipelago/IslandRoute");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "route-checks.json"),
                JsonUtility.ToJson(new Report { pass = passed, checks = checks }, true));
            if (!passed)
            {
                var failures = new List<string>();
                foreach (var check in checks) if (!check.pass) failures.Add(check.name + ": " + check.evidence);
                throw new InvalidOperationException("ISLAND_ROUTE FAIL: " + string.Join("; ", failures));
            }
            Debug.Log("ISLAND_ROUTE PASS: " + checks.Length + " math checks; exact polyline, periodic frames and zero hot-path allocations.");
        }

        /// <summary>Managed assertions reusable without opening or changing a Unity scene.</summary>
        public static Result[] CheckMath()
        {
            var checks = new List<Result>();
            Check(checks, "arc_length_and_exact_polyline", () =>
            {
                var route = new IslandRoute(new[] { P(0, 0), P(0, 3), P(9, 3), P(9, 0) });
                Near(route.Length, 24, .0001f, "unequal segment perimeter");
                Near(route.Evaluate(1).Position, P(0, 1), .0001f, "first short span");
                Near(route.Evaluate(7).Position, P(4, 3), .0001f, "distance advances along the longer span");
                Near(route.Evaluate(12).Position, P(9, 3), .0001f, "authored corner must not be cut");
                Near(route.Evaluate(23).Position, P(1, 0), .0001f, "implicit final-to-first edge");
                return "24m nonuniform loop; exact authored vertices and segment positions.";
            });
            Check(checks, "periodic_seam_position_and_frame", () =>
            {
                var route = Circle();
                var seam = route.Evaluate(0);
                var before = route.Evaluate(-.001d);
                var after = route.Evaluate(.001d);
                Near(seam.Position, route.Evaluate(route.ExactLength).Position, .00001f, "one lap closes");
                Near(before.Position, after.Position, .0021f, "position remains continuous across seam");
                Require(Vector3.Dot(before.Tangent, after.Tangent) > .99999f, "seam tangent snapped");
                Require(Vector3.Dot(before.Right, after.Right) > .99999f, "seam right vector snapped");
                Require(Math.Abs(Quaternion.Dot(before.Rotation, after.Rotation)) > .99999f, "seam yaw snapped");
                return "Position, tangent, right and yaw continuous on both sides of lap seam.";
            });
            Check(checks, "lateral_height_and_start_offset", () =>
            {
                var route = Circle();
                const float start = 11, chartDistance = 7, height = 2.25f;
                var frame = route.Evaluate(chartDistance + start);
                var right = route.Map(new Vector3(1.5f, height, chartDistance), start);
                var left = route.Map(new Vector3(-1.5f, height, chartDistance), start);
                Near(Vector3.Dot(right - frame.Position, frame.Right), 1.5f, .00001f, "positive chart X");
                Near(Vector3.Dot(left - frame.Position, frame.Right), -1.5f, .00001f, "negative chart X");
                Near(right.y - frame.Position.y, height, .00001f, "jump height is unchanged");
                Near(Vector3.Distance(left, right), 3, .00002f, "one chart unit equals one world metre");
                Near(route.Map(new Vector3(0, 0, chartDistance), start), frame.Position, .00001f, "start distance applied exactly once");
                return "Both ±1.5m lateral offsets, 2.25m vertical offset, +11m start distance, world scale 1.";
            });
            Check(checks, "changing_yaw_and_orthonormal_frame", () =>
            {
                var route = Square();
                var north = route.Evaluate(5);
                var east = route.Evaluate(15);
                Require(Vector3.Dot(north.Tangent, Vector3.forward) > .99999f, "first span should head north");
                Require(Vector3.Dot(east.Tangent, Vector3.right) > .99999f, "second span should head east");
                Require(Math.Abs(Vector3.Dot(north.Tangent, east.Tangent)) < .00001f, "yaw did not change along route");
                for (int i = 0; i < 160; i++)
                {
                    var frame = route.Evaluate(i * .25f);
                    Near(frame.Tangent.magnitude, 1, .00001f, "unit tangent");
                    Near(frame.Right.magnitude, 1, .00001f, "unit right");
                    Near(Vector3.Dot(frame.Tangent, frame.Right), 0, .00001f, "perpendicular frame axes");
                    Near(frame.Right, Vector3.Cross(Vector3.up, frame.Tangent), .00001f, "Unity right-handed lateral convention");
                    Near(frame.Rotation * Vector3.forward, frame.Tangent, .00001f, "rotation forward agrees with tangent");
                    Near(frame.Rotation * Vector3.right, frame.Right, .00001f, "rotation right agrees with frame");
                }
                return "90-degree changing heading; 160 unit/perpendicular orientation frames.";
            });
            Check(checks, "negative_distances_and_many_laps", () =>
            {
                var route = Circle();
                const double offset = 7.125, laps = 1000000;
                var expected = route.Evaluate(offset);
                Near(route.Evaluate(route.ExactLength * laps + offset).Position, expected.Position, .0001f, "one million forward laps");
                Near(route.Evaluate(-route.ExactLength * laps + offset).Position, expected.Position, .0001f, "one million reverse laps");
                Near(route.Evaluate(-2d).Position, route.Evaluate(route.ExactLength - 2).Position, .00001f, "negative distance wraps forward");
                return "Double-distance wrapping preserves offsets across ±1,000,000 laps.";
            });
            Check(checks, "duplicates_and_defensive_copy", () =>
            {
                var input = new List<Vector3> { P(0, 0), P(0, 0), P(0, 10), P(10, 10), P(10, 0), P(0, 0) };
                var route = new IslandRoute(input);
                input[2] = P(200, 300);
                input.Clear();
                Near(route.Length, 40, .0001f, "duplicate endpoints must not add a segment");
                Near(route.Evaluate(5).Position, P(0, 5), .0001f, "caller mutations must not alter the route");
                return "Consecutive/closing duplicates removed; caller list changes have no effect.";
            });
            Check(checks, "invalid_data_and_nonfinite_queries", () =>
            {
                Throws<ArgumentNullException>(() => new IslandRoute(null));
                Throws<ArgumentException>(() => new IslandRoute(new[] { P(0, 0), P(0, 1) }));
                Throws<ArgumentException>(() => new IslandRoute(new[] { P(0, 0), P(0, 0), P(0, 0) }));
                Throws<ArgumentException>(() => new IslandRoute(new[] { P(0, 0), P(float.NaN, 1), P(1, 0) }));
                Throws<ArgumentException>(() => new IslandRoute(new[] { P(0, 0), P(0, float.PositiveInfinity), P(1, 0) }));
                Throws<ArgumentException>(() => new IslandRoute(new[] { P(0, 0), new Vector3(0, 1, 1), P(1, 0) }));
                Throws<ArgumentException>(() => new IslandRoute(new[] { P(0, 0), P(0, 10), P(0, 5), P(10, 5) }));
                var route = Square();
                Throws<ArgumentOutOfRangeException>(() => route.Evaluate(double.NaN));
                Throws<ArgumentOutOfRangeException>(() => route.Evaluate(double.PositiveInfinity));
                Throws<ArgumentException>(() => route.Map(new Vector3(float.NaN, 0, 0)));
                Throws<ArgumentException>(() => route.Map(Vector3.zero, float.NegativeInfinity));
                return "Null/degenerate/nonplanar/nonfinite data and exact reversals rejected explicitly.";
            });
            Check(checks, "evaluate_and_map_allocate_no_managed_memory", () =>
            {
                var route = Circle();
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < 2000; i++) sum += route.Map(new Vector3(.5f, 1, i * .125f), 11);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 20000; i++)
                {
                    sum += route.Evaluate(i * .125d).Position;
                    sum += route.Map(new Vector3(-.5f, 2, i * .125f), -3);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                _allocationSink = sum;
                Require(_allocationSink.sqrMagnitude > 0, "sample results must be consumed");
                Require(allocated == 0, "hot-path allocated " + allocated + " managed bytes");
                return "40,000 hot-path calls allocated " + allocated.ToString(CultureInfo.InvariantCulture) + " managed bytes after warmup.";
            });
            return checks.ToArray();
        }

        private static IslandRoute Square() => new IslandRoute(new[] { P(0, 0), P(0, 10), P(10, 10), P(10, 0) });

        private static IslandRoute Circle()
        {
            var points = new Vector3[64];
            for (int i = 0; i < points.Length; i++)
            {
                double angle = i * Math.PI * 2 / points.Length;
                points[i] = P((float)(16 * Math.Cos(angle)), (float)(16 * Math.Sin(angle)));
            }
            return new IslandRoute(points);
        }

        private static Vector3 P(float x, float z) => new Vector3(x, 0, z);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Near(float actual, float expected, float tolerance, string message)
            => Require(Math.Abs(actual - expected) <= tolerance, message);
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
