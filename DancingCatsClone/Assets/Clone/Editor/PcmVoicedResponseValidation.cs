using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class PcmVoicedResponseValidation
    {
        [MenuItem("Dancing Cats/Validate measured voiced responses")]
        public static void RunChecks()
        {
            MusicalCue[] Analyze(PcmInstrumentalEvidence.Evidence e, PcmVocalAnalysis.Evidence v, MusicalCue[] supports = null)
                => PcmMelodicFollowAnalysis.AnalyzeVoicedResponses(e, v, supports ?? Array.Empty<MusicalCue>());
            var evidence = Notes(); var voice = Voice();
            var result = Analyze(evidence, voice);
            Require(result.Length == 2 && result.All(c => c.kind == "Follow" && Math.Abs(c.endTime - c.time - .75) < 1e-7),
                "Two recurring independently voiced rising pickups and stable bodies retain their measured boundaries.");
            Require(result.All(c => c.points.Length == 2), "A stable response needs native entry and exit without invented internal scoring checkpoints.");
            Require(Analyze(null, voice).Length == 0 && Analyze(evidence, null).Length == 0,
                "Unavailable optional evidence never creates a response.");
            var isolated = Notes(); isolated.events = isolated.events.Take(2).ToArray();
            Require(Analyze(isolated, voice).Length == 0, "One isolated rising body cannot establish a recurring phrase family.");
            var weak = Notes(); foreach (var n in weak.events) Set(n, "neuralOnset", .49);
            Require(Analyze(weak, voice).Length == 0, "Weak mixed neural onsets cannot establish a response.");
            var uncertain = Voice(); for (int i = 0; i < uncertain.pitchConfidence.Length; i++) uncertain.pitchConfidence[i] = .49;
            Require(Analyze(evidence, uncertain).Length == 0, "Uncertain separated voice cannot confirm a resonant mixed background.");
            var background = Voice(); for (int i = 0; i < background.pitch.Length; i++) background.pitch[i] = 67;
            Require(Analyze(evidence, background).Length == 0, "Strong mixed-pitch resonance under a different actual foreground voice is rejected.");
            var mismatch = Notes(); foreach (var n in mismatch.events.Where(n => n.midi == 72)) Set(n, "neuralDuration", 1.2);
            Require(Analyze(mismatch, voice).Length == 0, "Disagreeing physical and neural body durations cannot establish a sustained response.");
            var inner = Notes(); inner.events = inner.events.Concat(new[] { Note(2.65, 2.85, 75) }).OrderBy(n => n.time).ToArray();
            Require(Analyze(inner, voice).Length == 0, "A genuinely new source articulation inside the plateau invalidates the sustained-body interpretation.");
            var same = Notes(); foreach (var n in same.events.Where(n => n.midi == 70)) n.midi = 72;
            Require(Analyze(same, voice).Length == 0, "A repeated same-pitch entry is not a rising pickup.");
            var hold = new MusicalCue { kind = "Hold", time = 2.2, endTime = 2.95 };
            Require(Analyze(evidence, voice, new[] { hold }).Length == 1,
                "An existing valid Hold retains priority over a newly recognized response.");
            var follow = new MusicalCue
            {
                kind = "Follow",
                time = 2.2,
                endTime = 2.95,
                lane = -.4f,
                points = new[] { new MusicalPoint { time = 2.2, lane = -.4f }, new MusicalPoint { time = 2.95, lane = .4f } }
            };
            string protectedBefore = JsonUtility.ToJson(follow);
            Require(Analyze(evidence, voice, new[] { follow }).Length == 1 && JsonUtility.ToJson(follow) == protectedBefore,
                "An existing valid Follow and every protected path coordinate remain unchanged.");
            var flights = new[] { new PcmFlightAnalysis.Flight { time = 2.35, landingTime = 3.1 },
                new PcmFlightAnalysis.Flight { time = 1, landingTime = 1.8 } };
            var eligible = AudioChartGenerator.EligibleFlights(flights, voice, 5.5, result);
            Require(eligible.Length == 1 && eligible[0] == flights[1],
                "Only a flight overlapping a newly confirmed sustained body yields; unrelated flight protection remains active.");
            var shifted = Notes(); var shiftedVoice = Voice();
            foreach (var n in shifted.events) { n.time += .1234567; n.endTime += .1234567; n.midi += 7; }
            for (int i = 0; i < shiftedVoice.pitchTimes.Length; i++) { shiftedVoice.pitchTimes[i] += .1234567; shiftedVoice.pitch[i] += 7; }
            var shiftedResult = Analyze(shifted, shiftedVoice);
            Require(shiftedResult.Length == result.Length && shiftedResult.Zip(result, (a, b) =>
                Math.Abs(a.time - b.time - .1234567) < 1e-7 && Math.Abs(a.endTime - b.endTime - .1234567) < 1e-7).All(v => v),
                "Source clock and pitch translations preserve the phrase decision.");
            string before = JsonUtility.ToJson(evidence) + JsonUtility.ToJson(voice);
            Require(JsonUtility.ToJson(new CueSnapshot { cues = result }) == JsonUtility.ToJson(new CueSnapshot { cues = Analyze(evidence, voice) })
                && before == JsonUtility.ToJson(evidence) + JsonUtility.ToJson(voice),
                "Response analysis is deterministic and leaves source evidence unchanged.");
            Debug.Log("PCM_VOICED_RESPONSES PASS: measured recurring rising bodies, weak/background/mismatched/isolated/articulated counterexamples, protected legacy supports, targeted flight arbitration, immutable source and pitch/time invariance.");
        }

        [Serializable] private sealed class CueSnapshot { public MusicalCue[] cues; }
        private static PcmInstrumentalEvidence.Evidence Notes() => new PcmInstrumentalEvidence.Evidence
        { events = new[] { Note(2.2, 2.35, 70), Note(2.35, 2.95, 72), Note(4.2, 4.35, 70), Note(4.35, 4.95, 72) } };
        private static PcmInstrumentalEvidence.NoteEvent Note(double time, double end, int midi)
        {
            var n = new PcmInstrumentalEvidence.NoteEvent
            {
                time = time,
                endTime = end,
                midi = midi,
                features = new double[PcmInstrumentalEvidence.FeatureCount]
            };
            Set(n, "neuralOnset", .8); Set(n, "harmonicBody", .8); Set(n, "harmonicNovelty", .1);
            Set(n, "harmonicPartials", 4); Set(n, "neuralDuration", end - time); return n;
        }
        private static PcmVocalAnalysis.Evidence Voice()
        {
            var times = Enumerable.Range(0, 601).Select(i => i * .01).ToArray();
            return new PcmVocalAnalysis.Evidence
            {
                pitchTimes = times,
                pitchConfidence = times.Select(t => .8).ToArray(),
                pitch = times.Select(t => t >= 2.2 && t < 2.35 || t >= 4.2 && t < 4.35 ? 70
                    : t >= 2.35 && t < 2.95 || t >= 4.35 && t < 4.95 ? 72 : 67).ToArray(),
                voiceStarts = Array.Empty<double>()
            };
        }
        private static void Set(PcmInstrumentalEvidence.NoteEvent note, string name, double value)
            => note.features[PcmInstrumentalEvidence.Index(name)] = value;
        private static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
    }
}
