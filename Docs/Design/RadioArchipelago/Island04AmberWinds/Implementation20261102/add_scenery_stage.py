from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'AmberWindsIslandJobs.cs'
s=p.read_text(encoding='utf8')
start=s.index('                Directory.CreateDirectory(ContentPath);')
end=s.index('                if (!File.Exists(DefinitionPath)',start)
asset_body=s[start:end]
s=s[:start]+'                CreateSceneryAssets();\n'+s[end:]
start=s.index('                definition.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>')
end=s.index('                RebuildChart(definition);',start)
configuration=s[start:end]
configuration=configuration.replace('Eight metres','Ten metres').replace('definition.StartDistance = 8;','definition.StartDistance = 10;')
s=s[:start]+'                ConfigureWorldDefinition(definition);\n'+s[end:]
first='''            foreach (string name in new[] { "amber_winds_island.fbx", "amber_winds_route.json", "play_elevation.json", "RooftopMotion.json", "cloud_steps.json" })
                if (!File.Exists(ImportPath + "/" + name)) throw new FileNotFoundException("The separate Amber Winds export is not ready.", name);'''
assert first in s
s=s.replace(first,'            RequireSourceExports();',1)
needle='''        private static Vector2[] CloudRanges()
'''
methods='''        [Serializable] private sealed class SceneryReport
        {
            public bool passed, noSongBinding, noGameplayChart, earlierAssetsPreserved;
            public string scope = "Scenery-only native preview using the actual source route/raw heights, imported triangles, source-authored cloud windows and rigid windmill nodes. No published song, gameplay chart, saved definition, menu entry or gameplay scene is required or authored.";
            public string sourceModel, sourceModelDependencyHash, prefab;
            public Report world;
        }

        private static void RequireSourceExports()
        {
            foreach (string name in new[] { "amber_winds_island.fbx", "amber_winds_route.json", "play_elevation.json", "ground_elevation.json", "RooftopMotion.json", "cloud_steps.json" })
                if (!File.Exists(ImportPath + "/" + name)) throw new FileNotFoundException("The separate frozen Amber Winds export is not ready.", name);
        }

        private static void CreateSceneryAssets()
        {
ASSET_BODY
            Sky();
            SaveOwnMaterials();
        }

        private static void SaveOwnMaterials()
        {
            foreach (var material in AssetDatabase.FindAssets("t:Material", new[] { ContentPath })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid))))
                AssetDatabase.SaveAssetIfDirty(material);
        }

        private static void ConfigureWorldDefinition(IslandStageDefinition definition)
        {
CONFIGURATION
        }

        private static IslandStageDefinition CreateSceneryDefinition()
        {
            RequireSourceExports();
            var definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
            definition.name = "Amber Winds source-scenery preview only";
            definition.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                ConfigureWorldDefinition(definition);
                definition.SongId = string.Empty;
                definition.Chart = null;
                definition.EnableGroundFinish = false;
                definition.ElevationJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportPath + "/play_elevation.json");
                if (definition.Prefab == null || definition.RouteJson == null || definition.ElevationJson == null || definition.RooftopMotionJson == null)
                    throw new InvalidOperationException("Import the separate frozen scenery, route and raw heights before creating an art preview.");
                return definition;
            }
            catch { Object.DestroyImmediate(definition); throw; }
        }

        [MenuItem("Radio Archipelago/Amber Winds/Prepare scenery only")]
        public static void PrepareScenery()
        {
            IslandReliefJobs.RequireEditMode(); RequireSourceExports();
            var quality = File.ReadAllBytes("ProjectSettings/QualitySettings.asset");
            try
            {
                CreateSceneryAssets();
                CaptureScenery();
                Debug.Log("AMBER_SCENERY_READY: independent prefab/materials and transient source-route preview; no song, catalog, gameplay chart, definition or scene published.");
            }
            finally { RestoreQuality(quality); }
        }

        [MenuItem("Radio Archipelago/Amber Winds/Capture scenery only")]
        public static void CaptureScenery()
        {
            IslandReliefJobs.RequireEditMode();
            var definition = CreateSceneryDefinition();
            try
            {
                var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                    IslandElevationProfile.FromJson(definition.ElevationJson.text));
                var report = new SceneryReport { noSongBinding = definition.SongId == string.Empty,
                    noGameplayChart = definition.Chart == null, sourceModel = ImportPath + "/amber_winds_island.fbx",
                    sourceModelDependencyHash = AssetDatabase.GetAssetDependencyHash(ImportPath + "/amber_winds_island.fbx").ToString(),
                    prefab = AssetDatabase.GetAssetPath(definition.Prefab), world = new Report { routeLength = route.Length } };
                foreach (var material in definition.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
                    if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader))
                        throw new InvalidOperationException("Amber scenery has an invalid material or shader.");
                AmberWindsTerrainChecks.RunSceneryChecks(definition);
                CheckRooftops(definition); CheckClouds(definition, route, report.world);
                CaptureDefinitionViews(definition, ReviewFolder + "/SceneryOnly");
                report.earlierAssetsPreserved = VerifyEarlierIslands(true);
                if (!report.noSongBinding || !report.noGameplayChart || !report.earlierAssetsPreserved)
                    throw new InvalidOperationException("Scenery preview must remain independent of song publication and preserve existing assets.");
                report.passed = true; Directory.CreateDirectory(ReviewFolder);
                File.WriteAllText(ReviewFolder + "/scenery-only-checks.json", JsonUtility.ToJson(report, true));
                Debug.Log("AMBER_SCENERY_CHECKS PASS: actual imported geography, source heights, separated volumetric clouds and rigid windmills; no gameplay chart.");
            }
            finally { Object.DestroyImmediate(definition); }
        }

'''.replace('ASSET_BODY','\n'.join(line[4:] if line.startswith('    ') else line for line in asset_body.rstrip().split('\n'))).replace('CONFIGURATION','\n'.join(line[4:] if line.startswith('    ') else line for line in configuration.rstrip().split('\n')))
assert needle in s
s=s.replace(needle,methods+needle)
s=s.replace('public static bool VerifyEarlierIslands()','public static bool VerifyEarlierIslands(bool allowUnpublishedSong = false)')
s=s.replace('if (!RadioTrioChartAuthoring.PreservesCatalogSnapshot(File.ReadAllText(before + "songs.json"))) return false;', 'if (!(allowUnpublishedSong && File.ReadAllBytes(before + "songs.json").SequenceEqual(File.ReadAllBytes(path))) &&\n                        !RadioTrioChartAuthoring.PreservesCatalogSnapshot(File.ReadAllText(before + "songs.json"))) return false;')
needle='''            var definition = LoadDefinition();
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,'''
replacement='''            var definition = LoadDefinition();
            if (definition == null) throw new InvalidOperationException("Create the fourth gameplay definition before capturing its conditioned route.");
            CaptureDefinitionViews(definition, ReviewFolder + "/Scenery");
        }

        private static void CaptureDefinitionViews(IslandStageDefinition definition, string folder)
        {
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,'''
assert needle in s
s=s.replace(needle,replacement,1)
s=s.replace('''                string folder = ReviewFolder + "/Scenery"; Directory.CreateDirectory(folder);''','''                Directory.CreateDirectory(folder);''')
p.write_text(s,encoding='utf8')
print('Extracted independent scenery preparation and transient, chart-free capture path.')
