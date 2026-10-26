using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Opt-in short-support style with interval-relative voice windows, trained on eight known recordings.</summary>
    public static class PcmShortSupportAnalysis
    {
        public const string Version = "short-support-experimental-v2-relative-release";
        public const string RecurrenceVersion = "short-support-experimental-v3-source-motif";
        private sealed class Candidate
        {
            public PcmInstrumentalEvidence.NoteEvent note;
            public double end, score;
            public bool recurrent;
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
            if (notes.events.Any(note => note == null)) throw new InvalidDataException("Null instrumental event.");
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
            if (voice.rms.Max() <= .000001) return Array.Empty<MusicalCue>();
            double scale = Math.Max(.001, Quantile(voice.rms, .95));
            var hypotheses=source.ToLookup(n=>n.time);
            var attacks=source.Where(n=>Get(n,"sourceAttack")>.5 && Get(n,"neuralOnset")>=.5).ToLookup(n=>n.midi);
            var candidates = new List<Candidate>();
            var sourcePool = new List<Candidate>();
            foreach (var note in source)
            {
                foreach (double end in Endpoints(note, source, beatPeriod))
                {
                    double duration = end - note.time;
                    if (duration < .18 - 1e-7 || duration > .45 + 1e-7 || Protected(note.time, end, protectedSupports)) continue;
                    // Aggregate note reattack features span its full measured tail.
                    // Only a timestamped independent repeat inside this selected
                    // support can split it; a later repeat belongs to its own cue.
                    if (HasIndependentRepeat(note,end,hypotheses,attacks)) continue;
                    double score = StyleScore(Features(note, voice, end, beatPeriod, scale));
                    sourcePool.Add(new Candidate { note = note, end = end, score = score });
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
            AddRecurrentSupports(source, sourcePool, accepted, beatPeriod);
            return accepted.OrderBy(c => c.note.time).Select(c => new MusicalCue { kind = "Hold", time = c.note.time,
                endTime = c.end, confidence = c.score, lane = 0, phraseId = c.recurrent ? RecurrenceVersion : Version,
                reason = c.recurrent ? "Experimental source motif: seven relative-pitch attacks and normalized rhythm agree with two independent frozen seed instances; independent neural and harmonic durations support the selected exit. Known-recording calibration only, not unseen accuracy." : "Experimental short support; voice windows use the selected interval and first 75ms of release; other source/context features remain offline. "
                    + "The style score records calibration-leaf precision, not unseen-song accuracy." }).ToArray();
        }

        // Source motifs supplement a frozen snapshot of accepted V10 seeds.
        // Inferred supports never seed another inference. Semitone differences,
        // median-normalized attack gaps and pulse-relative tolerances describe
        // the phrase; recording identity and absolute pitch/time are absent.
        private sealed class Motif
        {
            public Candidate candidate;
            public double scale;
            public int[] pitches;
            public double[] rhythm;
        }
        private static void AddRecurrentSupports(PcmInstrumentalEvidence.NoteEvent[] source,
            List<Candidate> pool, List<Candidate> accepted, double beat)
        {
            var seeds = accepted.Where(c => c.score >= .95).ToArray();
            if (seeds.Length < 2) return;
            var leaders = source.GroupBy(n => n.time).OrderBy(g => g.Key)
                .Select(g => g.OrderBy(n => Get(n, "pitchRank")).First()).ToArray();
            var indices = leaders.Select((n, i) => new { n.time, i }).ToDictionary(p => p.time, p => p.i);
            Motif Describe(Candidate c)
            {
                int i = indices[c.note.time];
                if (i < 3 || i + 3 >= leaders.Length) return null;
                var gaps = Enumerable.Range(i - 3, 6).Select(j => leaders[j + 1].time - leaders[j].time).ToArray();
                double scale = Quantile(gaps, .5);
                if (scale < beat * .1) return null;
                return new Motif { candidate = c, scale = scale,
                    pitches = Enumerable.Range(i - 3, 7).Select(j => leaders[j].midi - c.note.midi).ToArray(),
                    rhythm = gaps.Select(gap => gap / scale).ToArray() };
            }
            var examples = seeds.Select(Describe).Where(m => m != null).ToArray();
            if (examples.Length < 2) return;
            var inferred = new List<Candidate>();
            foreach (var candidate in pool)
            {
                double start = candidate.note.time, duration = candidate.end - start;
                double neural = Get(candidate.note, "neuralDuration"), harmonic = Get(candidate.note, "harmonicDuration");
                // Harmonic resonance cannot supply duration by itself. Both
                // independent measurements must support this selected exit.
                if (duration < .18 || duration > .45 || Math.Min(neural, harmonic) < .18 || Math.Abs(neural - harmonic) > beat / 4
                    || Math.Max(Math.Abs(duration - neural), Math.Abs(duration - harmonic)) > beat / 4) continue;
                var motif = Describe(candidate); if (motif == null) continue;
                var matches = new List<Tuple<Candidate, double>>();
                foreach (var example in examples)
                {
                    var seed = example.candidate;
                    if (Math.Abs(seed.note.time - start) < 3 * beat) continue;
                    var pitchErrors = motif.pitches.Zip(example.pitches, (a, b) => Math.Abs(a - b)).ToArray();
                    if (pitchErrors.Average() > 3 || pitchErrors.Count(d => d > 4) > 1) continue;
                    var rhythmErrors = motif.rhythm.Zip(example.rhythm,
                        (a, b) => Math.Abs(Math.Log(Math.Max(.01, a) / Math.Max(.01, b)))).ToArray();
                    if (rhythmErrors.Average() > .4 || rhythmErrors.Max() > 1) continue;
                    double projected = (seed.end - seed.note.time) * motif.scale / example.scale;
                    double exitError = Math.Abs(duration - projected);
                    if (exitError > beat / 12) continue;
                    if (Math.Abs(neural - Get(seed.note, "neuralDuration")) > beat * .35
                        || Math.Abs(harmonic - Get(seed.note, "harmonicDuration")) > beat * .35
                        || Math.Abs(duration - (seed.end - seed.note.time)) > beat * .2
                        || Get(candidate.note, "neuralOnset") < Math.Max(.3, Get(seed.note, "neuralOnset") * .6)
                        || Get(candidate.note, "neuralPeak") < Math.Max(.15, Get(seed.note, "neuralPeak") * .5)) continue;
                    matches.Add(Tuple.Create(seed, pitchErrors.Average() + rhythmErrors.Average() + exitError / beat * 4));
                }
                var independent = new List<double>();
                foreach (var match in matches.OrderBy(m => m.Item2))
                    if (independent.All(time => Math.Abs(time - match.Item1.note.time) > 3 * beat)) independent.Add(match.Item1.note.time);
                if (independent.Count < 2) continue;
                inferred.Add(new Candidate { note = candidate.note, end = candidate.end,
                    score = 1 / (1 + matches.Min(m => m.Item2)), recurrent = true });
            }
            foreach (var candidate in inferred.OrderByDescending(c => c.score)
                .ThenByDescending(c => Get(c.note, "neuralOnset")).ThenBy(c => c.note.time))
            {
                if (accepted.Any(prior => Math.Min(prior.end, candidate.end) - Math.Max(prior.note.time, candidate.note.time) > -.078)) continue;
                accepted.Add(candidate);
            }
        }

        private static bool HasIndependentRepeat(PcmInstrumentalEvidence.NoteEvent note,double end,
            ILookup<double,PcmInstrumentalEvidence.NoteEvent> hypotheses,ILookup<int,PcmInstrumentalEvidence.NoteEvent> attacks)
        {
            foreach(var anchor in hypotheses[note.time])
            {
                // A weaker octave alias must not conceal its stronger source's
                // real repeat. Unrelated pitches and weaker anchors cannot veto.
                if((anchor.midi-note.midi)%12!=0 || Get(anchor,"pitchRank")>Get(note,"pitchRank"))continue;
                foreach(var repeat in attacks[anchor.midi])
                {
                    if(repeat.time>=end-.04)break;
                    if(repeat.time>=note.time+.08)return true;
                }
            }
            return false;
        }

        // Pruned CART: depth 8, minimum leaf 16, eight known recordings balanced
        // by candidate row count. Only leaves with >=90% training precision and
        // positives from >=2 recordings (>=2 rows each) are retained. The five
        // leaves below use source-only features. Exact training is reproducible
        // with train_short_support_release.py; all other leaves abstain.
        // No absolute timestamp, recording ID or absolute MIDI is a model input.
        private static double StyleScore(double[] f)
        {
            if (f[28] <= 3.3393975846072963)
            {
                if (f[25] <= .11150416666666665)
                {
                    if (f[31] <= 2.185078171930984)
                        return f[32] > .6617969721600504 && f[27] <= .10141111774310142
                            && f[22] <= .4999999999999992 ? 1 : 0;
                    return f[33] <= .25305338220162826 && f[22] <= .6737483836725249
                        && f[12] <= .25132331521739104 && f[21] > .5 && f[9] <= .533275095 ? 150.0/155 : 0;
                }
                return f[32] > 1.6745079974981902 && f[27] <= .8142527121907923
                    && f[33] > .3399387775310154 && f[24] > 1.3215081730568516 ? 1 : 0;
            }
            if (f[32] <= .3749968580310127)
                return f[26] > .2905420062820082 && f[22] <= .6095863620689643
                    && f[27] > .007198593979954308 && f[21] > .5 && f[22] > .3657830948275848
                    && f[27] <= .022726673724556293 ? 215.0/216 : 0;
            return f[26] > 6.26581815308335 && f[13] > .46846644452773567
                && f[11] <= .040495199999999995 ? 1 : 0;
        }

        // The first 22 positions keep the verified instrumental schema; five
        // time features are normalized by the independent measured pulse.
        // Appended values are duration/pulse, duration/local gap, then ten
        // interval-relative voice features in the trainer's declared order.
        // This bound applies to those voice windows, not to every instrumental
        // descriptor, phrase context or the whole-recording RMS normalization.
        private static double[] Features(PcmInstrumentalEvidence.NoteEvent note,
            PcmVocalAnalysis.Evidence voice, double end, double beat, double scale)
        {
            var f = new double[34]; Array.Copy(note.features, f, 22);
            foreach (int i in new[] {5,10,12,13,14}) f[i] /= beat;
            double start=note.time, body=Energy(voice,start+.03,end-.025), tail=Energy(voice,end+.015,end+.075);
            double early=Energy(voice,start+.01,start+.06), late=Energy(voice,end-.07,end-.02), before=Energy(voice,start-.09,start-.025);
            f[22]=(end-start)/beat;f[23]=(end-start)/Math.Max(.06,Get(note,"localGap"));
            f[24]=body/scale;f[25]=tail/scale;f[26]=Math.Min(8,body/Math.Max(.0001,tail));
            f[27]=early/scale;f[28]=late/Math.Max(.0001,early);f[31]=early/Math.Max(.0001,before);
            f[32]=Peak(voice,start,end)/scale;
            int first=LowerBound(voice.pitchTimes,start+.025),last=LowerBound(voice.pitchTimes,end-.025),strong=0;
            double confidence=0;var pitches=new Dictionary<int,int>();
            for(int i=first;i<last;i++)
            {
                confidence+=voice.pitchConfidence[i];
                if(voice.pitchConfidence[i]<.4)continue;
                strong++;int pitch=voice.pitch[i];pitches.TryGetValue(pitch,out int count);pitches[pitch]=count+1;
            }
            if(last>first){f[29]=(double)strong/(last-first);f[33]=confidence/(last-first);}
            if(strong>0)
            {
                int mode=pitches.OrderByDescending(p=>p.Value).First().Key;
                f[30]=(double)pitches.Where(p=>Math.Abs(p.Key-mode)<=1).Sum(p=>p.Value)/strong;
            }
            return f;
        }

        private static int LowerBound(double[] values,double time)
        {
            int lo=0,hi=values?.Length??0;
            while(lo<hi){int mid=(lo+hi)/2;if(values[mid]<time)lo=mid+1;else hi=mid;}
            return lo;
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
