using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class RecoveredDataValidation
    {
        private static int passed;
        private static int failed;

        public static void Run()
        {
            passed = failed = 0;
            Check("Five original songs load with original music", OriginalMusic);
            Check("All 620 authored elements and 629 events survive JSON import", OriginalCounts);
            Check("Original timing and delay survive import", Timing);
            Check("Original Hold ticks and Follow points survive import", Paths);
            Check("Missing or invalid map identifiers fail clearly", InvalidIds);
            Check("All 58 active Android songs, original maps and menu previews load", ActiveAndroidCatalog);
            Debug.Log("RecoveredDataValidation: " + passed + " passed, " + failed + " failed");
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
            else if (failed != 0) throw new Exception("RecoveredDataValidation failed");
        }

        private static Type Catalog
        {
            get
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DancingCats.Clone.RecoveredCatalog")).FirstOrDefault(t => t != null);
                Require(type != null, "RecoveredCatalog is not implemented yet");
                return type;
            }
        }

        private static Array Songs() { return (Array)Catalog.GetMethod("LoadOriginal").Invoke(null, null); }
        private static object Map(string id) { return Catalog.GetMethod("LoadMap").Invoke(null, new object[] { id }); }
        private static object Field(object value, string name)
        {
            var field = value.GetType().GetField(name);
            Require(field != null, value.GetType().Name + "." + name + " is missing");
            return field.GetValue(value);
        }
        private static string Text(object value, string name) { return (string)Field(value, name); }
        private static double Number(object value, string name) { return Convert.ToDouble(Field(value, name)); }
        private static Array Items(object value, string name) { return (Array)Field(value, name); }
        private static void Near(double expected, double actual, double tolerance, string message)
        { Require(Math.Abs(expected - actual) <= tolerance, message + ": expected " + expected + ", actual " + actual); }

        private static void OriginalMusic()
        {
            var songs = Songs();
            Require(songs.Length >= 5, "Five packaged songs must be available");
            string[] ids = { "UnityPop054", "TwinkleTwinkleLittleMeowCat054", "UnstoppableCat054", "RatDancePop054", "AllofMeCat054" };
            double[] lengths = { 45.008979591836734, 59.50693877551021, 67.00408163265305, 38.00816326530612, 67.00408163265305 };
            for (int i = 0; i < ids.Length; i++)
            {
                var song = songs.Cast<object>().Single(s => Text(s, "id") == ids[i]);
                var resource = Text(song, "audioResource");
                var clip = Resources.Load<AudioClip>(resource);
                Require(clip != null, "Missing playable original music " + resource);
                Require(clip.frequency == 44100 && clip.channels == 2, "Original stereo/sample rate changed: " + ids[i]);
                Near(lengths[i], (double)clip.samples / clip.frequency, 0.001, "Original decoded length " + ids[i]);
                Require(!Text(song, "title").Contains("Prototype"), "Original title missing");
            }
            Require(Resources.LoadAll<AudioClip>("OriginalAudio/Sfx").Length == 23, "All 23 packaged SFX must be present");
        }

        private static void OriginalCounts()
        {
            string[] ids = { "UnityPop054", "TwinkleTwinkleLittleMeowCat054", "UnstoppableCat054", "RatDancePop054", "AllofMeCat054" };
            int[] elementCounts = { 110, 97, 162, 97, 154 };
            int[] eventCounts = { 101, 117, 163, 91, 157 };
            for (int i = 0; i < ids.Length; i++)
            {
                var map = Map(ids[i]);
                Require(Text(map, "id") == ids[i], "Wrong map loaded");
                Require(Items(map, "elements").Length == elementCounts[i], "Lost authored elements: " + ids[i]);
                Require(Items(map, "events").Length == eventCounts[i], "Lost Koreography events: " + ids[i]);
                var start = Items(map, "elements").GetValue(0);
                Require(Text(start, "kind") == "Start", "Original Start element lost");
                Near(0, Number(start, "gameTime"), 0.000001, "Original Start time");
            }
        }

        private static void Timing()
        {
            var map = Map("UnityPop054");
            Near(6.5, Number(map, "forwardSpeed"), 0.000001, "Unity speed");
            Near(0.44501134753227234, Number(map, "delayTime"), 0.000001, "Unity audio delay");
            var firstJump = Items(map, "elements").GetValue(1);
            Near(0.5, Number(firstJump, "gameTime"), 0.000001, "First authored landing");
            Near(0.05498865246772766, Number(firstJump, "audioTime"), 0.000001, "First landing relative to music");
            var finish = Items(map, "segments").Cast<object>().Single(s => Text(s, "kind") == "FinishJump");
            Near(44.58623395745571, Number(finish, "audioTime"), 0.000001, "Original finish timing");
            Require(Items(map, "events").Cast<object>().Any(e => Number(e, "mStartSample") == 1966253 && Number(e, "command") == 100), "Original finish event samples/payload lost");
        }

        private static void Paths()
        {
            var map = Map("TwinkleTwinkleLittleMeowCat054");
            var elements = Items(map, "elements").Cast<object>().ToArray();
            Require(elements.Sum(e => Items(e, "holdTicks").Length) == 43, "All 43 Twinkle hold ticks must survive");
            var segments = Items(map, "segments").Cast<object>().ToArray();
            Require(segments.Sum(e => Items(e, "path").Length) == 22, "All 22 Twinkle follow points must survive");
            Require(segments.Any(e => Text(e, "kind") == "Hold" && Number(e, "duration") > 0 && Items(e, "holdTicks").Length > 0), "No complete hold segment");
            Require(segments.Any(e => Text(e, "kind") == "Follow" && Items(e, "path").Cast<object>().Any(p => Text(p, "kind") == "FollowEnd")), "FollowEnd was dropped");
            Require(elements.Any(e => Number(e, "updateType") != 0 && Field(e, "mstartPos") != null), "Authored dynamic motion must be retained");
        }

        private static void InvalidIds()
        {
            Songs();
            foreach (string id in new[] { "../UnityPop054", "", "not-a-recovered-song" })
            {
                bool rejected = false;
                try { Map(id); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ArgumentException || error.InnerException is System.IO.FileNotFoundException; }
                Require(rejected, "Invalid map identifier must be rejected clearly: " + id);
            }
        }

        private static void ActiveAndroidCatalog()
        {
            var songs = Songs();
            Require(songs.Length == 58, "The complete active Android table has 58 songs");
            Require(songs.Cast<object>().Select(s => Text(s,"id")).Distinct().Count() == 58, "Song IDs must be unique");
            int elements = 0, events = 0;
            foreach (var song in songs)
            {
                var id = Text(song,"id");
                var map = Map(id);
                var audio = Resources.Load<AudioClip>(Text(song,"audioResource"));
                var preview = Resources.Load<AudioClip>(Text(song,"previewAudioResource"));
                Require(audio != null, "Missing main original audio " + id);
                Require(preview != null && preview.samples > 0, "Missing original menu preview " + id);
                Near(Number(song,"audioLengthSeconds"), (double)audio.samples / audio.frequency, 0.001, "Recovered audio duration " + id);
                Require(audio.frequency == Number(map,"sampleRate"), "Recovered source sample rate changed " + id);
                Require(Items(map,"segments").Cast<object>().Count(s => Text(s,"kind")=="FinishJump")==1, "Original finish missing " + id);
                Require(Items(map,"elements").Length == Number(song,"elementCount"), "Song/map element count mismatch " + id);
                Require(Items(map,"events").Length == Number(song,"eventCount"), "Song/map event count mismatch " + id);
                var catalog = Field(song,"catalog");
                Require(Number(catalog,"id") == Number(song,"catalogId") && !string.IsNullOrEmpty(Text(catalog,"headSprite")), "Original catalog fields missing " + id);
                elements += Items(map,"elements").Length;
                events += Items(map,"events").Length;
            }
            Require(elements==10015 && events==12668, "Complete active authored chart counts changed");
            var symphony = Map("SymphonyNo40Piano");
            var variant = Items(symphony,"elements").Cast<object>().Single(e => Text(e,"name")=="Hold_Yu");
            Require(Number(variant,"id")==3 && Text(variant,"kind")=="Hold" && Items(variant,"holdTicks").Length==2, "Native prefab id3 Hold variant lost its behavior/ticks");
            foreach (var id in new[] { "DanceOfTheLittleSwans", "CarmenHabanera" })
                Require(Items(Map(id),"elements").Cast<object>().Where(e => Number(e,"id")==2).All(e => Text(e,"kind")=="Hold"), "Copied Hold display names must retain Hold behavior " + id);
        }

        private static void Check(string name, Action body)
        {
            try { body(); passed++; Debug.Log("PASS " + name); }
            catch (Exception error) { failed++; Debug.LogError("FAIL " + name + ": " + (error.InnerException ?? error).Message); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
