using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class ShortNativeSupportValidation
    {
        public static void RunChecks()
        {
            foreach (double duration in new[] { .25, .344, .609, .922, 3.4 })
            {
                int points = duration < .312 ? 2 : 6;
                var map = Compile("Follow", duration, points);
                AudioChartGeneratorValidation.ValidateMap(map);
                var support = map.segments.Single(s => s.kind == "Follow");
                var entry = map.elements[support.sourceIndex];
                var exit = map.elements[support.elementIndices.Last()];
                Require(entry.id == 4 && entry.updateType == 2 && entry.jumpDistance == 0,
                    "short native Follow enters a ride instead of launching a new jump");
                Require(exit.id == 6 && exit.updateType == 0 && exit.jumpDistance > 0,
                    "short Follow retains an actual exit and outgoing flight");
                Require(support.path.Length == points && support.elementIndices.Length == points,
                    "short Follow retains its measured number of native path samples");
                Require(Math.Abs(entry.mfendPos - duration * map.forwardSpeed) < .0001,
                    "short ride distance follows its measured duration at unchanged speed");
                Require(Math.Abs(support.endAudioTime - support.audioTime - duration) < 1e-9,
                    "compilation must not stretch a short contour to one second");
            }
            foreach (double duration in new[] { .229161, .234376, .281247, .291680, .354173 })
            {
                var map = Compile("Hold", duration, 0);
                AudioChartGeneratorValidation.ValidateMap(map);
                var support = map.segments.Single(s => s.kind == "Hold");
                Require(support.holdTicks.Length == 2 && Math.Abs(support.duration - duration) < 1e-9,
                    "short held notes retain only their measured attack and release");
            }
            bool rejected = false;
            try { Compile("Follow", .2, 2); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "a contour below the conservative authoring duration is rejected");
            rejected = false;
            try { Compile("Follow", .25, 6); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "shortening the allowed ride must not loosen unsafe point spacing");
            var fadingTail = MusicalChartCompiler.Compile(new MusicalScore { id = "FadingTailSupport", sampleRate = 8000,
                audioDuration = 10, bpm = 120, cues = new[] { .4, 1.6, 2.8, 4.0, 5.2, 6.2 }
                    .Select(at => new MusicalCue { kind = "Jump", time = at, endTime = at }).ToArray() },
                new AudioChartGenerator.Options(), 8000, 10);
            AudioChartGeneratorValidation.ValidateMap(fadingTail);
            var outgoing = fadingTail.elements[fadingTail.elements.Length - 2];
            Require(outgoing.jumpDistance > 1.95 * fadingTail.forwardSpeed
                && Math.Abs(NativeRhythmRules.JumpY(0, NativeRhythmRules.HeightForDistance(outgoing.jumpDistance), 1)) < .0001,
                "a measured final fading rest can remain one native arc without a fabricated scored tile");
            Debug.Log("SHORT_NATIVE_SUPPORT PASS: five short/long native Follow durations, adaptive path counts, real exit arcs, five short Holds and unsafe duration/spacing rejection.");
        }

        private static MapTimeline Compile(string kind, double duration, int count)
        {
            const double start = 1.2;
            var cue = new MusicalCue { kind = kind, time = start, endTime = start + duration, lane = 0 };
            if (kind == "Follow") cue.points = Enumerable.Range(0, count)
                .Select(i => new MusicalPoint { time = start + duration * i / (count - 1), lane = .5f * i / (count - 1) }).ToArray();
            var cues = new List<MusicalCue> { new MusicalCue { kind = "Jump", time = .4, endTime = .4 }, cue };
            for (double at = cue.endTime + .4; at < 8.8; at += 1.2)
                cues.Add(new MusicalCue { kind = "Jump", time = at, endTime = at, lane = kind == "Follow" ? .5f : 0 });
            return MusicalChartCompiler.Compile(new MusicalScore { id = "ShortNativeSupport", sampleRate = 8000,
                audioDuration = 10, bpm = 120, cues = cues.ToArray() }, new AudioChartGenerator.Options(), 8000, 10);
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("SHORT_NATIVE_SUPPORT RED/FAIL: " + message); }
    }
}
