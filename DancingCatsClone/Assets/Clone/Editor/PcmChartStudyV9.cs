using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Paired experiments on new recordings. References are evaluation inputs only; no playable assets are written here.</summary>
    public static class PcmChartStudyV9
    {
        private const float Preroll = .6f;
        private static readonly string Output = Path.GetFullPath("Library/ChartGenerationV9/benchmark");
        private sealed class Target
        {
            public readonly string id, mapHash, audioHash;
            public Target(string id, string mapHash, string audioHash)
            { this.id = id; this.mapHash = mapHash; this.audioHash = audioHash; }
        }
        private static readonly Target[] Calibration =
        {
            new Target("DisconnectPop", "2fdf744d7ad60b2df044a803cd5ef3877f6615f4b2c2938864864822f9f11b2a", "50e457b74a6c5189494d99db4c3b8cac6480c937e43cca03a270d71a39272c5f"),
            new Target("CanCanPiano", "2e04a04cc56346f6a4c6e5f832df5a55eb1abd52b16d1d1f61aee5120a6ee081", "8f53952f9f5002ed5f6b313ab170bb317e1494605d3cad8b183a0e78353ddcd3")
        };
        private static readonly Target[] Frozen =
        {
            new Target("IJustWannaDancePop", "9283d289245f52a5e0ce3a29c61a46fcd16f84298a96e81ccceaf69fdaa1b376", "bea4e85a21807ee88b90b745209a8057dedaf9a0fea82671cb0f8056f87ccc40"),
            new Target("MoonlightCloudsPiano", "25752621169c96f26c49e211e03373fdf27584eeef912950a642f022e60761ef", "9d99efd063bb86889578244439258c1f1692de1f5939e9a8693aa0dfbf9d7ed6")
        };
        private static readonly Target[] Regression =
        {
            new Target("UnstoppableCat054", "277e5fd678a41323273dd786f32450291338d4edf4752d9ff83939b52548de90", "7fdf15c8df324f3dff96d1992248081611f0751a2ad454013f719d15bcf91d6b"),
            new Target("TwinkleLittleStar", "96657140ead6377636747c6619bd3e060c60bfe41f32276c4436dbf6b15754db", "0cd8dc69f01e117562bf8d21afa17225c5594e7e1ba0920444275953fddec44f"),
            new Target("MoonlightPop", "ae158d469109c32c621cbe915062e3ff5129abe1c1001343f557f8ba0e7b3804", "35cb4620f0b6436f8c918538ecc6e3e81ef6180a2fb4ba4fd39931156909b843"),
            new Target("LABUBUSongsPop", "1e9b3f9363637f19b2bcaa45fe361a6ab13ac8c4b9c3ad45d50a6d1d58ef108f", "dadc0644e3f1b2fe9d9f10f19b0df81eff5dcf087cc382d9a44e74a2a6a57190")
        };
        [Serializable] private sealed class Fingerprint
        {
            public string referenceMapSha256 = "", sourceAudioSha256 = "", sourcePcmSha256 = "", voiceFeatureSha256 = "", generator = "";
            public float generatedDelayTime = 0, generatedForwardSpeed = 0;
            public bool geometryValid = false;
            public string geometryValidationError = "";
        }
        [Serializable] private sealed class Failure
        { public string stage, referenceId, error; public bool evaluationFailed = true; }
        [Serializable] private sealed class FreezePermit
        { public bool beforeValidation = false; public string frozenAtUtc = ""; }

        [Serializable] public sealed class IntervalMatch
        {
            public int referenceIndex, generatedIndex;
            public double referenceStart, referenceEnd, generatedStart, generatedEnd;
            public double onsetErrorMs, endErrorMs, intersectionOverUnion;
        }
        [Serializable] public sealed class IntervalMetrics
        {
            public string definition;
            public int referenceCount, generatedCount, truePositive, falsePositive, falseNegative;
            public double precision, recall, f1;
            public IntervalMatch[] matches;
        }
        [Serializable] public sealed class TypedReport
        {
            public string stage, referenceId, referenceMapSha256, sourceAudioSha256, sourcePcmSha256, voiceFeatureSha256, generator;
            public string generatedTimelineSha256;
            public float generatedDelayTime, generatedForwardSpeed;
            public bool geometryValid;
            public IntervalMetrics shortHolds, allHolds, follows;
        }

        public static void ExportCalibrationPcm() => Export(Calibration);
        public static void ExportFrozenPcm() => Export(Frozen);
        public static void RunCalibrationBaseline() => Run(Calibration, "baseline", "Calibration recordings; candidate errors may guide tuning.");
        public static void RunCalibrationCandidate() => Run(Calibration, "candidate", "Calibration recordings; candidate errors may guide tuning.");
        public static void RunFrozenBaseline() => Run(Frozen, "baseline", "Stratified frozen recordings; baseline errors must remain unopened before algorithm freeze.");
        public static void RunFrozenCandidate()
        {
            string path = Path.GetFullPath("Library/ChartGenerationV9/freeze.json");
            var permit = File.Exists(path) ? JsonUtility.FromJson<FreezePermit>(File.ReadAllText(path)) : null;
            if (permit == null || !permit.beforeValidation || !DateTimeOffset.TryParse(permit.frozenAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                throw new InvalidOperationException("Freeze the algorithm before opening the frozen candidate evaluation.");
            Run(Frozen, "candidate", "Stratified frozen recordings evaluated after the recorded algorithm freeze. Subsequent tuning revokes a held-out claim.");
        }
        public static void RunRegressionBaseline() => Run(Regression, "baseline", "Previously inspected regression recordings; not an independent validation set.");
        public static void RunRegressionCandidate() => Run(Regression, "candidate", "Previously inspected regression recordings; not an independent validation set.");

        private static void Export(Target[] targets)
        {
            RequireEditor();
            foreach (var target in targets) GuardSource(target, record => VocalPcmExport.Export(record.LoadAudio()));
        }

        private static void Run(Target[] targets, string stage, string scope)
        {
            RequireEditor();
            RunMetricChecks();
            var failures = new List<string>();
            foreach (var target in targets)
            {
                string folder = Path.Combine(Output, target.id);
                Directory.CreateDirectory(folder);
                try
                {
                    GuardSource(target, record =>
                    {
                        string voicePath = Path.Combine(folder, "voice.json");
                        var evidence = File.Exists(voicePath)
                            ? JsonUtility.FromJson<PcmVocalAnalysis.Evidence>(File.ReadAllText(voicePath))
                            : PcmVocalAnalysis.LoadForClip(record.LoadAudio());
                        if (evidence == null || evidence.sourceFileSha256 != target.audioHash)
                            throw new InvalidDataException("Prepare verified actual-PCM vocal evidence for " + target.id);
                        string baselinePath = Path.Combine(folder, "baseline.json");
                        if (stage == "candidate" || File.Exists(baselinePath))
                        {
                            if (!File.Exists(baselinePath)) throw new InvalidOperationException("Capture the immutable paired baseline first.");
                            var before = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(baselinePath));
                            if (before.referenceMapSha256 != target.mapHash || before.sourceAudioSha256 != target.audioHash
                                || before.sourcePcmSha256 != evidence.sourcePcmSha256 || before.voiceFeatureSha256 != evidence.featureSha256
                                || Math.Abs(before.generatedDelayTime - Preroll) > 1e-6 || Math.Abs(before.generatedForwardSpeed - record.forwardSpeed) > 1e-6)
                                throw new InvalidDataException("Paired source, PCM, evidence, speed or preroll differs from the baseline.");
                            if (stage == "baseline")
                            {
                                if (!File.Exists(Path.Combine(folder, "baseline.typed.json"))) WriteTyped(record, stage, folder);
                                Debug.Log("PCM_STUDY_V9 retained immutable baseline for " + target.id);
                                if (!before.geometryValid)
                                    Debug.LogWarning("PCM_STUDY_V9 baseline geometry remains invalid for " + target.id
                                        + "; retaining comparison evidence is not a baseline validation pass: " + before.geometryValidationError);
                                return;
                            }
                        }
                        OriginalChartCalibration.EvaluateSong(record, stage, folder, Preroll, evidence,
                            scope + " Record speed and common 0.6-second preroll; reference assets are never published. Candidate short supports are explicitly experimental.",
                            experimentalShortSupports: stage == "candidate");
                        WriteTyped(record, stage, folder);
                        var report = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(Path.Combine(folder, stage + ".json")));
                        if (!report.geometryValid) throw new InvalidDataException("Generated geometry failed; the complete report is retained: " + report.geometryValidationError);
                        string failure = Path.Combine(folder, stage + ".failure.json");
                        if (File.Exists(failure)) File.Delete(failure);
                    });
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(folder, stage + ".failure.json"), JsonUtility.ToJson(new Failure
                        { stage = stage, referenceId = target.id, error = error.ToString() }, true));
                    failures.Add(target.id + ": " + error.Message);
                }
            }
            if (failures.Count != 0) throw new InvalidOperationException("V9 study failures are retained, never silently skipped:\n" + string.Join("\n", failures));
            Debug.Log("PCM_STUDY_V9 " + stage + " completed for " + targets.Length + " recordings; no playable map, catalog or player build was written.");
        }

        private static void WriteTyped(SongRecord record, string stage, string folder)
        {
            // This boundary runs after independent audio generation has finished.
            var generated = JsonUtility.FromJson<MapTimeline>(File.ReadAllText(Path.Combine(folder, stage + ".generated.timeline.json")));
            var reference = JsonUtility.FromJson<MapTimeline>(File.ReadAllText("Assets/Resources/" + record.timelineResource + ".json"));
            var fingerprint = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(Path.Combine(folder, stage + ".json")));
            var report = new TypedReport { stage = stage, referenceId = record.id,
                generatedTimelineSha256 = Hash(Path.Combine(folder, stage + ".generated.timeline.json")),
                referenceMapSha256 = fingerprint.referenceMapSha256, sourceAudioSha256 = fingerprint.sourceAudioSha256,
                sourcePcmSha256 = fingerprint.sourcePcmSha256, voiceFeatureSha256 = fingerprint.voiceFeatureSha256,
                generator = fingerprint.generator, generatedDelayTime = fingerprint.generatedDelayTime,
                generatedForwardSpeed = fingerprint.generatedForwardSpeed, geometryValid = fingerprint.geometryValid,
                shortHolds = Match(reference.segments.Where(ShortHold).ToArray(), generated.segments.Where(ShortHold).ToArray(), .08, .08,
                    "Hold duration <=0.45s on both sides; onset <=80ms; end <=80ms; IoU >=0.5; maximum-cardinality one-to-one match."),
                allHolds = Match(reference.segments.Where(s => s.kind == "Hold").ToArray(), generated.segments.Where(s => s.kind == "Hold").ToArray(), .08, .08,
                    "All Hold intervals; onset <=80ms; end <=80ms; IoU >=0.5; maximum-cardinality one-to-one match."),
                follows = Match(reference.segments.Where(s => s.kind == "Follow").ToArray(), generated.segments.Where(s => s.kind == "Follow").ToArray(), .08, .15,
                    "Follow intervals; onset <=80ms; end <=150ms; IoU >=0.5; maximum-cardinality one-to-one match.") };
            File.WriteAllText(Path.Combine(folder, stage + ".typed.json"), JsonUtility.ToJson(report, true));
        }
        private static bool ShortHold(MapSegment s) => s.kind == "Hold" && s.endAudioTime - s.audioTime <= .45 + 1e-6;

        public static IntervalMetrics Match(MapSegment[] reference, MapSegment[] generated, double onsetTolerance, double endTolerance, string definition)
        {
            double IoU(MapSegment a, MapSegment b)
            {
                double intersection = Math.Max(0, Math.Min(a.endAudioTime, b.endAudioTime) - Math.Max(a.audioTime, b.audioTime));
                double union = Math.Max(a.endAudioTime, b.endAudioTime) - Math.Min(a.audioTime, b.audioTime);
                return union > 0 ? intersection / union : 0;
            }
            var edges = reference.Select(a => Enumerable.Range(0, generated.Length).Where(j =>
                Math.Abs(a.audioTime - generated[j].audioTime) <= onsetTolerance + 1e-9
                && Math.Abs(a.endAudioTime - generated[j].endAudioTime) <= endTolerance + 1e-9
                && IoU(a, generated[j]) >= .5 - 1e-9)
                .OrderBy(j => Math.Abs(a.audioTime - generated[j].audioTime) + Math.Abs(a.endAudioTime - generated[j].endAudioTime)).ThenBy(j => j).ToArray()).ToArray();
            var owner = Enumerable.Repeat(-1, generated.Length).ToArray();
            bool Assign(int i, bool[] visited)
            {
                foreach (int j in edges[i])
                {
                    if (visited[j]) continue;
                    visited[j] = true;
                    if (owner[j] < 0 || Assign(owner[j], visited)) { owner[j] = i; return true; }
                }
                return false;
            }
            for (int i = 0; i < reference.Length; i++) Assign(i, new bool[generated.Length]);
            var matches = Enumerable.Range(0, generated.Length).Where(j => owner[j] >= 0).Select(j =>
            {
                var a = reference[owner[j]]; var b = generated[j];
                return new IntervalMatch { referenceIndex = a.sourceIndex, generatedIndex = b.sourceIndex,
                    referenceStart = a.audioTime, referenceEnd = a.endAudioTime, generatedStart = b.audioTime, generatedEnd = b.endAudioTime,
                    onsetErrorMs = (b.audioTime - a.audioTime) * 1000, endErrorMs = (b.endAudioTime - a.endAudioTime) * 1000,
                    intersectionOverUnion = IoU(a, b) };
            }).OrderBy(m => m.referenceStart).ToArray();
            double precision = generated.Length == 0 ? 0 : (double)matches.Length / generated.Length;
            double recall = reference.Length == 0 ? 0 : (double)matches.Length / reference.Length;
            return new IntervalMetrics { definition = definition, referenceCount = reference.Length, generatedCount = generated.Length,
                truePositive = matches.Length, falsePositive = generated.Length - matches.Length, falseNegative = reference.Length - matches.Length,
                precision = precision, recall = recall, f1 = precision + recall > 0 ? 2 * precision * recall / (precision + recall) : 0, matches = matches };
        }

        public static void RunMetricChecks()
        {
            MapSegment S(int index, double a, double b) => new MapSegment { sourceIndex = index, kind = "Hold", audioTime = a, endAudioTime = b };
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("PCM_STUDY_V9_METRIC FAIL: " + message); }
            var exact = Match(new[] { S(0, 1, 1.3) }, new[] { S(1, 1.01, 1.31) }, .08, .08, "Fixture");
            Require(exact.truePositive == 1 && exact.falsePositive == 0 && exact.falseNegative == 0, "matching short intervals");
            var wrongEnd = Match(new[] { S(0, 1, 1.3) }, new[] { S(1, 1, 1.5) }, .08, .08, "Fixture");
            Require(wrongEnd.truePositive == 0 && wrongEnd.falsePositive == 1 && wrongEnd.falseNegative == 1, "onset alone cannot match the wrong duration");
            var duplicate = Match(new[] { S(0, 1, 1.3) }, new[] { S(1, 1, 1.3), S(2, 1.01, 1.31) }, .08, .08, "Fixture");
            Require(duplicate.truePositive == 1 && duplicate.falsePositive == 1, "one original cannot match two generated intervals");
            var augment = Match(new[] { S(0, 1.04, 1.34), S(1, .96, 1.26) }, new[] { S(0, 1.0, 1.30), S(1, 1.10, 1.40) }, .08, .08, "Fixture");
            Require(augment.truePositive == 2, "maximum matching must recover a valid alternate assignment");
            var poorOverlap = Match(new[] { S(0, 1, 1.1) }, new[] { S(0, 1.07, 1.17) }, .08, .08, "Fixture");
            Require(poorOverlap.truePositive == 0, "individually close boundaries still require meaningful overlap");
            Require(Match(Array.Empty<MapSegment>(), Array.Empty<MapSegment>(), .08, .08, "Empty").matches.Length == 0, "empty sets remain explicit");
            Debug.Log("PCM_STUDY_V9_METRIC PASS: typed duration boundaries, IoU, maximum-cardinality one-to-one assignment, duplicate rejection and explicit empty sets.");
        }

        private static void GuardSource(Target target, Action<SongRecord> action)
        {
            var record = RecoveredCatalog.LoadOriginal().Single(s => s.id == target.id);
            var clip = record.LoadAudio();
            if (clip == null) throw new FileNotFoundException("Study AudioClip is missing: " + record.audioResource);
            string map = Path.GetFullPath("Assets/Resources/" + record.timelineResource + ".json");
            string audio = Path.GetFullPath(AssetDatabase.GetAssetPath(clip));
            if (Hash(map) != target.mapHash || Hash(audio) != target.audioHash)
                throw new InvalidDataException("Selected map/audio differs from the V9 selection manifest: " + target.id);
            var paths = new[] { map, map + ".meta", audio, audio + ".meta", Path.GetFullPath("Assets/Resources/OriginalData/songs.json"), Path.GetFullPath("Assets/Resources/OriginalData/songs.json.meta") };
            var before = paths.Where(File.Exists).ToDictionary(p => p, Hash, StringComparer.OrdinalIgnoreCase);
            try { action(record); }
            finally
            {
                foreach (var pair in before)
                    if (!File.Exists(pair.Key) || Hash(pair.Key) != pair.Value)
                        throw new InvalidOperationException("Study altered a protected original resource: " + pair.Key);
            }
        }
        private static void RequireEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Run the study in Edit Mode after import and compilation complete.");
        }
        private static string Hash(string path)
        { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    }
}
