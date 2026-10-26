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
                var shortRides = mixed.segments.Where(s => s.kind == "Follow" && s.duration < .5).ToArray();
                Require(oldApi.segments.All(s => s.kind != "Follow") && shortRides.Length == 7,
                    "verified additional measurements retain the five prior cells and recover two quiet short responses without changing the pure six-argument API");
                Require(mixed.segments.Count(s => s.kind == "Follow") == 19,
                    "the full compiler and route composition must retain all nineteen measured calibration gestures");
                Require(shortRides.All(s => s.path.Length == 2),
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
                Require(experimental.segments.Count(s => s.kind == "Hold") == 9 && experimental.sourceMapBundle.EndsWith("+short-test", StringComparison.Ordinal),
                    "the opt-in test retains eight interval-relative supports and adds a ninth from two independent source motifs; strict accuracy is measured separately");
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
            CheckRecording("FurElisePiano", (pcm, rate, channels, voice, notes, speed) =>
            {
                var options = new AudioChartGenerator.Options { forwardSpeed = speed };
                var withoutNotes = AudioChartGenerator.GenerateFromSamples(pcm, rate, channels, "ArticulationFixture", options, voice);
                var verified = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "ArticulationFixture", options, voice, notes);
                Require(withoutNotes.segments.Count(s => s.kind == "Follow") == 2 && verified.segments.All(s => s.kind != "Follow"),
                    "independent note articulation rejects both legacy false phrases and sustained-activation trills without changing the pure API");
                AudioChartGeneratorValidation.ValidateMap(verified);
            });
            CheckRecording("JingleBells", (pcm, rate, channels, voice, notes, speed) =>
            {
                var options = new AudioChartGenerator.Options { forwardSpeed = speed };
                var map = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "ChordalFixture", options, voice, notes);
                Require(map.segments.Count(s => s.kind == "Follow") == 8,
                    "source-confirmed chordal and voiced rising responses must survive native compilation and targeted flight arbitration");
                var record = RecoveredCatalog.LoadOriginal().Single(s => s.id == "JingleBells");
                var reference = JsonUtility.FromJson<MapTimeline>(File.ReadAllText("Assets/Resources/" + record.timelineResource + ".json"));
                var matches = PcmChartStudyV9.Match(reference.segments.Where(s => s.kind == "Follow").ToArray(),
                    map.segments.Where(s => s.kind == "Follow").ToArray(), .08, .15, "Independent generation precedes typed interval evaluation.");
                Require(matches.truePositive == 7 && matches.falsePositive <= 1,
                    "seven integrated responses match original Follow intervals; the known sustained Hold/Follow confusion must not grow");
                AudioChartGeneratorValidation.ValidateMap(map);
            });
            CheckRecording("UnstoppableCat054", (pcm, rate, channels, voice, notes, speed) =>
            {
                var options = new AudioChartGenerator.Options { forwardSpeed = speed };
                var pure = AudioChartGenerator.GenerateFromSamples(pcm, rate, channels, "ProtectionFixture", options, voice);
                var verified = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "ProtectionFixture", options, voice, notes);
                foreach (string kind in new[] { "Hold", "Follow", "BigJump" })
                    Require(pure.segments.Where(s => s.kind == kind).Select(s => JsonUtility.ToJson(s))
                        .SequenceEqual(verified.segments.Where(s => s.kind == kind).Select(s => JsonUtility.ToJson(s))),
                        "source verification and targeted response priority must preserve valid Unstoppable " + kind + " geometry and timing");
                AudioChartGeneratorValidation.ValidateMap(verified);
            });
            CheckRecording("AajKiRaatPop", (pcm, rate, channels, voice, notes, speed) =>
            {
                var options = new AudioChartGenerator.Options { forwardSpeed = speed };
                var pure = AudioChartGenerator.GenerateFromSamples(pcm, rate, channels, "ArticulatedHoldFixture", options, voice);
                var verified = AudioChartGenerator.GenerateWithInstrumentalSamples(pcm, rate, channels, "ArticulatedHoldFixture", options, voice, notes);
                Require(pure.segments.Count(s => s.kind == "Hold" && s.duration > .45)
                    - verified.segments.Count(s => s.kind == "Hold" && s.duration > .45) == 4,
                    "only the four independently contradicted legacy long Holds are rejected; unresolved supports remain protected");
                AudioChartGeneratorValidation.ValidateMap(verified);
            });
            Debug.Log("INSTRUMENTAL_CHART_INTEGRATION PASS: real PCM, pure API, Can Can19, Jingle7 correct/one known confusion, valid Unstoppable supports, Aaj false-Hold rejection, default-off Holds, flags, native geometry and wrong-PCM rejection.");
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
