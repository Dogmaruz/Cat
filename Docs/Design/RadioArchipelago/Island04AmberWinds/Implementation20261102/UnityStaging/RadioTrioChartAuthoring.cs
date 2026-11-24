using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Source-specific entry points for the Radio Trio candidate recording; inference stays in the shared generator.</summary>
    public static class RadioTrioChartAuthoring
    {
        public const string SongId = "RadioTrio";
        public const string AudioPath = "Assets/Resources/CustomAudio/Music/RadioTrio.mp3";
        public const string BaseChartPath = "Assets/Resources/CustomData/Maps/RadioTrio.timeline.json";
        public const string SourceSha256 = "b3a7249d3f28984bf190d6498a2cadee7335d3bf61f6c22679e69e911d8759f6";
        public const string ReviewPath = "Library/RadioTrioChart";
        private const string CatalogPath = "Assets/Resources/CustomData/songs.json";

        [Serializable] private sealed class Report
        {
            public string sourceFileSha256, sourcePcmSha256, voiceFeatureSha256, instrumentalFeatureSha256;
            public string generator, chartSha256;
            public double duration, requestedBpm, detectedBpm, finishAudioTime, maximumAirGap;
            public int sampleRate, channels, sampleFrames, catalogId, elements, segments, scoringEvents;
            public int jumps, bigJumps, holds, shortHolds, follows, unchangedExistingCatalogRecords;
            public AudioChartGenerator.Options options;
            public PcmPlayablePhraseGrouping.Report playablePhrases;
        }

        [MenuItem("Radio Archipelago/Radio Trio/Export imported PCM")]
        public static void ExportPcm()
        {
            RequireEditMode();
            string folder = VocalPcmExport.Export(LoadSource());
            Debug.Log("RADIO_TRIO_PCM_READY " + folder);
        }

        [MenuItem("Radio Archipelago/Radio Trio/Generate base chart")]
        public static void GenerateBase() => Generate(false);

        [MenuItem("Radio Archipelago/Radio Trio/Publish reviewed base chart")]
        public static void PublishApproved() => Generate(true);

        private static void Generate(bool publish)
        {
            RequireEditMode();
            AudioClip clip = LoadSource();
            var voice = PcmVocalAnalysis.LoadForClip(clip);
            var instrument = PcmInstrumentalEvidence.LoadForClip(clip);
            if (voice == null || instrument == null)
                throw new InvalidDataException("Radio Trio requires both locally measured vocal and instrumental evidence from its exported Unity PCM before generation.");

            var customs = File.Exists(CatalogPath)
                ? RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(CatalogPath))
                : Array.Empty<SongRecord>();
            var options = new AudioChartGenerator.Options
            {
                catalogId = AudioChartGenerator.ResolveCatalogId(SongId, RecoveredCatalog.LoadOriginal(), customs, 20005),
                title = "Радио-трио", artist = "Радиоархипелаг",
                forwardSpeed = 6.5f, delayTime = .6f,
                includeHolds = true, includeMovingJumps = true, includeBigJumps = true,
                includeForegroundAccents = true, includeRouteChoreography = true,
                includeExperimentalShortSupports = true, preferAuthoredScore = false,
                includePlayablePhraseGrouping = true, playableWorldDistanceScale = 1.20f,
                includeMeasuredGapRecovery = true,
                playableLateralScale = 1.8f, playableMinimumSeparation = 1.40f, playableHoldMinimumSeparation = 1.50f
            };
            MapTimeline map = AudioChartGenerator.Generate(clip, SongId, options);
            Directory.CreateDirectory(ReviewPath);
            File.WriteAllText(Path.Combine(ReviewPath, "candidate.json"), JsonUtility.ToJson(map, true) + "\n");
            AudioChartGeneratorValidation.ValidateMap(map);
            int unchangedRecords = publish ? SavePreservingExistingCatalog(map, clip, options) : 0;

            var supports = map.segments.Where(s => s.kind != "Theme").OrderBy(s => s.audioTime).ToArray();
            double maximumGap = 0;
            for (int i = 1; i < supports.Length; i++)
                maximumGap = Math.Max(maximumGap, supports[i].audioTime - supports[i - 1].endAudioTime);
            var report = new Report
            {
                sourceFileSha256 = Hash(AudioPath), sourcePcmSha256 = voice.sourcePcmSha256,
                voiceFeatureSha256 = voice.featureSha256, instrumentalFeatureSha256 = instrument.featureSha256,
                generator = map.sourceMapBundle, chartSha256 = Hash(publish ? BaseChartPath : Path.Combine(ReviewPath, "candidate.json")),
                duration = (double)clip.samples / clip.frequency, requestedBpm = 144,
                detectedBpm = 60 * map.sampleRate / map.tempoSections[0].samplesPerBeat,
                finishAudioTime = supports.Last(s => s.kind == "FinishJump").audioTime, maximumAirGap = maximumGap,
                sampleRate = clip.frequency, channels = clip.channels, sampleFrames = clip.samples,
                catalogId = options.catalogId, elements = map.elements.Length, segments = map.segments.Length,
                scoringEvents = map.events.Length,
                jumps = supports.Count(s => s.kind == "Jump"), bigJumps = supports.Count(s => s.kind == "BigJump"),
                holds = supports.Count(s => s.kind == "Hold"), shortHolds = supports.Count(s => s.kind == "Hold" && s.duration <= .45),
                follows = supports.Count(s => s.kind == "Follow"), options = options,
                playablePhrases = PcmPlayablePhraseGrouping.ReadReport(map), unchangedExistingCatalogRecords = unchangedRecords
            };
            Directory.CreateDirectory(ReviewPath);
            File.WriteAllText(Path.Combine(ReviewPath, "generation.json"), JsonUtility.ToJson(report, true) + "\n");
            Debug.Log("RADIO_TRIO_BASE_READY " + (publish ? BaseChartPath : Path.Combine(ReviewPath, "candidate.json")) + "; " + report.duration.ToString("F3") + "s; "
                + report.jumps + " jumps, " + report.bigJumps + " big jumps, " + report.holds + " holds ("
                + report.shortHolds + " short), " + report.follows + " follows; " + report.generator
                + "; published=" + publish + "; preserved catalog records=" + unchangedRecords);
        }

        // Shared Save serializes every catalog row. Publish only this song's row, retaining
        // every other original byte (including unknown fields, float spelling and whitespace).
        private static int SavePreservingExistingCatalog(MapTimeline map, AudioClip clip, AudioChartGenerator.Options options)
        {
            byte[] original = File.Exists(CatalogPath) ? File.ReadAllBytes(CatalogPath) : null;
            try
            {
                AudioChartGenerator.Save(map, clip, options);
                string generated = File.ReadAllText(CatalogPath);
                var generatedSpan = CatalogObjects(generated).Single(s => JsonUtility.FromJson<SongRecord>(generated.Substring(s.Item1, s.Item2)).id == SongId);
                string record = generated.Substring(generatedSpan.Item1, generatedSpan.Item2);
                string before = original == null ? "[]\n" : Encoding.UTF8.GetString(original);
                var objects = CatalogObjects(before).ToArray();
                var existing = objects.Where(s => JsonUtility.FromJson<SongRecord>(before.Substring(s.Item1, s.Item2)).id == SongId).ToArray();
                if (existing.Length > 1) throw new InvalidDataException("The Radio Trio catalog entry is duplicated.");
                string published;
                if (existing.Length == 1)
                {
                    var span = existing[0];
                    published = before.Substring(0, span.Item1) + record + before.Substring(span.Item1 + span.Item2);
                }
                else
                {
                    int end = before.LastIndexOf(']');
                    if (end < 0) throw new InvalidDataException("The custom catalog is not a JSON array.");
                    published = before.Insert(end, (objects.Length == 0 ? "" : ",") + "\n" + record + "\n");
                }
                string[] preservedRows = objects.Select(s => before.Substring(s.Item1, s.Item2))
                    .Where(row => JsonUtility.FromJson<SongRecord>(row).id != SongId).ToArray();
                string[] finalRows = CatalogObjects(published).Select(s => published.Substring(s.Item1, s.Item2))
                    .Where(row => JsonUtility.FromJson<SongRecord>(row).id != SongId).ToArray();
                if (!preservedRows.SequenceEqual(finalRows, StringComparer.Ordinal))
                    throw new InvalidDataException("Radio Trio authoring changed another custom catalog record.");
                // Parse the final array before publishing as well as checking exact old row text.
                RecoveredCatalog.FromJsonArray<SongRecord>(published.TrimStart('\uFEFF'));
                File.WriteAllBytes(CatalogPath, Encoding.UTF8.GetBytes(published));
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                return preservedRows.Length;
            }
            catch
            {
                if (original != null) File.WriteAllBytes(CatalogPath, original);
                else if (File.Exists(CatalogPath)) File.Delete(CatalogPath);
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
        }

        internal static bool PreservesCatalogSnapshot(string before)
        {
            string after = File.ReadAllText(CatalogPath);
            var previous = CatalogObjects(before).Select(s => before.Substring(s.Item1,s.Item2)).ToArray();
            var current = CatalogObjects(after).Select(s => after.Substring(s.Item1,s.Item2)).ToArray();
            var oldRows = current.Where(row => JsonUtility.FromJson<SongRecord>(row).id != SongId).ToArray();
            return previous.SequenceEqual(oldRows,StringComparer.Ordinal) && current.Length == previous.Length + 1 &&
                current.Count(row => JsonUtility.FromJson<SongRecord>(row).id == SongId) == 1;
        }

        private static IEnumerable<Tuple<int, int>> CatalogObjects(string json)
        {
            int depth = 0, start = -1;
            bool quoted = false, escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '{') { if (depth++ == 0) start = i; }
                else if (c == '}' && --depth == 0) yield return Tuple.Create(start, i + 1 - start);
            }
            if (depth != 0 || quoted) throw new InvalidDataException("The custom catalog contains malformed JSON objects.");
        }

        internal static AudioClip LoadSource()
        {
            if (!File.Exists(AudioPath) || Hash(AudioPath) != SourceSha256)
                throw new InvalidDataException("The Radio Trio candidate MP3 is missing or its SHA-256 changed.");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            if (clip == null) throw new InvalidDataException("Import the Radio Trio candidate MP3 before authoring.");
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded)
                throw new InvalidDataException("Radio Trio needs Decompress On Load and Load In Background disabled for PCM authoring.");
            return clip;
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Author Radio Trio in Edit Mode after compilation.");
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
