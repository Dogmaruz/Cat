using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Counterfactual measurements for the foreground flight policy; no reference chart is consulted.</summary>
    public static class PcmFlightAnalysisValidation
    {
        [MenuItem("Dancing Cats/Validate vocal flight policy")]
        public static void RunChecks()
        {
            var type = typeof(PcmFlightAnalysisValidation).Assembly.GetType("DancingCats.Clone.Editor.PcmFlightAnalysis");
            Require(type != null, "foreground flight analysis is missing");
            var analyze = type.GetMethod("Analyze", BindingFlags.Public | BindingFlags.Static);
            Require(analyze != null, "foreground flight analysis entry point is missing");
            var positive = Fixture();
            var actual = Invoke(analyze, positive, new[] { 2.03, 2.20, 2.35, 2.70 }, .5);
            Require(actual.Length == 1 && Math.Abs(Number(actual.GetValue(0), "time") - 2.03) < 1e-8
                && Math.Abs(Number(actual.GetValue(0), "landingTime") - 2.70) < 1e-8,
                "a stable upper-register foreground body reserves one flight over accompaniment attacks to the measured post-release landing");
            var silent = Fixture(); Array.Clear(silent.rms, 0, silent.rms.Length); Seal(silent);
            Require(Invoke(analyze, silent, new[] { 2.03, 2.70 }, .5).Length == 0, "pitch confidence and a long gap cannot create a flight over silent vocals");
            var percussive = Fixture(); Array.Clear(percussive.pitchConfidence, 0, percussive.pitchConfidence.Length); Seal(percussive);
            Require(Invoke(analyze, percussive, new[] { 2.03, 2.70 }, .5).Length == 0, "percussion with a long gap has no stable voiced flight evidence");
            var lower = Fixture(); PaintPitch(lower, 2, 2.5, 60); Seal(lower);
            Require(Invoke(analyze, lower, new[] { 2.03, 2.70 }, .5).Length == 0, "the optional upper-register gesture does not turn every long low note into a large jump");
            var ornament = Fixture(); PaintPitch(ornament, 2.15, 2.30, 78); Seal(ornament);
            Require(Invoke(analyze, ornament, new[] { 2.03, 2.70 }, .5).Length == 0, "an ornament is not a stable foreground plateau");
            var reattack = Fixture(); reattack.voiceStarts = new[] { 2.0, 2.25 }; Seal(reattack);
            Require(Invoke(analyze, reattack, new[] { 2.03, 2.25, 2.70 }, .5).Length == 0, "a measured foreground rearticulation prevents suppression of its landing");
            var held = new[] { new MusicalCue { kind = "Hold", time = 2.2, endTime = 2.6 } };
            Require(Invoke(analyze, positive, new[] { 2.03, 2.70 }, .5, held).Length == 0, "confirmed sustained support takes priority over a proposed airborne gesture");
            Require(Invoke(analyze, positive, new[] { 2.03, 2.35 }, .5).Length == 0, "a real post-release landing is required; a timer cannot invent it");
            Require(Invoke(analyze, positive, new[] { 2.03, 2.70 }, 0).Length == 0, "without a measured pulse the flight policy is disabled");
            Require(Invoke(analyze, positive, new[] { 2.03, 2.70 }, 1).Length == 0,
                "an articulation shorter than one measured beat does not satisfy the tempo-relative flight envelope");
            var flat = Fixture(); PaintPitch(flat, 0, 8, 72); Seal(flat);
            Require(Invoke(analyze, flat, new[] { 2.03, 2.70 }, .5).Length == 0,
                "a recording without any measured register contrast has no upper-register gesture");
            var spike = Fixture(); PaintPitch(spike, 0, 8, 72); PaintPitch(spike, .5, .51, 60); Seal(spike);
            Require(Invoke(analyze, spike, new[] { 2.03, 2.70 }, .5).Length == 0,
                "a single-frame pitch outlier cannot establish the register contrast of an otherwise flat melody");
            Require(Rejects(analyze, positive, double.NaN), "a non-finite pulse is rejected clearly");
            var malformed = Fixture(); malformed.rms = null;
            Require(Rejects(analyze, malformed, .5), "malformed evidence fails validation instead of dereferencing missing arrays");
            var transposed = Fixture();
            for (int i = 0; i < transposed.pitch.Length; i++) transposed.pitch[i] -= 7;
            Seal(transposed);
            Require(Invoke(analyze, transposed, new[] { 2.03, 2.70 }, .5).Length == 1,
                "register is relative to this recording, so transposing the whole melody preserves the gesture");
            var shifted = Fixture(1);
            var shiftedActual = Invoke(analyze, shifted, new[] { 3.03, 3.20, 3.35, 3.70 }, .5);
            Require(shiftedActual.Length == 1 && Math.Abs(Number(shiftedActual.GetValue(0), "time") - 3.03) < 1e-8,
                "moving the phrase in time moves its flight instead of retaining a song-specific timestamp");
            Debug.Log("PCM_FLIGHT_ANALYSIS PASS: 16 counterfactual cases; foreground plateau versus silence, percussion, low/flat register, pitch outlier, ornament and rearticulation; protected support, measured landing, tempo-relative pulse, malformed input, pitch/time invariance.");
        }

        private static Array Invoke(MethodInfo method, PcmVocalAnalysis.Evidence evidence, double[] attacks, double period, MusicalCue[] supports = null)
            => (Array)method.Invoke(null, new object[] { evidence, attacks, period, supports ?? Array.Empty<MusicalCue>() });

        private static bool Rejects(MethodInfo method, PcmVocalAnalysis.Evidence e, double period)
        {
            try { Invoke(method, e, new[] { 2.03, 2.70 }, period); }
            catch (TargetInvocationException error) { return error.InnerException is ArgumentException || error.InnerException is InvalidDataException; }
            return false;
        }

        private static double Number(object value, string field) => (double)value.GetType().GetField(field).GetValue(value);

        private static PcmVocalAnalysis.Evidence Fixture(double offset = 0)
        {
            const int count = 800;
            var e = new PcmVocalAnalysis.Evidence
            {
                schemaVersion = 1, featureQuantization = 100000000, sampleRate = 44100, channels = 1,
                sampleFrames = count * 441, rmsStep = .01,
                sourceFileSha256 = new string('0', 64), sourcePcmSha256 = new string('0', 64),
                separatorModelSha256 = "ce74ef3b6a6024ce44211a07be9cf8bc6d87728cc852a68ab34eb8e58cde9c8b",
                noteModelSha256 = "2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec",
                analysisVersion = "synthetic-flight-policy", pitchTimes = new double[count], pitch = new int[count],
                pitchConfidence = new double[count], onset = new double[count], rms = new double[count], voiceStarts = new[] { 2 + offset }
            };
            for (int i = 0; i < count; i++)
            { e.pitchTimes[i] = i * .01; e.pitch[i] = 72; e.pitchConfidence[i] = .7; e.onset[i] = .1; }
            int from = (int)Math.Round((2 + offset) * 100), end = (int)Math.Round((2.5 + offset) * 100);
            for (int i = from; i < end; i++)
            { e.pitch[i] = 74; e.rms[i] = .12 + .08 * (i - from) / (end - from); }
            Seal(e); return e;
        }

        private static void PaintPitch(PcmVocalAnalysis.Evidence e, double from, double to, int pitch)
        { for (int i = (int)Math.Round(from * 100); i < (int)Math.Round(to * 100); i++) e.pitch[i] = pitch; }

        private static void Seal(PcmVocalAnalysis.Evidence e)
            => e.featureSha256 = (string)typeof(PcmVocalAnalysis).GetMethod("FeatureHash", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { e });

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException("PCM_FLIGHT_ANALYSIS RED/FAIL: " + message); }
    }
}
