using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Counterfactual classifier measurements, shared with the Python CLI tests. Not an ML accuracy benchmark.</summary>
    public static class PcmVocalGestureValidation
    {
        [Serializable] private sealed class Fixtures { public double durationSeconds = 0, stepSeconds = 0; public Case[] cases = Array.Empty<Case>(); }
        [Serializable] private sealed class Case
        {
            public string name = "";
            public int expectedHolds = 0;
            public double beatPeriod = 0;
            public double[] attacks = Array.Empty<double>();
            public Segment[] segments = Array.Empty<Segment>();
            public Onset[] onsets = Array.Empty<Onset>();
        }
        [Serializable] private sealed class Segment { public double start = 0, end = 0, confidence = 0, rms = 0; public int pitch = 0; }
        [Serializable] private sealed class Onset { public double time = 0, strength = 0; }

        [MenuItem("Dancing Cats/Validate vocal gesture patterns")]
        public static void RunChecks()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Tools/RhythmAnalysis/gesture_fixtures.json"));
            var fixtures = JsonUtility.FromJson<Fixtures>(File.ReadAllText(path));
            if (fixtures == null || fixtures.cases == null || fixtures.cases.Length < 10)
                throw new InvalidDataException("The shared vocal gesture fixtures are missing.");
            foreach (var fixture in fixtures.cases)
            {
                var evidence = Render(fixtures, fixture);
                var actual = PcmVocalAnalysis.Analyze(evidence, fixture.attacks, fixture.beatPeriod);
                if (actual.Length != fixture.expectedHolds)
                    throw new InvalidDataException("Vocal gesture fixture " + fixture.name + ": expected " + fixture.expectedHolds + ", actual " + actual.Length);
                for (int index = 0; index < actual.Length; index++)
                {
                    var cue = actual[index];
                    if (double.IsNaN(cue.time) || double.IsInfinity(cue.time) || double.IsNaN(cue.endTime)
                        || double.IsInfinity(cue.endTime) || cue.endTime <= cue.time
                        || index > 0 && actual[index - 1].endTime > cue.time)
                        throw new InvalidDataException("Vocal gesture fixture " + fixture.name + " produced an invalid or overlapping support.");
                }
            }
            Debug.Log("PCM_VOCAL_GESTURES PASS: " + fixtures.cases.Length + " shared counterfactual fixtures; continued voice versus real silence, "
                + "ornament versus vibrato, fading versus confident semitone, transposed pickup versus ordinary high note, "
                + "cadence rest versus intervening voice, and confirmed versus unconfirmed glide handoff. These test classifier rules, not unseen-song ML accuracy.");
        }

        private static PcmVocalAnalysis.Evidence Render(Fixtures fixtures, Case fixture)
        {
            int count = (int)Math.Round(fixtures.durationSeconds / fixtures.stepSeconds);
            var evidence = new PcmVocalAnalysis.Evidence
            {
                schemaVersion = 1, featureQuantization = 100000000, sampleRate = 44100, channels = 1,
                sampleFrames = (int)Math.Round(fixtures.durationSeconds * 44100), rmsStep = fixtures.stepSeconds,
                sourceFileSha256 = new string('0', 64), sourcePcmSha256 = new string('0', 64),
                separatorModelSha256 = "ce74ef3b6a6024ce44211a07be9cf8bc6d87728cc852a68ab34eb8e58cde9c8b",
                noteModelSha256 = "2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec",
                analysisVersion = "synthetic-classifier-fixture", pitchTimes = new double[count], pitch = new int[count],
                pitchConfidence = new double[count], onset = new double[count], rms = new double[count], voiceStarts = Array.Empty<double>()
            };
            for (int index = 0; index < count; index++)
            { evidence.pitchTimes[index] = index * fixtures.stepSeconds; evidence.pitch[index] = 60; evidence.pitchConfidence[index] = .1; evidence.onset[index] = .1; }
            foreach (var segment in fixture.segments)
            {
                int first = (int)Math.Round(segment.start / fixtures.stepSeconds), end = (int)Math.Round(segment.end / fixtures.stepSeconds);
                for (int index = first; index < end; index++)
                { evidence.pitch[index] = segment.pitch; evidence.pitchConfidence[index] = segment.confidence; evidence.rms[index] = segment.rms; }
            }
            foreach (var onset in fixture.onsets) evidence.onset[(int)Math.Round(onset.time / fixtures.stepSeconds)] = onset.strength;
            // Seal deliberately synthetic measurements without claiming they came
            // from an AudioClip or bypassing the production integrity validator.
            MethodInfo seal = typeof(PcmVocalAnalysis).GetMethod("FeatureHash", BindingFlags.NonPublic | BindingFlags.Static);
            if (seal == null) throw new MissingMethodException("Vocal evidence integrity function is missing.");
            evidence.featureSha256 = (string)seal.Invoke(null, new object[] { evidence });
            return evidence;
        }
    }
}
