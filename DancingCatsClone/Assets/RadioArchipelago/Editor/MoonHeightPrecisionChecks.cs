using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Fresh executable numerical proof for the opt-in Moon authoring policy.</summary>
    public static class MoonHeightPrecisionChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed, legacyReflectionCompatible, defaultBuildUnchanged, protectedAnchorsPreserved;
            public bool realInputRedReproduced, conditionedInputPassed, inputsAndEarlierAssetsUnchanged;
            public string checkedUtc, sourceChartSha256, sourceElevationSha256;
            public int earlierFilesChecked, firstKeys, secondKeys, legacyMathChecks;
            public float minimumStationSeparation = .005f;
            public IslandAbyssChartAuthoring.Report red, green;
            public string scope = "Actual Nori-No/Moon input replay, unchanged legacy rejection and tolerance thresholds, opt-in protected Hold anchors, default-output equality and read-only earlier-asset hashes. Independent imported-triangle and full native playback proof remain separately required.";
        }

        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Moon height precision proof requires stable Edit Mode.");
            var beforeFiles = InputHashes();
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O"), earlierFilesChecked = beforeFiles.Count };
            var flatten = typeof(IslandAbyssChartAuthoring).GetMethod("FlattenSpans", BindingFlags.NonPublic | BindingFlags.Static);
            Require(flatten != null && flatten.GetParameters().Length == 6,
                "The original six-argument FlattenSpans reflection entry must remain unique.");
            var buildParameter = typeof(IslandAbyssChartAuthoring).GetMethod("Build").GetParameters()
                .Single(parameter => parameter.Name == "minimumStationSeparation");
            Require(buildParameter.Name == "minimumStationSeparation" && buildParameter.IsOptional &&
                Convert.ToSingle(buildParameter.DefaultValue) == 0, "Build precision must be explicitly opt-in and default to zero.");
            report.legacyReflectionCompatible = true;

            CheckProtectedAnchors();
            report.protectedAnchorsPreserved = true;
            foreach (var result in IslandAbyssChartAuthoringChecks.CheckMath())
            {
                Require(result.pass, "Legacy authoring regression: " + result.name + ": " + result.evidence);
                report.legacyMathChecks++;
            }

            string sourceChartJson = File.ReadAllText(NoriNoChartAuthoring.BaseChartPath);
            string sourceHeightJson = File.ReadAllText(MoonResonanceIslandJobs.ImportPath + "/play_elevation.json");
            string routeJson = File.ReadAllText(MoonResonanceIslandJobs.ImportPath + "/moon_resonance_route.json");
            report.sourceChartSha256 = Hash(NoriNoChartAuthoring.BaseChartPath);
            report.sourceElevationSha256 = Hash(MoonResonanceIslandJobs.ImportPath + "/play_elevation.json");
            var map = JsonUtility.FromJson<MapTimeline>(sourceChartJson);
            var source = JsonUtility.FromJson<IslandElevationProfile.Document>(sourceHeightJson);
            string beforeMap = JsonUtility.ToJson(map), beforeHeight = JsonUtility.ToJson(source);
            var legacyFirst = IslandAbyssChartAuthoring.FlattenSpans(map, source, 0, 1.20f,
                new IslandAbyssChartAuthoring.Report());
            report.red = new IslandAbyssChartAuthoring.Report();
            try
            {
                IslandAbyssChartAuthoring.FlattenSpans(map, legacyFirst, 0, 1.20f, report.red, .67f);
            }
            catch (InvalidDataException error)
            {
                Require(error.Message.Contains("held-chord/clearance") && report.red.maxHoldGradeVariation > .002f &&
                    report.red.maxHoldChordError <= .0002f && report.red.minimumProfileDelta >= -.001f,
                    "The real RED must be the float-key held-grade counterexample, not a different failure.");
                report.realInputRedReproduced = true;
            }
            Require(report.realInputRedReproduced, "The real unconditioned Moon input must reproduce its measured grade ripple.");

            var conditionedFirst = IslandAbyssChartAuthoring.FlattenSpansWithConditioning(map, source, 0, 1.20f,
                new IslandAbyssChartAuthoring.Report(), 0, .005f);
            report.green = new IslandAbyssChartAuthoring.Report();
            var conditionedSecond = IslandAbyssChartAuthoring.FlattenSpansWithConditioning(map, conditionedFirst,
                0, 1.20f, report.green, .67f, .005f);
            Require(report.green.maxHoldChordError <= .0002f && report.green.maxHoldGradeVariation <= .002f &&
                report.green.minimumProfileDelta >= -.001f && report.green.maxProfileGrade <= .75f,
                "Conditioned actual Moon inputs must pass every unchanged chord, grade and floor threshold.");
            Require(beforeMap == JsonUtility.ToJson(map) && beforeHeight == JsonUtility.ToJson(source),
                "Precision authoring must not mutate its source map or elevation object.");
            report.firstKeys = conditionedFirst.keys.Length; report.secondKeys = conditionedSecond.keys.Length;
            report.conditionedInputPassed = true;

            string spatialChart = JsonUtility.ToJson(IslandChartAuthoring.Create(sourceChartJson, true));
            var defaultBuild = IslandAbyssChartAuthoring.Build(spatialChart, sourceHeightJson, routeJson, 0, 1.20f, 1.8f);
            var explicitLegacyBuild = IslandAbyssChartAuthoring.Build(spatialChart, sourceHeightJson, routeJson,
                0, 1.20f, 1.8f, minimumStationSeparation: 0);
            Require(defaultBuild.ChartJson == explicitLegacyBuild.ChartJson &&
                defaultBuild.ElevationJson == explicitLegacyBuild.ElevationJson &&
                JsonUtility.ToJson(defaultBuild.Report) == JsonUtility.ToJson(explicitLegacyBuild.Report),
                "Omitting the new option must preserve default chart/elevation/report bytes.");
            report.defaultBuildUnchanged = true;
            var afterFiles = InputHashes();
            Require(beforeFiles.Count == afterFiles.Count && beforeFiles.All(pair =>
                afterFiles.TryGetValue(pair.Key, out string hash) && hash == pair.Value),
                "Numerical proof must leave all source and earlier-island asset bytes unchanged.");
            report.inputsAndEarlierAssetsUnchanged = report.passed = true;
            Directory.CreateDirectory(MoonResonanceIslandJobs.ReviewFolder);
            File.WriteAllText(MoonResonanceIslandJobs.ReviewFolder + "/height-precision-checks.json", JsonUtility.ToJson(report, true));
            Debug.Log("MOON_HEIGHT_PRECISION PASS: real RED=" + report.red.maxHoldGradeVariation + ", GREEN=" +
                report.green.maxHoldGradeVariation + ", protected anchors, legacy defaults and " + report.earlierFilesChecked + " input/earlier files preserved.");
        }

        private static void CheckProtectedAnchors()
        {
            const float a = 20.003f, b = 26.003f;
            var map = new MapTimeline { segments = new[] { new MapSegment { kind = "Hold", z = a, endZ = b } } };
            var source = new IslandElevationProfile.Document { length = 80, closed = true, keys = new[] {
                new IslandElevationProfile.Key(0, 4), new IslandElevationProfile.Key(20.004f, 4),
                new IslandElevationProfile.Key(38.254f, 4), new IslandElevationProfile.Key(50.003f, 4),
                new IslandElevationProfile.Key(80, 4) } };
            var legacy = IslandAbyssChartAuthoring.FlattenSpans(map, source, 0, 1, new IslandAbyssChartAuthoring.Report());
            var explicitZero = IslandAbyssChartAuthoring.FlattenSpansWithConditioning(map, source, 0, 1,
                new IslandAbyssChartAuthoring.Report(), 0, 0);
            Require(JsonUtility.ToJson(legacy) == JsonUtility.ToJson(explicitZero), "The legacy wrapper must retain exact default semantics.");
            var conditioned = IslandAbyssChartAuthoring.FlattenSpansWithConditioning(map, source, 0, 1,
                new IslandAbyssChartAuthoring.Report(), 0, .005f);
            foreach (double station in new[] { a - .25 - 12, a - .25, a - .125, (double)a,
                b, b + .125, b + .25, b + .25 + 12 })
                Require(conditioned.keys.Any(key => key.distance == (float)Math.Round(station, 4)),
                    "A protected Hold/cap/guard/join anchor was removed: " + station);
            Require(!conditioned.keys.Any(key => key.distance == 20.004f),
                "A redundant inherited station within 5mm on the same linear chord should be removed.");
            Require(conditioned.keys.Any(key => key.distance == 38.254f) && conditioned.keys.Any(key => key.distance == 50.003f),
                "Join/unheld source stations must remain exact even beside another station.");
            Require(conditioned.keys[0].distance == 0 && conditioned.keys.Last().distance == 80,
                "Closed profile endpoint anchors must remain exact.");
        }

        private static Dictionary<string, string> InputHashes()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string folder in new[] { IslandAbyssJobs.ContentPath, CoralOasisIslandJobs.ContentPath,
                MoonResonanceIslandJobs.ImportPath })
                if (Directory.Exists(folder)) foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    result[path] = Hash(path);
            foreach (string path in new[] { IslandAbyssJobs.ScenePath, CoralOasisIslandJobs.ScenePath,
                NoriNoChartAuthoring.BaseChartPath })
                if (File.Exists(path)) result[path] = Hash(path);
            return result;
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
