using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class PcmMelodicFollowValidation
    {
        [MenuItem("Dancing Cats/Validate measured short melodic follows")]
        public static void RunChecks()
        {
            var type = typeof(PcmMelodicFollowValidation).Assembly.GetType("DancingCats.Clone.Editor.PcmMelodicFollowAnalysis");
            Require(type != null, "Measured recurrent short-note cells need an independent Follow detector.");
            MusicalCue[] Analyze(PcmInstrumentalEvidence.Evidence e, MusicalCue[] protectedCues = null)
                => (MusicalCue[])type.GetMethod("Analyze").Invoke(null, new object[] { e, protectedCues ?? Array.Empty<MusicalCue>(), .5 });
            var source = Fixture(); string before = JsonUtility.ToJson(source);
            var cues = Analyze(source);
            Require(cues.Length == 5 && cues.All(c => c.kind == "Follow"), "Five source-confirmed recurring cells must create five short supports.");
            Require(cues.All(c => Math.Abs(c.endTime - c.time - .4) < 1e-7), "The measured note release and actual next attack determine the endpoint.");
            Require(cues.All(c => c.points.Length == 2), "Brief two-note glides must not invent an extra scoring checkpoint on their internal pitch change.");
            Require(JsonUtility.ToJson(source) == before, "Analysis must not modify source evidence.");
            Require(JsonUtility.ToJson(new CueSnapshot { cues = cues }) == JsonUtility.ToJson(new CueSnapshot { cues = Analyze(source) }), "The same evidence must produce the same short follows.");
            Require(Analyze(null).Length == 0, "Unavailable optional note evidence retains existing detection.");
            Require(Analyze(Fixture(1)).Length == 0 && Analyze(Fixture(2)).Length == 0,
                "One or two isolated upper pairs do not establish a recurring musical phrase.");
            var weak = Fixture(); foreach (var n in weak.events) Set(n, "neuralOnset", .1);
            Require(Analyze(weak).Length == 0, "Uncertain neural attacks must not imply a moving support.");
            var percussion = Fixture(); foreach (var n in percussion.events) Set(n, "harmonicPartials", 0);
            Require(Analyze(percussion).Length == 0, "Repeated bright percussion without a tonal note body must be rejected.");
            var silent = Fixture(); foreach (var n in silent.events) Set(n, "harmonicNovelty", 0);
            Require(Analyze(silent).Length == 0, "A stable or silent background must not invent fresh melodic entries.");
            var samePitch = Fixture(); foreach (var n in samePitch.events.Where(n => n.midi == 78)) n.midi = 80;
            Require(Analyze(samePitch).Length == 0, "A repeated pitch is not a descending two-note gesture.");
            var irregular = Fixture(); var clocks = new[] { 2.0, 2.6, 3.7, 5.3, 7.7 };
            for (int cell = 0; cell < 5; cell++) foreach (var n in irregular.events.Skip(cell * 3).Take(3))
            { double delta = clocks[cell] - (2 + cell * .8); n.time += delta; n.endTime += delta; }
            Require(Analyze(irregular).Length == 0, "Unrelated pairs must not be joined into a periodic phrase.");
            var continuous = Fixture();
            var fills = Enumerable.Range(0, 45).Select(i => Note(1.8 + i * .1, 1.89 + i * .1, 60, .1)).ToArray();
            continuous.events = continuous.events.Concat(fills).OrderBy(n => n.time).ThenBy(n => n.midi).ToArray();
            Require(Analyze(continuous).Length == 0, "A short cell inside a long uninterrupted scalar run must not truncate that larger phrase.");
            var protectedCues = new[] { new MusicalCue { kind = "Hold", time = 2, endTime = 2.4 } };
            var protectedResult = Analyze(source, protectedCues);
            Require(protectedResult.Length == 4 && protectedResult.All(c => c.time > 2.4),
                "Confirmed native support boundaries take priority without changing other recurrent responses.");
            var protectedFlight = new[] { new MusicalCue { kind = "BigJump", time = 2, endTime = 2.6 } };
            var afterFlight = Analyze(source, protectedFlight);
            Require(afterFlight.Length == 4 && afterFlight.All(c => c.time > 2.6),
                "A confirmed large launch reserves its entire airborne interval against new short follows.");
            var shifted = Fixture(); foreach (var n in shifted.events) { n.time += .1234567; n.endTime += .1234567; n.midi += 7; }
            var shiftedResult = Analyze(shifted);
            Require(shiftedResult.Length == cues.Length && shiftedResult.Zip(cues, (a, b) =>
                Math.Abs(a.time - b.time - .1234567) < 1e-7 && Math.Abs(a.endTime - b.endTime - .1234567) < 1e-7).All(x => x),
                "The recognizer must transpose and translate with its measured input, without song-specific times or pitches.");
            var jittered = Fixture(); for (int i = 0; i < jittered.events.Length; i++)
            { double delta = (i % 2 == 0 ? 1 : -1) * 1e-8; jittered.events[i].time += delta; jittered.events[i].endTime += delta; }
            Require(Analyze(jittered).Length == cues.Length, "Evidence-grid rounding must not change which musical cells are recognized.");
            var released = Fixture(); foreach (var n in released.events.Where(n => n.midi == 78)) n.endTime = n.time + .147;
            Require(Analyze(released).All(c => Math.Abs(c.endTime - c.time - .347) < 1e-7),
                "A real release before the following attack must stay off-grid rather than being extended to a fixed duration.");
            var tight = TightFixture();
            var tightResult = Analyze(tight);
            Require(tightResult.Length == 3, "Three recurring short responses must remain available in a tight phrase.");
            RequireReachable(tightResult);
            Require(tightResult.All(c => Math.Abs(c.points.First().lane) < 1e-6),
                "Unanchored short responses retain centered entries while their exits respect the next entry corridor.");
            var anchors = new[]
            {
                Follow(1.45, 1.85, .2f, .8f),
                Follow(3.35, 3.75, -.8f, -.2f)
            };
            string anchorBefore = JsonUtility.ToJson(new CueSnapshot { cues = anchors });
            var anchored = Analyze(tight, anchors);
            Require(anchored.Length == 3, "A tight phrase must fit between reachable fixed legacy Follow neighbors.");
            RequireReachable(anchors.Concat(anchored).OrderBy(c => c.time).ToArray());
            Require(JsonUtility.ToJson(new CueSnapshot { cues = anchors }) == anchorBefore,
                "Only new paths may adapt; protected legacy path points are immutable.");
            CheckCompiledRoute(tightResult);
            CheckCompiledRoute(anchors.Concat(anchored).OrderBy(c => c.time).ToArray());
            foreach (var cue in cues.Concat(Analyze(released)).Concat(tightResult).Concat(anchored))
            {
                Require(cue.points.Length >= 2 && cue.points.Length <= 6 && cue.points.First().time == cue.time && cue.points.Last().time == cue.endTime,
                    "A short native Follow must retain its actual endpoints and an adaptive small path.");
                Require(cue.points.Zip(cue.points.Skip(1), (a, b) => b.time - a.time >= .0624
                    && Math.Abs(b.lane - a.lane) <= 12.01 * (b.time - a.time)).All(x => x), "Short native path markers remain spatially reachable.");
                Require(cue.points.Max(p => p.lane) - cue.points.Min(p => p.lane) >= .2f,
                    "A corridor-constrained Follow must retain meaningful lateral movement.");
            }
            Debug.Log("PCM_MELODIC_FOLLOW PASS: recurring source-confirmed upper cells, measured release/articulation ends, weak/percussion/silence rejection, "
                + "unrepeated/irregular/continuous-run rejection, protected supports and airborne flights, immutable deterministic evidence, pitch/time invariance and short native paths.");
        }

        private static void RequireReachable(MusicalCue[] cues)
        {
            Require(cues.Zip(cues.Skip(1), (a, b) => Math.Abs(a.points.Last().lane - b.points.First().lane)
                <= ChartRouteComposer.MaxLateralShift(b.time - a.endTime) + .00001f).All(x => x),
                "Fixed Follow exits must reach the next fixed entry inside the available lateral corridor.");
        }
        private static void CheckCompiledRoute(MusicalCue[] cues)
        {
            // The lightweight PCM harness lacks the compiler's Unity/runtime types;
            // this integration assertion runs when these checks execute in the Editor.
            var compiler = typeof(PcmMelodicFollowValidation).Assembly.GetType("DancingCats.Clone.Editor.MusicalChartCompiler");
            if (compiler == null) return;
            var compile = compiler.GetMethod("Compile");
            var options = Activator.CreateInstance(compile.GetParameters()[1].ParameterType);
            var score = new MusicalScore { id = "TightMeasuredFollows", sampleRate = 1000, audioDuration = 6, bpm = 120, cues = cues };
            var map = compile.Invoke(null, new object[] { score, options, 1000, 6.0 });
            typeof(ChartRouteComposer).GetMethod("Apply").Invoke(null, new[] { map });
        }
        private static MusicalCue Follow(double start, double end, float entry, float exit)
            => new MusicalCue { kind = "Follow", time = start, endTime = end, lane = entry,
                points = new[] { new MusicalPoint { time = start, lane = entry }, new MusicalPoint { time = end, lane = exit } } };
        private static PcmInstrumentalEvidence.Evidence TightFixture()
            => new PcmInstrumentalEvidence.Evidence { events = Enumerable.Range(0, 3).SelectMany(i => new[]
                { Note(2 + i * .45, 2.2 + i * .45, 80, .8), Note(2.15 + i * .45, 2.3 + i * .45, 78, .8) }).ToArray() };

        private static PcmInstrumentalEvidence.Evidence Fixture(int count = 5)
        {
            var notes = Enumerable.Range(0, count).SelectMany(i => new[]
            {
                Note(2 + i * .8, 2.26 + i * .8, 80, .8),
                Note(2.2 + i * .8, 2.4 + i * .8, 78, .8),
                Note(2.4 + i * .8, 2.48 + i * .8, 60, .1)
            }).ToArray();
            return new PcmInstrumentalEvidence.Evidence { events = notes };
        }
        [Serializable] private sealed class CueSnapshot { public MusicalCue[] cues; }
        private static PcmInstrumentalEvidence.NoteEvent Note(double time, double end, int pitch, double neural)
        {
            var n = new PcmInstrumentalEvidence.NoteEvent { time = time, endTime = end, midi = pitch,
                features = new double[PcmInstrumentalEvidence.FeatureCount] };
            Set(n, "neuralOnset", neural); Set(n, "harmonicPartials", 2); Set(n, "harmonicBody", .8);
            Set(n, "harmonicNovelty", .1); Set(n, "sourceAttack", 1); Set(n, "nextGap", .2);
            return n;
        }
        private static void Set(PcmInstrumentalEvidence.NoteEvent n, string field, double value) => n.features[PcmInstrumentalEvidence.Index(field)] = value;
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("PCM_MELODIC_FOLLOW RED/FAIL: " + message); }
    }
}
