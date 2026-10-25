using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Integration evidence from the actual decoded recording; no reference chart enters these checks.</summary>
    public static class PcmVocalIntegrationValidation
    {
        [MenuItem("Dancing Cats/Validate vocal chart integration")]
        public static void RunChecks()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Run vocal integration checks in Edit Mode after compilation.");
            PcmVocalGestureValidation.RunChecks();
            // This is a named calibration recording, not a general claim that songs contain
            // a particular number of holds or moving phrases. Only its audio is loaded.
            var record = RecoveredCatalog.LoadOriginal().Single(song => song.id == "UnstoppableCat054");
            var clip = record.LoadAudio();
            Require(clip != null, "The calibration AudioClip is missing.");
            bool loadedHere = clip.loadState != AudioDataLoadState.Loaded;
            try
            {
                if (loadedHere) clip.LoadAudioData();
                Require(clip.loadState == AudioDataLoadState.Loaded, "The real imported PCM is not ready.");
                var pcm = new float[checked(clip.samples * clip.channels)];
                Require(clip.GetData(pcm, 0), "AudioClip.GetData failed for the real recording.");
                var evidence = PcmVocalAnalysis.LoadForClip(clip);
                Require(evidence != null, "Prepare the optional local vocal evidence for the calibration source first.");
                string digest = PcmDigest(pcm);
                PcmVocalAnalysis.Validate(evidence, digest, clip.frequency, clip.channels, clip.samples);
                CheckMetadataMismatch(evidence, digest, clip.frequency, clip.channels, clip.samples);

                var spectral = PcmSpectralAnalysis.Analyze(pcm, clip.frequency, clip.channels);
                var options = Options();
                double duration = (double)clip.samples / clip.frequency;
                double cutoff = Math.Round(duration - Math.Max(1.1, 5 / options.forwardSpeed + .25), 6) - .18;
                var expectedGlides = spectral.follows.Where(cue => cue.time >= 0 && cue.endTime <= cutoff).ToArray();
                var map = AudioChartGenerator.GenerateFromSamples(pcm, clip.frequency, clip.channels, "VocalIntegration", options, evidence);
                double beatPeriod = map.tempoSections[0].samplesPerBeat / clip.frequency;
                var expectedHolds = PcmVocalAnalysis.Analyze(evidence, spectral.accents, beatPeriod)
                    .Where(cue => cue.time >= 0 && cue.endTime <= cutoff
                        && !expectedGlides.Any(glide => cue.time < glide.endTime + .078 && cue.endTime > glide.time - .078)).ToArray();
                Require(expectedHolds.Length > 0 && expectedGlides.Length > 0,
                    "This integration fixture needs both separated-vocal and recurrent-phrase evidence.");
                var again = AudioChartGenerator.GenerateFromSamples(pcm, clip.frequency, clip.channels, "VocalIntegration", Options(), evidence);
                Require(JsonUtility.ToJson(map) == JsonUtility.ToJson(again),
                    "Identical decoded PCM and verified vocal evidence must compile deterministically.");
                Require(map.audio.rawSha256 == digest && map.sourceMapBundle == "pcm-vocal-phrase-chart-v8",
                    "The compiled chart must retain its actual PCM identity and vocal analysis provenance.");
                SameSupports(map, "Hold", expectedHolds);
                SameSupports(map, "Follow", expectedGlides);
                var withoutFlightsOptions = Options(); withoutFlightsOptions.includeBigJumps = false;
                var withoutFlights = AudioChartGenerator.GenerateFromSamples(pcm, clip.frequency, clip.channels, "VocalNoFlights", withoutFlightsOptions, evidence);
                Require(!withoutFlights.segments.Any(segment => segment.kind == "BigJump")
                    && map.segments.Any(segment => segment.kind == "BigJump"),
                    "includeBigJumps controls large launches independently of Holds and Follow.");
                SameSupports(withoutFlights, "Hold", expectedHolds);
                SameSupports(withoutFlights, "Follow", expectedGlides);
                var physicalOnlyOptions = Options(); physicalOnlyOptions.includeForegroundAccents = false;
                var physicalOnly = AudioChartGenerator.GenerateFromSamples(pcm, clip.frequency, clip.channels, "VocalPhysicalOnly", physicalOnlyOptions, evidence);
                SameSupports(physicalOnly, "Hold", expectedHolds);
                SameSupports(physicalOnly, "Follow", expectedGlides);
                Require(physicalOnly.segments.Count(segment => segment.kind == "Jump") > map.segments.Count(segment => segment.kind == "Jump"),
                    "Foreground articulation must filter ordinary accompaniment accents while preserving support boundaries.");
                AudioChartGeneratorValidation.ValidateMap(map);
                var voiceOnly = expectedHolds.Where(cue => !spectral.accents.Any(attack => Math.Abs(attack - cue.time) <= .08)).ToArray();
                Require(voiceOnly.Length > 0,
                    "The source fixture must exercise a vocal entry missed by the mixed high-frequency attack detector.");
                foreach (var cue in voiceOnly)
                    Require(map.segments.Count(segment => segment.kind == "Hold" && Near(segment.audioTime, cue.time)
                        && Near(segment.endAudioTime, cue.endTime)) == 1,
                        "A measured vocal-only entry must survive the merge exactly once.");
                foreach (var support in map.segments.Where(segment => segment.kind == "Hold" || segment.kind == "Follow"))
                    Require(!map.segments.Any(segment => (segment.kind == "Jump" || segment.kind == "BigJump")
                        && segment.audioTime >= support.audioTime - .000001 && segment.audioTime <= support.endAudioTime + .077),
                        "A confirmed support must suppress overlapping ordinary tiles at entry, within its duration and at release.");

                var noHoldsOptions = Options(); noHoldsOptions.includeHolds = false;
                var noHolds = AudioChartGenerator.GenerateFromSamples(pcm, clip.frequency, clip.channels, "VocalNoHolds", noHoldsOptions, evidence);
                Require(!noHolds.segments.Any(segment => segment.kind == "Hold"), "includeHolds=false must suppress vocal Holds.");
                SameSupports(noHolds, "Follow", expectedGlides);
                AudioChartGeneratorValidation.ValidateMap(noHolds);
                var noMovingOptions = Options(); noMovingOptions.includeMovingJumps = false;
                var noMoving = AudioChartGenerator.GenerateFromSamples(pcm, clip.frequency, clip.channels, "VocalNoMoving", noMovingOptions, evidence);
                Require(!noMoving.segments.Any(segment => segment.kind == "Follow")
                    && !noMoving.elements.Any(element => element.id == 4 || element.id == 5 || element.id == 6),
                    "includeMovingJumps=false must suppress native Follow entries, markers and exits.");
                Require(noMoving.segments.Any(segment => segment.kind == "Hold"),
                    "Disabling moving phrases must retain independent vocal Holds.");
                AudioChartGeneratorValidation.ValidateMap(noMoving);
                Debug.Log("PCM_VOCAL_INTEGRATION PASS: actual GetData/cache identity, metadata rejection, deterministic merge, "
                    + expectedHolds.Length + " audio-derived Holds, " + expectedGlides.Length + " recurrent glides, "
                    + voiceOnly.Length + " vocal-only entries, no support overlap and independent option flags. This is within-song calibration.");
            }
            finally { if (loadedHere) clip.UnloadAudioData(); }
        }

        private static AudioChartGenerator.Options Options() => new AudioChartGenerator.Options
        {
            catalogId = 29007, title = "Vocal integration fixture", artist = "Local analysis", forwardSpeed = 6.5f, delayTime = .6f
        };

        private static void SameSupports(MapTimeline map, string kind, MusicalCue[] expected)
        {
            var actual = map.segments.Where(segment => segment.kind == kind).OrderBy(segment => segment.audioTime).ToArray();
            expected = expected.OrderBy(cue => cue.time).ToArray();
            Require(actual.Length == expected.Length, "Every eligible " + kind + " proposal must compile once; expected "
                + expected.Length + ", actual " + actual.Length);
            for (int index = 0; index < expected.Length; index++)
                Require(Near(actual[index].audioTime, expected[index].time) && Near(actual[index].endAudioTime, expected[index].endTime),
                    "The merge must preserve both measured " + kind + " boundaries.");
        }

        private static void CheckMetadataMismatch(PcmVocalAnalysis.Evidence evidence, string digest, int rate, int channels, int frames)
        {
            bool rejected = false;
            try { PcmVocalAnalysis.Validate(evidence, digest, rate + 1, channels, frames); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Evidence from a different decoded sample rate must be rejected.");
            rejected = false;
            try { PcmVocalAnalysis.Validate(evidence, digest, rate, channels == 1 ? 2 : 1, frames); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Evidence from a different channel layout must be rejected.");
            rejected = false;
            try { PcmVocalAnalysis.Validate(evidence, digest, rate, channels, frames + 1); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Even a one-frame source duration mismatch must be rejected.");
        }

        private static string PcmDigest(float[] pcm)
        {
            var bytes = new byte[checked(pcm.Length * sizeof(float))];
            Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static bool Near(double a, double b) => Math.Abs(a - b) < .000001;
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException("PCM_VOCAL_INTEGRATION FAIL: " + message); }
    }
}
