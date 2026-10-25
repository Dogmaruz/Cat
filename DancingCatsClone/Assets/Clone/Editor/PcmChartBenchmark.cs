using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Frozen cross-recording evaluation. Original charts are read only after independent generation inputs are selected.</summary>
    public static class PcmChartBenchmark
    {
        private const float BenchmarkDelay = .6f;
        private static readonly string Output = Path.GetFullPath("Library/ChartGenerationV8/benchmark");
        private sealed class Target
        {
            public readonly string id, mapHash, audioHash;
            public Target(string id, string mapHash, string audioHash)
            { this.id = id; this.mapHash = mapHash; this.audioHash = audioHash; }
        }
        private static readonly Target[] Calibration =
        {
            new Target("TwinkleLittleStar", "96657140ead6377636747c6619bd3e060c60bfe41f32276c4436dbf6b15754db", "0cd8dc69f01e117562bf8d21afa17225c5594e7e1ba0920444275953fddec44f"),
            new Target("MoonlightPop", "ae158d469109c32c621cbe915062e3ff5129abe1c1001343f557f8ba0e7b3804", "35cb4620f0b6436f8c918538ecc6e3e81ef6180a2fb4ba4fd39931156909b843")
        };
        private static readonly Target[] Validation =
        {
            new Target("LABUBUSongsPop", "1e9b3f9363637f19b2bcaa45fe361a6ab13ac8c4b9c3ad45d50a6d1d58ef108f", "dadc0644e3f1b2fe9d9f10f19b0df81eff5dcf087cc382d9a44e74a2a6a57190")
        };
        [Serializable] private sealed class Fingerprint
        {
            public string referenceMapSha256, sourceAudioSha256, sourcePcmSha256, voiceFeatureSha256, generator;
            public float generatedDelayTime, generatedForwardSpeed;
        }
        [Serializable] private sealed class Failure
        {
            public string stage, referenceId, error;
            public bool generationFailed = true;
        }

        public static void ExportCalibrationPcm() => Export(Calibration);
        public static void ExportValidationPcm() => Export(Validation);
        public static void RunCalibrationBaseline() => Run(Calibration, "baseline", false);
        public static void RunCalibrationCandidate() => Run(Calibration, "candidate", false);
        public static void RunFrozenBaseline() => Run(Validation, "baseline", true);
        public static void RunFrozenCandidate() => Run(Validation, "candidate", true);

        private static void Export(Target[] targets)
        {
            RequireEditor();
            foreach (var target in targets)
                GuardSource(target, record => VocalPcmExport.Export(record.LoadAudio()));
        }

        private static void Run(Target[] targets, string stage, bool frozen)
        {
            RequireEditor();
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
                        if (evidence == null) throw new FileNotFoundException("Prepare actual Unity PCM vocal evidence before a paired benchmark.", voicePath);
                        if (evidence == null || evidence.sourceFileSha256 != target.audioHash)
                            throw new InvalidDataException("The benchmark evidence identifies another source recording.");
                        string baseline = Path.Combine(folder, "baseline.json");
                        if (stage == "candidate" || File.Exists(baseline))
                        {
                            if (!File.Exists(baseline)) throw new InvalidOperationException("Capture the immutable paired baseline before evaluating a candidate.");
                            var fingerprint = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(baseline));
                            if (fingerprint == null || fingerprint.referenceMapSha256 != target.mapHash || fingerprint.sourceAudioSha256 != target.audioHash
                                || fingerprint.sourcePcmSha256 != evidence.sourcePcmSha256 || fingerprint.voiceFeatureSha256 != evidence.featureSha256
                                || Math.Abs(fingerprint.generatedDelayTime - BenchmarkDelay) > .000001
                                || Math.Abs(fingerprint.generatedForwardSpeed - record.forwardSpeed) > .000001)
                                throw new InvalidDataException("Paired benchmark inputs differ from the baseline; preserve the baseline and use identical recording/features/options.");
                            if (stage == "baseline")
                            {
                                Debug.Log("PCM_BENCHMARK retained immutable baseline for " + target.id);
                                return;
                            }
                        }
                        OriginalChartCalibration.EvaluateSong(record, stage, folder, BenchmarkDelay, evidence,
                            (frozen ? "Frozen recording validation; selection used aggregate annotation counts. Do not tune on these candidate errors while retaining a held-out claim."
                                : "Cross-recording calibration; these recordings may guide tuning.")
                            + " Audio-axis metrics use the record's forward speed and common 0.6-second preroll; reference charts and catalogs are never published.");
                        string failurePath = Path.Combine(folder, stage + ".failure.json");
                        if (File.Exists(failurePath)) File.Delete(failurePath);
                    });
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(folder, stage + ".failure.json"), JsonUtility.ToJson(new Failure
                        { stage = stage, referenceId = target.id, error = error.ToString() }, true));
                    failures.Add(target.id + ": " + error.Message);
                }
            }
            if (failures.Count > 0) throw new InvalidOperationException("Benchmark failures are reported, not skipped:\n" + string.Join("\n", failures));
            Debug.Log("PCM_BENCHMARK " + stage + " completed for " + targets.Length + " recording(s); no playable map, catalog or player build was written.");
        }

        private static void GuardSource(Target target, Action<SongRecord> action)
        {
            var record = RecoveredCatalog.LoadOriginal().Single(song => song.id == target.id);
            var clip = record.LoadAudio();
            if (clip == null) throw new FileNotFoundException("The benchmark AudioClip is missing: " + record.audioResource);
            string map = Path.GetFullPath("Assets/Resources/" + record.timelineResource + ".json");
            string audio = Path.GetFullPath(AssetDatabase.GetAssetPath(clip));
            if (Hash(map) != target.mapHash || Hash(audio) != target.audioHash)
                throw new InvalidDataException("A selected original recording/map differs from the frozen benchmark manifest: " + target.id);
            var paths = new[] { map, map + ".meta", audio, audio + ".meta", Path.GetFullPath("Assets/Resources/OriginalData/songs.json"), Path.GetFullPath("Assets/Resources/OriginalData/songs.json.meta") };
            var before = paths.Where(File.Exists).ToDictionary(path => path, Hash, StringComparer.OrdinalIgnoreCase);
            try { action(record); }
            finally
            {
                foreach (var pair in before)
                    if (!File.Exists(pair.Key) || Hash(pair.Key) != pair.Value)
                        throw new InvalidOperationException("Benchmarking altered a protected original source: " + pair.Key);
            }
        }

        private static void RequireEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Run the benchmark in Edit Mode after imports and compilation finish.");
        }
        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
