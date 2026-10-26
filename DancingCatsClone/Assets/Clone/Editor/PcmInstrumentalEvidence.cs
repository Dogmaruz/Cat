using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Verified optional measurements of notes in mixed PCM; no reference chart or song identity is used.</summary>
    public static class PcmInstrumentalEvidence
    {
        public const int FeatureCount = 22;
        private const string ModelHash = "2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec";
        private const string AnalysisVersion = "mixed-note-events-1";
        private const string ExpandedAnalysisVersion = "mixed-note-events-2-candidates8";
        private const string TimingVersion = "input-window-time-v2";
        private static readonly string[] Names = ("neuralOnset neuralPeak neuralBody neuralLate neuralTail neuralDuration "
            + "harmonicPartials harmonicBody harmonicLate harmonicTail harmonicDuration harmonicNovelty "
            + "nextGap previousGap localGap nextPitchDelta previousPitchDelta insideAttacks samePitchReattack pitchRank relativePitch sourceAttack").Split(' ');

        [Serializable] public sealed class NoteEvent
        {
            public double time, endTime;
            public int midi;
            public double[] features;
        }
        [Serializable] public sealed class Evidence
        {
            public int schemaVersion, featureQuantization, sampleRate, channels, sampleFrames;
            public string sourceFileSha256, sourcePcmSha256, featureSha256, noteModelSha256, analysisVersion, timingVersion;
            public bool audioUploaded;
            public string[] featureNames;
            public NoteEvent[] events;
        }

        /// <summary>Missing optional data returns null; damaged or mismatched data throws. Call Validate with the decoded PCM already owned by generation.</summary>
        public static Evidence LoadForClip(AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            string asset = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(asset)) return null;
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", asset));
            if (!File.Exists(source)) return null;
            string sourceHash;
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(source)) sourceHash = Hex(sha.ComputeHash(stream));
            string cache = Path.Combine(Application.dataPath, "Clone/Editor/InstrumentEvidence", sourceHash + ".notes.json");
            if (!File.Exists(cache)) return null;
            Evidence evidence;
            try { evidence = JsonUtility.FromJson<Evidence>(File.ReadAllText(cache)); }
            catch (ArgumentException error) { throw new InvalidDataException("Instrumental note evidence JSON is malformed: " + cache, error); }
            ValidateStructure(evidence);
            if (evidence.sourceFileSha256 != sourceHash || evidence.sampleRate != clip.frequency
                || evidence.channels != clip.channels || evidence.sampleFrames != clip.samples)
                throw new InvalidDataException("Instrumental note evidence differs from the selected imported audio file or clip metadata.");
            return evidence;
        }

        public static void Validate(Evidence evidence, string decodedPcmSha256, int sampleRate, int channels, int sampleFrames)
        {
            ValidateStructure(evidence);
            if (evidence.sourcePcmSha256 != decodedPcmSha256 || evidence.sampleRate != sampleRate
                || evidence.channels != channels || evidence.sampleFrames != sampleFrames)
                throw new InvalidDataException("Instrumental note evidence does not identify the actual decoded PCM.");
        }

        public static int Index(string name)
        {
            int index = Array.IndexOf(Names, name);
            if (index < 0) throw new ArgumentException("Unknown instrumental feature name: " + name, nameof(name));
            return index;
        }
        public static double Get(NoteEvent note, string name)
        {
            if (note == null || note.features == null || note.features.Length != FeatureCount)
                throw new InvalidDataException("Instrumental note features do not match the fixed schema.");
            double value = note.features[Index(name)];
            if (!Finite(value) || Math.Abs(value) > 100000)
                throw new InvalidDataException("Instrumental note feature is not finite or exceeds its supported numeric range.");
            return value;
        }

        private static void ValidateStructure(Evidence e)
        {
            if (e == null || e.schemaVersion != 1 || e.featureQuantization != 100000000 || e.audioUploaded
                || e.noteModelSha256 != ModelHash || (e.analysisVersion != AnalysisVersion && e.analysisVersion != ExpandedAnalysisVersion) || e.timingVersion != TimingVersion
                || e.sampleRate <= 0 || e.channels < 1 || e.channels > 2 || e.sampleFrames < e.sampleRate
                || !IsHash(e.sourceFileSha256) || !IsHash(e.sourcePcmSha256) || !IsHash(e.featureSha256))
                throw new InvalidDataException("Unsupported instrumental note schema, provenance, model or audio metadata.");
            if (e.featureNames == null || !e.featureNames.SequenceEqual(Names, StringComparer.Ordinal) || e.events == null)
                throw new InvalidDataException("Instrumental feature names/order or note events are missing or incompatible.");
            double duration = (double)e.sampleFrames / e.sampleRate;
            double previousTime = double.NegativeInfinity; int previousMidi = -1;
            foreach (var note in e.events)
            {
                // The producer rounds event boundaries to the digest's 1e-8-second grid.
                // Half a quantization step may exceed the unrounded final PCM boundary.
                if (note == null || !Finite(note.time) || !Finite(note.endTime) || note.time < 0
                    || note.endTime < note.time || note.endTime > duration + .0000000051
                    || note.midi < 21 || note.midi > 108 || note.features == null || note.features.Length != FeatureCount)
                    throw new InvalidDataException("Instrumental note contains invalid time, pitch or feature-vector shape.");
                if (note.time < previousTime || note.time == previousTime && note.midi <= previousMidi)
                    throw new InvalidDataException("Instrumental notes must be sorted by time and ascending simultaneous pitch without duplicates.");
                foreach (double value in note.features)
                    if (!Finite(value) || Math.Abs(value) > 100000)
                        throw new InvalidDataException("Instrumental note contains a non-finite or out-of-range feature.");
                previousTime = note.time; previousMidi = note.midi;
            }
            string actual = FeatureHash(e);
            if (actual != e.featureSha256)
                throw new InvalidDataException("Instrumental note integrity digest is invalid. Expected " + e.featureSha256 + "; calculated " + actual);
        }

        /// <summary>Explicit little-endian bytes shared with the independent local producer.</summary>
        public static string FeatureHash(Evidence e)
        {
            if (e == null || e.featureNames == null || e.events == null)
                throw new InvalidDataException("Instrumental evidence arrays are missing.");
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                string header = "RadioArchipelagoInstrumentEvidence/quantized-1e8/1\n" + e.sourcePcmSha256 + "\n"
                    + e.sourceFileSha256 + "\n" + e.noteModelSha256 + "\n" + e.analysisVersion + "\n" + e.timingVersion + "\n";
                if (header.Any(value => value > 127)) throw new InvalidDataException("Instrumental evidence header must use ASCII provenance.");
                writer.Write(Encoding.ASCII.GetBytes(header));
                writer.Write(e.sampleRate); writer.Write(e.channels); writer.Write(e.sampleFrames); writer.Write((long)e.featureQuantization);
                writer.Write(e.featureNames.Length);
                foreach (string name in e.featureNames)
                {
                    if (name == null) throw new InvalidDataException("Instrumental feature names cannot be null.");
                    byte[] bytes = Encoding.UTF8.GetBytes(name); writer.Write(bytes.Length); writer.Write(bytes);
                }
                writer.Write(e.events.Length);
                foreach (var note in e.events)
                {
                    if (note == null || note.features == null) throw new InvalidDataException("Instrumental events or features cannot be null.");
                    writer.Write(Quantize(note.time)); writer.Write(Quantize(note.endTime)); writer.Write(note.midi); writer.Write(note.features.Length);
                    foreach (double value in note.features) writer.Write(Quantize(value));
                }
                writer.Flush();
                using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(stream.ToArray()));
            }
        }
        private static long Quantize(double value)
        {
            double scaled = Math.Floor(value * 100000000 + .5);
            if (!Finite(scaled) || scaled < long.MinValue || scaled >= (double)long.MaxValue)
                throw new InvalidDataException("Instrumental numeric value cannot be represented in the digest.");
            return (long)scaled;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        private static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
    }
}
