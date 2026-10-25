using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class InstrumentalChartIntegrationValidation
    {
        public static void RunChecks()
        {
            CheckRecording("CanCanPiano", (pcm, rate, channels, voice, notes, speed) =>
            {
                var options = new AudioChartGenerator.Options { forwardSpeed = speed };
                var oldApi = AudioChartGenerator.GenerateFromSamples(pcm, rate, channels, "InstrumentalFixture", options, voice);
                var mixed = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "InstrumentalFixture", options, voice, notes);
                Require(oldApi.segments.All(s => s.kind != "Follow") && mixed.segments.Count(s => s.kind == "Follow") == 5,
                    "verified additional measurements reveal the five known calibration cells without changing the six-argument API");
                Require(mixed.segments.Where(s => s.kind == "Follow").All(s => s.duration < .5 && s.path.Length == 2),
                    "short rides retain entry and exit without inventing an internal scored checkpoint");
                options.includeMovingJumps = false;
                var noMoving = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "InstrumentalFixture", options, voice, notes);
                Require(noMoving.segments.All(s => s.kind != "Follow") && noMoving.elements.All(e => e.updateType != 3),
                    "the existing moving-jump switch disables both new glides and autonomous moving targets");
            });
            CheckRecording("DisconnectPop", (pcm, rate, channels, voice, notes, speed) =>
            {
                var options = new AudioChartGenerator.Options { forwardSpeed = speed };
                var normal = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "ShortSupportFixture", options, voice, notes);
                Require(!options.includeExperimentalShortSupports && normal.segments.All(s => s.kind != "Hold"),
                    "short-support style remains disabled by default rather than silently changing general generation");
                options.includeExperimentalShortSupports = true;
                var experimental = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "ShortSupportFixture", options, voice, notes);
                Require(experimental.segments.Count(s => s.kind == "Hold") == 2 && experimental.sourceMapBundle.EndsWith("+short-test", StringComparison.Ordinal),
                    "the opt-in test records exactly the two calibration supports and its experimental provenance");
                options.includeHolds = false;
                var noHolds = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "ShortSupportFixture", options, voice, notes);
                Require(noHolds.segments.All(s => s.kind != "Hold"), "the Hold switch also disables experimental support proposals");
                AudioChartGeneratorValidation.ValidateMap(normal);
                AudioChartGeneratorValidation.ValidateMap(experimental);
                var damaged = JsonUtility.FromJson<PcmInstrumentalEvidence.Evidence>(JsonUtility.ToJson(notes));
                damaged.sourcePcmSha256 = new string('0', 64);
                damaged.featureSha256 = PcmInstrumentalEvidence.FeatureHash(damaged);
                bool rejected = false;
                try { AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "ShortSupportFixture", options, voice, damaged); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "a correctly sealed cache for different decoded PCM must fail at the actual generation boundary");
            });
            Debug.Log("INSTRUMENTAL_CHART_INTEGRATION PASS: actual calibration PCM, compatible pure API, five short glides, default-off experimental Holds, independent flags, native geometry and wrong-PCM cache rejection.");
        }

        private static void CheckRecording(string id,
            Action<float[], int, int, PcmVocalAnalysis.Evidence, PcmInstrumentalEvidence.Evidence, float> action)
        {
            var record = RecoveredCatalog.LoadOriginal().Single(s => s.id == id);
            var clip = record.LoadAudio();
            if (clip == null) throw new FileNotFoundException("The calibration AudioClip is missing: " + id);
            bool loadedHere = clip.loadState != AudioDataLoadState.Loaded;
            try
            {
                if (loadedHere) clip.LoadAudioData();
                var pcm = new float[checked(clip.samples * clip.channels)];
                Require(clip.GetData(pcm, 0), "actual decoded PCM must be available for integration checks");
                var voice = PcmVocalAnalysis.LoadForClip(clip);
                var notes = PcmInstrumentalEvidence.LoadForClip(clip);
                Require(voice != null && notes != null, "integration requires verified independent source measurements");
                action(pcm, clip.frequency, clip.channels, voice, notes, record.forwardSpeed);
            }
            finally { if (loadedHere) clip.UnloadAudioData(); }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("INSTRUMENTAL_CHART_INTEGRATION RED/FAIL: " + message); }
    }
}
