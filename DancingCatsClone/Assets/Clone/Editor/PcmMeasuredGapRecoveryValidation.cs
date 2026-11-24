using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Counterexamples for opt-in recovery of measured attacks inside oversized airborne gaps.</summary>
    public static class PcmMeasuredGapRecoveryValidation
    {
        [MenuItem("Dancing Cats/Validate measured gap recovery")]
        public static void RunChecks()
        {
            var type = typeof(PcmMeasuredGapRecoveryValidation).Assembly.GetType("DancingCats.Clone.Editor.PcmMeasuredGapRecovery");
            Require(type != null, "Missing measured gap recovery type.");
            var method = type.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
            Require(method != null, "Missing measured gap recovery Apply entry point.");
            MusicalCue[] Run(MusicalCue[] cues, PcmInstrumentalEvidence.Evidence evidence, double limit = 1.95)
                => (MusicalCue[])method.Invoke(null, new object[] { cues, evidence, limit });

            var a = Jump(1);
            var b = Jump(4.2);
            var notes = Evidence(Note(2.6), Note(2.1), Note(3.1));
            string notesBefore = JsonUtility.ToJson(notes);
            var repaired = Run(new[] { a, b }, notes);
            Require(repaired.Length == 3 && ReferenceEquals(repaired[0], a) && ReferenceEquals(repaired[2], b),
                "An oversized ordinary gap recovers one real attack while preserving both existing cue objects.");
            Require(repaired[1].kind == "Jump" && repaired[1].time == 2.6 && repaired[1].endTime == 2.6,
                "Recovery uses the exact measured attack nearest the gap midpoint, with no grid rounding or new endpoint.");
            Require(repaired.Zip(repaired.Skip(1), (x, y) => y.time - x.endTime).All(g => g >= .18 && g <= 1.95),
                "Recovered takeoff-to-landing intervals fit the existing envelope.");
            Require(JsonUtility.ToJson(notes) == notesBefore && a.time == 1 && a.endTime == 1 && b.time == 4.2,
                "Recovery never mutates its input evidence or cue objects.");
            var shifted = Run(new[] { Jump(8.25), Jump(11.45) }, Evidence(Note(9.85), Note(9.35), Note(10.35)));
            Require(shifted.Length == 3 && shifted[1].time == 9.85,
                "Translated source times translate the recovery; no song-specific timestamp is involved.");
            var valid = new[] { Jump(1), Jump(2.8), Jump(4.6) };
            Require(SameObjects(valid, Run(valid, Evidence(Note(1.9), Note(3.7)))),
                "Already playable ordinary gaps preserve every original musical decision.");
            Require(SameObjects(new[] { a, b }, Run(new[] { a, b }, null))
                && SameObjects(new[] { a, b }, Run(new[] { a, b }, Evidence())),
                "Unavailable or empty evidence leaves a gap unresolved instead of inventing support.");

            var hold = new MusicalCue { kind = "Hold", time = 1, endTime = 2.5, lane = .4f };
            var afterHold = Jump(5.7);
            var holdResult = Run(new[] { hold, afterHold }, Evidence(Note(1.8), Note(4.1)));
            Require(holdResult.Length == 3 && ReferenceEquals(holdResult[0], hold) && hold.endTime == 2.5
                && holdResult[1].time == 4.1 && holdResult[1].lane == .4f,
                "Existing Hold bodies stay protected; recovery starts after their measured exit from the exit lane.");
            var follow = new MusicalCue { kind = "Follow", time = 1, endTime = 2.5, lane = .7f,
                points = new[] { new MusicalPoint { time = 1, lane = .7f }, new MusicalPoint { time = 2.5, lane = -.6f } } };
            var points = follow.points;
            var followResult = Run(new[] { follow, Jump(5.7) }, Evidence(Note(1.8), Note(4.1)));
            Require(followResult.Length == 3 && ReferenceEquals(followResult[0], follow)
                && ReferenceEquals(follow.points, points) && followResult[1].lane == -.6f,
                "Follow paths keep their object identity and recovery leaves from the terminal lane.");
            var flight = new MusicalCue { kind = "BigJump", time = 1, endTime = 1 };
            var landing = Jump(4.2);
            Require(SameObjects(new[] { flight, landing }, Run(new[] { flight, landing }, Evidence(Note(2.6)))),
                "A reserved BigJump-to-landing flight never receives an inserted ordinary support.");
            var final = new MusicalCue { kind = "FinishJump", time = 4.2, endTime = 4.2 };
            Require(SameObjects(new[] { a, final }, Run(new[] { a, final }, Evidence(Note(2.6))))
                && Run(new[] { a }, Evidence(Note(2.6))).Length == 1,
                "Neither a final fading tail nor the end after the last cue is filled.");

            foreach (string key in new[] { "sourceAttack", "neuralOnset", "harmonicPartials", "harmonicNovelty" })
            {
                var rejected = Note(2.6); Set(rejected, key, 0);
                Require(Run(new[] { a, b }, Evidence(rejected)).Length == 2,
                    "Unconfirmed, weak, percussive or static/hallucinated hypotheses must not add support: " + key);
            }
            Require(Run(new[] { a, b }, Evidence(Note(1.1), Note(4.1))).Length == 2,
                "Candidates too near existing supports cannot create an unplayable handoff.");
            Require(Rejects(() => Run(new[] { a, b }, Evidence(Note(double.NaN))))
                && Rejects(() => Run(new[] { a, b }, Evidence(Note(double.PositiveInfinity))))
                && Rejects(() => Run(new[] { a, b }, Evidence(Note(-1))))
                && Rejects(() => Run(new[] { a, b }, notes, double.NaN)),
                "Non-finite or negative measurement times and invalid gap limits are rejected.");
            var malformed = Note(2.6); Set(malformed, "harmonicBody", double.NaN);
            Require(Rejects(() => Run(new[] { a, b }, Evidence(malformed))), "NaN feature values cannot enter recovery.");
            var wrongShape = Note(2.6); wrongShape.features = new double[1];
            Require(Rejects(() => Run(new[] { a, b }, Evidence(wrongShape))), "Feature-vector shape must match the independent evidence schema.");
            Require(Rejects(() => Run(new[] { new MusicalCue { kind = "Hold", time = 1, endTime = 3 }, Jump(2) }, notes)),
                "Recovery rejects overlapping cue input instead of masking an invalid support contract.");
            var flag = typeof(AudioChartGenerator.Options).GetField("includeMeasuredGapRecovery");
            Require(flag != null && !(bool)flag.GetValue(new AudioChartGenerator.Options()),
                "The new measured recovery remains opt-in; default historical generation is unchanged.");
            Debug.Log("PCM_MEASURED_GAP_RECOVERY PASS: exact measured attacks, gap closure, translation, immutable inputs, valid-gap and Hold/Follow/BigJump/finish preservation, weak/static/unconfirmed and invalid evidence, default-off policy.");
        }

        private static MusicalCue Jump(double time) => new MusicalCue { kind = "Jump", time = time, endTime = time };
        private static PcmInstrumentalEvidence.NoteEvent Note(double time)
        {
            var note = new PcmInstrumentalEvidence.NoteEvent { time = time, endTime = time + .2, midi = 65,
                features = new double[PcmInstrumentalEvidence.FeatureCount] };
            Set(note, "sourceAttack", 1); Set(note, "neuralOnset", .7); Set(note, "harmonicPartials", 5);
            Set(note, "harmonicNovelty", .03); Set(note, "harmonicBody", .8);
            return note;
        }
        private static PcmInstrumentalEvidence.Evidence Evidence(params PcmInstrumentalEvidence.NoteEvent[] notes)
            => new PcmInstrumentalEvidence.Evidence { events = notes.OrderBy(n => n.time).ThenBy(n => n.midi).ToArray() };
        private static void Set(PcmInstrumentalEvidence.NoteEvent note, string key, double value)
            => note.features[PcmInstrumentalEvidence.Index(key)] = value;
        private static bool SameObjects(MusicalCue[] expected, MusicalCue[] actual)
            => expected.Length == actual.Length && expected.Zip(actual, (a, b) => ReferenceEquals(a, b)).All(equal => equal);
        private static bool Rejects(Action action)
        {
            try { action(); return false; }
            catch (TargetInvocationException error) when (error.InnerException is ArgumentException || error.InnerException is InvalidDataException) { return true; }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException("Measured gap recovery regression: " + message); }
    }
}
