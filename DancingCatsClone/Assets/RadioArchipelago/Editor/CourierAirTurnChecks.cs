using System;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Measures the rendered visual heading throughout actual-duration air arcs.</summary>
    public static class CourierAirTurnChecks
    {
        public static void Run()
        {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                using (var fixture = new Fixture())
                {
                    foreach (float duration in new[] { .65f, 1.6f, 2.4f })
                    {
                        fixture.Motion.ResetMotion();
                        float first = Flight(fixture, duration, fps);
                        float second = Flight(fixture, duration, fps);
                        Require(Mathf.Abs(first + 360) < .5f && Mathf.Abs(second - 360) < .5f,
                            "Long air arcs must each turn once, alternating with takeoff footing: "
                            + duration + " s at " + fps + " fps; turns=" + first + ", " + second + ".");
                        fixture.Motion.PlayLanding();
                        Step(fixture, .1f, fps);
                        Require(Mathf.Abs(fixture.Heading()) < .1f,
                            "A completed air turn must land at its original heading.");
                    }
                    foreach (float duration in new[] { .30f, .45f, .649f })
                    {
                        fixture.Motion.ResetMotion();
                        Require(Mathf.Abs(Flight(fixture, duration, fps)) < .5f,
                            "An ordinary air arc must retain its small alternating hop yaw.");
                    }
                    fixture.Motion.ResetMotion();
                    Require(Mathf.Abs(Flight(fixture, 1.6f, fps, false) + 360) < .5f,
                        "Elapsed-time fallback must also complete one long air turn.");
                }
            }
            CheckProgressAndPause();
            CheckSupportedTurns();
            Debug.Log("RADIO_AIR_TURNS PASS: one full long-flight turn at 30/60/120 fps; actual progress, alternating takeoff direction, ordinary hops, pause, contact heading, root/collider and supported turns preserved.");
        }

        private static float Flight(Fixture fixture, float duration, int fps, bool external = true)
        {
            fixture.Motion.PlayJump(duration);
            float before = fixture.Heading(), sum = 0, elapsed = 0;
            while (elapsed < duration - .000001f)
            {
                float dt = Mathf.Min(1f / fps, duration - elapsed);
                elapsed += dt;
                if (external) fixture.Motion.SetFlightProgress(elapsed / duration);
                fixture.Motion.SimulateSprings(dt);
                float after = fixture.Heading();
                float change = Mathf.DeltaAngle(before, after);
                if (duration >= .65f)
                    Require(sum <= 0 ? change <= .1f || sum == 0 : change >= -.1f,
                        "A long air turn must progress smoothly in one direction.");
                sum += change; before = after;
                fixture.RequireGameplayRest();
            }
            return sum;
        }

        private static void CheckProgressAndPause()
        {
            using (var fixture = new Fixture())
            {
                fixture.Motion.PlayJump(1.6f);
                fixture.Motion.SetFlightProgress(.25f);
                Step(fixture, .05f, 60);
                // SmoothStep(.25) = .15625: 56.25 degrees plus the
                // preserved eight-degree quarter-arc hop gesture.
                Require(Mathf.Abs(fixture.Heading() + 64.25f) < .2f,
                    "Long-flight yaw must follow measured arc progress, including its existing hop gesture.");
                float quarter = fixture.Heading();
                Step(fixture, .4f, 60);
                Require(Mathf.Abs(Mathf.DeltaAngle(quarter, fixture.Heading())) < .1f,
                    "An externally sampled flight must not run a second turn clock.");
                Quaternion frozen = fixture.Visual.localRotation;
                Vector3 frozenPosition = fixture.Visual.localPosition, frozenScale = fixture.Visual.localScale;
                fixture.Motion.SetPaused(true);
                Step(fixture, .5f, 30);
                Require(Quaternion.Angle(frozen, fixture.Visual.localRotation) < .01f
                    && fixture.Visual.localPosition == frozenPosition && fixture.Visual.localScale == frozenScale,
                    "Pausing must freeze the complete air pose.");
                fixture.Motion.SetPaused(false);
                fixture.Motion.SetFlightProgress(1);
                Step(fixture, 1f / 60, 60);
                Require(Mathf.Abs(fixture.Heading()) < .1f, "Resumed progress must finish at the original heading.");
                fixture.Motion.PlayLanding();
                fixture.Motion.PlayJump(.3f);
                fixture.Motion.SetFlightProgress(.5f);
                Step(fixture, 1f / 60, 60);
                Require(Mathf.Abs(fixture.Heading()) < 30,
                    "A short hop after a completed air turn must not inherit an extra rotation.");
                fixture.Motion.ResetMotion();
                Require(Quaternion.Angle(fixture.RestRotation, fixture.Visual.localRotation) < .01f,
                    "Reset must restore the authored visual orientation.");
            }
        }

        private static void CheckSupportedTurns()
        {
            using (var fixture = new Fixture())
            {
                foreach (bool left in new[] { false, true })
                {
                    fixture.Motion.ResetMotion();
                    fixture.Motion.PlayHold(left, .65f, true);
                    float before = fixture.Heading(), sum = 0;
                    for (int frame = 0; frame < 78; frame++)
                    {
                        fixture.Motion.SimulateSprings(.65f / 78);
                        float after = fixture.Heading();
                        sum += Mathf.DeltaAngle(before, after); before = after;
                        fixture.RequireGameplayRest();
                    }
                    Require(Mathf.Abs(sum - (left ? -360 : 360)) < .5f,
                        "Supported-tile turns must keep their existing duration and direction.");
                }
            }
        }

        private static void Step(Fixture fixture, float duration, int fps)
        {
            while (duration > .000001f)
            {
                float dt = Mathf.Min(1f / fps, duration);
                fixture.Motion.SimulateSprings(dt); fixture.RequireGameplayRest(); duration -= dt;
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly CapsuleCollider collider;
            private readonly Vector3 rootPosition, rootScale;
            private readonly Quaternion rootRotation;
            public readonly CourierMotion Motion;
            public readonly Transform Visual;
            public readonly Quaternion RestRotation;

            public Fixture()
            {
                root = new GameObject("Courier air turn check") { hideFlags = HideFlags.HideAndDontSave };
                root.transform.SetPositionAndRotation(new Vector3(11, 2, -3), Quaternion.Euler(5, 27, -3));
                root.transform.localScale = Vector3.one * .47f;
                rootPosition = root.transform.position; rootRotation = root.transform.rotation; rootScale = root.transform.localScale;
                collider = root.AddComponent<CapsuleCollider>(); collider.radius = .34f; collider.height = 1.5f;
                Visual = new GameObject("visual").transform; Visual.SetParent(root.transform, false);
                RestRotation = Visual.localRotation = Quaternion.Euler(6, 17, -4);
                Visual.localScale = new Vector3(1.2f, .9f, 1.1f);
                Motion = root.AddComponent<CourierMotion>(); Motion.ManualSimulation = true;
                Motion.Configure(Visual, null, null, null, null);
            }

            public float Heading()
            {
                Vector3 forward = Quaternion.Inverse(RestRotation) * Visual.localRotation * Vector3.forward;
                return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }

            public void RequireGameplayRest()
            {
                Require(root.transform.position == rootPosition && root.transform.localScale == rootScale
                    && Quaternion.Angle(root.transform.rotation, rootRotation) < .01f,
                    "An air turn changed the gameplay root.");
                Require(collider.radius == .34f && collider.height == 1.5f && collider.enabled,
                    "An air turn changed the gameplay collider.");
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
