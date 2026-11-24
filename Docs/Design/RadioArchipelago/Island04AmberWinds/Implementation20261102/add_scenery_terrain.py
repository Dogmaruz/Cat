from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'AmberWindsTerrainChecks.cs'
s=p.read_text(encoding='utf8')
needle='''        private static StructureReport CheckStructureVolume(IslandStageDefinition definition, IslandRoute route, float width)
'''
method='''        public static void RunSceneryChecks(IslandStageDefinition definition)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Amber scenery triangle checks require stable Edit Mode.");
            Require(definition != null && definition.Prefab != null && definition.RouteJson != null && definition.ElevationJson != null &&
                definition.Chart == null && string.IsNullOrEmpty(definition.SongId), "The scenery proof requires an unsaved, chart-free source definition.");
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            RunRegressionChecks();
            var report = new IslandTerrainClearanceDiagnostics.Report {
                checkedUtc = DateTime.UtcNow.ToString("O"),
                prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(definition.Prefab)).ToString(),
                scope = "Scenery-only actual imported Amber triangles over the entire source route/rawplay elevation at .25m across full steering. Includes solid architecture/wood/metal/bark body volume rays. No chart, song binding, holds, final reward path or saved definition is asserted by this art proof." };
            var index = new IslandTerrainClearanceDiagnostics.HeightIndex(definition.Prefab, report);
            float width = NativeRhythmRules.HorizontalLimit * definition.LateralScale + .75f;
            for (float distance = 0; distance < route.Length; distance += .25f)
            {
                var frame = route.Evaluate(distance);
                for (int lane = 0; lane <= 16; lane++)
                {
                    float lateral = Mathf.Lerp(-width, width, lane / 16f);
                    var bottom = frame.Position + frame.Right * lateral + Vector3.up * (definition.TileElevation - definition.TileThickness);
                    Observe(index, report, bottom, -1, "source-scenery-corridor", 0, distance, lateral, 0, true);
                }
            }
            var structures = CheckStructureVolume(definition, route, width);
            structures.floorRegressionPassed = true; structures.fixtureCheckedUtc = DateTime.UtcNow.ToString("O");
            CheckAssetAnchors(definition, structures, false);
            report.worstCorridor.Sort((a,b) => a.clearance.CompareTo(b.clearance));
            report.passed = report.solidMeshes > 0 && report.triangles > 1000 && report.corridorSamples > 1000 &&
                report.corridorSurfaceHits > 100 && report.corridorViolations == 0 &&
                structures.structureRegressionPassed && structures.floorRegressionPassed && structures.anchorChecksPassed &&
                structures.structureSamples > 1000 && structures.structureViolations == 0;
            Directory.CreateDirectory(AmberWindsIslandJobs.ReviewFolder);
            string path = AmberWindsIslandJobs.ReviewFolder + "/scenery-terrain-clearance.json";
            File.WriteAllText(path, CombineEvidence(report, structures));
            Require(report.passed, "Amber source scenery corridor failed: terrain=" + report.corridorViolations +
                ", structures=" + structures.structureViolations + "; see " + path);
            Debug.Log("AMBER_SCENERY_TERRAIN PASS: imported raw-route floor and structure corridor; no musical chart implied.");
        }

'''
assert needle in s
s=s.replace(needle,method+needle)
s=s.replace('private static void CheckAssetAnchors(IslandStageDefinition definition, StructureReport report)','private static void CheckAssetAnchors(IslandStageDefinition definition, StructureReport report, bool requireSavedDefinition = true)')
s=s.replace('!string.IsNullOrEmpty(report.prefabGuid) && !string.IsNullOrEmpty(report.definitionGuid),','!string.IsNullOrEmpty(report.prefabGuid) && (!requireSavedDefinition || !string.IsNullOrEmpty(report.definitionGuid)),')
p.write_text(s,encoding='utf8')
print('Added independent native scenery-only corridor and source-mesh anchor proof.')
