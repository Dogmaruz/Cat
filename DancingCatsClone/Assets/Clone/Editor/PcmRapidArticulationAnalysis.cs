using System;
using System.Collections.Generic;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Confirms short instrumental runs from harmonic attacks rather than shortening global peak suppression.</summary>
    public static class PcmRapidArticulationAnalysis
    {
        private static readonly double[] HarmonicWeights = { 1, .9, .8, .65, .55, .45 };
        public static double[] Analyze(double[] candidates, double[] times, double[][] magnitude,
            double[] pitch, double[] confidence, int[] partials, double binWidth)
        {
            if (candidates == null || times == null || magnitude == null || pitch == null || confidence == null || partials == null
                || times.Length != magnitude.Length || times.Length != pitch.Length || times.Length != confidence.Length
                || times.Length != partials.Length || double.IsNaN(binWidth) || double.IsInfinity(binWidth) || binWidth <= 0)
                throw new ArgumentException("Rapid articulation needs aligned spectral and harmonic measurements.");
            if (times.Length < 8 || candidates.Length < 3) return Array.Empty<double>();
            var measured = new List<double>();
            foreach (double attack in candidates)
            {
                int first = LowerBound(times, attack + .025), last = LowerBound(times, attack + .085);
                var stable = Enumerable.Range(first, last - first).Where(i => confidence[i] >= 7.2 && partials[i] >= 3).ToArray();
                int beforeFirst = LowerBound(times, attack - .060), beforeLast = LowerBound(times, attack + .005);
                int afterFirst = LowerBound(times, attack + .015), afterLast = LowerBound(times, attack + .080);
                if (beforeFirst >= beforeLast || afterFirst >= afterLast) continue;
                bool confirmed = false;
                if (stable.Length >= 3)
                {
                    var notes = stable.Select(i => pitch[i]).OrderBy(value => value).ToArray();
                    double note = notes[notes.Length / 2];
                    if (notes.Count(value => Math.Abs(value - note) <= 1) >= stable.Length * .7)
                        confirmed = ConfirmsAttack(magnitude, note, binWidth, beforeFirst, beforeLast, afterFirst, afterLast);
                }
                // A continuous bass can own the strongest pitch while a quieter
                // melody rearticulates. Search alternative harmonic ridges only
                // when they themselves rise and persist; the bass does not.
                for (int note = 48; !confirmed && note <= 84; note++)
                {
                    int harmonicFrames = 0;
                    for (int i = first; i < last; i++) if (HasHarmonicBody(magnitude[i], note, binWidth)) harmonicFrames++;
                    if (harmonicFrames >= 3)
                        confirmed = ConfirmsAttack(magnitude, note, binWidth, beforeFirst, beforeLast, afterFirst, afterLast);
                }
                if (confirmed) measured.Add(attack);
            }
            var result = new List<double>(); int start = 0;
            // A close pair can be an accidental transient. Require at least three
            // independently pitched attacks, retaining their measured timestamps.
            for (int i = 1; i <= measured.Count; i++)
            {
                if (i < measured.Count)
                {
                    double interval = measured[i] - measured[i - 1];
                    if (interval >= .078 - .000001 && interval <= .155) continue;
                }
                if (i - start >= 3) result.AddRange(measured.Skip(start).Take(i - start));
                start = i;
            }
            return result.ToArray();
        }

        private static bool ConfirmsAttack(double[][] magnitude, double note, double binWidth,
            int beforeFirst, int beforeLast, int afterFirst, int afterLast)
        {
            int trough = beforeFirst, crest = afterFirst;
            double low = double.PositiveInfinity, high = 0;
            for (int i = beforeFirst; i < beforeLast; i++)
            {
                double energy = HarmonicEnergy(magnitude[i], note, binWidth);
                if (energy < low) { low = energy; trough = i; }
            }
            for (int i = afterFirst; i < afterLast; i++)
            {
                double energy = HarmonicEnergy(magnitude[i], note, binWidth);
                if (energy > high) { high = energy; crest = i; }
            }
            if (high < .01) return false;
            int changedPartials = 0;
            double novelHarmonicEnergy = 0, novelBandEnergy = 0;
            int lowBin = Math.Max(1, (int)Math.Ceiling(150 / binWidth));
            int highBin = Math.Min(magnitude[crest].Length - 1, (int)Math.Floor(5000 / binWidth));
            for (int bin = lowBin; bin <= highBin; bin++)
            {
                double rise = Math.Max(0, magnitude[crest][bin] - magnitude[trough][bin]);
                novelBandEnergy += rise * rise;
            }
            for (int harmonic = 1; harmonic <= 6; harmonic++)
            {
                double before = HarmonicMagnitude(magnitude[trough], note, harmonic, binWidth);
                double after = HarmonicMagnitude(magnitude[crest], note, harmonic, binWidth);
                int center = (int)Math.Round(440 * Math.Pow(2, (note - 69) / 12) * harmonic / binWidth);
                double deltaPeak = 0, deltaMean = 0;
                int neighborhoodFirst = Math.Max(0, center - 5), neighborhoodLast = Math.Min(magnitude[crest].Length - 1, center + 5);
                for (int bin = neighborhoodFirst; bin <= neighborhoodLast; bin++)
                {
                    double rise = Math.Max(0, magnitude[crest][bin] - magnitude[trough][bin]);
                    deltaMean += rise;
                    if (Math.Abs(bin - center) <= 1) deltaPeak = Math.Max(deltaPeak, rise);
                }
                double deltaProminence = deltaPeak / (deltaMean / Math.Max(1, neighborhoodLast - neighborhoodFirst + 1) + .03);
                if (after > .03 && after > before * 1.4 && deltaProminence >= 1.5) changedPartials++;
                for (int bin = Math.Max(lowBin, center - 1); bin <= Math.Min(highBin, center + 1); bin++)
                {
                    double rise = Math.Max(0, magnitude[crest][bin] - magnitude[trough][bin]);
                    novelHarmonicEnergy += rise * rise;
                }
            }
            // A melody note can share its low partials with a louder bass. Three
            // independently rising partials still establish a real new note,
            // even when that bass masks the aggregate energy ratio.
            return changedPartials >= 2 && (high >= Math.Max(1e-8, low) * 1.65 || changedPartials >= 3)
                && novelBandEnergy > 1e-8 && novelHarmonicEnergy / novelBandEnergy >= .20;
        }

        private static bool HasHarmonicBody(double[] spectrum, double note, double binWidth)
        {
            int covered = 0; double confidence = 0;
            for (int harmonic = 1; harmonic <= 6; harmonic++)
            {
                int center = (int)Math.Round(440 * Math.Pow(2, (note - 69) / 12) * harmonic / binWidth);
                if (center < 1 || center + 1 >= spectrum.Length) break;
                double mean = 0; int first = Math.Max(0, center - 5), last = Math.Min(spectrum.Length - 1, center + 5);
                for (int bin = first; bin <= last; bin++) mean += spectrum[bin];
                double value = Math.Min(8, HarmonicMagnitude(spectrum, note, harmonic, binWidth) / (mean / (last - first + 1) + .03));
                if (value > 1.35) covered++;
                confidence += value * HarmonicWeights[harmonic - 1];
            }
            return covered >= 3 && confidence >= 7.2;
        }

        private static double HarmonicEnergy(double[] spectrum, double midi, double binWidth)
        {
            double result = 0;
            for (int harmonic = 1; harmonic <= 6; harmonic++)
            {
                double value = HarmonicMagnitude(spectrum, midi, harmonic, binWidth);
                result += value * value / harmonic;
            }
            return result;
        }

        private static double HarmonicMagnitude(double[] spectrum, double midi, int harmonic, double binWidth)
        {
            double frequency = 440 * Math.Pow(2, (midi - 69) / 12) * harmonic;
            int bin = (int)Math.Round(frequency / binWidth);
            if (bin < 1 || bin + 1 >= spectrum.Length) return 0;
            return Math.Max(spectrum[bin], Math.Max(spectrum[bin - 1], spectrum[bin + 1]));
        }

        private static int LowerBound(double[] values, double target)
        { int left = 0, right = values.Length; while (left < right) { int middle = left + (right - left) / 2; if (values[middle] < target) left = middle + 1; else right = middle; } return left; }
    }
}
