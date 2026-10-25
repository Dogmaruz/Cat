using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Read-only evaluation of the PCM route. A reference chart is never a generator input.</summary>
    public static class OriginalChartCalibration
    {
        private const string ReferenceId = "UnstoppableCat054";
        private static readonly string Output = Path.GetFullPath("Library/OriginalChartCalibration");
        [Serializable] private sealed class Report
        {
            public string stage, referenceId, referenceMapSha256, sourceAudioSha256, generator;
            public double duration, heldOutStart;
            public int referenceElements, generatedElements, referenceEvents, generatedEvents;
            public Evaluation all, calibration, heldOut;
            public Evaluation logicalSupports, logicalHeldOut;
            public Evaluation baselineLogicalSupports, baselineLogicalHeldOut;
            public TileTypeMatch[] tileTypes;
            public int matchedHeldNotes;
            public double matchedHoldDurationErrorMs;
            public string[] referenceTypes, generatedTypes;
            public double referenceHeldSeconds, generatedHeldSeconds;
            public string evaluationScope = "Repeated within-song calibration; the last third is not an independent or blind generalization test.";
            public SustainedMetric[] sustainedSupports, previousV3SustainedSupports;
            public SupportDetail[] supportDetails;
            public PredictedSupport[] falsePositiveSupports;
            public FollowShapeMatch[] followPaths;
        }
        [Serializable] private sealed class SustainedMetric
        {
            public string kind;
            public Match onset80ms, boundaries150ms;
            public double meanMatchedStartErrorMs, meanMatchedEndErrorMs, meanMatchedDurationErrorMs;
            public double meanMatchedIoU, referenceCoverageIoU;
        }
        [Serializable] private sealed class SupportDetail
        {
            public int referenceIndex, generatedIndex = -1;
            public string referenceKind, generatedKind, status;
            public double referenceStart, referenceEnd, generatedStart, generatedEnd;
            public bool onsetMatched, typeMatched, boundariesMatched;
            public double startErrorMs, endErrorMs, durationErrorMs, intersectionOverUnion;
        }
        [Serializable] private sealed class PredictedSupport
        {
            public int generatedIndex;
            public string kind, status, nearestReferenceKind;
            public double start, end, nearestReferenceStart, nearestReferenceStartErrorMs;
        }
        [Serializable] private sealed class FollowShapeMatch
        {
            public int referenceIndex, generatedIndex, samples;
            public double meanLaneError, translationInvariantMeanLaneError, directionAgreement;
            public double referenceExcursion, generatedExcursion, startErrorMs, endErrorMs;
            public bool terminalReleaseResetExcluded;
        }
        private struct Pair { public int reference, generated; }
        private struct PathSample { public double time, x; }
        [Serializable] private sealed class TileTypeMatch
        {
            public string kind;
            public Match onset80ms;
        }
        [Serializable] private sealed class Evaluation
        {
            public Match within40ms, within80ms;
        }
        [Serializable] private sealed class Match
        {
            public int referenceCount, generatedCount, matched;
            public double precision, recall, f1, meanAbsoluteErrorMs;
        }

        public static void RunBaseline() => Run("baseline");
        public static void ExportPcmForAnalysis() => VocalPcmExport.Export(RecoveredCatalog.LoadOriginal().Single(song => song.id == ReferenceId).LoadAudio());
        public static void RunChecks()
        {
            CheckMetricFixtures();
            AudioChartGeneratorValidation.RunChecks();
            PcmPhraseAnalysisValidation.RunChecks();
            PcmFlightAnalysisValidation.RunChecks();
            PcmSparsePhraseAnalysisValidation.RunChecks();
            PcmVocalIntegrationValidation.RunChecks();
            Run("candidate");
        }

        public static void GenerateUnstoppableComparison()
        {
            const string id = "UnstoppableGeneratorTest";
            var originals = RecoveredCatalog.LoadOriginal();
            var record = originals.Single(song => song.id == ReferenceId);
            string referencePath = Path.GetFullPath("Assets/Resources/" + record.timelineResource + ".json");
            string hash = Hash(referencePath);
            string customPath = "Assets/Resources/CustomData/songs.json";
            var customs = File.Exists(customPath) ? RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(customPath)) : Array.Empty<SongRecord>();
            var existing = customs.FirstOrDefault(song => song.id == id);
            int catalogId;
            try { catalogId = AudioChartGenerator.ResolveCatalogId(id, originals, customs, existing != null ? existing.catalogId : 29001); }
            catch (InvalidDataException) { catalogId = AudioChartGenerator.ResolveCatalogId(id, originals, customs, 0); }
            var options = new AudioChartGenerator.Options { catalogId = catalogId, title = "Unstoppable · тест генератора",
                artist = record.artist, forwardSpeed = record.forwardSpeed, delayTime = record.delayTime };
            if (File.Exists("Assets/Clone/Editor/MusicalScores/" + id + ".score.json"))
                throw new InvalidOperationException("The comparison must use independent PCM analysis, not an authored score.");
            var clip = record.LoadAudio();
            if (clip == null) throw new FileNotFoundException("The original comparison recording is missing.");
            string audioPath = UnityEditor.AssetDatabase.GetAssetPath(clip), audioHash = Hash(audioPath);
            try { AudioChartGenerator.Save(AudioChartGenerator.Generate(clip, id, options), clip, options); }
            finally
            {
                if (Hash(referencePath) != hash || Hash(audioPath) != audioHash)
                    throw new InvalidOperationException("Comparison publishing must preserve the original map and music.");
            }
        }

        private static void Run(string stage)
        {
            if (stage == "candidate") ArchivePreviousCandidate();
            var record = RecoveredCatalog.LoadOriginal().Single(song => song.id == ReferenceId);
            string referencePath = Path.GetFullPath("Assets/Resources/" + record.timelineResource + ".json");
            string originalHash = Hash(referencePath);
            var reference = JsonUtility.FromJson<MapTimeline>(File.ReadAllText(referencePath));
            var clip = record.LoadAudio();
            if (clip == null) throw new FileNotFoundException("Calibration AudioClip is missing: " + record.audioResource);
            string audioPath = UnityEditor.AssetDatabase.GetAssetPath(clip);
            string audioHash = Hash(audioPath);
            try
            {
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                var pcm = new float[checked(clip.samples * clip.channels)];
                if (clip.loadState != AudioDataLoadState.Loaded || !clip.GetData(pcm, 0))
                    throw new InvalidDataException("The real imported calibration PCM is not ready.");
                // Optional vocal features are generated from this audio only, validated
                // against its decoded PCM. No score or reference cues enter generation.
                var generated = AudioChartGenerator.GenerateFromSamples(pcm, clip.frequency, clip.channels,
                    "PcmEvaluation", new AudioChartGenerator.Options
                    {
                        catalogId = 29001, title = "PCM evaluation", artist = "Offline analysis",
                        forwardSpeed = reference.forwardSpeed, delayTime = reference.delayTime
                    }, PcmVocalAnalysis.LoadForClip(clip));
                double duration = (double)clip.samples / clip.frequency;
                double split = duration * 2 / 3;
                var report = new Report
                {
                    stage = stage, referenceId = ReferenceId, referenceMapSha256 = originalHash,
                    sourceAudioSha256 = audioHash, duration = duration, heldOutStart = split,
                    generator = generated.sourceMapBundle,
                    referenceElements = reference.elements.Length, generatedElements = generated.elements.Length,
                    referenceEvents = reference.events.Length, generatedEvents = generated.events.Length,
                    all = Evaluate(reference, generated, 0, duration),
                    calibration = Evaluate(reference, generated, 0, split),
                    heldOut = Evaluate(reference, generated, split, duration),
                    logicalSupports = EvaluateLogical(reference, generated, 0, duration),
                    logicalHeldOut = EvaluateLogical(reference, generated, split, duration),
                    referenceTypes = Counts(reference), generatedTypes = Counts(generated),
                    referenceHeldSeconds = reference.segments.Where(IsHeld).Sum(segment => segment.duration),
                    generatedHeldSeconds = generated.segments.Where(IsHeld).Sum(segment => segment.duration)
                };
                string baselineMapPath = Path.Combine(Output, "baseline.generated.timeline.json");
                if (File.Exists(baselineMapPath))
                {
                    var baselineMap = JsonUtility.FromJson<MapTimeline>(File.ReadAllText(baselineMapPath));
                    report.baselineLogicalSupports = EvaluateLogical(reference, baselineMap, 0, duration);
                    report.baselineLogicalHeldOut = EvaluateLogical(reference, baselineMap, split, duration);
                }
                report.tileTypes = new[] { "Jump", "BigJump", "Hold", "Follow", "Theme" }.Select(kind => new TileTypeMatch
                {
                    kind = kind, onset80ms = Score(reference.segments.Where(segment => segment.kind == kind).Select(segment => segment.audioTime).ToArray(),
                        generated.segments.Where(segment => segment.kind == kind).Select(segment => segment.audioTime).ToArray(), .08)
                }).ToArray();
                var generatedHolds = generated.segments.Where(segment => segment.kind == "Hold").ToList();
                double durationErrors = 0;
                foreach (var originalHold in reference.segments.Where(segment => segment.kind == "Hold"))
                {
                    var match = generatedHolds.Where(segment => Math.Abs(segment.audioTime - originalHold.audioTime) <= .08)
                        .OrderBy(segment => Math.Abs(segment.audioTime - originalHold.audioTime)).FirstOrDefault();
                    if (match == null) continue;
                    generatedHolds.Remove(match); report.matchedHeldNotes++;
                    durationErrors += Math.Abs(match.duration - originalHold.duration) * 1000;
                }
                report.matchedHoldDurationErrorMs = report.matchedHeldNotes == 0 ? 0 : durationErrors / report.matchedHeldNotes;
                report.sustainedSupports = SustainedMetrics(reference, generated);
                report.supportDetails = SupportDetails(reference, generated);
                report.falsePositiveSupports = FalsePositiveSupports(reference, generated);
                report.followPaths = FollowPaths(reference, generated);
                string previousV3Map = Path.Combine(Output, "v4/pre-v4.generated.timeline.json");
                if (File.Exists(previousV3Map))
                    report.previousV3SustainedSupports = SustainedMetrics(reference, JsonUtility.FromJson<MapTimeline>(File.ReadAllText(previousV3Map)));
                Directory.CreateDirectory(Output);
                string reportPath = Path.Combine(Output, stage + ".json");
                if (stage == "baseline" && File.Exists(reportPath))
                    throw new InvalidOperationException("The existing baseline is immutable; archive it explicitly before repeating baseline capture.");
                File.WriteAllText(Path.Combine(Output, stage + ".generated.timeline.json"), JsonUtility.ToJson(generated, true));
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                WriteSupportCsv(Path.Combine(Output, stage + ".supports.csv"), report);
                Debug.Log("ORIGINAL_CHART_CALIBRATION " + stage + ": F1@40=" + report.all.within40ms.f1.ToString("F4", CultureInfo.InvariantCulture)
                    + ", F1@80=" + report.all.within80ms.f1.ToString("F4", CultureInfo.InvariantCulture)
                    + ", final-third F1@80=" + report.heldOut.within80ms.f1.ToString("F4", CultureInfo.InvariantCulture)
                    + "; reference unchanged. Report: " + reportPath);
            }
            finally
            {
                if (Hash(referencePath) != originalHash || Hash(audioPath) != audioHash)
                    throw new InvalidOperationException("Calibration must not alter the original map or source audio.");
            }
        }

        private static void ArchivePreviousCandidate()
        {
            string folder = Path.Combine(Output, "v4");
            string previousReport = Path.Combine(folder, "pre-v4.json"), previousMap = Path.Combine(folder, "pre-v4.generated.timeline.json");
            if (File.Exists(previousReport) && File.Exists(previousMap)) return;
            string report = Path.Combine(Output, "candidate.json"), map = Path.Combine(Output, "candidate.generated.timeline.json");
            if (!File.Exists(report) || !File.Exists(map)) return;
            Directory.CreateDirectory(folder);
            // Preserve the previous measured output before any current-generation result is written.
            // The v2 baseline has a separate lifetime and is never copied over or regenerated here.
            if (!File.Exists(previousReport)) File.Copy(report, previousReport);
            if (!File.Exists(previousMap)) File.Copy(map, previousMap);
        }

        private static MapSegment[] Supports(MapTimeline map, string kind = null) => (map.segments ?? Array.Empty<MapSegment>())
            .Where(segment => segment != null && (kind == null ? IsHeld(segment) : segment.kind == kind))
            .OrderBy(segment => segment.audioTime).ToArray();
        private static MapSegment[] Primary(MapTimeline map) => (map.segments ?? Array.Empty<MapSegment>())
            .Where(segment => segment != null && (segment.kind == "Jump" || segment.kind == "BigJump"
                || segment.kind == "Theme" || IsHeld(segment))).OrderBy(segment => segment.audioTime).ToArray();
        private static double End(MapSegment segment) => segment.endAudioTime;
        private static double IoU(MapSegment reference, MapSegment generated)
        {
            double intersection = Math.Max(0, Math.Min(End(reference), End(generated)) - Math.Max(reference.audioTime, generated.audioTime));
            double union = Math.Max(End(reference), End(generated)) - Math.Min(reference.audioTime, generated.audioTime);
            return union > 0 ? intersection / union : 0;
        }
        private static bool OnsetNear(MapSegment reference, MapSegment generated) => Math.Abs(reference.audioTime - generated.audioTime) <= .08;
        private static bool BoundariesNear(MapSegment reference, MapSegment generated) => Math.Abs(reference.audioTime - generated.audioTime) <= .15
            && Math.Abs(End(reference) - End(generated)) <= .15 && IoU(reference, generated) >= .5;

        private static SustainedMetric[] SustainedMetrics(MapTimeline reference, MapTimeline generated)
        {
            return new[] { "Hold", "Follow" }.Select(kind =>
            {
                var expected = Supports(reference, kind); var actual = Supports(generated, kind);
                var onsetPairs = PairSegments(expected, actual, OnsetNear);
                var boundaryPairs = PairSegments(expected, actual, BoundariesNear);
                double count = onsetPairs.Length;
                return new SustainedMetric
                {
                    kind = kind, onset80ms = PairScore(expected, actual, onsetPairs), boundaries150ms = PairScore(expected, actual, boundaryPairs),
                    meanMatchedStartErrorMs = count == 0 ? 0 : onsetPairs.Sum(pair => Math.Abs(expected[pair.reference].audioTime - actual[pair.generated].audioTime)) * 1000 / count,
                    meanMatchedEndErrorMs = count == 0 ? 0 : onsetPairs.Sum(pair => Math.Abs(End(expected[pair.reference]) - End(actual[pair.generated]))) * 1000 / count,
                    meanMatchedDurationErrorMs = count == 0 ? 0 : onsetPairs.Sum(pair => Math.Abs(expected[pair.reference].duration - actual[pair.generated].duration)) * 1000 / count,
                    meanMatchedIoU = count == 0 ? 0 : onsetPairs.Sum(pair => IoU(expected[pair.reference], actual[pair.generated])) / count,
                    referenceCoverageIoU = expected.Length == 0 ? 0 : onsetPairs.Sum(pair => IoU(expected[pair.reference], actual[pair.generated])) / expected.Length
                };
            }).ToArray();
        }

        private static SupportDetail[] SupportDetails(MapTimeline reference, MapTimeline generated)
        {
            var expected = Primary(reference); var actual = Primary(generated);
            var pairs = PairSegments(expected, actual, OnsetNear);
            var details = new List<SupportDetail>();
            for (int index = 0; index < expected.Length; index++)
            {
                var support = expected[index]; if (!IsHeld(support)) continue;
                var pair = pairs.Where(value => value.reference == index).ToArray();
                var detail = new SupportDetail { referenceIndex = support.sourceIndex, referenceKind = support.kind,
                    referenceStart = support.audioTime, referenceEnd = End(support), status = "missed onset", generatedKind = "none" };
                if (pair.Length > 0)
                {
                    var found = actual[pair[0].generated]; detail.generatedIndex = found.sourceIndex; detail.generatedKind = found.kind;
                    detail.generatedStart = found.audioTime; detail.generatedEnd = End(found); detail.onsetMatched = true;
                    detail.typeMatched = support.kind == found.kind; detail.boundariesMatched = detail.typeMatched && BoundariesNear(support, found);
                    detail.startErrorMs = (found.audioTime - support.audioTime) * 1000; detail.endErrorMs = (End(found) - End(support)) * 1000;
                    detail.durationErrorMs = (found.duration - support.duration) * 1000; detail.intersectionOverUnion = IoU(support, found);
                    detail.status = !detail.typeMatched ? "wrong type" : detail.boundariesMatched ? "matched support" : "boundary mismatch";
                }
                details.Add(detail);
            }
            return details.ToArray();
        }

        private static PredictedSupport[] FalsePositiveSupports(MapTimeline reference, MapTimeline generated)
        {
            var result = new List<PredictedSupport>(); var primary = Primary(reference);
            foreach (string kind in new[] { "Hold", "Follow" })
            {
                var expected = Supports(reference, kind); var actual = Supports(generated, kind);
                var valid = new HashSet<int>(PairSegments(expected, actual, BoundariesNear).Select(pair => pair.generated));
                for (int index = 0; index < actual.Length; index++)
                {
                    if (valid.Contains(index)) continue;
                    var support = actual[index];
                    var nearest = primary.OrderBy(segment => Math.Abs(segment.audioTime - support.audioTime)).FirstOrDefault();
                    bool sameTypeOnset = expected.Any(segment => OnsetNear(segment, support));
                    var containing = Supports(reference).FirstOrDefault(segment => support.audioTime > segment.audioTime && support.audioTime < End(segment));
                    result.Add(new PredictedSupport { generatedIndex = support.sourceIndex, kind = support.kind, start = support.audioTime, end = End(support),
                        status = sameTypeOnset ? "boundary mismatch" : nearest != null && OnsetNear(nearest, support) ? "wrong type"
                            : containing != null ? "extra onset inside " + containing.kind : "extra onset",
                        nearestReferenceKind = nearest?.kind ?? "none", nearestReferenceStart = nearest?.audioTime ?? 0,
                        nearestReferenceStartErrorMs = nearest == null ? 0 : (support.audioTime - nearest.audioTime) * 1000 });
                }
            }
            return result.OrderBy(support => support.start).ToArray();
        }

        private static FollowShapeMatch[] FollowPaths(MapTimeline reference, MapTimeline generated)
        {
            var expected = Supports(reference, "Follow"); var actual = Supports(generated, "Follow");
            return PairSegments(expected, actual, OnsetNear).Select(pair =>
            {
                var source = expected[pair.reference]; var target = actual[pair.generated];
                var referencePath = SemanticPath(source, out bool sourceReset); var generatedPath = SemanticPath(target, out bool targetReset);
                const int samples = 33; var a = new double[samples]; var b = new double[samples];
                for (int index = 0; index < samples; index++)
                { double phase = index / (double)(samples - 1); a[index] = PathAt(referencePath, phase); b[index] = PathAt(generatedPath, phase); }
                double aMean = a.Average(), bMean = b.Average(); int moving = 0, agreeing = 0;
                for (int index = 1; index < samples; index++)
                {
                    double change = a[index] - a[index - 1]; if (Math.Abs(change) < .03) continue;
                    moving++; if (change * (b[index] - b[index - 1]) > 0) agreeing++;
                }
                return new FollowShapeMatch { referenceIndex = source.sourceIndex, generatedIndex = target.sourceIndex, samples = samples,
                    meanLaneError = a.Zip(b, (x, y) => Math.Abs(x - y)).Average(),
                    translationInvariantMeanLaneError = a.Zip(b, (x, y) => Math.Abs((x - aMean) - (y - bMean))).Average(),
                    directionAgreement = moving == 0 ? 0 : agreeing / (double)moving,
                    referenceExcursion = a.Max() - a.Min(), generatedExcursion = b.Max() - b.Min(),
                    startErrorMs = (target.audioTime - source.audioTime) * 1000, endErrorMs = (End(target) - End(source)) * 1000,
                    terminalReleaseResetExcluded = sourceReset || targetReset };
            }).ToArray();
        }

        private static PathSample[] SemanticPath(MapSegment segment, out bool excludedReset)
        {
            var points = new List<PathSample> { new PathSample { time = 0, x = segment.x } }; excludedReset = false;
            foreach (var point in (segment.path ?? Array.Empty<ChartPoint>()).Where(point => point != null).OrderBy(point => point.audioTime))
            {
                double time = point.audioTime - segment.audioTime; if (time < 0 || time > segment.duration + .001) continue;
                var previous = points[points.Count - 1];
                double phase = Math.Min(1, time / Math.Max(.000001, segment.duration));
                // Native release markers may reset X just 1-5 ms after the last scoring midpoint.
                // That reset does not describe a traversable musical contour.
                if (point.kind == "FollowEnd" && (phase - previous.time) * segment.duration < .02 && Math.Abs(point.x - previous.x) > .2)
                { excludedReset = true; continue; }
                var sample = new PathSample { time = phase, x = point.x };
                if (Math.Abs(sample.time - previous.time) < .000001) points[points.Count - 1] = sample; else points.Add(sample);
            }
            if (points[points.Count - 1].time < 1) points.Add(new PathSample { time = 1, x = points[points.Count - 1].x });
            return points.ToArray();
        }

        private static double PathAt(PathSample[] points, double time)
        {
            for (int index = 1; index < points.Length; index++)
                if (time <= points[index].time)
                {
                    var a = points[index - 1]; var b = points[index];
                    double fraction = (time - a.time) / Math.Max(.000001, b.time - a.time);
                    return a.x + Math.Max(0, Math.Min(1, fraction)) * (b.x - a.x);
                }
            return points[points.Length - 1].x;
        }

        private static Pair[] PairSegments(MapSegment[] reference, MapSegment[] generated, Func<MapSegment, MapSegment, bool> eligible)
        {
            int columns = generated.Length + 1, length = (reference.Length + 1) * columns;
            var counts = new int[length]; var errors = new double[length]; var direction = new byte[length];
            for (int i = 1; i <= reference.Length; i++) for (int j = 1; j <= generated.Length; j++)
            {
                int at = i * columns + j, up = at - columns, left = at - 1, diagonal = up - 1;
                bool takeUp = counts[up] > counts[left] || counts[up] == counts[left] && errors[up] <= errors[left];
                int best = takeUp ? up : left; counts[at] = counts[best]; errors[at] = errors[best]; direction[at] = takeUp ? (byte)1 : (byte)2;
                double error = Math.Abs(reference[i - 1].audioTime - generated[j - 1].audioTime);
                if (eligible(reference[i - 1], generated[j - 1]) && (counts[diagonal] + 1 > counts[at]
                    || counts[diagonal] + 1 == counts[at] && errors[diagonal] + error < errors[at]))
                { counts[at] = counts[diagonal] + 1; errors[at] = errors[diagonal] + error; direction[at] = 3; }
            }
            var pairs = new List<Pair>(); int row = reference.Length, column = generated.Length;
            while (row > 0 && column > 0)
            {
                byte choice = direction[row * columns + column];
                if (choice == 3) { pairs.Add(new Pair { reference = --row, generated = --column }); }
                else if (choice == 1) row--; else column--;
            }
            pairs.Reverse(); return pairs.ToArray();
        }

        private static Match PairScore(MapSegment[] reference, MapSegment[] generated, Pair[] pairs) => new Match
        {
            referenceCount = reference.Length, generatedCount = generated.Length, matched = pairs.Length,
            precision = generated.Length == 0 ? 0 : pairs.Length / (double)generated.Length,
            recall = reference.Length == 0 ? 0 : pairs.Length / (double)reference.Length,
            f1 = reference.Length + generated.Length == 0 ? 0 : 2.0 * pairs.Length / (reference.Length + generated.Length),
            meanAbsoluteErrorMs = pairs.Length == 0 ? 0 : pairs.Sum(pair => Math.Abs(reference[pair.reference].audioTime - generated[pair.generated].audioTime)) * 1000 / pairs.Length
        };

        private static void WriteSupportCsv(string path, Report report)
        {
            Func<double, string> number = value => value.ToString("R", CultureInfo.InvariantCulture);
            var lines = new List<string> { "status,reference_kind,generated_kind,reference_index,generated_index,reference_start,reference_end,generated_start,generated_end,start_error_ms,end_error_ms,duration_error_ms,iou" };
            foreach (var detail in report.supportDetails)
                lines.Add(string.Join(",", new[] { detail.status, detail.referenceKind, detail.generatedKind, detail.referenceIndex.ToString(CultureInfo.InvariantCulture),
                    detail.generatedIndex.ToString(CultureInfo.InvariantCulture), number(detail.referenceStart), number(detail.referenceEnd), number(detail.generatedStart),
                    number(detail.generatedEnd), number(detail.startErrorMs), number(detail.endErrorMs), number(detail.durationErrorMs), number(detail.intersectionOverUnion) }));
            foreach (var support in report.falsePositiveSupports)
                lines.Add(string.Join(",", new[] { support.status, support.nearestReferenceKind, support.kind, "-1", support.generatedIndex.ToString(CultureInfo.InvariantCulture),
                    number(support.nearestReferenceStart), "", number(support.start), number(support.end), number(support.nearestReferenceStartErrorMs), "", "", "" }));
            File.WriteAllLines(path, lines);
        }

        private static void CheckMetricFixtures()
        {
            var reference = new[] { new MapSegment { kind = "Hold", audioTime = 1, endAudioTime = 2, duration = 1 } };
            var generated = new[] { new MapSegment { kind = "Hold", audioTime = 1.04, endAudioTime = 2.03, duration = .99 },
                new MapSegment { kind = "Hold", audioTime = 1.06, endAudioTime = 1.9, duration = .84 } };
            var pairs = PairSegments(reference, generated, OnsetNear);
            var metric = PairScore(reference, generated, pairs);
            if (pairs.Length != 1 || pairs[0].generated != 0 || metric.precision != .5 || metric.recall != 1 || IoU(reference[0], generated[0]) < .93)
                throw new InvalidOperationException("Support metric fixture failed: duplicate predictions must not earn duplicate credit.");
            generated[0].endAudioTime = 3;
            if (BoundariesNear(reference[0], generated[0]))
                throw new InvalidOperationException("Support metric fixture failed: an onset match must not hide a wrong support length.");
            var sourcePath = new MapSegment { kind = "Follow", sourceIndex = 1, audioTime = 1, endAudioTime = 2.4, duration = 1.4, x = -1,
                path = new[] { new ChartPoint { kind = "FollowMids", audioTime = 2.399, x = 1 }, new ChartPoint { kind = "FollowEnd", audioTime = 2.4, x = 0 } } };
            var mirroredPath = new MapSegment { kind = "Follow", sourceIndex = 2, audioTime = 1, endAudioTime = 2.4, duration = 1.4, x = 1,
                path = new[] { new ChartPoint { kind = "FollowMid", audioTime = 2.399, x = -1 }, new ChartPoint { kind = "FollowEnd", audioTime = 2.4, x = 0 } } };
            var shape = FollowPaths(new MapTimeline { segments = new[] { sourcePath } }, new MapTimeline { segments = new[] { mirroredPath } }).Single();
            if (!shape.terminalReleaseResetExcluded || shape.directionAgreement != 0 || shape.translationInvariantMeanLaneError < .9)
                throw new InvalidOperationException("Follow metric fixture failed: a release reset is not a path segment, and mirrored motion must not receive matching-shape credit.");
        }

        private static bool IsHeld(MapSegment segment) => segment.kind == "Hold" || segment.kind == "Follow";
        private static string[] Counts(MapTimeline map) => map.elements.GroupBy(element => element.kind)
            .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => group.Key + ": " + group.Count()).ToArray();
        private static Evaluation Evaluate(MapTimeline reference, MapTimeline generated, double start, double end)
        {
            double[] target = reference.events.Select(item => item.audioTime).Where(time => time >= start && time < end).OrderBy(time => time).ToArray();
            double[] actual = generated.events.Select(item => item.audioTime).Where(time => time >= start && time < end).OrderBy(time => time).ToArray();
            return new Evaluation { within40ms = Score(target, actual, .04), within80ms = Score(target, actual, .08) };
        }
        private static Evaluation EvaluateLogical(MapTimeline reference, MapTimeline generated, double start, double end)
        {
            Func<MapSegment, bool> playable = segment => segment.kind == "Jump" || segment.kind == "BigJump"
                || segment.kind == "Hold" || segment.kind == "Follow" || segment.kind == "Theme";
            double[] target = reference.segments.Where(playable).Select(item => item.audioTime).Where(time => time >= start && time < end).OrderBy(time => time).ToArray();
            double[] actual = generated.segments.Where(playable).Select(item => item.audioTime).Where(time => time >= start && time < end).OrderBy(time => time).ToArray();
            return new Evaluation { within40ms = Score(target, actual, .04), within80ms = Score(target, actual, .08) };
        }
        private static Match Score(double[] target, double[] actual, double tolerance)
        {
            // Monotonic sequence matching maximizes unique matches, then minimizes their timing error.
            int columns = actual.Length + 1;
            var counts = new int[(target.Length + 1) * columns];
            var errors = new double[counts.Length];
            for (int i = 1; i <= target.Length; i++)
            for (int j = 1; j <= actual.Length; j++)
            {
                int at = i * columns + j, up = at - columns, left = at - 1;
                int best = counts[up] > counts[left] || counts[up] == counts[left] && errors[up] <= errors[left] ? up : left;
                counts[at] = counts[best]; errors[at] = errors[best];
                double error = Math.Abs(target[i - 1] - actual[j - 1]);
                int diagonal = at - columns - 1;
                if (error <= tolerance && (counts[diagonal] + 1 > counts[at]
                    || counts[diagonal] + 1 == counts[at] && errors[diagonal] + error < errors[at]))
                { counts[at] = counts[diagonal] + 1; errors[at] = errors[diagonal] + error; }
            }
            int last = counts.Length - 1, matched = counts[last];
            return new Match
            {
                referenceCount = target.Length, generatedCount = actual.Length, matched = matched,
                precision = actual.Length == 0 ? 0 : matched / (double)actual.Length,
                recall = target.Length == 0 ? 0 : matched / (double)target.Length,
                f1 = target.Length + actual.Length == 0 ? 0 : 2.0 * matched / (target.Length + actual.Length),
                meanAbsoluteErrorMs = matched == 0 ? 0 : errors[last] * 1000 / matched
            };
        }
        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
