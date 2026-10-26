using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Checks actual presentation transforms, independent of CourierMotion's internal pose fields.</summary>
    public static class CourierHopChecks
    {
        private static readonly int[] FrameRates = { 30, 60, 120 };

        [MenuItem("Radio Archipelago/Imported couriers/Check alternating hops")]
        public static void Run()
        {
            var failures = new List<string>();
            foreach (int fps in FrameRates)
            {
                int rate = fps;
                Check("alternating ordinary hops at " + rate + " fps", () => CheckAlternatingHops(rate), failures);
                foreach (float duration in new[] { .30f, .45f, .65f })
                {
                    float seconds = duration;
                    Check("in-flight deformation, " + seconds.ToString("F2") + "s at " + rate + " fps",
                        () => CheckFlightDeformation(seconds, rate), failures);
                }
            }
            Check("elapsed-time ordinary hops", () => CheckAlternatingHops(60, false), failures);
            Check("contact squash survives immediate short-hop takeoff", CheckContactSquash, failures);
            Check("phase agreement across frame rates", CheckFrameRateAgreement, failures);
            Check("pause, reset and sequence restart", CheckPauseAndReset, failures);
            Check("role rebinding restores both rigs", CheckRebinding, failures);
            Check("long flight keeps arms and secondary motion", CheckLongFlight, failures);
            if (failures.Count > 0)
                throw new InvalidOperationException("RADIO_HOPS FAIL (" + failures.Count + "):\n" + string.Join("\n", failures));
            Debug.Log("RADIO_HOPS PASS: first-left/next-right body lean and independent feet; visible volume-preserving flight squash/stretch at 30/60/120 fps; contact squash survives immediate takeoff; rear-view yaw variation; unchanged gameplay root/collider; pause, reset, rebinding and sustained long-flight gestures.");
        }

        private static void CheckAlternatingHops(int fps, bool externalProgress = true)
        {
            using (var fixture = new Fixture())
            {
                var samples = new HopPose[4];
                var failures = new List<string>();
                for (int hop = 0; hop < samples.Length; hop++)
                {
                    float duration = hop % 2 == 0 ? .30f : .45f;
                    fixture.Motion.PlayJump(duration);
                    float time = 0;
                    AdvanceFlight(fixture, duration, ref time, .5f, fps, externalProgress);
                    samples[hop] = ReadPose(fixture.Rig);
                    Check("hop " + (hop + 1), () => CheckHopSide(samples[hop], hop % 2 == 0, "pose"), failures);
                    AdvanceFlight(fixture, duration, ref time, 1, fps, externalProgress);

                    // Contacts and progress checkpoints must not consume the next takeoff side.
                    fixture.Motion.PlayLanding(.9f);
                    Step(fixture, .08f, fps);
                    fixture.Motion.PlayLanding(.7f);
                    fixture.Motion.SetFlightProgress(.9f);
                    fixture.Motion.SetFlightProgress(1);
                    Step(fixture, .02f, fps);
                }
                Check("rear silhouette yaw", () =>
                {
                    Require(Mathf.Abs(Mathf.DeltaAngle(samples[0].yaw, samples[1].yaw)) >= 10,
                        "Alternating ordinary hops need at least 10 degrees of combined local yaw variation so the rear silhouette changes; got "
                        + Mathf.Abs(Mathf.DeltaAngle(samples[0].yaw, samples[1].yaw)).ToString("F2") + ".");
                    Require(Mathf.Abs(samples[0].yaw) < 30 && Mathf.Abs(samples[1].yaw) < 30,
                        "An ordinary hop should use a small facing variation.");
                }, failures);
                Require(failures.Count == 0, string.Join("\n", failures));
            }
        }

        private static void CheckContactSquash()
        {
            using (var fixture = new Fixture())
            {
                const float duration = .30f, frameTime = 1f / 60;
                fixture.Motion.PlayJump(duration);
                float time = 0;
                AdvanceFlight(fixture, duration, ref time, 1, 60);

                // The session emits contact and next takeoff together on ordinary consecutive tiles.
                fixture.Motion.PlayLanding(.65f);
                fixture.Motion.PlayJump(duration);
                int compressedFrames = 0;
                var observed = new List<string>();
                for (int frame = 1; frame * frameTime <= .08f; frame++)
                {
                    fixture.Motion.SetFlightProgress(frame * frameTime / duration);
                    fixture.Motion.SimulateSprings(frameTime);
                    fixture.RequireGameplayRest();
                    Vector3 ratio = ScaleRatio(fixture.Rig);
                    observed.Add(ratio.y.ToString("F3"));
                    if (ratio.y <= .90f) compressedFrames++;
                    Require(Mathf.Abs(ratio.x * ratio.y * ratio.z - 1) <= .002f,
                        "Contact squash during the next takeoff must preserve authored body volume.");
                }
                Require(compressedFrames >= 2,
                    "Contact followed immediately by a .30s hop must keep at least two rendered 60-fps samples at scaleY <= .90 in the first 80ms; observed "
                    + string.Join(", ", observed) + ".");
            }
        }

        private static void CheckFlightDeformation(float duration, int fps)
        {
            using (var fixture = new Fixture())
            {
                fixture.Motion.PlayJump(duration);
                float time = 0, low = float.PositiveInfinity, high = float.NegativeInfinity;
                float middleLow = float.PositiveInfinity, middleHigh = float.NegativeInfinity;
                while (time < duration - .000001f)
                {
                    float dt = Mathf.Min(1f / fps, duration - time);
                    time += dt;
                    fixture.Motion.SetFlightProgress(time / duration);
                    fixture.Motion.SimulateSprings(dt);
                    fixture.RequireGameplayRest();
                    Vector3 ratio = ScaleRatio(fixture.Rig);
                    Require(ratio.x > 0 && ratio.y > 0 && ratio.z > 0,
                        "In-flight presentation scales must remain positive.");
                    Require(Mathf.Abs(ratio.x * ratio.y * ratio.z - 1) <= .002f,
                        "In-flight squash/stretch must preserve authored body volume; ratio="
                        + (ratio.x * ratio.y * ratio.z).ToString("F5") + ".");
                    float progress = time / duration;
                    if (progress >= .05f && progress <= .95001f)
                    {
                        low = Mathf.Min(low, ratio.y); high = Mathf.Max(high, ratio.y);
                    }
                    if (progress >= .2f && progress <= .85001f)
                    {
                        middleLow = Mathf.Min(middleLow, ratio.y); middleHigh = Mathf.Max(middleHigh, ratio.y);
                    }
                }
                Require(high - low >= .18f,
                    "Normal flight must visibly change body height without requiring PlayLanding; scaleY ratio range="
                    + (high - low).ToString("F3") + " (need .18).");
                Require(high >= 1.07f && low <= .97f,
                    "Flight itself needs both positive stretch and compression; min=" + low.ToString("F3")
                    + ", max=" + high.ToString("F3") + ".");
                Require(middleHigh - middleLow >= .10f,
                    "Body deformation must continue through the flight, beyond a takeoff-only pulse; middle range="
                    + (middleHigh - middleLow).ToString("F3") + ".");
            }
        }

        private static void CheckFrameRateAgreement()
        {
            foreach (float duration in new[] { .30f, .45f, .65f })
            {
                HopPose[] reference = FlightSamples(duration, 120);
                foreach (int fps in new[] { 30, 60 })
                {
                    HopPose[] actual = FlightSamples(duration, fps);
                    for (int i = 0; i < reference.Length; i++)
                    {
                        Require(Mathf.Abs(actual[i].lean - reference[i].lean) < 3
                            && Mathf.Abs(Mathf.DeltaAngle(actual[i].yaw, reference[i].yaw)) < 3,
                            "Equal musical flight phases must keep body lean/yaw close at " + fps + " and 120 fps.");
                        Require(Mathf.Abs(actual[i].scale.y - reference[i].scale.y) < .045f,
                            "Equal flight phases must keep visible deformation close across frame rates.");
                        Require(Mathf.Abs(actual[i].leftLift - reference[i].leftLift) < .012f
                            && Mathf.Abs(actual[i].rightLift - reference[i].rightLift) < .012f,
                            "The alternating foot pose must remain stable across frame rates.");
                    }
                }
            }
        }

        private static HopPose[] FlightSamples(float duration, int fps)
        {
            using (var fixture = new Fixture())
            {
                var samples = new HopPose[3];
                fixture.Motion.PlayJump(duration);
                float time = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    AdvanceFlight(fixture, duration, ref time, (i + 1) * .25f, fps);
                    samples[i] = ReadPose(fixture.Rig);
                }
                return samples;
            }
        }

        private static void CheckPauseAndReset()
        {
            using (var fixture = new Fixture())
            {
                fixture.Motion.PlayJump(.45f);
                float time = 0;
                AdvanceFlight(fixture, .45f, ref time, .5f, 60);
                CheckHopSide(ReadPose(fixture.Rig), true, "first hop before pause");
                TransformPose[] frozen = Capture(fixture.Rig.Nodes);
                fixture.Motion.SetPaused(true);
                Step(fixture, .5f, 30);
                Same(frozen, fixture.Rig.Nodes, "Paused flight changed a presentation transform");
                fixture.Motion.SetPaused(false);
                fixture.Motion.PlayJump(.45f);
                time = 0;
                AdvanceFlight(fixture, .45f, ref time, .5f, 60);
                CheckHopSide(ReadPose(fixture.Rig), false, "next hop after pause");

                fixture.Motion.ResetMotion();
                fixture.Rig.RequireRest();
                fixture.RequireGameplayRest();
                fixture.Motion.SimulateSprings(float.NaN);
                fixture.Motion.SimulateSprings(float.PositiveInfinity);
                fixture.Motion.SimulateSprings(-1);
                fixture.Rig.RequireRest();
                fixture.Motion.PlayJump(.45f);
                time = 0;
                AdvanceFlight(fixture, .45f, ref time, .5f, 60);
                CheckHopSide(ReadPose(fixture.Rig), true, "first hop after ResetMotion");
            }
        }

        private static void CheckRebinding()
        {
            using (var fixture = new Fixture())
            {
                Rig oldRig = fixture.Rig;
                fixture.Motion.PlayJump(.45f);
                Step(fixture, .2f, 60);
                fixture.Rebind();
                oldRig.RequireRest();
                fixture.Rig.RequireRest();
                float time = 0;
                fixture.Motion.PlayJump(.45f);
                AdvanceFlight(fixture, .45f, ref time, .5f, 60);
                CheckHopSide(ReadPose(fixture.Rig), true, "first hop on replacement rig");
                oldRig.RequireRest();
                fixture.Motion.ResetMotion();
                oldRig.RequireRest();
                fixture.Rig.RequireRest();

                // Reusing the same role binding also resets sequence without capturing an animated rest pose.
                fixture.Motion.PlayJump(.30f);
                Step(fixture, .15f, 60);
                fixture.Motion.ConfigureRig(fixture.Rig.Binding);
                fixture.Rig.RequireRest();
                fixture.Motion.PlayJump(.30f);
                time = 0;
                AdvanceFlight(fixture, .30f, ref time, .5f, 60);
                CheckHopSide(ReadPose(fixture.Rig), true, "first hop after rebinding the same roles");
            }
        }

        private static void CheckLongFlight()
        {
            using (var fixture = new Fixture())
            {
                fixture.Motion.PlayJump(2.4f);
                float time = 0;
                AdvanceFlight(fixture, 2.4f, ref time, .375f, 60);
                float leftArm = Quaternion.Angle(fixture.Rig.Rest[3].rotation, fixture.Rig.LeftArm.localRotation);
                float rightArm = Quaternion.Angle(fixture.Rig.Rest[4].rotation, fixture.Rig.RightArm.localRotation);
                Require(leftArm > 15 && rightArm > 15,
                    "Both arms must still gesture .9 seconds into a long flight, after an ordinary jump clip would end.");
                float greatestBend = 0;
                for (int sample = 0; sample < 36; sample++)
                {
                    AdvanceFlight(fixture, 2.4f, ref time, (.9f + (sample + 1) / 60f) / 2.4f, 60);
                    foreach (Transform part in fixture.Rig.Secondary)
                    {
                        int index = Array.IndexOf(fixture.Rig.Nodes, part);
                        greatestBend = Mathf.Max(greatestBend, Quaternion.Angle(fixture.Rig.Rest[index].rotation, part.localRotation));
                    }
                }
                Require(greatestBend > 8, "Long flight must retain visible secondary motion on the bound antenna/scarf controls.");
                fixture.Motion.ResetMotion();
                fixture.Rig.RequireRest();
            }
        }

        private static void CheckHopSide(HopPose pose, bool left, string context)
        {
            var failures = new List<string>();
            if (!(left ? pose.lean <= -5 : pose.lean >= 5))
                failures.Add(context + ": the body top must visibly lean " + (left ? "left" : "right")
                    + " in its authored visual frame; lean=" + pose.lean.ToString("F2") + " degrees.");
            float support = left ? pose.leftLift : pose.rightLift;
            float free = left ? pose.rightLift : pose.leftLift;
            if (!(free - support >= .025f))
                failures.Add(context + ": the " + (left ? "left" : "right")
                    + " support foot must stay lower than the independent free foot; left lift="
                    + pose.leftLift.ToString("F3") + ", right lift=" + pose.rightLift.ToString("F3") + ".");
            Require(failures.Count == 0, string.Join("\n", failures));
        }

        private static HopPose ReadPose(Rig rig)
        {
            // Remove the presentation parent's transform and then the authored visual rotation.
            // Measuring markers prevents a world axis or a rotated FBX pivot from masquerading as a lean.
            Vector3 up = RestVisualVector(rig, rig.TopMarker);
            Vector3 forward = RestVisualVector(rig, rig.FrontMarker);
            return new HopPose
            {
                lean = Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg,
                yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
                leftLift = rig.Visual.InverseTransformPoint(rig.LeftFoot.position).y - rig.Rest[5].position.y,
                rightLift = rig.Visual.InverseTransformPoint(rig.RightFoot.position).y - rig.Rest[6].position.y,
                scale = ScaleRatio(rig)
            };
        }

        private static Vector3 RestVisualVector(Rig rig, Transform marker)
        {
            Transform parent = rig.Visual.parent;
            Vector3 displacement = parent.InverseTransformPoint(marker.position) - parent.InverseTransformPoint(rig.Visual.position);
            return Quaternion.Inverse(rig.Rest[0].rotation) * displacement;
        }

        private static Vector3 ScaleRatio(Rig rig)
        {
            Vector3 scale = rig.Visual.localScale, rest = rig.Rest[0].scale;
            return new Vector3(scale.x / rest.x, scale.y / rest.y, scale.z / rest.z);
        }

        private static void AdvanceFlight(Fixture fixture, float duration, ref float time, float phase, int fps, bool externalProgress = true)
        {
            float end = duration * phase;
            while (time < end - .000001f)
            {
                float dt = Mathf.Min(1f / fps, end - time);
                time += dt;
                if (externalProgress) fixture.Motion.SetFlightProgress(time / duration);
                fixture.Motion.SimulateSprings(dt);
                fixture.RequireGameplayRest();
            }
        }

        private static void Step(Fixture fixture, float seconds, int fps)
        {
            while (seconds > .000001f)
            {
                float dt = Mathf.Min(1f / fps, seconds);
                fixture.Motion.SimulateSprings(dt);
                fixture.RequireGameplayRest();
                seconds -= dt;
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly CourierMotion Motion;
            public Rig Rig;
            private readonly CapsuleCollider collider;
            private readonly TransformPose[] rootRest;
            private readonly Vector3 colliderCenter;
            private readonly float colliderHeight, colliderRadius;

            public Fixture()
            {
                Root = new GameObject("Courier hop checks") { hideFlags = HideFlags.HideAndDontSave };
                Root.transform.SetPositionAndRotation(new Vector3(11, 2, -3), Quaternion.Euler(5, 27, -3));
                Root.transform.localScale = Vector3.one * .47f;
                collider = Root.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(.02f, .65f, -.03f); collider.height = 1.5f; collider.radius = .34f;
                colliderCenter = collider.center; colliderHeight = collider.height; colliderRadius = collider.radius;
                rootRest = Capture(Root.transform);
                Rig = new Rig(Root.transform, false);
                Motion = Root.AddComponent<CourierMotion>();
                Motion.ManualSimulation = true;
                Motion.ConfigureRig(Rig.Binding);
            }

            public void Rebind()
            {
                Rig = new Rig(Root.transform, true);
                Motion.ConfigureRig(Rig.Binding);
            }

            public void RequireGameplayRest()
            {
                Same(rootRest, new[] { Root.transform }, "Presentation animation changed the gameplay root");
                Require(collider.center == colliderCenter && collider.height == colliderHeight
                    && collider.radius == colliderRadius && collider.direction == 1 && collider.enabled,
                    "Presentation animation changed the gameplay collider.");
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }

        private sealed class Rig
        {
            public readonly Transform Visual, TopMarker, FrontMarker, LeftArm, RightArm, LeftFoot, RightFoot;
            public readonly Transform[] Nodes, Secondary;
            public readonly TransformPose[] Rest;
            public readonly CourierRigBinding Binding;

            public Rig(Transform root, bool replacement)
            {
                Visual = Child(root, replacement ? "replacement visual" : "visual", new Vector3(.03f, -.02f, .01f));
                Visual.localRotation = replacement ? Quaternion.Euler(-7, -21, 5) : Quaternion.Euler(6, 17, -4);
                Visual.localScale = replacement ? new Vector3(.83f, 1.13f, .95f) : new Vector3(1.2f, .9f, 1.1f);
                TopMarker = Child(Visual, "body top measurement", Vector3.up * 1.5f);
                FrontMarker = Child(Visual, "front measurement", Vector3.forward * .5f);
                Transform head = Child(Visual, "head", Vector3.up * 1.15f);
                head.localRotation = Quaternion.Euler(3, -8, 2);
                LeftArm = Child(Visual, "left shoulder", new Vector3(-.45f, .9f, 0));
                RightArm = Child(Visual, "right shoulder", new Vector3(.45f, .9f, 0));
                LeftArm.localRotation = Quaternion.Euler(90, 17, 35);
                RightArm.localRotation = Quaternion.Euler(-90, -23, -41);
                LeftFoot = Child(Visual, "left foot", new Vector3(-.28f, .07f, .05f));
                RightFoot = Child(Visual, "right foot", new Vector3(.28f, .07f, .05f));
                LeftFoot.localRotation = Quaternion.Euler(83, 11, -7);
                RightFoot.localRotation = Quaternion.Euler(-88, -13, 9);
                Transform antenna = Child(Visual, "antenna", new Vector3(0, 1.45f, -.03f));
                antenna.localRotation = Quaternion.Euler(90, 0, 17);
                Transform scarf = Child(Visual, "scarf", new Vector3(.12f, .83f, -.1f));
                scarf.localRotation = Quaternion.Euler(0, 0, 90);
                Transform scarfTip = Child(scarf, "scarf tip", Vector3.down * .24f);
                Secondary = new[] { antenna, scarf, scarfTip };
                // Stable order permits the rest snapshots to name each public role, never private motion state.
                Nodes = new[] { Visual, TopMarker, head, LeftArm, RightArm, LeftFoot, RightFoot, antenna, scarf, scarfTip, FrontMarker };
                Rest = Capture(Nodes);
                Binding = new CourierRigBinding
                {
                    visualPivot = Visual, headPivot = head,
                    leftArm = LeftArm, rightArm = RightArm, leftFoot = LeftFoot, rightFoot = RightFoot,
                    antennaChain = new[] { antenna }, scarfChain = new[] { scarf, scarfTip }
                };
            }

            public void RequireRest() => Same(Rest, Nodes, "A bound rig failed to restore its own authored rest transform");
        }

        private struct HopPose
        {
            public float lean, yaw, leftLift, rightLift;
            public Vector3 scale;
        }

        private struct TransformPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.localPosition = position;
            return child;
        }

        private static TransformPose[] Capture(params Transform[] nodes)
        {
            var result = new TransformPose[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                result[i] = new TransformPose { position = nodes[i].localPosition, rotation = nodes[i].localRotation, scale = nodes[i].localScale };
            return result;
        }

        private static void Same(TransformPose[] expected, Transform[] nodes, string message)
        {
            for (int i = 0; i < nodes.Length; i++)
                Require((nodes[i].localPosition - expected[i].position).sqrMagnitude < 1e-9f
                    && (nodes[i].localScale - expected[i].scale).sqrMagnitude < 1e-9f
                    && Quaternion.Angle(nodes[i].localRotation, expected[i].rotation) < .03f,
                    message + ": " + nodes[i].name + ".");
        }

        private static void Check(string name, Action check, List<string> failures)
        {
            try { check(); }
            catch (Exception error) { failures.Add(name + ": " + error.Message); }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
