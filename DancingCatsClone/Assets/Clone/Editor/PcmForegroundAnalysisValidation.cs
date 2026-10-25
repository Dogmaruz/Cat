using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Counterfactual foreground decisions; reference maps never enter these fixtures.</summary>
    public static class PcmForegroundAnalysisValidation
    {
        [MenuItem("Dancing Cats/Validate foreground chart selection")]
        public static void RunChecks()
        {
            var type = typeof(PcmForegroundAnalysisValidation).Assembly.GetType("DancingCats.Clone.Editor.PcmForegroundAnalysis");
            Require(type != null, "foreground selection is missing");
            var apply = type.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
            Require(apply != null, "foreground selection entry point is missing");
            MusicalCue[] Run(MusicalCue[] cues, PcmVocalAnalysis.Evidence evidence, double latest = 8)
                => (MusicalCue[])apply.Invoke(null, new object[] { cues, evidence, latest });

            var ordinary = new[] { Jump(1), Jump(1.25), Jump(1.5), Jump(2) };
            var voice = Voice(new[] { 1.0, 1.5, 2.0 });
            var selected = Run(ordinary, voice);
            Require(selected.Length == 3 && selected.All(cue => !Near(cue.time, 1.25)),
                "an accompaniment attack inside reliable voice must not create another ordinary tile");
            Require(selected.Any(cue => ReferenceEquals(cue, ordinary[0])) && ordinary.Length == 4 && Near(ordinary[1].time, 1.25),
                "selection preserves surviving cue identities and never mutates its input");

            var silence = Voice(new[] { 1.0, 2.0 });
            for (int i = 115; i < 140; i++) silence.rms[i] = 0;
            Require(Run(ordinary, silence).Any(cue => Near(cue.time, 1.25)),
                "instrumental attacks remain available during an actual vocal rest");
            Require(Run(ordinary, null).Length == 4 && Run(ordinary, new PcmVocalAnalysis.Evidence { schemaVersion = 1 }).Length == 4,
                "missing and historical feature caches retain the measured physical route");
            Require(Run(ordinary, Voice(Array.Empty<double>())).Length == 4 && Run(ordinary, Voice(new[] { 1.0 }, .3)).Length == 4,
                "empty or entirely uncertain foreground detections cannot silence a chart");
            var uncertainBody = Voice(new[] { 1.0, 1.5, 2.0 });
            for (int i = 0; i < uncertainBody.pitchConfidence.Length; i++) uncertainBody.pitchConfidence[i] = .2;
            Require(Run(ordinary, uncertainBody).Length == ordinary.Length,
                "an uncertain separated foreground body cannot erase physical entries or releases");

            var fastVoice = Voice(new[] { 1.0, 1.125, 1.25, 1.375, 1.5 });
            var fast = Run(new[] { Jump(1), Jump(1.25), Jump(1.5) }, fastVoice);
            Require(fast.Length == 5 && fast.Any(cue => Near(cue.time, 1.125)) && fast.Any(cue => Near(cue.time, 1.375)),
                "confirmed 125ms foreground reattacks survive a 160ms mixed-source refractory");
            Require(Run(new[] { Jump(1), Jump(1.25), Jump(1.5) }, Voice(new[] { 1.0, 1.125, 1.25, 1.375, 1.5 }, .5)).Length == 3,
                "weak onset confidence does not create rapid extra tiles");
            Require(Run(new[] { Jump(1), Jump(1.23) }, Voice(new[] { 1.0, 1.09, 1.23 })).Length == 2,
                "irregular within-syllable modulation is insufficient for the rapid articulation policy");
            Require(Run(new[] { Jump(1), Jump(1.125) }, Voice(new[] { 1.0, 1.125 })).Length == 2,
                "an isolated close pair cannot establish a rapid run");

            var hold = new MusicalCue { kind = "Hold", time = 1, endTime = 2 };
            var follow = new MusicalCue { kind = "Follow", time = 3, endTime = 4,
                points = new[] { new MusicalPoint { time = 3, lane = .8f }, new MusicalPoint { time = 4, lane = -.8f } } };
            var big = new MusicalCue { kind = "BigJump", time = 5, endTime = 5 };
            var landing = Jump(6);
            var supports = new[] { hold, follow, big, landing, Jump(7) };
            var protectedResult = Run(supports, Voice(new[] { 1.125, 1.25, 1.375, 3.125, 3.25, 3.375, 5.125, 5.25, 5.375, 7.0 }));
            Require(protectedResult.Length == supports.Length && supports.All(cue => protectedResult.Any(result => ReferenceEquals(cue, result))),
                "Hold, Follow, BigJump, its complete outgoing arc and its actual landing remain protected");

            var afterFollow = Run(new[] { follow, Jump(4.5) }, Voice(new[] { 4.125, 4.25, 4.375, 4.5 }));
            var restored = afterFollow.Where(cue => cue.kind == "Jump" && cue.time < 4.5).ToArray();
            Require(restored.Length == 3 && Near(restored[0].lane, -.6) && Near(restored[1].lane, -.4) && Near(restored[2].lane, -.2),
                "rapid restored tiles interpolate from the Follow exit, preserving reachable lateral steps");

            var crowded = Run(new[] { Jump(1.05), Jump(1.5) }, Voice(new[] { 1.0, 1.125, 1.25, 1.375, 1.5 }));
            Require(crowded.Zip(crowded.Skip(1), (a, b) => b.time - a.endTime).All(gap => gap >= .078 - 1e-8),
                "restoring foreground detail must preserve the native minimum takeoff-to-landing interval");
            var limited = Run(new[] { Jump(1), Jump(1.25) }, fastVoice, 1.30);
            Require(limited.All(cue => cue.time <= 1.30), "new attacks must leave the technical finish arc clear");

            var shifted = Run(new[] { Jump(2), Jump(2.25), Jump(2.5) }, Voice(new[] { 2.0, 2.125, 2.25, 2.375, 2.5 }));
            Require(shifted.Select(cue => Math.Round(cue.time - 1, 6)).SequenceEqual(fast.Select(cue => Math.Round(cue.time, 6))),
                "time translation moves foreground detail rather than selecting song-specific positions");
            var malformed = Voice(new[] { 1.0 }); Set(malformed, "vocalAttackConfidence", Array.Empty<double>());
            bool rejected = false;
            try { Run(ordinary, malformed); }
            catch (TargetInvocationException error) { rejected = error.InnerException is ArgumentException; }
            Require(rejected, "inconsistent foreground vectors fail explicitly");
            var instrumentalMethod = type.GetMethod("ApplyInstrumentalRuns", BindingFlags.Public | BindingFlags.Static);
            Require(instrumentalMethod != null, "verified instrumental rapid articulation insertion is missing");
            MusicalCue[] Instrumental(MusicalCue[] input, PcmVocalAnalysis.Evidence vocal, double[] attacks, double latest = 8)
                => (MusicalCue[])instrumentalMethod.Invoke(null, new object[] { input, vocal, attacks, latest });
            var instrumentalSource = new[] { Jump(1), Jump(1.25), Jump(1.5) };
            var measuredRun = new[] { 1.0, 1.125, 1.25, 1.375, 1.5 };
            Require(Instrumental(instrumentalSource, null, measuredRun).Length == 5,
                "source-confirmed harmonic rapid notes can restore 125ms instrumental detail without a vocal cache");
            Require(Instrumental(instrumentalSource, Voice(Array.Empty<double>()), measuredRun).Length == 3,
                "an instrumental branch cannot reintroduce accompaniment while actual voice continues");
            var instrumentalVoice = Voice(Array.Empty<double>()); Array.Clear(instrumentalVoice.rms, 0, instrumentalVoice.rms.Length);
            Require(Instrumental(instrumentalSource, instrumentalVoice, measuredRun).Length == 5,
                "verified harmonic reattacks remain available through an actual vocal rest");
            var reservedInstrumental = Instrumental(supports, null,
                new[] { 1.125, 1.25, 1.375, 3.125, 3.25, 3.375, 5.125, 5.25, 5.375 });
            Require(reservedInstrumental.Length == supports.Length && supports.All(cue => reservedInstrumental.Any(item => ReferenceEquals(cue, item))),
                "instrumental insertion protects existing supports, the complete BigJump arc and its landing");
            var instrumentalExit = Instrumental(new[] { follow, Jump(4.5) }, null, new[] { 4.125, 4.25, 4.375, 4.5 });
            Require(instrumentalExit.Any(cue => Near(cue.time, 4.125) && Near(cue.lane, -.6)),
                "instrumental insertion uses Follow exit geometry rather than its entry lane");
            Require(Instrumental(new[] { Jump(1.05), Jump(1.5) }, null, measuredRun)
                .Zip(Instrumental(new[] { Jump(1.05), Jump(1.5) }, null, measuredRun).Skip(1), (a, b) => b.time - a.endTime)
                .All(gap => gap >= .078 - 1e-8), "instrumental insertion preserves native minimum gaps");
            Debug.Log("PCM_FOREGROUND PASS: accompaniment rejection, weak-voice physical preservation, instrumental fallback, legacy/uncertain cache, immutable cues, "
                + "confirmed 125ms articulation, weak/irregular rejection, protected supports and BigJump landing, Follow exit lanes, spacing, finish and time translation.");
        }

        private static MusicalCue Jump(double time) => new MusicalCue { kind = "Jump", time = time, endTime = time };
        private static PcmVocalAnalysis.Evidence Voice(double[] attacks, double confidence = .9)
        {
            var result = new PcmVocalAnalysis.Evidence { schemaVersion = 2, rmsStep = .01,
                rms = Enumerable.Repeat(.1, 1000).ToArray(),
                pitchTimes = Enumerable.Range(0, 1000).Select(i => i * .01).ToArray(),
                pitchConfidence = Enumerable.Repeat(.8, 1000).ToArray() };
            Set(result, "vocalAttackTimes", attacks);
            Set(result, "vocalAttackConfidence", Enumerable.Repeat(confidence, attacks.Length).ToArray());
            return result;
        }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name);
            Require(field != null, "vocal evidence is missing " + name);
            field.SetValue(target, value);
        }
        private static bool Near(double a, double b) => Math.Abs(a - b) < .000001;
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException("PCM_FOREGROUND RED/FAIL: " + message); }
    }
}
