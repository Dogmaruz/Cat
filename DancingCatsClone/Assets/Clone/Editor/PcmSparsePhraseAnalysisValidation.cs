using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Counterexamples for evidence-supported traversal through sparse musical passages.</summary>
    public static class PcmSparsePhraseAnalysisValidation
    {
        [MenuItem("Dancing Cats/Validate sparse musical support")]
        public static void RunChecks()
        {
            var type = typeof(PcmSparsePhraseAnalysisValidation).Assembly.GetType("DancingCats.Clone.Editor.PcmSparsePhraseAnalysis");
            Require(type != null, "sparse phrase analysis is missing");
            var method = type.GetMethod("Complete", BindingFlags.Public | BindingFlags.Static);
            Require(method != null, "sparse phrase completion entry point is missing");
            MusicalCue[] Run(MusicalCue[] cues, PcmVocalAnalysis.Evidence voice, MusicalNote[] notes, double finish = 4.5)
                => (MusicalCue[])method.Invoke(null, new object[] { cues, voice, notes, finish, 1.95 });
            var plain = new[] { Jump(1) };
            var voice = Voice(2.5);
            var voiced = Run(plain, voice, Array.Empty<MusicalNote>());
            Require(voiced.Length == 2 && voiced[1].kind == "Jump" && Near(voiced[1].time, 2.5), "a measured audible confident vocal entry can bridge a sparse interval");
            var silent = Voice(2.5); Array.Clear(silent.rms, 0, silent.rms.Length);
            Require(Run(plain, silent, Array.Empty<MusicalNote>()).Length == 1, "silence stays unresolved; no periodic or beat-grid filler is allowed");
            var unvoiced = Voice(2.5); Array.Clear(unvoiced.pitchConfidence, 0, unvoiced.pitchConfidence.Length);
            Require(Run(plain, unvoiced, Array.Empty<MusicalNote>()).Length == 1, "energy without foreground pitch confidence does not invent a vocal landing");
            var held = Run(plain, null, new[] { Note(2, 3.5) });
            Require(held.Length == 2 && held[1].kind == "Hold" && Near(held[1].time, 2) && Near(held[1].endTime, 3.47), "a measured stable multi-partial body adds support at its own onset and release");
            var upgraded = Run(plain, null, new[] { Note(.8, 2.8) });
            Require(upgraded[0].kind == "Hold" && Near(upgraded[0].time, 1) && Near(upgraded[0].endTime, 2.77), "a harmonic note spanning the preceding attack can extend that support");
            Require(plain[0].kind == "Jump" && Near(plain[0].endTime, 1), "completion never mutates the input cue objects");
            var tail = Run(new[] { Jump(3) }, null, new[] { Note(2.8, 4.3), Note(5, 8) }, 6.5);
            Require(tail.Length == 2 && tail.All(c => c.kind == "Hold") && Near(tail[0].endTime, 4.27) && Near(tail[1].time, 5) && Near(tail[1].endTime, 6.32), "two measured harmonic bodies can bridge a sparse outro, with room for the finish arc");
            var protectedHold = new MusicalCue { kind = "Hold", time = 1, endTime = 2, lane = .4f };
            var protectedFollow = new MusicalCue { kind = "Follow", time = 5, endTime = 6, points = new[] { new MusicalPoint { time = 5, lane = -.5f }, new MusicalPoint { time = 6, lane = .5f } } };
            var protectedResult = Run(new[] { protectedHold, protectedFollow }, Voice(1.5), new[] { Note(.8, 4) }, 7);
            Require(protectedResult.Length == 2 && ReferenceEquals(protectedResult[0], protectedHold) && ReferenceEquals(protectedResult[1], protectedFollow), "existing Hold/Follow intervals and their path data are protected");
            var next = Jump(4);
            var clamped = Run(new[] { Jump(1), next }, null, new[] { Note(2, 6) }, 5);
            Require(clamped.Length == 3 && Near(clamped[1].endTime, 3.82) && ReferenceEquals(clamped[2], next), "a long harmonic body stops before the next existing attack instead of overwriting it");
            var ordinary = new[] { Jump(1), Jump(2.8) };
            var ordinaryResult = Run(ordinary, Voice(2), new[] { Note(1.5, 2.5) }, 4.5);
            Require(ordinaryResult.Length == 2 && ReferenceEquals(ordinaryResult[0], ordinary[0]) && ReferenceEquals(ordinaryResult[1], ordinary[1]), "already playable gaps retain their original musical decisions");
            var tooClose = Run(plain, Voice(4.4), Array.Empty<MusicalNote>());
            Require(tooClose.Length == 1, "a voice entry too close to the following cue does not create an unplayable pair");
            var moving = new MusicalCue { kind = "Follow", time = 1, endTime = 2, lane = .8f,
                points = new[] { new MusicalPoint { time = 1, lane = .8f }, new MusicalPoint { time = 2, lane = -.8f } } };
            var following = Run(new[] { moving }, Voice(3.2), Array.Empty<MusicalNote>(), 5);
            Require(following.Length == 2 && following[1].lane == -.8f, "sparse traversal leaves a Follow from its exit lane, without jumping back to its entry side");
            Require(Run(plain, null, new[] { Note(2, 3.5, .5) }).Length == 1, "an uncertain mixed pitch body cannot manufacture support");
            var shifted = Run(new[] { Jump(2) }, Voice(3.5), Array.Empty<MusicalNote>(), 5.5);
            Require(shifted.Length == 2 && Near(shifted[1].time, 3.5), "time translation moves proposals with measured evidence");
            Debug.Log("PCM_SPARSE_PHRASE PASS: measured vocal and harmonic entries, silence and uncertainty, protected supports, immutable inputs, next-attack and finish clamps, ordinary-gap preservation and time translation.");
        }

        private static MusicalCue Jump(double time) => new MusicalCue { kind = "Jump", time = time, endTime = time };
        private static MusicalNote Note(double start, double end, double confidence = .8) => new MusicalNote { start = start, end = end, confidence = confidence, midi = 60 };
        private static bool Near(double a, double b) => Math.Abs(a - b) < .000001;
        private static PcmVocalAnalysis.Evidence Voice(double entry)
        {
            const int count = 1000;
            return new PcmVocalAnalysis.Evidence { rmsStep = .01, voiceStarts = new[] { entry },
                pitchTimes = Enumerable.Range(0, count).Select(i => i * .01).ToArray(),
                pitchConfidence = Enumerable.Repeat(.8, count).ToArray(), rms = Enumerable.Repeat(.08, count).ToArray() };
        }
        private static void Require(bool pass, string message)
        { if (!pass) throw new InvalidDataException("Sparse phrase regression: " + message); }
    }
}
