using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class MusicalChartCompilerValidation
    {
        [MenuItem("Dancing Cats/Validate Musical Chart Compiler")]
        public static void RunChecks()
        {
            var matching = new MusicalScore { id = "ExactCase", sourceSha256 = "abc" };
            AudioChartGenerator.ValidateScoreSource(matching, "ExactCase", "ABC");
            foreach (var source in new[] { new[] { "exactcase", "abc" }, new[] { "ExactCase", "changed" }, new[] { "ExactCase", "" } })
            {
                bool rejected = false;
                try { AudioChartGenerator.ValidateScoreSource(matching, source[0], source[1]); }
                catch (System.IO.InvalidDataException) { rejected = true; }
                Require(rejected, "score must reject case-changed identity, changed audio and missing source hash");
            }
            var assembly = typeof(MusicalChartCompilerValidation).Assembly;
            var compiler = assembly.GetType("DancingCats.Clone.Editor.MusicalChartCompiler");
            Require(compiler != null, "musical score compiler is missing");
            var scoreType = assembly.GetType("DancingCats.Clone.Editor.MusicalScore");
            Require(scoreType != null, "portable musical score DTO is missing");
            var score = Activator.CreateInstance(scoreType);
            Set(score, "id", "CompilerFixture"); Set(score, "audioDuration", 12.0); Set(score, "bpm", 120.0); Set(score, "sampleRate", 8000);
            Set(score, "beats", new[] { 0.0, .45, .92, 1.43, 1.91, 2.42, 2.9, 3.4, 3.9, 4.4, 4.9 });
            var cueType = scoreType.GetField("cues").FieldType.GetElementType();
            var cues = Array.CreateInstance(cueType, 8);
            cues.SetValue(Cue(cueType, "Jump", 0, 0, 0), 0);
            cues.SetValue(Cue(cueType, "Hold", .92, 2.9, .5f), 1);
            var follow = Cue(cueType, "Follow", 3.4, 5.4, -.5f);
            var pointType = cueType.GetField("points").FieldType.GetElementType();
            var points = Array.CreateInstance(pointType, 3);
            for (int i = 0; i < 3; i++) { var p = Activator.CreateInstance(pointType); Set(p, "time", 3.4 + i); Set(p, "lane", -.5f + i * .5f); points.SetValue(p, i); }
            Set(follow, "points", points); cues.SetValue(follow, 2);
            cues.SetValue(Cue(cueType, "Rest", 5.4, 7.0, .5f), 3);
            cues.SetValue(Cue(cueType, "Jump", 7.0, 7.0, .5f), 4);
            cues.SetValue(Cue(cueType, "Jump", 8.5, 8.5, 0), 5);
            cues.SetValue(Cue(cueType, "Jump", 9.5, 9.5, 0), 6);
            cues.SetValue(Cue(cueType, "Rest", 11.2, 12.0, 0), 7);
            Set(score, "cues", cues);
            var map = (MapTimeline)compiler.GetMethod("Compile").Invoke(null, new object[] { score, new AudioChartGenerator.Options(), 8000, 12.0 });
            var hold = map.segments.Single(s => s.kind == "Hold");
            Require(hold.holdTicks.Select(t => t.audioTime).SequenceEqual(new[] { .92, 1.43, 1.91, 2.42, 2.9 }), "hold checkpoints follow measured beats, including entry and exit");
            Require(Math.Abs(hold.endZ - 22.75) < .0001, "hold exit follows audio plus preroll at unchanged speed");
            var glide = map.segments.Single(s => s.kind == "Follow");
            Require(glide.path.Length == 3 && glide.elementIndices.Length == 3 && glide.motion.jumpDistance == 0, "Follow retains native entry, path marker and exit");
            var entry = map.elements[glide.sourceIndex];
            var mid = map.elements[glide.elementIndices[1]];
            var exit = map.elements[glide.elementIndices.Last()];
            Require(entry.id == 4 && entry.updateType == 2 && entry.jumpDistance == 0 && Math.Abs(entry.mfendPos - 13) < .0001, "Follow entry moves to native terminal Z without launching an arc");
            Require(mid.id == 5 && mid.updateType == 0 && mid.jumpDistance == 0 && Math.Abs(mid.pos.x) < .0001, "Follow midpoint preserves authored lane and is a stationary marker");
            Require(exit.id == 6 && exit.updateType == 0 && Math.Abs(exit.jumpDistance - 10.4) < .0001, "Follow exit launches from endpoint to next authored landing");
            Require(!map.events.Any(e => e.audioTime > 5.40001 && e.audioTime < 6.99999), "rest longer than one second contains no scored filler");
            Require(!map.elements.Any(e => e.updateType == 3), "glides do not invent timer-based oscillating jumps");
            Require(!map.events.Any(e => e.audioTime > 10.90001), "trailing silence may reach recording end beyond the technical finish");
            AudioChartGeneratorValidation.ValidateMap(map);
            Set(points.GetValue(1), "time", 3.400001);
            bool unsafeFollowRejected = false;
            try { compiler.GetMethod("Compile").Invoke(null, new object[] { score, new AudioChartGenerator.Options(), 8000, 12.0 }); }
            catch (TargetInvocationException error) { unsafeFollowRejected = error.InnerException is System.IO.InvalidDataException; }
            Require(unsafeFollowRejected, "near-instantaneous Follow movement must be rejected before saving");
            Console.WriteLine("MUSICAL_CHART_COMPILER PASS: measured beat holds, native Follow, rests, timing and exit arcs");
        }
        private static object Cue(Type type, string kind, double start, double end, float lane)
        {
            var cue = Activator.CreateInstance(type); Set(cue, "kind", kind); Set(cue, "time", start); Set(cue, "endTime", end); Set(cue, "lane", lane); return cue;
        }
        private static void Set(object instance, string field, object value) { instance.GetType().GetField(field).SetValue(instance, value); }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception("MUSICAL_CHART_COMPILER RED/FAIL: " + message); }
    }
}
