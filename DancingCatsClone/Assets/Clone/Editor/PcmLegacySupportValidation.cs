using System;
using System.Linq;
using UnityEngine;
namespace DancingCats.Clone.Editor
{
    public static class PcmLegacySupportValidation
    {
        [UnityEditor.MenuItem("Dancing Cats/Validate legacy support articulation")]
        public static void RunChecks()
        {
            bool Reject(PcmInstrumentalEvidence.Evidence e, PcmVocalAnalysis.Evidence v, MusicalCue c)
             => PcmLegacySupportAnalysis.RejectsArticulatedHold(e, v, c);
            var e = Instrument(); var v = Voice(); var cue = Cue();
            Require(Reject(e, v, cue), "Two fresh interior pitched bodies, each independently confirmed by voice, must challenge an overlong protected Hold.");
            Require(!Reject(null, v, cue) && !Reject(e, null, cue) && !Reject(e, v, null), "Unavailable optional evidence never vetoes a support.");
            Require(!Reject(e, v, new MusicalCue { kind = "Follow", time = 2, endTime = 3.2 }), "Follow arbitration is unchanged.");
            Require(!Reject(e, v, new MusicalCue { kind = "Hold", time = 2.3, endTime = 2.7 }), "This verification never challenges short supports.");
            var one = Instrument(); one.events = one.events.Take(1).ToArray(); Require(!Reject(one, v, cue), "One articulated release is compatible with a legitimate held plateau.");
            var same = Instrument(); same.events[1].midi = 64; var sameVoice = Voice(); for (int i = 0; i < sameVoice.pitch.Length; i++) if (sameVoice.pitch[i] == 60) sameVoice.pitch[i] = 64;
            Require(!Reject(same, sameVoice, cue), "Repeated detections of one pitch cannot establish two distinct body pitches.");
            var weak = Instrument(); foreach (var n in weak.events) Set(n, "neuralOnset", .59); Require(!Reject(weak, v, cue), "Weak mixed note evidence cannot invalidate a legacy support.");
            var stable = Instrument(); foreach (var n in stable.events) Set(n, "harmonicNovelty", 0); Require(!Reject(stable, v, cue), "Stable harmonic leakage cannot establish new pitched bodies.");
            var disagreement = Instrument(); foreach (var n in disagreement.events) Set(n, "neuralDuration", .8); Require(!Reject(disagreement, v, cue), "Disagreeing neural and acoustic duration estimates cannot veto a support.");
            var uncertain = Voice(); for (int i = 0; i < uncertain.pitchConfidence.Length; i++) uncertain.pitchConfidence[i] = .49; Require(!Reject(e, uncertain, cue), "Uncertain separated voice never corroborates a mixed-pitch claim.");
            var octave = Voice(); for (int i = 0; i < octave.pitch.Length; i++) octave.pitch[i] += 12; Require(!Reject(e, octave, cue), "Octave-related accompaniment alone does not confirm the actual foreground pitch.");
            var brief = Voice(); for (int i = 0; i < brief.pitchConfidence.Length; i++) if (brief.pitchTimes[i] > 2.255 && brief.pitchTimes[i] < 2.6) brief.pitchConfidence[i] = 0;
            Require(!Reject(e, brief, cue), "A fleeting pitch coincidence below 60 ms is insufficient corroboration.");
            var edge = Instrument(); edge.events[0].time = 2.1; edge.events[0].endTime = 2.34; Require(!Reject(edge, v, cue), "Entry articulation is not a fresh interior body.");
            var simultaneous = Instrument(); simultaneous.events[1].time = simultaneous.events[0].time; Require(!Reject(simultaneous, v, cue), "A simultaneous chord is one boundary, not multiple articulations.");
            string before = UnityEngine.JsonUtility.ToJson(e) + UnityEngine.JsonUtility.ToJson(v) + UnityEngine.JsonUtility.ToJson(cue);
            Require(Reject(e, v, cue) && before == UnityEngine.JsonUtility.ToJson(e) + UnityEngine.JsonUtility.ToJson(v) + UnityEngine.JsonUtility.ToJson(cue), "Verification must be deterministic and leave all evidence/cue bytes unchanged.");
            var shifted = Instrument(); var shiftedVoice = Voice(); var shiftedCue = Cue();
            foreach (var n in shifted.events) { n.time += .1234567; n.endTime += .1234567; n.midi += 7; }
            for (int i = 0; i < shiftedVoice.pitchTimes.Length; i++) { shiftedVoice.pitchTimes[i] += .1234567; shiftedVoice.pitch[i] += 7; }
            shiftedCue.time += .1234567; shiftedCue.endTime += .1234567;
            Require(Reject(shifted, shiftedVoice, shiftedCue), "Clock and pitch translations cannot change the decision.");
            Debug.Log("PCM_LEGACY_SUPPORT PASS: 16 checks for independent voiced articulation, sustained and repeated-pitch counterexamples, source agreement, protected short supports, immutable inputs and pitch/time invariance.");
        }
        static MusicalCue Cue() => new MusicalCue { kind = "Hold", time = 2, endTime = 3.2 };
        static PcmInstrumentalEvidence.Evidence Instrument() => new PcmInstrumentalEvidence.Evidence { events = new[] { Note(2.2, 2.44, 64), Note(2.6, 2.95, 60) } };
        static PcmVocalAnalysis.Evidence Voice()
        {
            var times = Enumerable.Range(0, 401).Select(i => i * .01).ToArray();
            return new PcmVocalAnalysis.Evidence { pitchTimes = times, pitch = times.Select(t => t >= 2.22 && t < 2.39 ? 64 : t >= 2.62 && t < 2.8 ? 60 : 67).ToArray(), pitchConfidence = times.Select(t => .8).ToArray() };
        }
        static PcmInstrumentalEvidence.NoteEvent Note(double time, double end, int midi)
        { var n = new PcmInstrumentalEvidence.NoteEvent { time = time, endTime = end, midi = midi, features = new double[PcmInstrumentalEvidence.FeatureCount] }; Set(n, "neuralOnset", .8); Set(n, "harmonicBody", .8); Set(n, "harmonicNovelty", .1); Set(n, "harmonicPartials", 3); Set(n, "neuralDuration", end - time); return n; }
        static void Set(PcmInstrumentalEvidence.NoteEvent n, string key, double value) => n.features[PcmInstrumentalEvidence.Index(key)] = value;
        static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
    }


}


