using System;
using System.Collections.Generic;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Conservative short moving supports from recurring, source-confirmed two-note cells.</summary>
    public static class PcmMelodicFollowAnalysis
    {
        private sealed class Cell
        {
            public PcmInstrumentalEvidence.NoteEvent first, second;
            public double end, interval;
        }

        // The caller verifies the evidence's PCM, model, schema and digest before this
        // musical policy runs. Legacy supports retain priority independently of it.
        public static MusicalCue[] Analyze(PcmInstrumentalEvidence.Evidence evidence, MusicalCue[] protectedSupports, double beatPeriod = 0)
        {
            if (evidence?.events == null || evidence.events.Length < 3) return Array.Empty<MusicalCue>();
            var notes = evidence.events.Where(Qualifies).OrderBy(n => n.time).ThenBy(n => n.midi).ToArray();
            if (notes.Length < 6) return Array.Empty<MusicalCue>();
            var pitches = notes.Select(n => (double)n.midi).OrderBy(p => p).ToArray();
            double upperRegister = Quantile(pitches, .8);
            var attacks = evidence.events.Where(n => Get(n, "sourceAttack") >= .5).Select(n => n.time).Distinct().OrderBy(t => t).ToArray();
            var cells = new List<Cell>();
            foreach (var first in notes)
            {
                if (first.midi < upperRegister) continue;
                PcmInstrumentalEvidence.NoteEvent second = null; double best = double.NegativeInfinity;
                foreach (var candidate in notes)
                {
                    double interval = candidate.time - first.time;
                    if (interval < .10 - 1e-7 || interval > .30 + 1e-7 || first.midi - candidate.midi < 2 || first.midi - candidate.midi > 5
                        || first.endTime < candidate.time - .10 - 1e-7) continue;
                    double end = End(candidate), duration = end - first.time;
                    if (duration < .25 - 1e-7 || duration > .7 + 1e-7) continue;
                    double strength = Get(candidate, "neuralOnset") * Get(candidate, "harmonicBody");
                    if (strength <= best) continue;
                    best = strength; second = candidate;
                }
                if (second == null) continue;
                var cell = new Cell { first = first, second = second, interval = second.time - first.time, end = End(second) };
                int at = Nearest(attacks, first.time), left = at, right = at;
                double connectedGap = Math.Max(.205, 1.2 * cell.interval);
                while (left > 0 && attacks[left] - attacks[left - 1] <= connectedGap + 1e-7) left--;
                while (right + 1 < attacks.Length && attacks[right + 1] - attacks[right] <= connectedGap + 1e-7) right++;
                // A pair embedded in an uninterrupted longer run is not truncated
                // into a short support. Its larger phrasing remains unresolved.
                if (attacks[right] - attacks[left] > 8 * cell.interval + 1e-7) continue;
                cells.Add(cell);
            }
            var accepted = new List<Cell>();
            foreach (var cell in cells)
            {
                var peers = cells.Where(c => c.first.midi == cell.first.midi && c.second.midi == cell.second.midi
                    && Math.Abs(c.interval - cell.interval) < .04 + 1e-7).ToArray();
                int index = Array.IndexOf(peers, cell); bool recurrent = false;
                for (int first = Math.Max(0, index - 2); first <= Math.Min(index, peers.Length - 3); first++)
                {
                    double before = peers[first + 1].first.time - peers[first].first.time;
                    double after = peers[first + 2].first.time - peers[first + 1].first.time;
                    if (before >= 2.7 * cell.interval - 1e-7 && before <= 5 * cell.interval + 1e-7 && Math.Abs(before - after) <= .08 + 1e-7)
                        recurrent = true;
                }
                if (!recurrent || (protectedSupports ?? Array.Empty<MusicalCue>()).Any(s => s != null && (s.kind == "Hold" || s.kind == "Follow" || s.kind == "BigJump")
                    && cell.first.time < s.endTime + .078 && cell.end > s.time - .078)) continue;
                accepted.Add(cell);
            }
            var result = new List<MusicalCue>();
            foreach (var cell in accepted.OrderBy(c => c.first.time))
            {
                if (result.Count > 0 && cell.first.time < result[result.Count - 1].endTime + .078) continue;
                double start = cell.first.time, end = cell.end;
                float direction = (result.Count & 1) == 0 ? -1 : 1;
                var points = new List<MusicalPoint> { new MusicalPoint { time = start, lane = 0 } };
                // A brief two-note glide needs entry and exit, not an extra
                // scoring checkpoint on every internal pitch articulation.
                if (end - start >= .5 && cell.second.time - start >= .0624 && end - cell.second.time >= .0624)
                    points.Add(new MusicalPoint { time = cell.second.time, lane = (float)(direction * .8 * (cell.second.time - start) / (end - start)) });
                points.Add(new MusicalPoint { time = end, lane = direction * .8f });
                result.Add(new MusicalCue { kind = "Follow", time = start, endTime = end, lane = 0, points = points.ToArray(),
                    confidence = .5 * (Math.Min(Get(cell.first, "neuralOnset"), Get(cell.second, "neuralOnset"))
                        + Math.Min(1, Math.Min(Get(cell.first, "harmonicBody"), Get(cell.second, "harmonicBody")))),
                    phraseId = "mixed-recurrent-cell-" + result.Count,
                    reason = "Recurring source-confirmed upper two-note response; measured harmonic release or actual subsequent articulation ends the short support. Lateral direction is independent gameplay choreography." });
            }
            FitNewPathCorridors(result, protectedSupports ?? Array.Empty<MusicalCue>());
            return result.ToArray();
        }

        private sealed class PathSupport
        {
            public MusicalCue cue;
            public bool isNew;
            public float entry, exit;
        }

        private static void FitNewPathCorridors(List<MusicalCue> proposals, MusicalCue[] protectedSupports)
        {
            // These paths become fixed in ChartRouteComposer. Fit only our new
            // endpoints before compilation; never bend a confirmed legacy path.
            while (proposals.Count > 0)
            {
                var supports = protectedSupports.Where(c => c != null && c.kind == "Follow")
                    .Concat(proposals).OrderBy(c => c.time).Select(c => new PathSupport
                    {
                        cue = c, isNew = proposals.Contains(c),
                        entry = c.points != null && c.points.Length > 0 ? c.points.First().lane : c.lane,
                        exit = c.points != null && c.points.Length > 0 ? c.points.Last().lane : c.lane
                    }).ToArray();
                if (TryFitCorridors(supports)) return;
                // A source gesture is optional. Reject an infeasible proposal
                // rather than changing a protected path or emitting a broken map.
                proposals.RemoveAt(proposals.Count - 1);
            }
        }

        private static bool TryFitCorridors(PathSupport[] supports)
        {
            int count = supports.Length * 2;
            var lower = new double[count]; var upper = new double[count]; var desired = new double[count];
            var stepLower = new double[count - 1]; var stepUpper = new double[count - 1];
            for (int i = 0; i < supports.Length; i++)
            {
                var support = supports[i]; int entry = 2 * i, exit = entry + 1;
                lower[entry] = lower[exit] = -1; upper[entry] = upper[exit] = 1;
                desired[entry] = support.entry; desired[exit] = support.exit;
                if (support.isNew)
                {
                    bool right = support.exit > support.entry;
                    // A tiny numeric margin keeps the serialized float span at
                    // or above the compiler's meaningful-movement threshold.
                    stepLower[entry] = right ? .20001 : -.8;
                    stepUpper[entry] = right ? .8 : -.20001;
                }
                else
                {
                    lower[entry] = upper[entry] = support.entry;
                    lower[exit] = upper[exit] = support.exit;
                    stepLower[entry] = -2; stepUpper[entry] = 2;
                }
                if (i + 1 == supports.Length) continue;
                double gap = supports[i + 1].cue.time - support.cue.endTime;
                if (gap < -.00001) return false;
                double shift = ChartRouteComposer.MaxLateralShift(Math.Max(0, gap));
                stepLower[exit] = -shift; stepUpper[exit] = shift;
                if (support.isNew && gap < .30 - .00001)
                    desired[exit] = Clamp(desired[exit], supports[i + 1].entry - shift, supports[i + 1].entry + shift);
            }
            // The constraint graph is a chain of entries and exits. Backward
            // reachable intervals followed by forward target projection solve
            // all neighboring endpoint corridors, including two legacy anchors.
            for (int i = count - 2; i >= 0; i--)
            {
                lower[i] = Math.Max(lower[i], lower[i + 1] - stepUpper[i]);
                upper[i] = Math.Min(upper[i], upper[i + 1] - stepLower[i]);
                if (lower[i] > upper[i] + 1e-7) return false;
            }
            var fitted = new double[count];
            for (int i = 0; i < count; i++)
            {
                double low = lower[i], high = upper[i];
                if (i > 0)
                {
                    low = Math.Max(low, fitted[i - 1] + stepLower[i - 1]);
                    high = Math.Min(high, fitted[i - 1] + stepUpper[i - 1]);
                }
                if (low > high + 1e-7) return false;
                fitted[i] = Clamp(desired[i], low, high);
            }
            for (int i = 0; i < supports.Length; i++)
            {
                var support = supports[i]; if (!support.isNew) continue;
                var cue = support.cue; float entry = (float)fitted[2 * i], exit = (float)fitted[2 * i + 1];
                cue.lane = entry;
                foreach (var point in cue.points)
                    point.lane = (float)(entry + (exit - entry) * (point.time - cue.time) / (cue.endTime - cue.time));
            }
            return true;
        }

        private static double Clamp(double value, double lower, double upper) => Math.Max(lower, Math.Min(upper, value));

        private static bool Qualifies(PcmInstrumentalEvidence.NoteEvent n)
            => n != null && Get(n, "neuralOnset") >= .6 && Get(n, "harmonicPartials") >= 1
                && Get(n, "harmonicBody") >= .45 && Get(n, "harmonicNovelty") >= .02 && Get(n, "sourceAttack") >= .5;
        private static double End(PcmInstrumentalEvidence.NoteEvent n) => Math.Min(n.endTime, n.time + Get(n, "nextGap"));
        private static double Get(PcmInstrumentalEvidence.NoteEvent n, string feature) => PcmInstrumentalEvidence.Get(n, feature);
        private static double Quantile(double[] sorted, double q)
        { double at = (sorted.Length - 1) * q; int lower = (int)at; return sorted[lower] + (sorted[Math.Min(lower + 1, sorted.Length - 1)] - sorted[lower]) * (at - lower); }
        private static int Nearest(double[] times, double time)
        {
            int index = Array.BinarySearch(times, time);
            if (index >= 0) return index;
            index = ~index;
            if (index >= times.Length) return times.Length - 1;
            return index > 0 && time - times[index - 1] < times[index] - time ? index - 1 : index;
        }
    }
}
