using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Flight elevation math only; never changes a scene, song or native simulation.</summary>
    public static class IslandFlightElevationChecks
    {
        [Serializable] public sealed class Result
        {
            public string name, evidence;
            public bool pass;
        }

        [Serializable] private sealed class Report
        {
            public bool pass;
            public string scope = "Presentation-only endpoint heights and native arc clearance; no gameplay or rendering test.";
            public Result[] checks;
        }

        private static float _allocationSink;

        [MenuItem("Tools/Radio Archipelago/Check island flight elevation")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Check island flight elevation in Edit Mode.");
            var checks = CheckMath();
            bool passed = true;
            foreach (var check in checks) passed &= check.pass;
            string folder = Path.GetFullPath("Library/RadioArchipelago/IslandElevation");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "flight-elevation-checks.json"),
                JsonUtility.ToJson(new Report { pass = passed, checks = checks }, true));
            if (!passed)
            {
                var failures = new List<string>();
                foreach (var check in checks) if (!check.pass) failures.Add(check.name + ": " + check.evidence);
                throw new InvalidOperationException("ISLAND_FLIGHT_ELEVATION FAIL: " + string.Join("; ", failures));
            }
            Debug.Log("ISLAND_FLIGHT_ELEVATION PASS: " + checks.Length + " checks; native chart distance and jump timing unchanged.");
        }

        public static Result[] CheckMath()
        {
            var checks = new List<Result>();
            Check(checks, "null_and_flat_profile_preserve_native_arc", () =>
            {
                var flat = Profile(new IslandElevationProfile.Key(0, 2), new IslandElevationProfile.Key(40, 2));
                var start = new Vector3(.7f, .35f, 3);
                for (int i = -20; i <= 120; i++)
                {
                    float t = i * .01f;
                    float z = start.z + 4 * t;
                    Near(IslandFlightElevation.Sample(null, 11, 2, start, 4, z), 0, 0, "absent profile");
                    Near(IslandFlightElevation.Sample(flat, 11, 2, start, 4, z), 2, 0, "constant profile contribution");
                    float native = NativeRhythmRules.JumpY(start.y, NativeRhythmRules.HeightForDistance(4), t);
                    Near(native + IslandFlightElevation.Sample(flat, 11, 2, start, 4, z), native + 2, .000001f,
                        "only a constant height may be added on flat ground");
                }
                return "141 samples before, during and after flight preserve the recovered native arc on absent/flat profiles.";
            });
            Check(checks, "uphill_and_downhill_use_endpoint_chords", () =>
            {
                var hill = Profile(new IslandElevationProfile.Key(0, 0), new IslandElevationProfile.Key(10, 2),
                    new IslandElevationProfile.Key(20, 0), new IslandElevationProfile.Key(40, 0));
                CheckChord(hill, 0, 1, new Vector3(0, .25f, 2), 4);
                CheckChord(hill, 0, 1, new Vector3(0, .25f, 12), 4);
                CheckChord(hill, 2, 2, new Vector3(0, .25f, 3), 4);
                float airborne = IslandFlightElevation.Sample(hill, 0, 1, new Vector3(0, 0, 2), 4, 3);
                Require(Math.Abs(airborne - hill.Evaluate(3)) > .001f, "airborne actor followed instantaneous ground height");
                return "Ascending, descending and offset/scaled flights interpolate their contact heights at unchanged chart progress.";
            });
            Check(checks, "convex_ridge_gets_only_required_clearance", () =>
            {
                var ridge = Profile(new IslandElevationProfile.Key(0, 0), new IslandElevationProfile.Key(4, 0),
                    new IslandElevationProfile.Key(5, 1.15f), new IslandElevationProfile.Key(6, 0),
                    new IslandElevationProfile.Key(40, 0));
                var start = new Vector3(0, .25f, 4);
                float maximumExtra = 0, minimumInteriorClearance = float.MaxValue;
                for (int i = 1; i < 1000; i++)
                {
                    float t = i * .001f;
                    float z = start.z + 2 * t;
                    float extra = IslandFlightElevation.Sample(ridge, 0, 1, start, 2, z);
                    float nativeArc = NativeRhythmRules.JumpY(0, NativeRhythmRules.HeightForDistance(2), t);
                    float clearance = nativeArc + extra - ridge.Evaluate(z);
                    Require(clearance >= -.00001f, "courier arc intersects the convex ridge at progress " + t);
                    if (t >= .35f && t <= .65f) minimumInteriorClearance = Mathf.Min(minimumInteriorClearance, clearance);
                    maximumExtra = Mathf.Max(maximumExtra, extra);
                }
                Require(maximumExtra > .3f && maximumExtra < .65f, "ridge lift should remain small and positive");
                Require(minimumInteriorClearance > .04f, "ridge crest needs a small clearance margin");
                Near(IslandFlightElevation.Sample(ridge, 0, 1, start, 2, 4), 0, 0, "takeoff height");
                Near(IslandFlightElevation.Sample(ridge, 0, 1, start, 2, 6), 0, 0, "landing height");
                return "999 interior samples clear a 1.15m convex crest with less than 0.65m extra lift and exact zero lift at contact.";
            });
            Check(checks, "periodic_seam_and_progress_clamping", () =>
            {
                var seam = Profile(new IslandElevationProfile.Key(0, .6f), new IslandElevationProfile.Key(5, .8f),
                    new IslandElevationProfile.Key(20, 0), new IslandElevationProfile.Key(35, .4f),
                    new IslandElevationProfile.Key(40, .6f));
                var start = new Vector3(0, 0, 38);
                CheckChord(seam, 0, 1, start, 4);
                for (int i = 0; i <= 100; i++)
                {
                    float z = start.z + 4 * i * .01f;
                    float expected = IslandFlightElevation.Sample(seam, 0, 1, start, 4, z);
                    Near(IslandFlightElevation.Sample(seam, 40, 1, start, 4, z), expected, .00001f, "one-lap alias");
                    Near(IslandFlightElevation.Sample(seam, 0, 1, new Vector3(0, 0, -2), 4, z - 40), expected,
                        .00001f, "negative-distance alias");
                }
                Near(IslandFlightElevation.Sample(seam, 0, 1, start, 4, 30), seam.Evaluate(38), 0, "before takeoff clamps baseline");
                Near(IslandFlightElevation.Sample(seam, 0, 1, start, 4, 50), seam.Evaluate(42), 0, "after landing clamps baseline");
                Require(NativeRhythmRules.JumpY(0, 1, 1.25f) < 0, "native missed-jump fall must remain unbounded");
                Near(seam.Grade(-.0001f), seam.Grade(39.9999f), .00001f, "grade is periodic at seam");
                return "Seam-crossing flight matches signed/later-lap aliases; only visual baseline clamps while native missed jumps keep falling.";
            });
            Check(checks, "sample_allocates_no_managed_memory", () =>
            {
                var profile = Profile(new IslandElevationProfile.Key(0, 0), new IslandElevationProfile.Key(4, 0),
                    new IslandElevationProfile.Key(5, 1.15f), new IslandElevationProfile.Key(6, 0),
                    new IslandElevationProfile.Key(40, 0));
                var start = new Vector3(0, .25f, 4);
                float sum = 0;
                for (int i = 0; i < 2000; i++) sum += IslandFlightElevation.Sample(profile, 0, 1, start, 2, 4 + (i % 100) * .02f);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 20000; i++) sum += IslandFlightElevation.Sample(profile, 0, 1, start, 2, 4 + (i % 100) * .02f);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                _allocationSink = sum;
                Require(_allocationSink > 0, "consume sampled elevations");
                Require(allocated == 0, "sampler allocated " + allocated + " managed bytes");
                return "20,000 warmed flight samples allocated " + allocated + " managed bytes.";
            });
            return checks.ToArray();
        }

        private static IslandElevationProfile Profile(params IslandElevationProfile.Key[] keys)
            => new IslandElevationProfile(40, true, keys);

        private static void CheckChord(IslandElevationProfile profile, float offset, float scale, Vector3 start, float distance)
        {
            float first = profile.Evaluate((double)offset + start.z * scale);
            float last = profile.Evaluate((double)offset + (start.z + distance) * scale);
            for (int i = 0; i <= 100; i++)
            {
                float t = i * .01f;
                Near(IslandFlightElevation.Sample(profile, offset, scale, start, distance, start.z + distance * t),
                    Mathf.Lerp(first, last, t), .00001f, "contact-height chord at progress " + t);
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Near(float actual, float expected, float tolerance, string message)
            => Require(Math.Abs(actual - expected) <= tolerance, message + ": " + actual + " versus " + expected);
        private static void Check(List<Result> results, string name, Func<string> action)
        {
            try { results.Add(new Result { name = name, pass = true, evidence = action() }); }
            catch (Exception error) { results.Add(new Result { name = name, pass = false, evidence = error.ToString() }); }
        }
    }
}
