from pathlib import Path
stage=Path(__file__).with_name('UnityStaging')
p=stage/'LivingWaterReview.cs'; s=p.read_text(encoding='utf8').replace('EditorApplication.timeSinceStartup - started > 60','EditorApplication.timeSinceStartup - started > (AmberLive ? 60 : 30)'); s=s.replace('''            if (state == PlayModeStateChange.EnteredEditMode)
            {''','''            if (state == PlayModeStateChange.ExitingPlayMode && live != null) { live.Dispose(); live = null; firstLive = null; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {'''); p.write_text(s,encoding='utf8')
p=stage/'AmberWindsIslandJobs.cs'; s=p.read_text(encoding='utf8'); s=s.replace('''        [Serializable] private sealed class SceneryReport
''','''        [Serializable] private sealed class SceneryMetrics
        {
            public int renderers,materials,clouds;
            public long triangles;
            public float routeLength,cloudMinimumGap,cloudMinimumClearance,cloudVolumeRatio;
        }

        [Serializable] private sealed class SceneryReport
''',1).replace('''            public Report world;
''','''            public SceneryMetrics world;
''',1).replace('world = new Report { routeLength = route.Length }','world = new SceneryMetrics { routeLength = route.Length }')
s=s.replace('''                CheckRooftops(definition); CheckClouds(definition, route, report.world);''','''                CheckRooftops(definition);
                var cloudReport = new Report(); CheckClouds(definition, route, cloudReport);
                report.world.clouds = cloudReport.clouds; report.world.cloudMinimumGap = cloudReport.cloudMinimumGap;
                report.world.cloudMinimumClearance = cloudReport.cloudMinimumClearance; report.world.cloudVolumeRatio = cloudReport.cloudVolumeRatio;''',1)
p.write_text(s,encoding='utf8')
print('Scenery report now exposes only art metrics; shared live-proof default Coral timeout unchanged.')
