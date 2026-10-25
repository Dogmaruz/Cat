using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Exercises the real tile component without scenes, prefabs, timing changes or a reference chart.</summary>
    public static class NativeMovingTileValidation
    {
        [MenuItem("Dancing Cats/Validate native moving tile contacts")]
        public static void RunChecks()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Run native moving-tile checks in Edit Mode.");
            // Reflection lets this regression first fail on the previous runtime,
            // rather than introducing a compile error in the open Editor.
            var contact = typeof(OriginalTile).GetProperty("ContactPosition", BindingFlags.Public | BindingFlags.Instance);
            Require(contact != null && contact.PropertyType == typeof(Vector3),
                "A moving tile must expose its actual rewind contact instead of treating its phase as world X.");
            foreach (float lane in new[] { -.8f, -.4f, 0, .4f, .8f })
            foreach (int direction in new[] { 0, 1 })
            foreach (float speed in new[] { 6.5f, 9f })
                CheckMoving(contact, lane, direction, speed);
            foreach (int id in new[] { 0, 1, 2, 3, 4, 6 }) CheckStationaryContact(contact, id);
            CheckApproachingContact(contact);
            Debug.Log("NATIVE_MOVING_TILE PASS: real OriginalTile.Tick, inverse phase, both directions, two speeds, "
                + "three route positions, repeated-distance pause, full spatial cycle, non-mutating rewind contact, "
                + "stationary Jump/Hold/Follow and native approach interpolation.");
        }

        private static void CheckMoving(PropertyInfo contact, float lane, int direction, float speed)
        {
            var go = new GameObject("Native moving tile check");
            try
            {
                float phase = (float)(5 * Math.Asin(lane) / (2 * Math.PI));
                var source = new MapElement { id = 0, updateType = 3, msLeft = direction,
                    pos = new Vector3(phase, .2f, 70), begin = 50, end = 5 };
                go.transform.position = source.pos;
                var tile = go.AddComponent<OriginalTile>();
                tile.Initialize(null, source);
                tile.Tick(70 - speed * .1f); float before = tile.CenterX;
                tile.Tick(70); float at = tile.CenterX;
                Near(lane, at, .00001f, "inverse phase must land at the requested world X");
                Near(.2f, tile.TopY, .00001f, "cycle movement must preserve Y");
                Near(70, go.transform.position.z, .00001f, "cycle movement must preserve the tile's Z");
                tile.Tick(70 + speed * .1f); float after = tile.CenterX;
                Require(direction == 1 ? before < at && at < after : before > at && at > after,
                    "the two native motion flags must approach and leave the landing in opposite directions");
                var paused = go.transform.position;
                tile.Tick(70 + speed * .1f);
                Near(paused, go.transform.position, .00001f,
                    "repeating player distance must preserve motion during pause");
                var savedPose = go.transform.position;
                Near(new Vector3(lane, .2f, 70), (Vector3)contact.GetValue(tile), .00001f,
                    "rewinding a moving tile must restore the contact position, not its encoded phase");
                Near(savedPose, go.transform.position, .00001f,
                    "querying the rewind contact must not move the live tile");
                tile.Tick(100);
                Near(lane, tile.CenterX, .00001f, "one full native spatial period returns to the contact phase");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void CheckStationaryContact(PropertyInfo contact, int id)
        {
            var go = new GameObject("Native stationary contact check");
            try
            {
                var source = new MapElement { id = id, updateType = id == 4 ? 2 : 0,
                    pos = new Vector3(.35f, .1f, 42) };
                var tile = go.AddComponent<OriginalTile>();
                tile.Initialize(null, source);
                go.transform.position = new Vector3(-.9f, 1, 90);
                Near(source.pos, (Vector3)contact.GetValue(tile), .00001f,
                    "rewind must preserve the original static Jump, Hold and Follow entry positions");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void CheckApproachingContact(PropertyInfo contact)
        {
            var go = new GameObject("Native approach contact check");
            try
            {
                // The approach may still be in progress at ahead=0 when its end is negative.
                var source = new MapElement { id = 0, updateType = 1,
                    pos = new Vector3(.6f, .4f, 40), mstartPos = new Vector3(-.8f, -.2f, 40), begin = 4, end = -2 };
                go.transform.position = source.pos;
                var tile = go.AddComponent<OriginalTile>();
                tile.Initialize(null, source);
                tile.Tick(40);
                var actualContact = go.transform.position;
                Near(new Vector3(.13333333f, .2f, 40), actualContact, .00001f,
                    "the actual type-1 contact uses its authored approach interval");
                tile.Tick(35);
                var savedPose = go.transform.position;
                Near(actualContact, (Vector3)contact.GetValue(tile), .00001f,
                    "rewind contact must match the real Tick result for an approaching tile");
                Near(savedPose, go.transform.position, .00001f,
                    "an approach contact query must not advance the live tile");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Near(Vector3 expected, Vector3 actual, float tolerance, string message)
        { Near(expected.x, actual.x, tolerance, message + " X"); Near(expected.y, actual.y, tolerance, message + " Y"); Near(expected.z, actual.z, tolerance, message + " Z"); }
        private static void Near(float expected, float actual, float tolerance, string message)
        { Require(!float.IsNaN(actual) && !float.IsInfinity(actual) && Math.Abs(expected - actual) <= tolerance,
            message + ": expected " + expected + ", actual " + actual); }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("NATIVE_MOVING_TILE FAIL: " + message); }
    }
}
