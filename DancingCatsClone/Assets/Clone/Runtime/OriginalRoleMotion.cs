using System;
using System.Collections.Generic;
using System.IO;
using RhythmGame.Services;
using UnityEngine;

namespace DancingCats.Clone
{
    /// <summary>Original Animator states and the shipped UnityChan spring equations, using recovered bindings.</summary>
    public sealed class OriginalRoleMotion : MonoBehaviour
    {
        private sealed class Sphere
        {
            public Transform Transform;
            public float Radius;
        }
        private sealed class Spring
        {
            public Transform Bone, Child;
            public Quaternion RestRotation;
            public Vector3 Axis, Force, Tip, PreviousTip;
            public float Length, Radius, Stiffness, Drag, Threshold, DynamicRatio;
            public Sphere[] Colliders;
        }

        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int HoldHash = Animator.StringToHash("Hold");
        private static readonly int EndHash = Animator.StringToHash("End");
        private static readonly int BlendHash = Animator.StringToHash("Blend");
        private Animator animator;
        private Transform model;
        private Spring[] springs = Array.Empty<Spring>();
        private float facingDegrees;
        private bool holdLeft;

        public bool ManualSimulation { get; set; }
        public int SpringBoneCount => springs.Length;
        public Animator Animator => animator;

        public void Bind(GameObject instance, string sourcePrefabName, RoleMotionCatalog catalog)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var binding = catalog.GetBinding(sourcePrefabName);
            model = instance.transform.Find("Model");
            if (model == null) throw new InvalidDataException("Original role is missing its Model transform: " + sourcePrefabName);
            animator = FindAnimator(instance);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var colliderLookup = new Dictionary<string, Sphere>(StringComparer.Ordinal);
            foreach (var collider in binding.colliders ?? Array.Empty<RoleMotionCatalog.ColliderBinding>())
                colliderLookup.Add(collider.path, new Sphere { Transform = RequireTransform(instance.transform, collider.path), Radius = collider.radius });
            var boneLookup = new Dictionary<string, RoleMotionCatalog.BoneBinding>(StringComparer.Ordinal);
            foreach (var bone in binding.bones ?? Array.Empty<RoleMotionCatalog.BoneBinding>()) boneLookup.Add(bone.path, bone);
            var ordered = new List<Spring>();
            foreach (var manager in binding.managers ?? Array.Empty<RoleMotionCatalog.ManagerBinding>())
            {
                foreach (string path in manager.bonePathsInUpdateOrder)
                {
                    if (!boneLookup.TryGetValue(path, out var bone))
                        throw new InvalidDataException("Original spring order references an unknown bone: " + path);
                    var transform = RequireTransform(instance.transform, bone.path);
                    var child = RequireTransform(instance.transform, bone.childPath);
                    var spheres = new Sphere[bone.colliderPaths.Length];
                    for (int i = 0; i < spheres.Length; i++)
                    {
                        if (!colliderLookup.TryGetValue(bone.colliderPaths[i], out spheres[i]))
                            throw new InvalidDataException("Original spring collider binding is missing: " + bone.colliderPaths[i]);
                    }
                    ordered.Add(new Spring
                    {
                        Bone = transform, Child = child, RestRotation = transform.localRotation,
                        Axis = bone.boneAxis, Force = bone.springForce, Radius = bone.radius,
                        Stiffness = bone.stiffnessForce, Drag = bone.dragForce, Threshold = bone.threshold,
                        DynamicRatio = manager.dynamicRatio, Colliders = spheres,
                        Length = Vector3.Distance(transform.position, child.position)
                    });
                }
            }
            if (ordered.Count != boneLookup.Count)
                throw new InvalidDataException("Original spring manifest does not order every bone: " + sourcePrefabName);
            springs = ordered.ToArray();
            ResetMotion();
        }

        public void PlayIdle() => Play(IdleHash);
        public void PlayJump() => Play(JumpHash);

        public void PlayHold(bool follow = false)
        {
            RequireBound();
            animator.SetFloat(BlendHash, holdLeft ? 1f : 0f);
            holdLeft = !holdLeft;
            // Tile.TriggerEnter selects End only for names containing EndPlatform.
            // Follow and normal sliders share the ordinary alternating Hold animation.
            Play(HoldHash);
        }

