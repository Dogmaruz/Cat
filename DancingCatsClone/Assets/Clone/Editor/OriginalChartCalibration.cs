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
        }
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
        public static void RunChecks()
        {
            AudioChartGeneratorValidation.RunChecks();
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
                // Call the pure PCM API directly: do not consult a score, original cues or source identity.
                var generated = AudioChartGenerator.GenerateFromSamples(pcm, clip.frequency, clip.channels,
                    "PcmEvaluation", new AudioChartGenerator.Options
                    {
                        catalogId = 29001, title = "PCM evaluation", artist = "Offline analysis",
                        forwardSpeed = reference.forwardSpeed, delayTime = reference.delayTime
                    });
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
                Directory.CreateDirectory(Output);
                string reportPath = Path.Combine(Output, stage + ".json");
                if (stage == "baseline" && File.Exists(reportPath))
                    throw new InvalidOperationException("The existing baseline is immutable; archive it explicitly before repeating baseline capture.");
                File.WriteAllText(Path.Combine(Output, stage + ".generated.timeline.json"), JsonUtility.ToJson(generated, true));
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Debug.Log("ORIGINAL_CHART_CALIBRATION " + stage + ": F1@40=" + report.all.within40ms.f1.ToString("F4", CultureInfo.InvariantCulture)
                    + ", F1@80=" + report.all.within80ms.f1.ToString("F4", CultureInfo.InvariantCulture)
                    + ", held-out F1@80=" + report.heldOut.within80ms.f1.ToString("F4", CultureInfo.InvariantCulture)
                    + "; reference unchanged. Report: " + reportPath);
            }
            finally
            {
                if (Hash(referencePath) != originalHash || Hash(audioPath) != audioHash)
                    throw new InvalidOperationException("Calibration must not alter the original map or source audio.");
            }
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
