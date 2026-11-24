using System;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Spatial-only regression checks for the opt-in readable opening.</summary>
    public static class IslandOpeningChecks
    {
        public static void RunChecks()
        {
            string sourceJson = File.ReadAllText(LumiLaiChartAuthoring.BaseChartPath);
            var source = JsonUtility.FromJson<MapTimeline>(sourceJson);
            var opening = IslandChartAuthoring.Create(sourceJson, true);
            VerifyOpening(opening);
            IslandChartAuthoring.Verify(source, opening);
            var legacy = IslandChartAuthoring.Create(sourceJson);
            Require(JsonUtility.ToJson(legacy) == JsonUtility.ToJson(IslandChartAuthoring.Create(sourceJson, false)),
                "The legacy authoring call must keep the opt-in disabled.");
            var firstLegacy = legacy.segments.Where(s => s.kind == "Jump").OrderBy(s => s.audioTime).First();
            Require(Mathf.Abs(ChartRouteComposer.LandingX(legacy.elements[firstLegacy.sourceIndex])) > .5f,
                "The centered opening leaked into legacy Lumi-Lai authoring.");
            Debug.Log("ISLAND_OPENING_CHECKS_PASS: three centered stationary landings; gradual reachable transition; musical evidence unchanged.");
        }

        public static void VerifyOpening(MapTimeline map)
        {
            var scoring = map.segments.Where(s => s.kind != "Theme" && s.kind != "Start" && s.kind != "FinishJump")
                .OrderBy(s => s.audioTime).ToArray();
            Require(scoring.Length >= 6, "The opening check requires six scoring supports.");
            for (int i = 0; i < 5; i++)
            {
                var element = map.elements[scoring[i].sourceIndex];
                float limit = i < 3 ? .001f : i == 3 ? .251f : .501f;
                Require(Mathf.Abs(ChartRouteComposer.LandingX(element)) <= limit,
                    "Opening scoring support " + (i + 1) + " is too far from center.");
                Require(element.updateType == 0 && scoring[i].motion != null && scoring[i].motion.updateType == 0,
                    "Opening scoring support " + (i + 1) + " must stay stationary.");
            }
            Require(scoring.Skip(5).Any(s => Mathf.Abs(ChartRouteComposer.LandingX(map.elements[s.sourceIndex])) > 1),
                "Normal lateral phrasing must resume after the opening.");
            // On the accepted song these central landings must fit full-size
            // round supports even before the later stage spacing pass.
            for (int i = 1; i < 3; i++)
                Require((scoring[i].z - scoring[i - 1].endZ) * IslandChartAuthoring.WorldDistanceScale >= 1.38f,
                    "Centered opening supports overlap at the native .65 radius.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
