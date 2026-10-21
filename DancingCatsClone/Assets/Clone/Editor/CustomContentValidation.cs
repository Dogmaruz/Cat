using System;
using System.IO;
using System.Globalization;
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
            Check("Native Follow route accepts zero-distance entry and markers", NativeFollowFixture);
            Check("Broken Follow support length fails clearly", BrokenFollowFixture);
            if (Resources.Load<TextAsset>("CustomData/songs") != null || requireCustom)
                Check("Always Remember custom metadata, audio, chart and finish are playable", CustomSong);
            else Debug.Log("CustomContentValidation: optional custom catalog is absent; custom-song checks skipped.");
            Debug.Log("CustomContentValidation: " + passed + " passed, " + failed + " failed");
            return failed;
        }

        private static SongRecord[] Originals()
        {
            var method = typeof(RecoveredCatalog).GetMethod("LoadOriginal", BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
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

        private static void NativeFollowFixture() => ValidateGeometry(FollowFixture(), 3.5);

        private static void BrokenFollowFixture()
        {
            var map = FollowFixture();
            map.elements[3].mfendPos = 5;
            try { ValidateGeometry(map, 3.5); }
            catch (Exception error) when (error.Message.IndexOf("Follow", StringComparison.OrdinalIgnoreCase) >= 0) { return; }
            throw new Exception("Malformed Follow support must identify its mismatched exit length");
        }

        private static MapTimeline FollowFixture()
        {
            MapElement Element(int index, int id, string kind, float z, float distance) => new MapElement
            {
                sourceIndex = index, id = id, kind = kind, name = kind, pos = new Vector3(0, 0, z),
                gameTime = z / 10.0, audioTime = z / 10.0 - .5, jumpDistance = distance,
                begin = 50, end = 5, ext = "", holdTicks = Array.Empty<HoldTick>()
            };
            HoldTick Tick(float z) => new HoldTick { z = z, gameTime = z / 10.0, audioTime = z / 10.0 - .5 };
            var elements = new[]
            {
                Element(0, 0, "Start", 0, 5), Element(1, 0, "Jump", 5, 5),
                Element(2, 2, "Hold", 10, 5), Element(3, 4, "Follow", 19, 0),
                Element(4, 5, "FollowMids", 22, 0), Element(5, 6, "FollowEnd", 25, 5),
                Element(6, 0, "Jump", 30, 5), Element(7, 1, "FinishJump", 35, 5)
            };
            elements[2].se = new Vector2(0, -4); elements[2].ext = "10,12,14";
            elements[2].holdTicks = new[] { Tick(10), Tick(12), Tick(14) };
            elements[3].updateType = 2; elements[3].mfendPos = 6;
            return new MapTimeline
            {
                sampleRate = 1000, forwardSpeed = 10, delayTime = .5f, elements = elements,
                segments = new[]
                {
                    new MapSegment { sourceIndex = 2, id = 2, kind = "Hold", z = 10, endZ = 14, endGameTime = 1.4,
                        holdTicks = elements[2].holdTicks, elementIndices = new[] { 2 } },
                    new MapSegment { sourceIndex = 3, id = 4, kind = "Follow", z = 19, endZ = 25, endGameTime = 2.5,
                        elementIndices = new[] { 3, 4, 5 }, path = new[] { new ChartPoint { sourceIndex = 4, x = 0, z = 22 } } },
                    new MapSegment { sourceIndex = 7, id = 1, kind = "FinishJump", z = 35, endZ = 35 }
                },
                events = new[] { new KoreographyEvent { command = 100, mStartSample = 3000, mEndSample = 3000, audioTime = 3, gameTime = 3.5 } }
            };
        }

        private static void ValidateGeometry(MapTimeline map, double audioDuration)
        {
            Require(map != null && map.elements != null && map.segments != null && map.events != null, "Custom chart collections are missing");
            Require(Finite(map.forwardSpeed) && map.forwardSpeed > 0 && Finite(map.delayTime) && map.sampleRate > 0, "Custom chart timing is not finite/positive");
            Require(map.elements.All(e => e != null), "Custom chart contains a null element");
            Require(map.elements.Length > 3 && map.elements[0].kind == "Start", "Custom chart must begin with Start");
            Require(map.elements.Count(e => e.kind == "Start") == 1 && map.elements.Count(e => e.kind == "FinishJump") == 1, "Custom chart needs exactly one Start and FinishJump");
            Require(map.elements.Select(e => e.sourceIndex).Distinct().Count() == map.elements.Length, "Custom source indices must be unique");
            Require(map.elements.Select((e, i) => e.sourceIndex == i).All(valid => valid), "Custom source indices must address their actual element array entries");
            Require(map.elements.Any(e => e.kind == "Jump") && map.elements.Any(e => e.kind == "Hold"), "Custom chart must include jumps and holds");
            var finish = map.elements.Single(e => e.kind == "FinishJump");
            Require(finish == map.elements.Last() && finish.id == 1, "FinishJump must be the final chart element and use the original finish prefab");
            Near(5, finish.jumpDistance, .001, "Finish jump must land on the platform five units ahead");
            Near(0, finish.pos.x, .001, "Finish jump must align with the center end platform");
            double platformAudioTime = finish.audioTime + 5 / map.forwardSpeed;
            Require(platformAudioTime >= audioDuration - 1.0 && platformAudioTime <= audioDuration + .05, "Finish platform landing must cover the full recording");
            ValidateFollowRoutes(map);
            for (int i = 0; i < map.elements.Length; i++)
            {
                var element = map.elements[i];
                Require(element != null && Finite(element.pos.x) && Finite(element.pos.y) && Finite(element.pos.z) && Finite(element.gameTime) && Finite(element.audioTime), "Custom element has non-finite coordinates/timing");
                Require(Math.Abs(element.pos.x) <= NativeRhythmRules.HorizontalLimit + .001, "Custom lane is beyond the playable horizontal limit");
                Near(element.gameTime * map.forwardSpeed, element.pos.z, .002, "Custom position/game time alignment");
                Near(element.gameTime - map.delayTime, element.audioTime, .0001, "Custom audio/game time alignment");
                Require(element.id >= 0 && element.id <= 7, "Custom chart contains an unsupported source prefab ID");
                Require(Finite(element.jumpDistance), "Custom jump distance must be finite");
                if (i + 1 < map.elements.Length)
                    Require(map.elements[i + 1].pos.z > element.pos.z && map.elements[i + 1].gameTime > element.gameTime, "Custom chart must progress monotonically");
                if (element.id == 4 || element.id == 5 || element.id == 7)
                {
                    Near(0, element.jumpDistance, .00001, "Follow entry/markers and decorations must not launch an arc");
                    continue;
                }
                Require(element.jumpDistance > 0, "Custom outgoing arc distance must be positive");
                Require(element.id == 2 || element.id == 3 || element.id == 6 ? element.updateType == 0 : element.updateType == 0 || element.updateType == 3,
                    "Custom source type has no supported native movement contract");
                double launchZ = element.pos.z;
                if (element.id == 2 || element.id == 3)
                {
                    var segment = map.segments.SingleOrDefault(s => s.sourceIndex == element.sourceIndex && s.kind == "Hold");
                    Require(segment != null && Finite(segment.endZ) && segment.endZ > segment.z, "Custom hold needs a positive finite segment");
                    Near(element.pos.z, segment.z, .002, "Hold start position");
                    Near(segment.endGameTime * map.forwardSpeed, segment.endZ, .002, "Hold end position/time");
                    Require(segment.holdTicks != null && segment.holdTicks.Length >= 2, "Custom hold needs entry and exit scoring ticks");
                    Near(0, element.se.x, .002, "Hold mesh starts at its authored entry");
                    Near(segment.endZ - segment.z, -element.se.y, .002, "Hold mesh length matches support geometry");
                    double lastZ = double.NegativeInfinity;
                    foreach (var tick in segment.holdTicks)
                    {
                        Require(tick != null && Finite(tick.z) && Finite(tick.gameTime) && Finite(tick.audioTime) && tick.z >= segment.z - .002 && tick.z > lastZ && tick.z <= segment.endZ + .002, "Hold ticks must advance within the hold");
                        Near(tick.gameTime * map.forwardSpeed, tick.z, .002, "Hold tick position/time");
                        Near(tick.gameTime - map.delayTime, tick.audioTime, .0001, "Hold tick audio time");
                        lastZ = tick.z;
                    }
                    Near(segment.z, segment.holdTicks.First().z, .002, "Hold first tick is its single entry score");
                    Near(segment.endZ, segment.holdTicks.Last().z, .002, "Hold last tick reaches its exit");
                    Require(element.holdTicks != null && element.holdTicks.Length == segment.holdTicks.Length, "Hold element and segment checkpoints differ");
                    var serializedTicks = (element.ext ?? "").Split(',');
                    Require(serializedTicks.Length == segment.holdTicks.Length, "Hold native ext checkpoint count differs");
                    for (int tickIndex = 0; tickIndex < serializedTicks.Length; tickIndex++)
                    {
                        Require(double.TryParse(serializedTicks[tickIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out double nativeZ), "Hold native ext checkpoint is not an invariant number");
                        Near(segment.holdTicks[tickIndex].z, nativeZ, .002, "Hold native ext checkpoint Z");
                        Near(segment.holdTicks[tickIndex].z, element.holdTicks[tickIndex].z, .002, "Hold element checkpoint Z");
                    }
                    launchZ = segment.endZ;
                }
                var next = map.elements.Skip(i + 1).FirstOrDefault(e => e.id != 5 && e.id != 7);
                if (next == null) continue;
                Require(next.pos.z > launchZ, "Custom outgoing arc must lead to a later contact after its actual support exit");
                Near(next.pos.z - launchZ, element.jumpDistance, .003, "Custom jump must reach its next landing");
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

        private static void ValidateFollowRoutes(MapTimeline map)
        {
            var owned = new System.Collections.Generic.HashSet<int>();
            foreach (var entry in map.elements.Where(e => e.id == 4))
            {
                Require(entry.updateType == 2 && Finite(entry.mfendPos) && entry.mfendPos > 0, "Follow entry must use native forward-with-ball movement");
                var segment = map.segments.SingleOrDefault(s => s.sourceIndex == entry.sourceIndex && s.kind == "Follow");
                Require(segment != null && segment.elementIndices != null && segment.elementIndices.Length >= 2, "Follow needs an entry-to-exit support segment");
                var indices = segment.elementIndices;
                Require(indices.First() == entry.sourceIndex && indices.All(index => index >= 0 && index < map.elements.Length) &&
                    indices.Distinct().Count() == indices.Length && indices.SequenceEqual(indices.OrderBy(index => index)), "Follow element indices must be valid, unique and ordered");
                var exit = map.elements[indices.Last()];
                Require(exit.id == 6 && exit.updateType == 0, "Follow must terminate at its native FollowEnd");
                Near(entry.pos.z, segment.z, .002, "Follow segment starts at its platform entry");
                Near(exit.pos.z, segment.endZ, .002, "Follow support ends at its actual exit");
                Near(segment.endZ - segment.z, entry.mfendPos, .002, "Follow native travel length reaches its exit");
                Near(segment.endGameTime * map.forwardSpeed, segment.endZ, .002, "Follow end position/time");
                foreach (int index in indices.Skip(1))
                {
                    var element = map.elements[index];
                    Require(owned.Add(index), "Follow marker/exit belongs to more than one support");
                    Require(index == indices.Last() || element.id == 5 && (element.updateType == 0 || element.updateType == 3), "Follow interior must contain native path markers");
                    Require(element.pos.z > entry.pos.z && element.pos.z <= exit.pos.z, "Follow path must advance inside its support");
                }
                Require(!map.elements.Skip(entry.sourceIndex + 1).Take(exit.sourceIndex - entry.sourceIndex - 1).Any(e => e.id != 5 && e.id != 7), "Follow support cannot overlap another landing or sustained support");
                Require(segment.path != null, "Follow derived path is missing");
                foreach (var point in segment.path)
                {
                    Require(point != null && indices.Contains(point.sourceIndex), "Follow path references a foreign element");
                    var element = map.elements[point.sourceIndex];
                    Near(element.pos.x, point.x, .002, "Follow path X matches its actual marker");
                    Near(element.pos.z, point.z, .002, "Follow path Z matches its actual marker");
                }
                Require(indices.Where(index => map.elements[index].id == 5).All(index => segment.path.Any(point => point.sourceIndex == index)), "Follow derived path omits a scoring midpoint");
            }
            Require(map.elements.Where(e => e.id == 5 || e.id == 6).All(e => owned.Contains(e.sourceIndex)), "Follow marker/exit has no owning support");
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
