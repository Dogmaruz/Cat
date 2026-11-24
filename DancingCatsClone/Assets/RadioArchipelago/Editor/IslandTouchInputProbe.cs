using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DancingCats.Clone.Editor
{
    /// <summary>Menu-only integration probe of the real touchscreen ReadPointer branch; never advances gameplay.</summary>
    public static class IslandTouchInputProbe
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] FieldNames = { "_dragging", "_destinationX", "_pointerOrigin", "_dragOrigin" };
        private static readonly object[] DynamicUpdate = { InputUpdateType.Dynamic };
        private static readonly MethodInfo UpdateInput = typeof(InputSystem).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(InputUpdateType) }, null);

        [Serializable] private sealed class Report
        {
            public bool passed, integrationOnly = true, temporaryReadyGate;
            public bool touchDownAnchored, touchBranchSelected, immediateEdgeReversal, touchReleased;
            public bool controllerStateRestored, phaseRestored, actorUnchanged, clockUnchanged, inputSettingsUnchanged, devicesRestored;
            public float screenWidth, actorAnchorX, targetAfterMove, targetAtEdge, targetAfterReversal;
            public string scope = "Synchronous virtual Touchscreen events and reflection of real ReadPointer in the prepared menu. Ready gate is temporary; no BeginRun, actor movement, audio seeking or gameplay clock advance.";
            public string[] errors;
        }

        public static void Verify(OriginalPlayController game)
        {
            if (game == null || game.Actor == null) throw new ArgumentException("Prepare the game menu before the touch probe.", nameof(game));
            if (game.Phase != ClonePhase.Menu) throw new InvalidOperationException("Run the touch integration probe only in the prepared menu.");
            if (UpdateInput == null) throw new MissingMethodException("Input System dynamic update hook is unavailable.");
            var readPointer = typeof(OriginalPlayController).GetMethod("ReadPointer", PrivateInstance);
            var phase = typeof(OriginalPlayController).GetProperty(nameof(OriginalPlayController.Phase));
            if (readPointer == null || phase == null || phase.GetSetMethod(true) == null)
                throw new MissingMemberException("Controller input/phase bindings are unavailable.");
            var fields = new FieldInfo[FieldNames.Length]; var values = new object[FieldNames.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = typeof(OriginalPlayController).GetField(FieldNames[i], PrivateInstance);
                if (fields[i] == null) throw new MissingFieldException(typeof(OriginalPlayController).FullName, FieldNames[i]);
                values[i] = fields[i].GetValue(game);
            }
            var previousPhase = game.Phase; var position = game.Actor.position; double clock = game.GameTime;
            var settings = InputSystem.settings; var previousTouch = Touchscreen.current; var previousMouse = Mouse.current; var previousKeyboard = Keyboard.current;
            var report = new Report { screenWidth = Screen.width, actorAnchorX = position.x };
            var errors = new List<string>(); Touchscreen touch = null;
            Vector2 pointer = new Vector2(Screen.width * .15f, Screen.height * .55f);
            try
            {
                Require(Screen.width > 0, "The GameView has no valid screen width.");
                Require(position.x >= -1.5f && position.x <= .5f, "The prepared menu anchor must leave room for the +1 touch test.");
                Require(previousMouse == null || !previousMouse.leftButton.isPressed, "The probe requires an unpressed mouse; it never overwrites mouse state.");
                Require(previousTouch == null || !previousTouch.primaryTouch.press.isPressed, "A real touch is already held; the probe will not replace it.");
                Require(!KeyboardSteeringHeld(), "Release keyboard steering before the touch-only probe.");
                // The real presenter blocks Menu input. Ready only opens that
                // gate synchronously; no game update, notification or run starts.
                phase.SetValue(game, ClonePhase.Ready); report.temporaryReadyGate = true;
                Require(game.Presenter == null || !game.Presenter.IsBlockingInput, "A modal still blocks the touchscreen probe.");
                fields[0].SetValue(game, false);
                touch = InputSystem.AddDevice<Touchscreen>("Island touch input integration probe"); touch.MakeCurrent();
                Send(touch, pointer, UnityEngine.InputSystem.TouchPhase.Began);
                Require(touch.primaryTouch.press.isPressed, "Virtual primary touch did not become pressed.");
                readPointer.Invoke(game, null);
                Close(Target(game, fields), position.x, "Touch-down jumped from the actor anchor.");
                Require((bool)fields[0].GetValue(game), "ReadPointer did not enter the dragging state.");
                report.touchDownAnchored = report.touchBranchSelected = true;

                pointer.x = Screen.width * .23f;
                Send(touch, pointer, UnityEngine.InputSystem.TouchPhase.Moved); readPointer.Invoke(game, null);
                report.targetAfterMove = Target(game, fields);
                Close(report.targetAfterMove, position.x + 1f, "+8% screen touch movement did not move the real destination by +1.");

                pointer.x = Screen.width * .75f;
                Send(touch, pointer, UnityEngine.InputSystem.TouchPhase.Moved); readPointer.Invoke(game, null);
                report.targetAtEdge = Target(game, fields); Close(report.targetAtEdge, 1.5f, "Touch overscroll did not clamp the real destination.");
                pointer.x = Screen.width * .73f;
                Send(touch, pointer, UnityEngine.InputSystem.TouchPhase.Moved); readPointer.Invoke(game, null);
                report.targetAfterReversal = Target(game, fields); Close(report.targetAfterReversal, 1.25f, "Reversal did not immediately discard invisible overscroll.");
                report.immediateEdgeReversal = true;

                Send(touch, pointer, UnityEngine.InputSystem.TouchPhase.Ended); readPointer.Invoke(game, null);
                Require(!touch.primaryTouch.press.isPressed && !(bool)fields[0].GetValue(game), "Touch release did not end real controller dragging.");
                Require(Mouse.current == null || !Mouse.current.leftButton.isPressed, "A mouse press contaminated the touch-only probe.");
                report.touchReleased = true;
            }
            catch (Exception error) { errors.Add(error.ToString()); }
            finally
            {
                Restore(() => { if (touch != null && touch.added) InputSystem.RemoveDevice(touch); }, "Remove virtual touch", errors);
                Restore(() => { for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]); }, "Restore controller fields", errors);
                Restore(() => phase.SetValue(game, previousPhase), "Restore menu phase", errors);
                Restore(() => { if (previousTouch != null && previousTouch.added) previousTouch.MakeCurrent(); }, "Restore current touchscreen", errors);
                Restore(() => { if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent(); }, "Restore current mouse", errors);
                Restore(() => { if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent(); }, "Restore current keyboard", errors);
                report.controllerStateRestored = true;
                for (int i = 0; i < fields.Length; i++) report.controllerStateRestored &= Equals(fields[i].GetValue(game), values[i]);
                report.phaseRestored = game.Phase == previousPhase;
                report.actorUnchanged = game.Actor.position == position;
                report.clockUnchanged = game.GameTime == clock;
                report.inputSettingsUnchanged = ReferenceEquals(InputSystem.settings, settings);
                report.devicesRestored = Touchscreen.current == previousTouch && Mouse.current == previousMouse && Keyboard.current == previousKeyboard && (touch == null || !touch.added);
                if (!report.controllerStateRestored || !report.phaseRestored || !report.actorUnchanged || !report.clockUnchanged || !report.inputSettingsUnchanged || !report.devicesRestored)
                    errors.Add("The touch integration probe did not restore all controller/input state.");
                report.errors = errors.ToArray(); report.passed = errors.Count == 0;
                Directory.CreateDirectory(IslandPilotJobs.ReviewPath);
                File.WriteAllText(Path.Combine(IslandPilotJobs.ReviewPath, "touch-input-checks.json"), JsonUtility.ToJson(report, true));
            }
            if (!report.passed) throw new InvalidOperationException("Touch integration probe failed: " + string.Join(" | ", report.errors));
        }

        private static float Target(OriginalPlayController game, FieldInfo[] fields) => (float)fields[1].GetValue(game);
        private static void Close(float actual, float expected, string message) { Require(Mathf.Abs(actual - expected) < .0001f, message + " actual=" + actual + ", expected=" + expected); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Restore(Action action, string label, List<string> errors) { try { action(); } catch (Exception error) { errors.Add(label + ": " + error); } }
        private static bool KeyboardSteeringHeld()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed || keyboard.aKey.isPressed || keyboard.dKey.isPressed);
        }
        private static void Send(Touchscreen touch, Vector2 position, UnityEngine.InputSystem.TouchPhase phase)
        {
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 741, phase = phase, position = position, pressure = phase == UnityEngine.InputSystem.TouchPhase.Ended ? 0 : 1 });
            UpdateInput.Invoke(null, DynamicUpdate);
        }
    }
}
