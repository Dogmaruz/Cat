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

        public static MusicalCue[] Analyze(Evidence evidence, double[] pcmAttackTimes)
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
                int missing = 0, end = start + 1, limit = Math.Min(times.Length, start + (int)Math.Round(1.8 / dt));
                for (; end < limit; end++)
                {
                    missing = conf[end] < .35 ? missing + 1 : 0;
                    if (missing >= Math.Round(.06 / dt)) { end -= missing - 1; break; }
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
                if ((!plateau && !descending) || runs.Any(r => r.confidence >= .4 && r.pitch > first.pitch + 1)) continue;
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
                double finish = Math.Min(times[end], amplitudeEnd * .01) + .06;
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
                result.Add(new MusicalCue { kind = "Hold", time = time, endTime = finish, confidence = Math.Min(1, initialConfidence),
                    phraseId = "vocal-release-" + result.Count,
                    reason = descending ? "Separated vocal: continuous descending legato; measured voiced release." : "Separated vocal: held plateau followed by downward release; measured voice envelope." });
            }
            return result.ToArray();
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
