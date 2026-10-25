using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Opt-in experimental short-support style, trained on independent source measurements from six calibration recordings.</summary>
    public static class PcmShortSupportAnalysis
    {
        public const string Version = "short-support-experimental-v1";
        private sealed class Candidate
        {
            public PcmInstrumentalEvidence.NoteEvent note;
            public double end, score;
        }

        // The caller verifies both optional evidence files against the actual PCM.
        // This policy receives no chart annotations, recording identity or coordinates.
        public static MusicalCue[] Analyze(PcmInstrumentalEvidence.Evidence notes, PcmVocalAnalysis.Evidence voice,
            double beatPeriod, MusicalCue[] protectedSupports = null)
        {
            if (notes?.events == null || voice?.rms == null || voice.rms.Length == 0 || notes.events.Length == 0)
                return Array.Empty<MusicalCue>();
            if (!Finite(beatPeriod) || beatPeriod < .15 || beatPeriod > 2)
                throw new ArgumentException("A measured finite pulse period is required for short-support phrasing.");
            var source = notes.events.OrderBy(n => n.time).ThenBy(n => n.midi).ToArray();
            foreach (var note in source)
            {
                if (note == null || !Finite(note.time) || !Finite(note.endTime) || note.endTime < note.time)
                    throw new InvalidDataException("Malformed instrumental event boundary.");
                for (int i = 0; i < PcmInstrumentalEvidence.FeatureCount; i++)
                    if (note.features == null || note.features.Length != PcmInstrumentalEvidence.FeatureCount
                        || !Finite(note.features[i]) || Math.Abs(note.features[i]) > 100000)
                        throw new InvalidDataException("Malformed instrumental event features.");
            }
            double scale = Math.Max(.001, Quantile(voice.rms, .95));
            var candidates = new List<Candidate>();
            foreach (var note in source)
            {
                // A real repeated syllable must not become one sustained support.
                if (Get(note, "samePitchReattack") >= .7 && Get(note, "insideAttacks") >= 1) continue;
                double body = Energy(voice, note.time + .04, note.time + .22) / scale;
                double tailRaw = Energy(voice, note.time + .25, note.time + .42);
                double tail = tailRaw / scale;
                double peak = Peak(voice, note.time, note.time + .3) / scale;
                double bodyTail = Math.Min(8, body * scale / Math.Max(.0001, tailRaw));
                foreach (double end in Endpoints(note, source, beatPeriod))
                {
                    double duration = end - note.time;
                    if (duration < .18 - 1e-7 || duration > .45 + 1e-7 || Protected(note.time, end, protectedSupports)) continue;
                    double score = StyleScore(body, tail, peak, bodyTail, duration / beatPeriod,
                        Get(note, "localGap") / beatPeriod, duration / Math.Max(.06, Get(note, "localGap")), Get(note, "sourceAttack"));
                    if (score < .9) continue;
                    candidates.Add(new Candidate { note = note, end = end, score = score });
                }
            }
            var accepted = new List<Candidate>();
            foreach (var candidate in candidates.OrderByDescending(c => c.score).ThenByDescending(c => Get(c.note, "neuralOnset"))
                .ThenBy(c => c.note.time))
            {
                if (accepted.Any(prior => Math.Min(prior.end, candidate.end) - Math.Max(prior.note.time, candidate.note.time) > -.078)) continue;
                accepted.Add(candidate);
            }
            return accepted.OrderBy(c => c.note.time).Select(c => new MusicalCue { kind = "Hold", time = c.note.time,
                endTime = c.end, confidence = c.score, lane = 0, phraseId = Version,
                reason = "Experimental multi-recording short-support style; measured note release permits bounded musical-pulse phrasing. "
                    + "The style score records calibration-leaf precision, not unseen-song accuracy." }).ToArray();
        }

        // Pruned weighted CART: depth 6, minimum leaf 8, positive weight 2, six
        // calibration recordings balanced by row count. These are the only two
        // leaves with >=90% training precision and positives from >=2 recordings
        // (at least two positive examples per recording). Every other leaf abstains.
        // No absolute timestamp, recording ID or absolute MIDI is a model input.
        private static double StyleScore(double body, double tail, double peak, double bodyTail,
            double durationBeats, double localGapBeats, double durationToLocalGap, double sourceAttack)
        {
            if (tail <= .1128373128163614) return 0;
            if (body <= .12290058594036639)
                return bodyTail > .2827520899624273 && durationBeats <= .6313237931034489
                    && localGapBeats > .2597977338591345 && durationToLocalGap > 1.3581860437183202
                    ? 52.0 / 54 : 0;
            return peak > 1.6384165719856725 && tail <= .604104816520417 && sourceAttack > .5
                && durationBeats <= .6866406170212782 ? 30.0 / 31 : 0;
        }

        private static IEnumerable<double> Endpoints(PcmInstrumentalEvidence.NoteEvent note,
            PcmInstrumentalEvidence.NoteEvent[] source, double beat)
        {
            double harmonic = Get(note, "harmonicDuration"), neural = Get(note, "neuralDuration");
            var durations = new List<double>();
            foreach (double measured in new[] { harmonic, neural })
                if (measured >= .18 && measured <= .45) durations.Add(measured);
            foreach (double subdivision in new[] { .25, .5, .75, 1.0, 1.5 })
            {
                double value = subdivision * beat;
                if (value >= .18 && value <= .45 && Math.Min(Math.Abs(value-harmonic), Math.Abs(value-neural)) <= .12)
                    durations.Add(value);
            }
            double preferred = harmonic;
            if ((preferred < .18 || preferred > .45) && neural >= .18 && neural <= .45) preferred = neural;
            if (preferred >= .18 && preferred <= .45)
            {
                double end = note.time + preferred;
                var following = source.Where(n => n.time >= note.time + .18 && n.time <= note.time + .45
                    && (Get(n, "sourceAttack") > .5 || Get(n, "neuralOnset") >= .3)).Select(n => n.time).Distinct().ToArray();
                if (following.Length > 0)
                {
                    double near = following.OrderBy(t => Math.Abs(t-end)).ThenBy(t => t).First();
                    if (Math.Abs(near-end) <= .10) end = near;
                }
                durations.Add(end-note.time);
            }
            return durations.Select(duration => Math.Round(note.time+duration,8)).Distinct().OrderBy(t=>t);
        }
        private static bool Protected(double start, double end, MusicalCue[] supports)
            => (supports ?? Array.Empty<MusicalCue>()).Any(s => s != null
                && (s.kind == "Hold" || s.kind == "Follow" || s.kind == "BigJump")
                && start < Math.Max(s.time,s.endTime) + .078 && end > s.time - .078);
        private static double Energy(PcmVocalAnalysis.Evidence voice, double start, double end)
        {
            int first=Math.Max(0,(int)Math.Ceiling(start/.01-1e-7)),last=Math.Min(voice.rms.Length,(int)Math.Ceiling(end/.01-1e-7));
            if(last<=first)return 0;double sum=0;for(int i=first;i<last;i++)sum+=voice.rms[i];return sum/(last-first);
        }
        private static double Peak(PcmVocalAnalysis.Evidence voice, double start, double end)
        {
            int first=Math.Max(0,(int)Math.Ceiling(start/.01-1e-7)),last=Math.Min(voice.rms.Length,(int)Math.Ceiling(end/.01-1e-7));
            double peak=0;for(int i=first;i<last;i++)peak=Math.Max(peak,voice.rms[i]);return peak;
        }
        private static double Quantile(double[] values,double fraction)
        {
            var ordered=values.OrderBy(x=>x).ToArray();double at=(ordered.Length-1)*fraction;int lower=(int)Math.Floor(at),upper=(int)Math.Ceiling(at);
            return ordered[lower]+(ordered[upper]-ordered[lower])*(at-lower);
        }
        private static double Get(PcmInstrumentalEvidence.NoteEvent note,string key)=>PcmInstrumentalEvidence.Get(note,key);
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
