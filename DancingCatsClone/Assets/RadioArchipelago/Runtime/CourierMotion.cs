using UnityEngine;

namespace RadioArchipelago
{
    /// <summary>Local presentation only: the session remains authoritative over the actor root and collisions.</summary>
    [DisallowMultipleComponent]
    public sealed class CourierMotion : MonoBehaviour
    {
        [SerializeField] private Transform visualPivot;
        [SerializeField] private Transform antennaPivot;
        [SerializeField] private Transform scarfPivot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        private Quaternion antennaRest, scarfRest, leftRest, rightRest;
        private Vector3 visualRest;
        private float elapsed, phase, facing, facingTarget, secondary;
        private Pose pose;
        private bool holdLeft;
        private bool captured;
        private enum Pose { Idle, Jump, Hold, Celebrate }

        public bool ManualSimulation { get; set; }

        public void Configure(Transform visual, Transform antenna, Transform scarf, Transform armLeft, Transform armRight)
        {
            visualPivot = visual; antennaPivot = antenna; scarfPivot = scarf;
            leftArm = armLeft; rightArm = armRight;
            captured = false;
            ResetMotion();
        }

        private void Awake() => CaptureRestPose();

        private bool CaptureRestPose()
        {
            if (captured) return true;
            if (visualPivot == null) return false;
            visualRest = visualPivot.localPosition;
            antennaRest = antennaPivot != null ? antennaPivot.localRotation : Quaternion.identity;
            scarfRest = scarfPivot != null ? scarfPivot.localRotation : Quaternion.identity;
            leftRest = leftArm != null ? leftArm.localRotation : Quaternion.identity;
            rightRest = rightArm != null ? rightArm.localRotation : Quaternion.identity;
            captured = true;
            return true;
        }

        public void ResetMotion()
        {
            if (!CaptureRestPose()) return;
            elapsed = phase = facing = facingTarget = secondary = 0;
            pose = Pose.Idle;
            visualPivot.localPosition = visualRest;
            visualPivot.localScale = Vector3.one;
            visualPivot.localRotation = Quaternion.identity;
            if (antennaPivot != null) antennaPivot.localRotation = antennaRest;
            if (scarfPivot != null) scarfPivot.localRotation = scarfRest;
            if (leftArm != null) leftArm.localRotation = leftRest;
            if (rightArm != null) rightArm.localRotation = rightRest;
        }

        public void PlayIdle() { pose = Pose.Idle; elapsed = 0; }
        public void PlayJump() { pose = Pose.Jump; elapsed = 0; }
        public void PlayHold(bool left = false) { pose = Pose.Hold; holdLeft = left; elapsed = 0; }
        public void PlayEnd() { pose = Pose.Celebrate; elapsed = 0; }
        public void SetFacing(float horizonDegrees) => facingTarget = Mathf.Clamp(horizonDegrees, -70, 70);

        public void SimulateSprings(float deltaTime)
        {
            if (!CaptureRestPose() || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            float dt = Mathf.Min(deltaTime, .1f);
            phase += dt; elapsed += dt;
            facing = Mathf.Lerp(facing, facingTarget, 1 - Mathf.Exp(-12 * dt));
            float bob = Mathf.Sin(phase * 3.2f) * .018f;
            float tilt = Mathf.Sin(phase * 2.1f) * 2;
            float pitch = 0, stretch = 0, armLift = 0;
            switch (pose)
            {
                case Pose.Jump:
                    float attack = Mathf.Sin(Mathf.Clamp01(elapsed / .32f) * Mathf.PI);
                    stretch = attack * .075f;
                    pitch = -attack * 8;
                    armLift = attack * 20;
                    if (elapsed > .34f) { pose = Pose.Idle; elapsed = 0; }
                    break;
                case Pose.Hold:
                    tilt += (holdLeft ? -1 : 1) * 9;
                    pitch = 5;
                    bob *= .35f;
                    armLift = 14 + Mathf.Sin(phase * 5) * 3;
                    break;
                case Pose.Celebrate:
                    bob += Mathf.Abs(Mathf.Sin(elapsed * 7)) * .065f;
                    tilt += Mathf.Sin(elapsed * 5) * 7;
                    armLift = 115 + Mathf.Sin(elapsed * 7) * 8;
                    break;
            }
            visualPivot.localPosition = visualRest + Vector3.up * bob;
            visualPivot.localRotation = Quaternion.Euler(pitch, facing, tilt);
            visualPivot.localScale = new Vector3(1 - stretch * .45f, 1 + stretch, 1 - stretch * .45f);
            float target = Mathf.Sin(phase * 4) * 3 + pitch * .6f - tilt * .65f;
            secondary = Mathf.Lerp(secondary, target, 1 - Mathf.Exp(-7 * dt));
            if (antennaPivot != null) antennaPivot.localRotation = antennaRest * Quaternion.Euler(secondary * .6f, 0, secondary);
            if (scarfPivot != null) scarfPivot.localRotation = scarfRest * Quaternion.Euler(secondary * 1.5f, Mathf.Sin(phase * 5) * 5, secondary * .5f);
            if (leftArm != null) leftArm.localRotation = leftRest * Quaternion.Euler(0, 0, -armLift);
            if (rightArm != null) rightArm.localRotation = rightRest * Quaternion.Euler(0, 0, armLift);
        }

        private void LateUpdate()
        {
            if (!ManualSimulation) SimulateSprings(Time.deltaTime);
        }
    }
}
