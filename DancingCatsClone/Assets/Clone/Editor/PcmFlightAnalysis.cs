using System;
using System.Collections.Generic;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>
    /// Optional musical policy: an articulated, stable upper-register vocal body may
    /// launch a large airborne gesture until a measured post-release attack. This is
    /// a conservative authoring decision, not a reconstruction of a designer's tile labels.
    /// </summary>
    public static class PcmFlightAnalysis
    {
        public sealed class Flight
        {
            public double time, landingTime, vocalStart, releaseTime, confidence;
            public string reason;
        }

        /// <summary>Returns proposals only. The caller owns cue replacement and saving.</summary>
        public static Flight[] Analyze(PcmVocalAnalysis.Evidence evidence, double[] physicalAttacks,
            double beatPeriod, MusicalCue[] confirmedSupports)
        {
            if (!Finite(beatPeriod)) throw new ArgumentException("The flight pulse must be finite.", nameof(beatPeriod));
            if (evidence == null || beatPeriod <= 0) return Array.Empty<Flight>();
            // Also protects this public entry point from malformed, unsealed arrays.
            PcmVocalAnalysis.Validate(evidence, evidence.sourcePcmSha256, evidence.sampleRate, evidence.channels, evidence.sampleFrames);
            double duration = evidence.sampleFrames / (double)evidence.sampleRate;
            var attacks = physicalAttacks ?? Array.Empty<double>();
            if (attacks.Any(t => !Finite(t) || t < 0 || t > duration))
                throw new ArgumentException("Flight attacks must be finite times inside the recording.", nameof(physicalAttacks));
            attacks = attacks.Distinct().OrderBy(t => t).ToArray();
            var supports = confirmedSupports ?? Array.Empty<MusicalCue>();
            if (supports.Any(c => c == null || !Finite(c.time) || !Finite(c.endTime) || c.endTime < c.time))
                throw new ArgumentException("Flight support intervals are malformed.", nameof(confirmedSupports));
            var voicedPitches = evidence.pitch.Where((pitch, i) => evidence.pitchConfidence[i] >= .45).OrderBy(pitch => pitch).ToArray();
            if (voicedPitches.Length == 0 || attacks.Length == 0) return Array.Empty<Flight>();
            var frameSteps = evidence.pitchTimes.Skip(1).Select((time, i) => time - evidence.pitchTimes[i]).OrderBy(step => step).ToArray();
            double pitchStep = frameSteps[frameSteps.Length / 2];
            var sustainedPitches = voicedPitches.GroupBy(pitch => pitch).Where(group => group.Count() * pitchStep >= .20)
                .Select(group => group.Key).OrderBy(pitch => pitch).ToArray();
            // Brief transcription spikes do not establish a recording's register.
            if (sustainedPitches.Length < 2 || sustainedPitches[sustainedPitches.Length - 1] - sustainedPitches[0] < 2)
                return Array.Empty<Flight>();
            // Recording-relative upper range, invariant under a whole-song transposition.
            int upperRegister = voicedPitches[(int)Math.Floor((voicedPitches.Length - 1) * .70)];
            var starts = new List<double>();
            foreach (double start in evidence.voiceStarts)
                if (starts.Count == 0 || start - starts[starts.Count - 1] > .08) starts.Add(start);
            var result = new List<Flight>();
            foreach (double start in starts)
            {
                int first = LowerBound(evidence.pitchTimes, start + .14 * beatPeriod);
                int last = LowerBound(evidence.pitchTimes, start + .92 * beatPeriod);
                if (first >= last || last - first < 10) continue;
                int mode = Enumerable.Range(first, last - first).GroupBy(i => evidence.pitch[i])
                    .OrderByDescending(group => group.Count()).ThenBy(group => group.Key).First().Key;
                double stable = 0, confidence = 0;
                for (int i = first; i < last; i++)
                { if (Math.Abs(evidence.pitch[i] - mode) <= 1) stable++; confidence += evidence.pitchConfidence[i]; }
                stable /= last - first; confidence /= last - first;
                if (mode < upperRegister || stable < .90 || confidence < .40) continue;
                int peakFirst = (int)Math.Ceiling(start / evidence.rmsStep);
                int peakLast = Math.Min(evidence.rms.Length, (int)Math.Ceiling((start + 1.06 * beatPeriod) / evidence.rmsStep));
                double peak = .005;
                for (int i = peakFirst; i < peakLast; i++) peak = Math.Max(peak, evidence.rms[i]);
                // A confident pitch trace over actual vocal silence cannot create a flight.
                if (BelowFor(evidence, start + .03, start + .64 * beatPeriod, peak * .10, .06)) continue;
                double release = FindRelease(evidence, start + .68 * beatPeriod, start + 2.12 * beatPeriod, peak * .30);
                if (!Finite(release)) continue;
                var landings = attacks.Where(t => t >= release && t > start + beatPeriod && t < start + 2.22 * beatPeriod).ToArray();
                if (landings.Length == 0) continue;
                double landing = landings[0];
                var near = attacks.Where(t => Math.Abs(t - start) <= .08).OrderBy(t => Math.Abs(t - start)).ThenBy(t => t).ToArray();
                double launch = near.Length == 0 ? start : near[0];
                // A measured next voice is foreground evidence, so it must never be
                // discarded as an accompaniment transient inside the proposed flight.
                if (starts.Any(t => t > start + .10 && t < Math.Min(release, landing - .12))) continue;
                if (supports.Any(c => launch < c.endTime + .078 && landing > c.time + .001)) continue;
                if (result.Any(f => launch < f.landingTime - .078 && landing > f.time + .078)) continue;
                result.Add(new Flight
                {
                    time = launch, landingTime = landing, vocalStart = start, releaseTime = release,
                    confidence = Math.Min(stable, confidence),
                    reason = "Upper-register flight policy: stable articulated foreground body, measured vocal release and post-release attack; accompaniment peaks remain airborne."
                });
            }
            return result.ToArray();
        }

        private static double FindRelease(PcmVocalAnalysis.Evidence e, double from, double to, double threshold)
        {
            int count = Math.Max(1, (int)Math.Ceiling(.06 / e.rmsStep));
            int first = Math.Max(0, (int)Math.Ceiling(from / e.rmsStep));
            int last = Math.Min(e.rms.Length - count, (int)Math.Floor(to / e.rmsStep));
            for (int i = first; i <= last; i++)
            {
                bool below = true;
                for (int j = 0; j < count; j++) if (e.rms[i + j] >= threshold) { below = false; break; }
                if (below) return i * e.rmsStep;
            }
            return double.NaN;
        }

        private static bool BelowFor(PcmVocalAnalysis.Evidence e, double from, double to, double threshold, double seconds)
        {
            int run = 0, required = Math.Max(1, (int)Math.Ceiling(seconds / e.rmsStep));
            int first = Math.Max(0, (int)Math.Ceiling(from / e.rmsStep)), last = Math.Min(e.rms.Length, (int)Math.Ceiling(to / e.rmsStep));
            for (int i = first; i < last; i++)
            { run = e.rms[i] < threshold ? run + 1 : 0; if (run >= required) return true; }
            return false;
        }

        private static int LowerBound(double[] values, double target)
        {
            int first = 0, last = values.Length;
            while (first < last) { int mid = first + (last - first) / 2; if (values[mid] < target) first = mid + 1; else last = mid; }
            return first;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
