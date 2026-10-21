using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class AudioChartGeneratorValidation
    {
        [MenuItem("Dancing Cats/Validate Audio Chart Generator")]
        public static void RunChecks()
        {
            RunSafetyChecks();
            var type = typeof(AudioChartGeneratorValidation).Assembly.GetType("DancingCats.Clone.Editor.AudioChartGenerator");
            Require(type != null, "PCM audio chart generator is missing");
            var method = type.GetMethod("GenerateFromSamples", BindingFlags.Public | BindingFlags.Static);
            Require(method != null, "reusable PCM generation interface is missing");
            const int rate = 8000;
            bool rejected = false;
            try { Generate(method, new float[rate * 12], rate); }
            catch (TargetInvocationException error) { rejected = error.InnerException is InvalidDataException; }
            Require(rejected, "silence must fail clearly instead of producing zero-distance jumps");
            var pulses = PulseTrack(rate, false);
            var map = Generate(method, pulses, rate);
            var again = Generate(method, pulses, rate);
            Require(JsonUtility.ToJson(map) == JsonUtility.ToJson(again), "identical decoded PCM must produce byte-identical JSON");
            double bpm = rate * 60 / map.tempoSections[0].samplesPerBeat;
            Require(Math.Abs(bpm - 80) < 2, "synthetic 80 BPM pulses retain tempo (actual " + bpm + ")");
            var accents = map.elements.Where(e => e.audioTime > .3 && e.audioTime < 21 && e.kind == "Jump").ToArray();
            Require(accents.Count(e => Math.Abs((e.audioTime - .4) / .75 - Math.Round((e.audioTime - .4) / .75)) < .07) >= 16,
                "ordinary landings follow measured off-grid pulse phase");
            ValidateMap(map);
            var sustained = Generate(method, PulseTrack(rate, true), rate);
            Require(sustained.segments.Any(s => s.kind == "Hold" && s.duration >= .6), "sustained PCM creates a native hold section");
            ValidateMap(sustained);
            Debug.Log("AUDIO_CHART_GENERATOR PASS: silence rejected; 80 BPM and measured phase; deterministic PCM output; safe gaps; native holds and exit distances; finish before audio end");
        }

        public static void RunSafetyChecks()
        {
            var type = typeof(AudioChartGeneratorValidation).Assembly.GetType("DancingCats.Clone.Editor.AudioChartGenerator");
            Require(type != null, "PCM audio chart generator is missing");
            var resolver = type.GetMethod("ResolveCatalogId", BindingFlags.Public | BindingFlags.Static);
            Require(resolver != null, "catalog identity guard is missing");
            var original = new[] { new SongRecord { id = "Original", catalogId = 1 } };
            var custom = new[] { new SongRecord { id = "FirstCustom", catalogId = 20001 } };
            Require((int)resolver.Invoke(null, new object[] { "SecondCustom", original, custom, 0 }) == 20002, "selected audio receives the next free custom catalog id");
            Require((int)resolver.Invoke(null, new object[] { "FirstCustom", original, custom, 0 }) == 20001, "regeneration preserves an existing custom identity");
            Require((int)resolver.Invoke(null, new object[] { "FirstCustom", original, custom, 20001 }) == 20001, "exact-case regeneration preserves an explicit existing identity");
            foreach (var casing in new[]
            {
                new object[] { "original", original, custom, 20002 },
                new object[] { "firstcustom", original, custom, 0 },
                new object[] { "FIRSTCUSTOM", original, custom, 20002 }
            })
            {
                bool rejected = false;
                try { resolver.Invoke(null, casing); }
                catch (TargetInvocationException error) { rejected = error.InnerException is InvalidDataException; }
                Require(rejected, "case-insensitive song id collision is rejected before a Windows chart file can be overwritten: " + casing[0]);
            }
            var generate = type.GetMethod("GenerateFromSamples", BindingFlags.Public | BindingFlags.Static);
            var optionsType = type.GetNestedType("Options");
            foreach (var fieldName in new[] { "forwardSpeed", "delayTime" })
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var options = Activator.CreateInstance(optionsType);
                optionsType.GetField(fieldName).SetValue(options, invalid);
                bool rejected = false;
                try { generate.Invoke(null, new object[] { PulseTrack(8000, false), 8000, 1, "FiniteOptions", options }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ArgumentException; }
                Require(rejected, "non-finite " + fieldName + " is rejected before generating geometry");
            }
            foreach (var identity in new[] { new object[] { "Original", original, custom, 20002 }, new object[] { "New", original, custom, 1 }, new object[] { "New", original, custom, 20001 } })
            {
                bool rejected = false;
                try { resolver.Invoke(null, identity); }
                catch (TargetInvocationException error) { rejected = error.InnerException is InvalidDataException; }
                Require(rejected, "catalog collisions are rejected before saving any resource");
            }
        }
        public static void ValidateMap(MapTimeline map)
        {
            Require(map.schemaVersion == 1 && map.elements.Length >= 4, "playable schema and populated chart");
            Require(map.elements[0].kind == "Start" && map.elements[0].gameTime == 0, "Start remains at game time zero");
            Require(Math.Abs(map.elements[0].audioTime + map.delayTime) < .00001, "Start uses negative audio preroll");
            Require(map.events.All(e => e.mStartSample >= 0 && e.mEndSample >= e.mStartSample && e.mEndSample < map.audio.declaredLengthSeconds * map.sampleRate), "events are inside decoded PCM");
            for (int i = 0; i < map.elements.Length; i++)
            {
                var element = map.elements[i];
                Require(!float.IsNaN(element.pos.z) && !float.IsInfinity(element.pos.z), "finite geometry");
                Require(Math.Abs(element.gameTime - element.audioTime - map.delayTime) < .00001, "audio/game timing relation");
                Require(Math.Abs(element.pos.z - element.gameTime * map.forwardSpeed) < .0003, "position follows native forward speed");
                Require(element.jumpDistance > 0, "every arc has a positive distance");
                if (i + 1 >= map.elements.Length) continue;
                var segment = map.segments.Single(s => s.sourceIndex == element.sourceIndex);
                var next = map.elements[i + 1];
                double gap = next.gameTime - segment.endGameTime;
                Require(gap >= .319 && gap <= 1.001, "takeoff-to-landing interval is safe: " + gap);
                Require(Math.Abs(element.jumpDistance - (next.pos.z - segment.endZ)) < .0003, "hold exit, rather than hold start, determines outgoing arc");
                Require(Math.Abs(next.pos.x - element.pos.x) <= 1.001, "lane changes stay reachable");
                if (element.kind == "Hold")
                {
                    Require(element.se.y < 0 && element.holdTicks.Length >= 2, "hold native mesh length and checkpoints exist");
                    Require(element.holdTicks.Select(t => t.z).Distinct().Count() == element.holdTicks.Length, "hold checkpoints do not double-score");
                    Require(Math.Abs(element.holdTicks.Last().z - segment.endZ) < .0003, "hold checkpoint end matches native exit");
                }
            }
            var finish = map.elements.Last();
            Require(finish.kind == "FinishJump" && Math.Abs(finish.jumpDistance - 5) < .00001, "real finish jumps five units to the original end platform");
            Require(finish.audioTime + 5 / map.forwardSpeed < map.audio.declaredLengthSeconds, "audio continues through finish platform landing");
        }

        private static MapTimeline Generate(MethodInfo method, float[] samples, int rate)
        {
            return (MapTimeline)method.Invoke(null, new object[] { samples, rate, 1, "SyntheticPulse", null });
        }
        private static float[] PulseTrack(int rate, bool sustained)
        {
            var samples = new float[rate * 24];
            for (int i = 0; i < samples.Length; i++)
            {
                double time = (double)i / rate;
                double since = time - .4;
                since -= Math.Floor(since / .75) * .75;
                double pulse = since < .10 ? .8 * Math.Exp(-since * 45) : 0;
                double bed = sustained && time > 5 && time < 20 ? .22 : 0;
                samples[i] = (float)((pulse + bed) * Math.Sin(2 * Math.PI * 220 * time));
            }
            return samples;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("AUDIO_CHART_GENERATOR RED/FAIL: " + message);
        }
    }
}
