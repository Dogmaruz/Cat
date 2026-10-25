using System;
using System.Collections.Generic;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>
    /// Selects ordinary foreground articulations without changing measured
    /// support boundaries. Audio identity and feature digests are checked by
    /// the caller; these decisions use no song or reference-map identifiers.
    /// </summary>
    public static class PcmForegroundAnalysis
    {
        private const double NativeGap = .078, MatchWindow = .12;
        private struct Reserved { public double start, end; }

        /// <summary>Returns a new array, retaining existing cue objects unmodified.</summary>
        public static MusicalCue[] Apply(MusicalCue[] cues, PcmVocalAnalysis.Evidence evidence, double latestCueTime)
        {
            if (cues == null || !Finite(latestCueTime) || latestCueTime < 0)
                throw new ArgumentException("Foreground selection needs cues and a finite finish boundary.");
            if (cues.Any(cue => cue == null || !Finite(cue.time) || !Finite(cue.endTime) || cue.time < 0 || cue.endTime < cue.time))
                throw new ArgumentException("Foreground cues contain invalid intervals.", nameof(cues));
            var ordered = cues.OrderBy(cue => cue.time).ToArray();
            if (evidence == null || evidence.schemaVersion < 2 || ordered.Length == 0) return ordered;
            var attacks = evidence.vocalAttackTimes;
            var confidence = evidence.vocalAttackConfidence;
            if (attacks == null || confidence == null || attacks.Length != confidence.Length
                || attacks.Any(time => !Finite(time) || time < 0)
                || confidence.Any(value => !Finite(value) || value < 0 || value > 1.00001))
                throw new ArgumentException("Foreground attack vectors are inconsistent.", nameof(evidence));
            for (int i = 1; i < attacks.Length; i++)
                if (attacks[i] <= attacks[i - 1]) throw new ArgumentException("Foreground attack times must increase.", nameof(evidence));
            // A detector which found nothing reliable is not evidence that the
            // accompaniment should become unplayable or the chart become empty.
            if (attacks.Length == 0 || !confidence.Any(value => value >= .4)) return ordered;
            double audibleFloor = AudibilityFloor(evidence);
            var reserved = Reservations(ordered, latestCueTime, out var protectedLandings);
            var result = new List<MusicalCue>();
            foreach (var cue in ordered)
            {
                if (cue.kind != "Jump" || protectedLandings.Contains(cue)
                    || !Audible(evidence, cue.time, audibleFloor) || !ConfidentBody(evidence, cue.time)
                    || NearAttack(attacks, confidence, cue.time))
                    result.Add(cue);
            }

            // The mixed-source 160ms refractory cannot represent a genuine
            // 125ms repeated vocal run. Restore only a measured sequence of at
            // least three strong, similarly spaced articulations.
            var strong = Enumerable.Range(0, attacks.Length)
                .Where(i => confidence[i] >= .6 && Audible(evidence, attacks[i], audibleFloor)).Select(i => attacks[i]).ToArray();
            return InsertRapid(result, ordered, RapidRuns(strong), reserved, latestCueTime,
                "Measured rapid foreground articulation; strong neural onset consensus and a continuous short-interval run, without beat-grid filler.");
        }

        /// <summary>Restore source-confirmed harmonic runs only outside audible voice.</summary>
        public static MusicalCue[] ApplyInstrumentalRuns(MusicalCue[] cues, PcmVocalAnalysis.Evidence evidence,
            double[] confirmedAttacks, double latestCueTime)
        {
            if (cues == null || !Finite(latestCueTime) || latestCueTime < 0
                || cues.Any(cue => cue == null || !Finite(cue.time) || !Finite(cue.endTime) || cue.time < 0 || cue.endTime < cue.time))
                throw new ArgumentException("Instrumental insertion needs finite cue intervals and a finish boundary.");
            var ordered = cues.OrderBy(cue => cue.time).ToArray();
            var attacks = confirmedAttacks ?? Array.Empty<double>();
            if (attacks.Any(time => !Finite(time) || time < 0))
                throw new ArgumentException("Instrumental attacks must be finite measured times.", nameof(confirmedAttacks));
            for (int i = 1; i < attacks.Length; i++)
                if (attacks[i] <= attacks[i - 1]) throw new ArgumentException("Instrumental attack times must increase.", nameof(confirmedAttacks));
            if (attacks.Length < 3 || ordered.Length == 0) return ordered;
            if (evidence != null)
            {
                double floor = AudibilityFloor(evidence);
                attacks = attacks.Where(time => !Audible(evidence, time, floor)).ToArray();
            }
            var reserved = Reservations(ordered, latestCueTime, out _);
            return InsertRapid(ordered.ToList(), ordered, RapidRuns(attacks), reserved, latestCueTime,
                "Measured rapid instrumental articulation; repeated coherent harmonic attacks during a vocal rest, without beat-grid filler.");
        }

        private static List<Reserved> Reservations(MusicalCue[] ordered, double latestCueTime, out HashSet<MusicalCue> protectedLandings)
        {
            protectedLandings = new HashSet<MusicalCue>();
            var result = new List<Reserved>();
            for (int i = 0; i < ordered.Length; i++)
            {
                var cue = ordered[i];
                if (cue.kind == "Hold" || cue.kind == "Follow")
                    result.Add(new Reserved { start = cue.time - NativeGap, end = cue.endTime + NativeGap });
                if (cue.kind != "BigJump") continue;
                var landing = i + 1 < ordered.Length ? ordered[i + 1] : null;
                if (landing != null) protectedLandings.Add(landing);
                result.Add(new Reserved { start = cue.time - NativeGap, end = (landing?.time ?? latestCueTime) + NativeGap });
            }
            return result;
        }

        private static SortedSet<double> RapidRuns(double[] attacks)
        {
            var rapid = new SortedSet<double>();
            for (int i = 0; i + 2 < attacks.Length; i++)
            {
                double a = attacks[i], b = attacks[i + 1], c = attacks[i + 2];
                double first = b - a, second = c - b, mean = (first + second) * .5;
                if (first < .08 - 1e-8 || second < .08 - 1e-8 || first > .15 + 1e-8 || second > .15 + 1e-8
                    || Math.Abs(first - second) * .5 / mean > .20 + 1e-8) continue;
                rapid.Add(a); rapid.Add(b); rapid.Add(c);
            }
            return rapid;
        }

        private static MusicalCue[] InsertRapid(List<MusicalCue> result, MusicalCue[] ordered, SortedSet<double> rapid,
            List<Reserved> reserved, double latestCueTime, string reason)
        {
            foreach (double time in rapid)
            {
                if (time > latestCueTime || reserved.Any(range => time >= range.start && time <= range.end)
                    || result.Any(cue => time >= cue.time - NativeGap + 1e-8 && time <= cue.endTime + NativeGap - 1e-8)) continue;
                var prior = ordered.LastOrDefault(cue => cue.endTime < time);
                var next = ordered.FirstOrDefault(cue => cue.time > time);
                float from = prior != null ? ExitLane(prior) : next?.lane ?? 0;
                float lane = from;
                if (prior != null && next != null && next.time > prior.endTime)
                {
                    double phase = Math.Max(0, Math.Min(1, (time - prior.endTime) / (next.time - prior.endTime)));
                    lane = (float)(from + (next.lane - from) * phase);
                }
                result.Add(new MusicalCue { kind = "Jump", time = Round(time), endTime = Round(time), lane = lane,
                    confidence = .6, reason = reason });
            }
            return result.OrderBy(cue => cue.time).ToArray();
        }

        private static bool NearAttack(double[] times, double[] confidence, double time)
        {
            for (int i = LowerBound(times, time - MatchWindow); i < times.Length && times[i] <= time + MatchWindow; i++)
                if (confidence[i] >= .4) return true;
            return false;
        }

        private static bool Audible(PcmVocalAnalysis.Evidence evidence, double time, double floor)
        {
            int first = Math.Max(0, (int)Math.Floor((time - .06) / evidence.rmsStep));
            int last = Math.Min(evidence.rms.Length, (int)Math.Ceiling((time + .10) / evidence.rmsStep));
            for (int i = first; i < last; i++) if (evidence.rms[i] >= floor) return true;
            return false;
        }

        private static double AudibilityFloor(PcmVocalAnalysis.Evidence evidence)
        {
            if (!Finite(evidence.rmsStep) || evidence.rmsStep <= 0 || evidence.rms == null || evidence.rms.Length == 0
                || evidence.rms.Any(value => !Finite(value) || value < 0))
                throw new ArgumentException("A finite vocal envelope is required.", nameof(evidence));
            var sorted = (double[])evidence.rms.Clone(); Array.Sort(sorted);
            return Math.Max(.005, sorted[(int)Math.Floor((sorted.Length - 1) * .95)] * .01);
        }

        private static bool ConfidentBody(PcmVocalAnalysis.Evidence evidence, double time)
        {
            if (evidence.pitchTimes == null || evidence.pitchConfidence == null
                || evidence.pitchTimes.Length != evidence.pitchConfidence.Length) return false;
            int first = LowerBound(evidence.pitchTimes, time - .16), last = LowerBound(evidence.pitchTimes, time + .16);
            if (last <= first) return false;
            double sum = 0;
            for (int i = first; i < last; i++) sum += evidence.pitchConfidence[i];
            // Keep physical entries/releases when the foreground body estimate
            // is weak. Historical caches retain their measured pitch clock;
            // the independent attack group always uses native window times.
            return sum / (last - first) >= .45;
        }

        private static float ExitLane(MusicalCue cue)
        {
            if (cue.kind != "Follow" || cue.points == null || cue.points.Length == 0) return cue.lane;
            var terminal = cue.points.Where(point => point != null).OrderBy(point => point.time).LastOrDefault();
            return terminal?.lane ?? cue.lane;
        }
        private static int LowerBound(double[] values, double target)
        { int first = 0, last = values.Length; while (first < last) { int mid = first + (last - first) / 2; if (values[mid] < target) first = mid + 1; else last = mid; } return first; }
        private static double Round(double time) => Math.Round(time, 6, MidpointRounding.AwayFromZero);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
