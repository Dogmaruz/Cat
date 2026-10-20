using System.Collections.Generic;
using UnityEngine;

namespace DancingCat
{
    // Authored rig animation and secondary tail motion never own gameplay movement.
    public sealed class CatVisualMotion : MonoBehaviour
    {
        private sealed class TailBone
        {
            public Transform transform;
            public Vector3 localTip;
            public Quaternion rest, applied;
            public Vector3 bend, bendVelocity;
        }

        private sealed class RestArm
        {
            public Transform transform;
            public Quaternion rotation;
        }

        private readonly List<TailBone> _tail = new List<TailBone>();
        private readonly List<RestArm> _fallbackArms = new List<RestArm>();
        private Animator _animator;
        private Transform _model;
        private Vector3 _modelPosition, _modelScale, _previousRootPosition;
        private Vector3 _rootVelocity, _rootAngularVelocity;
        private Quaternion _modelRotation, _baseRotation, _previousRootRotation;
        private CatRunState _lastRun;
        private float _lastSongTime, _posePhase, _idleTime, _landingAge = 10;
        private bool _bound;

        public string CurrentPose { get; private set; } = "Idle";
        public int TailBoneCount => _tail.Count;

        private void Start() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _baseRotation = transform.localRotation;
            _model = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (_model != null)
            {
                _modelPosition = _model.localPosition;
                _modelRotation = _model.localRotation;
                _modelScale = _model.localScale;
            }
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _animator.speed = 0;
                _animator.Rebind();
                _animator.Update(0);
            }
            foreach (Transform bone in GetComponentsInChildren<Transform>())
            {
                string name = bone.name.ToLowerInvariant();
                bool customTail = name.StartsWith("bone") && bone.GetComponent<Rigidbody>() != null;
                if (!name.Contains("end") && (name == "tail" || name.StartsWith("tail.") || customTail))
                {
                    // Actual model endpoints set length; local positions are never changed.
                    if (bone.childCount == 0) continue;
                    Vector3 localTip = bone.GetChild(0).localPosition;
                    if (localTip.sqrMagnitude < .00000001f) continue;
                    _tail.Add(new TailBone { transform = bone, localTip = localTip, rest = bone.localRotation });
                }
                if (name.StartsWith("upper_arm") && bone.childCount > 0)
                    _fallbackArms.Add(new RestArm { transform = bone, rotation = bone.localRotation });
            }
            SamplePose();
            ResetTail();
        }

        public void Present(CatRunState state, float x)
        {
            Bind();
            bool newTimeline = state != _lastRun || state != null && state.Time < _lastSongTime;
            _lastRun = state;
            _lastSongTime = state?.Time ?? 0;
            _posePhase = 0;
            float turn = 0;
            if (state == null || state.Current == null)
                CurrentPose = "Idle";
            else if (state.Finished || state.Failed)
                CurrentPose = "End";
            else
            {
                var note = state.Current;
                float departure = note.time + note.duration;
                if (note.duration > 0 && state.Time < departure)
                {
                    CurrentPose = "Hold";
                    _posePhase = Mathf.Clamp01((state.Time - note.time) / note.duration);
                    // Our authored dance turn is independent from collision and input.
                    turn = _posePhase * 360 * (state.NextIndex % 2 == 0 ? -1 : 1);
                }
                else if (state.Next != null && state.Time < state.Next.time)
                {
                    CurrentPose = "Jump";
                    _posePhase = Mathf.InverseLerp(departure, state.Next.time, state.Time);
                }
                else CurrentPose = "End";
            }
            transform.localRotation = _baseRotation * Quaternion.Euler(0, turn, 0);
            SamplePose();
            if (newTimeline)
            {
                _landingAge = 10;
                ResetTail();
            }
        }

        private void SamplePose()
        {
            bool authored = false;
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                int hash = StateHash(CurrentPose);
                // Compatibility for the source Ginger controller. New controllers use explicit states.
                if (hash == 0 && CurrentPose == "Jump") hash = StateHash("Armature|Jump_Pose_001");
                if (hash == 0 && CurrentPose != "Jump") hash = StateHash("Armature|Idel_Pose");
                if (hash != 0)
                {
                    float phase = CurrentPose == "Idle" || CurrentPose == "End" ? _idleTime % 1 : _posePhase;
                    _animator.Play(hash, 0, Mathf.Clamp01(phase));
                    _animator.Update(0);
                    authored = true;
                }
            }
            if (!authored) ApplyFallbackPose();
            // Animator sampling may not reset the model's normalization or facing.
            if (_model != null)
            {
                _model.localPosition = _modelPosition;
                _model.localRotation = _modelRotation;
                _model.localScale = _modelScale;
            }
        }

        private int StateHash(string name)
        {
            int hash = Animator.StringToHash("Base Layer." + name);
            return _animator.HasState(0, hash) ? hash : 0;
        }

        private void ApplyFallbackPose()
        {
            // Preview setup supplies compatible clips. A missing controller still gets a modest
            // arms-down pose rather than exposing the package humanoid's bind/T-pose.
            foreach (RestArm arm in _fallbackArms)
            {
                if (arm.transform == null) continue;
                arm.transform.localRotation = arm.rotation;
                Vector3 direction = arm.transform.GetChild(0).position - arm.transform.position;
                Vector3 down = Vector3.down + transform.forward * (CurrentPose == "Jump" ? .35f : .12f);
                if (direction.sqrMagnitude > .00000001f)
                    arm.transform.rotation = Quaternion.FromToRotation(direction, down) * arm.transform.rotation;
            }
        }

        public void Land() => _landingAge = 0;

        private void ResetTail()
        {
            foreach (TailBone bone in _tail)
            {
                if (bone.transform == null) continue;
                bone.transform.localRotation = bone.rest;
                bone.applied = bone.rest;
                bone.bend = bone.bendVelocity = Vector3.zero;
            }
            _previousRootPosition = transform.position;
            _previousRootRotation = transform.rotation;
            _rootVelocity = _rootAngularVelocity = Vector3.zero;
        }

        // Explicit step lets validation exercise the real spring at several frame rates.
        public void StepSecondaryMotion(float deltaTime)
        {
            Bind();
            if (!(deltaTime > 0) || float.IsInfinity(deltaTime)) return;
            float dt = Mathf.Min(deltaTime, .25f);
            _idleTime += dt;
            _landingAge += dt;
            if (CurrentPose == "Idle" || CurrentPose == "End") SamplePose();
            // Home, revive and scene teleports reset history instead of imparting huge impulses.
            if ((transform.position - _previousRootPosition).sqrMagnitude > 4) ResetTail();
            float filter = 1 - Mathf.Exp(-18 * dt);
            Vector3 velocity = Vector3.ClampMagnitude((transform.position - _previousRootPosition) / dt, 20);
            Vector3 previousVelocity = _rootVelocity;
            _rootVelocity = Vector3.Lerp(_rootVelocity, velocity, filter);
            Vector3 acceleration = Vector3.ClampMagnitude((_rootVelocity - previousVelocity) / dt, 60);
            Quaternion turn = transform.rotation * Quaternion.Inverse(_previousRootRotation);
            turn.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180) degrees -= 360;
            Vector3 angularVelocity = degrees == 0 ? Vector3.zero : axis * (degrees / dt);
            _rootAngularVelocity = Vector3.Lerp(_rootAngularVelocity,
                Vector3.ClampMagnitude(angularVelocity, 720), filter);
            _previousRootPosition = transform.position;
            _previousRootRotation = transform.rotation;
            Vector3 force = -_rootVelocity * .3f - acceleration * .08f + Vector3.down * .2f;
            for (int i = 0; i < _tail.Count; i++)
            {
                TailBone bone = _tail[i];
                Transform joint = bone.transform;
                if (joint == null) continue;
                joint.localRotation = bone.rest;
                // Local torque comes from translation, jumping and angular inertia. Keeping
                // angular state avoids feeding a projected world-tip correction back as velocity.
                Quaternion worldToRest = Quaternion.Inverse(joint.rotation);
                Vector3 torque = Vector3.Cross(bone.localTip.normalized, worldToRest * force) * 4;
                torque -= worldToRest * _rootAngularVelocity * .025f;
                Vector3 target = Vector3.ClampMagnitude(torque * (.65f + i * .1f), 35);
                const float frequency = 18;
                Vector3 displacement = bone.bend - target;
                Vector3 coefficient = bone.bendVelocity + displacement * frequency;
                float decay = Mathf.Exp(-frequency * dt);
                // Exact critically damped spring solution for this frame's target remains
                // stable for tiny frames and hitches, without an explicit Euler feedback loop.
                bone.bend = Vector3.ClampMagnitude(target + (displacement + coefficient * dt) * decay, 35);
                bone.bendVelocity = (bone.bendVelocity - coefficient * (frequency * dt)) * decay;
                float angle = bone.bend.magnitude;
                Quaternion offset = angle > .00001f ? Quaternion.AngleAxis(angle, bone.bend / angle) : Quaternion.identity;
                bone.applied = Quaternion.RotateTowards(bone.applied, bone.rest * offset, 360 * dt);
                joint.localRotation = bone.applied;
            }
            float squash = Mathf.Exp(-_landingAge * 18) * .14f;
            transform.localScale = new Vector3(1 + squash * .5f, 1 - squash, 1 + squash * .5f);
        }

        private void LateUpdate() => StepSecondaryMotion(Time.unscaledDeltaTime);
    }
}