        public void PlayEnd()
        {
            RequireBound();
            animator.SetFloat(BlendHash, 0f); // Native End uses the unchanged left flag, separate from Hold's toggle.
            Play(EndHash);
        }
        public void SetFacing(float horizonDegrees) => facingDegrees = horizonDegrees;

        public void ResetMotion()
        {
            RequireBound();
            holdLeft = false;
            facingDegrees = 0f;
            animator.speed = 1f;
            animator.SetFloat(BlendHash, 0f);
            animator.Play(IdleHash, 0, 0f);
            animator.Update(0f);
            model.rotation = Quaternion.identity;
            foreach (var spring in springs) spring.Bone.localRotation = spring.RestRotation;
            foreach (var spring in springs) spring.Tip = spring.PreviousTip = spring.Child.position;
        }

        public void Step(float deltaTime)
        {
            RequireBound();
            if (deltaTime <= 0f) return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            // Native Horizen input currently supplies zero degrees; no extra lateral spin is authored here.
            var target = Quaternion.LookRotation(Quaternion.Euler(0f, facingDegrees, 0f) * Vector3.forward);
            model.rotation = Quaternion.Slerp(model.rotation, target, 10f * deltaTime);
            foreach (var spring in springs)
            {
                if (spring.DynamicRatio <= 0f || spring.DynamicRatio <= spring.Threshold) continue;
                UpdateSpring(spring);
            }
        }

        private void LateUpdate()
        {
            if (!ManualSimulation && animator != null) Step(Time.deltaTime);
        }

        private void Play(int state)
        {
            RequireBound();
            animator.speed = 1f;
            // The original Animator.Play calls preserve progress when the requested state is already playing.
            animator.Play(state, 0, float.NegativeInfinity);
        }

        private static void UpdateSpring(Spring spring)
        {
            var bone = spring.Bone;
            bone.localRotation = spring.RestRotation;
            var baseRotation = bone.rotation;
            var oldTip = spring.Tip;
            // Original UpdateSpring divides every force by dt² then multiplies by dt².
            // Cancelling those terms avoids underflow at tiny dt and retains the original frame step.
            spring.Tip += (spring.Tip - spring.PreviousTip) * (1f - spring.Drag)
                + baseRotation * (spring.Axis * spring.Stiffness) + spring.Force;
            spring.Tip = ProjectTip(spring.Tip, bone.position, spring.Length, baseRotation * spring.Axis);
            foreach (var sphere in spring.Colliders)
            {
                var difference = spring.Tip - sphere.Transform.position;
                float combinedRadius = spring.Radius + sphere.Radius;
                if (difference.sqrMagnitude > combinedRadius * combinedRadius) continue;
                var direction = difference.sqrMagnitude > 1e-12f ? difference.normalized : baseRotation * spring.Axis;
                spring.Tip = sphere.Transform.position + direction * combinedRadius;
                spring.Tip = ProjectTip(spring.Tip, bone.position, spring.Length, baseRotation * spring.Axis);
            }
            spring.PreviousTip = oldTip;
            var aim = bone.TransformDirection(spring.Axis);
            var simulated = Quaternion.FromToRotation(aim, spring.Tip - bone.position) * baseRotation;
            bone.rotation = Quaternion.Lerp(baseRotation, simulated, spring.DynamicRatio);
        }

        private static Vector3 ProjectTip(Vector3 tip, Vector3 origin, float length, Vector3 fallback)
        {
            var direction = tip - origin;
            return origin + (direction.sqrMagnitude > 1e-12f ? direction.normalized : fallback.normalized) * length;
        }

        private static Transform RequireTransform(Transform root, string path)
        {
            var transform = string.IsNullOrEmpty(path) ? root : root.Find(path);
            if (transform == null) throw new InvalidDataException("Original role binding cannot resolve: " + path);
            return transform;
        }

        private static Animator FindAnimator(GameObject instance)
        {
            foreach (var candidate in instance.GetComponentsInChildren<Animator>(true))
                if (candidate.runtimeAnimatorController != null && candidate.HasState(0, IdleHash)
                    && candidate.HasState(0, JumpHash) && candidate.HasState(0, HoldHash) && candidate.HasState(0, EndHash))
                    return candidate;
            throw new InvalidDataException("Original role has no compatible recovered Animator: " + instance.name);
        }

        private void RequireBound()
        {
            if (animator == null || model == null) throw new InvalidOperationException("Bind an original role before presenting it.");
        }
    }
}
