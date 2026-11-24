using System;
using System.IO;
using System.Reflection;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Real-collider contact boundaries and desktop edge reversal; no scene or saved data is changed.</summary>
    public static class IslandWideInputChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Report
        {
            public bool passed, legacyUnchanged, fixedWorldContactWidth, movingContactWidth, holdSupportWidth;
            public bool triggerCannotBypassWidth, finishWidthUnchanged, perfectUsesWorldError, desktopEdgeReversal;
            public float lateralScale, worldHalfWidth;
            public int boundarySamples;
        }

        [MenuItem("Radio Archipelago/Floating archipelago/Check widened input and contacts")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run widened input checks in Edit Mode.");
            var report = new Report();
            using (var fixture = new Fixture())
            {
                fixture.Definition.LateralScale = 1;
                report.legacyUnchanged = fixture.CompareLegacy(report);
                fixture.Definition.LateralScale = 1.9f;
                report.lateralScale = fixture.Game.IslandLateralScale;
                report.worldHalfWidth = fixture.Game.IslandWorldLateralLimit;
                Near(report.lateralScale, 1.9f, "The active island did not expose its widening scale.");
                Near(report.worldHalfWidth, 2.85f, "The chart limits do not cover the widened world corridor.");

                fixture.Tile.element.id = 0;
                report.fixedWorldContactWidth = fixture.CheckWorldBoundary(report);
                fixture.Tile.element.updateType = 3;
                fixture.Tile.initialX = .4f;
                fixture.Tile.Tick(7);
                Physics.SyncTransforms();
                report.movingContactWidth = fixture.CheckWorldBoundary(report);
                fixture.Tile.element.id = 2;
                fixture.Box.size = new Vector3(1, .2f, 6);
                Physics.SyncTransforms();
                report.holdSupportWidth = fixture.CheckWorldBoundary(report);

                fixture.Actor.position = fixture.Tile.transform.position + Vector3.right * .6f;
                fixture.SetGameProperty("Phase", ClonePhase.Playing);
                Require(fixture.Tile.ContainsSphere(fixture.Actor.position, .25f), "The trigger bypass fixture is not inside the original broad collider.");
                Require(!fixture.Contains(fixture.Actor.position, .25f), "The trigger bypass fixture unexpectedly lies inside the widened contact width.");
                fixture.Game.TouchTile(fixture.Tile);
                report.triggerCannotBypassWidth = !fixture.Tile.consumed && fixture.Game.Support == null;
                Require(report.triggerCannotBypassWidth, "A direct trigger callback bypassed the widened landing width.");

                fixture.Tile.element.id = 10;
                report.finishWidthUnchanged = fixture.CompareLegacy(report);
                fixture.Tile.element.id = 0;
                fixture.Actor.position = fixture.Tile.transform.position + Vector3.right * .2f;
                float error = fixture.LandingError();
                report.perfectUsesWorldError = !NativeRhythmRules.IsPerfect(error, false, false) &&
                    NativeRhythmRules.IsPerfect(.2f, false, false);
                Require(report.perfectUsesWorldError, "The widened island grants Perfect for an enlarged world-space error.");

                fixture.SetGameProperty("Selected", new SongRecord { id = "a different song" });
                Near(fixture.Game.IslandLateralScale, 1, "An inactive island changed another song's input/collision rules.");
                report.legacyUnchanged &= fixture.CompareLegacy(report);
            }
            report.desktopEdgeReversal = CheckDesktopEdgeReversal();
            report.passed = report.legacyUnchanged && report.fixedWorldContactWidth && report.movingContactWidth &&
                report.holdSupportWidth && report.triggerCannotBypassWidth && report.finishWidthUnchanged &&
                report.perfectUsesWorldError && report.desktopEdgeReversal;
            string folder = Path.GetFullPath("Library/IslandPilotReview");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "wide-input-checks.json"), JsonUtility.ToJson(report, true));
            Require(report.passed, "ISLAND_WIDE_INPUT failed: " + JsonUtility.ToJson(report));
            Debug.Log("ISLAND_WIDE_INPUT_PASS native limits map to +/-2.85m; real collider widths, triggers, world Perfect and immediate desktop edge reversal.");
        }

        private static bool CheckDesktopEdgeReversal()
        {
            MethodInfo clamp = typeof(OriginalPlayController).GetMethod("ClampIslandPointerTarget", BindingFlags.Static | BindingFlags.NonPublic);
            Require(clamp != null, "The widened desktop edge policy is unavailable.");
            float origin = 500, dragOrigin = 0;
            float native = NativeRhythmRules.DragTargetX(900, 1000, false, ref origin, ref dragOrigin);
            Require(native > NativeRhythmRules.HorizontalLimit, "Legacy desktop drag semantics unexpectedly changed.");
            object[] args = { native, 900f, origin, dragOrigin };
            Near((float)clamp.Invoke(null, args), 1.5f, "The expanded desktop target passes its positive edge.");
            origin = (float)args[2]; dragOrigin = (float)args[3];
            Near(origin, 900, "Desktop overshoot was retained in the pointer origin.");
            Near(dragOrigin, 1.5f, "Desktop overshoot did not rebase the drag origin to the lane edge.");
            float reversed = NativeRhythmRules.DragTargetX(890, 1000, false, ref origin, ref dragOrigin);
            Require(reversed < 1.5f && reversed > 1.4f, "Reversing ten pixels after overshoot still has a dead zone.");

            args = new object[] { -2.5f, 40f, 300f, .8f };
            Near((float)clamp.Invoke(null, args), -1.5f, "The expanded desktop target passes its negative edge.");
            Near((float)args[2], 40, "Negative-edge pointer overshoot was retained.");
            Near((float)args[3], -1.5f, "Negative-edge drag origin was not rebased.");
            float touchOrigin = 500, touchDragOrigin = 0;
            Near(NativeRhythmRules.DragTargetX(740, 1000, true, ref touchOrigin, ref touchDragOrigin), 1.5f,
                "Existing touch full-span calibration changed.");
            return true;
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root, ActorObject, TileObject;
            internal readonly OriginalPlayController Game;
            internal readonly IslandStageDefinition Definition;
            internal readonly OriginalTile Tile;
            internal readonly BoxCollider Box;
            internal Transform Actor => ActorObject.transform;
            private readonly MethodInfo contains, landingError;

            internal Fixture()
            {
                Root = new GameObject("Isolated widened input check") { hideFlags = HideFlags.HideAndDontSave };
                Game = Root.AddComponent<OriginalPlayController>();
                var stage = Root.AddComponent<IslandStagePresentation>();
                Definition = ScriptableObject.CreateInstance<IslandStageDefinition>();
                Definition.SongId = "isolated-wide-input";
                typeof(IslandStagePresentation).GetField("definition", Private).SetValue(stage, Definition);
                Game.SetIslandStage(stage);
                SetGameProperty("Selected", new SongRecord { id = Definition.SongId });
                ActorObject = new GameObject("Isolated actor") { hideFlags = HideFlags.HideAndDontSave };
                ActorObject.transform.SetParent(Root.transform);
                SetGameProperty("Actor", ActorObject.transform);
                TileObject = new GameObject("Isolated support") { hideFlags = HideFlags.HideAndDontSave };
                TileObject.transform.SetParent(Root.transform);
                Box = TileObject.AddComponent<BoxCollider>();
                Box.size = new Vector3(1, .2f, 1);
                Tile = TileObject.AddComponent<OriginalTile>();
                Tile.Initialize(Game, new MapElement { id = 0, sourceIndex = 5, pos = Vector3.zero });
                contains = typeof(OriginalPlayController).GetMethod("ContainsTileContact", Private);
                landingError = typeof(OriginalPlayController).GetMethod("LandingError", Private);
                Require(contains != null && landingError != null, "The widened contact policy is unavailable.");
                Physics.SyncTransforms();
            }

            internal void SetGameProperty(string property, object value) =>
                typeof(OriginalPlayController).GetProperty(property).SetValue(Game, value);
            internal bool Contains(Vector3 center, float radius) => (bool)contains.Invoke(Game, new object[] { Tile, center, radius });
            internal float LandingError() => (float)landingError.Invoke(Game, new object[] { Tile });

            internal bool CheckWorldBoundary(Report report)
            {
                // The original 1m-wide box plus .25m sphere accepts .75m X
                // error. A widened chart must accept the same WORLD error.
                float center = Tile.CenterX;
                Vector3 point = Tile.transform.position;
                foreach (float sign in new[] { -1f, 1f })
                {
                    point.x = center + sign * .749f / 1.9f;
                    Require(Contains(point, .25f), "A valid world-width landing was rejected.");
                    point.x = center + sign * .752f / 1.9f;
                    Require(!Contains(point, .25f), "A landing outside the fixed world width was accepted.");
                    report.boundarySamples += 2;
                }
                point = Tile.transform.position + Vector3.up * .36f;
                Require(!Contains(point, .25f), "Horizontal widening enlarged vertical contact tolerance.");
                return true;
            }

            internal bool CompareLegacy(Report report)
            {
                for (int x = -5; x <= 5; x++) for (int y = -2; y <= 2; y++) for (int z = -2; z <= 2; z++)
                {
                    Vector3 point = Tile.transform.position + new Vector3(x * .2f, y * .18f, z * .4f);
                    Require(Contains(point, .25f) == Tile.ContainsSphere(point, .25f), "Legacy or finish collider behavior changed.");
                    report.boundarySamples++;
                }
                return true;
            }

            public void Dispose()
            {
                if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
                if (Definition != null) UnityEngine.Object.DestroyImmediate(Definition);
            }
        }

        private static void Near(float actual, float expected, string message)
        { Require(Mathf.Abs(actual - expected) < .0001f, message + " Expected " + expected + ", got " + actual); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
