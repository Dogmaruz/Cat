using System;
using UnityEngine;

namespace RadioArchipelago
{
    /// <summary>
    /// Local poses and damped secondary motion. The session alone moves the gameplay root;
    /// animation never changes a collider or the musical flight trajectory.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CourierMotion : MonoBehaviour
    {
        // Preserve the first-generation prefab bindings until imported replacements are authored.
        [SerializeField] private Transform visualPivot;
        [SerializeField] private Transform antennaPivot;
        [SerializeField] private Transform scarfPivot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private CourierRigBinding rig;
        private Part visual, head, armLeft, armRight, footLeft, footRight, antennaSwivel;
        private Part[] antennas = Array.Empty<Part>(), crests = Array.Empty<Part>(), scarves = Array.Empty<Part>(), eyes = Array.Empty<Part>();
        private Pose pose;
        private bool captured, paused, externallyTimedFlight, holdLeft, spinning, nextRideLeft;
        private float elapsed, phase, facing, facingTarget, flightDuration = .45f, flightProgress;
        private float landingTime = 10, landingStrength, holdDuration = 2.6f;
        private float bodyPitch, bodyRoll, bodyStretch, armLeftLift, armRightLift, footLeftLift, footRightLift;
        private float spinAngle, headTilt, bob;
        private float turnRate, turnAcceleration, rotorAngle, rotorWorldSpeed, rotorBlend, rotorReturnSpeed;
        private Vector3 previousPosition, movementVelocity, movementAcceleration;
        private bool movementCaptured;
        private int blinkIndex = -1;
        private float blinkRest;
        private enum Pose { Idle, Jump, Hold, Celebrate }

        public bool ManualSimulation { get; set; }
        public bool IsPaused => paused;

#if UNITY_EDITOR
        [Serializable]
        public struct EditorTimingPose
        {
            public float landingTime, landingStrength, impactCompression, bodyStretch, scaleYRatio, leftFootLift, rightFootLift, bob;
            public Vector3 visualLocalPosition;
        }
        public EditorTimingPose ReadEditorTimingPose() => new EditorTimingPose
        {
            landingTime = landingTime, landingStrength = landingStrength,
            impactCompression = ImpactCompression(), bodyStretch = bodyStretch,
            scaleYRatio = visual != null ? visual.node.localScale.y / visual.scale.y : 1,
            leftFootLift = footLeftLift, rightFootLift = footRightLift, bob = bob,
            visualLocalPosition = visual != null ? visual.node.localPosition : Vector3.zero
        };
#endif

        public void Configure(Transform visualRoot, Transform antenna, Transform scarf, Transform armL, Transform armR)
        {
            RestoreRestPose();
            visualPivot = visualRoot; antennaPivot = antenna; scarfPivot = scarf;
            leftArm = armL; rightArm = armR; rig = null; captured = false;
            ResetMotion();
        }

        public void ConfigureRig(CourierRigBinding binding)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (binding.visualPivot == null || binding.visualPivot == transform)
                throw new ArgumentException("The courier visual pivot must be a child of its presentation root.", nameof(binding));
            RestoreRestPose(); rig = binding; visualPivot = binding.visualPivot; captured = false;
            ResetMotion();
        }

        private void Awake() => CaptureRestPose();
        private bool CaptureRestPose()
        {
            if (captured) return true;
            Transform pivot = rig != null ? rig.visualPivot : visualPivot;
            if (pivot == null || pivot == transform) return false;
            visual = new Part(pivot, pivot); head = Part.Create(rig?.headPivot, pivot);
            armLeft = Part.Create(rig != null ? rig.leftArm : leftArm, pivot);
            armRight = Part.Create(rig != null ? rig.rightArm : rightArm, pivot);
            footLeft = Part.Create(rig?.leftFoot, pivot); footRight = Part.Create(rig?.rightFoot, pivot);
            antennaSwivel = Part.Create(rig?.antennaYawPivot, pivot);
            var weighted = rig != null ? WeightedBones(pivot) : null;
            antennas = MakeParts(ActiveNodes(rig != null ? rig.antennaChain : Single(antennaPivot), weighted), pivot, weighted);
            crests = MakeParts(ActiveNodes(rig?.crestChain, weighted), pivot, weighted);
            scarves = MakeParts(ActiveNodes(rig != null ? rig.scarfChain : Single(scarfPivot), weighted), pivot, weighted);
            eyes = MakeParts(rig?.blinkEyes, pivot);
            blinkIndex = rig?.blinkRenderer != null && rig.blinkRenderer.sharedMesh != null && !string.IsNullOrEmpty(rig.blinkShapeName)
                ? rig.blinkRenderer.sharedMesh.GetBlendShapeIndex(rig.blinkShapeName) : -1;
            blinkRest = blinkIndex >= 0 ? rig.blinkRenderer.GetBlendShapeWeight(blinkIndex) : 0;
            captured = true; return true;
        }

