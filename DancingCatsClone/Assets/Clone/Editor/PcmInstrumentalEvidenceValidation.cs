using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Known cross-language bytes and malformed-cache cases; no learned accuracy or reference chart is involved.</summary>
    public static class PcmInstrumentalEvidenceValidation
    {
        private const string Model = "2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec";
        private const string KnownHash = "e8f8b9ce0cc0bdf4ad3f53f414f1cb4570a6bf2dca890219d7865a8859cfeb8d";
        private static readonly string[] Names = ("neuralOnset neuralPeak neuralBody neuralLate neuralTail neuralDuration "
            + "harmonicPartials harmonicBody harmonicLate harmonicTail harmonicDuration harmonicNovelty "
            + "nextGap previousGap localGap nextPitchDelta previousPitchDelta insideAttacks samePitchReattack pitchRank relativePitch sourceAttack").Split(' ');
        [Serializable] private sealed class FixtureEvent { public double time, endTime; public int midi; public double[] features; }
        [Serializable] private sealed class FixtureEvidence
        {
            public int schemaVersion = 1, featureQuantization = 100000000, sampleRate = 44100, channels = 2, sampleFrames = 44100;
            public string sourceFileSha256 = new string('1', 64), sourcePcmSha256 = new string('2', 64), featureSha256 = KnownHash, noteModelSha256 = Model;
            public string analysisVersion = "mixed-note-events-1", timingVersion = "input-window-time-v2";
            public bool audioUploaded = false;
            public string[] featureNames = (string[])Names.Clone();
            public FixtureEvent[] events = {
                new FixtureEvent { time = .125, endTime = .425, midi = 60, features = Enumerable.Range(0, 22).Select(i => Math.Round(i / 20.0 - .5, 8)).ToArray() },
                new FixtureEvent { time = .125, endTime = .25, midi = 64, features = new[] { -.000000005 }.Concat(Enumerable.Range(0, 21).Select(i => Math.Round(.75 - i / 10.0, 8))).ToArray() },
                new FixtureEvent { time = .875, endTime = 1, midi = 72, features = new double[22] }
            };
        }

        public static void RunChecks()
        {
            var owner = typeof(PcmInstrumentalEvidenceValidation).Assembly.GetType("DancingCats.Clone.Editor.PcmInstrumentalEvidence");
            Require(owner != null, "The optional independent instrumental evidence reader is missing.");
            var evidenceType = owner.GetNestedType("Evidence"); var noteType = owner.GetNestedType("NoteEvent");
            Require(evidenceType != null && noteType != null, "Instrument evidence needs a portable event schema.");
            var hash = owner.GetMethod("FeatureHash"); var validate = owner.GetMethod("Validate");
            Require(hash != null && validate != null, "Instrument evidence needs public integrity and actual-PCM validation.");
            object Fixture() => JsonUtility.FromJson(JsonUtility.ToJson(new FixtureEvidence()), evidenceType);
            object Field(object o, string key) => o.GetType().GetField(key).GetValue(o);
            void Set(object o, string key, object value) => o.GetType().GetField(key).SetValue(o, value);
            object Event(object e, int i) => ((Array)Field(e, "events")).GetValue(i);
            string Digest(object e) => (string)hash.Invoke(null, new[] { e });
            void Seal(object e) => Set(e, "featureSha256", Digest(e));
            void Valid(object e, string pcm = null, int rate = 44100, int channels = 2, int frames = 44100)
                => validate.Invoke(null, new object[] { e, pcm ?? new string('2', 64), rate, channels, frames });
            bool Rejects(object e, string pcm = null, int rate = 44100, int channels = 2, int frames = 44100)
            {
                try { Valid(e, pcm, rate, channels, frames); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { return true; }
                return false;
            }
            void RejectChange(Action<object> change, string reason, bool reseal = false)
            { var e = Fixture(); change(e); if (reseal) Seal(e); Require(Rejects(e), reason); }

            var known = Fixture();
            Require(Digest(known) == KnownHash, "The C# digest differs from the independent Python struct/UTF8 fixture.");
            Valid(known);
            RejectChange(e => ((double[])Field(Event(e, 0), "features"))[0] += .01, "Changed feature values must invalidate the digest.");
            RejectChange(e => Set(Event(e, 0), "endTime", .43), "Changed measured note endings must invalidate the digest.");
            RejectChange(e => Set(Event(e, 0), "time", .12), "Changed measured note starts must invalidate the digest.");
            var reordered = Fixture(); var labels = (string[])Field(reordered, "featureNames");
            var temporary = labels[0]; labels[0] = labels[1]; labels[1] = temporary;
            Require(Digest(reordered) != KnownHash && Rejects(reordered), "Feature meaning/order must be bound to the digest and schema.");
            RejectChange(e => Set(e, "analysisVersion", "mixed-note-events-2"), "Unknown analysis versions must fail even with a resealed digest.", true);
            RejectChange(e => Set(e, "timingVersion", "flattened-frame-time"), "Unknown timing provenance must fail even with a resealed digest.", true);
            RejectChange(e => Set(e, "noteModelSha256", new string('3', 64)), "Unknown note models must fail even with a resealed digest.", true);
            RejectChange(e => Set(e, "schemaVersion", 2), "Unknown schemas must fail.", true);
            RejectChange(e => Set(e, "featureQuantization", 10000000), "Unknown quantization formats must fail.", true);
            RejectChange(e => Set(e, "audioUploaded", true), "Evidence must retain local-only provenance.", true);
            Require(Rejects(known, new string('3', 64)), "Evidence must identify the actual decoded PCM.");
            Require(Rejects(known, rate: 48000) && Rejects(known, channels: 1) && Rejects(known, frames: 44101), "Clip metadata mismatches must fail.");
            RejectChange(e => Set(e, "sourceFileSha256", "not-a-sha256"), "Malformed source file identity must fail.", true);
            RejectChange(e => Set(Event(e, 0), "features", new double[21]), "Every event needs the complete fixed feature vector.", true);
            RejectChange(e => Set(Event(e, 0), "features", null), "Missing feature vectors must fail before hashing.");
            RejectChange(e => Set(Event(e, 0), "time", double.NaN), "Non-finite starts must fail before hashing.");
            RejectChange(e => Set(Event(e, 0), "endTime", double.PositiveInfinity), "Non-finite endings must fail before hashing.");
            RejectChange(e => ((double[])Field(Event(e, 0), "features"))[0] = double.NaN, "Non-finite features must fail before hashing.");
            RejectChange(e => ((double[])Field(Event(e, 0), "features"))[0] = double.PositiveInfinity, "Infinite features must fail before hashing.");
            RejectChange(e => ((double[])Field(Event(e, 0), "features"))[0] = 100001, "Out-of-range features must fail.", true);
            RejectChange(e => Set(Event(e, 1), "midi", 60), "Duplicate time/pitch events must fail.", true);
            RejectChange(e => Set(Event(e, 1), "midi", 59), "Simultaneous notes must use ascending MIDI order.", true);
            RejectChange(e => Set(Event(e, 0), "midi", 20), "Notes below supported MIDI range must fail.", true);
            RejectChange(e => Set(Event(e, 2), "midi", 109), "Notes above supported MIDI range must fail.", true);
            RejectChange(e => Set(Event(e, 0), "endTime", .1), "A measured note cannot end before its start.", true);
            RejectChange(e => Set(Event(e, 2), "endTime", 1.01), "A measured note cannot leave the decoded PCM.", true);
            RejectChange(e => Set(Event(e, 2), "time", .1), "Event times must remain sorted.", true);
            RejectChange(e => ((Array)Field(e, "events")).SetValue(null, 0), "Null events must fail clearly.");
            var empty = Fixture(); Set(empty, "events", Array.CreateInstance(noteType, 0)); Seal(empty); Valid(empty);

            var index = owner.GetMethod("Index"); var get = owner.GetMethod("Get");
            Require(index != null && get != null && (int)index.Invoke(null, new object[] { "neuralOnset" }) == 0
                && (int)index.Invoke(null, new object[] { "sourceAttack" }) == 21, "Feature names need stable explicit indices.");
            Require((double)get.Invoke(null, new[] { Event(known, 0), "neuralOnset" }) == -.5,
                "Named feature lookup must read the validated semantic vector.");
            bool unknown = false;
            try { index.Invoke(null, new object[] { "typo" }); }
            catch (TargetInvocationException error) when (error.InnerException is ArgumentException) { unknown = true; }
            Require(unknown, "Unknown feature names must fail rather than silently selecting another column.");
            var load = owner.GetMethod("LoadForClip");
            Require(load != null, "Optional source-keyed cache lookup is missing.");
            var uncached = AudioClip.Create("Instrumental optional evidence check", 44100, 1, 44100, false);
            try { Require(load.Invoke(null, new object[] { uncached }) == null, "An unsaved clip without an optional cache must return null."); }
            finally { UnityEngine.Object.DestroyImmediate(uncached); }
            Debug.Log("PCM_INSTRUMENT_EVIDENCE PASS: independent Python digest, negative quantization, source/PCM/model/timing provenance, "
                + "feature semantics and tampering, sorted unique notes, bounded numeric data, empty evidence and named feature lookup.");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException("PCM_INSTRUMENT_EVIDENCE RED/FAIL: " + message); }
    }
}
