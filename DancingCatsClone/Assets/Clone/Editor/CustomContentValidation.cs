using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Independent checks for optional authored content and its recovered baseline.</summary>
    public static class CustomContentValidation
    {
        public const string SongId = "AlwaysRememberMeowCat";
        private static int passed, failed;

        public static void Run()
        {
            int failures = RunChecks(true);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
            else if (failures != 0) throw new Exception("CustomContentValidation failed");
        }

        public static int RunChecks() { return RunChecks(false); }

        // Optional during development; the release harness must pass requireCustom: true.
        public static int RunChecks(bool requireCustom)
        {
            passed = failed = 0;
            Check("Recovered catalog remains 58 songs / 10015 elements / 12668 events", OriginalBaseline);
            Check("Absent custom catalog preserves all originals", OptionalCatalog);
            Check("Duplicate song and catalog identifiers fail clearly", DuplicateIdentifiers);
            Check("Merged catalog identifiers are unique", MergedCatalog);
            if (Resources.Load<TextAsset>("CustomData/songs") != null || requireCustom)
                Check("Always Remember custom metadata, audio, chart and finish are playable", CustomSong);
            else Debug.Log("CustomContentValidation: optional custom catalog is absent; custom-song checks skipped.");
            Debug.Log("CustomContentValidation: " + passed + " passed, " + failed + " failed");
            return failed;
        }

        private static SongRecord[] Originals()
        {
            var method = typeof(RecoveredCatalog).GetMethod("LoadOriginal", BindingFlags.Public | BindingFlags.Static);
            Require(method != null, "RecoveredCatalog.LoadOriginal is not implemented yet");
            return (SongRecord[])method.Invoke(null, null);
        }

        private static SongRecord[] Merge(SongRecord[] originals, SongRecord[] custom)
        {
            var method = typeof(RecoveredCatalog).GetMethod("MergeCatalog", BindingFlags.NonPublic | BindingFlags.Static);
            Require(method != null, "Optional custom catalog merge is not implemented yet");
            return (SongRecord[])method.Invoke(null, new object[] { originals, custom });
        }

        private static void OriginalBaseline()
        {
            var songs = Originals();
            Require(songs.Length == 58, "Original catalog must remain exactly 58 songs");
            var maps = songs.Select(s => RecoveredCatalog.LoadMap(s.id)).ToArray();
            Require(maps.Sum(m => m.elements.Length) == 10015, "Original authored element total changed");
            Require(maps.Sum(m => m.events.Length) == 12668, "Original authored event total changed");
        }

        private static void OptionalCatalog()
        {
            var originals = Originals();
            var merged = Merge(originals, null);
            Require(merged.Length == 58 && merged.Select(s => s.id).SequenceEqual(originals.Select(s => s.id)),
                "Missing custom catalog must preserve original songs and order");
            Require(Merge(originals, Array.Empty<SongRecord>()).Length == 58, "An empty optional catalog must preserve originals");
        }

        private static SongRecord Fixture(string id, int catalogId)
        {
            return new SongRecord { id = id, catalogId = catalogId, catalog = new OriginalSongMetadata { id = catalogId } };
        }

        private static void DuplicateIdentifiers()
        {
            var original = Fixture("Original", 1);
            Rejected(() => Merge(new[] { original }, new[] { Fixture("Original", 20001) }), "Original");
            Rejected(() => Merge(new[] { original }, new[] { Fixture("original", 20001) }), "original");
            Rejected(() => Merge(new[] { original }, new[] { Fixture("Custom", 1) }), "1");
            Rejected(() => Merge(new[] { original }, new[] { Fixture("Custom", 20001), Fixture("Custom", 20002) }), "Custom");
            Rejected(() => Merge(new[] { original }, new[] { Fixture("One", 20001), Fixture("one", 20002) }), "one");
            Rejected(() => Merge(new[] { original }, new[] { Fixture("One", 20001), Fixture("Two", 20001) }), "20001");
            Rejected(() => Merge(new[] { original }, new[] { Fixture("../unsafe", 20001) }), "identifier");
            Rejected(() => Merge(new[] { original }, new SongRecord[] { null }), "record");
        }

        private static void MergedCatalog()
        {
            var songs = RecoveredCatalog.Load();
            Require(songs.Select(s => s.id).Distinct(StringComparer.Ordinal).Count() == songs.Length, "Merged song IDs must be unique");
            Require(songs.Select(s => s.catalogId).Distinct().Count() == songs.Length, "Merged catalog IDs must be unique");
            var originals = Originals();
            Require(songs.Take(58).Select(s => s.id).SequenceEqual(originals.Select(s => s.id)), "Merging must retain original catalog ordering");
        }

        private static void CustomSong()
        {
            var songs = RecoveredCatalog.Load();
            Require(songs.Length >= 59, "Expected the 58 original songs plus the custom song");
            var song = songs.SingleOrDefault(s => s.id == SongId);
            Require(song != null && song.catalogId == 20001, "Custom song/catalog identifier is missing");
            Require(song.catalog != null && song.catalog.id == 20001 && song.catalog.builtin && !song.catalog.disable && song.catalog.musicUnlockType == 0,
                "Custom song must be enabled, builtin and unlocked");
            Require(!string.IsNullOrWhiteSpace(song.title) && !string.IsNullOrWhiteSpace(song.artist), "Custom song display metadata is missing");
            Require(song.audioResource == "CustomAudio/Music/" + SongId && song.timelineResource == "CustomData/Maps/" + SongId + ".timeline", "Custom resource paths are incorrect");
            var clip = song.LoadAudio();
            Require(clip != null && clip.samples > 0 && clip.frequency > 0, "Custom song audio is missing or empty");
            Require(clip.loadState == AudioDataLoadState.Loaded || clip.LoadAudioData(), "Custom audio cannot be loaded");
            Near(song.audioLengthSeconds, (double)clip.samples / clip.frequency, .01, "Decoded custom audio duration");
            Require(clip.frequency == song.sampleRate, "Custom audio sample rate differs from catalog");
            var map = RecoveredCatalog.LoadMap(SongId);
            Require(map.sampleRate == clip.frequency, "Custom chart sample rate differs from decoded recording");
            Require(song.elementCount == map.elements.Length && song.segmentCount == map.segments.Length && song.eventCount == map.events.Length,
                "Custom catalog/chart counts differ");
            Near(song.forwardSpeed, map.forwardSpeed, .00001, "Custom forward speed");
            Near(song.delayTime, map.delayTime, .00001, "Custom audio delay");
            ValidateGeometry(map, (double)clip.samples / clip.frequency);
        }

        private static void ValidateGeometry(MapTimeline map, double audioDuration)
        {
            Require(Finite(map.forwardSpeed) && map.forwardSpeed > 0 && Finite(map.delayTime), "Custom chart timing is not finite/positive");
            Require(map.elements.Length > 3 && map.elements[0].kind == "Start", "Custom chart must begin with Start");
            Require(map.elements.Count(e => e.kind == "Start") == 1 && map.elements.Count(e => e.kind == "FinishJump") == 1, "Custom chart needs exactly one Start and FinishJump");
            Require(map.elements.Select(e => e.sourceIndex).Distinct().Count() == map.elements.Length, "Custom source indices must be unique");
            Require(map.elements.Any(e => e.kind == "Jump") && map.elements.Any(e => e.kind == "Hold"), "Custom chart must include jumps and holds");
            var finish = map.elements.Single(e => e.kind == "FinishJump");
            Require(finish == map.elements.Last() && finish.id == 1, "FinishJump must be the final chart element and use the original finish prefab");
            Near(5, finish.jumpDistance, .001, "Finish jump must land on the platform five units ahead");
            Near(0, finish.pos.x, .001, "Finish jump must align with the center end platform");
            double platformAudioTime = finish.audioTime + 5 / map.forwardSpeed;
            Require(platformAudioTime >= audioDuration - 1.0 && platformAudioTime <= audioDuration + .05, "Finish platform landing must cover the full recording");
            for (int i = 0; i < map.elements.Length; i++)
            {
                var element = map.elements[i];
                Require(element != null && Finite(element.pos.x) && Finite(element.pos.y) && Finite(element.pos.z) && Finite(element.gameTime) && Finite(element.audioTime), "Custom element has non-finite coordinates/timing");
                Require(Math.Abs(element.pos.x) <= NativeRhythmRules.HorizontalLimit + .001, "Custom lane is beyond the playable horizontal limit");
                Near(element.gameTime * map.forwardSpeed, element.pos.z, .002, "Custom position/game time alignment");
                Near(element.gameTime - map.delayTime, element.audioTime, .0001, "Custom audio/game time alignment");
                Require(Finite(element.jumpDistance) && element.jumpDistance > 0, "Custom jump distance must be finite and positive");
                if (i == map.elements.Length - 1) continue;
                var next = map.elements[i + 1];
                Require(next.pos.z > element.pos.z && next.gameTime > element.gameTime, "Custom chart must progress monotonically");
                double launchZ = element.pos.z;
                if (element.kind == "Hold")
                {
                    var segment = map.segments.SingleOrDefault(s => s.sourceIndex == element.sourceIndex && s.kind == "Hold");
                    Require(segment != null && Finite(segment.endZ) && segment.endZ > segment.z, "Custom hold needs a positive finite segment");
                    Near(element.pos.z, segment.z, .002, "Hold start position");
                    Near(segment.endGameTime * map.forwardSpeed, segment.endZ, .002, "Hold end position/time");
                    Require(segment.holdTicks != null && segment.holdTicks.Length > 0, "Custom hold needs scoring ticks");
                    double lastZ = double.NegativeInfinity;
                    foreach (var tick in segment.holdTicks)
                    {
                        Require(tick != null && Finite(tick.z) && Finite(tick.gameTime) && Finite(tick.audioTime) && tick.z >= segment.z - .002 && tick.z > lastZ && tick.z <= segment.endZ + .002, "Hold ticks must advance within the hold");
                        Near(tick.gameTime * map.forwardSpeed, tick.z, .002, "Hold tick position/time");
                        Near(tick.gameTime - map.delayTime, tick.audioTime, .0001, "Hold tick audio time");
                        lastZ = tick.z;
                    }
                    launchZ = segment.endZ;
                }
                Require(next.pos.z - launchZ >= map.forwardSpeed * .24, "Custom landing/hold exit is too close for a safe jump");
                Near(next.pos.z - launchZ, element.jumpDistance, .003, "Custom jump must reach its next landing");
                Require(Math.Abs(next.pos.x - element.pos.x) <= 1.51, "Custom chart lane change is too wide");
            }
            double previous = -double.MaxValue;
            foreach (var evt in map.events)
            {
                Require(evt != null && Finite(evt.gameTime) && Finite(evt.audioTime) && evt.audioTime >= previous && evt.mStartSample >= 0 && evt.mEndSample >= evt.mStartSample,
                    "Custom rhythm events must be finite, ordered sample ranges");
                Near((double)evt.mStartSample / map.sampleRate, evt.audioTime, 1.5 / map.sampleRate, "Custom event sample/audio time");
                Near(evt.audioTime + map.delayTime, evt.gameTime, .0001, "Custom event game time");
                previous = evt.audioTime;
            }
            Require(map.events.Count(e => e.command == 100) == 1, "Custom chart needs one finish rhythm event");
            Require(map.segments.Count(s => s.kind == "FinishJump") == 1, "Custom chart needs one finish segment");
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static void Near(double expected, double actual, double tolerance, string message)
        { Require(Finite(expected) && Finite(actual) && Math.Abs(expected - actual) <= tolerance, message + ": expected " + expected + ", actual " + actual); }
        private static void Rejected(Action body, string diagnostic)
        {
            try { body(); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidDataException)
            {
                Require(error.InnerException.Message.IndexOf(diagnostic, StringComparison.OrdinalIgnoreCase) >= 0, "Catalog failure should identify " + diagnostic);
                return;
            }
            throw new Exception("Invalid custom catalog must throw InvalidDataException identifying " + diagnostic);
        }
        private static void Check(string name, Action body)
        {
            try { body(); passed++; Debug.Log("PASS " + name); }
            catch (Exception error) { failed++; Debug.LogError("FAIL " + name + ": " + (error.InnerException ?? error).Message); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
