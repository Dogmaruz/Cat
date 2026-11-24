using System;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Binds the approved song to the existing island without replacing its scenery or presentation.</summary>
    public static class LumiLaiIslandJobs
    {
        private const string CatalogPath = "Assets/Resources/CustomData/songs.json";

        [Serializable] private sealed class Report
        {
            public bool passed, trialRemoved, approvedSource, currentIsland, chartTimingPreserved, uniformRoundTiles, centeredOpening;
            public float lateralScale, playableWidth;
            public string songId, title, audioSha256, islandChart;
            public double duration;
            public int catalogId, elements, holds, follows, bigJumps, movingRounds;
        }

        [MenuItem("Radio Archipelago/Lumi-Lai/Replace island trial song")]
        public static void Install()
        {
            IslandReliefJobs.RequireEditMode();
            LumiLaiChartAuthoring.LoadSource();
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(LumiLaiChartAuthoring.BaseChartPath);
            if (source == null) throw new InvalidDataException("Generate the approved Lumi-Lai base chart first.");
            var map = JsonUtility.FromJson<MapTimeline>(source.text);
            AudioChartGeneratorValidation.ValidateMap(map);
            if (map.id != LumiLaiChartAuthoring.SongId || map.audio.rawSha256 != LumiLaiChartAuthoring.SourceSha256)
                throw new InvalidDataException("The new island chart must come from the approved Lumi-Lai recording.");

            var songs = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(CatalogPath));
            if (songs.Count(s => s.id == LumiLaiChartAuthoring.SongId) != 1)
                throw new InvalidDataException("The new song must be published exactly once before replacing the trial.");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            if (definition == null) throw new InvalidDataException("The current floating island definition is missing.");
            definition.SongId = LumiLaiChartAuthoring.SongId;
            definition.LateralScale = IslandAbyssJobs.CourseLateralScale;
            definition.UniformRoundTiles = true;
            IslandAbyssJobs.RebuildMusicChart(definition);

            // Keep historical audio/maps for authoring reproduction, but remove the
            // retired trial from every runtime menu loaded from the custom catalog.
            var active = songs.Where(s => s.id != AudioChartGenerator.AlwaysRememberId).OrderBy(s => s.catalogId);
            File.WriteAllText(CatalogPath, "[\n" + string.Join(",\n", active.Select(s => JsonUtility.ToJson(s, true))) + "\n]\n");
            AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            RunChecks();
            Debug.Log("LUMI_LAI_ISLAND_READY: the current island now plays Lumi-Lai; the trial song is retired.");
        }

        [MenuItem("Radio Archipelago/Lumi-Lai/Check current island song")]
        public static void RunChecks()
        {
            IslandReliefJobs.RequireEditMode();
            var songs = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(CatalogPath));
            var song = songs.Single(s => s.id == LumiLaiChartAuthoring.SongId);
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
            var clip = LumiLaiChartAuthoring.LoadSource();
            var report = new Report
            {
                songId = song.id, title = song.title, catalogId = song.catalogId,
                duration = song.audioLengthSeconds, audioSha256 = map.audio.rawSha256,
                trialRemoved = songs.All(s => s.id != AudioChartGenerator.AlwaysRememberId),
                approvedSource = clip != null && map.audio.rawSha256 == LumiLaiChartAuthoring.SourceSha256 &&
                    map.sampleRate == clip.frequency && song.sampleRate == clip.frequency &&
                    Math.Abs(song.audioLengthSeconds - (double)clip.samples / clip.frequency) < .001 &&
                    song.audioResource == "CustomAudio/Music/LumiLai" && song.previewAudioResource == song.audioResource,
                currentIsland = definition.SongId == song.id && map.id == song.id,
                uniformRoundTiles = definition.UniformRoundTiles,
                lateralScale = definition.LateralScale,
                playableWidth = 2 * NativeRhythmRules.HorizontalLimit * definition.LateralScale,
                islandChart = AssetDatabase.GetAssetPath(definition.Chart),
                elements = map.elements.Length,
                holds = map.segments.Count(s => s.kind == "Hold"),
                follows = map.segments.Count(s => s.kind == "Follow"),
                bigJumps = map.segments.Count(s => s.kind == "BigJump"),
                movingRounds = map.elements.Count(e => e.updateType == 3)
            };
            if (!report.trialRemoved || !report.approvedSource || !report.currentIsland)
                throw new InvalidDataException("Lumi-Lai source, catalog retirement or island binding is inconsistent.");
            if (!report.uniformRoundTiles || Mathf.Abs(report.lateralScale - IslandAbyssJobs.CourseLateralScale) > .001f)
                throw new InvalidDataException("The current island must retain full-size tiles and the comfortable lane width.");
            IslandOpeningChecks.RunChecks();
            IslandOpeningChecks.VerifyOpening(map);
            report.centeredOpening = true;
            IslandAbyssJobs.RunChecks();
            report.chartTimingPreserved = true;
            report.passed = true;
            Directory.CreateDirectory(LumiLaiChartAuthoring.ReviewPath);
            File.WriteAllText(Path.Combine(LumiLaiChartAuthoring.ReviewPath, "island-integration.json"), JsonUtility.ToJson(report, true) + "\n");
        }
    }
}
