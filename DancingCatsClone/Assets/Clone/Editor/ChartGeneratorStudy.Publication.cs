using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DancingCats.Clone.Editor
{
    public static partial class ChartGeneratorStudy
    {
        private const string CatalogPath = "Assets/Resources/CustomData/songs.json", MapFolder = "Assets/Resources/CustomData/Maps/";
        [Serializable] public sealed class PublishedTest
        {
            public string sourceId, id, audioResource, timelineResource, mapSha256, generator;
            public int catalogId, elements, jumps, bigJumps, holds, shortHolds, follows, movingTargets;
            public bool geometryValid;
        }
        [Serializable] public sealed class PublicationReport
        {
            public string publishedAtUtc, frozenAtUtc, originalFreezeSha256, originalFreezeScope, publicationRepairSha256;
            public int protectedOriginalFiles, protectedExistingMaps, protectedCatalogRecords;
            public bool originalsUnchanged, existingMapsUnchanged, existingRecordsUnchanged;
            public PublishedTest[] tests;
        }
        private sealed class Pending
        {
            public Source source;
            public AudioClip clip;
            public MapTimeline map;
            public AudioChartGenerator.Options options;
        }

        public static void PublishTests()
        {
            RequireEditor();
            var m = LoadManifest();
            string freezePath = m.studyDirectory + "/freeze.json", actualFreezeSha256 = Hash(freezePath);
            bool usesPublicationRepair = UsesPublicationRepair(actualFreezeSha256);
            if (usesPublicationRepair) ValidatePublicationFrozenInputs(); else ValidateFrozenInputs();
            ValidateInitialProtection(m);
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Save open scenes before publishing study tests.");
            // Every selected source must have a successful paired evaluation before any playable write.
            foreach (var source in m.sources) Guard(source, record => ValidateCandidate(m, source, ReadInputs(source, record)));
            var originals = RecoveredCatalog.LoadOriginal();
            byte[] catalogBefore = File.ReadAllBytes(CatalogPath);
            var customs = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(CatalogPath));
            var allowedIds = new HashSet<string>(m.publishSourceIds.Select(id => id + m.testSuffix), StringComparer.Ordinal);
            var oldRecords = ChartTestPublicationV10.CatalogObjects(File.ReadAllText(CatalogPath))
                .ToDictionary(text => JsonUtility.FromJson<SongRecord>(text).id, text => text, StringComparer.Ordinal);
            Require(oldRecords.Count == customs.Length, "Catalog parsed records and raw objects differ.");
            var protectedRecords = oldRecords.Where(p => !allowedIds.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var originalFiles = Directory.GetFiles("Assets/Resources/OriginalData", "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Resources/OriginalAudio", "*", SearchOption.AllDirectories)).ToDictionary(p => p, Hash);
            bool IsTarget(string path) => allowedIds.Any(id => path.Replace('\\', '/').EndsWith("/" + id + ".timeline.json", StringComparison.Ordinal)
                || path.Replace('\\', '/').EndsWith("/" + id + ".timeline.json.meta", StringComparison.Ordinal));
            var protectedMaps = Directory.GetFiles(MapFolder, "*", SearchOption.AllDirectories).Where(p => !IsTarget(p)).ToDictionary(p => p, Hash);
            Require(originalFiles.Count == m.protectedOriginalFileCount && protectedMaps.Count == m.protectedCustomMapFileCount
                && protectedRecords.Count == m.protectedCatalogRecordCount, "Protected original/custom/catalog scope differs from the approved manifest.");
            var rollback = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var pending = new List<Pending>();
            var workingCatalog = customs.ToList();
            foreach (string sourceId in m.publishSourceIds)
            {
                var source = m.sources.Single(s => s.id == sourceId);
                Guard(source, record =>
                {
                    string testId = source.id + m.testSuffix;
                    Require(allowedIds.Contains(testId) && !originals.Any(s => s.id == testId), "Only explicitly separate study test identities may be saved.");
                    var inputs = ReadInputs(source, record);
                    var candidate = ValidateCandidate(m, source, inputs);
                    int cid = workingCatalog.FirstOrDefault(s => s.id == testId)?.catalogId ?? m.catalogIdStart;
                    while (workingCatalog.Any(s => s.id != testId && s.catalogId == cid) || originals.Any(s => s.catalogId == cid)) cid++;
                    Require(cid >= m.catalogIdStart, "Study test catalog ID is outside the approved range.");
                    cid = AudioChartGenerator.ResolveCatalogId(testId, originals, workingCatalog.ToArray(), cid);
                    var options = new AudioChartGenerator.Options { catalogId = cid, title = record.title + " · тест " + m.testSuffix.Replace("GeneratorTest", "").ToLowerInvariant(),
                        artist = record.artist, forwardSpeed = record.forwardSpeed, delayTime = m.preroll, preferAuthoredScore = m.preferAuthoredScore,
                        includeExperimentalShortSupports = m.includeExperimentalShortSupports };
                    AudioChartGenerator.ValidateOptions(testId, options);
                    var map = AudioChartGenerator.Generate(inputs.clip, testId, options);
                    AudioChartGeneratorValidation.ValidateMap(map);
                    Require(map.sourceMapBundle == candidate.generator && map.sourceMapBundleSha256 == candidate.sourcePcmSha256
                        && map.audio.rawSha256 == source.sourceFileSha256, "Test generation no longer identifies evaluated algorithm/PCM/audio.");
                    Require(SameEventsAndSupports(Read<MapTimeline>(Folder(m, source) + "/candidate.generated.timeline.json"), map),
                        "Test native movement/timing/scoring payload differs from evaluated candidate: " + source.id);
                    string target = MapFolder + testId + ".timeline.json";
                    foreach (string path in new[] { target, target + ".meta" }) rollback.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
                    pending.Add(new Pending { source = source, clip = inputs.clip, map = map, options = options });
                    if (!workingCatalog.Any(s => s.id == testId)) workingCatalog.Add(new SongRecord { id = testId, catalogId = cid });
                });
            }
            ValidateInitialProtection(m);
            try
            {
                foreach (var item in pending) AudioChartGenerator.Save(item.map, item.clip, item.options);
                foreach (var item in pending) Require(SameEventsAndSupports(item.map, Read<MapTimeline>(MapFolder + item.map.id + ".timeline.json")),
                    "Persisted test native payload differs from the preflight candidate: " + item.source.id);
                // Save serializes the whole catalog. Restore every protected object's exact nested JSON and numbers.
                var saved = ChartTestPublicationV10.CatalogObjects(File.ReadAllText(CatalogPath));
                var finalObjects = saved.Select(text => protectedRecords.TryGetValue(JsonUtility.FromJson<SongRecord>(text).id, out var prior) ? prior : text).ToArray();
                File.WriteAllText(CatalogPath, "[\n" + string.Join(",\n", finalObjects) + "\n]\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                var final = ChartTestPublicationV10.CatalogObjects(File.ReadAllText(CatalogPath)).ToDictionary(text => JsonUtility.FromJson<SongRecord>(text).id, text => text, StringComparer.Ordinal);
                Require(protectedRecords.All(p => final.TryGetValue(p.Key, out var text) && text == p.Value), "An existing catalog object changed.");
                Require(originalFiles.All(p => File.Exists(p.Key) && Hash(p.Key) == p.Value), "An original map/audio/meta changed.");
                Require(protectedMaps.All(p => File.Exists(p.Key) && Hash(p.Key) == p.Value), "An existing custom map/meta changed.");
                Require(final.Keys.All(id => oldRecords.ContainsKey(id) || allowedIds.Contains(id))
                    && final.Count == oldRecords.Count + allowedIds.Count(id => !oldRecords.ContainsKey(id)), "Publication wrote an unapproved catalog identity.");
                var merged = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(CatalogPath));
                Write(m.studyDirectory + "/publication.json", new PublicationReport { publishedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    frozenAtUtc = Read<Freeze>(freezePath).frozenAtUtc, protectedOriginalFiles = originalFiles.Count,
                    originalFreezeSha256 = actualFreezeSha256, originalFreezeScope = Read<OriginalFreezeMetadata>(freezePath).scope,
                    publicationRepairSha256 = usesPublicationRepair ? Hash(m.studyDirectory + "/publication-repair.json") : null,
                    protectedExistingMaps = protectedMaps.Count, protectedCatalogRecords = protectedRecords.Count,
                    originalsUnchanged = true, existingMapsUnchanged = true, existingRecordsUnchanged = true,
                    tests = pending.Select(item => new PublishedTest { sourceId = item.source.id, id = item.map.id, catalogId = item.options.catalogId,
                        audioResource = merged.Single(s => s.id == item.map.id).audioResource, timelineResource = "CustomData/Maps/" + item.map.id + ".timeline",
                        mapSha256 = Hash(MapFolder + item.map.id + ".timeline.json"), generator = item.map.sourceMapBundle, geometryValid = true,
                        elements = item.map.elements.Length, jumps = item.map.segments.Count(s => s.kind == "Jump"), bigJumps = item.map.segments.Count(s => s.kind == "BigJump"),
                        holds = item.map.segments.Count(s => s.kind == "Hold"), shortHolds = item.map.segments.Count(s => s.kind == "Hold" && s.duration <= .45 + 1e-6),
                        follows = item.map.segments.Count(s => s.kind == "Follow"), movingTargets = item.map.elements.Count(e => e.updateType == 3) }).ToArray() });
            }
            catch
            {
                foreach (var pair in rollback) if (pair.Value != null) File.WriteAllBytes(pair.Key, pair.Value); else if (File.Exists(pair.Key)) File.Delete(pair.Key);
                File.WriteAllBytes(CatalogPath, catalogBefore);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
            Debug.Log("CHART_GENERATOR_STUDY_PUBLICATION PASS: " + pending.Count + " explicit " + m.studyId + " tests; originals and old custom payloads preserved.");
        }
        private static bool UsesPublicationRepair(string freezeSha256) => freezeSha256 == PublicationRepairFreezeSha256;
        private static void ValidateInitialProtection(Manifest m) => ValidateProtectedSnapshot(m, m.studyDirectory + "/before/protected-files.json",
            m.studyDirectory + "/before/songs.json", CatalogPath, Path.GetFullPath(".."));
        private static void ValidateProtectedSnapshot(Manifest m, string snapshotPath, string beforeCatalogPath, string liveCatalogPath, string repo)
        {
            Require(File.Exists(snapshotPath) && Hash(snapshotPath) == m.protectedSnapshotSha256
                && File.Exists(beforeCatalogPath) && Hash(beforeCatalogPath) == m.protectedCatalogSha256,
                "Initial protected-file inventory or raw catalog snapshot differs from the pinned manifest.");
            var snapshot = Read<ProtectedFiles>(snapshotPath)?.files;
            Require(snapshot != null && snapshot.Length > 0 && snapshot.All(e => e != null && !e.normalizedLf)
                && snapshot.Select(e => e.path).Distinct(StringComparer.Ordinal).Count() == snapshot.Length
                && snapshot.All(e => MatchesFrozenFile(e, repo)), "An initial protected file is missing or changed; publication cannot adopt a new starting state.");
            var before = ChartTestPublicationV10.CatalogObjects(File.ReadAllText(beforeCatalogPath))
                .ToDictionary(text => JsonUtility.FromJson<SongRecord>(text).id, text => text, StringComparer.Ordinal);
            var live = ChartTestPublicationV10.CatalogObjects(File.ReadAllText(liveCatalogPath))
                .ToDictionary(text => JsonUtility.FromJson<SongRecord>(text).id, text => text, StringComparer.Ordinal);
            var allowed = new HashSet<string>(m.publishSourceIds.Select(id => id + m.testSuffix), StringComparer.Ordinal);
            Require(before.Count == m.protectedCatalogRecordCount && before.Keys.All(id => !allowed.Contains(id))
                && before.All(p => live.TryGetValue(p.Key, out var text) && text == p.Value)
                && live.Keys.All(id => before.ContainsKey(id) || allowed.Contains(id)), "The initial raw catalog objects changed or an unapproved record was added.");
        }
        private static Report ValidateCandidate(Manifest m, Source source, Inputs inputs)
        {
            string folder = Folder(m, source);
            ValidateBaseline(m, source, inputs, folder);
            Require(!File.Exists(folder + "/candidate.failure.json"), "A failed candidate rerun blocks stale result publication: " + source.id);
            var r = Read<Report>(folder + "/candidate.json");
            ValidateReport(m, source, inputs, r, true);
            Require(r.geometryValid && ReportsAgree(r, Read<PcmChartStudyV9.TypedReport>(folder + "/candidate.typed.json"), Hash(folder + "/candidate.generated.timeline.json")),
                "Candidate geometry/typed results/native timeline SHA are stale or failed: " + source.id);
            return r;
        }
        private static bool SameEventsAndSupports(MapTimeline a, MapTimeline b)
        {
            if (a == null || b == null) return false;
            // Compare persisted native representation; only the test ID and imported audio metadata may differ.
            a = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(a));
            b = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(b));
            return a.schemaVersion == b.schemaVersion && a.sampleRate == b.sampleRate && SameValue(a.forwardSpeed, b.forwardSpeed)
                && SameValue(a.delayTime, b.delayTime) && SameValue(a.tempoSections, b.tempoSections) && SameValue(a.events, b.events)
                && SameValue(a.elements, b.elements) && SameValue(a.segments, b.segments);
        }
        private static bool SameValue(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a.GetType() != b.GetType()) return false;
            if (a is double d) return SamePersistedDouble(d, (double)b);
            if (a is float f) return !float.IsNaN(f) && !float.IsInfinity(f) && f.Equals((float)b);
            var type = a.GetType();
            if (type.IsPrimitive || type.IsEnum || a is string) return a.Equals(b);
            if (a is Array array)
            {
                var other = (Array)b;
                return array.Length == other.Length && Enumerable.Range(0, array.Length).All(i => SameValue(array.GetValue(i), other.GetValue(i)));
            }
            return type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).All(field => SameValue(field.GetValue(a), field.GetValue(b)));
        }
        private static bool SamePersistedDouble(double a, double b)
        {
            // Unity's decimal JSON round trip can move a finite double by one representable value.
            // Permit at most two ULPs, with identical sign; floats and all discrete payload stay exact.
            if (double.IsNaN(a) || double.IsInfinity(a) || double.IsNaN(b) || double.IsInfinity(b)) return false;
            ulong left = unchecked((ulong)BitConverter.DoubleToInt64Bits(a)), right = unchecked((ulong)BitConverter.DoubleToInt64Bits(b));
            if ((left >> 63) != (right >> 63)) return false;
            return left >= right ? left - right <= 2 : right - left <= 2;
        }
    }
}
