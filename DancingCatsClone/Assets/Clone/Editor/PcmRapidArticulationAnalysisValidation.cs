using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Real PCM counterexamples for rapid instrumental notes, independent of a reference level.</summary>
    public static class PcmRapidArticulationAnalysisValidation
    {
        private const int Rate = 11025, NoteCount = 24;

        [MenuItem("Dancing Cats/Validate rapid instrumental articulation")]
        public static void RunChecks()
        {
            var changing = Recording(true, false, false, false, NoteCount);
            var rapid = Rapid(PcmSpectralAnalysis.Analyze(changing, Rate, 1));
            Require(Enumerable.Range(0, NoteCount).Count(i => rapid.Any(time => Math.Abs(time - (1 + i * .125)) <= .06)) >= 20,
                "a coherent 125ms pitched run must retain at least twenty of its twenty-four measured notes");
            var repeated = Rapid(PcmSpectralAnalysis.Analyze(Recording(true, true, false, false, NoteCount), Rate, 1));
            Require(Enumerable.Range(0, NoteCount).Count(i => repeated.Any(time => Math.Abs(time - (1 + i * .125)) <= .06)) >= 20,
                "repeated same-pitch instrumental notes with real amplitude attacks must survive the old 160ms suppression");
            var accompanied = Rapid(PcmSpectralAnalysis.Analyze(Recording(true, false, false, false, NoteCount, true), Rate, 1));
            Require(Enumerable.Range(0, NoteCount).Count(i => accompanied.Any(time => Math.Abs(time - (1 + i * .125)) <= .06)) >= 20,
                "a quiet rapid melody remains measurable when a louder unchanged harmonic accompaniment owns the strongest pitch");
            Require(Rapid(PcmSpectralAnalysis.Analyze(Recording(false, false, true, false, NoteCount), Rate, 1)).Length == 0,
                "rapid hi-hats over an unchanged harmonic bed cannot create melodic rapid-note tiles");
            Require(Rapid(PcmSpectralAnalysis.Analyze(Recording(false, false, false, true, NoteCount), Rate, 1)).Length == 0,
                "unpitched drum hits over an unchanged harmonic bed cannot create melodic rapid-note tiles");
            Require(Rapid(PcmSpectralAnalysis.Analyze(Recording(false, false, false, false, NoteCount), Rate, 1)).Length == 0,
                "a constant harmonic bed has no rapid articulations");
            Require(Rapid(PcmSpectralAnalysis.Analyze(Recording(true, false, false, false, 2), Rate, 1)).Length == 0,
                "an isolated close pair is insufficient evidence for a coherent rapid instrumental run");
            var stereo = new float[changing.Length * 2];
            for (int i = 0; i < changing.Length; i++) { stereo[i * 2] = changing[i]; stereo[i * 2 + 1] = -changing[i]; }
            var opposite = Rapid(PcmSpectralAnalysis.Analyze(stereo, Rate, 2));
            Require(rapid.SequenceEqual(opposite), "antiphase stereo preserves the same real instrumental articulation times");
            Require(rapid.Zip(rapid.Skip(1), (left, right) => right - left).All(gap => gap >= .078 - .000001),
                "rapid proposals still respect the native minimum landing interval");
            Require(rapid.All(time => time >= .9 && time <= 4.05), "silence around a real run receives no timer-based filler");
            Debug.Log("PCM_RAPID_ARTICULATION PASS: real 125ms pitched and repeated-note runs; hi-hat/drum/constant-bed rejection; isolated pair, native spacing, surrounding silence and antiphase stereo.");
        }

        private static double[] Rapid(PcmSpectralAnalysis.Result result)
        {
            var field = typeof(PcmSpectralAnalysis.Result).GetField("rapidAttacks");
            Require(field != null, "the spectral analyzer must expose separately confirmed rapid instrumental attacks");
            return (double[])field.GetValue(result);
        }

        private static float[] Recording(bool notes, bool samePitch, bool hats, bool drums, int count, bool accompaniment = false)
        {
            var samples = new float[Rate * 6];
            int[] melody = { 69, 72, 74, 76, 74, 72 };
            uint random = 1979;
            for (int i = 0; i < samples.Length; i++)
            {
                double time = i / (double)Rate, value = 0;
                if (!notes || accompaniment)
                    for (int partial = 1; partial <= 6; partial++) value += (accompaniment ? .32 : .09) / partial
                        * Math.Sin(2 * Math.PI * (accompaniment ? 261.625565 : 440) * partial * time);
                for (int note = 0; note < count; note++)
                {
                    double elapsed = time - (1 + note * .125);
                    if (elapsed < 0 || elapsed > .105) continue;
                    if (notes)
                    {
                        double frequency = 440 * Math.Pow(2, ((samePitch ? 69 : melody[note % melody.Length]) - 69) / 12.0);
                        double envelope = Math.Min(1, elapsed / .003) * Math.Exp(-elapsed * 14)
                            * Math.Min(1, (.105 - elapsed) / .008);
                        for (int partial = 1; partial <= 6; partial++)
                            value += (accompaniment ? .09 : .22) / partial * envelope * Math.Sin(2 * Math.PI * frequency * partial * elapsed);
                    }
                    if (hats && elapsed < .03)
                    {
                        random = 1664525 * random + 1013904223;
                        double noise = (random / (double)uint.MaxValue) * 2 - 1;
                        value += .35 * Math.Exp(-elapsed * 85) * (noise + Math.Sin(2 * Math.PI * 4000 * elapsed));
                    }
                    if (drums && elapsed < .06)
                        value += .5 * Math.Exp(-elapsed * 60) * Math.Sin(2 * Math.PI * (160 * elapsed - 700 * elapsed * elapsed));
                }
                samples[i] = (float)value;
            }
            return samples;
        }

        private static void Require(bool passed, string message)
        { if (!passed) throw new InvalidDataException("PCM_RAPID_ARTICULATION FAIL: " + message); }
    }
}
