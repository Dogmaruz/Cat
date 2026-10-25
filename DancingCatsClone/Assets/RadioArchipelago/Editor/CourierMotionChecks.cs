using System;
using System.Reflection;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Editor-only checks using a deliberately rotated, non-uniform authoring rig.</summary>
    public static class CourierMotionChecks
    {
        public static void Run()
        {
            CheckEverySupportTurn();
            CheckContinuousBounce();
            var root = new GameObject("Courier motion test");
            try
            {
                var visual = Child(root.transform, "visual", Vector3.zero);
                visual.localScale = new Vector3(1.2f, .9f, 1.1f);
                visual.localRotation = Quaternion.Euler(0, 13, 0);
                var arm = Child(visual, "arm", new Vector3(.5f, 1, 0));
                arm.localRotation = Quaternion.Euler(90, 0, 35);
                var foot = Child(visual, "foot", new Vector3(.2f, .1f, 0));
                var antenna = Child(visual, "antenna", Vector3.up * 1.5f);
                antenna.localRotation = Quaternion.Euler(90, 0, 0);
                var eye = Child(visual, "eye", new Vector3(.1f, 1.1f, .4f));
                var motion = root.AddComponent<CourierMotion>();
                motion.ManualSimulation = true;

                var bindingType = typeof(CourierMotion).Assembly.GetType("RadioArchipelago.CourierRigBinding");
                Require(bindingType != null, "The courier rig must bind independent feet, eyes and secondary bone chains.");
                object binding = Activator.CreateInstance(bindingType);
                Field(binding, "visualPivot", visual); Field(binding, "rightArm", arm);
                Field(binding, "rightFoot", foot); Field(binding, "antennaChain", new[] { antenna });
                Field(binding, "blinkEyes", new[] { eye });
                var configure = typeof(CourierMotion).GetMethod("ConfigureRig");
                Require(configure != null, "The imported rig needs an explicit ConfigureRig entry point.");
                configure.Invoke(motion, new[] { binding });
                var rest = Snapshot(visual, arm, foot, antenna, eye);
                root.transform.SetPositionAndRotation(new Vector3(11, 2, -3), Quaternion.Euler(5, 17, 3));
                root.transform.localScale = Vector3.one * .47f;
                var rootRest = Snapshot(root.transform);

                Invoke(motion, "PlayJump", new[] { typeof(float) }, 2.4f);
                Invoke(motion, "SetFlightProgress", new[] { typeof(float) }, .6f);
                Step(motion, .9f);
                Require(Quaternion.Angle(arm.localRotation, rest[1].rotation) > 15, "A long flight must keep the arms airborne after the old .32 second clip would end.");
                var pausedPose = Snapshot(visual, arm, foot, antenna, eye);
                Invoke(motion, "SetPaused", new[] { typeof(bool) }, true);
                Step(motion, .6f);
                Same(pausedPose, visual, arm, foot, antenna, eye);
                Invoke(motion, "SetPaused", new[] { typeof(bool) }, false);
                Invoke(motion, "PlayLanding", new[] { typeof(float) }, 1f);
                Step(motion, .04f);
                Require(visual.localScale.y < rest[0].scale.y, "Landing must visibly compress the body without delaying the next jump.");
                Invoke(motion, "PlayHold", new[] { typeof(bool), typeof(float), typeof(bool) }, false, 2.2f, true);
                Step(motion, .6f);
                Require(Quaternion.Angle(visual.localRotation, rest[0].rotation) > 45, "A riding courier must turn around its local axis.");
                motion.PlayIdle(); Step(motion, .35f);
                Require((foot.localPosition - rest[2].position).sqrMagnitude > .00001f, "Idle must alternate independent feet.");
                motion.PlayEnd(); Step(motion, .6f);
                Same(rootRest, root.transform);
                motion.ResetMotion();
                Same(rest, visual, arm, foot, antenna, eye);
                Same(rootRest, root.transform);
                motion.PlayIdle(); Step(motion, 1.58f);
                Require(eye.localScale.y < rest[4].scale.y * .12f, "The courier must blink using the independent eye pivot.");
                motion.ResetMotion(); Same(rest, visual, arm, foot, antenna, eye);
                motion.SimulateSprings(float.NaN); motion.SimulateSprings(float.PositiveInfinity); motion.SimulateSprings(-1);
                Same(rest, visual, arm, foot, antenna, eye);

                motion.Configure(visual, antenna, null, null, arm);
                motion.PlayJump(); Step(motion, .5f); motion.PlayHold(); Step(motion, .2f);
                motion.ResetMotion(); Same(rootRest, root.transform);
                CheckIndependentChains();
                CheckShoulderArcs();
                Debug.Log("RADIO_MOTION PASS: long flight, pause, landing compression, riding spin, separate idle feet, blink, rest-pose reuse, legacy binding, bounded independent secondary chains and whole-arm shoulder swings.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CheckEverySupportTurn()
        {
            var root = new GameObject("Every supported tile turns");
            try
            {
                var visual = Child(root.transform, "visual", Vector3.zero);
                var motion = root.AddComponent<CourierMotion>(); motion.ManualSimulation = true;
                motion.Configure(visual, null, null, null, null);
                float Turn(float duration)
                {
                    float previous = Heading(visual.forward), sum = 0;
                    int frames = Mathf.CeilToInt(duration * 240);
                    for (int frame = 0; frame < frames; frame++)
                    {
                        motion.SimulateSprings(duration / frames);
                        float next = Heading(visual.forward);
                        sum += Mathf.DeltaAngle(previous, next); previous = next;
                    }
                    return sum;
                }
                foreach (float duration in new[] { .18f, .35f, .65f, 1.4f })
                {
                    motion.ResetMotion(); motion.PlayHold(false, duration, true);
                    Require(Mathf.Abs(Turn(duration) - 360) < .5f,
                        "Every long support, including a short " + duration + " second tile, needs a complete 360 degree turn.");
                }
                motion.ResetMotion();
                float last = 0;
                foreach (float duration in new[] { .35f, .65f, 1.4f, .18f })
                {
                    Invoke(motion, "PlaySupportedRide", new[] { typeof(float) }, duration);
                    float turn = Turn(duration);
                    Require(Mathf.Abs(Mathf.Abs(turn) - 360) < .5f && (last == 0 || turn * last < 0),
                        "Successive supported tiles must alternate complete clockwise and anticlockwise turns independently of tile type.");
                    last = turn;
                    motion.PlayJump(.3f); Step(motion, .3f);
                }
                motion.ResetMotion();
                Invoke(motion, "PlaySupportedRide", new[] { typeof(float) }, .35f);
                Require(Turn(.35f) > 359.5f, "Restarting a run must reset the supported-turn sequence.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static float Heading(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        private static void CheckContinuousBounce()
        {
            var root = new GameObject("Continuous courier bounce");
            try
            {
                var visual = Child(root.transform, "visual", Vector3.zero);
                var motion = root.AddComponent<CourierMotion>(); motion.ManualSimulation = true;
                motion.Configure(visual, null, null, null, null);
                foreach (bool riding in new[] { false, true })
                {
                    motion.ResetMotion();
                    if (riding) motion.PlayHold(false, 2.4f, true); else motion.PlayIdle();
                    float low = 10, high = 0;
                    for (int frame = 0; frame < 144; frame++)
                    {
                        motion.SimulateSprings(1f / 60);
                        if (frame < 30) continue;
                        low = Mathf.Min(low, visual.localScale.y); high = Mathf.Max(high, visual.localScale.y);
                        Require(Mathf.Abs(visual.localScale.x * visual.localScale.y * visual.localScale.z - 1) < .001f,
                            "Squash and stretch must preserve visual volume.");
                    }
                    Require(high - low > .075f,
                        "The courier needs a visible continuing squash/stretch cycle during " + (riding ? "a supported ride." : "idle."));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CheckIndependentChains()
        {
            var root = new GameObject("Independent scarf chain check");
            try
            {
                var visual = Child(root.transform, "visual", Vector3.zero);
                var left = new Transform[3]; var right = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    left[i] = Child(i == 0 ? visual : left[i - 1], "left." + i, i == 0 ? new Vector3(-.3f, .8f, 0) : Vector3.up * .2f);
                    right[i] = Child(i == 0 ? visual : right[i - 1], "right." + i, i == 0 ? new Vector3(.3f, .8f, 0) : Vector3.up * .2f);
                }
                left[0].localRotation = Quaternion.Euler(90, 0, 0);
                right[0].localRotation = Quaternion.Euler(0, 0, 90);
                var motion = root.AddComponent<CourierMotion>(); motion.ManualSimulation = true;
                // Deliberately interleave the tails: array position must not change their dynamics.
                object binding = Activator.CreateInstance(typeof(CourierMotion).Assembly.GetType("RadioArchipelago.CourierRigBinding", true));
                Field(binding, "visualPivot", visual);
                Field(binding, "scarfChain", new[] { right[1], left[0], right[2], left[2], right[0], left[1] });
                typeof(CourierMotion).GetMethod("ConfigureRig").Invoke(motion, new[] { binding });
                Quaternion leftRest = Quaternion.Inverse(visual.rotation) * left[2].rotation;
                Quaternion rightRest = Quaternion.Inverse(visual.rotation) * right[2].rotation;
                float greatest = 0;
                Invoke(motion, "PlayJump", new[] { typeof(float) }, 2.4f);
                for (int frame = 0; frame < 240; frame++)
                {
                    Invoke(motion, "SetFlightProgress", new[] { typeof(float) }, Mathf.Clamp01(frame / 144f));
                    if (frame == 80) Invoke(motion, "PlayLanding", new[] { typeof(float) }, 1.5f);
                    motion.SimulateSprings(1f / 60);
                    float bendL = Quaternion.Angle(leftRest, Quaternion.Inverse(visual.rotation) * left[2].rotation);
                    float bendR = Quaternion.Angle(rightRest, Quaternion.Inverse(visual.rotation) * right[2].rotation);
                    Require(bendL <= 68.2f && bendR <= 68.2f, "A scarf must share one bounded bend budget across its joints.");
                    Require(Mathf.Abs(bendL - bendR) < .2f, "Independent scarf tails must respond alike regardless of flattened binding order or FBX bone axes.");
                    greatest = Mathf.Max(greatest, bendL, bendR);
                }
                Require(greatest > 30, "The scarf chains need visibly free secondary motion during flight.");
                motion.ResetMotion();
                Require(Quaternion.Angle(leftRest, Quaternion.Inverse(visual.rotation) * left[2].rotation) < .02f
                    && Quaternion.Angle(rightRest, Quaternion.Inverse(visual.rotation) * right[2].rotation) < .02f,
                    "Both secondary branches must return to their own rest orientations.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CheckShoulderArcs()
        {
            var root = new GameObject("Whole-arm shoulder arc check");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(-8, 3, 6), Quaternion.Euler(4, 27, -6));
                root.transform.localScale = Vector3.one * .47f;
                var visual = Child(root.transform, "visual", Vector3.zero);
                visual.localRotation = Quaternion.Euler(0, 13, 0);
                visual.localScale = new Vector3(1.2f, .9f, 1.1f);
                var arms = new[]
                {
                    Child(visual, "left shoulder", new Vector3(-.45f, 1, 0)),
                    Child(visual, "right shoulder", new Vector3(.45f, 1, 0))
                };
                arms[0].localRotation = Quaternion.Euler(90, 17, 35);
                arms[1].localRotation = Quaternion.Euler(-90, -23, -41);
                var markers = new Transform[2][];
                var shoulders = new Vector3[2]; var restReach = new float[2];
                const float length = .7f;
                float down = 55 * Mathf.Deg2Rad;
                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? -1 : 1;
                    Vector3 direction = Quaternion.Inverse(arms[side].localRotation)
                        * new Vector3(sign * Mathf.Cos(down), -Mathf.Sin(down), 0);
                    markers[side] = new[]
                    {
                        Child(arms[side], "proximal", direction * length * .18f),
                        Child(arms[side], "middle", direction * length * .52f),
                        Child(arms[side], "tip", direction * length)
                    };
                    shoulders[side] = visual.InverseTransformPoint(arms[side].position);
                    restReach[side] = sign * (visual.InverseTransformPoint(markers[side][2].position) - shoulders[side]).x;
                }
                var motion = root.AddComponent<CourierMotion>(); motion.ManualSimulation = true;
                motion.ConfigureRig(new CourierRigBinding { visualPivot = visual, leftArm = arms[0], rightArm = arms[1] });
                var rootRest = Snapshot(root.transform);

                motion.PlayIdle(); Step(motion, 4.4f);
                CheckShoulderPose("greeting", visual, arms, markers, shoulders, restReach, length, 8, 30, false);
                motion.ResetMotion(); motion.PlayJump(.45f); motion.SetFlightProgress(.5f); Step(motion, .6f);
                CheckShoulderPose("short jump", visual, arms, markers, shoulders, restReach, length, 15, 34);
                motion.ResetMotion(); motion.PlayJump(2.4f); motion.SetFlightProgress(.5f); Step(motion, .9f);
                CheckShoulderPose("long flight", visual, arms, markers, shoulders, restReach, length, 42, 62);
                motion.ResetMotion(); motion.PlayHold(false, 2.2f, true); Step(motion, .9f);
                CheckShoulderPose("riding", visual, arms, markers, shoulders, restReach, length, 14, 26);
                motion.ResetMotion(); motion.PlayEnd(); Step(motion, .9f);
                CheckShoulderPose("celebration", visual, arms, markers, shoulders, restReach, length, 24, 36);
                Same(rootRest, root.transform);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CheckShoulderPose(string pose, Transform visual, Transform[] arms, Transform[][] markers,
            Vector3[] shoulders, float[] restReach, float length, float minimumLift, float maximumLift, bool bothArms = true)
        {
            for (int side = 0; side < 2; side++)
            {
                Vector3 shoulder = visual.InverseTransformPoint(arms[side].position);
                Require((shoulder - shoulders[side]).sqrMagnitude < 1e-9f, pose + ": the shoulder attachment moved away from the body.");
                Vector3 proximal = visual.InverseTransformPoint(markers[side][0].position) - shoulder;
                Vector3 middle = visual.InverseTransformPoint(markers[side][1].position) - shoulder;
                Vector3 tip = visual.InverseTransformPoint(markers[side][2].position) - shoulder;
                Require(Mathf.Abs(proximal.magnitude - length * .18f) < .0001f
                    && Mathf.Abs(middle.magnitude - length * .52f) < .0001f
                    && Mathf.Abs(tip.magnitude - length) < .0001f,
                    pose + ": the whole main arm must retain its lengths while swinging from the shoulder.");
                Require(Vector3.Angle(proximal, tip) < .05f && Vector3.Angle(middle, tip) < .05f,
                    pose + ": the proximal arm must follow the hand instead of creating an elbow bend.");
                if (!bothArms && side == 0) continue;
                float outward = (side == 0 ? -1 : 1) * tip.x;
                Require(outward > restReach[side] + length * .08f,
                    pose + ": the hand must swing gently outward from its shoulder, rather than folding upward beside the face.");
                float elevation = Mathf.Atan2(tip.y, outward) * Mathf.Rad2Deg;
                Require(elevation < Mathf.Max(5, maximumLift - 54), pose + ": shoulder swing raises the hand beyond its intended stroke; elevation=" + elevation.ToString("F2"));
                float lift = elevation + 55;
                Require(lift >= minimumLift && lift <= maximumLift,
                    pose + ": shoulder lift outside its expressive range; lift=" + lift.ToString("F2"));
            }
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false); node.localPosition = position;
            return node;
        }
        private static void Field(object binding, string name, object value)
        {
            var field = binding.GetType().GetField(name);
            Require(field != null, "Missing explicit rig binding: " + name);
            field.SetValue(binding, value);
        }
        private static void Invoke(CourierMotion motion, string method, Type[] signature, params object[] values)
        {
            var entry = typeof(CourierMotion).GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, signature, null);
            Require(entry != null, "Missing courier motion event: " + method);
            entry.Invoke(motion, values);
        }
        private static void Step(CourierMotion motion, float seconds)
        {
            while (seconds > .000001f) { float dt = Mathf.Min(1f / 60, seconds); motion.SimulateSprings(dt); seconds -= dt; }
        }
        private struct Rest { public Vector3 position, scale; public Quaternion rotation; }
        private static Rest[] Snapshot(params Transform[] nodes)
        {
            var result = new Rest[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) result[i] = new Rest { position = nodes[i].localPosition, rotation = nodes[i].localRotation, scale = nodes[i].localScale };
            return result;
        }
        private static void Same(Rest[] expected, params Transform[] nodes)
        {
            for (int i = 0; i < nodes.Length; i++)
                Require((nodes[i].localPosition - expected[i].position).sqrMagnitude < 1e-9f
                    && (nodes[i].localScale - expected[i].scale).sqrMagnitude < 1e-9f
                    && Quaternion.Angle(nodes[i].localRotation, expected[i].rotation) < .02f,
                    "Motion changed a paused/rest/gameplay transform: " + nodes[i].name);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
