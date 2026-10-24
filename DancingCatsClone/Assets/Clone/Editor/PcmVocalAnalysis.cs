using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Consumes optional local vocal features. Classifies audio gestures without reading a reference chart.</summary>
    public static class PcmVocalAnalysis
    {
        private const string SeparatorHash = "ce74ef3b6a6024ce44211a07be9cf8bc6d87728cc852a68ab34eb8e58cde9c8b";
        private const string NoteHash = "2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec";

        [Serializable]
        public sealed class Evidence
        {
            public int schemaVersion, featureQuantization, sampleRate, channels, sampleFrames;
            public string sourceFileSha256, sourcePcmSha256, featureSha256, separatorModelSha256, noteModelSha256, analysisVersion;
            public double rmsStep;
            public double[] pitchTimes, pitchConfidence, onset, rms, voiceStarts;
            public int[] pitch;
            public bool audioUploaded;
        }

        private sealed class Run { public int pitch; public double duration, confidence, at; }

        /// <summary>Returns null when no optional cache exists. Corrupt/mismatched evidence throws explicitly.</summary>
        public static Evidence LoadForClip(AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            string asset = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(asset)) return null;
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", asset));
            if (!File.Exists(source)) return null;
            string hash; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(source)) hash = Hex(sha.ComputeHash(stream));
            string path = Path.Combine(Application.dataPath, "Clone/Editor/AudioEvidence", hash + ".voice.json");
            if (!File.Exists(path)) return null;
            Evidence evidence = JsonUtility.FromJson<Evidence>(File.ReadAllText(path));
            ValidateStructure(evidence);
            if (!string.Equals(evidence.sourceFileSha256, hash, StringComparison.Ordinal))
                throw new InvalidDataException("Vocal evidence source file hash does not match the selected audio.");
            return evidence;
        }

        public static void Validate(Evidence evidence, string decodedPcmSha256, int sampleRate, int channels, int sampleFrames)
        {
            ValidateStructure(evidence);
            if (!string.Equals(evidence.sourcePcmSha256, decodedPcmSha256, StringComparison.Ordinal)
                || evidence.sampleRate != sampleRate || evidence.channels != channels || evidence.sampleFrames != sampleFrames)
                throw new InvalidDataException("Vocal evidence does not match decoded PCM; regenerate the local audio evidence.");
        }

        private static void ValidateStructure(Evidence e)
        {
            if (e == null || e.schemaVersion != 1 || e.featureQuantization != 100000000 || e.audioUploaded || e.sampleRate < 8000 || e.channels < 1 || e.channels > 2 || e.sampleFrames < e.sampleRate
                || double.IsNaN(e.rmsStep) || double.IsInfinity(e.rmsStep) || Math.Abs(e.rmsStep - .01) > 1e-9 || e.separatorModelSha256 != SeparatorHash || e.noteModelSha256 != NoteHash
                || !IsHash(e.sourcePcmSha256) || !IsHash(e.sourceFileSha256) || !IsHash(e.featureSha256))
                throw new InvalidDataException("Unsupported vocal evidence schema, audio metadata or model provenance.");
            if (e.pitchTimes == null || e.pitchConfidence == null || e.onset == null || e.rms == null || e.voiceStarts == null || e.pitch == null
                || e.pitchTimes.Length < 10 || e.pitch.Length != e.pitchTimes.Length || e.pitchConfidence.Length != e.pitch.Length || e.onset.Length != e.pitch.Length)
                throw new InvalidDataException("Vocal evidence arrays are missing or inconsistent.");
            double duration = e.sampleFrames / (double)e.sampleRate;
            if (Math.Abs(e.rms.Length * e.rmsStep - duration) > .03 || e.pitchTimes[e.pitchTimes.Length - 1] < duration - .5
                || e.pitchTimes[e.pitchTimes.Length - 1] > duration + .1)
                throw new InvalidDataException("Vocal evidence does not cover the recording.");
            CheckFinite(e.pitchTimes, 0, duration + .1, true); CheckFinite(e.pitchConfidence, 0, 1.00001, false);
            CheckFinite(e.onset, 0, 1.00001, false); CheckFinite(e.rms, 0, 10, false); CheckFinite(e.voiceStarts, 0, duration, true);
            if (e.pitch.Any(value => value < 48 || value > 84))
                throw new InvalidDataException("Vocal evidence pitch lies outside the measured model range.");
            string actualHash = FeatureHash(e);
            if (actualHash != e.featureSha256)
                throw new InvalidDataException("Vocal evidence integrity digest is invalid; regenerate the evidence. Expected " + e.featureSha256 + "; calculated " + actualHash);
        }

        private static void CheckFinite(double[] values, double min, double max, bool increasing)
        {
            for (int i = 0; i < values.Length; i++)
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]) || values[i] < min || values[i] > max || (increasing && i > 0 && values[i] <= values[i - 1]))
                    throw new InvalidDataException("Vocal evidence contains invalid or unsorted numeric values.");
        }

        public static MusicalCue[] Analyze(Evidence evidence, double[] pcmAttackTimes, double beatPeriod = 0)
        {
            if (evidence == null) return Array.Empty<MusicalCue>();
            ValidateStructure(evidence);
            var physicalAttacks = (pcmAttackTimes ?? Array.Empty<double>())
                .Where(t => !double.IsNaN(t) && !double.IsInfinity(t) && t >= 0 && t < evidence.sampleFrames / (double)evidence.sampleRate).Distinct().OrderBy(t => t).ToArray();
            var attacks = physicalAttacks.Concat(evidence.voiceStarts).Distinct().OrderBy(t => t);
            double[] times = evidence.pitchTimes, conf = evidence.pitchConfidence, energy = evidence.rms;
            int[] pitch = evidence.pitch; double dt = MedianStep(times); var result = new List<MusicalCue>();
            foreach (double time in attacks)
            {
                int start = LowerBound(times, time + .08);
                if (start + 8 >= times.Length) continue;
                int initialPitch = pitch[start]; double initialConfidence = Mean(conf, start, start + 8);
                if (initialPitch < 55 || initialPitch > 72 || initialConfidence < .5) continue;
                int gapFrom = Math.Max(0, (int)Math.Ceiling(time / .01)), gapTo = Math.Min(energy.Length, (int)Math.Ceiling((time + .8) / .01));
                double gapPeak = .005; for (int i = gapFrom; i < gapTo; i++) gapPeak = Math.Max(gapPeak, energy[i]);
                int missing = 0, end = start + 1, limit = Math.Min(times.Length, start + (int)Math.Round(1.8 / dt));
                for (; end < limit; end++)
                {
                    missing = conf[end] < .35 ? missing + 1 : 0;
                    int amplitudeIndex = Math.Min(energy.Length - 1, (int)Math.Ceiling(times[end] / .01));
                    // A pitch estimate may briefly fail during a still-audible glide.
                    // Genuine vocal silence retains the original shorter gap limit.
                    if (missing >= Math.Round(.12 / dt) || missing >= Math.Round(.06 / dt) && energy[amplitudeIndex] < gapPeak * .1)
                    { end -= missing - 1; break; }
                }
                end = Math.Min(end, limit - 1);
                if (times[end] - time < .45 || times[end] - time > 1.9) continue;
                var runs = new List<Run>(); int runStart = start;
                for (int i = start + 1; i <= end; i++)
                {
                    if (i != end && pitch[i] == pitch[runStart]) continue;
                    if ((i - runStart) * dt >= .06) runs.Add(new Run { pitch = pitch[runStart], duration = (i - runStart) * dt, confidence = Mean(conf, runStart, i), at = times[runStart] });
                    runStart = i;
                }
                if (runs.Count < 2) continue;
                Run first = runs[0]; var release = runs.Skip(1).Where(r => r.confidence >= .35 && first.pitch - r.pitch >= 2 && r.duration >= .07).ToArray();
                var strong = runs.Where(r => r.confidence >= .4).ToArray();
                bool descending = strong.Length >= 3 && strong[1].pitch <= strong[0].pitch && strong[2].pitch <= strong[1].pitch && strong[0].pitch - strong[2].pitch >= 5;
                bool plateau = first.duration >= .3 && first.duration <= .75 && release.Length > 0;
                bool softRelease = first.duration >= .3 && first.duration <= .75 && runs.Skip(1).Any(r => r.confidence >= .35
                    && r.confidence <= first.confidence * .75 && first.pitch - r.pitch == 1 && r.duration >= .07);
                bool ornament = strong.Length >= 3 && strong[0].duration >= .08 && strong[0].duration <= .28 && strong[1].duration <= .2
                    && strong[1].pitch - strong[0].pitch >= 2 && strong[1].pitch - strong[0].pitch <= 4
                    && Math.Abs(strong[2].pitch - strong[0].pitch) <= 1 && strong[2].duration >= .3;
                if ((!plateau && !descending && !ornament && !softRelease)
                    || !ornament && runs.Any(r => r.confidence >= .4 && r.pitch > first.pitch + 1)) continue;
                if (release.Length > 0)
                {
                    Run later = runs.FirstOrDefault(r => r.at > release[0].at && r.pitch >= release[0].pitch + 2 && r.confidence >= .35);
                    if (later != null) end = Math.Min(end, LowerBound(times, later.at));
                }
                int index = Math.Max(0, (int)Math.Ceiling(time / .01));
                int maxIndex = Math.Min(energy.Length - 1, (int)Math.Ceiling(times[end] / .01));
                if (maxIndex < index || index >= energy.Length) continue;
                double peak = .005; for (int i = index; i <= maxIndex; i++) peak = Math.Max(peak, energy[i]);
                missing = 0; int amplitudeEnd = maxIndex;
                for (int i = index + 35; i <= maxIndex; i++)
                {
                    missing = energy[i] < peak * .15 ? missing + 1 : 0;
                    if (missing >= 4) { amplitudeEnd = i - missing + 1; break; }
                }
                double finish = softRelease && !plateau ? first.at + first.duration : Math.Min(times[end], amplitudeEnd * .01) + .06;
                // Prefer physical rearticulation near the perceived voice release;
                // vocal-envelope starts are deliberately excluded from this snap.
                double nearest = finish, nearestDistance = .18 + 1e-9;
                foreach (double attack in physicalAttacks)
                {
                    double distance = Math.Abs(attack - finish);
                    if (attack < time + .45 || attack > time + 1.4 || distance > .18 || distance >= nearestDistance) continue;
                    nearest = attack; nearestDistance = distance;
                }
                finish = nearest;
                if (finish - time < .45 || finish - time > 1.4 || result.Any(cue => Math.Min(finish, cue.endTime) - Math.Max(time, cue.time) > .25)) continue;
                string reason = descending ? "Separated vocal: continuous descending legato; measured voiced release."
                    : "Separated vocal: held plateau followed by downward release; measured voice envelope.";
                if (ornament) reason = "Separated vocal: returning ornament followed by a sustained body note.";
                else if (softRelease && !plateau) reason = "Separated vocal: stable plateau ending in a weakening semitone fall; release at the main note boundary.";
                result.Add(new MusicalCue { kind = "Hold", time = time, endTime = finish, confidence = Math.Min(1, initialConfidence),
                    phraseId = "vocal-release-" + result.Count,
                    reason = reason });
            }
            int ceiling = RegisterCeiling(evidence, dt);
            if (ceiling >= 0)
            {
                AppendWithoutOverlap(result, UpperPickups(evidence, ceiling));
                AppendWithoutOverlap(result, CadenceRestFills(evidence, physicalAttacks, beatPeriod, ceiling, dt));
                AppendWithoutOverlap(result, FallingGlideHandoffs(evidence, physicalAttacks, result, beatPeriod, ceiling, dt));
            }
            return result.OrderBy(cue => cue.time).ToArray();
        }

        private static void AppendWithoutOverlap(List<MusicalCue> result, IEnumerable<MusicalCue> candidates)
        {
            foreach (var candidate in candidates.OrderBy(cue => cue.time))
                if (!result.Any(cue => Math.Min(candidate.endTime, cue.endTime) - Math.Max(candidate.time, cue.time) > .25)) result.Add(candidate);
        }

        private static int RegisterCeiling(Evidence e, double dt)
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < e.pitch.Length; i++)
                if (e.pitchConfidence[i] >= .45) { int count; counts.TryGetValue(e.pitch[i], out count); counts[e.pitch[i]] = count + 1; }
            return counts.Where(pair => pair.Value * dt >= .2).Select(pair => pair.Key).DefaultIfEmpty(-1).Max();
        }

        private static MusicalCue[] UpperPickups(Evidence e, int ceiling)
        {
            var peaks = OnsetPeaks(e.onset); var candidates = new List<MusicalCue>();
            foreach (int onset in peaks)
            {
                double start = e.pitchTimes[onset]; int low = e.pitch[onset];
                double initial = Mean(e.pitchConfidence, onset, Math.Min(e.pitch.Length, onset + 5));
                if (low < ceiling - 3 || low >= ceiling || initial < .25) continue;
                for (int high = Math.Max(ceiling - 1, low + 1); high < Math.Min(ceiling + 1, low + 4); high++)
                {
                    int first = -1;
                    for (int i = LowerBound(e.pitchTimes, start + .04); i < e.pitch.Length && e.pitchTimes[i] <= start + .25; i++)
                        if (e.pitch[i] == high) { first = i; break; }
                    if (first < 0) continue;
                    int end = first; while (end + 1 < e.pitch.Length && e.pitch[end + 1] == high) end++;
                    if (e.pitchTimes[end] - e.pitchTimes[first] < .3 || Mean(e.pitchConfidence, first, end + 1) < .45) continue;
                    int release = -1;
                    foreach (int peak in peaks)
                        if (e.pitchTimes[peak] >= start + .45 && e.pitchTimes[peak] <= start + 1.1 && e.pitch[peak] == high && e.onset[peak] >= .7)
                        { release = peak; break; }
                    if (release < 0) continue;
                    if (!HasAudibleContinuity(e, start, e.pitchTimes[release])) continue;
                    candidates.Add(new MusicalCue { kind = "Hold", time = start, endTime = e.pitchTimes[release], confidence = e.onset[onset],
                        reason = "Separated vocal: brief pickup into the measured upper register; release on strong same-note rearticulation." });
                    break;
                }
            }
            var selected = new List<MusicalCue>();
            foreach (var candidate in candidates.OrderByDescending(cue => cue.confidence).ThenBy(cue => cue.time))
                if (!selected.Any(cue => Math.Abs(candidate.time - cue.time) < .15)) selected.Add(candidate);
            return selected.OrderBy(cue => cue.time).ToArray();
        }

        private static int[] OnsetPeaks(double[] values)
        {
            var maxima = new List<int>();
            for (int i = 1; i < values.Length - 1; i++)
            {
                if (values[i] < .45 || values[i] <= values[i - 1]) continue;
                int end = i; while (end + 1 < values.Length && values[end + 1] == values[i]) end++;
                if (end + 1 < values.Length && values[end] > values[end + 1]) maxima.Add((i + end) / 2);
                i = end;
            }
            // Match height/distance/prominence ordering used by the offline CLI.
            var separated = new List<int>();
            foreach (int peak in maxima.OrderByDescending(i => values[i]).ThenByDescending(i => i))
                if (!separated.Any(other => Math.Abs(peak - other) < 7)) separated.Add(peak);
            return separated.Where(peak =>
            {
                double left = values[peak], right = values[peak];
                for (int i = peak; i >= 0 && values[i] <= values[peak]; i--) left = Math.Min(left, values[i]);
                for (int i = peak; i < values.Length && values[i] <= values[peak]; i++) right = Math.Min(right, values[i]);
                return values[peak] - Math.Max(left, right) >= .08;
            }).OrderBy(i => i).ToArray();
        }

        private static MusicalCue[] CadenceRestFills(Evidence e, double[] attacks, double beatPeriod, int ceiling, double dt)
        {
            if (!(beatPeriod >= .3 && beatPeriod <= 1.2)) return Array.Empty<MusicalCue>();
            var result = new List<MusicalCue>();
            foreach (double start in attacks.Concat(e.voiceStarts).Distinct().OrderBy(value => value))
            {
                int beginPitch = LowerBound(e.pitchTimes, start + .08);
                if (beginPitch + 8 >= e.pitch.Length) continue;
                int initial = e.pitch[beginPitch]; double confidence = Mean(e.pitchConfidence, beginPitch, beginPitch + 8);
                if (initial < ceiling - 2 || confidence < .5) continue;
                int upperEnd = beginPitch;
                while (upperEnd + 1 < e.pitch.Length && e.pitch[upperEnd + 1] == initial && e.pitchConfidence[upperEnd + 1] >= .35) upperEnd++;
                double plateau = e.pitchTimes[upperEnd] - e.pitchTimes[beginPitch];
                if (plateau < .3 || plateau > .75) continue;
                int tail = upperEnd + 1, releaseEnd = Math.Min(e.pitch.Length, tail + (int)Math.Round(.3 / dt)), released = 0;
                for (int i = tail; i < releaseEnd; i++)
                    if (e.pitch[i] <= initial - 2 && e.pitch[i] >= initial - 5 && e.pitchConfidence[i] >= .35) released++;
                if (released < Math.Round(.07 / dt)) continue;
                int begin = (int)Math.Ceiling(start / .01), limit = Math.Min(e.rms.Length - 1, (int)Math.Ceiling((start + 1.3) / .01));
                if (limit <= begin + 35) continue;
                double peak = .005; for (int i = begin; i < limit; i++) peak = Math.Max(peak, e.rms[i]);
                int missing = 0, voiceEnd = -1;
                for (int i = begin + 35; i < limit; i++)
                { missing = e.rms[i] < peak * .15 ? missing + 1 : 0; if (missing >= 6) { voiceEnd = i - missing + 1; break; } }
                if (voiceEnd < 0) continue;
                int response = -1;
                for (int i = voiceEnd + 6; i < Math.Min(e.rms.Length - 6, voiceEnd + 230); i++)
                {
                    if (Mean(e.rms, i, i + 5) < peak * .2) continue;
                    int note = Math.Min(e.pitch.Length - 1, LowerBound(e.pitchTimes, i * .01 + .06));
                    if (e.pitchConfidence[note] < .4) continue;
                    if (e.pitch[note] <= initial - 5) response = i;
                    break; // Do not skip an intervening confident higher syllable.
                }
                if (response < 0 || (response - voiceEnd) * .01 < .6) continue;
                if (Quantile(e.rms, voiceEnd + 6, Math.Max(voiceEnd + 7, response - 5), .9) > peak * .2) continue;
                double measured = response * .01, nearestDistance = .1 + 1e-9;
                foreach (double attack in attacks)
                { double distance = Math.Abs(attack - response * .01); if (distance <= .1 && distance < nearestDistance) { measured = attack; nearestDistance = distance; } }
                double finish = Math.Min(measured - beatPeriod / 2, start + 1.6);
                if (finish - start < .9) continue;
                var candidate = new MusicalCue { kind = "Hold", time = start, endTime = finish, confidence = confidence,
                    reason = "Musical support policy: upper cadence and genuine vocal rest; half-beat handoff before the next measured lower answer." };
                if (!result.Any(cue => Math.Min(finish, cue.endTime) - Math.Max(start, cue.time) > .25)) result.Add(candidate);
            }
            return result.ToArray();
        }

        private static MusicalCue[] FallingGlideHandoffs(Evidence e, double[] attacks, List<MusicalCue> confirmed, double beatPeriod, int ceiling, double dt)
        {
            if (!(beatPeriod >= .3 && beatPeriod <= 1.2)) return Array.Empty<MusicalCue>();
            var result = new List<MusicalCue>();
            foreach (double start in attacks.Concat(e.voiceStarts).Distinct().OrderBy(value => value))
            {
                int begin = LowerBound(e.pitchTimes, start), end = Math.Min(e.pitch.Length - 1, LowerBound(e.pitchTimes, start + 1.3)), last = begin;
                var runs = new List<Run>();
                for (int i = begin + 1; i <= end; i++)
                {
                    if (i != end && e.pitch[i] == e.pitch[last]) continue;
                    if ((i - last) * dt >= .045) runs.Add(new Run { pitch = e.pitch[last], duration = (i - last) * dt,
                        confidence = Mean(e.pitchConfidence, last, i), at = e.pitchTimes[last] });
                    last = i;
                }
                if (runs.Count < 3) continue;
                Run body = null;
                for (int i = 2; i < Math.Min(runs.Count, 5); i++)
                {
                    var prefix = runs.Take(i).ToArray(); Run candidate = runs[i]; double length = prefix.Sum(run => run.duration);
                    double confidence = prefix.Sum(run => run.confidence * run.duration) / length; int high = prefix.Max(run => run.pitch), low = prefix.Min(run => run.pitch);
                    if (length < .1 || length > .35 || high - low != 1 || low < ceiling - 5 || confidence >= .6) continue;
                    if (candidate.duration < .3 || candidate.duration > .75 || candidate.confidence < .5 || high - candidate.pitch < 2 || candidate.at - start > .4) continue;
                    body = candidate; break;
                }
                if (body == null) continue;
                var next = confirmed.Where(cue => cue.time > start + .5 && cue.time < start + 1.5).OrderBy(cue => cue.time).FirstOrDefault();
                if (next == null) continue;
                double flight = Math.Max(beatPeriod / 2, body.duration);
                double finish = Math.Min(body.at + body.duration, next.time - flight);
                if (finish - start < .4 || finish - start > 1.1) continue;
                if (!HasAudibleContinuity(e, start, finish)) continue;
                var gesture = new MusicalCue { kind = "Hold", time = start, endTime = finish, confidence = body.confidence,
                    reason = "Musical support policy: falling upper-register glide into a plateau; reserve a note-length airborne handoff to the next independently confirmed support." };
                if (!result.Any(cue => Math.Min(finish, cue.endTime) - Math.Max(start, cue.time) > .25)) result.Add(gesture);
            }
            return result.ToArray();
        }

        private static bool HasAudibleContinuity(Evidence e, double start, double finish)
        {
            int begin = Math.Max(0, (int)Math.Ceiling(start / e.rmsStep)), end = Math.Min(e.rms.Length, (int)Math.Ceiling(finish / e.rmsStep));
            if (end <= begin) return false;
            double peak = .005; for (int i = begin; i < end; i++) peak = Math.Max(peak, e.rms[i]);
            int missing = 0;
            for (int i = begin; i < end; i++)
            {
                missing = e.rms[i] < peak * .1 ? missing + 1 : 0;
                // Pitch confidence can persist through a real pause. Only the
                // explicit cadence/rest policy may extend across such silence.
                if (missing * e.rmsStep >= .06) return false;
            }
            return true;
        }

        private static double Quantile(double[] values, int begin, int end, double fraction)
        {
            var sorted = new double[Math.Max(1, end - begin)]; Array.Copy(values, begin, sorted, 0, sorted.Length); Array.Sort(sorted);
            double index = fraction * (sorted.Length - 1); int lower = (int)Math.Floor(index), upper = (int)Math.Ceiling(index);
            return sorted[lower] + (index - lower) * (sorted[upper] - sorted[lower]);
        }

        private static int LowerBound(double[] values, double target)
        { int low = 0, high = values.Length; while (low < high) { int mid = (low + high) / 2; if (values[mid] < target) low = mid + 1; else high = mid; } return low; }
        private static double Mean(double[] values, int first, int end)
        { double total = 0; for (int i = first; i < end; i++) total += values[i]; return total / Math.Max(1, end - first); }
        private static double MedianStep(double[] times)
        { var steps = new double[times.Length - 1]; for (int i = 0; i < steps.Length; i++) steps[i] = times[i + 1] - times[i]; Array.Sort(steps); return steps[steps.Length / 2]; }
        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        private static bool IsHash(string value) { return value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'); }

        private static string FeatureHash(Evidence e)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RadioArchipelagoVoiceEvidence/quantized-1e8/1\n" + e.sourcePcmSha256 + "\n" + e.sourceFileSha256 + "\n"));
                writer.Write(e.sampleRate); writer.Write(e.channels); writer.Write(e.sampleFrames); writer.Write(Quantize(e.rmsStep));
                foreach (var values in new[] { e.pitchTimes, e.pitchConfidence, e.onset, e.rms, e.voiceStarts })
                { writer.Write(values.Length); foreach (double value in values) writer.Write(Quantize(value)); }
                writer.Write(e.pitch.Length); foreach (int value in e.pitch) writer.Write(value);
                writer.Write(Encoding.ASCII.GetBytes(e.separatorModelSha256 + "\n" + e.noteModelSha256 + "\n")); writer.Flush();
                using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(stream.ToArray()));
            }
        }
        private static long Quantize(double value) { return (long)Math.Floor(value * 100000000 + .5); }
    }
}
