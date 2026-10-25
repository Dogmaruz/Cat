using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DancingCats.Clone.Editor
{
    /// <summary>Publishes a spatial-only revision of explicitly separate custom maps.</summary>
    public static class ChartRouteJobs
    {
        private const string CatalogPath = "Assets/Resources/CustomData/songs.json";
        private const string MapFolder = "Assets/Resources/CustomData/Maps/";
        private const string Output = "Library/ChartChoreographyV1";

        [Serializable] public sealed class Report
        {
            public string id, beforeRhythmSha256, afterRhythmSha256;
            public bool rhythmUnchanged, followsUnchanged, idempotent;
            public int elements, left, right, centre, moving, movingLeftAtContact, movingRightAtContact;
        }
        private sealed class Pending
        {
            public string path;
            public byte[] before;
            public MapTimeline map;
            public AudioClip clip;
            public AudioChartGenerator.Options options;
            public Report report;
        }

        [MenuItem("Dancing Cats/Apply routes to all custom levels")]
        public static void ApplyAllCustomLevels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Route publishing requires a compiled Editor in Edit Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open scene before publishing routes.");
            var records = RecoveredCatalog.FromJsonArray<SongRecord>(File.ReadAllText(CatalogPath));
            var originals = RecoveredCatalog.LoadOriginal();
            var pending = new List<Pending>();
            var protectedResources = Directory.GetFiles("Assets/Resources/OriginalData", "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Resources/OriginalAudio", "*", SearchOption.AllDirectories))
                .ToDictionary(path => path, path => Hash(File.ReadAllBytes(path)));
            foreach (var record in records)
            {
                if (record == null) throw new InvalidDataException("Custom route catalog contains a null record.");
                var options = new AudioChartGenerator.Options { catalogId = record.catalogId, title = record.title,
                    artist = record.artist, forwardSpeed = record.forwardSpeed, delayTime = record.delayTime };
                AudioChartGenerator.ValidateOptions(record.id, options);
                AudioChartGenerator.ResolveCatalogId(record.id, originals, records, record.catalogId);
                string path = MapFolder + record.id + ".timeline.json";
                if (record.timelineResource != "CustomData/Maps/" + record.id + ".timeline")
                    throw new InvalidDataException("A custom route must stay inside CustomData/Maps: " + record.id);
                var bytes = File.ReadAllBytes(path);
                var map = JsonUtility.FromJson<MapTimeline>(File.ReadAllText(path));
                if (map.id != record.id) throw new InvalidDataException("Custom map/catalog identity mismatch.");
                string before = RhythmFingerprint(map), follows = FollowFingerprint(map);
                ChartRouteComposer.Apply(map);
                AudioChartGeneratorValidation.ValidateMap(map);
                var report = Describe(map);
                report.beforeRhythmSha256 = before;
                report.afterRhythmSha256 = RhythmFingerprint(map);
                report.rhythmUnchanged = before == report.afterRhythmSha256;
                report.followsUnchanged = follows == FollowFingerprint(map);
                string first = JsonUtility.ToJson(map);
                ChartRouteComposer.Apply(map);
                report.idempotent = first == JsonUtility.ToJson(map);
                if (!report.rhythmUnchanged || !report.followsUnchanged || !report.idempotent)
                    throw new InvalidDataException("Route composition changed music, Follow geometry or repeated output: " + map.id);
                var clip = record.LoadAudio();
                if (clip == null) throw new FileNotFoundException("Custom audio is missing: " + record.audioResource);
                pending.Add(new Pending { path = path, before = bytes, map = map, clip = clip, report = report,
                    options = options });
            }
            byte[] catalogBefore = File.ReadAllBytes(CatalogPath);
            try
            {
                // Every map has passed its checks before the first asset is written.
                foreach (var item in pending) AudioChartGenerator.Save(item.map, item.clip, item.options);
                if (protectedResources.Any(pair => pair.Value != Hash(File.ReadAllBytes(pair.Key))))
                    throw new InvalidOperationException("Protected original resources changed during route publishing.");
            }
            catch
            {
                foreach (var item in pending)
                {
                    File.WriteAllBytes(item.path, item.before);
                    AssetDatabase.ImportAsset(item.path, ImportAssetOptions.ForceSynchronousImport);
                }
                File.WriteAllBytes(CatalogPath, catalogBefore);
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
            Directory.CreateDirectory(Output);
            foreach (var item in pending)
                File.WriteAllText(Path.Combine(Output, item.map.id + ".route.json"), JsonUtility.ToJson(item.report, true));
            Debug.Log("CHART_ROUTE_PUBLISH PASS: " + pending.Count + " custom maps; musical timing, Follow paths and original resources unchanged.");
        }

        public static Report Describe(MapTimeline map)
        {
            var supports = map.elements.Where(e => e.kind == "Jump" || e.kind == "BigJump" || e.kind == "Hold").ToArray();
            return new Report { id = map.id, elements = map.elements.Length,
                left = supports.Count(e => ChartRouteComposer.LandingX(e) < -.05f),
                right = supports.Count(e => ChartRouteComposer.LandingX(e) > .05f),
                centre = supports.Count(e => Math.Abs(ChartRouteComposer.LandingX(e)) <= .05f),
                moving = supports.Count(e => e.updateType == 3),
                movingLeftAtContact = supports.Count(e => e.updateType == 3 && e.msLeft == 0),
                movingRightAtContact = supports.Count(e => e.updateType == 3 && e.msLeft != 0) };
        }

        /// <summary>All non-spatial fields remain part of the invariant, including events and imported audio provenance.</summary>
        public static string RhythmFingerprint(MapTimeline source)
        {
            var map = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(source));
            map.normalizationNotes = Array.Empty<string>();
            foreach (var e in map.elements)
            {
                e.pos.x = 0; e.updateType = e.msLeft = 0; e.mstartPos = Vector3.zero;
            }
            foreach (var segment in map.segments)
            {
                segment.x = 0;
                if (segment.motion != null)
                { segment.motion.updateType = segment.motion.msLeft = 0; segment.motion.mstartPos = Vector3.zero; }
                foreach (var p in segment.path ?? Array.Empty<ChartPoint>())
                { p.x = 0; p.updateType = p.msLeft = 0; }
            }
            return Hash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(map)));
        }
        private static string FollowFingerprint(MapTimeline map)
        {
            string value = string.Join("\n", map.segments.Where(s => s.kind == "Follow")
                .SelectMany(s => new[] { JsonUtility.ToJson(s) }.Concat(s.elementIndices.Select(i => JsonUtility.ToJson(map.elements[i])))));
            return Hash(System.Text.Encoding.UTF8.GetBytes(value));
        }
        private static string Hash(byte[] value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }
    }
}
