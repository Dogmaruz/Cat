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
    /// <summary>Versioned read-only evaluation. The source selection and annotations never enter gesture inference.</summary>
    public static class PcmChartStudyV10
    {
        public const string Study = "Library/ChartGenerationV10";
        private const string BaselineGenerator = "pcm-vocal-phrase-chart-v9+short-test";
        private const string BaselineInstrumentVersion = "mixed-note-events-1";
        private const string CandidateInstrumentVersion = "mixed-note-events-2-candidates8";
        [Serializable] public sealed class Source
        {
            public string id, role, sourceFile, sourceFileSha256, referenceMapSha256;
            public string TestId => id + "GeneratorTestV10";
        }
        [Serializable] private sealed class Selection { public Source[] selection; }
        [Serializable] public sealed class Fingerprint
        {
            public string stage, referenceId, referenceMapSha256, sourceAudioSha256, sourcePcmSha256, voiceFeatureSha256, instrumentFeatureSha256, generator;
            public float generatedDelayTime, generatedForwardSpeed;
            public bool geometryValid;
            public string geometryValidationError;
        }
        [Serializable] private sealed class Failure { public string stage, referenceId, error; }
        [Serializable] public sealed class FrozenHash { public string path, sha256; public bool normalizedLf; }
        [Serializable] private sealed class FreezePermit
        {
            public bool beforeValidation;
            public string frozenAtUtc;
            public FrozenHash[] algorithm, measurements, baselines, manifests;
        }
        [Serializable] private sealed class MeasurementTransition
        {
            public string id, sourceFileSha256, sourcePcmSha256, baselineInstrumentFeatureSha256, candidateInstrumentFeatureSha256;
            public string baselineAnalysisVersion, candidateAnalysisVersion;
        }
        [Serializable] private sealed class MeasurementTransitions { public MeasurementTransition[] selection; }

        public static Source[] Sources => JsonUtility.FromJson<Selection>(File.ReadAllText(Study + "/selection.json")).selection;
        public static void ExportNewPcm()
        {
            RequireEditor();
            foreach (var source in Sources.Where(s => s.role != "known")) Guard(source, record => VocalPcmExport.Export(record.LoadAudio()));
        }
        public static void BaselineKnown() => Run(false, false);
        public static void BaselineHeldout() => Run(true, false);
        public static void CandidateKnown() => Run(false, true);
        public static void CandidateHeldout()
        {
            ValidateFrozenInputs();
            Run(true, true);
        }
        public static void ValidateFrozenInputs()
        {
            var permit = File.Exists(Study + "/freeze.json") ? JsonUtility.FromJson<FreezePermit>(File.ReadAllText(Study + "/freeze.json")) : null;
            Require(permit != null && permit.beforeValidation && DateTimeOffset.TryParse(permit.frozenAtUtc,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _), "Freeze source and measurement hashes before opening heldout candidates.");
            Require(permit.algorithm != null && permit.algorithm.Length >= 17 && permit.measurements != null && permit.measurements.Length >= 20
                && permit.baselines != null && permit.baselines.Length == 30 && ManifestsBound(permit.manifests),
                "The freeze must bind algorithm, all source measurements, ten complete baselines and the selection/provenance/transition manifests.");
            var entries = permit.algorithm.Concat(permit.measurements).Concat(permit.baselines).Concat(permit.manifests).ToArray();
            Require(entries.All(e => e != null), "Frozen input entries may not be null.");
            Require(entries.Select(e => e.path).Distinct(StringComparer.Ordinal).Count() == entries.Length, "Frozen input paths must be unique.");
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            Require(entries.All(entry => MatchesFrozenFile(entry, repo)), "Frozen source, measurement or baseline changed; this is no longer the frozen evaluation.");
        }
        private static bool ManifestsBound(FrozenHash[] manifests)
            => manifests != null && manifests.Length >= 3 && manifests.All(e => e != null)
                && new[] { "selection.json", "baseline-source-inputs.json", "measurement-transition.json" }
                    .All(name => manifests.Any(e => e.path == "DancingCatsClone/" + Study + "/" + name && !e.normalizedLf));
        private static bool MatchesFrozenFile(FrozenHash entry, string repo)
        {
            if (entry == null || string.IsNullOrEmpty(entry.path) || string.IsNullOrEmpty(entry.sha256)) return false;
            string path = Path.GetFullPath(Path.Combine(repo, entry.path));
            if (!path.StartsWith(repo.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return false;
            byte[] bytes = File.ReadAllBytes(path);
            if (entry.normalizedLf) bytes = bytes.Where((b, i) => b != 13 || i + 1 == bytes.Length || bytes[i + 1] != 10).ToArray();
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == entry.sha256;
        }
        public static void RunFreezeChecks()
        {
            string path = Study + "/GuardFixtures/freeze.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes("one\r\ntwo\r\n"));
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string normalized;
            using (var sha = SHA256.Create()) normalized = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("one\ntwo\n"))).Replace("-", "").ToLowerInvariant();
            var entry = new FrozenHash { path = "DancingCatsClone/" + path, sha256 = normalized, normalizedLf = true };
            Require(MatchesFrozenFile(entry, repo), "Line-ending normalization must agree with source snapshot hashing.");
            entry.normalizedLf = false;
            Require(!MatchesFrozenFile(entry, repo), "Raw measurement hashes must retain byte-level changes.");
            entry.normalizedLf = true;
            File.WriteAllText(path, "changed");
            Require(!MatchesFrozenFile(entry, repo), "A changed algorithm/input must invalidate the frozen permit.");
            entry.path = "../outside-the-repository";
            Require(!MatchesFrozenFile(entry, repo), "A frozen manifest may not read a path outside the repository.");
            File.Delete(path);
            RunBaselineGuardChecks();
            Debug.Log("PCM_STUDY_V10_FREEZE PASS: source normalization, byte-exact measurements, changed inputs and path containment.");
        }
        private static void Run(bool heldout, bool candidate)
        {
            RequireEditor();
            PcmChartStudyV9.RunMetricChecks();
            string stage = candidate ? "candidate" : "baseline";
            var failures = new List<string>();
            foreach (var source in Sources.Where(s => (s.role != "known") == heldout))
            {
                string folder = Study + "/benchmark/" + source.id;
                Directory.CreateDirectory(folder);
                try
                {
                    Guard(source, record =>
                    {
                        string baseline = folder + "/baseline.json";
                        var clip = record.LoadAudio();
                        var voice = PcmVocalAnalysis.LoadForClip(clip);
                        Require(voice != null && voice.sourceFileSha256 == source.sourceFileSha256, "Install verified actual-PCM vocal evidence before the comparison.");
                        var notes = PcmInstrumentalEvidence.LoadForClip(clip);
                        Require(notes != null && notes.sourcePcmSha256 == voice.sourcePcmSha256, "Install verified mixed-note evidence before the comparison.");
                        string actualPcm = DecodedPcmHash(clip);
                        PcmVocalAnalysis.Validate(voice, actualPcm, clip.frequency, clip.channels, clip.samples);
                        PcmInstrumentalEvidence.Validate(notes, actualPcm, clip.frequency, clip.channels, clip.samples);
                        if (!candidate && File.Exists(baseline))
                        {
                            ValidateBaseline(source, record, voice, notes, folder);
                            return;
                        }
                        if (candidate)
                        {
                            Require(File.Exists(baseline), "Capture the v9 baseline before changing inference.");
                            ValidateBaseline(source, record, voice, notes, folder);
                        }
                        OriginalChartCalibration.EvaluateSong(record, stage, folder, .6f, voice,
                            (heldout ? "New aggregate-selected heldout pair; candidate evaluated after recorded freeze." : "Eight previously studied tuning/regression recordings.")
                            + " Both baseline and candidate explicitly enable experimental short supports. References are evaluation labels only.", experimentalShortSupports: true);
                        WriteTyped(record, stage, folder);
                        var report = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(folder + "/" + stage + ".json"));
                        if (candidate) Require(report.geometryValid, "Candidate geometry failed: " + report.geometryValidationError);
                        string failure = folder + "/" + stage + ".failure.json";
                        if (File.Exists(failure)) File.Delete(failure);
                    });
                }
                catch (Exception error)
                {
                    File.WriteAllText(folder + "/" + stage + ".failure.json", JsonUtility.ToJson(new Failure { stage = stage, referenceId = source.id, error = error.ToString() }, true));
                    failures.Add(source.id + ": " + error.Message);
                }
            }
            Require(failures.Count == 0, "V10 study failures retained:\n" + string.Join("\n", failures));
            Debug.Log("PCM_STUDY_V10 PASS: " + stage + "; " + (heldout ? "heldout" : "known") + "; no playable assets or builds written.");
        }
        private static void ValidateBaseline(Source source, SongRecord record, PcmVocalAnalysis.Evidence voice,
            PcmInstrumentalEvidence.Evidence currentNotes, string folder)
        {
            string archivedPath = Study + "/before/measurements/" + source.sourceFileSha256 + ".notes.json";
            Require(File.Exists(archivedPath), "The immutable v9 mixed-note measurements are missing: " + source.id);
            var archived = JsonUtility.FromJson<PcmInstrumentalEvidence.Evidence>(File.ReadAllText(archivedPath));
            PcmInstrumentalEvidence.Validate(archived, voice.sourcePcmSha256, voice.sampleRate, voice.channels, voice.sampleFrames);
            Require(archived.sourceFileSha256 == source.sourceFileSha256 && archived.analysisVersion == BaselineInstrumentVersion,
                "The archived baseline measurements differ from the v9 source/version.");
            if (currentNotes.featureSha256 != archived.featureSha256 || currentNotes.analysisVersion != archived.analysisVersion)
            {
                string transitionPath = Study + "/measurement-transition.json";
                Require(File.Exists(transitionPath), "A changed measurement set requires explicit v9-to-v10 provenance.");
                var transitions = JsonUtility.FromJson<MeasurementTransitions>(File.ReadAllText(transitionPath));
                Require(transitions?.selection != null && transitions.selection.All(row => row != null)
                    && transitions.selection.Select(row => row.id).Distinct(StringComparer.Ordinal).Count() == transitions.selection.Length,
                    "Measurement transition entries must exist and have unique source identities.");
                var transition = transitions.selection.SingleOrDefault(row => row.id == source.id);
                Require(TransitionMatches(transition, source, archived, currentNotes), "Current measurements are not the pinned v9-to-v10 transition: " + source.id);
            }
            var expected = new Fingerprint { stage = "baseline", referenceId = source.id, referenceMapSha256 = source.referenceMapSha256,
                sourceAudioSha256 = source.sourceFileSha256, sourcePcmSha256 = voice.sourcePcmSha256, voiceFeatureSha256 = voice.featureSha256,
                instrumentFeatureSha256 = archived.featureSha256, generator = BaselineGenerator, generatedDelayTime = .6f,
                generatedForwardSpeed = record.forwardSpeed };
            ValidateCachedBaseline(folder, expected);
        }
        private static bool TransitionMatches(MeasurementTransition transition, Source source,
            PcmInstrumentalEvidence.Evidence baseline, PcmInstrumentalEvidence.Evidence candidate)
            => transition != null && source != null && baseline != null && candidate != null
                && transition.id == source.id && transition.sourceFileSha256 == source.sourceFileSha256
                && baseline.sourceFileSha256 == source.sourceFileSha256 && candidate.sourceFileSha256 == source.sourceFileSha256
                && transition.sourcePcmSha256 == baseline.sourcePcmSha256 && transition.sourcePcmSha256 == candidate.sourcePcmSha256
                && transition.baselineInstrumentFeatureSha256 == baseline.featureSha256 && transition.candidateInstrumentFeatureSha256 == candidate.featureSha256
                && transition.baselineAnalysisVersion == BaselineInstrumentVersion && baseline.analysisVersion == BaselineInstrumentVersion
                && transition.candidateAnalysisVersion == CandidateInstrumentVersion && candidate.analysisVersion == CandidateInstrumentVersion;
        private static void ValidateCachedBaseline(string folder, Fingerprint expected)
        {
            string baselinePath = folder + "/baseline.json", typedPath = folder + "/baseline.typed.json", mapPath = folder + "/baseline.generated.timeline.json";
            Require(File.Exists(baselinePath) && File.Exists(typedPath) && File.Exists(mapPath), "Baseline is incomplete; never silently regenerate it.");
            Require(!File.Exists(folder + "/baseline.failure.json"), "A failed baseline attempt must be resolved explicitly before immutable reuse.");
            var prior = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(baselinePath));
            var typed = JsonUtility.FromJson<PcmChartStudyV9.TypedReport>(File.ReadAllText(typedPath));
            Require(BaselineReportsAgree(prior, typed, expected, Hash(mapPath)), "Cached v9 baseline source/PCM/evidence/options or typed timeline identity differs from the immutable comparison.");
        }
        private static bool BaselineReportsAgree(Fingerprint prior, PcmChartStudyV9.TypedReport typed, Fingerprint expected, string mapHash)
            => prior != null && typed != null && expected != null && prior.stage == "baseline" && typed.stage == "baseline"
                && prior.generator == BaselineGenerator && typed.generator == BaselineGenerator
                && prior.referenceId == expected.referenceId && typed.referenceId == expected.referenceId
                && prior.referenceMapSha256 == expected.referenceMapSha256 && typed.referenceMapSha256 == expected.referenceMapSha256
                && prior.sourceAudioSha256 == expected.sourceAudioSha256 && typed.sourceAudioSha256 == expected.sourceAudioSha256
                && prior.sourcePcmSha256 == expected.sourcePcmSha256 && typed.sourcePcmSha256 == expected.sourcePcmSha256
                && prior.voiceFeatureSha256 == expected.voiceFeatureSha256 && typed.voiceFeatureSha256 == expected.voiceFeatureSha256
                && prior.instrumentFeatureSha256 == expected.instrumentFeatureSha256 && prior.geometryValid == typed.geometryValid
                && Math.Abs(prior.generatedDelayTime - expected.generatedDelayTime) < 1e-6 && Math.Abs(typed.generatedDelayTime - expected.generatedDelayTime) < 1e-6
                && Math.Abs(prior.generatedForwardSpeed - expected.generatedForwardSpeed) < 1e-6 && Math.Abs(typed.generatedForwardSpeed - expected.generatedForwardSpeed) < 1e-6
                && !string.IsNullOrEmpty(mapHash) && typed.generatedTimelineSha256 == mapHash;
        private static string DecodedPcmHash(AudioClip clip)
        {
            var pcm = new float[checked(clip.samples * clip.channels)];
            Require(clip.GetData(pcm, 0), "Actual decoded audio PCM is unavailable for immutable baseline verification.");
            var bytes = new byte[checked(pcm.Length * sizeof(float))];
            Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian)
                for (int i = 0; i < bytes.Length; i += sizeof(float)) Array.Reverse(bytes, i, sizeof(float));
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static void RunBaselineGuardChecks()
        {
            string folder = Study + "/GuardFixtures/Baseline-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            string baselinePath = folder + "/baseline.json", typedPath = folder + "/baseline.typed.json";
            string mapPath = folder + "/baseline.generated.timeline.json", failurePath = folder + "/baseline.failure.json";
            var expected = new Fingerprint { stage = "baseline", referenceId = "Fixture", referenceMapSha256 = "map",
                sourceAudioSha256 = "audio", sourcePcmSha256 = "pcm", voiceFeatureSha256 = "voice", instrumentFeatureSha256 = "notes-v1",
                generator = BaselineGenerator, generatedDelayTime = .6f, generatedForwardSpeed = 8, geometryValid = true };
            var typed = new PcmChartStudyV9.TypedReport { stage = "baseline", referenceId = "Fixture", referenceMapSha256 = "map",
                sourceAudioSha256 = "audio", sourcePcmSha256 = "pcm", voiceFeatureSha256 = "voice", generator = BaselineGenerator,
                generatedDelayTime = .6f, generatedForwardSpeed = 8, geometryValid = true };
            Fingerprint prior = null;
            void Reset()
            {
                prior = JsonUtility.FromJson<Fingerprint>(JsonUtility.ToJson(expected));
                typed.stage = "baseline"; typed.referenceId = "Fixture"; typed.referenceMapSha256 = "map"; typed.sourceAudioSha256 = "audio";
                typed.sourcePcmSha256 = "pcm"; typed.voiceFeatureSha256 = "voice"; typed.generator = BaselineGenerator;
                typed.generatedDelayTime = .6f; typed.generatedForwardSpeed = 8; typed.geometryValid = true;
                File.WriteAllText(mapPath, "fixture native map\n");
                typed.generatedTimelineSha256 = Hash(mapPath);
                File.WriteAllText(baselinePath, JsonUtility.ToJson(prior));
                File.WriteAllText(typedPath, JsonUtility.ToJson(typed));
                if (File.Exists(failurePath)) File.Delete(failurePath);
            }
            void CheckRejected(Action change, string reason)
            {
                Reset(); change();
                bool rejected = false;
                try { ValidateCachedBaseline(folder, expected); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Immutable baseline guard missed " + reason);
            }
            try
            {
                Reset(); ValidateCachedBaseline(folder, expected);
                CheckRejected(() => File.Delete(mapPath), "a missing generated map");
                CheckRejected(() => File.Delete(typedPath), "missing typed results");
                CheckRejected(() => File.AppendAllText(mapPath, "changed"), "a changed native map hash");
                CheckRejected(() => File.WriteAllText(failurePath, "prior failed attempt"), "a stale failure marker");
                CheckRejected(() => { typed.stage = "candidate"; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "a candidate report reused as baseline");
                CheckRejected(() => { typed.referenceId = "Other"; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "typed reference identity");
                CheckRejected(() => { typed.referenceMapSha256 = "other-map"; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "typed source map identity");
                CheckRejected(() => { typed.sourceAudioSha256 = "other-audio"; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "typed source audio identity");
                CheckRejected(() => { typed.sourcePcmSha256 = "other-pcm"; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "typed PCM identity");
                CheckRejected(() => { typed.voiceFeatureSha256 = "other-voice"; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "typed vocal measurements");
                CheckRejected(() => { typed.generatedDelayTime = .7f; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "typed preroll");
                CheckRejected(() => { typed.generatedForwardSpeed = 9; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "typed speed");
                CheckRejected(() => { typed.generator = "v10"; File.WriteAllText(typedPath, JsonUtility.ToJson(typed)); }, "typed generator version");
                CheckRejected(() => { prior.stage = "candidate"; File.WriteAllText(baselinePath, JsonUtility.ToJson(prior)); }, "a non-baseline generic report");
                CheckRejected(() => { prior.sourceAudioSha256 = "other"; File.WriteAllText(baselinePath, JsonUtility.ToJson(prior)); }, "generic source identity");
                CheckRejected(() => { prior.sourcePcmSha256 = "other"; File.WriteAllText(baselinePath, JsonUtility.ToJson(prior)); }, "generic PCM identity");
                CheckRejected(() => { prior.voiceFeatureSha256 = "other"; File.WriteAllText(baselinePath, JsonUtility.ToJson(prior)); }, "generic vocal measurements");
                CheckRejected(() => { prior.instrumentFeatureSha256 = "notes-v2"; File.WriteAllText(baselinePath, JsonUtility.ToJson(prior)); }, "unarchived baseline instrument measurements");
                CheckRejected(() => { prior.generatedDelayTime = .7f; File.WriteAllText(baselinePath, JsonUtility.ToJson(prior)); }, "generic preroll");
                CheckRejected(() => { prior.generator = "v10"; File.WriteAllText(baselinePath, JsonUtility.ToJson(prior)); }, "a retuned generator as immutable v9");
                Reset(); prior.generatedDelayTime = float.NaN;
                Require(!BaselineReportsAgree(prior, typed, expected, Hash(mapPath)), "Non-finite baseline options must be rejected.");
                var source = new Source { id = "Fixture", sourceFileSha256 = "audio" };
                var oldNotes = new PcmInstrumentalEvidence.Evidence { sourceFileSha256 = "audio", sourcePcmSha256 = "pcm", featureSha256 = "notes-v1", analysisVersion = BaselineInstrumentVersion };
                var newNotes = new PcmInstrumentalEvidence.Evidence { sourceFileSha256 = "audio", sourcePcmSha256 = "pcm", featureSha256 = "notes-v2", analysisVersion = CandidateInstrumentVersion };
                var transition = new MeasurementTransition { id = "Fixture", sourceFileSha256 = "audio", sourcePcmSha256 = "pcm",
                    baselineInstrumentFeatureSha256 = "notes-v1", candidateInstrumentFeatureSha256 = "notes-v2",
                    baselineAnalysisVersion = BaselineInstrumentVersion, candidateAnalysisVersion = CandidateInstrumentVersion };
                Require(TransitionMatches(transition, source, oldNotes, newNotes), "The explicit source-bound V1-to-V2 measurement transition is accepted.");
                transition.candidateInstrumentFeatureSha256 = "other";
                Require(!TransitionMatches(transition, source, oldNotes, newNotes), "A different new measurement digest must not use the transition permit.");
                transition.candidateInstrumentFeatureSha256 = "notes-v2"; transition.sourcePcmSha256 = "other";
                Require(!TransitionMatches(transition, source, oldNotes, newNotes), "A different decoded PCM must not use the transition permit.");
                transition.sourcePcmSha256 = "pcm"; transition.candidateAnalysisVersion = "future-version";
                Require(!TransitionMatches(transition, source, oldNotes, newNotes), "A different producer version must not use the transition permit.");
                var manifests = new[] { "selection.json", "baseline-source-inputs.json", "measurement-transition.json" }
                    .Select(name => new FrozenHash { path = "DancingCatsClone/" + Study + "/" + name, sha256 = "fixture" }).ToArray();
                Require(ManifestsBound(manifests) && !ManifestsBound(manifests.Take(2).ToArray()) && !ManifestsBound(null), "All three provenance manifests are required.");
                manifests[1] = null;
                Require(!ManifestsBound(manifests), "Null frozen manifest entries must be rejected before enumeration.");
            }
            finally
            {
                foreach (string path in new[] { baselinePath, typedPath, mapPath, failurePath }) if (File.Exists(path)) File.Delete(path);
                Directory.Delete(folder);
            }
            Debug.Log("PCM_STUDY_V10_BASELINE_GUARDS PASS: immutable source/PCM/evidence/options, typed timeline hash, failure markers, V1-to-V2 provenance and frozen manifests.");
        }
        private static void WriteTyped(SongRecord record, string stage, string folder)
        {
            string mapPath = folder + "/" + stage + ".generated.timeline.json";
            var generated = JsonUtility.FromJson<MapTimeline>(File.ReadAllText(mapPath));
            var reference = JsonUtility.FromJson<MapTimeline>(File.ReadAllText("Assets/Resources/" + record.timelineResource + ".json"));
            var fingerprint = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(folder + "/" + stage + ".json"));
            bool Short(MapSegment s) => s.kind == "Hold" && s.endAudioTime - s.audioTime <= .45 + 1e-6;
            var report = new PcmChartStudyV9.TypedReport { stage = stage, referenceId = record.id,
                generatedTimelineSha256 = Hash(mapPath), referenceMapSha256 = fingerprint.referenceMapSha256,
                sourceAudioSha256 = fingerprint.sourceAudioSha256, sourcePcmSha256 = fingerprint.sourcePcmSha256,
                voiceFeatureSha256 = fingerprint.voiceFeatureSha256, generator = fingerprint.generator,
                generatedDelayTime = fingerprint.generatedDelayTime, generatedForwardSpeed = fingerprint.generatedForwardSpeed, geometryValid = fingerprint.geometryValid,
                shortHolds = PcmChartStudyV9.Match(reference.segments.Where(Short).ToArray(), generated.segments.Where(Short).ToArray(), .08, .08,
                    "Hold duration <=0.45s on both sides; onset/end <=80ms; IoU>=0.5; maximum-cardinality one-to-one."),
                allHolds = PcmChartStudyV9.Match(reference.segments.Where(s => s.kind == "Hold").ToArray(), generated.segments.Where(s => s.kind == "Hold").ToArray(), .08, .08,
                    "All Hold; onset/end <=80ms; IoU>=0.5; maximum-cardinality one-to-one."),
                follows = PcmChartStudyV9.Match(reference.segments.Where(s => s.kind == "Follow").ToArray(), generated.segments.Where(s => s.kind == "Follow").ToArray(), .08, .15,
                    "Follow; onset <=80ms; end <=150ms; IoU>=0.5; maximum-cardinality one-to-one.") };
            File.WriteAllText(folder + "/" + stage + ".typed.json", JsonUtility.ToJson(report, true));
        }
        public static void Guard(Source source, Action<SongRecord> action)
        {
            var record = RecoveredCatalog.LoadOriginal().Single(r => r.id == source.id);
            var clip = record.LoadAudio();
            Require(clip != null && Hash(AssetDatabase.GetAssetPath(clip)) == source.sourceFileSha256
                && Hash("Assets/Resources/" + record.timelineResource + ".json") == source.referenceMapSha256, "Selected source identity differs from the manifest.");
            string[] paths = { AssetDatabase.GetAssetPath(clip), AssetDatabase.GetAssetPath(clip) + ".meta",
                "Assets/Resources/" + record.timelineResource + ".json", "Assets/Resources/" + record.timelineResource + ".json.meta" };
            var hashes = paths.Where(File.Exists).ToDictionary(p => p, Hash);
            try { action(record); }
            finally { Require(hashes.All(p => File.Exists(p.Key) && Hash(p.Key) == p.Value), "Original resources changed during the study."); }
        }
        public static string Hash(string path)
        { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        private static void RequireEditor() => Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating, "Use the compiled Editor in Edit Mode.");
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
