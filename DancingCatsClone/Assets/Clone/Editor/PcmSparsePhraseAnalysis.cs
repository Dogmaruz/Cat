using System;
using System.Collections.Generic;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>
    /// Recovers measured foreground entries and harmonic bodies only where the
    /// current chart exceeds its airborne authoring envelope. Silence stays empty.
    /// </summary>
    public static class PcmSparsePhraseAnalysis
    {
        private const double MinimumGap = .18, MinimumBody = .45, ReleaseTail = .03;

        /// <summary>
        /// Returns a new cue array; existing support objects are never changed.
        /// The caller verifies source identity/integrity before supplying vocal
        /// evidence. Stable notes must come from the multi-partial PCM analyzer.
        /// No reference level, song identity or beat-grid coordinates are inputs.
        /// </summary>
        public static MusicalCue[] Complete(MusicalCue[] cues, PcmVocalAnalysis.Evidence voice,
            MusicalNote[] stableHarmonicNotes, double finishTime, double maximumGap = 1.95)
        {
            if (cues == null || !Finite(finishTime) || finishTime <= 0 || !Finite(maximumGap) || maximumGap < MinimumGap * 2)
                throw new ArgumentException("Sparse completion requires cues, a finite finish and a playable gap envelope.");
            if (cues.Any(c => c == null || !Finite(c.time) || !Finite(c.endTime) || c.time < 0 || c.endTime < c.time || c.endTime > finishTime))
                throw new ArgumentException("Sparse completion cues must have finite, ordered intervals inside the finish.", nameof(cues));
            var result = cues.OrderBy(c => c.time).ToList();
            for (int i = 1; i < result.Count; i++)
                if (result[i].time <= result[i - 1].time || result[i].time < result[i - 1].endTime - .000001)
                    throw new ArgumentException("Sparse completion cannot repair overlapping input cues.", nameof(cues));
            var notes = stableHarmonicNotes ?? Array.Empty<MusicalNote>();
            if (notes.Any(n => n == null || !Finite(n.start) || !Finite(n.end) || !Finite(n.confidence)
                || n.start < 0 || n.end < n.start || n.confidence < 0 || n.confidence > 1))
                throw new ArgumentException("Stable harmonic notes contain invalid measurements.", nameof(stableHarmonicNotes));
            ValidateVoice(voice);
            // Each pass adds a distinct measured onset or upgrades one Jump to a
            // Hold. An unresolved silent gap remains for the caller's normal
            // geometry validator to reject, rather than receiving timed filler.
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < result.Count; i++)
                {
                    var prior = result[i];
                    double next = i + 1 < result.Count ? result[i + 1].time : finishTime;
                    if (next - prior.endTime <= maximumGap + .000001) continue;
                    float exitLane = prior.kind == "Follow" && prior.points != null && prior.points.Length > 0
                        ? prior.points[prior.points.Length - 1].lane : prior.lane;
                    double entry = AudibleEntry(voice, prior.endTime + MinimumGap, next - MinimumGap);
                    if (Finite(entry))
                    {
                        result.Insert(i + 1, new MusicalCue { kind = "Jump", time = Round(entry), endTime = Round(entry), lane = exitLane,
                            confidence = .6, reason = "Measured foreground envelope entry inside a sparse passage; continuing vocal RMS and pitch confidence, without beat-grid filler." });
                        changed = true;
                        break;
                    }
                    MusicalCue proposed = null;
                    bool replacePrior = false;
                    foreach (var note in notes)
                    {
                        if (note.confidence < .60 || note.end - note.start < MinimumBody) continue;
                        bool upgrade = prior.kind == "Jump" && note.start <= prior.time + .08 && note.end - prior.time >= MinimumBody;
                        double start = upgrade ? prior.time : note.start;
                        double end = Math.Min(note.end - ReleaseTail, next - MinimumGap);
                        if (end - start < MinimumBody || (!upgrade && start < prior.endTime + MinimumGap)
                            || start >= next - MinimumGap) continue;
                        if (proposed != null && (start > proposed.time || (Math.Abs(start - proposed.time) < .000001 && note.confidence <= proposed.confidence))) continue;
                        proposed = new MusicalCue { kind = "Hold", time = Round(start), endTime = Round(end), lane = exitLane,
                            confidence = note.confidence, reason = "Measured stable multi-partial harmonic body inside a sparse passage; its release and the next existing attack bound support." };
                        replacePrior = upgrade;
                    }
                    if (proposed == null) continue;
                    if (replacePrior) result[i] = proposed;
                    else result.Insert(i + 1, proposed);
                    changed = true;
                    break;
                }
            } while (changed);
            return result.ToArray();
        }

        private static double AudibleEntry(PcmVocalAnalysis.Evidence voice, double from, double to)
        {
            if (voice == null) return double.NaN;
            foreach (double entry in voice.voiceStarts)
            {
                if (entry < from || entry > to) continue;
                int first = LowerBound(voice.pitchTimes, entry + .02), last = LowerBound(voice.pitchTimes, entry + .15);
                if (last - first < 6) continue;
                double confidence = 0;
                for (int i = first; i < last; i++) confidence += voice.pitchConfidence[i];
                if (confidence / (last - first) < .40) continue;
                int rmsFirst = Math.Max(0, (int)Math.Ceiling((entry + .02) / voice.rmsStep));
                int rmsLast = Math.Min(voice.rms.Length, (int)Math.Ceiling((entry + .15) / voice.rmsStep));
                if (rmsLast - rmsFirst < 6) continue;
                bool audible = true;
                for (int i = rmsFirst; i < rmsLast; i++) if (voice.rms[i] < .005) { audible = false; break; }
                if (audible) return entry;
            }
            return double.NaN;
        }

        private static void ValidateVoice(PcmVocalAnalysis.Evidence voice)
        {
            if (voice == null) return;
            if (!Finite(voice.rmsStep) || voice.rmsStep <= 0 || voice.pitchTimes == null || voice.pitchConfidence == null
                || voice.pitchTimes.Length != voice.pitchConfidence.Length || voice.rms == null || voice.voiceStarts == null
                || voice.pitchTimes.Any(t => !Finite(t) || t < 0) || voice.pitchConfidence.Any(c => !Finite(c) || c < 0 || c > 1)
                || voice.rms.Any(r => !Finite(r) || r < 0) || voice.voiceStarts.Any(t => !Finite(t) || t < 0))
                throw new ArgumentException("Sparse vocal measurements are incomplete or non-finite.", nameof(voice));
            for (int i = 1; i < voice.pitchTimes.Length; i++)
                if (voice.pitchTimes[i] <= voice.pitchTimes[i - 1]) throw new ArgumentException("Sparse vocal times must increase.", nameof(voice));
            for (int i = 1; i < voice.voiceStarts.Length; i++)
                if (voice.voiceStarts[i] <= voice.voiceStarts[i - 1]) throw new ArgumentException("Sparse vocal entries must increase.", nameof(voice));
        }

        private static int LowerBound(double[] values, double target)
        { int first = 0, last = values.Length; while (first < last) { int mid = first + (last - first) / 2; if (values[mid] < target) first = mid + 1; else last = mid; } return first; }
        private static double Round(double time) => Math.Round(time, 6, MidpointRounding.AwayFromZero);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
