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
    /// <summary>Manifest-driven paired studies. Inference never receives reference charts or study roles.</summary>
    public static partial class ChartGeneratorStudy
    {
        public const string CurrentManifest = "Assets/Clone/Editor/ChartStudies/v11.json";
        [Serializable] public sealed class Source
        {
            public string id, role, sourceFile, sourceFileSha256, referenceMapSha256;
            public bool Heldout => role.StartsWith("heldout", StringComparison.Ordinal);
        }
        [Serializable] public sealed class Manifest
        {
            public int schemaVersion, catalogIdStart, protectedOriginalFileCount, protectedCustomMapFileCount, protectedCatalogRecordCount;
            public string studyId, studyDirectory, selectionPath, baselineGenerator, candidateGenerator, testSuffix;
            public string protectedSnapshotSha256, protectedCatalogSha256;
            public float preroll;
            public bool includeExperimentalShortSupports, preferAuthoredScore;
            public Source[] sources;
            public string[] publishSourceIds, algorithmPaths;
        }
        [Serializable] private sealed class Selection { public Source[] selection; }
        [Serializable] private sealed class ProtectedFiles { public FrozenHash[] files; }
        [Serializable] private sealed class Report
        {
            public string stage, referenceId, referenceMapSha256, sourceAudioSha256, sourcePcmSha256, voiceFeatureSha256, instrumentFeatureSha256;
            public string voiceAnalysisVersion, instrumentAnalysisVersion, generator, geometryValidationError;
            public float generatedDelayTime, generatedForwardSpeed;
            public bool geometryValid;
        }
        [Serializable] private sealed class BaselineSeal
        {
            public string id, reportSha256, typedSha256, timelineSha256;
            public Report inputs;
        }
        [Serializable] private sealed class BaselineSeals { public BaselineSeal[] selection = Array.Empty<BaselineSeal>(); }
        [Serializable] private sealed class Failure { public string stage, referenceId, error; }
        [Serializable] public sealed class FrozenHash { public string path, sha256; public bool normalizedLf; }
        [Serializable] private sealed class Freeze
        {
            public bool beforeValidation;
            public string frozenAtUtc;
            public FrozenHash[] algorithm, measurements, baselines, manifests;
        }
        private sealed class Inputs
        {
            public SongRecord record;
            public AudioClip clip;
            public PcmVocalAnalysis.Evidence voice;
            public PcmInstrumentalEvidence.Evidence notes;
        }

        public static Manifest LoadManifest()
        {
            var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(CurrentManifest));
            ValidateManifest(m);
            return m;
        }
        private static void ValidateManifest(Manifest m)
        {
            Require(m != null && m.schemaVersion == 1 && !string.IsNullOrEmpty(m.studyId) && m.studyId.All(c => char.IsLetterOrDigit(c) || c == '-'), "Invalid study identity.");
            Require(m.studyDirectory == "Library/" + m.studyId && m.selectionPath == m.studyDirectory + "/selection.json", "Study outputs must stay in the manifest's Library directory.");
            Require(!string.IsNullOrEmpty(m.testSuffix) && m.testSuffix.StartsWith("GeneratorTest", StringComparison.Ordinal)
                && m.testSuffix.All(char.IsLetterOrDigit) && m.catalogIdStart >= 30021, "Use separately named study tests and the reserved catalog range.");
            Require(!string.IsNullOrEmpty(m.baselineGenerator) && !string.IsNullOrEmpty(m.candidateGenerator) && m.baselineGenerator != m.candidateGenerator,
                "Pin distinct baseline and candidate generator versions.");
            Require(m.preroll == .6f && m.includeExperimentalShortSupports && !m.preferAuthoredScore, "This experiment requires explicit PCM inference, short supports and common 0.6-second preroll.");
            Require(m.sources != null && m.sources.Length > 0 && m.sources.All(s => s != null && !string.IsNullOrEmpty(s.id) && s.id.All(char.IsLetterOrDigit)
                && !string.IsNullOrEmpty(s.role) && (s.role == "known" || s.role == "calibration" || s.Heldout)
                && s.sourceFile != null && s.sourceFile.StartsWith("Assets/Resources/OriginalAudio/", StringComparison.Ordinal)
                && IsHash(s.sourceFileSha256) && IsHash(s.referenceMapSha256)), "Manifest sources are malformed.");
            Require(m.sources.Select(s => s.id).Distinct(StringComparer.Ordinal).Count() == m.sources.Length
                && m.sources.Select(s => s.sourceFileSha256).Distinct(StringComparer.Ordinal).Count() == m.sources.Length, "Sources must have distinct IDs and audio hashes.");
            Require(m.publishSourceIds != null && m.publishSourceIds.Length > 0 && m.publishSourceIds.Distinct(StringComparer.Ordinal).Count() == m.publishSourceIds.Length
                && m.publishSourceIds.All(id => m.sources.Any(s => s.id == id)), "Publication sources must be a unique explicit manifest subset.");
            Require(m.algorithmPaths != null && m.algorithmPaths.Length > 0 && m.algorithmPaths.All(p => !string.IsNullOrEmpty(p) && !Path.IsPathRooted(p)
                && !p.Split('/').Contains("..")) && m.algorithmPaths.Distinct(StringComparer.Ordinal).Count() == m.algorithmPaths.Length,
                "Pin the required inference, measurement and study source paths in the tracked manifest.");
            Require(IsHash(m.protectedSnapshotSha256) && IsHash(m.protectedCatalogSha256), "Pin the initial protected-file inventory and raw catalog snapshot hashes.");
        }
        public static void ExportPcm()
        {
            RequireEditor();
            foreach (var source in LoadManifest().sources.Where(s => s.role != "known")) Guard(source, r => VocalPcmExport.Export(r.LoadAudio()));
        }
        public static void BaselineKnown() => Run(false, false);
        public static void BaselineHeldout() => Run(true, false);
        public static void CandidateKnown() => Run(false, true);
        public static void CandidateHeldout() { ValidateFrozenInputs(); Run(true, true); }
        private static void Run(bool heldout, bool candidate)
        {
            RequireEditor();
            var m = LoadManifest();
            ValidateSelection(m);
            if (candidate) ValidateBaselineInventory(m);
            PcmChartStudyV9.RunMetricChecks();
            string stage = candidate ? "candidate" : "baseline";
            var failures = new List<string>();
            foreach (var source in m.sources.Where(s => s.Heldout == heldout))
            {
                string folder = Folder(m, source);
                Directory.CreateDirectory(folder);
                try
                {
                    Guard(source, record =>
                    {
                        var inputs = ReadInputs(source, record);
                        if (candidate || HasBaselineState(m, source, folder))
                        {
                            ValidateBaseline(m, source, inputs, folder);
                            if (!candidate) return;
                        }
                        OriginalChartCalibration.EvaluateSong(record, stage, folder, m.preroll, inputs.voice,
                            (heldout ? "Aggregate-selected heldout; candidate opened only after frozen hashes were verified." : "Known/calibration recordings; may guide tuning.")
                            + " Baseline and candidate use identical actual PCM, measured evidence and explicit short-support options.", experimentalShortSupports: m.includeExperimentalShortSupports);
                        WriteTyped(record, stage, folder);
                        var report = Read<Report>(folder + "/" + stage + ".json");
                        ValidateReport(m, source, inputs, report, candidate);
                        Require(!candidate || report.geometryValid, "Candidate geometry failed: " + report.geometryValidationError);
                        string failure = folder + "/" + stage + ".failure.json";
                        if (File.Exists(failure)) File.Delete(failure);
                        if (!candidate) SealBaseline(m, source, folder, report);
                    });
                }
                catch (Exception error)
                {
                    Write(folder + "/" + stage + ".failure.json", new Failure { stage = stage, referenceId = source.id, error = error.ToString() });
                    failures.Add(source.id + ": " + error.Message);
                }
            }
            Require(failures.Count == 0, "Study failures retained:\n" + string.Join("\n", failures));
            Debug.Log("CHART_GENERATOR_STUDY PASS: " + m.studyId + "; " + stage + "; " + (heldout ? "heldout" : "known/calibration") + "; Library reports only.");
        }
        private static Inputs ReadInputs(Source source, SongRecord record)
        {
            var clip = record.LoadAudio();
            var voice = PcmVocalAnalysis.LoadForClip(clip);
            var notes = PcmInstrumentalEvidence.LoadForClip(clip);
            Require(voice != null && notes != null && voice.sourceFileSha256 == source.sourceFileSha256 && notes.sourceFileSha256 == source.sourceFileSha256,
                "Install actual-PCM source-verified vocal and instrumental evidence: " + source.id);
            string pcm = DecodedPcmHash(clip);
            PcmVocalAnalysis.Validate(voice, pcm, clip.frequency, clip.channels, clip.samples);
            PcmInstrumentalEvidence.Validate(notes, pcm, clip.frequency, clip.channels, clip.samples);
            return new Inputs { record = record, clip = clip, voice = voice, notes = notes };
        }
        private static void ValidateReport(Manifest m, Source source, Inputs inputs, Report report, bool candidate)
        {
            Require(report != null && report.stage == (candidate ? "candidate" : "baseline") && report.referenceId == source.id
                && report.referenceMapSha256 == source.referenceMapSha256 && report.sourceAudioSha256 == source.sourceFileSha256
                && report.sourcePcmSha256 == inputs.voice.sourcePcmSha256 && report.voiceFeatureSha256 == inputs.voice.featureSha256
                && report.instrumentFeatureSha256 == inputs.notes.featureSha256 && report.voiceAnalysisVersion == inputs.voice.analysisVersion
                && report.instrumentAnalysisVersion == inputs.notes.analysisVersion && report.generator == (candidate ? m.candidateGenerator : m.baselineGenerator)
                && Close(report.generatedDelayTime, m.preroll) && Close(report.generatedForwardSpeed, inputs.record.forwardSpeed), "Report differs from pinned source/PCM/current evidence/version/options: " + source.id);
        }
        private static void SealBaseline(Manifest m, Source source, string folder, Report report)
        {
            string path = m.studyDirectory + "/baseline-source-inputs.json";
            var seals = File.Exists(path) ? Read<BaselineSeals>(path) : new BaselineSeals();
            Require(seals?.selection != null && !seals.selection.Any(s => s.id == source.id), "Never replace an immutable baseline seal.");
            seals.selection = seals.selection.Concat(new[] { new BaselineSeal { id = source.id, inputs = report,
                reportSha256 = Hash(folder + "/baseline.json"), typedSha256 = Hash(folder + "/baseline.typed.json"), timelineSha256 = Hash(folder + "/baseline.generated.timeline.json") } }).ToArray();
            Write(path, seals);
        }
        private static bool HasBaselineState(Manifest m, Source source, string folder)
        {
            if (BaselinePaths(folder).Any(File.Exists) || File.Exists(folder + "/baseline.failure.json")) return true;
            string path = m.studyDirectory + "/baseline-source-inputs.json";
            if (!File.Exists(path)) return false;
            var seals = Read<BaselineSeals>(path)?.selection;
            Require(seals != null && seals.All(s => s != null), "Baseline provenance is malformed; never infer a replacement baseline.");
            return seals.Any(s => s.id == source.id);
        }
        private static void ValidateBaseline(Manifest m, Source source, Inputs inputs, string folder)
        {
            Require(BaselinePaths(folder).All(File.Exists) && !File.Exists(folder + "/baseline.failure.json"), "Immutable baseline is incomplete or has a failure marker; never regenerate or repair silently.");
            var seals = Read<BaselineSeals>(m.studyDirectory + "/baseline-source-inputs.json");
            Require(seals?.selection != null && seals.selection.All(s => s != null) && seals.selection.Select(s => s.id).Distinct().Count() == seals.selection.Length,
                "Baseline provenance must contain unique immutable source seals.");
            var seal = seals.selection.SingleOrDefault(s => s.id == source.id);
            Require(seal != null && Hash(folder + "/baseline.json") == seal.reportSha256 && Hash(folder + "/baseline.typed.json") == seal.typedSha256
                && Hash(folder + "/baseline.generated.timeline.json") == seal.timelineSha256, "The captured baseline report/typed/map bytes changed.");
            var prior = Read<Report>(folder + "/baseline.json");
            ValidateReport(m, source, inputs, prior, false);
            Require(JsonUtility.ToJson(prior) == JsonUtility.ToJson(seal.inputs), "Baseline provenance and report inputs disagree.");
            Require(ReportsAgree(prior, Read<PcmChartStudyV9.TypedReport>(folder + "/baseline.typed.json"), seal.timelineSha256), "Typed baseline identity/options/native timeline SHA differ.");
        }
        private static void ValidateBaselineInventory(Manifest m)
        {
            var seals = Read<BaselineSeals>(m.studyDirectory + "/baseline-source-inputs.json")?.selection;
            Require(seals != null && seals.Length == m.sources.Length && seals.All(s => s != null)
                && seals.Select(s => s.id).Distinct(StringComparer.Ordinal).Count() == seals.Length, "Capture all selected immutable baselines before candidate evaluation.");
            foreach (var source in m.sources)
            {
                string folder = Folder(m, source);
                var seal = seals.SingleOrDefault(s => s.id == source.id);
                Require(seal != null && BaselinePaths(folder).All(File.Exists) && !File.Exists(folder + "/baseline.failure.json")
                    && Hash(folder + "/baseline.json") == seal.reportSha256 && Hash(folder + "/baseline.typed.json") == seal.typedSha256
                    && Hash(folder + "/baseline.generated.timeline.json") == seal.timelineSha256, "Complete baseline inventory changed or failed: " + source.id);
            }
        }
        private static bool ReportsAgree(Report report, PcmChartStudyV9.TypedReport typed, string mapHash)
            => report != null && typed != null && typed.stage == report.stage && typed.referenceId == report.referenceId && typed.generator == report.generator
                && typed.referenceMapSha256 == report.referenceMapSha256 && typed.sourceAudioSha256 == report.sourceAudioSha256
                && typed.sourcePcmSha256 == report.sourcePcmSha256 && typed.voiceFeatureSha256 == report.voiceFeatureSha256 && typed.geometryValid == report.geometryValid
                && Close(typed.generatedDelayTime, report.generatedDelayTime) && Close(typed.generatedForwardSpeed, report.generatedForwardSpeed)
                && !string.IsNullOrEmpty(mapHash) && typed.generatedTimelineSha256 == mapHash;
        private static void WriteTyped(SongRecord record, string stage, string folder)
        {
            string path = folder + "/" + stage + ".generated.timeline.json";
            var generated = Read<MapTimeline>(path);
            var reference = Read<MapTimeline>("Assets/Resources/" + record.timelineResource + ".json");
            var r = Read<Report>(folder + "/" + stage + ".json");
            bool Short(MapSegment s) => s.kind == "Hold" && s.endAudioTime - s.audioTime <= .45 + 1e-6;
            var typed = new PcmChartStudyV9.TypedReport { stage = stage, referenceId = record.id, generatedTimelineSha256 = Hash(path), referenceMapSha256 = r.referenceMapSha256,
                sourceAudioSha256 = r.sourceAudioSha256, sourcePcmSha256 = r.sourcePcmSha256, voiceFeatureSha256 = r.voiceFeatureSha256, generator = r.generator,
                generatedDelayTime = r.generatedDelayTime, generatedForwardSpeed = r.generatedForwardSpeed, geometryValid = r.geometryValid,
                shortHolds = PcmChartStudyV9.Match(reference.segments.Where(Short).ToArray(), generated.segments.Where(Short).ToArray(), .08, .08, "Hold <=0.45s on both sides; onset/end <=80ms; IoU>=0.5; maximum-cardinality one-to-one."),
                allHolds = PcmChartStudyV9.Match(reference.segments.Where(s => s.kind == "Hold").ToArray(), generated.segments.Where(s => s.kind == "Hold").ToArray(), .08, .08, "All Hold; onset/end <=80ms; IoU>=0.5; maximum-cardinality one-to-one."),
                follows = PcmChartStudyV9.Match(reference.segments.Where(s => s.kind == "Follow").ToArray(), generated.segments.Where(s => s.kind == "Follow").ToArray(), .08, .15, "Follow; onset <=80ms; end <=150ms; IoU>=0.5; maximum-cardinality one-to-one.") };
            Write(folder + "/" + stage + ".typed.json", typed);
        }
        public static void ValidateFrozenInputs()
        {
            var m = LoadManifest();
            ValidateSelection(m);
            ValidateBaselineInventory(m);
            var freeze = Read<Freeze>(m.studyDirectory + "/freeze.json");
            Require(freeze != null && freeze.beforeValidation && DateTimeOffset.TryParse(freeze.frozenAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _), "Record algorithm freeze before opening heldout candidates.");
            ValidateFreezeCoverage(m, freeze);
            var all = freeze.algorithm.Concat(freeze.measurements).Concat(freeze.baselines).Concat(freeze.manifests).ToArray();
            string repo = Path.GetFullPath("..");
            Require(all.All(e => MatchesFrozenFile(e, repo)), "Algorithm, measurements, baseline or manifest changed after freeze.");
        }
        private static void ValidateFreezeCoverage(Manifest m, Freeze freeze)
        {
            Require(freeze.algorithm != null && freeze.algorithm.Length > 0 && freeze.measurements != null && freeze.baselines != null && freeze.manifests != null, "Freeze algorithm, all measurements, all baseline triples and manifests.");
            var all = freeze.algorithm.Concat(freeze.measurements).Concat(freeze.baselines).Concat(freeze.manifests).ToArray();
            Require(all.All(e => e != null) && all.Select(e => e.path).Distinct(StringComparer.Ordinal).Count() == all.Length, "Frozen paths must be non-null and unique.");
            string prefix = Path.GetFileName(Path.GetFullPath(".")) + "/";
            bool Has(FrozenHash[] group, string path) => group.Any(e => e.path == prefix + path && !e.normalizedLf);
            Require(freeze.baselines.Length == m.sources.Length * 3 && m.sources.All(s => BaselinePaths(Folder(m, s)).All(p => Has(freeze.baselines, p))), "Freeze must bind exactly three baseline artifacts per selected source.");
            Require(m.algorithmPaths.All(p => freeze.algorithm.Any(e => e.path == p)), "Freeze every algorithm/measurement/study source pinned by the tracked manifest.");
            Require(freeze.measurements.Length == m.sources.Length * 2 && m.sources.All(s => Has(freeze.measurements, "Assets/Clone/Editor/AudioEvidence/" + s.sourceFileSha256 + ".voice.json")
                && Has(freeze.measurements, "Assets/Clone/Editor/InstrumentEvidence/" + s.sourceFileSha256 + ".notes.json")), "Freeze every selected current measurement file.");
            Require(new[] { CurrentManifest, m.selectionPath, m.studyDirectory + "/baseline-source-inputs.json", m.studyDirectory + "/before/protected-files.json",
                m.studyDirectory + "/before/songs.json" }.All(p => Has(freeze.manifests, p)), "Freeze the study, selection, baseline provenance and initial protected-file/catalog snapshots.");
        }
        private static bool MatchesFrozenFile(FrozenHash entry, string repo)
        {
            if (entry == null || string.IsNullOrEmpty(entry.path) || !IsHash(entry.sha256)) return false;
            string path = Path.GetFullPath(Path.Combine(repo, entry.path));
            if (!path.StartsWith(repo.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return false;
            byte[] data = File.ReadAllBytes(path);
            if (entry.normalizedLf) data = data.Where((b, i) => b != 13 || i + 1 == data.Length || data[i + 1] != 10).ToArray();
            return HashBytes(data) == entry.sha256;
        }
        private static void ValidateSelection(Manifest m)
        {
            var selection = Read<Selection>(m.selectionPath)?.selection;
            Require(selection != null && selection.Length == m.sources.Length && selection.All(s => s != null)
                && m.sources.All(s => selection.Count(t => t.id == s.id && t.role == s.role && t.sourceFile == s.sourceFile
                    && t.sourceFileSha256 == s.sourceFileSha256 && t.referenceMapSha256 == s.referenceMapSha256) == 1), "Tracked manifest and aggregate source selection disagree.");
        }
        private static void Guard(Source source, Action<SongRecord> action)
        {
            var record = RecoveredCatalog.LoadOriginal().Single(r => r.id == source.id);
            var clip = record.LoadAudio();
            string audio = AssetDatabase.GetAssetPath(clip), map = "Assets/Resources/" + record.timelineResource + ".json";
            Require(clip != null && audio == source.sourceFile && Hash(audio) == source.sourceFileSha256 && Hash(map) == source.referenceMapSha256, "Original source differs from selected identity: " + source.id);
            var before = new[] { audio, audio + ".meta", map, map + ".meta" }.Where(File.Exists).ToDictionary(p => p, Hash);
            try { action(record); }
            finally { Require(before.All(p => File.Exists(p.Key) && Hash(p.Key) == p.Value), "Original source changed during the study."); }
        }
        private static string DecodedPcmHash(AudioClip clip)
        {
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            Require(clip.loadState == AudioDataLoadState.Loaded, "Actual decoded PCM has not finished loading.");
            var pcm = new float[checked(clip.samples * clip.channels)];
            Require(clip.GetData(pcm, 0), "Actual decoded PCM is unavailable.");
            var bytes = new byte[checked(pcm.Length * sizeof(float))];
            Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian) for (int i = 0; i < bytes.Length; i += sizeof(float)) Array.Reverse(bytes, i, sizeof(float));
            return HashBytes(bytes);
        }
        private static string Folder(Manifest m, Source s) => m.studyDirectory + "/benchmark/" + s.id;
        private static string[] BaselinePaths(string folder) => new[] { folder + "/baseline.json", folder + "/baseline.typed.json", folder + "/baseline.generated.timeline.json" };
        private static T Read<T>(string path) => JsonUtility.FromJson<T>(File.ReadAllText(path));
        private static void Write<T>(string path, T value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(value, true)); }
        private static bool Close(float a, float b) => Math.Abs(a - b) < 1e-6;
        private static bool IsHash(string s) => s != null && s.Length == 64 && s.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        private static string Hash(string path) => HashBytes(File.ReadAllBytes(path));
        private static string HashBytes(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void RequireEditor() => Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating, "Use the compiled Editor in Edit Mode.");
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("CHART_GENERATOR_STUDY FAIL: " + message); }
    }
}
