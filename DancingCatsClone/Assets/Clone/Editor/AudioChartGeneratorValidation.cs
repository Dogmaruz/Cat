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
            RunMusicalFallbackChecks();
            RunSpectralEvidenceChecks();
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
            var accents = map.elements.Where(e => e.audioTime > .3 && e.audioTime < 21 && (e.kind == "Jump" || e.kind == "BigJump")).ToArray();
            Require(accents.Count(e => Math.Abs((e.audioTime - .4) / .75 - Math.Round((e.audioTime - .4) / .75)) < .07) >= 16,
                "ordinary landings follow measured off-grid pulse phase");
            ValidateMap(map);
            var sustained = Generate(method, PulseTrack(rate, true), rate);
            Require(!sustained.segments.Any(s => s.kind == "Hold"), "a mixed-audio RMS bed alone must not invent a melodic hold");
            ValidateMap(sustained);
            Debug.Log("AUDIO_CHART_GENERATOR PASS: silence rejected; 80 BPM and measured phase; deterministic PCM output; preserved rests; no RMS-only holds; exit distances; finish before audio end");
        }

        private static void RunSpectralEvidenceChecks()
        {
            const int rate = 8000;
            var mono = PulseTrack(rate, false);
            Array.Clear(mono, rate * 6, (int)(rate * .8)); // Remove one pulse: a measured two-beat airborne gap.
            var stereo = new float[mono.Length * 2];
            for (int index = 0; index < mono.Length; index++) { stereo[index * 2] = mono[index]; stereo[index * 2 + 1] = -mono[index]; }
            var source = AudioChartGenerator.GenerateFromSamples(mono, rate, 1, "StereoEvidence");
            var antiphase = AudioChartGenerator.GenerateFromSamples(stereo, rate, 2, "StereoEvidence");
            Require(source.events.Select(item => item.mStartSample).SequenceEqual(antiphase.events.Select(item => item.mStartSample)),
                "antiphase stereo preserves the same measured musical attacks as mono");
            var annotated = MusicalChartCompiler.Compile(new MusicalScore
            {
                id = "LargeJumpEvidence", sampleRate = rate, audioDuration = 5, bpm = 80,
                cues = new[] { new MusicalCue { kind = "BigJump", time = 1, endTime = 1 },
                    new MusicalCue { kind = "Jump", time = 2.25, endTime = 2.25 } }
            }, new AudioChartGenerator.Options(), rate, 5);
            Require(annotated.elements.Any(element => element.kind == "BigJump" && element.id == 1),
                "an explicit large-jump annotation compiles to its native tile without guessing from gap length");
            ValidateMap(annotated);
            var harmonic = new float[rate * 5];
            for (int index = rate; index < rate * 3; index++)
            {
                double time = (double)index / rate;
                double edge = Math.Min(1, Math.Min((time - 1) / .025, (3 - time) / .025));
                double value = 0;
                for (int partial = 1; partial <= 5; partial++) value += Math.Sin(2 * Math.PI * 440 * partial * time) / partial;
                harmonic[index] = (float)(value * .3 * edge);
            }
            var measured = PcmSpectralAnalysis.Analyze(harmonic, rate, 1);
            Require(measured.sustained.Any(note => note.end - note.start >= 1.5 && note.confidence >= .65),
                "a genuine stable multi-partial note provides positive sustained-pitch evidence");
            var equalEnergy = new float[rate * 24];
            for (int index = 0; index < equalEnergy.Length; index++)
            {
                double time = (double)index / rate;
                double frequency = (int)(time / .75) % 2 == 0 ? 200 : 400;
                double value = 0;
                for (int partial = 1; partial <= 5; partial++) value += Math.Sin(2 * Math.PI * frequency * partial * time) / partial;
                equalEnergy[index] = (float)(value * .3);
            }
            var changingPitch = AudioChartGenerator.GenerateFromSamples(equalEnergy, rate, 1, "EqualEnergyPitch");
            var repeatedPitch = AudioChartGenerator.GenerateFromSamples(equalEnergy, rate, 1, "EqualEnergyPitch");
            Require(JsonUtility.ToJson(changingPitch) == JsonUtility.ToJson(repeatedPitch),
                "equal-energy spectral attacks generate a deterministic chart");
            double changingBpm = rate * 60 / changingPitch.tempoSections[0].samplesPerBeat;
            Require(Math.Abs(changingBpm - 80) < 2, "equal-energy pitch changes retain their measured 80 BPM pulse");
            // A measured articulation can be a Hold release as well as a new support.
            // All thirty changes before the finish cutoff must still score exactly once.
            var changingAttacks = changingPitch.events.Where(item => item.command != 100).ToArray();
            Require(changingAttacks.Length == 30 && Enumerable.Range(1, 30).All(index =>
                changingAttacks.Any(item => Math.Abs(item.audioTime - index * .75) < .08)),
                "equal-energy pitch changes preserve every articulation exactly once within 80ms");
            double changingPhase = (double)changingPitch.tempoSections[0].startSample / rate;
            Require(Math.Abs(changingPhase - .75 * Math.Round(changingPhase / .75)) < .08,
                "equal-energy spectral attacks retain their measured phase");
            ValidateMap(changingPitch);
        }

        public static void RunMusicalFallbackChecks()
        {
            var select = typeof(AudioChartGenerator).GetMethod("SelectAccentTimes", BindingFlags.Public | BindingFlags.Static);
            Require(select != null, "strength-ranked accent selection is missing");
            var selected = (double[])select.Invoke(null, new object[] { new[] { .4, .5, 1.2, 2.0 }, new[] { .1, 1.0, .8, .7 }, .18 });
            Require(selected.SequenceEqual(new[] { .5, 1.2, 2.0 }), "strong late accent replaces weak early accent inside the minimum gap");
            var samples = PulseTrack(8000, false);
            Array.Clear(samples, 6 * 8000, 3 * 8000);
            var map = AudioChartGenerator.GenerateFromSamples(samples, 8000, 1, "SparsePulse");
            Require(!map.events.Any(e => e.audioTime > 6.15 && e.audioTime < 8.95), "PCM silence receives no arbitrary .8-second scored filler");
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
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var pcm = new float[8000 * 6]; pcm[32] = invalid;
                bool rejected = false;
                try { AudioChartGenerator.GenerateFromSamples(pcm, 8000, 1, "FinitePcm"); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "non-finite PCM is rejected before spectral analysis");
            }
        }
        public static void ValidateMap(MapTimeline map)
        {
            Require(map.schemaVersion == 1 && map.elements.Length >= 4, "playable schema and populated chart");
            Require(map.elements[0].kind == "Start" && map.elements[0].gameTime == 0, "Start remains at game time zero");
            Require(Math.Abs(map.elements[0].audioTime + map.delayTime) < .00001, "Start uses negative audio preroll");
            Require(map.events.All(e => e.mStartSample >= 0 && e.mEndSample >= e.mStartSample && e.mEndSample < map.audio.declaredLengthSeconds * map.sampleRate), "events are inside decoded PCM");
            foreach (var element in map.elements)
            {
                Require(!float.IsNaN(element.pos.z) && !float.IsInfinity(element.pos.z) && !float.IsNaN(element.pos.x) && !float.IsInfinity(element.pos.x), "finite geometry");
                Require(Math.Abs(element.gameTime - element.audioTime - map.delayTime) < .00001, "audio/game timing relation");
                Require(Math.Abs(element.pos.z - element.gameTime * map.forwardSpeed) < .0003, "position follows native forward speed");
                Require(element.id == 4 || element.id == 5 ? element.jumpDistance == 0 : element.jumpDistance > 0, "Follow entry and markers do not launch arcs; actual exits have positive arcs");
            }
            foreach (var segment in map.segments)
            {
                var element = map.elements[segment.sourceIndex];
                var exit = map.elements[segment.elementIndices.Last()];
                if (segment.kind == "Follow")
                {
                    Require(element.id == 4 && element.updateType == 2 && exit.id == 6 && exit.updateType == 0, "native Follow entry and exit contract");
                    Require(segment.path.Length == segment.elementIndices.Length && segment.path.Length >= 2, "Follow stores its native path");
                    Require(Math.Abs(element.mfendPos - (segment.endZ - element.pos.z)) < .0003, "Follow native travel ends at terminal Z");
                    Require(segment.elementIndices.Skip(1).Take(segment.elementIndices.Length - 2).All(i => map.elements[i].id == 5 && map.elements[i].updateType == 0), "Follow mids remain stationary native markers");
                }
                if (segment.kind == "Hold")
                {
                    Require(element.se.y < 0 && element.holdTicks.Length >= 2, "hold native mesh length and checkpoints exist");
                    Require(element.holdTicks.Select(t => t.z).Distinct().Count() == element.holdTicks.Length, "hold checkpoints do not double-score");
                    Require(Math.Abs(element.holdTicks.First().z - element.pos.z) < .0003 && Math.Abs(element.holdTicks.Last().z - segment.endZ) < .0003, "hold checkpoints include native entry and exit");
                }
                if (exit.sourceIndex + 1 >= map.elements.Length) continue;
                var next = map.elements[exit.sourceIndex + 1];
                double gap = next.gameTime - segment.endGameTime;
                Require(gap >= .077 && gap <= 1.964, "takeoff-to-landing interval exceeds measured native arc range; annotate sustained support rather than scoring filler: " + gap);
                Require(Math.Abs(exit.jumpDistance - (next.pos.z - segment.endZ)) < .0003, "sustained support exit determines outgoing arc");
                Require(Math.Abs(next.pos.x - exit.pos.x) <= 1.001, "lane changes stay reachable");
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
