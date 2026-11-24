using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Independent solid-triangle clearance for the new island; never writes first-island assets or receipts.</summary>
    public static class MoonResonanceTerrainChecks
    {
        private const float NativeSpanHalfWidth = .70f;
        private const float StructureStep = .8f, StructureBottom = .1f, StructureTop = 8.2f;
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class StructureHit
        {
            public string rayKind, material, mesh;
            public float routeDistance, lateral, fraction;
            public Vector3 origin, target, intersection;
        }
        [Serializable] private sealed class StructureReport
        {
            public string structureScope = "Actual imported opaque solid/architecture/wood/metal/tree-bark surfaces; no grass, foliage or water. Closed-route stations every .8m, five lanes across the full corridor, vertical body rays plus transverse and longitudinal rays at five heights from surface+.1m through surface+8.2m. Finite ray samples supplement the source artist's corridor exclusion and the native camera/play review; overhead projections outside this volume are not obstructions.";
            public int structureSamples, structureViolations;
            public bool structureRegressionPassed;
            public bool floorRegressionPassed, anchorChecksPassed;
            public string fixtureCheckedUtc, sourceModelPath, sourceModelGuid, sourceModelDependencyHash;
            public string sourceRouteGuid, prefabGuid, definitionGuid;
            public int actualFinishFloorSamples;
            public float structureStationStep = StructureStep, structureBottomClearance = StructureBottom, structureTopClearance = StructureTop;
            public List<StructureHit> structureEvidence = new List<StructureHit>();
        }

        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Moon terrain evidence requires stable Edit Mode.");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(MoonResonanceIslandJobs.DefinitionPath);
            if (definition == null || definition.Prefab == null || definition.Chart == null || definition.RouteJson == null || definition.ElevationJson == null)
                throw new InvalidOperationException("The separate Moon Resonance definition, scenery, chart and elevation must exist first.");
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
            RunRegressionChecks();
            var report = new IslandTerrainClearanceDiagnostics.Report
            {
                checkedUtc = DateTime.UtcNow.ToString("O"),
                prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(definition.Prefab)).ToString(),
                definitionDependencyHash = AssetDatabase.GetAssetDependencyHash(MoonResonanceIslandJobs.DefinitionPath).ToString(),
                scope = "Actual imported Moon Resonance Terrain/Cliffs/Rocks triangles, both windings. Entire closed route at 0.25m stations across full steering plus tile rim, every native moving lane envelope, real long-support mesh vertices and interior floor stations. No first-island assets or reference charts are changed. A passing native collision run alone does not establish terrain clearance."
            };
            var index = new IslandTerrainClearanceDiagnostics.HeightIndex(definition.Prefab, report);
            float width = NativeRhythmRules.HorizontalLimit * definition.LateralScale + .75f;
            for (float distance = 0; distance < route.Length; distance += .25f)
            {
                var frame = route.Evaluate(distance);
                for (int lane = 0; lane <= 16; lane++)
                {
                    float lateral = Mathf.Lerp(-width, width, lane / 16f);
                    var bottom = frame.Position + frame.Right * lateral + Vector3.up * (definition.TileElevation - definition.TileThickness);
                    Observe(index, report, bottom, -1, "corridor", 0, distance, lateral, 0, true);
                }
            }
            foreach (var element in map.elements)
            {
                if (element == null || element.id == 7 || element.id == 8 || element.id == 10) continue;
                float end = element.pos.z;
                foreach (var segment in map.segments)
                    if (segment.sourceIndex == element.sourceIndex && (segment.kind == "Hold" || segment.kind == "Follow"))
                    { end = segment.endZ; break; }
                if ((element.id == 2 || element.id == 3 || element.id == 4) && end > element.pos.z + .02f)
                    ObserveSpan(index, report, route, definition, element, end);
                else ObserveRound(index, report, route, definition, element);
            }
            report.worstTiles.Sort((a, b) => a.clearance.CompareTo(b.clearance));
            report.worstCorridor.Sort((a, b) => a.clearance.CompareTo(b.clearance));
            var structures = CheckStructureVolume(definition, route, width);
            structures.floorRegressionPassed = true;
            structures.fixtureCheckedUtc = DateTime.UtcNow.ToString("O");
            CheckAssetAnchors(definition, structures);
            ObserveActualFinish(index, report, structures, definition, route, map);
            report.worstTiles.Sort((a, b) => a.clearance.CompareTo(b.clearance));
            report.passed = report.solidMeshes > 0 && report.triangles > 1000 && report.corridorSamples > 1000 && report.tileSamples > 100 &&
                report.corridorSurfaceHits > 100 && report.tileSurfaceHits > 100 && report.corridorViolations == 0 && report.tileViolations == 0 &&
                structures.structureRegressionPassed && structures.floorRegressionPassed && structures.anchorChecksPassed &&
                structures.actualFinishFloorSamples > 100 && structures.structureSamples > 1000 && structures.structureViolations == 0;
            Directory.CreateDirectory(MoonResonanceIslandJobs.ReviewFolder);
            File.WriteAllText(MoonResonanceIslandJobs.TerrainReportPath, CombineEvidence(report, structures));
            if (!report.passed) throw new InvalidOperationException("Moon Resonance imported triangle clearance failed: corridor=" +
                report.corridorViolations + ", tiles=" + report.tileViolations + ", structures=" + structures.structureViolations +
                "; see " + MoonResonanceIslandJobs.TerrainReportPath);
            Debug.Log("MOON_RESONANCE_TERRAIN PASS: corridor=" + report.corridorSamples + ", support samples=" + report.tileSamples +
                ", min clearance=" + report.minTileClearance + ", solid triangles=" + report.triangles + ", structure rays=" + structures.structureSamples);
        }

        private static StructureReport CheckStructureVolume(IslandStageDefinition definition, IslandRoute route, float width)
        {
            var result = new StructureReport { structureRegressionPassed = true };
            var index = new IslandTerrainClearanceDiagnostics.HeightIndex(definition.Prefab, new IslandTerrainClearanceDiagnostics.Report(), true);
            int stations = Mathf.Max(1, Mathf.CeilToInt(route.Length / StructureStep));
            for (int station = 0; station < stations; station++)
            {
                float distance = route.Length * station / stations;
                var frame = route.Evaluate(distance);
                var previous = route.Evaluate(route.Length * (station - 1f) / stations);
                Vector3 surface = frame.Position + Vector3.up * definition.TileElevation;
                Vector3 previousSurface = previous.Position + Vector3.up * definition.TileElevation;
                for (int lane = 0; lane < 5; lane++)
                {
                    float lateral = Mathf.Lerp(-width, width, lane / 4f);
                    Vector3 here = surface + frame.Right * lateral;
                    StructureRay(index, result, "vertical", distance, lateral,
                        here + Vector3.up * StructureBottom, here + Vector3.up * StructureTop);
                    for (int height = 0; height < 5; height++)
                    {
                        float y = Mathf.Lerp(StructureBottom, StructureTop, height / 4f);
                        Vector3 from = previousSurface + previous.Right * lateral + Vector3.up * y;
                        Vector3 to = here + Vector3.up * y;
                        StructureRay(index, result, "longitudinal", distance, lateral, from, to);
                    }
                }
                for (int height = 0; height < 5; height++)
                {
                    Vector3 center = surface + Vector3.up * Mathf.Lerp(StructureBottom, StructureTop, height / 4f);
                    StructureRay(index, result, "transverse", distance, 0, center - frame.Right * width, center + frame.Right * width);
                }
            }
            return result;
        }

        private static void CheckAssetAnchors(IslandStageDefinition definition, StructureReport report)
        {
            report.sourceModelPath = MoonResonanceIslandJobs.ImportPath + "/moon_resonance_island.fbx";
            report.sourceModelGuid = AssetDatabase.AssetPathToGUID(report.sourceModelPath);
            report.sourceRouteGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition.RouteJson));
            report.prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition.Prefab));
            report.definitionGuid = AssetDatabase.AssetPathToGUID(MoonResonanceIslandJobs.DefinitionPath);
            report.sourceModelDependencyHash = AssetDatabase.GetAssetDependencyHash(report.sourceModelPath).ToString();
            Require(!string.IsNullOrEmpty(report.sourceModelGuid) && !string.IsNullOrEmpty(report.sourceRouteGuid) &&
                !string.IsNullOrEmpty(report.prefabGuid) && !string.IsNullOrEmpty(report.definitionGuid),
                "Moon geometry, route, prefab and definition require persistent imported meta GUID anchors.");
            Require(AssetDatabase.GUIDToAssetPath(report.sourceModelGuid) == report.sourceModelPath &&
                AssetDatabase.GetAssetPath(definition.RouteJson) == MoonResonanceIslandJobs.ImportPath + "/moon_resonance_route.json",
                "Triangle proof must resolve to the separate Moon source export and route.");
            int importedMeshes = 0;
            foreach (var filter in definition.Prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Require(AssetDatabase.GetAssetPath(filter.sharedMesh) == report.sourceModelPath,
                    "Moon scenery triangle proof must use its own imported FBX meshes: " + filter.name);
                importedMeshes++;
            }
            Require(importedMeshes > 0, "The Moon prefab has no anchored source model triangles.");
            report.anchorChecksPassed = true;
        }

        private static void ObserveActualFinish(IslandTerrainClearanceDiagnostics.HeightIndex index,
            IslandTerrainClearanceDiagnostics.Report report, StructureReport structures,
            IslandStageDefinition definition, IslandRoute route, MapTimeline map)
        {
            MapElement finish = null;
            foreach (var element in map.elements) if (element.kind == "FinishJump") finish = element;
            Require(finish != null && definition.EnableGroundFinish, "The Moon native ground finale is required.");
            var path = new IslandGroundFinishPath(route, definition.StartDistance, definition.DistanceScale, definition.TileElevation,
                finish.pos.z, finish.pos.z + finish.jumpDistance, finish.pos.z + finish.jumpDistance + 29, definition.FinishExitYaw);
            float width = NativeRhythmRules.HorizontalLimit * definition.LateralScale + .75f;
            float begin = definition.StartDistance + path.TakeoffChartZ * definition.DistanceScale;
            float end = definition.StartDistance + path.EndChartZ * definition.DistanceScale;
            for (float distance = begin; distance <= end; distance += .25f)
            {
                var frame = path.Evaluate(distance);
                for (int lane = 0; lane <= 16; lane++)
                {
                    float lateral = Mathf.Lerp(-width, width, lane / 16f);
                    Vector3 bottom = frame.Position + frame.Right * lateral +
                        Vector3.up * (definition.TileElevation - definition.TileThickness);
                    Observe(index, report, bottom, finish.sourceIndex, "actual-finish-reserve", 0, distance, lateral, finish.gameTime, true);
                    structures.actualFinishFloorSamples++;
                }
            }
        }

        private static void StructureRay(IslandTerrainClearanceDiagnostics.HeightIndex index, StructureReport report,
            string kind, float distance, float lateral, Vector3 from, Vector3 to)
        {
            report.structureSamples++;
            if (!index.TryRayHit(from, to, out float fraction, out string material, out string mesh)) return;
            report.structureViolations++;
            if (report.structureEvidence.Count < 100) report.structureEvidence.Add(new StructureHit
            {
                rayKind = kind, routeDistance = distance, lateral = lateral, fraction = fraction,
                origin = from, target = to, intersection = Vector3.Lerp(from, to, fraction), material = material, mesh = mesh
            });
        }

        private static string CombineEvidence(IslandTerrainClearanceDiagnostics.Report floor, StructureReport structures)
        {
            // Both objects have disjoint, fixed authoring fields. Keep the existing
            // floor schema at the top level for the independent PlayReview reader.
            string floorJson = JsonUtility.ToJson(floor, true), structureJson = JsonUtility.ToJson(structures, true);
            return floorJson.Substring(0, floorJson.LastIndexOf('}')).TrimEnd() + ",\n" +
                structureJson.Substring(structureJson.IndexOf('{') + 1).TrimStart();
        }

        private static void CheckStructureRegression()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null; Material material = null;
            try
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(root, preview);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "RA_Moon_Architecture" };
                root.GetComponent<MeshRenderer>().sharedMaterial = material;
                root.transform.position = new Vector3(0, 3, 0); root.transform.localScale = new Vector3(2, 4, 2);
                var index = new IslandTerrainClearanceDiagnostics.HeightIndex(root, new IslandTerrainClearanceDiagnostics.Report(), true);
                Require(index.TryRayHit(new Vector3(0, 1.1f, 0), new Vector3(0, 9.2f, 0), out _, out _, out _),
                    "A physical roof inside the airborne volume must be detected.");
                Require(index.TryRayHit(new Vector3(-3, 3, 0), new Vector3(3, 3, 0), out _, out _, out _),
                    "A vertical building wall must be detected without relying on height projection.");
                root.transform.position = new Vector3(0, 15, 0); root.transform.localScale = new Vector3(2, 2, 2);
                index = new IslandTerrainClearanceDiagnostics.HeightIndex(root, new IslandTerrainClearanceDiagnostics.Report(), true);
                Require(!index.TryRayHit(new Vector3(0, 1.1f, 0), new Vector3(0, 9.2f, 0), out _, out _, out _),
                    "A high overhead projection outside the traversable volume is not a body obstruction.");
                root.transform.position = new Vector3(0, -3, 0);
                index = new IslandTerrainClearanceDiagnostics.HeightIndex(root, new IslandTerrainClearanceDiagnostics.Report(), true);
                Require(!index.TryRayHit(new Vector3(0, 1.1f, 0), new Vector3(0, 9.2f, 0), out _, out _, out _),
                    "A solid mesh under the playable surface is not an airborne obstruction.");
                root.transform.position = new Vector3(0, 3, 0);
                foreach (string excluded in new[] { "Grass", "Foliage", "Water" })
                {
                    material.name = "RA_Moon_" + excluded;
                    index = new IslandTerrainClearanceDiagnostics.HeightIndex(root, new IslandTerrainClearanceDiagnostics.Report(), true);
                    Require(!index.TryRayHit(new Vector3(0, 1.1f, 0), new Vector3(0, 9.2f, 0), out _, out _, out _),
                        "Nonstructural " + excluded + " must not be classified as a solid wall.");
                }
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        /// <summary>Execute synthetic triangle and airborne-volume fixtures afresh; never accept a saved proof.</summary>
        public static void RunRegressionChecks()
        {
            CheckFloorRegression();
            CheckStructureRegression();
            Debug.Log("MOON_RESONANCE_TERRAIN_FIXTURES PASS: transformed/mirrored floor triangles, penetration rejection, walls, roofs and excluded non-solids.");
        }

        private static void CheckFloorRegression()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null; Material material = null; Mesh mesh = null;
            try
            {
                root = new GameObject("Moon independent floor fixture");
                SceneManager.MoveGameObjectToScene(root, preview);
                mesh = new Mesh { name = "Known physical floor triangles" };
                mesh.vertices = new[] { new Vector3(-2, 0, -2), new Vector3(2, 0, -2),
                    new Vector3(-2, 0, 2), new Vector3(2, 0, 2) };
                mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
                mesh.RecalculateBounds();
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "RA_Moon_Terrain" };
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                root.transform.position = new Vector3(7, 6, -3);
                root.transform.rotation = Quaternion.Euler(0, 37, 0);
                foreach (float handedness in new[] { 1f, -1f })
                {
                    root.transform.localScale = new Vector3(2 * handedness, 1, 1.5f);
                    var evidence = new IslandTerrainClearanceDiagnostics.Report();
                    var index = new IslandTerrainClearanceDiagnostics.HeightIndex(root, evidence);
                    Vector3 point = new Vector3(7, 6.3f, -3);
                    Require(index.TryHeight(point, true, out float height, out _, out _) && Mathf.Abs(height - 6) < .00001f,
                        "World-space floor height must be invariant under mirrored FBX winding and transformed vertices.");
                    Require(handedness > 0 ? evidence.downwardFaces == 2 : evidence.upwardFaces == 2,
                        "The independent fixture must actually exercise both triangle winding signs.");
                    Observe(index, evidence, point, -1, "fixture-clear", 0, 0, 0, 0, false);
                    Require(evidence.tileSamples == 1 && evidence.tileViolations == 0,
                        "A real .3m floor clearance must pass.");
                    point.y = 6.1f;
                    Observe(index, evidence, point, -1, "fixture-penetrating", 0, 0, 0, 0, false);
                    Require(evidence.tileViolations == 1 && evidence.worstTiles.Count == 1,
                        "A real sub-.15m clearance must fail and preserve triangle hit evidence.");
                }
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static void ObserveSpan(IslandTerrainClearanceDiagnostics.HeightIndex index, IslandTerrainClearanceDiagnostics.Report report,
            IslandRoute route, IslandStageDefinition definition, MapElement element, float end)
        {
            var body = new IslandHoldSurface(route, definition, element.pos.z, end, element.pos.x, element.pos.y, NativeSpanHalfWidth);
            try
            {
                var vertices = body.Mesh.vertices; var uv = body.Mesh.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float z = Mathf.LerpUnclamped(element.pos.z, end, uv[i].y);
                    Observe(index, report, body.Pivot + vertices[i], element.sourceIndex, element.kind, z,
                        definition.StartDistance + z * definition.DistanceScale, element.pos.x * definition.LateralScale, element.gameTime, false);
                }
                float startY = route.Evaluate(definition.StartDistance + element.pos.z * definition.DistanceScale).Position.y;
                float endY = route.Evaluate(definition.StartDistance + end * definition.DistanceScale).Position.y;
                int steps = Mathf.Max(1, Mathf.CeilToInt((end - element.pos.z) * definition.DistanceScale / .15f));
                for (int station = 0; station <= steps; station++)
                {
                    float t = station / (float)steps, z = Mathf.Lerp(element.pos.z, end, t);
                    var frame = route.Evaluate(definition.StartDistance + z * definition.DistanceScale);
                    for (int side = -4; side <= 4; side++)
                    {
                        float lateral = element.pos.x * definition.LateralScale + side * NativeSpanHalfWidth / 4;
                        var bottom = frame.Position + frame.Right * lateral;
                        bottom.y = Mathf.LerpUnclamped(startY, endY, t) + element.pos.y + definition.TileElevation - definition.TileThickness;
                        Observe(index, report, bottom, element.sourceIndex, element.kind, z,
                            definition.StartDistance + z * definition.DistanceScale, lateral, element.gameTime, false);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(body.Mesh); }
        }

        private static void ObserveRound(IslandTerrainClearanceDiagnostics.HeightIndex index, IslandTerrainClearanceDiagnostics.Report report,
            IslandRoute route, IslandStageDefinition definition, MapElement element)
        {
            int phases = element.updateType == 3 || element.updateType == 1 ? 33 : 1;
            for (int phase = 0; phase < phases; phase++)
            {
                float t = phases == 1 ? 1 : phase / (float)(phases - 1);
                float x = element.updateType == 3 ? Mathf.Sin(2 * Mathf.PI * t) :
                    element.updateType == 1 ? Mathf.Lerp(element.mstartPos.x, element.pos.x, t) : element.pos.x;
                var frame = route.Evaluate(definition.StartDistance + element.pos.z * definition.DistanceScale);
                var center = frame.Position + frame.Right * (x * definition.LateralScale) +
                    Vector3.up * (element.pos.y + definition.TileElevation - definition.TileThickness);
                for (int rim = 0; rim < 17; rim++)
                {
                    float angle = (rim - 1) * Mathf.PI / 8;
                    var bottom = rim == 0 ? center : center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .65f;
                    Observe(index, report, bottom, element.sourceIndex, element.kind, element.pos.z,
                        definition.StartDistance + element.pos.z * definition.DistanceScale, x * definition.LateralScale, element.gameTime, false);
                }
            }
        }

        private static void Observe(IslandTerrainClearanceDiagnostics.HeightIndex index, IslandTerrainClearanceDiagnostics.Report report,
            Vector3 bottom, int source, string kind, float chartZ, float distance, float lane, double gameTime, bool corridor)
        {
            if (corridor) report.corridorSamples++; else report.tileSamples++;
            if (!index.TryHeight(bottom, true, out float height, out string material, out string mesh)) return;
            if (corridor) report.corridorSurfaceHits++; else report.tileSurfaceHits++;
            float clearance = bottom.y - height;
            if (corridor) report.minCorridorClearance = Mathf.Min(report.minCorridorClearance, clearance);
            else report.minTileClearance = Mathf.Min(report.minTileClearance, clearance);
            if (clearance >= .15f) return;
            if (corridor) report.corridorViolations++; else report.tileViolations++;
            var hits = corridor ? report.worstCorridor : report.worstTiles;
            if (hits.Count < 400) hits.Add(new IslandTerrainClearanceDiagnostics.Hit
            {
                sourceIndex = source, kind = kind, chartZ = chartZ, distance = distance, lateral = lane, gameTime = gameTime,
                position = bottom, tileBottom = bottom.y, surfaceHeight = height, clearance = clearance, material = material, mesh = mesh
            });
        }
    }
}
