using System;
using System.Reflection;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>The reward passage faces forward without spinning or moving gameplay geometry.</summary>
    public static class CourierFinishHoverChecks
    {
        public static void Run()
        {
            foreach (int fps in new[] { 30, 60, 120 })
                using (var fixture = new Fixture())
                {
                    fixture.EnterFinishHover();
                    float first = Turn(fixture, 2, fps);
                    float second = Turn(fixture, 2, fps);
                    float third = Turn(fixture, 2, fps);
                    Require(Mathf.Abs(first) < .1f && Mathf.Abs(second) < .1f && Mathf.Abs(third) < .1f,
                        "Finish collection must remain forward-facing without continuous axial turns; " + fps +
                        " fps: " + first + ", " + second + ", " + third + ".");
                    Require(fixture.MaximumBob - fixture.MinimumBob > .015f && fixture.MaximumBob < .12f && fixture.MinimumBob > -.03f,
                        "Finish hover needs a gentle continuing bob rather than a grounded running/jumping animation.");
                    fixture.Motion.SetPaused(true);
                    var paused = fixture.Snapshot();
                    fixture.Motion.SimulateSprings(.2f);
                    fixture.Motion.SimulateSprings(.2f);
                    fixture.RequireSnapshot(paused, "Pausing must freeze the complete finish hover.");
                    fixture.Motion.SetPaused(false);
                    fixture.Motion.SetFacing(25);
                    Require(Mathf.Abs(Turn(fixture, 2, fps)) < .1f,
                        "Resumed collection must still face forward even while the gameplay pointer steers laterally.");
                    fixture.Motion.PlayFinishDeparture();
                    Require(fixture.Motion.IsFinishDeparture && !fixture.Motion.IsFinishHover,
                        "The final departure must own a separate flight pose after collection.");
                    Require(Mathf.Abs(Turn(fixture, 1, fps)) < .1f,
                        "The outgoing leap must keep looking forward without an axial turn.");
                    fixture.Motion.ResetMotion();
                    fixture.RequireSnapshot(fixture.Rest, "Reset must restore the authored visual and clear finish rotation.");
                    fixture.Motion.PlayIdle();
                    Require(Mathf.Abs(Turn(fixture, 2, fps)) < 5,
                        "Starting idle again must not inherit continuous finish rotation.");
                }
            CheckEntryContinuity();
            Debug.Log("RADIO_FINISH_HOVER PASS: forward-facing collection and departure at 30/60/120 fps, gentle hover, shortest entry settle, pause, restart and unchanged root/collider.");
        }

        private static void CheckEntryContinuity()
        {
            using (var fixture = new Fixture())
            {
                fixture.Motion.PlayJump(1.6f);
                fixture.Motion.SetFlightProgress(.8f);
                fixture.Motion.SimulateSprings(.08f);
                float before = fixture.Heading();
                fixture.EnterFinishHover();
                Require(Mathf.Abs(Mathf.DeltaAngle(before, fixture.Heading())) < .01f,
                    "Entering finish collection must not snap the rendered heading.");
                fixture.Motion.SimulateSprings(1f / 120);
                Require(Mathf.Abs(Mathf.DeltaAngle(before, fixture.Heading())) < 2,
                    "The previous air pose must settle smoothly rather than snap to the forward heading.");
                for (int i = 0; i < 60; i++) fixture.Motion.SimulateSprings(1f / 120);
                Require(Mathf.Abs(fixture.Heading()) < .1f,
                    "After the short entry settle the courier must look forward along the collection path.");
                fixture.RequireGameplayRest();
            }
        }

        private static float Turn(Fixture fixture, float seconds, int fps)
        {
            float elapsed = 0, previous = fixture.Heading(), sum = 0;
            while (elapsed < seconds - .000001f)
            {
                float dt = Mathf.Min(1f / fps, seconds - elapsed);
                fixture.Motion.SimulateSprings(dt); elapsed += dt;
                float next = fixture.Heading(), delta = Mathf.DeltaAngle(previous, next);
                Require(Mathf.Abs(delta) < .06f,
                    "Settled finish yaw must remain forward rather than continuing a turn.");
                sum += delta; previous = next;
                float bob = fixture.Visual.localPosition.y - fixture.Rest.Position.y;
                fixture.MinimumBob = Mathf.Min(fixture.MinimumBob, bob);
                fixture.MaximumBob = Mathf.Max(fixture.MaximumBob, bob);
                fixture.RequireGameplayRest();
            }
            return sum;
        }

        private readonly struct PoseSnapshot
        {
            internal readonly Vector3 Position, Scale;
            internal readonly Quaternion Rotation;
            internal PoseSnapshot(Transform transform)
            { Position = transform.localPosition; Rotation = transform.localRotation; Scale = transform.localScale; }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly CapsuleCollider collider;
            private readonly Vector3 rootPosition, rootScale;
            private readonly Quaternion rootRotation;
            internal readonly CourierMotion Motion;
            internal readonly Transform Visual;
            internal readonly PoseSnapshot Rest;
            internal float MinimumBob = float.PositiveInfinity, MaximumBob = float.NegativeInfinity;

            internal Fixture()
            {
                root = new GameObject("Courier finish hover check") { hideFlags = HideFlags.HideAndDontSave };
                root.transform.SetPositionAndRotation(new Vector3(11, 2, -3), Quaternion.Euler(5, 27, -3));
                root.transform.localScale = new Vector3(.47f, .55f, .51f);
                rootPosition = root.transform.position; rootRotation = root.transform.rotation; rootScale = root.transform.localScale;
                collider = root.AddComponent<CapsuleCollider>(); collider.radius = .34f; collider.height = 1.5f;
                Visual = new GameObject("visual").transform; Visual.SetParent(root.transform, false);
                Visual.localPosition = new Vector3(.1f, .2f, -.1f);
                Visual.localRotation = Quaternion.Euler(0, 17, 0);
                Visual.localScale = new Vector3(1.2f, .9f, 1.1f); Rest = new PoseSnapshot(Visual);
                Motion = root.AddComponent<CourierMotion>(); Motion.ManualSimulation = true;
                Motion.Configure(Visual, null, null, null, null);
            }

            internal void EnterFinishHover()
            {
                var enter = typeof(CourierMotion).GetMethod("PlayFinishHover", BindingFlags.Public | BindingFlags.Instance);
                // The old finish celebration is the reproduction until the explicit finish pose is present.
                if (enter == null) Motion.PlayEnd(); else enter.Invoke(Motion, null);
            }

            internal float Heading()
            {
                Vector3 forward = Quaternion.Inverse(Rest.Rotation) * Visual.localRotation * Vector3.forward;
                return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }

            internal PoseSnapshot Snapshot() => new PoseSnapshot(Visual);
            internal void RequireSnapshot(PoseSnapshot expected, string message)
            {
                Require((Visual.localPosition - expected.Position).sqrMagnitude < .00000001f
                    && (Visual.localScale - expected.Scale).sqrMagnitude < .00000001f
                    && Quaternion.Angle(Visual.localRotation, expected.Rotation) < .01f, message);
                RequireGameplayRest();
            }

            internal void RequireGameplayRest()
            {
                Require(root.transform.position == rootPosition && root.transform.localScale == rootScale
                    && Quaternion.Angle(root.transform.rotation, rootRotation) < .01f,
                    "Finish hover must not move or rotate the gameplay root.");
                Require(collider.radius == .34f && collider.height == 1.5f && collider.enabled,
                    "Finish hover must not alter gameplay contacts or reward colliders.");
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
