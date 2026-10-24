using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class PcmPhraseAnalysisValidation
    {
        [MenuItem("Dancing Cats/Validate recurrent phrase analysis")]
        public static void RunChecks()
        {
            var recording = Recording(true, true);
            var repeated = PcmSpectralAnalysis.Analyze(recording, 11025, 1).follows;
            Require(repeated.Length == 2, "Two repeated, tonal legato gestures need two Follow proposals; found " + repeated.Length);
            Require(Math.Abs(repeated[0].time - 1) < .16 && Math.Abs(repeated[1].time - 6) < .16,
                "The two gesture starts follow actual spectral attacks.");
            Require(repeated[0].endTime > 2.1 && repeated[0].endTime < 2.45 && repeated[1].endTime > 7.1 && repeated[1].endTime < 7.45,
                "Follow proposals end at measured terminal articulations.");
            Require(PcmSpectralAnalysis.Analyze(Recording(false, true), 11025, 1).follows.Length == 0,
                "Repeated isolated drum attacks do not imply continuous Follow support.");
            Require(PcmSpectralAnalysis.Analyze(Recording(true, false), 11025, 1).follows.Length == 0,
                "A single occurrence is insufficient evidence for this recurrence detector.");
            CheckCompilation(recording, repeated);
            Debug.Log("PCM_PHRASE_ANALYSIS PASS: repeated tonal gesture, measured boundaries, isolated drum rejection, single-occurrence rejection, native Follow compilation, support suppression, disabled options and finish cutoff.");
        }

        private static void CheckCompilation(float[] recording, MusicalCue[] proposals)
        {
            var map = AudioChartGenerator.GenerateFromSamples(recording, 11025, 1, "PhraseIntegration",
                new AudioChartGenerator.Options { includeHolds = false });
            var glides = map.segments.Where(segment => segment.kind == "Follow").ToArray();
            Require(glides.Length == proposals.Length, "The PCM generator must compile both confirmed phrase proposals, including the first and last.");
            Require(!map.segments.Any(segment => segment.kind == "Hold"), "Disabling Holds also applies when Follow generation is enabled.");
            for (int index = 0; index < glides.Length; index++)
            {
                var glide = glides[index]; var proposal = proposals[index];
                Require(Math.Abs(glide.audioTime - proposal.time) < .000001 && Math.Abs(glide.endAudioTime - proposal.endTime) < .000001,
                    "Follow integration must preserve the measured phrase boundaries.");
                Require(glide.elementIndices.Length == 6 && glide.path.Length == 6,
                    "Each detected phrase must retain its six authored route samples.");
                var entry = map.elements[glide.elementIndices[0]];
                var exit = map.elements[glide.elementIndices[glide.elementIndices.Length - 1]];
                Require(entry.id == 4 && entry.updateType == 2 && entry.jumpDistance == 0
                    && exit.id == 6 && exit.updateType == 0 && exit.jumpDistance > 0,
                    "A detected Follow needs the native moving entry, stationary exit and outgoing arc.");
                Require(!map.elements.Any(element => (element.kind == "Jump" || element.kind == "Hold")
                    && element.audioTime >= glide.audioTime - .000001 && element.audioTime <= glide.endAudioTime + .077),
                    "Articulations inside continuous Follow support must not produce overlapping ordinary tiles.");
                var next = map.elements[exit.sourceIndex + 1];
                Require(Math.Abs(exit.jumpDistance - (next.pos.z - glide.endZ)) < .0003,
                    "The first airborne arc after a Follow must depart from its measured endpoint.");
                foreach (int elementIndex in glide.elementIndices)
                {
                    int sample = (int)Math.Round(map.elements[elementIndex].audioTime * 11025);
                    Require(map.events.Count(item => item.mStartSample == sample) == 1,
                        "An articulation that becomes a Follow sample must score exactly once.");
                }
            }
            var ordinary = AudioChartGenerator.GenerateFromSamples(recording, 11025, 1, "PhraseDisabled",
                new AudioChartGenerator.Options { includeHolds = false, includeMovingJumps = false });
            Require(!ordinary.segments.Any(segment => segment.kind == "Follow" || segment.kind == "Hold")
                && !ordinary.elements.Any(element => element.id == 4 || element.id == 5 || element.id == 6),
                "Disabling moving phrases and Holds must retain an ordinary-jump PCM chart.");
            // Keep the same full recording and phrase evidence. A slower forward speed moves
            // the technical finish earlier, so the second phrase no longer leaves an exit arc.
            var cutoff = AudioChartGenerator.GenerateFromSamples(recording, 11025, 1, "PhraseFinishCutoff",
                new AudioChartGenerator.Options { includeHolds = false, forwardSpeed = 2 });
            var retained = cutoff.segments.Where(segment => segment.kind == "Follow").ToArray();
            Require(retained.Length == 1 && Math.Abs(retained[0].audioTime - proposals[0].time) < .000001,
                "Finish filtering must retain the first phrase while rejecting a terminal phrase without finish clearance.");
            Require(cutoff.segments.All(segment => segment.kind != "Follow"
                || segment.endAudioTime <= cutoff.elements[cutoff.elements.Length - 1].audioTime - .18 + .000001),
                "Every compiled phrase must leave the required finish arc.");
        }

        private static float[] Recording(bool tonal, bool repeat)
        {
            const int rate = 11025;
            var samples = new float[rate * 10];
            double[] relative = { 0, .23, .75, .93, 1.27 };
            double[] phraseStarts = repeat ? new[] { 1.0, 6.0 } : new[] { 1.0 };
            for (int index = 0; index < samples.Length; index++)
            {
                double time = (double)index / rate, value = 0;
                foreach (double start in phraseStarts)
                {
                    double local = time - start;
                    if (tonal && local >= -.04 && local < 1.42)
                    {
                        double envelope = Math.Min(1, Math.Min((local + .04) / .04, (1.42 - local) / .08));
                        for (int partial = 1; partial <= 6; partial++) value += .16 * envelope * Math.Sin(2 * Math.PI * 440 * partial * time) / partial;
                    }
                    foreach (double attack in relative)
                    {
                        double elapsed = local - attack;
                        if (elapsed >= 0 && elapsed < .055)
                            value += .5 * Math.Exp(-elapsed * 65) * Math.Sin(2 * Math.PI * (3200 * elapsed + 1200 * elapsed * elapsed));
                    }
                    double extra = local - 2.15;
                    if (extra >= 0 && extra < .04) value += .45 * Math.Exp(-extra * 80) * Math.Sin(2 * Math.PI * 3600 * extra);
                }
                samples[index] = (float)value;
            }
            return samples;
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException("PCM_PHRASE_ANALYSIS FAIL: " + message); }
    }
}