        public void ResetMotion()
        {
            if (!CaptureRestPose()) return;
            elapsed = phase = facing = facingTarget = flightProgress = 0;
            bodyPitch = bodyRoll = bodyStretch = armLeftLift = armRightLift = footLeftLift = footRightLift = 0;
            spinAngle = headTilt = bob = 0;
            turnRate = turnAcceleration = rotorAngle = rotorWorldSpeed = rotorBlend = rotorReturnSpeed = 0;
            previousPosition = transform.position; movementVelocity = movementAcceleration = Vector3.zero; movementCaptured = true;
            paused = externallyTimedFlight = holdLeft = spinning = nextRideLeft = false;
            landingTime = 10; landingStrength = 0; flightDuration = .45f; holdDuration = 2.6f; pose = Pose.Idle;
            RestoreRestPose();
        }
        public void PlayIdle() { pose = Pose.Idle; elapsed = 0; externallyTimedFlight = false; }
        public void PlayJump() => PlayJump(.45f);
        public void PlayJump(float durationSeconds)
        {
            flightDuration = Finite(durationSeconds) ? Mathf.Max(.08f, durationSeconds) : .45f;
            flightProgress = 0; elapsed = 0; pose = Pose.Jump; externallyTimedFlight = false;
        }
        /// <summary>Follows the actual session arc, including long gaps, rather than an unrelated clip clock.</summary>
        public void SetFlightProgress(float normalizedProgress)
        {
            if (!Finite(normalizedProgress)) return;
            flightProgress = Mathf.Max(0, normalizedProgress); externallyTimedFlight = true;
        }
        public void PlayLanding(float strength = 1)
        {
            landingTime = 0; landingStrength = Finite(strength) ? Mathf.Clamp(strength, .25f, 1.5f) : 1;
        }
        public void PlayHold(bool left = false) => PlayHold(left, 2.6f, true);
        public void PlayHold(bool left, float durationSeconds, bool spin = true)
        {
            pose = Pose.Hold; holdLeft = left; elapsed = 0;
            holdDuration = Finite(durationSeconds) ? Mathf.Max(.08f, durationSeconds) : 2.6f;
            spinning = spin;
        }
        /// <summary>One complete turn per supported tile; tile type does not choose its direction.</summary>
        public void PlaySupportedRide(float durationSeconds)
        {
            PlayHold(nextRideLeft, durationSeconds, true);
            nextRideLeft = !nextRideLeft;
        }
        public void PlayEnd() { pose = Pose.Celebrate; elapsed = 0; externallyTimedFlight = false; }
        public void SetFacing(float horizonDegrees) { if (Finite(horizonDegrees)) facingTarget = Mathf.Clamp(horizonDegrees, -70, 70); }
        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            // A resumed presentation starts measuring displacement at the resumed location.
            previousPosition = transform.position; movementVelocity = movementAcceleration = Vector3.zero;
        }

        public void SimulateSprings(float deltaTime)
        {
            if (paused || !CaptureRestPose() || deltaTime <= 0 || !Finite(deltaTime)) return;
            MeasureMovement(deltaTime);
            // Bounded substeps keep the damped chains stable through an Editor/render hitch.
            float remaining = Mathf.Min(deltaTime, .25f);
            while (remaining > .000001f)
            {
                float dt = Mathf.Min(remaining, 1f / 120); Step(dt); remaining -= dt;
            }
            ApplyPose();
        }

        private void MeasureMovement(float dt)
        {
            Vector3 position = transform.position;
            Vector3 displacement = position - previousPosition; previousPosition = position;
            if (!movementCaptured || displacement.sqrMagnitude > 100 || dt > .3f)
            {
                movementCaptured = true; movementVelocity = movementAcceleration = Vector3.zero; return;
            }
            // Presentation input only: never write back into the session's musical trajectory.
            float scale = Mathf.Max(.1f, Mathf.Abs(transform.lossyScale.y));
            Vector3 velocity = Vector3.ClampMagnitude(transform.InverseTransformDirection(displacement) / (dt * scale), 18);
            Vector3 previousVelocity = movementVelocity;
            movementVelocity = Vector3.Lerp(movementVelocity, velocity, 1 - Mathf.Exp(-14 * dt));
            Vector3 acceleration = Vector3.ClampMagnitude((movementVelocity - previousVelocity) / dt, 80);
            movementAcceleration = Vector3.Lerp(movementAcceleration, acceleration, 1 - Mathf.Exp(-12 * dt));
        }

        private void Step(float dt)
        {
            float previousYaw = facing + spinAngle;
            elapsed += dt; phase += dt; landingTime += dt;
            if (pose == Pose.Jump && !externallyTimedFlight) flightProgress = elapsed / flightDuration;
            float smooth = 1 - Mathf.Exp(-19 * dt);
            facing = Mathf.Lerp(facing, facingTarget, 1 - Mathf.Exp(-12 * dt));
            float pitch = 0, roll = Mathf.Sin(phase * 2.1f) * 1.3f, stretch = 0;
            float liftL = 2, liftR = 2, footL = 0, footR = 0, turn = 0, nod = 0;
            float height = Mathf.Sin(phase * 3.2f) * .012f, wind = 0, rotorDemand = 0, rotorDirection = -1;
            switch (pose)
            {
                case Pose.Idle:
                    float beat = phase * 5.4f;
                    float hopL = Mathf.Max(0, Mathf.Sin(beat)), hopR = Mathf.Max(0, -Mathf.Sin(beat));
                    height += (hopL * hopL + hopR * hopR) * .045f; roll += Mathf.Sin(beat) * 4.5f;
                    stretch = Mathf.Cos(beat * 2) * .07f; footL = hopL * .07f; footR = hopR * .07f;
                    liftL += hopR * 8; liftR += hopL * 8;
                    float greeting = (phase + .8f) % 6.8f;
                    if (greeting > 4.3f && greeting < 6.1f)
                    {
                        float envelope = Mathf.Sin((greeting - 4.3f) / 1.8f * Mathf.PI);
                        // Blend out the idle hop gesture so the greeting stays a small shoulder swing.
                        liftR = Mathf.Lerp(liftR, 20 + Mathf.Sin(greeting * 18) * 8, envelope); nod = envelope * -7;
                    }
                    break;
                case Pose.Jump:
                    float p = Mathf.Clamp01(flightProgress), airborne = Mathf.Sin(p * Mathf.PI);
                    float longFlight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.6f, 1.2f, flightDuration));
                    stretch = .09f * Mathf.Exp(-Mathf.Pow((p - .13f) / .17f, 2))
                        - .025f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1, p))
                        + .045f * Mathf.Sin(elapsed * 11) * airborne;
                    pitch = -9 * Mathf.Sin(p * Mathf.PI * 1.15f);
                    // The reference cat uses simultaneous mirrored shoulder strokes at four Hz.
                    // Keep that timing and eased stroke, retargeted to the couriers' short paws.
                    float strokePhase = Mathf.Repeat(elapsed * 4, 1);
                    float stroke = strokePhase < .52f ? Mathf.SmoothStep(0, 1, strokePhase / .52f)
                        : 1 - Mathf.SmoothStep(0, 1, (strokePhase - .52f) / .48f);
                    float flightBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .06f, p))
                        * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.91f, 1, p)));
                    float strokeRange = Mathf.Lerp(42, 70, longFlight);
                    liftL = liftR = Mathf.Lerp(8, -10 + stroke * strokeRange, flightBlend);
                    footL = footR = airborne * .05f;
                    roll += Mathf.Sin(elapsed * 6) * longFlight * airborne * 2.5f; nod = -airborne * 4;
                    wind = 10 + longFlight * 14 + airborne * 17;
                    rotorDemand = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1.2f, flightDuration))
                        * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .18f, p))
                        * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.84f, 1, p)));
                    break;
                case Pose.Hold:
                    float direction = holdLeft ? -1 : 1;
                    roll += direction * 7 + Mathf.Sin(elapsed * 3) * 2.5f;
                    pitch = 6; stretch = -.025f + Mathf.Sin(elapsed * 10.8f) * .065f;
                    height += -.018f + Mathf.Abs(Mathf.Sin(elapsed * 5.4f)) * .018f;
                    liftL = 20 + Mathf.Sin(elapsed * 4.1f) * 5; liftR = 20 - Mathf.Sin(elapsed * 4.1f) * 5;
                    nod = -3; wind = 14;
                    rotorDemand = 1;
                    rotorDirection = -direction;
                    if (spinning)
                    {
                        turn = direction * 360 * Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / holdDuration));
                    }
                    break;
                case Pose.Celebrate:
                    height += Mathf.Abs(Mathf.Sin(elapsed * 7)) * .10f; roll += Mathf.Sin(elapsed * 5) * 7;
                    stretch = Mathf.Cos(elapsed * 14) * .035f;
                    liftL = 30 + Mathf.Sin(elapsed * 9) * 5; liftR = 30 - Mathf.Sin(elapsed * 9) * 5;
                    footL = Mathf.Max(0, Mathf.Sin(elapsed * 7)) * .06f; footR = Mathf.Max(0, -Mathf.Sin(elapsed * 7)) * .06f;
                    nod = Mathf.Sin(elapsed * 5) * 5;
                    break;
            }
            bodyPitch = Mathf.Lerp(bodyPitch, pitch, smooth); bodyRoll = Mathf.Lerp(bodyRoll, roll, smooth);
            bodyStretch = Mathf.Lerp(bodyStretch, stretch, smooth);
            // A slow body blend erased the four-Hz paw strokes. The short shoulder gesture
            // follows its own response while takeoff/landing still ease into the pose.
            float armBlend = 1 - Mathf.Exp(-(pose == Pose.Jump ? 70 : 19) * dt);
            armLeftLift = Mathf.Lerp(armLeftLift, liftL, armBlend); armRightLift = Mathf.Lerp(armRightLift, liftR, armBlend);
            footLeftLift = Mathf.Lerp(footLeftLift, footL, smooth); footRightLift = Mathf.Lerp(footRightLift, footR, smooth);
            headTilt = Mathf.Lerp(headTilt, nod, smooth); bob = Mathf.Lerp(bob, height, smooth);
            if (pose == Pose.Hold) spinAngle = turn;
            else spinAngle = Mathf.Lerp(Mathf.DeltaAngle(0, spinAngle), 0, 1 - Mathf.Exp(-13 * dt));
            float yawDelta = Mathf.DeltaAngle(previousYaw, facing + spinAngle);
            float previousRate = turnRate;
            turnRate = Mathf.Lerp(turnRate, yawDelta / dt, 1 - Mathf.Exp(-9 * dt));
            turnAcceleration = Mathf.Lerp(turnAcceleration, (turnRate - previousRate) / dt, 1 - Mathf.Exp(-12 * dt));
            StepSwivel(dt, rotorDemand, rotorDirection, yawDelta);
            float impact = ImpactCompression(), sway = Mathf.Sin(phase * 3.6f) * 2.2f - bodyRoll * .55f - facing * .12f;
            // Measured displacement drives a trailing target; each chain maps the
            // response onto its own rest arc instead of sharing a global roll axis.
            float lateralLag = Mathf.Clamp(-movementVelocity.x * 13f - movementAcceleration.x * .75f, -75, 75);
            Vector3 trailingForce = transform.TransformDirection(Vector3.right * lateralLag);
            // Secondary chains can live beneath a separate spinning yaw mount. Update
            // their real non-chain anchors before evaluating world-space inertia.
            ApplyFramePose();
            float verticalLag = Mathf.Clamp(-movementVelocity.y * 2.5f - movementAcceleration.y * .30f, -24, 24);
            float hingeLag = Mathf.Clamp(-turnRate * .04f - turnAcceleration * .003f, -32, 32);
            float clothLag = Mathf.Clamp(-turnRate * .11f - turnAcceleration * .009f, -52, 52);
            StepChain(antennas, dt, bodyPitch * -.75f - impact * 100 + verticalLag * .45f, sway + hingeLag, 8.2f, .40f,
                trailingForce: trailingForce * .85f);
            StepChain(crests, dt, bodyPitch * -.7f - impact * 65, -sway * .8f, 12, .55f,
                trailingForce: trailingForce * .35f);
            StepChain(scarves, dt, -wind + bodyPitch * .8f + impact * 125 + verticalLag,
                sway + clothLag, 9.5f, .34f, phase, 5 + wind * .28f, 45, 48, trailingForce);
        }

        private void StepSwivel(float dt, float demand, float direction, float bodyYawDelta)
        {
            if (antennaSwivel == null) return;
            rotorBlend = Mathf.Lerp(rotorBlend, demand, 1 - Mathf.Exp(-8 * dt));
            rotorWorldSpeed = Mathf.Lerp(rotorWorldSpeed, direction * 110 * demand, 1 - Mathf.Exp(-6 * dt));
            // During a spin this is a world-heading motor: remove the body's actual yaw,
            // so a clockwise courier produces a visibly anticlockwise rotor in the scene.
            // Outside a spin the same hinge eases back to its nearest rest heading.
            float restWeight = 1 - rotorBlend;
            float torque = Mathf.DeltaAngle(rotorAngle, 0) * 27 - rotorReturnSpeed * 6.5f
                - Mathf.Clamp(turnRate * 2.1f + turnAcceleration * .10f, -700, 700);
            rotorReturnSpeed += torque * restWeight * dt;
            rotorReturnSpeed *= Mathf.Exp(-rotorBlend * 10 * dt);
            rotorAngle += (rotorWorldSpeed + rotorReturnSpeed * restWeight) * dt - bodyYawDelta * rotorBlend;
            // Equivalent angles keep floating-point precision during an arbitrarily long session.
            rotorAngle = Mathf.Repeat(rotorAngle + 180, 360) - 180;
        }

        private void ApplyPose()
        {
            ApplyFramePose();
            armLeft?.Rotate(-Mathf.Sin(phase * 3) * 3, 0, -armLeftLift);
            armRight?.Rotate(Mathf.Sin(phase * 3) * 3, 0, armRightLift);
            if (footLeft != null) { footLeft.Rotate(-footLeftLift * 130, 0, -bodyRoll * .4f); footLeft.Lift(footLeftLift); }
            if (footRight != null) { footRight.Rotate(-footRightLift * 130, 0, -bodyRoll * .4f); footRight.Lift(footRightLift); }
            ApplyChains(antennas); ApplyChains(crests); ApplyChains(scarves);
            float blink = BlinkAmount();
            foreach (var eye in eyes) eye.node.localScale = Vector3.Scale(eye.scale, new Vector3(1, Mathf.Lerp(1, .045f, blink), 1));
            if (blinkIndex >= 0 && rig?.blinkRenderer != null)
                rig.blinkRenderer.SetBlendShapeWeight(blinkIndex, Mathf.Lerp(blinkRest, 100, blink));
        }
        private void ApplyFramePose()
        {
            float stretch = Mathf.Clamp(bodyStretch - ImpactCompression(), -.24f, .16f);
            visual.node.localPosition = visual.position + visual.rotation * Vector3.up * bob;
            visual.node.localRotation = visual.rotation * Quaternion.Euler(bodyPitch, facing + spinAngle, bodyRoll);
            float width = 1 / Mathf.Sqrt(1 + stretch);
            visual.node.localScale = Vector3.Scale(visual.scale, new Vector3(width, 1 + stretch, width));
            head?.Rotate(headTilt, 0, -bodyRoll * .15f);
            antennaSwivel?.Rotate(0, rotorAngle, 0);
        }
        private float ImpactCompression()
        {
            if (landingTime >= .36f) return 0;
            float t = landingTime;
            return landingStrength * (.16f * Mathf.Exp(-Mathf.Pow((t - .035f) / .05f, 2))
                - .055f * Mathf.Exp(-Mathf.Pow((t - .14f) / .055f, 2)));
        }
        private float BlinkAmount()
        {
            float cycle = phase % 4.7f, blink = Pulse(cycle, 1.5f, .16f);
            if (((int)(phase / 4.7f) & 1) != 0) blink = Mathf.Max(blink, Pulse(cycle, 1.76f, .13f));
            return blink;
        }
        private static float Pulse(float t, float start, float duration)
        {
            float p = (t - start) / duration;
            return p > 0 && p < 1 ? Mathf.Sin(p * Mathf.PI) : 0;
        }
        private static void StepChain(Part[] parts, float dt, float pitch, float roll, float frequency, float damping,
            float time = 0, float flutter = 0, float targetLimit = 32, float bendLimit = 40,
            Vector3 trailingForce = default)
        {
            foreach (var part in parts) part.TransportInertia();
            // Read the previous state of every joint before writing any new state. This
            // keeps interleaved independent scarf branches independent of array order.
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                // A scarf array can contain several independent tails. Distribute one total bend
                // over each actual branch, rather than repeating the full angle at every joint.
                Vector2 target = new Vector2(Mathf.Clamp(pitch, -targetLimit, targetLimit), Mathf.Clamp(roll, -targetLimit * .875f, targetLimit * .875f));
                Vector3 inertia = Vector3.Cross(part.restLever, part.chainAnchor.InverseTransformDirection(trailingForce));
                if (part.chainParent != null)
                {
                    var parent = part.chainParent;
                    target = target * .55f + parent.bend / parent.bendShare * .45f;
                    inertia = inertia * .55f + parent.inertialBend / parent.bendShare * .45f;
                    float tip = part.chainDepth / (float)Mathf.Max(1, part.chainLength - 1);
                    target += new Vector2(Mathf.Sin(time * 6.3f - part.chainDepth * .72f),
                        Mathf.Sin(time * 5.1f - part.chainDepth * .88f) * .65f) * flutter * tip;
                }
                // Wind/turn and inertial rotations can point along the same axis.
                // A sum budget bounds their actual composed angle; quadrature would
                // incorrectly allow two overlapping 48-degree bends inside 68 degrees.
                float combined = target.magnitude + inertia.magnitude;
                float scale = Mathf.Min(1, targetLimit / Mathf.Max(.001f, combined));
                part.targetBend = target * scale * part.bendShare;
                part.targetInertia = inertia * scale * part.bendShare;
            }
            foreach (var part in parts)
            {
                float omega = frequency * (1 - Mathf.Min(part.chainDepth, 4) * .055f);
                part.velocity += (omega * omega * (part.targetBend - part.bend) - 2 * damping * omega * part.velocity) * dt;
                part.bend += part.velocity * dt;
                part.inertialVelocity += (omega * omega * (part.targetInertia - part.inertialBend)
                    - 2 * damping * omega * part.inertialVelocity) * dt;
                part.inertialBend += part.inertialVelocity * dt;
                float combined = part.bend.magnitude + part.inertialBend.magnitude;
                float scale = Mathf.Min(1, bendLimit * part.bendShare / Mathf.Max(.001f, combined));
                part.bend *= scale; part.inertialBend *= scale;
            }
        }
        private static void ApplyChains(Part[] parts) { foreach (var part in parts) part.Bend(part.bend.x, part.bend.y); }
        private void RestoreRestPose()
        {
            if (!captured) return;
            visual?.Reset(); head?.Reset(); armLeft?.Reset(); armRight?.Reset(); footLeft?.Reset(); footRight?.Reset(); antennaSwivel?.Reset();
            ResetParts(antennas); ResetParts(crests); ResetParts(scarves); ResetParts(eyes);
            if (blinkIndex >= 0 && rig?.blinkRenderer != null) rig.blinkRenderer.SetBlendShapeWeight(blinkIndex, blinkRest);
        }
        private static void ResetParts(Part[] parts) { foreach (var part in parts) part.Reset(); }
        private static Transform[] Single(Transform node) => node != null ? new[] { node } : Array.Empty<Transform>();
        private static System.Collections.Generic.Dictionary<Transform, Vector3> WeightedBones(Transform frame)
        {
            var result = new System.Collections.Generic.Dictionary<Transform, Vector3>();
            var sums = new System.Collections.Generic.Dictionary<Transform, float>();
            foreach (var renderer in frame.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                var bones = renderer.bones;
                var counts = renderer.sharedMesh.GetBonesPerVertex();
                var weights = renderer.sharedMesh.GetAllBoneWeights();
                var scratch = new Mesh { name = "Owned courier rest inertia sample" };
                try
                {
                    renderer.BakeMesh(scratch, true);
                    var vertices = scratch.vertices; int at = 0;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        Vector3 world = renderer.transform.TransformPoint(vertices[vertex]);
                        for (int influence = 0; influence < counts[vertex]; influence++)
                        {
                            var weight = weights[at++];
                            if (weight.weight <= .0001f || weight.boneIndex >= bones.Length) continue;
                            var bone = bones[weight.boneIndex]; if (bone == null) continue;
                            if (!result.ContainsKey(bone)) { result.Add(bone, Vector3.zero); sums.Add(bone, 0); }
                            result[bone] += world * weight.weight; sums[bone] += weight.weight;
                        }
                    }
                }
                finally
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(scratch); else UnityEngine.Object.DestroyImmediate(scratch);
                }
            }
            foreach (var bone in new System.Collections.Generic.List<Transform>(result.Keys)) result[bone] /= sums[bone];
            return result;
        }
        private static Transform[] ActiveNodes(Transform[] nodes, System.Collections.Generic.Dictionary<Transform, Vector3> weighted)
        {
            if (nodes == null || weighted == null || weighted.Count == 0) return nodes;
            // FBX orientation markers can remain after an attachment was made rigid.
            // They carry no surface, so allocating them a share of the spring's bend
            // budget makes the visible antenna almost motionless.
            var result = new System.Collections.Generic.List<Transform>(nodes.Length);
            foreach (var node in nodes)
            {
                if (node == null) continue;
                bool active = weighted.ContainsKey(node);
                if (!active)
                    foreach (var descendant in nodes)
                        if (descendant != null && weighted.ContainsKey(descendant) && descendant.IsChildOf(node))
                        { active = true; break; }
                // An unweighted joint can still move weighted children; only inert
                // terminal branches are omitted, never a real ancestor in the chain.
                if (active) result.Add(node);
            }
            return result.ToArray();
        }
        private static Part[] MakeParts(Transform[] nodes, Transform frame,
            System.Collections.Generic.Dictionary<Transform, Vector3> weighted = null)
        {
            if (nodes == null || nodes.Length == 0) return Array.Empty<Part>();
            int count = 0;
            foreach (var node in nodes) if (node != null) count++;
            var parts = new Part[count]; int at = 0;
            foreach (var node in nodes) if (node != null) parts[at++] = new Part(node, frame);
            var indices = new System.Collections.Generic.Dictionary<Transform, int>(count);
            for (int i = 0; i < count; i++) indices.Add(parts[i].node, i);
            var parents = new int[count]; var roots = new int[count]; var lengths = new int[count];
            for (int i = 0; i < count; i++)
            {
                parents[i] = -1;
                // This hierarchy walk happens once when binding the rig; simulation uses only caches.
                for (var ancestor = parts[i].node.parent; ancestor != null && ancestor != frame; ancestor = ancestor.parent)
                    if (indices.TryGetValue(ancestor, out int parent)) { parents[i] = parent; break; }
            }
            for (int i = 0; i < count; i++)
            {
                parts[i].chainParent = parents[i] >= 0 ? parts[parents[i]] : null;
                int root = i, depth = 0;
                while (parents[root] >= 0) { root = parents[root]; depth++; }
                roots[i] = root; parts[i].chainDepth = depth;
                lengths[root] = Mathf.Max(lengths[root], depth + 1);
            }
            for (int i = 0; i < count; i++)
            {
                int length = lengths[roots[i]];
                parts[i].chainLength = length;
                // A slightly softer tip retains one fixed angle budget for the whole branch.
                float sum = length + .12f * length * (length - 1) * .5f;
                parts[i].bendShare = (1 + .12f * parts[i].chainDepth) / sum;
                int tip = i;
                for (int candidate = 0; candidate < count; candidate++)
                    if (roots[candidate] == roots[i] && parts[candidate].chainDepth > parts[tip].chainDepth) tip = candidate;
                Vector3 tipPosition = weighted != null && weighted.TryGetValue(parts[tip].node, out var centroid)
                    ? centroid : parts[tip].node.position;
                parts[i].SetChainFrame(parts[roots[i]].node.parent != null ? parts[roots[i]].node.parent : frame,
                    tipPosition - parts[roots[i]].node.position);
            }
            return parts;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void LateUpdate() { if (!ManualSimulation) SimulateSprings(Time.deltaTime); }

        private sealed class Part
        {
            public readonly Transform node;
            public readonly Vector3 position, scale;
            public readonly Quaternion rotation;
            private readonly Vector3 rightAxis, upAxis, forwardAxis, parentUp;
            private Vector3 inertialRight, inertialUp, inertialForward;
            private Quaternion inertialFrameRotation;
            public Transform chainAnchor;
            public Vector2 bend, velocity, targetBend;
            public Vector3 restLever, inertialBend, inertialVelocity, targetInertia;
            public Part chainParent;
            public int chainDepth, chainLength = 1;
            public float bendShare = 1;
            public Part(Transform target, Transform frame)
            {
                node = target; position = target.localPosition; rotation = target.localRotation; scale = target.localScale;
                // FBX bone axes vary: use the visual frame expressed in this bone's rest space.
                rightAxis = target.InverseTransformDirection(frame.right).normalized;
                upAxis = target.InverseTransformDirection(frame.up).normalized;
                forwardAxis = target.InverseTransformDirection(frame.forward).normalized;
                parentUp = target.parent != null ? target.parent.InverseTransformDirection(frame.up).normalized : frame.up;
            }
            public static Part Create(Transform target, Transform frame) => target != null ? new Part(target, frame) : null;
            public void Rotate(float pitch, float yaw, float roll)
            {
                node.localRotation = rotation * Quaternion.AngleAxis(pitch, rightAxis)
                    * Quaternion.AngleAxis(yaw, upAxis) * Quaternion.AngleAxis(roll, forwardAxis);
            }
            public void SetChainFrame(Transform anchor, Vector3 worldLever)
            {
                chainAnchor = anchor; inertialFrameRotation = anchor.rotation;
                restLever = worldLever.sqrMagnitude > .000001f ? anchor.InverseTransformDirection(worldLever).normalized : Vector3.up;
                inertialRight = node.InverseTransformDirection(anchor.right).normalized;
                inertialUp = node.InverseTransformDirection(anchor.up).normalized;
                inertialForward = node.InverseTransformDirection(anchor.forward).normalized;
            }
            public void TransportInertia()
            {
                Quaternion transport = Quaternion.Inverse(chainAnchor.rotation) * inertialFrameRotation;
                inertialBend = transport * inertialBend; inertialVelocity = transport * inertialVelocity;
                inertialFrameRotation = chainAnchor.rotation;
            }
            public void Bend(float pitch, float lateral)
            {
                Quaternion bendRotation = Quaternion.AngleAxis(pitch, rightAxis) * Quaternion.AngleAxis(lateral, forwardAxis);
                float angle = inertialBend.magnitude;
                if (angle > .00001f)
                {
                    Vector3 axis = (inertialRight * inertialBend.x + inertialUp * inertialBend.y + inertialForward * inertialBend.z) / angle;
                    bendRotation = Quaternion.AngleAxis(angle, axis) * bendRotation;
                }
                node.localRotation = rotation * bendRotation;
            }
            public void Lift(float amount) => node.localPosition = position + parentUp * amount;
            public void Reset()
            {
                if (node == null) return;
                node.localPosition = position; node.localRotation = rotation; node.localScale = scale;
                bend = velocity = targetBend = Vector2.zero;
                inertialBend = inertialVelocity = targetInertia = Vector3.zero;
                if (chainAnchor != null) inertialFrameRotation = chainAnchor.rotation;
            }
        }
    }
}
