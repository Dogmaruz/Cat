using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    public static class IslandFinishPresentationChecks
    {
        public const string Folder = "Library/RadioArchipelago/FinishMeadow";
        [Serializable] private sealed class RouteData { public Vector3[] points; }
        [Serializable] private sealed class Candidate
        {
            public float yaw, groundHeight, maximumPenetration = float.NegativeInfinity;
            public int samples, obstructed, terrainObstructions, bodyRayObstructions, flightBodyRayObstructions;
            public Vector3 landing, end;
            public string worstMesh, worstMaterial;
        }
        [Serializable] private sealed class SurveyReport
        {
            public string scope = "Actual imported opaque solid triangles including structures; reward corridor samples at 0.5m stations across full lateral width. Chart, audio and imported geometry unchanged.";
            public List<Candidate> candidates = new List<Candidate>();
        }
        public static void Survey()
        {
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            var route = new IslandRoute(JsonUtility.FromJson<RouteData>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
            MapElement last = null;
            foreach (var element in map.elements) if (element.kind == "FinishJump") last = element;
            if (last == null) throw new InvalidOperationException("Missing genuine finish jump.");
            float landingZ = last.pos.z + last.jumpDistance;
            var index = new IslandTerrainClearanceDiagnostics.HeightIndex(definition.Prefab,
                new IslandTerrainClearanceDiagnostics.Report(), true);
            var report = new SurveyReport();
            var yaws = new List<float> { 0, -30, 30, 45, -60, 60, -90, 90 };
            for (int yaw = -35; yaw >= -55; yaw--) yaws.Add(yaw);
            foreach (float yaw in yaws)
            {
                var path = new IslandGroundFinishPath(route, definition.StartDistance, definition.DistanceScale,
                    definition.TileElevation, last.pos.z, landingZ, landingZ + 29, yaw);
                var candidate = new Candidate { yaw = yaw, groundHeight = path.GroundHeight,
                    landing = path.LandingFrame.Position,
                    end = path.Evaluate(definition.StartDistance + path.EndChartZ * definition.DistanceScale).Position };
                for (float z = landingZ; z <= landingZ + 29; z += .5f)
                {
                    var frame = path.Evaluate(definition.StartDistance + z * definition.DistanceScale);
                    for (float x = -3.4f; x <= 3.401f; x += .4f)
                    {
                        candidate.samples++;
                        var world = frame.Position + frame.Right * x;
                        if (index.TryHeight(world, false, out float terrainHeight, out _, out _) && terrainHeight > path.GroundHeight + .02f)
                            candidate.terrainObstructions++;
                        var floorPoint = world; floorPoint.y = path.GroundHeight + .1f;
                        if (index.TryRayHit(floorPoint, floorPoint + Vector3.up * 3.1f, out _, out _, out _)) candidate.bodyRayObstructions++;
                        if (z > landingZ)
                            for (float rayHeight = .3f; rayHeight <= 3.1f; rayHeight += .4f)
                            {
                                var from = floorPoint - frame.Tangent * (.5f * definition.DistanceScale); from.y = path.GroundHeight + rayHeight;
                                var to = floorPoint; to.y = from.y;
                                if (index.TryRayHit(from, to, out _, out _, out _)) candidate.bodyRayObstructions++;
                            }
                        if (!index.TryHeight(world, true, out float height, out string material, out string mesh)) continue;
                        float penetration = height - path.GroundHeight;
                        if (penetration > .02f) candidate.obstructed++;
                        if (penetration > candidate.maximumPenetration)
                        { candidate.maximumPenetration = penetration; candidate.worstMesh = mesh; candidate.worstMaterial = material; }
                    }
                }
                for (int station = 1; station < 64; station++)
                {
                    float progress = station / 64f, chartZ = Mathf.Lerp(last.pos.z, landingZ, progress);
                    var frame = path.Evaluate(definition.StartDistance + chartZ * definition.DistanceScale);
                    float feet = path.FlightSurfaceY(last.pos, last.jumpDistance, chartZ) +
                        NativeRhythmRules.JumpY(last.pos.y, NativeRhythmRules.HeightForDistance(last.jumpDistance), progress) +
                        IslandFlightElevation.Boost(last.jumpDistance, last.pos.z, chartZ, definition.JumpHeightScale) - .1f;
                    for (float x = -2.7f; x <= 2.701f; x += .54f)
                    {
                        var world = frame.Position + frame.Right * x; world.y = feet;
                        if (index.TryRayHit(world, world + Vector3.up * 3.2f, out _, out _, out _)) candidate.flightBodyRayObstructions++;
                    }
                }
                report.candidates.Add(candidate);
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/finish-route-survey.json", JsonUtility.ToJson(report, true));
        }

        public static void RunChecks()
        {
            Survey();
            var report = JsonUtility.FromJson<SurveyReport>(File.ReadAllText(Folder + "/finish-route-survey.json"));
            var chosen = report.candidates.Find(candidate => candidate.yaw == IslandGroundFinishPath.MeadowExitYaw);
            Require(chosen != null && chosen.terrainObstructions == 0 && chosen.bodyRayObstructions == 0 && chosen.flightBodyRayObstructions == 0,
                "Final reward corridor intersects the imported terrain or a physical structure.");
            var route = new IslandRoute(new[] { Vector3.zero, new Vector3(30, 0, 0), new Vector3(30, 0, 30), new Vector3(0, 0, 30) });
            var path = new IslandGroundFinishPath(route, 0, 1, 1, 8, 14, 43, IslandGroundFinishPath.MeadowExitYaw);
            Require(path.CourierHoverOffset(7) == 0 && Mathf.Abs(path.CourierHoverOffset(14) - .42f) < .00001f,
                "Hover must start only on the last jump and settle at the intended clearance.");
            Require(Vector3.Distance(path.Evaluate(7).Position, route.Evaluate(7).Position) < .00001f,
                "Finale presentation changed the earlier level.");
            foreach (float seam in new[] { 8f, 14f })
            {
                var before = path.Evaluate(seam - .0001f); var after = path.Evaluate(seam + .0001f);
                Require(Vector3.Distance(before.Position, after.Position) < .001f && Vector3.Angle(before.Tangent, after.Tangent) < .1f,
                    "Final jump and meadow frames must join continuously.");
            }
            float prior = -1;
            for (float z = 0; z <= 45; z += .01f)
            {
                float hover = path.CourierHoverOffset(z);
                Require(hover >= prior - .000001f && hover >= 0 && hover <= .42001f, "Finish hover must be bounded and monotonic.");
                prior = hover;
            }
            float previousYaw = 0;
            var entry = route.Evaluate(8).Tangent;
            for (int i = 0; i <= 600; i++)
            {
                float distance = Mathf.Lerp(8, 14, i / 600f);
                var frame = path.Evaluate(distance);
                float yaw = Vector3.SignedAngle(entry, frame.Tangent, Vector3.up);
                Require(yaw <= previousYaw + .001f && yaw >= IslandGroundFinishPath.MeadowExitYaw - .001f,
                    "The exit camera heading must never overshoot its turn or turn back near landing.");
                previousYaw = yaw;
                if (i > 0 && i < 600)
                {
                    var derivative = path.Evaluate(distance + .001f).Position - path.Evaluate(distance - .001f).Position;
                    derivative.y = 0;
                    Require(Vector3.Angle(derivative, frame.Tangent) < .2f, "The final arc heading disagrees with its geometric tangent.");
                }
            }
            Debug.Log("RADIO_FINISH_PRESENTATION PASS: imported terrain/structure corridor clear; continuous final flight/hover frames; earlier route unchanged.");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
