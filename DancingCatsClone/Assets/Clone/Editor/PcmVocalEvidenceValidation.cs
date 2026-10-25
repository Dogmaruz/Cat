using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class PcmVocalEvidenceValidation
    {
        public static void RunChecks()
        {
            var type = typeof(PcmVocalAnalysis.Evidence);
            var times = type.GetField("vocalAttackTimes");
            var confidence = type.GetField("vocalAttackConfidence");
            var version = type.GetField("attackAnalysisVersion");
            Require(times != null && confidence != null && version != null, "Independent foreground attack measurements are missing.");
            var e = Fixture(); Seal(e); PcmVocalAnalysis.Validate(e, e.sourcePcmSha256, 44100, 1, 44100);
            string legacy = e.featureSha256;
            times.SetValue(e, new[] { .2, .6 }); confidence.SetValue(e, new[] { .8, .9 }); version.SetValue(e, "stem-high-flux-1");
            Seal(e); Require(e.featureSha256 == legacy, "Legacy cache digests must remain byte-compatible.");
            e.schemaVersion = 2; Seal(e); PcmVocalAnalysis.Validate(e, e.sourcePcmSha256, 44100, 1, 44100);
            Require(e.featureSha256 != legacy, "New measurements require a distinct integrity domain.");
            ((double[])times.GetValue(e))[0] += .01;
            Require(Rejects(e), "Changed attack timing must invalidate the evidence digest.");
            Seal(e); ((double[])confidence.GetValue(e))[0] += .01;
            Require(Rejects(e), "Changed attack confidence must invalidate the evidence digest.");
            Seal(e); times.SetValue(e, new[] { double.NaN, .6 });
            Require(Rejects(e), "Non-finite foreground attacks must be rejected before hashing.");
            times.SetValue(e, new[] { .2, .6 }); confidence.SetValue(e, new[] { .8 });
            Require(Rejects(e), "Attack times and confidence must have matching lengths.");
            confidence.SetValue(e, new[] { .8, .9 }); version.SetValue(e, "unknown");
            Require(Rejects(e), "Unknown attack-analysis provenance must fail clearly.");
            version.SetValue(e, "stem-high-flux-1"); times.SetValue(e, Array.Empty<double>()); confidence.SetValue(e, Array.Empty<double>());
            Seal(e); PcmVocalAnalysis.Validate(e, e.sourcePcmSha256, 44100, 1, 44100);
            Debug.Log("PCM_VOCAL_EVIDENCE PASS: legacy digest compatibility, separate domain, timing/confidence corruption, non-finite data, shape, provenance and empty optional features.");
        }

        private static PcmVocalAnalysis.Evidence Fixture() => new PcmVocalAnalysis.Evidence
        {
            schemaVersion = 1, featureQuantization = 100000000, sampleRate = 44100, channels = 1, sampleFrames = 44100,
            sourcePcmSha256 = new string('0', 64), sourceFileSha256 = new string('1', 64), rmsStep = .01,
            separatorModelSha256 = "ce74ef3b6a6024ce44211a07be9cf8bc6d87728cc852a68ab34eb8e58cde9c8b",
            noteModelSha256 = "2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec",
            pitchTimes = Enumerable.Range(0, 100).Select(i => i * .01).ToArray(), pitch = Enumerable.Repeat(60, 100).ToArray(),
            pitchConfidence = Enumerable.Repeat(.7, 100).ToArray(), onset = Enumerable.Repeat(.1, 100).ToArray(),
            rms = Enumerable.Repeat(.1, 100).ToArray(), voiceStarts = new[] { .1 }
        };
        private static void Seal(PcmVocalAnalysis.Evidence e) => e.featureSha256 = (string)typeof(PcmVocalAnalysis)
            .GetMethod("FeatureHash", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { e });
        private static bool Rejects(PcmVocalAnalysis.Evidence e)
        { try { PcmVocalAnalysis.Validate(e, e.sourcePcmSha256, 44100, 1, 44100); } catch (InvalidDataException) { return true; } return false; }
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
    }
}
