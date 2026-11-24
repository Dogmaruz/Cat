using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Source-specific entry points for the approved Lumi-Lai recording; all musical inference stays in the shared generator.</summary>
    public static class LumiLaiChartAuthoring
    {
        public const string SongId = "LumiLai";
        public const string AudioPath = "Assets/Resources/CustomAudio/Music/LumiLai.mp3";
        public const string BaseChartPath = "Assets/Resources/CustomData/Maps/LumiLai.timeline.json";
        public const string SourceSha256 = "f37309c134b71fe47e37ca615c088e8bb3513a2a4cc05c47c4ad994ab0d55e0f";
        public const string ReviewPath = "Library/LumiLaiChart";

        [Serializable] private sealed class Report
        {
            public string sourceFileSha256, sourcePcmSha256, voiceFeatureSha256, instrumentalFeatureSha256;
            public string generator, chartSha256;
            public double duration, detectedBpm, finishAudioTime, maximumAirGap;
            public int sampleRate, channels, sampleFrames, catalogId, elements, segments, scoringEvents;
            public int jumps, bigJumps, holds, shortHolds, follows;
            public AudioChartGenerator.Options options;
            public PcmPlayablePhraseGrouping.Report playablePhrases;
        }

        [MenuItem("Radio Archipelago/Lumi-Lai/Export imported PCM")]
        public static void ExportPcm()
        {
            RequireEditMode();
            string folder = VocalPcmExport.Export(LoadSource());
            Debug.Log("LUMI_LAI_PCM_READY " + folder);
        }

        [MenuItem("Radio Archipelago/Lumi-Lai/Generate base chart")]
        public static void GenerateBase()
        {
            RequireEditMode();
            AudioClip clip = LoadSource();
            var voice = PcmVocalAnalysis.LoadForClip(clip);
            var instrument = PcmInstrumentalEvidence.LoadForClip(clip);
            if (voice == null || instrument == null)
                throw new InvalidDataException("Lumi-Lai requires both locally measured vocal and instrumental evidence from its exported Unity PCM before generation.");

            string catalogPath = "Assets/Resources/CustomData/songs.json";
            var customs = File.Exists(catalogPath)
                ? RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(catalogPath))
                : Array.Empty<SongRecord>();
            var options = new AudioChartGenerator.Options
            {
                catalogId = AudioChartGenerator.ResolveCatalogId(SongId, RecoveredCatalog.LoadOriginal(), customs, 0),
                title = "Lumi-Lai", artist = "Радиоархипелаг",
                forwardSpeed = 6.5f, delayTime = .6f,
                includeHolds = true, includeMovingJumps = true, includeBigJumps = true,
                includeForegroundAccents = true, includeRouteChoreography = true,
                includeExperimentalShortSupports = true, preferAuthoredScore = false,
                includePlayablePhraseGrouping = true, playableWorldDistanceScale = 1.18f,
                playableLateralScale = 1.8f, playableMinimumSeparation = 1.40f, playableHoldMinimumSeparation = 1.50f
            };
            MapTimeline map = AudioChartGenerator.Generate(clip, SongId, options);
            AudioChartGeneratorValidation.ValidateMap(map);
            AudioChartGenerator.Save(map, clip, options);

            var supports = map.segments.Where(s => s.kind != "Theme").OrderBy(s => s.audioTime).ToArray();
            double maximumGap = 0;
            for (int i = 1; i < supports.Length; i++)
                maximumGap = Math.Max(maximumGap, supports[i].audioTime - supports[i - 1].endAudioTime);
            var report = new Report
            {
                sourceFileSha256 = SourceSha256, sourcePcmSha256 = voice.sourcePcmSha256,
                voiceFeatureSha256 = voice.featureSha256, instrumentalFeatureSha256 = instrument.featureSha256,
                generator = map.sourceMapBundle, chartSha256 = Hash(BaseChartPath),
                duration = (double)clip.samples / clip.frequency,
                detectedBpm = 60 * map.sampleRate / map.tempoSections[0].samplesPerBeat,
                finishAudioTime = supports.Last(s => s.kind == "FinishJump").audioTime, maximumAirGap = maximumGap,
                sampleRate = clip.frequency, channels = clip.channels, sampleFrames = clip.samples,
                catalogId = options.catalogId, elements = map.elements.Length, segments = map.segments.Length,
                scoringEvents = map.events.Length,
                jumps = supports.Count(s => s.kind == "Jump"), bigJumps = supports.Count(s => s.kind == "BigJump"),
                holds = supports.Count(s => s.kind == "Hold"), shortHolds = supports.Count(s => s.kind == "Hold" && s.duration <= .45),
                follows = supports.Count(s => s.kind == "Follow"), options = options,
                playablePhrases = PcmPlayablePhraseGrouping.ReadReport(map)
            };
            Directory.CreateDirectory(ReviewPath);
            File.WriteAllText(Path.Combine(ReviewPath, "generation.json"), JsonUtility.ToJson(report, true) + "\n");
            Debug.Log("LUMI_LAI_BASE_READY " + BaseChartPath + "; " + report.duration.ToString("F3") + "s; "
                + report.jumps + " jumps, " + report.bigJumps + " big jumps, " + report.holds + " holds ("
                + report.shortHolds + " short), " + report.follows + " follows; " + report.generator);
        }

        internal static AudioClip LoadSource()
        {
            if (!File.Exists(AudioPath) || Hash(AudioPath) != SourceSha256)
                throw new InvalidDataException("The approved Lumi-Lai MP3 is missing or its SHA-256 changed.");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            if (clip == null) throw new InvalidDataException("Import the approved Lumi-Lai MP3 before authoring.");
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded)
                throw new InvalidDataException("Lumi-Lai needs Decompress On Load and Load In Background disabled for PCM authoring.");
            return clip;
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Author Lumi-Lai in Edit Mode after compilation.");
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
