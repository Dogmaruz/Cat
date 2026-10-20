using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class OriginalRoleMotionValidation
    {
        [Serializable] private sealed class Manifest { public Role[] roles; }
        [Serializable] private sealed class Role { public string prefab, assetPath; public Bone[] bones; }
        [Serializable] private sealed class Bone { public string path, childPath; }

        public static void Run()
        {
            var type = typeof(RecoveredCatalog).Assembly.GetType("DancingCats.Clone.OriginalRoleMotion");
            Require(type != null, "original role motion has not been implemented");
            var json = Resources.Load<TextAsset>("OriginalData/role-spring-bindings");
            Require(json != null, "original spring binding manifest is available");
            var manifest = JsonUtility.FromJson<Manifest>(json.text);
            Require(manifest.roles.Length == 63, "all 63 original role prefabs are represented");
            int animated = 0, springs = 0;
            foreach (var role in manifest.roles)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(role.assetPath);
                Require(prefab != null, "prefab exists: " + role.assetPath);
                var owner = new GameObject("OriginalRoleMotion validation");
                try
                {
                    var instance = UnityEngine.Object.Instantiate(prefab, owner.transform);
                    var motion = owner.AddComponent(type);
                    type.GetProperty("ManualSimulation").SetValue(motion, true);
                    Invoke(type, motion, "Bind", instance, role.prefab);
                    Require((int)type.GetProperty("SpringBoneCount").GetValue(motion) == role.bones.Length,
                        role.prefab + " binds exactly its authored tail bones");
                    var animator = instance.GetComponentInChildren<Animator>(true);
                    Require(animator != null && animator.runtimeAnimatorController != null,
                        role.prefab + " retains its original Animator Controller");
                    foreach (string state in new[] { "Idle", "Jump", "Hold", "End" })
                        Require(animator.HasState(0, Animator.StringToHash(state)), role.prefab + " contains " + state);
                    Invoke(type, motion, "PlayIdle");
                    animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorClipInfo(0).Length > 0,
                        role.prefab + " samples a real imported animation clip");
                    Invoke(type, motion, "PlayHold", false);
                    animator.Update(.02f);
                    float firstBlend = animator.GetFloat("Blend");
                    Invoke(type, motion, "PlayHold", false);
                    animator.Update(.02f);
                    Require(Mathf.Abs(firstBlend - animator.GetFloat("Blend")) > .9f,
                        role.prefab + " alternates the original Hold blend");
                    Invoke(type, motion, "PlayHold", true);
                    animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Hold"),
                        role.prefab + " keeps Follow on the original Hold state");
                    Invoke(type, motion, "PlayJump");
                    animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Jump"),
                        role.prefab + " enters Jump");
                    var bones = new List<Transform>();
                    foreach (var bone in role.bones)
                    {
                        var transform = instance.transform.Find(bone.path);
                        Require(transform != null && instance.transform.Find(bone.childPath) != null,
                            role.prefab + " resolves authored bone pointers");
                        bones.Add(transform);
                    }
                    foreach (float delta in new[] { 1f / 30f, 1f / 60f, 1f / 144f, .0001f, .2f })
                    {
                        Invoke(type, motion, "ResetMotion");
                        for (int frame = 0; frame < 16; frame++)
                        {
                            owner.transform.position = new Vector3(Mathf.Sin(frame * .2f), Mathf.Sin(frame * .1f), frame * .03f);
                            animator.Update(delta);
                            var positions = new Vector3[bones.Count];
                            for (int i = 0; i < bones.Count; i++) positions[i] = bones[i].localPosition;
                            Invoke(type, motion, "Step", delta);
                            for (int i = 0; i < bones.Count; i++)
                            {
                                Require(Vector3.Distance(positions[i], bones[i].localPosition) < .00001f,
                                    role.prefab + " preserves bone positions at " + delta);
                                var rotation = bones[i].localRotation;
                                Require(IsFinite(rotation.x) && IsFinite(rotation.y) && IsFinite(rotation.z) && IsFinite(rotation.w),
                                    role.prefab + " keeps a finite physical tail pose");
                            }
                        }
                    }
                    animated++;
                    springs += bones.Count;
                }
                finally { UnityEngine.Object.DestroyImmediate(owner); }
            }
            Debug.Log("ORIGINAL_ROLE_MOTION PASS: " + animated + " imported rigs, " + springs + " exact spring bones, native states and preserved lengths");
        }

        private static void Invoke(Type type, Component component, string name, params object[] arguments)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Require(method != null, "motion interface exists: " + name);
            method.Invoke(component, arguments);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("ORIGINAL_ROLE_MOTION RED/FAIL: " + message);
        }
    }
}
