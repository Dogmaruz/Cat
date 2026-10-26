using System;
using System.Linq;

namespace DancingCats.Clone.Editor
{
    /// <summary>Challenges long vocal supports only with agreeing independent pitched bodies.</summary>
    public static class PcmLegacySupportAnalysis
    {
        // The generator validates both caches against the actual decoded PCM
        // before this policy runs. Missing optional evidence never vetoes a cue.
        public static bool RejectsArticulatedHold(PcmInstrumentalEvidence.Evidence instrumental, PcmVocalAnalysis.Evidence voice, MusicalCue cue)
        {
            if (instrumental?.events == null || voice?.pitchTimes == null || voice.pitchTimes.Length < 2
                || voice.pitch == null || voice.pitchConfidence == null || cue == null || cue.kind != "Hold"
                || cue.endTime - cue.time <= .45) return false;
            var steps = voice.pitchTimes.Zip(voice.pitchTimes.Skip(1), (a, b) => b - a).OrderBy(x => x).ToArray();
            double step = steps[steps.Length / 2];
            int boundaries = 0, previousPitchClass = -1;
            foreach (var group in instrumental.events.Where(n => n != null && n.time > cue.time + .12
                && n.time < cue.endTime - .12 && Tone(n)).GroupBy(n => n.time).OrderBy(g => g.Key))
            {
                foreach (var note in group.OrderByDescending(n => Get(n, "neuralOnset") * Math.Pow(Math.Max(.001, Get(n, "harmonicNovelty")), .4)))
                {
                    double neural = Get(note, "neuralDuration"), duration = note.endTime - note.time;
                    // Reuse the existing sustained-response agreement criterion:
                    // a pitch estimate alone cannot establish its physical body.
                    if (neural <= 0 || duration / neural < .65 || duration / neural > 1.8) continue;
                    int confirmed = 0;
                    for (int i = 0; i < voice.pitchTimes.Length; i++)
                        if (voice.pitchTimes[i] >= note.time + .015 && voice.pitchTimes[i] < Math.Min(note.time + .17, note.endTime)
                            && voice.pitchConfidence[i] >= .5 && voice.pitch[i] == note.midi) confirmed++;
                    if (confirmed * step < .06) continue;
                    int pitchClass = (note.midi % 12 + 12) % 12;
                    if (pitchClass != previousPitchClass) { boundaries++; previousPitchClass = pitchClass; }
                    break;
                }
                // One interior change is compatible with a held plateau and
                // release. Two independently articulated body pitches challenge
                // treating the entire phrase as a single protected support.
                if (boundaries >= 2) return true;
            }
            return false;
        }
        private static bool Tone(PcmInstrumentalEvidence.NoteEvent n)
            => Get(n, "neuralOnset") >= .6 && Get(n, "harmonicBody") >= .45
                && Get(n, "harmonicNovelty") >= .025 && Get(n, "harmonicPartials") >= 2;
        private static double Get(PcmInstrumentalEvidence.NoteEvent n, string name) => PcmInstrumentalEvidence.Get(n, name);
    }
}

