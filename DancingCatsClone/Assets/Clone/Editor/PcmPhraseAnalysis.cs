using System;
using System.Collections.Generic;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Finds repeated, spectrally continuous legato gestures from recording features only.</summary>
    public static class PcmPhraseAnalysis
    {
        private sealed class Candidate
        {
            public double start, end, continuity;
            public double[] intervals, spectrum;
        }

        public static MusicalCue[] Analyze(double[] attacks, double[] times, double[][] magnitude, double[] novelty, double binWidth)
        {
            if (attacks == null || attacks.Length < 7 || times == null || times.Length < 8
                || magnitude == null || magnitude.Length != times.Length || novelty == null || novelty.Length != times.Length)
                return Array.Empty<MusicalCue>();
            int firstBin = Math.Max(0, (int)Math.Floor(200 / binWidth) + 1);
            int endBin = Math.Min(magnitude[0].Length, (int)Math.Ceiling(3500 / binWidth));
            int bins = endBin - firstBin;
            if (bins < 4) return Array.Empty<MusicalCue>();
            var candidates = new List<Candidate>();
            for (int index = 0; index + 5 < attacks.Length; index++)
            {
                double start = attacks[index], end = attacks[index + 4], duration = end - start;
                if (duration < 1.05 || duration > 1.6 || attacks[index + 5] - end < .45) continue;
                var intervals = new double[4]; double mean = 0;
                for (int offset = 0; offset < intervals.Length; offset++)
                { intervals[offset] = attacks[index + offset + 1] - attacks[index + offset]; mean += intervals[offset]; }
                mean /= intervals.Length;
                if (intervals.Min() <= .13 || intervals.Max() >= .6) continue;
                double variation = Math.Sqrt(intervals.Sum(value => (value - mean) * (value - mean)) / intervals.Length) / mean;
                if (variation <= .18 || variation >= .7) continue;
                int begin = LowerBound(times, start + .08), limit = UpperBound(times, end - .04);
                if (limit - begin < 8) continue;
                var energy = new double[limit - begin]; var spectrum = new double[bins];
                for (int frame = begin; frame < limit; frame++)
                {
                    double square = 0;
                    for (int bin = firstBin; bin < endBin; bin++)
                    {
                        double value = magnitude[frame][bin]; square += value * value;
                        spectrum[bin - firstBin] += value;
                    }
                    energy[frame - begin] = Math.Sqrt(square / bins);
                }
                Array.Sort(energy);
                double continuity = Quantile(energy, .1) / (Quantile(energy, .5) + 1e-9);
                if (continuity < .65) continue;
                double logMean = 0, arithmeticMean = 0;
                for (int bin = 0; bin < bins; bin++)
                { double value = spectrum[bin] / (limit - begin); logMean += Math.Log(value + 1e-8); arithmeticMean += value; }
                // Broadband noise can be continuous and repeated too; require tonal concentration.
                if (Math.Exp(logMean / bins) / (arithmeticMean / bins + 1e-9) >= .55) continue;
                double norm = 0;
                for (int bin = 0; bin < bins; bin++)
                { spectrum[bin] = Math.Log(1 + spectrum[bin] / (limit - begin) * 12); norm += spectrum[bin] * spectrum[bin]; }
                norm = Math.Sqrt(norm) + 1e-9;
                for (int bin = 0; bin < bins; bin++) spectrum[bin] /= norm;
                candidates.Add(new Candidate { start = start, end = end, intervals = intervals, spectrum = spectrum, continuity = continuity });
            }
            var cues = new List<MusicalCue>();
            foreach (var candidate in candidates)
            {
                double bestCosine = 0, partnerContinuity = 0;
                foreach (var other in candidates)
                {
                    if (candidate == other || Math.Abs(candidate.start - other.start) < 3) continue;
                    double error = 0, cosine = 0;
                    for (int index = 0; index < candidate.intervals.Length; index++)
                    { double delta = candidate.intervals[index] - other.intervals[index]; error += delta * delta; }
                    if (Math.Sqrt(error / candidate.intervals.Length) >= .055) continue;
                    for (int bin = 0; bin < bins; bin++) cosine += candidate.spectrum[bin] * other.spectrum[bin];
                    if (cosine <= .99 || cosine <= bestCosine) continue;
                    bestCosine = cosine; partnerContinuity = other.continuity;
                }
                if (bestCosine == 0) continue;
                double start = RefineStart(candidate.start, times, novelty), end = candidate.end;
                if (cues.Count > 0 && start < cues[cues.Count - 1].endTime + .078) continue;
                // Spatial movement is an independent gameplay style: settle the entry,
                // then make one broad gesture. Repeated phrases alternate a scoop and
                // an opposite sweep; no pitch estimate or reference path is claimed.
                var points = new MusicalPoint[6];
                start = Round(start); end = Round(end);
                for (int point = 0; point < points.Length; point++)
                {
                    double phase = point / (double)(points.Length - 1);
                    points[point] = new MusicalPoint { time = Round(start + (end - start) * phase),
                        lane = ChoreographyLane(phase, (cues.Count & 1) != 0) };
                }
                cues.Add(new MusicalCue
                {
                    kind = "Follow", time = start, endTime = end, lane = points[0].lane, points = points,
                    confidence = .5 * (bestCosine + Math.Min(candidate.continuity, partnerContinuity)),
                    phraseId = "recurrent-legato-" + cues.Count,
                    reason = "Repeated spectrally continuous articulation phrase; measured terminal accent. Six spatial samples use a settled scoop/sweep gameplay style, not inferred pitch."
                });
            }
            return cues.ToArray();
        }

        private static float ChoreographyLane(double phase, bool sweep)
        {
            const double settle = .25, turn = .6, reach = .8;
            if (sweep)
                return (float)(phase <= settle ? reach : reach - 2 * reach * (phase - settle) / (1 - settle));
            if (phase <= settle) return 0;
            return (float)(phase <= turn ? -reach * (phase - settle) / (turn - settle)
                : -reach + 2 * reach * (phase - turn) / (1 - turn));
        }

        private static double RefineStart(double attack, double[] times, double[] novelty)
        {
            int peak = LowerBound(times, attack);
            if (peak > 0 && (peak == times.Length || attack - times[peak - 1] < times[peak] - attack)) peak--;
            peak = Math.Min(peak, times.Length - 1);
            int first = LowerBound(times, attack - .14), last = Math.Min(peak, LowerBound(times, attack - .04));
            int valley = first;
            for (int frame = first + 1; frame <= last; frame++) if (novelty[frame] < novelty[valley]) valley = frame;
            double threshold = novelty[valley] + .25 * (novelty[peak] - novelty[valley]);
            for (int frame = valley; frame <= peak; frame++) if (novelty[frame] >= threshold) return times[frame];
            return attack;
        }
        private static int LowerBound(double[] values, double target)
        { int lo = 0, hi = values.Length; while (lo < hi) { int mid = (lo + hi) / 2; if (values[mid] < target) lo = mid + 1; else hi = mid; } return lo; }
        private static int UpperBound(double[] values, double target)
        { int lo = 0, hi = values.Length; while (lo < hi) { int mid = (lo + hi) / 2; if (values[mid] <= target) lo = mid + 1; else hi = mid; } return lo; }
        private static double Quantile(double[] sorted, double fraction)
        { double position = (sorted.Length - 1) * fraction; int lo = (int)position, hi = Math.Min(sorted.Length - 1, lo + 1); return sorted[lo] + (sorted[hi] - sorted[lo]) * (position - lo); }
        private static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
