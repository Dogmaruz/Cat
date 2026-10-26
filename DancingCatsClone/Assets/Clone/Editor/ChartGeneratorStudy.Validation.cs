using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static partial class ChartGeneratorStudy
    {
        public static void RunChecks()
        {
            var manifest = LoadManifest();
            ValidateSelection(manifest);
            PcmChartStudyV9.RunMetricChecks();
            CheckBaselineGuards(manifest);
            CheckFrozenHashGuards(manifest);
            CheckFreezeCoverageGuards(manifest);
            CheckInitialProtectionGuards(manifest);
            CheckNativePayloadGuards();
            CheckPublicationRepairProofGuards(manifest);
            Require(UsesPublicationRepair(PublicationRepairFreezeSha256), "The exact recorded v11 freeze must always use its mandatory publication repair guard.");
            Require(!UsesPublicationRepair(new string('a', 64)) && !UsesPublicationRepair(PublicationRepairFreezeSha256.Substring(0, 63) + "0")
                && !UsesPublicationRepair(null), "Future or different freeze identities must use strict freeze validation, never the v11 repair guard.");
            foreach (int count in new[] { 1, Math.Min(3, manifest.sources.Length), manifest.sources.Length })
            {
                var varied = JsonUtility.FromJson<Manifest>(JsonUtility.ToJson(manifest));
                varied.sources = varied.sources.Take(count).ToArray();
                varied.publishSourceIds = new[] { varied.sources[0].id };
                ValidateManifest(varied);
            }
            var unsafeManifest = JsonUtility.FromJson<Manifest>(JsonUtility.ToJson(manifest));
            unsafeManifest.studyDirectory = "../escape";
            Reject(() => ValidateManifest(unsafeManifest), "manifest output containment");
            unsafeManifest = JsonUtility.FromJson<Manifest>(JsonUtility.ToJson(manifest));
            unsafeManifest.publishSourceIds = new[] { "NotSelected" };
            Reject(() => ValidateManifest(unsafeManifest), "unselected publication source");
            Debug.Log("CHART_GENERATOR_STUDY_CHECKS PASS: variable manifest counts; immutable baseline bytes/identity/evidence/options; frozen hashes; publication doubles <=2ULP, floats/discrete payload exact.");
        }
        private static void CheckBaselineGuards(Manifest current)
        {
            var m = JsonUtility.FromJson<Manifest>(JsonUtility.ToJson(current));
            m.studyDirectory += "/GuardFixtures/" + Guid.NewGuid().ToString("N");
            var s = new Source { id = "Fixture", referenceMapSha256 = "reference", sourceFileSha256 = "audio" };
            string folder = Folder(m, s);
            var inputs = new Inputs { record = new SongRecord { forwardSpeed = 8 },
                voice = new PcmVocalAnalysis.Evidence { sourcePcmSha256 = "pcm", featureSha256 = "voice", analysisVersion = "voice-version" },
                notes = new PcmInstrumentalEvidence.Evidence { featureSha256 = "notes", analysisVersion = "note-version" } };
            var report = new Report { stage = "baseline", referenceId = s.id, referenceMapSha256 = "reference", sourceAudioSha256 = "audio",
                sourcePcmSha256 = "pcm", voiceFeatureSha256 = "voice", instrumentFeatureSha256 = "notes", voiceAnalysisVersion = "voice-version",
                instrumentAnalysisVersion = "note-version", generator = m.baselineGenerator, generatedDelayTime = .6f, generatedForwardSpeed = 8, geometryValid = true };
            var typed = new PcmChartStudyV9.TypedReport { stage = "baseline", referenceId = s.id, referenceMapSha256 = "reference", sourceAudioSha256 = "audio",
                sourcePcmSha256 = "pcm", voiceFeatureSha256 = "voice", generator = m.baselineGenerator, generatedDelayTime = .6f, generatedForwardSpeed = 8, geometryValid = true };
            void Reset()
            {
                Directory.CreateDirectory(folder);
                string seal = m.studyDirectory + "/baseline-source-inputs.json";
                if (File.Exists(seal)) File.Delete(seal);
                string failed = folder + "/baseline.failure.json";
                if (File.Exists(failed)) File.Delete(failed);
                inputs.voice.sourcePcmSha256 = "pcm"; inputs.voice.featureSha256 = "voice"; inputs.notes.featureSha256 = "notes";
                inputs.record.forwardSpeed = 8;
                File.WriteAllText(folder + "/baseline.generated.timeline.json", "fixture native map");
                typed.stage = "baseline"; typed.generatedTimelineSha256 = Hash(folder + "/baseline.generated.timeline.json");
                Write(folder + "/baseline.json", report); Write(folder + "/baseline.typed.json", typed);
                SealBaseline(m, s, folder, report);
            }
            void Check(Action change, string reason) { Reset(); change(); Reject(() => ValidateBaseline(m, s, inputs, folder), reason); }
            try
            {
                Reset(); ValidateBaseline(m, s, inputs, folder);
                Check(() => File.Delete(folder + "/baseline.generated.timeline.json"), "missing native baseline");
                Check(() => File.Delete(folder + "/baseline.typed.json"), "missing typed baseline");
                Reset(); foreach (string path in BaselinePaths(folder)) File.Delete(path);
                Require(HasBaselineState(m, s, folder), "An existing seal must reach baseline validation even when all three artifacts are absent.");
                Reject(() => ValidateBaseline(m, s, inputs, folder), "all three missing artifacts with retained seal");
                Reset(); foreach (string path in BaselinePaths(folder)) File.Delete(path);
                File.Delete(m.studyDirectory + "/baseline-source-inputs.json"); File.WriteAllText(folder + "/baseline.failure.json", "failed");
                Require(HasBaselineState(m, s, folder), "An orphan failure marker must block first-capture inference.");
                Reject(() => ValidateBaseline(m, s, inputs, folder), "orphan baseline failure marker");
                Check(() => File.AppendAllText(folder + "/baseline.generated.timeline.json", "changed"), "changed map bytes");
                Check(() => File.AppendAllText(folder + "/baseline.json", " "), "changed report bytes");
                Check(() => File.AppendAllText(folder + "/baseline.typed.json", " "), "changed typed bytes");
                Check(() => File.WriteAllText(folder + "/baseline.failure.json", "failed"), "stale failure marker");
                Check(() => inputs.voice.sourcePcmSha256 = "other", "changed actual PCM");
                Check(() => inputs.voice.featureSha256 = "other", "changed voice measurements");
                Check(() => inputs.notes.featureSha256 = "other", "changed current instrumental measurements");
                Check(() => inputs.record.forwardSpeed = 9, "changed speed options");
                Reset(); Reject(() => SealBaseline(m, s, folder, report), "baseline seal replacement");
                typed.stage = "candidate";
                Require(!ReportsAgree(report, typed, typed.generatedTimelineSha256), "Candidate typed results must not authorize a baseline.");
                typed.stage = "baseline"; typed.generatedDelayTime = float.NaN;
                Require(!ReportsAgree(report, typed, typed.generatedTimelineSha256), "Non-finite options must be rejected.");
            }
            finally { if (Directory.Exists(m.studyDirectory)) Directory.Delete(m.studyDirectory, true); }
        }
        private static void CheckFrozenHashGuards(Manifest m)
        {
            string folder = m.studyDirectory + "/GuardFixtures/" + Guid.NewGuid().ToString("N"), path = folder + "/freeze.txt";
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("one\r\ntwo\r\n"));
                var entry = new FrozenHash { path = Path.GetFileName(Path.GetFullPath(".")) + "/" + path,
                    sha256 = HashBytes(Encoding.UTF8.GetBytes("one\ntwo\n")), normalizedLf = true };
                Require(MatchesFrozenFile(entry, Path.GetFullPath("..")), "Normalized source hashing must agree with freeze writer.");
                entry.normalizedLf = false;
                Require(!MatchesFrozenFile(entry, Path.GetFullPath("..")), "Raw measurement hashes must retain line-ending bytes.");
                entry.normalizedLf = true; File.AppendAllText(path, "changed");
                Require(!MatchesFrozenFile(entry, Path.GetFullPath("..")), "Changed frozen inputs must be rejected.");
                entry.path = "../outside-repository";
                Require(!MatchesFrozenFile(entry, Path.GetFullPath("..")), "Frozen paths must remain within the repository.");
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        private static void CheckNativePayloadGuards()
        {
            string raw = "{\"id\":\"Old\",\"nested\":{\"value\":1.0000000000000002},\"title\":\"brace }\"}";
            Require(ChartTestPublicationV10.CatalogObjects("[" + raw + "]").Single() == raw, "Old catalog objects must retain exact nested payload bytes.");
            var map = new MapTimeline { id = "evaluation", sampleRate = 1000, forwardSpeed = 8, delayTime = .6f,
                tempoSections = Array.Empty<TempoSection>(), events = new[] { new KoreographyEvent { mStartSample = 1000 } },
                elements = new[] { new MapElement { holdTicks = new[] { new HoldTick { audioTime = 1.2 } }, se = new Vector2(.7f, -.7f) } },
                segments = new[] { new MapSegment { elementIndices = new[] { 0 }, motion = new MotionParameters { mfendPos = 2 }, path = Array.Empty<ChartPoint>() } } };
            MapTimeline Copy() => JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(map));
            var copy = Copy(); copy.id = "SeparateTest";
            Require(SameEventsAndSupports(map, copy), "Separately named tests must preserve identical native gameplay.");
            copy.elements[0].holdTicks[0].audioTime += .000001;
            Require(!SameEventsAndSupports(map, copy), "Changed scoring ticks must invalidate publication.");
            copy = Copy(); copy.elements[0].se.x += .01f;
            Require(!SameEventsAndSupports(map, copy), "Changed sideways range must invalidate publication.");
            copy = Copy(); copy.segments[0].motion.mfendPos++;
            Require(!SameEventsAndSupports(map, copy), "Changed native motion must invalidate publication.");
            copy = Copy(); copy.events[0].mStartSample++;
            Require(!SameEventsAndSupports(map, copy), "Changed sample times must invalidate publication.");
            Require(!SameValue(1.0, 1.0 + 1e-11), "Native comparison must reject even sub-tolerance double changes.");
            double Offset(double value, long ulps) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + ulps);
            foreach (double value in new[] { 1.0, -1.0, 16.0, -16.0 })
            {
                Require(SameValue(value, Offset(value, 1)) && SameValue(value, Offset(value, 2)), "One/two same-sign double ULPs from persistence must be accepted.");
                Require(!SameValue(value, Offset(value, 3)), "Three double ULPs must invalidate publication.");
            }
            Require(!SameValue(0.0, BitConverter.Int64BitsToDouble(long.MinValue)), "Opposite signed zero must not use the same-sign persistence allowance.");
            Require(!SameValue(1.0f, BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(1.0f), 0) + 1), 0)), "Float payloads remain exact even at one ULP.");
            Require(!SameValue(1000, 1001) && !SameValue("Hold", "Follow") && !SameValue(new[] { 0 }, new[] { 1 }), "Discrete native payload changes remain invalid.");
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) Require(!SameValue(bad, bad), "Non-finite native values must be rejected.");
        }
        private static void CheckFreezeCoverageGuards(Manifest m)
        {
            string prefix = Path.GetFileName(Path.GetFullPath(".")) + "/";
            FrozenHash Entry(string path) => new FrozenHash { path = path, sha256 = new string('a', 64) };
            var valid = new Freeze { algorithm = m.algorithmPaths.Select(Entry).ToArray(),
                measurements = m.sources.SelectMany(s => new[] { "AudioEvidence/" + s.sourceFileSha256 + ".voice.json", "InstrumentEvidence/" + s.sourceFileSha256 + ".notes.json" })
                    .Select(p => Entry(prefix + "Assets/Clone/Editor/" + p)).ToArray(),
                baselines = m.sources.SelectMany(s => BaselinePaths(Folder(m, s))).Select(p => Entry(prefix + p)).ToArray(),
                manifests = new[] { CurrentManifest, m.selectionPath, m.studyDirectory + "/baseline-source-inputs.json", m.studyDirectory + "/before/protected-files.json",
                    m.studyDirectory + "/before/songs.json" }.Select(p => Entry(prefix + p)).ToArray() };
            ValidateFreezeCoverage(m, valid);
            Freeze Copy() => JsonUtility.FromJson<Freeze>(JsonUtility.ToJson(valid));
            var bad = Copy(); bad.algorithm = Array.Empty<FrozenHash>(); Reject(() => ValidateFreezeCoverage(m, bad), "empty frozen algorithm");
            bad = Copy(); bad.measurements = Array.Empty<FrozenHash>(); Reject(() => ValidateFreezeCoverage(m, bad), "empty frozen measurements");
            bad = Copy(); bad.baselines = Array.Empty<FrozenHash>(); Reject(() => ValidateFreezeCoverage(m, bad), "empty frozen baselines");
            bad = Copy(); bad.manifests = Array.Empty<FrozenHash>(); Reject(() => ValidateFreezeCoverage(m, bad), "empty frozen manifests");
            bad = Copy(); bad.manifests = bad.manifests.Take(3).ToArray(); Reject(() => ValidateFreezeCoverage(m, bad), "unbound initial protection snapshots");
            bad = Copy(); bad.measurements[0].normalizedLf = true; Reject(() => ValidateFreezeCoverage(m, bad), "normalized measurement bytes");
        }
        private static void CheckInitialProtectionGuards(Manifest current)
        {
            var m = JsonUtility.FromJson<Manifest>(JsonUtility.ToJson(current));
            string folder = m.studyDirectory + "/GuardFixtures/" + Guid.NewGuid().ToString("N"), snapshot = folder + "/protected-files.json";
            string initial = folder + "/before-songs.json", live = folder + "/live-songs.json", protectedFile = folder + "/protected.txt";
            string raw = "{\"id\":\"Old\",\"nested\":{\"value\":1.0000000000000002}}";
            m.protectedCatalogRecordCount = 1;
            void Reset()
            {
                Directory.CreateDirectory(folder); File.WriteAllText(protectedFile, "initial bytes");
                Write(snapshot, new ProtectedFiles { files = new[] { new FrozenHash { path = Path.GetFileName(Path.GetFullPath(".")) + "/" + protectedFile, sha256 = Hash(protectedFile) } } });
                File.WriteAllText(initial, "[" + raw + "]"); File.WriteAllText(live, "[" + raw + "]");
                m.protectedSnapshotSha256 = Hash(snapshot); m.protectedCatalogSha256 = Hash(initial);
            }
            void Validate() => ValidateProtectedSnapshot(m, snapshot, initial, live, Path.GetFullPath(".."));
            void Check(Action change, string reason) { Reset(); change(); Reject(Validate, reason); }
            try
            {
                Reset(); Validate();
                Check(() => File.AppendAllText(protectedFile, "changed before publication"), "changed initial protected file");
                Check(() => File.AppendAllText(snapshot, " "), "changed initial protected inventory");
                Check(() => File.AppendAllText(initial, " "), "changed initial raw catalog snapshot");
                Check(() => File.WriteAllText(live, "[" + raw.Replace("1.0000000000000002", "1.0") + "]"), "changed initial raw catalog object");
                Check(() => File.WriteAllText(live, "[" + raw + ",{\"id\":\"Unapproved\"}]"), "unapproved initial catalog addition");
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        private static void CheckPublicationRepairProofGuards(Manifest m)
        {
            string freezePath = m.studyDirectory + "/freeze.json", prefix = Path.GetFileName(Path.GetFullPath(".")) + "/";
            var freeze = Read<Freeze>(freezePath);
            var metadata = Read<OriginalFreezeMetadata>(freezePath);
            var valid = new PublicationRepairProof { schemaVersion = 1, purpose = PublicationRepairPurpose, createdAtUtc = "2026-10-26T00:00:00+00:00",
                doubleMaxUlps = 2, finiteDoubles = true, sameSignDoubles = true, absoluteEpsilon = 0, floatComparison = "exact", discreteComparison = "exact",
                originalFreezeSha256 = PublicationRepairFreezeSha256, originalFrozenAtUtc = metadata.frozenAtUtc, originalFreezeScope = metadata.scope,
                changedSources = PublicationRepairAllowedPaths.Select(path => new PublicationSourceChange { path = path, normalizedLf = true,
                    beforeSha256 = freeze.algorithm.Single(e => e.path == path).sha256, afterSha256 = new string('b', 64) }).ToArray(),
                guardSource = new FrozenHash { path = PublicationRepairGuardPath, sha256 = new string('b', 64), normalizedLf = true },
                candidates = m.sources.SelectMany(s => new[] { "/candidate.json", "/candidate.typed.json", "/candidate.generated.timeline.json" }
                    .Select(suffix => new FrozenHash { path = prefix + Folder(m, s) + suffix, sha256 = new string('c', 64) })).ToArray() };
            ValidatePublicationRepairStructure(m, freeze, valid, metadata);
            PublicationRepairProof Copy() => JsonUtility.FromJson<PublicationRepairProof>(JsonUtility.ToJson(valid));
            void Check(Action<PublicationRepairProof> change, string reason)
            {
                var bad = Copy(); change(bad); Reject(() => ValidatePublicationRepairStructure(m, freeze, bad, metadata), reason);
            }
            Check(p => p.originalFreezeSha256 = new string('0', 64), "changed original assessment freeze identity");
            Check(p => p.doubleMaxUlps = 3, "widened double persistence permission");
            Check(p => p.absoluteEpsilon = 1e-8, "absolute epsilon publication comparison");
            Check(p => p.floatComparison = "approximate", "non-exact float publication comparison");
            Check(p => p.originalFreezeScope += " changed", "changed original assessment scope");
            Check(p => p.changedSources[0].path = "DancingCatsClone/Assets/Clone/Editor/AudioChartGenerator.cs", "inference source repair permission");
            Check(p => p.changedSources[0].beforeSha256 = new string('0', 64), "incorrect utility before-hash");
            Check(p => p.changedSources = p.changedSources.Take(1).ToArray(), "incomplete utility repair scope");
            Check(p => p.guardSource.path = p.changedSources[0].path, "unbound new repair guard path");
            Check(p => p.candidates = p.candidates.Take(p.candidates.Length - 1).ToArray(), "missing assessed candidate byte proof");
            Check(p => p.candidates[0].normalizedLf = true, "normalized assessed candidate bytes");
            var wrong = Copy(); wrong.guardSource.sha256 = new string('0', 64);
            Require(!MatchesFrozenFile(wrong.guardSource, Path.GetFullPath("..")), "A changed repair guard must fail its actual source hash.");
            wrong.candidates[0].sha256 = new string('0', 64);
            Require(!MatchesFrozenFile(wrong.candidates[0], Path.GetFullPath("..")), "Changed assessed candidate bytes must fail their raw hash.");
        }
        private static void Reject(Action action, string reason)
        {
            bool rejected = false;
            try { action(); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Guard missed " + reason + ".");
        }
    }
}
