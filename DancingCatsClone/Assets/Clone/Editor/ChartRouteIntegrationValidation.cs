using System;
using System.Linq;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class ChartRouteIntegrationValidation
    {
        public static void CheckPublishedGeneration()
        {
            var records = RecoveredCatalog.FromJsonArray<SongRecord>(System.IO.File.ReadAllText("Assets/Resources/CustomData/songs.json"));
            foreach (var record in records)
            {
                var published = RecoveredCatalog.LoadMap(record.id);
                var regenerated = AudioChartGenerator.Generate(record.LoadAudio(), record.id,
                    new AudioChartGenerator.Options { catalogId = record.catalogId, title = record.title, artist = record.artist,
                        forwardSpeed = record.forwardSpeed, delayTime = record.delayTime, preferAuthoredScore = false });
                System.IO.Directory.CreateDirectory("Library/ChartChoreographyV1/Regeneration");
                System.IO.File.WriteAllText("Library/ChartChoreographyV1/Regeneration/" + System.IO.Path.GetFileName(record.id) + ".json",
                    JsonUtility.ToJson(regenerated, true));
                var comparable = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(regenerated));
                NormalizeSerializationNoise(comparable, published);
                Require(ChartRouteJobs.RhythmFingerprint(published) == ChartRouteJobs.RhythmFingerprint(comparable),
                    "regenerating real audio must retain its complete published acoustic map: " + record.id);
                string Layout(MapTimeline map) => string.Join(";", map.elements.Select(e => e.sourceIndex + ":" + e.updateType + ":" + e.msLeft + ":"
                    + Math.Round(ChartRouteComposer.LandingX(e), 5).ToString("F5", System.Globalization.CultureInfo.InvariantCulture)));
                Require(Layout(published) == Layout(regenerated),
                    "real-audio regeneration must reproduce the spatial revision of each existing new level: " + record.id);
            }
            Debug.Log("CHART_ROUTE_REAL_AUDIO PASS: all " + records.Length + " published new levels reproduce their acoustic data and route on real-audio regeneration.");
        }

        // Unity's native JSON double parser can shift a saved value by one ULP.
        // Permit only sub-sample serialization noise in double fields; integer event
        // samples, array lengths, float geometry, types and provenance remain exact.
        private static void NormalizeSerializationNoise(object actual, object expected)
        {
            if (actual == null || expected == null || actual.GetType() != expected.GetType()) return;
            foreach (var field in actual.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                object a = field.GetValue(actual), b = field.GetValue(expected);
                if (field.FieldType == typeof(double))
                {
                    if (Math.Abs((double)a - (double)b) <= 1e-8) field.SetValue(actual, b);
                }
                else if (a is Array aa && b is Array bb && aa.Length == bb.Length)
                    for (int i = 0; i < aa.Length; i++) NormalizeSerializationNoise(aa.GetValue(i), bb.GetValue(i));
                else if (field.FieldType.IsClass && field.FieldType != typeof(string)) NormalizeSerializationNoise(a, b);
            }
        }

        public static void RunChecks()
        {
            const int rate = 8000;
            var pcm = new float[rate * 32];
            for (int i = 0; i < pcm.Length; i++)
            {
                double t = (double)i / rate, phase = t - .4;
                phase -= Math.Floor(phase / .48) * .48;
                if (phase < .08) pcm[i] = (float)(.8 * Math.Exp(-phase * 45) * Math.Sin(2 * Math.PI * 440 * t));
            }
            var straight = AudioChartGenerator.GenerateFromSamples(pcm, rate, 1, "RouteIntegration",
                new AudioChartGenerator.Options { includeHolds = false, includeRouteChoreography = false });
            var routed = AudioChartGenerator.GenerateFromSamples(pcm, rate, 1, "RouteIntegration",
                new AudioChartGenerator.Options { includeHolds = false });
            Require(ChartRouteJobs.RhythmFingerprint(straight) == ChartRouteJobs.RhythmFingerprint(routed),
                "spatial generation must preserve the entire acoustic map and provenance");
            var tiles = routed.elements.Where(e => e.kind == "Jump").ToArray();
            Require(tiles.Any(e => ChartRouteComposer.LandingX(e) <= -.25f)
                && tiles.Any(e => ChartRouteComposer.LandingX(e) >= .25f),
                "future automatic charts must contain genuine left and right landing targets");
            Require(tiles.Any(e => e.updateType == 3 && e.msLeft == 0)
                && tiles.Any(e => e.updateType == 3 && e.msLeft != 0),
                "a sufficiently long regular phrase must exercise actual side-to-side targets in both directions");
            Require(straight.elements.All(e => e.updateType != 3), "route opt-out retains the acoustic-only chart");
            var stationary = AudioChartGenerator.GenerateFromSamples(pcm, rate, 1, "RouteIntegration",
                new AudioChartGenerator.Options { includeHolds = false, includeMovingJumps = false });
            Require(stationary.elements.All(e => e.updateType != 3),
                "the existing moving-jump opt-out must also disable newly introduced sideways oscillators");
            AudioChartGeneratorValidation.ValidateMap(routed);
            string first = JsonUtility.ToJson(routed);
            ChartRouteComposer.Apply(routed);
            Require(first == JsonUtility.ToJson(routed), "reapplying published choreography must be idempotent");
            var changed = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(routed));
            changed.events[0].mStartSample++;
            NormalizeSerializationNoise(changed, routed);
            Require(ChartRouteJobs.RhythmFingerprint(changed) != ChartRouteJobs.RhythmFingerprint(routed),
                "the publication invariant must detect event corruption");
            var shifted = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(routed));
            shifted.events[0].audioTime += .0001;
            NormalizeSerializationNoise(shifted, routed);
            Require(ChartRouteJobs.RhythmFingerprint(shifted) != ChartRouteJobs.RhythmFingerprint(routed),
                "JSON noise tolerance must reject a real submillisecond timing change");
            Debug.Log("CHART_ROUTE_INTEGRATION PASS: real PCM automatic routes, both moving directions, acoustic-only opt-out, unchanged rhythm, idempotence and event corruption guard.");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("CHART_ROUTE_INTEGRATION RED/FAIL: " + message); }
    }
}
