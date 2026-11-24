using System;
using System.IO;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Opt-in recovery of independently measured physical attacks in otherwise unsupported ordinary gaps.</summary>
    public static class PcmMeasuredGapRecovery
    {
        public const string Version = "measured-gap-recovery-v1";
        private const double MinimumGap = .18;

        /// <summary>
        /// The generator verifies evidence against its actual source PCM before calling.
        /// Existing cue objects, support bodies, paths and reserved flights remain unchanged.
        /// Only measured note times can be inserted; there are no song identities or grid inputs.
        /// </summary>
        public static MusicalCue[] Apply(MusicalCue[] cues, PcmInstrumentalEvidence.Evidence evidence, double maximumGap = 1.95)
        {
            if (cues == null || !Finite(maximumGap) || maximumGap < MinimumGap * 2)
                throw new ArgumentException("Measured gap recovery requires cues and a finite airborne envelope.");
            if (cues.Any(c => c == null || !Finite(c.time) || !Finite(c.endTime) || c.time < 0
                || c.endTime < c.time || float.IsNaN(c.lane) || float.IsInfinity(c.lane)))
                throw new ArgumentException("Measured gap recovery requires finite, ordered cue intervals.", nameof(cues));
            var result = cues.OrderBy(c => c.time).ToList();
            for (int i = 1; i < result.Count; i++)
                if (result[i].time <= result[i - 1].time || result[i].time < result[i - 1].endTime - .000001)
                    throw new ArgumentException("Measured gap recovery cannot repair overlapping input cues.", nameof(cues));
            if (evidence == null) return result.ToArray();
            if (evidence.events == null) throw new InvalidDataException("Measured note events are missing.");
            foreach (var note in evidence.events)
            {
                if (note == null || !Finite(note.time) || !Finite(note.endTime) || note.time < 0 || note.endTime < note.time
                    || note.midi < 21 || note.midi > 108 || note.features == null || note.features.Length != PcmInstrumentalEvidence.FeatureCount
                    || note.features.Any(value => !Finite(value) || Math.Abs(value) > 100000))
                    throw new InvalidDataException("Measured gap recovery received invalid note times, pitch or features.");
            }
            var measuredAttacks = evidence.events.Where(note =>
                    PcmInstrumentalEvidence.Get(note, "sourceAttack") >= .5
                    && PcmInstrumentalEvidence.Get(note, "neuralOnset") >= .4
                    && PcmInstrumentalEvidence.Get(note, "harmonicPartials") >= 3
                    && PcmInstrumentalEvidence.Get(note, "harmonicNovelty") > 0)
                .Select(note => note.time).Distinct().OrderBy(time => time).ToArray();
            // No synthetic finish boundary is supplied: the final fade after the
            // last cue stays untouched. Each insertion splits an existing gap.
            for (int i = 0; i + 1 < result.Count; i++)
            {
                var prior = result[i];
                var next = result[i + 1];
                if (prior.kind == "BigJump" || next.kind == "FinishJump"
                    || next.time - prior.endTime <= maximumGap + .000001) continue;
                double from = prior.endTime + MinimumGap, to = next.time - MinimumGap;
                double midpoint = (prior.endTime + next.time) * .5;
                var eligible = measuredAttacks.Where(time => time >= from && time <= to)
                    .OrderBy(time => Math.Abs(time - midpoint)).ThenBy(time => time).ToArray();
                if (eligible.Length == 0) continue;
                double attack = eligible[0];
                result.Insert(i + 1, new MusicalCue
                {
                    kind = "Jump", time = attack, endTime = attack, lane = ExitLane(prior), confidence = .6,
                    reason = "Independent source-confirmed tonal attack recovered only inside an oversized ordinary airborne gap; exact measured onset, no periodic filler."
                });
                // Revisit both sub-gaps; every new cue consumes a distinct,
                // separated measured attack, so the process is bounded.
                i--;
            }
            return result.ToArray();
        }

        private static float ExitLane(MusicalCue cue)
        {
            if (cue.kind != "Follow") return cue.lane;
            if (cue.points == null || cue.points.Length == 0 || cue.points.Any(p => p == null
                || !Finite(p.time) || float.IsNaN(p.lane) || float.IsInfinity(p.lane)))
                throw new ArgumentException("A Follow must provide its finite measured exit path.", nameof(cue));
            return cue.points.OrderBy(p => p.time).Last().lane;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
