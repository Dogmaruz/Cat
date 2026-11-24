using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Derivative-only spatial policy. Musical clocks, native physics and XZ route stay authoritative.</summary>
    public static class IslandAbyssChartAuthoring
    {
        private const double KeySpacing = .1, JoinWidth = 12, LinearGuard = .25;
        private const float MinimumRadius = .33f, MovingLane = .92f;

        [Serializable] public sealed class Report
        {
            public int sourceHoldSpans, mergedHoldIntervals, eligibleRounds, movingRounds, totalMovingRounds, addedMovingRounds, stationaryLaneRepairs, capCoverageBridgedGaps;
            public int minimumRadiusIndex = -1, minimumRadiusOtherIndex = -1;
            public bool fullLoopFlattened, reachable, noOverlaps, musicalTimingPreserved, uniformRoundTiles;
            public string sourceChartSha256, sourceElevationSha256, routeSha256, chartSha256, elevationSha256;
            public float holdCoverage, maxHoldChordError, maxHoldGradeVariation, maxHoldGrade, maxLift, maxProfileGrade, sourceMaxProfileGrade,
                minimumProfileDelta, minimumProfileDeltaDistance,
                movingShare, minimumRadius, maxLateralShift, lateralScale,
                maxProfileGradeDistance, sourceGradeAtPeak, heightLiftAtPeak, capCoverageBridgedDistance;
            public string maxProfileGradeRegion;
            public string policy = "Merged periodic Hold/Follow chords; C2 joins outside holds; native sine cycles on 40% of eligible round supports.";
            public IslandSpanSupportSpacing.Report fullSupportFootprints;
        }

        public sealed class Result
        {
            public string ChartJson, ElevationJson;
            public Report Report;
        }

        [Serializable] private sealed class RouteDocument { public bool closed; public Vector3[] points; }
        private sealed class Interval
        {
            public double a, b, height, grade, leftGuard, rightGuard, leftJoin, rightJoin;
        }

        /// <summary>Returns new JSON only; never writes, re-times or mutates a source asset.</summary>
        public static Result Build(string sourceChartJson, string sourceElevationJson, string routeJson,
            float startDistance, float distanceScale, float lateralScale = 1,
            GameObject terrainPrefab = null, float tileElevation = 1, float tileThickness = .30f, bool requireUniformRounds = false,
            float minimumStationSeparation = 0, bool repairFullSupportLanes = false)
        {
            if (repairFullSupportLanes && !requireUniformRounds)
                throw new ArgumentException("Full-support lane repair requires the strict full-size round policy.", nameof(repairFullSupportLanes));
            MapTimeline original = JsonUtility.FromJson<MapTimeline>(sourceChartJson);
            MapTimeline chart = JsonUtility.FromJson<MapTimeline>(sourceChartJson);
            var source = JsonUtility.FromJson<IslandElevationProfile.Document>(sourceElevationJson);
            var routeDocument = JsonUtility.FromJson<RouteDocument>(routeJson);
            if (chart?.elements == null || chart.segments == null || routeDocument?.points == null || !routeDocument.closed)
                throw new InvalidDataException("A compiled chart and closed route are required for Abyss authoring.");
            if (float.IsNaN(lateralScale) || float.IsInfinity(lateralScale) || lateralScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(lateralScale));
            var report = new Report { sourceChartSha256 = Digest(sourceChartJson), sourceElevationSha256 = Digest(sourceElevationJson), routeSha256 = Digest(routeJson), lateralScale = lateralScale };
            var elevation = FlattenSpansWithConditioning(chart, source, startDistance, distanceScale, report, 0, minimumStationSeparation);
            if (report.maxProfileGrade > .75f)
                throw new InvalidDataException("Abyss authoring exceeds the gentle vertical grade budget: " + report.maxProfileGrade + ".");
            var route = new IslandRoute(routeDocument.points,
                new IslandElevationProfile(elevation.length, elevation.closed, elevation.keys));
            PromoteMovingRounds(chart, route, startDistance, distanceScale, report, lateralScale, requireUniformRounds);
            if (terrainPrefab != null)
            {
                // Choreography is compiled against the accepted legacy profile first.
                // Scene clearance may change only the presentation elevation afterwards.
                var floor = IslandTerrainClearanceAuthoring.BuildFloor(terrainPrefab, routeJson, elevation, chart,
                    startDistance, distanceScale, lateralScale, tileElevation, tileThickness);
                var safety = new Report();
                elevation = FlattenSpansWithConditioning(chart, floor, startDistance, distanceScale, safety, .67f, minimumStationSeparation);
                report.sourceHoldSpans=safety.sourceHoldSpans;report.mergedHoldIntervals=safety.mergedHoldIntervals;
                report.holdCoverage=safety.holdCoverage;report.fullLoopFlattened=safety.fullLoopFlattened;
                report.maxHoldChordError=safety.maxHoldChordError;report.maxHoldGradeVariation=safety.maxHoldGradeVariation;
                report.maxHoldGrade=safety.maxHoldGrade;report.maxProfileGrade=safety.maxProfileGrade;
                report.sourceMaxProfileGrade=safety.sourceMaxProfileGrade;report.minimumProfileDelta=safety.minimumProfileDelta;
                report.minimumProfileDeltaDistance=safety.minimumProfileDeltaDistance;
                report.maxProfileGradeDistance=safety.maxProfileGradeDistance;report.sourceGradeAtPeak=safety.sourceGradeAtPeak;
                report.heightLiftAtPeak=safety.heightLiftAtPeak;report.maxProfileGradeRegion=safety.maxProfileGradeRegion;
                report.capCoverageBridgedGaps=safety.capCoverageBridgedGaps;report.capCoverageBridgedDistance=safety.capCoverageBridgedDistance;
                report.maxLift=Math.Max(report.maxLift,safety.maxLift);
                if(report.maxProfileGrade>.75f)
                {
                    const string diagnostics = "Library/RadioArchipelago/TerrainClearance";
                    Directory.CreateDirectory(diagnostics);
                    File.WriteAllText(diagnostics + "/failed-hold-floor.json", JsonUtility.ToJson(floor, true));
                    File.WriteAllText(diagnostics + "/failed-hold-profile.json", JsonUtility.ToJson(elevation, true));
                    File.WriteAllText(diagnostics + "/failed-hold-grade-report.json", JsonUtility.ToJson(safety, true));
                    throw new InvalidDataException("Terrain-safe full Hold caps exceed the gentle vertical grade budget: " +
                        JsonUtility.ToJson(safety) + ". Floor/profile evidence: " + diagnostics);
                }
            }
            if (requireUniformRounds)
            {
                // The final terrain-safe profile supplies the actual long-body
                // chord/cap geometry. Round-only spacing cannot validate it.
                var finalRoute = new IslandRoute(routeDocument.points,
                    new IslandElevationProfile(elevation.length, elevation.closed, elevation.keys));
                var settings = new IslandDynamicSupportSpacing.Settings(startDistance, distanceScale, 35, 4, chart.forwardSpeed, lateralScale);
                if (repairFullSupportLanes)
                {
                    var repair = IslandFullSupportLaneRepair.Repair(chart, finalRoute, settings, tileThickness, tileElevation);
                    report.stationaryLaneRepairs += repair.repairedRounds;
                    report.maxLateralShift = Math.Max(report.maxLateralShift, repair.maximumLateralChange);
                    report.policy += " Full-support static-round lane repair: " + JsonUtility.ToJson(repair) + ".";
                }
                var preferred = Enumerable.Repeat(IslandSupportSpacing.NativeJumpRadius, chart.elements.Length).ToArray();
                var radii = new float[preferred.Length]; var limits = new IslandDynamicSupportSpacing.RadiusLimit[preferred.Length];
                IslandDynamicSupportSpacing.Resolve(finalRoute, settings, chart.elements, preferred, radii, limits);
                for (int i = 0; i < radii.Length; i++)
                {
                    int id = chart.elements[i].id;
                    if (id != 0 && id != 1 && id != 4 || radii[i] >= IslandSupportSpacing.NativeJumpRadius) continue;
                    throw new InvalidDataException("Final terrain-safe route would shrink a native round: pair=" + i + "/" +
                        limits[i].otherIndex + ", radius=" + radii[i] + ", distance=" + limits[i].distance + ".");
                }
                report.fullSupportFootprints = IslandSpanSupportSpacing.Audit(chart, finalRoute,
                    settings,
                    tileThickness, tileElevation);
                if (!report.fullSupportFootprints.passed)
                    throw new InvalidDataException("The full-size ceramic support footprint challenge failed: " +
                        JsonUtility.ToJson(report.fullSupportFootprints));
            }
            chart.normalizationNotes = (chart.normalizationNotes ?? Array.Empty<string>())
                .Where(n => n == null || !n.StartsWith("Abyss spatial policy:", StringComparison.Ordinal))
                .Concat(new[] { "Abyss spatial policy: " + report.policy }).ToArray();
            IslandChartAuthoring.Verify(original, chart);
            report.musicalTimingPreserved = true;
            string chartJson = JsonUtility.ToJson(chart, true), elevationJson = JsonUtility.ToJson(elevation, true);
            report.chartSha256 = Digest(chartJson); report.elevationSha256 = Digest(elevationJson);
            return new Result { ChartJson = chartJson, ElevationJson = elevationJson, Report = report };
        }

        internal static IslandElevationProfile.Document FlattenSpans(MapTimeline map,
            IslandElevationProfile.Document source, float startDistance, float distanceScale, Report report, float capGuard=0)
            => FlattenSpansWithConditioning(map, source, startDistance, distanceScale, report, capGuard, 0);

        /// <summary>Optional Editor-only station conditioning; legacy entry and default output remain unchanged.</summary>
        internal static IslandElevationProfile.Document FlattenSpansWithConditioning(MapTimeline map,
            IslandElevationProfile.Document source, float startDistance, float distanceScale, Report report,
            float capGuard, float minimumStationSeparation)
        {
            ValidateCoordinates(startDistance, distanceScale);
            if (float.IsNaN(minimumStationSeparation) || float.IsInfinity(minimumStationSeparation) || minimumStationSeparation < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumStationSeparation));
            if (source == null) throw new ArgumentNullException(nameof(source));
            var original = new IslandElevationProfile(source.length, source.closed, source.keys);
            if (!source.closed) throw new InvalidDataException("Abyss height authoring requires a closed profile.");
            double length = source.length;
            var pieces = new List<Interval>();
            foreach (MapSegment segment in map.segments)
            {
                if (segment == null || segment.kind != "Hold" && segment.kind != "Follow") continue;
                double span = ((double)segment.endZ - segment.z) * distanceScale;
                if (span <= 0) continue;
                report.sourceHoldSpans++;
                span += 2 * capGuard;
                if (span >= length)
                {
                    pieces.Clear(); pieces.Add(new Interval { a = 0, b = length });
                    report.fullLoopFlattened = true;
                    break;
                }
                double a = Wrap(startDistance + (double)segment.z * distanceScale - capGuard, length), b = a + span;
                pieces.Add(new Interval { a = a, b = Math.Min(b, length) });
                if (b > length) pieces.Add(new Interval { a = 0, b = b - length });
            }
            pieces.Sort((a, b) => a.a.CompareTo(b.a));
            var intervals = new List<Interval>();
            // A cap-protected seam shorter than two normal key stations cannot
            // accommodate two independent C2 feathers. Bridge only that tiny
            // geometric gap with their shared straight chord; musical spans do
            // not change. Unguarded legacy authoring keeps its original union.
            double mergeGap = capGuard > 0 ? 2 * KeySpacing : .0001;
            foreach (Interval piece in pieces)
            {
                if (intervals.Count == 0 || piece.a > intervals[intervals.Count - 1].b + mergeGap)
                    intervals.Add(piece);
                else
                {
                    double gap = piece.a - intervals[intervals.Count - 1].b;
                    if (gap > .0001) { report.capCoverageBridgedGaps++; report.capCoverageBridgedDistance += (float)gap; }
                    intervals[intervals.Count - 1].b = Math.Max(intervals[intervals.Count - 1].b, piece.b);
                }
            }
            if (intervals.Count > 1 && (capGuard > 0 ?
                intervals[0].a + length - intervals[intervals.Count - 1].b <= mergeGap :
                intervals[0].a <= .0001 && intervals[intervals.Count - 1].b >= length - .0001))
            {
                double gap = intervals[0].a + length - intervals[intervals.Count - 1].b;
                if (gap > .0001) { report.capCoverageBridgedGaps++; report.capCoverageBridgedDistance += (float)gap; }
                intervals[intervals.Count - 1].b = intervals[0].b + length;
                intervals.RemoveAt(0);
            }
            if (capGuard > 0 && intervals.Count == 1 && length - (intervals[0].b - intervals[0].a) <= mergeGap)
            {
                double gap = length - (intervals[0].b - intervals[0].a);
                if (gap > .0001) { report.capCoverageBridgedGaps++; report.capCoverageBridgedDistance += (float)gap; }
                intervals[0].a = 0; intervals[0].b = length;
            }
            double coverage = intervals.Sum(i => i.b - i.a);
            report.mergedHoldIntervals = intervals.Count;
            report.holdCoverage = (float)(coverage / length);
            report.fullLoopFlattened |= coverage >= length - .0001;
            for (int i = 0; i < intervals.Count; i++)
            {
                Interval interval = intervals[i];
                interval.height = original.Evaluate(interval.a);
                interval.grade = report.fullLoopFlattened ? 0 :
                    (original.Evaluate(interval.b) - interval.height) / (interval.b - interval.a);
                if (report.fullLoopFlattened) continue;
                double prior = intervals[(i + intervals.Count - 1) % intervals.Count].b - (i == 0 ? length : 0);
                double next = intervals[(i + 1) % intervals.Count].a + (i == intervals.Count - 1 ? length : 0);
                double before = Math.Max(0, interval.a - prior), after = Math.Max(0, next - interval.b);
                // Separate joins cannot enter another hold, even when lap aliases
                // leave only a small free gap. A linear guard protects boundary
                // key slopes from the runtime profile's harmonic interpolation.
                interval.leftGuard = Math.Min(LinearGuard, before * .03);
                interval.rightGuard = Math.Min(LinearGuard, after * .03);
                interval.leftJoin = Math.Min(JoinWidth, before * .45);
                interval.rightJoin = Math.Min(JoinWidth, after * .45);
            }
            foreach (Interval interval in intervals)
            {
                double low = interval.a - interval.leftGuard, high = interval.b + interval.rightGuard;
                double lift = 0;
                int count = Math.Max(2, (int)Math.Ceiling((high - low) / .025));
                for (int j = 0; j <= count; j++)
                {
                    double s = low + (high - low) * j / count;
                    lift = Math.Max(lift, original.Evaluate(s) - interval.height - interval.grade * (s - interval.a));
                }
                // Preserve every authored local maximum, including a source key
                // that lands between clearance samples. The tiny reserve covers
                // float key quantisation without changing the intended grade.
                foreach (var key in source.keys)
                {
                    double s = key.distance + Math.Round(((low + high) * .5 - key.distance) / length) * length;
                    if (s >= low && s <= high)
                        lift = Math.Max(lift, key.height - interval.height - interval.grade * (s - interval.a));
                }
                if (lift > 0) lift += .0003;
                interval.height += lift;
                report.maxLift = Math.Max(report.maxLift, (float)lift);
            }
            var stations = new SortedSet<float> { 0, source.length };
            // Float heights separated by millimetre-spaced keys can quantise to
            // equal values, inventing zero secants in an otherwise linear Hold.
            // Protect all exact cap/guard/join anchors first. Only redundant
            // samples within the same linear chord may be conditioned below.
            if (minimumStationSeparation > 0)
                foreach (Interval i in intervals)
                    foreach (double s in new[] { i.a - i.leftGuard - i.leftJoin, i.a - i.leftGuard,
                        i.a - i.leftGuard * .5, i.a, i.b, i.b + i.rightGuard * .5,
                        i.b + i.rightGuard, i.b + i.rightGuard + i.rightJoin })
                        AddStation(stations, s, length);
            void AddAuthoredStation(double s)
            {
                if (minimumStationSeparation <= 0) { AddStation(stations, s, length); return; }
                float candidate = (float)Math.Round(Wrap(s, length), 4);
                foreach (float prior in stations.GetViewBetween(candidate - minimumStationSeparation, candidate + minimumStationSeparation))
                    foreach (Interval interval in intervals)
                    {
                        double a = candidate + Math.Round(((interval.a + interval.b) * .5 - candidate) / length) * length;
                        double b = prior + Math.Round(((interval.a + interval.b) * .5 - prior) / length) * length;
                        if (a >= interval.a - interval.leftGuard && a <= interval.b + interval.rightGuard &&
                            b >= interval.a - interval.leftGuard && b <= interval.b + interval.rightGuard) return;
                    }
                stations.Add(candidate);
            }
            for (double s = 0; s < length; s += KeySpacing) AddAuthoredStation(s);
            foreach (var key in source.keys) AddAuthoredStation(key.distance);
            foreach (Interval i in intervals)
                foreach (double s in new[] { i.a - i.leftGuard - i.leftJoin, i.a - i.leftGuard,
                    i.a - i.leftGuard * .5, i.a, i.b, i.b + i.rightGuard * .5,
                    i.b + i.rightGuard, i.b + i.rightGuard + i.rightJoin })
                    AddAuthoredStation(s);
            float HeightAt(double s)
            {
                double height = original.Evaluate(s);
                foreach (Interval interval in intervals)
                {
                    double d = s + Math.Round(((interval.a + interval.b) * .5 - s) / length) * length;
                    if (report.fullLoopFlattened) { height = interval.height; break; }
                    double left = interval.a - interval.leftGuard, right = interval.b + interval.rightGuard;
                    if (d < left - interval.leftJoin || d > right + interval.rightJoin) continue;
                    double line = interval.height + interval.grade * (d - interval.a);
                    double blend = d < left ? Smooth5((d - left + interval.leftJoin) / interval.leftJoin) :
                        d > right ? Smooth5((right + interval.rightJoin - d) / interval.rightJoin) : 1;
                    double difference = line - height;
                    double softness = .04 * 4 * blend * (1 - blend);
                    double positive = .5 * (difference + Math.Sqrt(difference * difference + softness * softness));
                    height += positive * blend;
                    break;
                }
                return (float)height;
            }
            IslandElevationProfile.Document document = null;
            IslandElevationProfile result = null;
            // Refine only where smooth profile interpolation could dip below the
            // original between keys. This preserves clearance without globally
            // inflating the height or storing millimetre-spaced keys everywhere.
            for (int pass = 0; pass <= 7; pass++)
            {
                var keys = new List<IslandElevationProfile.Key>(stations.Count);
                foreach (float s in stations)
                    if (s < source.length) keys.Add(new IslandElevationProfile.Key(s, HeightAt(s)));
                keys.Add(new IslandElevationProfile.Key(source.length, keys[0].height));
                document = new IslandElevationProfile.Document { length = source.length, closed = true, keys = keys.ToArray() };
                result = new IslandElevationProfile(document.length, true, document.keys);
                var extra = new List<double>();
                for (int i = 0; i + 1 < keys.Count; i++)
                    for (int quarter = 1; quarter <= 3; quarter++)
                    {
                        double s = keys[i].distance + ((double)keys[i + 1].distance - keys[i].distance) * quarter / 4;
                        if (result.Evaluate(s) < original.Evaluate(s) - .0001f) extra.Add(s);
                    }
                if (extra.Count == 0) break;
                int before = stations.Count;
                foreach (double s in extra) AddAuthoredStation(s);
                if (stations.Count == before) break;
            }
            report.minimumProfileDelta = float.MaxValue;
            void TrackGrade(double distance)
            {
                float grade = Math.Abs(result.Grade(distance));
                if (grade <= report.maxProfileGrade) return;
                report.maxProfileGrade = grade; report.maxProfileGradeDistance = (float)Wrap(distance, length);
                report.sourceGradeAtPeak = original.Grade(distance);
                report.heightLiftAtPeak = result.Evaluate(distance) - original.Evaluate(distance);
            }
            for (double s = 0; s < length; s += .025)
            {
                float delta = result.Evaluate(s) - original.Evaluate(s);
                TrackGrade(s);
                report.sourceMaxProfileGrade = Math.Max(report.sourceMaxProfileGrade, Math.Abs(original.Grade(s)));
                if (delta < report.minimumProfileDelta) { report.minimumProfileDelta = delta; report.minimumProfileDeltaDistance = (float)s; }
            }
            for (int i = 0; i + 1 < document.keys.Length; i++)
                for (int quarter = 0; quarter <= 4; quarter++)
                {
                    double s = document.keys[i].distance + ((double)document.keys[i + 1].distance - document.keys[i].distance) * quarter / 4;
                    TrackGrade(s);
                }
            report.maxProfileGradeRegion = "outside Hold joins";
            foreach (Interval interval in intervals)
            {
                double d = report.maxProfileGradeDistance + Math.Round(((interval.a + interval.b) * .5 - report.maxProfileGradeDistance) / length) * length;
                if (d < interval.a - interval.leftGuard - interval.leftJoin || d > interval.b + interval.rightGuard + interval.rightJoin) continue;
                string region = d < interval.a - interval.leftGuard ? "left C2 join" : d > interval.b + interval.rightGuard ? "right C2 join" : "linear Hold/guard";
                report.maxProfileGradeRegion = region + " at interval " + interval.a.ToString("R") + ".." + interval.b.ToString("R") +
                    ", chordGrade=" + interval.grade.ToString("R") + ", guards=" + interval.leftGuard.ToString("R") + "/" + interval.rightGuard.ToString("R") +
                    ", joins=" + interval.leftJoin.ToString("R") + "/" + interval.rightJoin.ToString("R");
                break;
            }
            foreach (Interval interval in intervals)
            {
                float minGrade = float.MaxValue, maxGrade = float.MinValue;
                int count = Math.Max(2, (int)Math.Ceiling((interval.b - interval.a) / .05));
                for (int j = 0; j <= count; j++)
                {
                    double s = interval.a + (interval.b - interval.a) * j / count;
                    float grade = result.Grade(s);
                    minGrade = Math.Min(minGrade, grade); maxGrade = Math.Max(maxGrade, grade);
                    report.maxHoldChordError = Math.Max(report.maxHoldChordError,
                        (float)Math.Abs(result.Evaluate(s) - interval.height - interval.grade * (s - interval.a)));
                    report.maxHoldGrade = Math.Max(report.maxHoldGrade, Math.Abs(grade));
                }
                report.maxHoldGradeVariation = Math.Max(report.maxHoldGradeVariation, maxGrade - minGrade);
            }
            if (report.maxHoldChordError > .0002f || report.maxHoldGradeVariation > .002f || report.minimumProfileDelta < -.001f)
                throw new InvalidDataException("The generated profile failed held-chord/clearance checks: error=" + report.maxHoldChordError +
                    ", gradeVariation=" + report.maxHoldGradeVariation + ", minimumDelta=" + report.minimumProfileDelta +
                    " at=" + report.minimumProfileDeltaDistance + ", maxGrade=" + report.maxProfileGrade + ".");
            return document;
        }

        internal static void PromoteMovingRounds(MapTimeline map, IslandRoute route,
            float startDistance, float distanceScale, Report report, float lateralScale = 1, bool requireUniformRounds = false)
        {
            ValidateCoordinates(startDistance, distanceScale);
            MapSegment[] nodes = map.segments.Where(s => s != null && s.kind != "Theme").OrderBy(s => s.audioTime).ToArray();
            var settings = new IslandDynamicSupportSpacing.Settings(startDistance, distanceScale, 35, 4, map.forwardSpeed, lateralScale);
            var opening = new HashSet<int>(nodes.Where(Round).Take(5).Select(s => s.sourceIndex));
            if (requireUniformRounds)
            {
                report.uniformRoundTiles = true;
                report.policy = "Merged periodic Hold/Follow chords; C2 joins outside holds; full-size .65m rounds with reachable lateral spacing repairs and only lifetime-safe native sine cycles; five stable opening supports.";
                // A source chart may already contain native moving runs. Keep
                // safe runs, but restore an unsafe one to its exact impact lane
                // before selecting further movers. Timing and native bodies stay intact.
                foreach (MapSegment node in nodes)
                {
                    MapElement e = map.elements[node.sourceIndex];
                    if (!Round(node) || e.updateType != 3 || !opening.Contains(node.sourceIndex) &&
                        IslandDynamicSupportSpacing.CanKeepRadius(route, settings, map.elements, node.sourceIndex, IslandSupportSpacing.NativeJumpRadius)) continue;
                    Vector3 at = e.pos; at.x = ChartRouteComposer.LandingX(e);
                    e.pos = at; e.updateType = 0; e.msLeft = 0; Sync(node, e);
                }
                RepairStationaryRoundSpacing(map, nodes, route, settings, opening, report);
            }
            var eligible = new List<int>();
            for (int i = 1; i + 1 < nodes.Length; i++)
            {
                MapSegment s = nodes[i]; MapElement e = map.elements[s.sourceIndex];
                if (requireUniformRounds && opening.Contains(s.sourceIndex) ||
                    !Round(nodes[i - 1]) || !Round(s) || !Round(nodes[i + 1]) || e.id != 0 && e.id != 1 ||
                    e.updateType != 0 && e.updateType != 3 || s.elementIndices == null || s.elementIndices.Length != 1 ||
                    s.elementIndices[0] != s.sourceIndex || s.audioTime < 4 || s.audioTime > nodes[nodes.Length - 1].audioTime - 3)
                    continue;
                eligible.Add(i);
            }
            report.eligibleRounds = eligible.Count;
            int before = map.elements.Count(e => e.updateType == 3);
            int count = eligible.Count(i => map.elements[nodes[i].sourceIndex].updateType == 3);
            int wanted = Math.Max(count, (int)Math.Round(eligible.Count * .4));
            foreach (int i in eligible.OrderBy(i => Hash(nodes[i].sourceIndex)))
            {
                if (count >= wanted) break;
                MapSegment segment = nodes[i]; MapElement e = map.elements[segment.sourceIndex];
                if (e.updateType == 3) continue;
                float previous = ExitX(map, nodes[i - 1]), next = ChartRouteComposer.LandingX(map.elements[nodes[i + 1].sourceIndex]);
                float incoming = ChartRouteComposer.MaxLateralShift(segment.audioTime - nodes[i - 1].endAudioTime);
                float outgoing = ChartRouteComposer.MaxLateralShift(nodes[i + 1].audioTime - segment.endAudioTime);
                float lo = Math.Max(-MovingLane, Math.Max(previous - incoming, next - outgoing));
                float hi = Math.Min(MovingLane, Math.Min(previous + incoming, next + outgoing));
                if (lo > hi) continue;
                uint hash = Hash(e.sourceIndex);
                float priorX = e.pos.x; int priorType = e.updateType, priorDirection = e.msLeft;
                float impact = Math.Max(lo, Math.Min(hi, ChartRouteComposer.LandingX(e) + ((hash >> 8) % 9 - 4f) * .04f));
                Vector3 position = e.pos;
                position.x = (float)(Math.Asin(impact) * 5 / (2 * Math.PI));
                e.pos = position; e.updateType = 3; e.msLeft = (int)(hash & 1);
                // The uniform stage accepts a mover only if its actual world
                // sweep fits two native bodies, including visibility/hit tails
                // and the spring reserve. Legacy stages retain their policy.
                if (requireUniformRounds ?
                    !IslandDynamicSupportSpacing.CanKeepRadius(route, settings, map.elements, segment.sourceIndex, IslandSupportSpacing.NativeJumpRadius) :
                    PairRadius(map, route, e, startDistance, distanceScale) < MinimumRadius)
                {
                    position.x = priorX; e.pos = position; e.updateType = priorType; e.msLeft = priorDirection;
                    continue;
                }
                report.maxLateralShift = Math.Max(report.maxLateralShift, Math.Abs(impact - priorX));
                Sync(segment, e); count++;
            }
            report.movingRounds = count;
            report.totalMovingRounds = map.elements.Count(e => e.updateType == 3);
            report.addedMovingRounds = report.totalMovingRounds - before;
            report.movingShare = eligible.Count == 0 ? 0 : (float)count / eligible.Count;
            report.reachable = true;
            for (int i = 1; i < nodes.Length; i++)
            {
                float delta = Math.Abs(ExitX(map, nodes[i - 1]) - ChartRouteComposer.LandingX(map.elements[nodes[i].sourceIndex]));
                float allowed = ChartRouteComposer.MaxLateralShift(nodes[i].audioTime - nodes[i - 1].endAudioTime);
                if (delta > allowed + .001f) report.reachable = false;
            }
            var preferred = new float[map.elements.Length]; var radii = new float[map.elements.Length];
            var limits = new IslandDynamicSupportSpacing.RadiusLimit[map.elements.Length];
            for (int i = 0; i < preferred.Length; i++) preferred[i] = IslandSupportSpacing.NativeJumpRadius;
            IslandDynamicSupportSpacing.Resolve(route,
                settings,
                map.elements, preferred, radii, limits);
            report.minimumRadius = IslandSupportSpacing.NativeJumpRadius;
            for (int i = 0; i < map.elements.Length; i++)
                if (map.elements[i].id == 0 || map.elements[i].id == 1 || map.elements[i].id == 4)
                    if (radii[i] < report.minimumRadius)
                    { report.minimumRadius = radii[i]; report.minimumRadiusIndex = i; report.minimumRadiusOtherIndex = limits[i].otherIndex; }
            report.noOverlaps = report.minimumRadius >= (requireUniformRounds ? IslandSupportSpacing.NativeJumpRadius : MinimumRadius);
            if (!report.reachable || !report.noOverlaps || !requireUniformRounds && eligible.Count > 0 && (report.movingShare < .35f || report.movingShare > .45f))
            {
                string pair = report.minimumRadiusIndex < 0 ? "none" :
                    report.minimumRadiusIndex + "(id=" + map.elements[report.minimumRadiusIndex].id + ",z=" + map.elements[report.minimumRadiusIndex].pos.z +
                    ",type=" + map.elements[report.minimumRadiusIndex].updateType + ")/" + report.minimumRadiusOtherIndex +
                    "(id=" + map.elements[report.minimumRadiusOtherIndex].id + ",z=" + map.elements[report.minimumRadiusOtherIndex].pos.z +
                    ",type=" + map.elements[report.minimumRadiusOtherIndex].updateType + ") distance=" + limits[report.minimumRadiusIndex].distance +
                    " followSweep=" + limits[report.minimumRadiusIndex].sweptFollow + " player=" + limits[report.minimumRadiusIndex].playerBegin + ".." + limits[report.minimumRadiusIndex].playerEnd;
                throw new InvalidDataException("The moving-round challenge failed: reachable=" + report.reachable + ", share=" + report.movingShare +
                    ", minimumRadius=" + report.minimumRadius + ", lateralScale=" + lateralScale + ", limitingPair=" + pair);
            }
        }

        private static void RepairStationaryRoundSpacing(MapTimeline map, MapSegment[] nodes, IslandRoute route,
            IslandDynamicSupportSpacing.Settings settings, HashSet<int> opening, Report report)
        {
            // A repaired target must fit every coexisting support. Later repairs
            // are checked against it too, so they cannot undo its clearance.
            for (int pass = 0; pass < nodes.Length; pass++)
            {
                int best = -1; float bestLane = 0, bestChange = float.PositiveInfinity;
                for (int i = 1; i + 1 < nodes.Length; i++)
                {
                    MapSegment node = nodes[i]; MapElement e = map.elements[node.sourceIndex];
                    if (!Round(node) || e.id != 0 && e.id != 1 || e.updateType != 0 || opening.Contains(node.sourceIndex) ||
                        IslandDynamicSupportSpacing.CanKeepRadius(route, settings, map.elements, node.sourceIndex, IslandSupportSpacing.NativeJumpRadius)) continue;
                    float incoming = ChartRouteComposer.MaxLateralShift(node.audioTime - nodes[i - 1].endAudioTime);
                    float outgoing = ChartRouteComposer.MaxLateralShift(nodes[i + 1].audioTime - node.endAudioTime);
                    float previous = ExitX(map, nodes[i - 1]), next = ChartRouteComposer.LandingX(map.elements[nodes[i + 1].sourceIndex]);
                    float lo = Math.Max(-IslandChartAuthoring.LaneLimit, Math.Max(previous - incoming, next - outgoing));
                    float hi = Math.Min(IslandChartAuthoring.LaneLimit, Math.Min(previous + incoming, next + outgoing));
                    if (lo > hi || !FindSafeStationaryLane(map, route, settings, node.sourceIndex, lo, hi, out float lane)) continue;
                    float change = Math.Abs(lane - e.pos.x);
                    if (change < bestChange) { best = i; bestLane = lane; bestChange = change; }
                }
                if (best < 0) return; // The final strict check rejects an infeasible subdivision.
                MapSegment selected = nodes[best]; MapElement target = map.elements[selected.sourceIndex];
                Vector3 at = target.pos; at.x = bestLane; target.pos = at; Sync(selected, target);
                report.stationaryLaneRepairs++;
                report.maxLateralShift = Math.Max(report.maxLateralShift, bestChange);
            }
        }

        private static bool FindSafeStationaryLane(MapTimeline map, IslandRoute route,
            IslandDynamicSupportSpacing.Settings settings, int index, float lo, float hi, out float lane)
        {
            const float searchStep = .0025f;
            MapElement target = map.elements[index]; Vector3 original = target.pos;
            lane = original.x;
            var candidates = new List<float> { lo, hi };
            int steps = (int)Math.Ceiling(Math.Max(Math.Abs(lo - original.x), Math.Abs(hi - original.x)) / searchStep);
            for (int i = 1; i <= steps; i++)
            {
                float left = original.x - i * searchStep, right = original.x + i * searchStep;
                if (left >= lo && left <= hi) candidates.Add(left);
                if (right >= lo && right <= hi) candidates.Add(right);
            }
            try
            {
                foreach (float candidate in candidates.OrderBy(x => Math.Abs(x - original.x)).ThenBy(x => x))
                {
                    Vector3 at = original; at.x = candidate; target.pos = at;
                    if (!IslandDynamicSupportSpacing.CanKeepRadius(route, settings, map.elements, index, IslandSupportSpacing.NativeJumpRadius)) continue;
                    // Refine the first sampled safe boundary, keeping the exact
                    // runtime motion gate as the authority at every step.
                    float safe = candidate, unsafeLane = original.x;
                    for (int iteration = 0; iteration < 18; iteration++)
                    {
                        float middle = (safe + unsafeLane) * .5f; at.x = middle; target.pos = at;
                        if (IslandDynamicSupportSpacing.CanKeepRadius(route, settings, map.elements, index, IslandSupportSpacing.NativeJumpRadius)) safe = middle;
                        else unsafeLane = middle;
                    }
                    lane = safe; return true;
                }
                return false;
            }
            finally { target.pos = original; }
        }

        private static float PairRadius(MapTimeline map, IslandRoute route, MapElement target, float start, float scale)
        {
            float radius = IslandSupportSpacing.NativeJumpRadius, window = 39 / scale;
            IslandRoute.Frame a = route.Evaluate(start + (double)target.pos.z * scale);
            foreach (MapElement other in map.elements)
            {
                if (other == target || other.id != 0 && other.id != 1 && other.id != 4 || Math.Abs(other.pos.z - target.pos.z) > window) continue;
                IslandRoute.Frame b = route.Evaluate(start + (double)other.pos.z * scale);
                // Common visibility window. Sampling one native sine period is
                // conservative even when the pair shares less time on screen.
                for (int phase = 0; phase <= 96; phase++)
                {
                    float actorZ = Math.Min(target.pos.z, other.pos.z) - 30 + phase * (30f / 96);
                    float x = target.updateType == 3 ? NativeRhythmRules.MoveCycleX(target.pos.x, target.pos.z - actorZ, target.msLeft != 0) : target.pos.x;
                    float y = other.updateType == 3 ? NativeRhythmRules.MoveCycleX(other.pos.x, other.pos.z - actorZ, other.msLeft != 0) : other.pos.x;
                    Vector3 pa = a.Position + a.Right * x + Vector3.up * target.pos.y;
                    Vector3 pb = b.Position + b.Right * y + Vector3.up * other.pos.y;
                    radius = Math.Min(radius, IslandSupportSpacing.LimitRadius(IslandSupportSpacing.NativeJumpRadius, Vector3.Distance(pa, pb)));
                }
            }
            return radius;
        }

        private static bool Round(MapSegment s) => s.kind == "Jump" || s.kind == "BigJump";
        private static float ExitX(MapTimeline map, MapSegment s) => ChartRouteComposer.LandingX(
            map.elements[s.kind == "Hold" ? s.sourceIndex : s.elementIndices[s.elementIndices.Length - 1]]);
        private static void Sync(MapSegment segment, MapElement e)
        {
            segment.x = e.pos.x;
            if (segment.motion == null) segment.motion = new MotionParameters();
            segment.motion.updateType = e.updateType; segment.motion.msLeft = e.msLeft;
        }
        private static uint Hash(int value)
        {
            unchecked
            {
                uint x = (uint)value + 0x9e3779b9u;
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; return x ^ x >> 16;
            }
        }
        private static double Wrap(double value, double length) { value %= length; return value < 0 ? value + length : value; }
        private static string Digest(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        private static double Smooth5(double t) { t = Math.Max(0, Math.Min(1, t)); return t * t * t * (10 + t * (-15 + 6 * t)); }
        private static void AddStation(SortedSet<float> stations, double s, double length)
            => stations.Add((float)Math.Round(Wrap(s, length), 4));
        private static void ValidateCoordinates(float start, float scale)
        {
            if (float.IsNaN(start) || float.IsInfinity(start) || float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0)
                throw new ArgumentException("A finite start and positive distance scale are required.");
        }
    }
}
