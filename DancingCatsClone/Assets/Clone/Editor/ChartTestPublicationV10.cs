using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DancingCats.Clone.Editor
{
    /// <summary>Publishes only the six explicitly separate study levels after their frozen evaluation.</summary>
    public static class ChartTestPublicationV10
    {
        private const string CatalogPath = "Assets/Resources/CustomData/songs.json";
        private const string MapFolder = "Assets/Resources/CustomData/Maps/";
        private const string Study = "Library/ChartGenerationV10";
        private const float Preroll = .6f;
        private sealed class Source
        {
            public readonly string id, mapHash, audioHash;
            public string TestId => id + "GeneratorTestV10";
            public Source(string id, string mapHash, string audioHash)
            { this.id = id; this.mapHash = mapHash; this.audioHash = audioHash; }
        }
        private static readonly Source[] Sources =
        {
            new Source("DisconnectPop", "2fdf744d7ad60b2df044a803cd5ef3877f6615f4b2c2938864864822f9f11b2a", "50e457b74a6c5189494d99db4c3b8cac6480c937e43cca03a270d71a39272c5f"),
            new Source("CanCanPiano", "2e04a04cc56346f6a4c6e5f832df5a55eb1abd52b16d1d1f61aee5120a6ee081", "8f53952f9f5002ed5f6b313ab170bb317e1494605d3cad8b183a0e78353ddcd3"),
            new Source("IJustWannaDancePop", "9283d289245f52a5e0ce3a29c61a46fcd16f84298a96e81ccceaf69fdaa1b376", "bea4e85a21807ee88b90b745209a8057dedaf9a0fea82671cb0f8056f87ccc40"),
            new Source("MoonlightPop", "ae158d469109c32c621cbe915062e3ff5129abe1c1001343f557f8ba0e7b3804", "35cb4620f0b6436f8c918538ecc6e3e81ef6180a2fb4ba4fd39931156909b843"),
            new Source("FurElisePiano", "9220f268ee214ddcdde5fffa2758ee845a8fe9889938de93276976e32095097b", "6adc81188a4c82e24ee1f04ea875dc95338630de98199b4475cbb7a15d88f573"),
            new Source("JingleBells", "26012df75d3718e3065653d85757d2a42894ec8a6c0bc89723ad30bb1b23c5cb", "f83d9bf3ccb1bb49b31736d8bd72477cb344ababff68e0801dce1b4d2e9eaa7c")
        };
        [Serializable] private sealed class FreezePermit { public bool beforeValidation = false; public string frozenAtUtc = ""; }
        [Serializable] private sealed class Candidate
        {
            public string stage = "", referenceId = "", referenceMapSha256 = "", sourceAudioSha256 = "", sourcePcmSha256 = "", voiceFeatureSha256 = "", instrumentFeatureSha256 = "", generator = "";
            public float generatedForwardSpeed = 0, generatedDelayTime = 0;
            public bool geometryValid = false;
        }
        [Serializable] public sealed class PublishedTest
        {
            public string sourceId, id, audioResource, timelineResource, mapSha256, generator;
            public int catalogId, elements, jumps, bigJumps, holds, shortHolds, follows, movingTargets;
            public bool geometryValid;
        }
        [Serializable] public sealed class PublicationReport
        {
            public string publishedAtUtc, frozenAtUtc;
            public int protectedOriginalFiles, protectedExistingMaps, protectedCatalogRecords;
            public bool originalsUnchanged, existingMapsUnchanged, existingRecordsUnchanged;
            public PublishedTest[] tests;
        }
        private sealed class Pending
        {
            public Source source;
            public MapTimeline map;
            public AudioClip clip;
            public AudioChartGenerator.Options options;
        }

        public static void PublishTests()
        {
            PcmChartStudyV10.ValidateFrozenInputs();
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating,
                "Publish study tests in Edit Mode after compilation and imports finish.");
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Save the open scene before publishing study tests.");
            string freezePath = Study + "/freeze.json";
            var freeze = File.Exists(freezePath) ? JsonUtility.FromJson<FreezePermit>(File.ReadAllText(freezePath)) : null;
            Require(freeze != null && freeze.beforeValidation && DateTimeOffset.TryParse(freeze.frozenAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
                "The frozen study must be explicitly evaluated before test publication. Root verification must also confirm its source hashes.");
            var originals = RecoveredCatalog.LoadOriginal();
            byte[] catalogBefore = File.ReadAllBytes(CatalogPath);
            string catalogText = File.ReadAllText(CatalogPath);
            var customs = RecoveredCatalog.FromJsonArray<SongRecord>(catalogText);
            var allowedIds = new HashSet<string>(Sources.Select(s => s.TestId), StringComparer.Ordinal);
            var oldRecords = CatalogObjects(catalogText).ToDictionary(text => JsonUtility.FromJson<SongRecord>(text).id, text => text, StringComparer.Ordinal);
            Require(oldRecords.Count == customs.Length, "Catalog JSON fragments and parsed record counts differ.");
            var protectedRecords = oldRecords.Where(pair => !allowedIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var originalFiles = Directory.GetFiles("Assets/Resources/OriginalData", "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Resources/OriginalAudio", "*", SearchOption.AllDirectories)).ToDictionary(path => path, Hash);
            var protectedMaps = Directory.GetFiles(MapFolder, "*", SearchOption.AllDirectories)
                .Where(path => !Sources.Any(s => path.Replace('\\', '/').EndsWith("/" + s.TestId + ".timeline.json", StringComparison.Ordinal)
                    || path.Replace('\\', '/').EndsWith("/" + s.TestId + ".timeline.json.meta", StringComparison.Ordinal)))
                .ToDictionary(path => path, Hash);
            var rollback = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var pending = new List<Pending>();
            var workingCatalog = customs.ToList();
            foreach (var source in Sources)
            {
                var record = originals.Single(s => s.id == source.id);
                var clip = record.LoadAudio();
                Require(clip != null, "Study source audio is missing: " + source.id);
                string referencePath = "Assets/Resources/" + record.timelineResource + ".json";
                string audioPath = AssetDatabase.GetAssetPath(clip);
                Require(Hash(referencePath) == source.mapHash && Hash(audioPath) == source.audioHash, "Study source identity changed: " + source.id);
                string folder = Study + "/benchmark/" + source.id;
                Require(File.Exists(folder + "/candidate.json") && File.Exists(folder + "/candidate.typed.json"), "Evaluate every study candidate before publishing tests: " + source.id);
                var candidate = JsonUtility.FromJson<Candidate>(File.ReadAllText(folder + "/candidate.json"));
                Require(candidate != null && candidate.geometryValid && candidate.referenceId == source.id
                    && candidate.referenceMapSha256 == source.mapHash && candidate.sourceAudioSha256 == source.audioHash
                    && Math.Abs(candidate.generatedForwardSpeed - record.forwardSpeed) < 1e-6 && Math.Abs(candidate.generatedDelayTime - Preroll) < 1e-6,
                    "Candidate evaluation is invalid or uses different source/options: " + source.id);
                var typed = JsonUtility.FromJson<PcmChartStudyV9.TypedReport>(File.ReadAllText(folder + "/candidate.typed.json"));
                Require(ReportsAgree(candidate, typed, Hash(folder + "/candidate.generated.timeline.json"),
                    File.Exists(folder + "/candidate.failure.json")), "Typed gesture evaluation is stale or failed: " + source.id);
                var evidence = PcmVocalAnalysis.LoadForClip(clip);
                Require(evidence != null && evidence.sourcePcmSha256 == candidate.sourcePcmSha256 && evidence.featureSha256 == candidate.voiceFeatureSha256,
                    "Install the evaluated source-verified Editor vocal cache before test publication: " + source.id);
                var instrument = PcmInstrumentalEvidence.LoadForClip(clip);
                Require(instrument != null && instrument.featureSha256 == candidate.instrumentFeatureSha256
                    && instrument.sourcePcmSha256 == candidate.sourcePcmSha256, "Install exactly the evaluated mixed-note measurements before publication.");
                int catalogId = workingCatalog.FirstOrDefault(s => s.id == source.TestId)?.catalogId ?? 30011;
                while (workingCatalog.Any(s => s.id != source.TestId && s.catalogId == catalogId) || originals.Any(s => s.catalogId == catalogId)) catalogId++;
                Require(catalogId >= 30011, "Study test identifiers must use the dedicated catalog range.");
                catalogId = AudioChartGenerator.ResolveCatalogId(source.TestId, originals, workingCatalog.ToArray(), catalogId);
                var options = new AudioChartGenerator.Options { catalogId = catalogId, title = record.title + " · тест v10", artist = record.artist,
                    forwardSpeed = record.forwardSpeed, delayTime = Preroll, preferAuthoredScore = false,
                    includeExperimentalShortSupports = true };
                AudioChartGenerator.ValidateOptions(source.TestId, options);
                var map = AudioChartGenerator.Generate(clip, source.TestId, options);
                AudioChartGeneratorValidation.ValidateMap(map);
                Require(map.sourceMapBundle == candidate.generator && map.sourceMapBundleSha256 == candidate.sourcePcmSha256
                    && map.audio.rawSha256 == source.audioHash, "Current generation differs from its evaluated algorithm/PCM: " + source.id);
                var evaluated = JsonUtility.FromJson<MapTimeline>(File.ReadAllText(folder + "/candidate.generated.timeline.json"));
                Require(SameEventsAndSupports(evaluated, map), "Current test event samples or support intervals differ from the evaluated candidate: " + source.id);
                string target = MapFolder + source.TestId + ".timeline.json";
                foreach (string path in new[] { target, target + ".meta" }) rollback.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
                pending.Add(new Pending { source = source, map = map, clip = clip, options = options });
                if (!workingCatalog.Any(s => s.id == source.TestId)) workingCatalog.Add(new SongRecord { id = source.TestId, catalogId = catalogId });
            }
            try
            {
                foreach (var item in pending) AudioChartGenerator.Save(item.map, item.clip, item.options);
                // Preserve every non-test catalog object verbatim; Save otherwise reserializes old doubles.
                var updated = CatalogObjects(File.ReadAllText(CatalogPath));
                var finalObjects = updated.Select(text =>
                {
                    string id = JsonUtility.FromJson<SongRecord>(text).id;
                    return protectedRecords.TryGetValue(id, out var originalText) ? originalText : text;
                }).ToArray();
                File.WriteAllText(CatalogPath, "[\n" + string.Join(",\n", finalObjects) + "\n]\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                var finalRecords = CatalogObjects(File.ReadAllText(CatalogPath)).ToDictionary(text => JsonUtility.FromJson<SongRecord>(text).id, text => text, StringComparer.Ordinal);
                Require(protectedRecords.All(pair => finalRecords.TryGetValue(pair.Key, out var value) && value == pair.Value), "An existing non-test catalog record changed.");
                Require(originalFiles.All(pair => File.Exists(pair.Key) && Hash(pair.Key) == pair.Value), "An original map or audio changed.");
                Require(protectedMaps.All(pair => File.Exists(pair.Key) && Hash(pair.Key) == pair.Value), "An existing non-test custom map changed.");
                var merged = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(CatalogPath));
                Require(merged.Length == customs.Length + Sources.Count(s => !oldRecords.ContainsKey(s.TestId)), "Publication created unexpected catalog records.");
                var proof = new PublicationReport { publishedAtUtc = DateTimeOffset.UtcNow.ToString("O"), frozenAtUtc = freeze.frozenAtUtc,
                    protectedOriginalFiles = originalFiles.Count, protectedExistingMaps = protectedMaps.Count, protectedCatalogRecords = protectedRecords.Count,
                    originalsUnchanged = true, existingMapsUnchanged = true, existingRecordsUnchanged = true,
                    tests = pending.Select(item => new PublishedTest { sourceId = item.source.id, id = item.map.id, catalogId = item.options.catalogId,
                        audioResource = merged.Single(s => s.id == item.map.id).audioResource, timelineResource = "CustomData/Maps/" + item.map.id + ".timeline",
                        mapSha256 = Hash(MapFolder + item.map.id + ".timeline.json"), generator = item.map.sourceMapBundle, geometryValid = true,
                        elements = item.map.elements.Length, jumps = item.map.segments.Count(s => s.kind == "Jump"), bigJumps = item.map.segments.Count(s => s.kind == "BigJump"),
                        holds = item.map.segments.Count(s => s.kind == "Hold"), shortHolds = item.map.segments.Count(s => s.kind == "Hold" && s.duration <= .45 + 1e-6),
                        follows = item.map.segments.Count(s => s.kind == "Follow"), movingTargets = item.map.elements.Count(e => e.updateType == 3) }).ToArray() };
                Directory.CreateDirectory(Study);
                File.WriteAllText(Study + "/publication.json", JsonUtility.ToJson(proof, true));
            }
            catch
            {
                foreach (var pair in rollback)
                    if (pair.Value != null) File.WriteAllBytes(pair.Key, pair.Value);
                    else if (File.Exists(pair.Key)) File.Delete(pair.Key);
                File.WriteAllBytes(CatalogPath, catalogBefore);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
            Debug.Log("CHART_TEST_PUBLICATION_V10 PASS: six separate test maps; originals, existing non-test maps and catalog objects unchanged.");
        }

        private static bool SameEventsAndSupports(MapTimeline a, MapTimeline b)
        {
            if (a == null || b == null) return false;
            // Normalize both sides through Unity's persisted representation:
            // absent strings/arrays may deserialize as empty defaults.
            a = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(a));
            b = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(b));
            // Test identity and AudioClip asset metadata intentionally differ.
            // Every native movement, timing and scoring field must still agree.
            return a != null && b != null && a.schemaVersion == b.schemaVersion && a.sampleRate == b.sampleRate
                && SameValue(a.forwardSpeed, b.forwardSpeed) && SameValue(a.delayTime, b.delayTime)
                && SameValue(a.tempoSections, b.tempoSections) && SameValue(a.events, b.events)
                && SameValue(a.elements, b.elements) && SameValue(a.segments, b.segments);
        }
        private static bool SameValue(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a.GetType() != b.GetType()) return false;
            if (a is double d) return Math.Abs(d - (double)b) <= 1e-8;
            if (a is float f) return Math.Abs(f - (float)b) <= 1e-5;
            var type = a.GetType();
            if (type.IsPrimitive || type.IsEnum || a is string) return a.Equals(b);
            if (a is Array array)
            {
                var other = (Array)b;
                return array.Length == other.Length && Enumerable.Range(0, array.Length).All(i => SameValue(array.GetValue(i), other.GetValue(i)));
            }
            return type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .All(field => SameValue(field.GetValue(a), field.GetValue(b)));
        }

        /// <summary>Extracts top-level catalog objects without rewriting their nested JSON or numbers.</summary>
        public static string[] CatalogObjects(string json)
        {
            var objects = new List<string>();
            int depth = 0, start = -1; bool quoted = false, escaped = false;
            foreach (int i in Enumerable.Range(0, json.Length))
            {
                char value = json[i];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (value == '\\') escaped = true;
                    else if (value == '"') quoted = false;
                    continue;
                }
                if (value == '"') { quoted = true; continue; }
                if (value == '{') { if (depth++ == 0) start = i; }
                else if (value == '}')
                {
                    Require(depth > 0, "Malformed catalog JSON object.");
                    if (--depth == 0) objects.Add(json.Substring(start, i - start + 1));
                }
            }
            Require(!quoted && depth == 0, "Unterminated catalog JSON object or string.");
            return objects.ToArray();
        }
        public static void RunGuardChecks()
        {
            string first = "{\"id\":\"Old\",\"title\":\"brace } and \\\"quote\\\"\",\"finish\":[{\"t\":1.0000000000000002}]}";
            string second = "{\"id\":\"New\",\"nested\":{\"value\":2}}";
            Require(CatalogObjects("[\n" + first + ",\n" + second + "\n]").SequenceEqual(new[] { first, second }), "Catalog protection must retain exact object text, escaped quotes and nested braces.");
            bool rejected = false;
            try { CatalogObjects("[{\"id\":\"broken\""); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Malformed catalog JSON must be rejected before publication.");
            Require(Sources.Select(s => s.TestId).Distinct(StringComparer.Ordinal).Count() == 6 && Sources.All(s => s.TestId.EndsWith("GeneratorTestV10", StringComparison.Ordinal)),
                "Publication scope must be exactly the six study test identifiers.");
            var candidate = new Candidate { stage = "candidate", referenceId = "Fixture", referenceMapSha256 = "map",
                sourceAudioSha256 = "audio", sourcePcmSha256 = "pcm", voiceFeatureSha256 = "voice", generator = "v10",
                generatedDelayTime = .6f, generatedForwardSpeed = 7, geometryValid = true };
            var typed = new PcmChartStudyV9.TypedReport { stage = "candidate", referenceId = "Fixture", referenceMapSha256 = "map",
                sourceAudioSha256 = "audio", sourcePcmSha256 = "pcm", voiceFeatureSha256 = "voice", generator = "v10",
                generatedDelayTime = .6f, generatedForwardSpeed = 7, geometryValid = true, generatedTimelineSha256 = "timeline" };
            Require(ReportsAgree(candidate, typed, "timeline", false), "Matching successful typed evaluation is accepted.");
            typed.stage = "baseline";
            Require(!ReportsAgree(candidate, typed, "timeline", false), "A stale baseline typed report must not authorize publication.");
            typed.stage = "candidate";
            Require(!ReportsAgree(candidate, typed, "different", false), "Typed results must bind the exact generated timeline.");
            Require(!ReportsAgree(candidate, typed, "timeline", true), "A failed candidate rerun must block stale result publication.");
            typed.generator = "old";
            Require(!ReportsAgree(candidate, typed, "timeline", false), "Typed results from a different generator must be rejected.");
            var payload = new MapTimeline { id = "evaluation", sampleRate = 1000, forwardSpeed = 7, delayTime = .6f,
                elements = new[] { new MapElement { holdTicks = new[] { new HoldTick { audioTime = 1.2 } }, se = new Vector2(.7f, -.7f) } },
                segments = new[] { new MapSegment { elementIndices = new[] { 0 }, motion = new MotionParameters { mfendPos = 2 }, path = Array.Empty<ChartPoint>() } },
                events = new[] { new KoreographyEvent { mStartSample = 1000 } }, tempoSections = Array.Empty<TempoSection>() };
            MapTimeline Copy() => JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(payload));
            var copy = Copy(); copy.id = "new-test";
            Require(SameEventsAndSupports(payload, copy), "A separately named test may retain identical native gameplay.");
            copy.elements[0].holdTicks[0].audioTime += .01;
            Require(!SameEventsAndSupports(payload, copy), "A changed scoring tick must invalidate publication.");
            copy = Copy(); copy.elements[0].se.x += .1f;
            Require(!SameEventsAndSupports(payload, copy), "A changed native sideways range must invalidate publication.");
            copy = Copy(); copy.segments[0].motion.mfendPos += 1;
            Require(!SameEventsAndSupports(payload, copy), "A changed segment motion payload must invalidate publication.");
            copy = Copy(); copy.segments[0].elementIndices[0] = 1;
            Require(!SameEventsAndSupports(payload, copy), "Changed native element membership must invalidate publication.");
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                object shared = invalid;
                Require(!SameValue(shared, shared), "Even a shared non-finite scoring value must be rejected.");
            }
            Require(!SameValue(new[] { 1 }, new[] { 1, 2 }), "Different native payload lengths must be rejected.");
            Debug.Log("CHART_TEST_PUBLICATION_V10_GUARDS PASS: exact existing catalog objects, nested/escaped JSON, malformed-input rejection and fixed six-test scope.");
        }
        private static bool ReportsAgree(Candidate candidate, PcmChartStudyV9.TypedReport typed, string timelineHash, bool failed)
            => !failed && candidate != null && candidate.stage == "candidate" && typed != null && typed.stage == "candidate"
                && typed.geometryValid && typed.referenceId == candidate.referenceId && typed.generator == candidate.generator
                && typed.referenceMapSha256 == candidate.referenceMapSha256 && typed.sourceAudioSha256 == candidate.sourceAudioSha256
                && typed.voiceFeatureSha256 == candidate.voiceFeatureSha256 && typed.sourcePcmSha256 == candidate.sourcePcmSha256
                && Math.Abs(typed.generatedForwardSpeed - candidate.generatedForwardSpeed) < 1e-6
                && Math.Abs(typed.generatedDelayTime - candidate.generatedDelayTime) < 1e-6
                && !string.IsNullOrEmpty(timelineHash) && typed.generatedTimelineSha256 == timelineHash;
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("CHART_TEST_PUBLICATION_V10 FAIL: " + message); }
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    }
}
