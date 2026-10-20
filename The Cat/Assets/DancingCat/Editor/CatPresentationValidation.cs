using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DancingCat.Editor
{
    // These checks instantiate the real project rigs and sample their real clips.
    // They do not substitute for the root project's PlayMode/video validation.
    public static class CatPresentationValidation
    {
        private static readonly string[] Models = {
            "Assets/Prefabs/Orange_Cat_001.prefab",
            "Assets/Lowpoly Humanoid Cat/Prefab/Shiro.prefab",
            "Assets/Lowpoly Humanoid Cat/Prefab/Kuroneko.prefab",
            "Assets/Lowpoly Humanoid Cat/Prefab/Mikeneko.prefab",
            "Assets/Lowpoly Humanoid Cat/Prefab/Silver.prefab"
        };

        [MenuItem("Dancing Cat/Validate cat presentation")]
        public static void Run()
        {
            var pose = typeof(CatVisualMotion).GetProperty("CurrentPose");
            var step = typeof(CatVisualMotion).GetMethod("StepSecondaryMotion", new[] { typeof(float) });
            Require(pose != null && step != null,
                "Explicit Idle/Jump/Hold/End presentation and a deterministic spring step are missing");
            foreach (string path in Models) ValidateRig(path, pose, step);
            Debug.Log("CAT_PRESENTATION PASS: five real rigs animate; tail springs react and preserve their bones");
        }

        private static void ValidateRig(string path, PropertyInfo pose, MethodInfo step)
        {
            GameObject root = new GameObject("CatPresentationCheck");
            AnimatorController controller = null;
            var songs = new List<CatSong>();
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null, "Missing model " + path);
                var model = Object.Instantiate(prefab, visual.transform);
                model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                foreach (var body in model.GetComponentsInChildren<Rigidbody>())
                { body.isKinematic = true; body.detectCollisions = false; }
                foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;

                Require(model.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0,
                    "The real skinned mesh is absent: " + path);
                var animator = model.GetComponentInChildren<Animator>();
                Require(animator != null, "The real rig has no Animator: " + path);
                bool ginger = path.Contains("Orange_Cat");
                string clipPath = ginger ? "Assets/Objects/Cat_001.fbx" :
                    "Assets/Lowpoly Humanoid Cat/Model_Animation/catwait.fbx";
                AnimationClip idle = Clip(clipPath, ginger ? "Idel_Pose" : "catwait");
                AnimationClip jump = Clip(ginger ? clipPath :
                    "Assets/Lowpoly Humanoid Cat/Model_Animation/catjump.fbx", ginger ? "Jump_Pose" : "catjump");
                controller = new AnimatorController { name = "CatValidationController", hideFlags = HideFlags.HideAndDontSave };
                controller.AddLayer("Base Layer");
                var machine = controller.layers[0].stateMachine;
                foreach (string stateName in new[] { "Idle", "Jump", "Hold", "End" })
                {
                    var state = machine.AddState(stateName);
                    state.motion = stateName == "Jump" ? jump : idle;
                    if (stateName == "Idle") machine.defaultState = state;
                }
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                var motion = visual.AddComponent<CatVisualMotion>();
                motion.Present(null, 0);
                Require((string)pose.GetValue(motion) == "Idle", "Menu must select Idle: " + path);
                var transforms = model.GetComponentsInChildren<Transform>();
                var idleRotations = transforms.Select(t => t.localRotation).ToArray();

                var song = Song(songs, false);
                var run = new CatRunState(song);
                run.Advance(0, 0); run.Advance(.35f, 0);
                motion.Present(run, 0);
                Require((string)pose.GetValue(motion) == "Jump", "Flight must select Jump: " + path);
                Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"),
                    "The Animator did not enter the Jump state: " + path);
                bool changed = false;
                for (int i = 0; i < transforms.Length; i++)
                    if (Quaternion.Angle(idleRotations[i], transforms[i].localRotation) > .1f) changed = true;
                Require(changed, "Jump clip did not animate the actual skeleton: " + path);

                var holdSong = Song(songs, true);
                var hold = new CatRunState(holdSong);
                hold.Advance(0, 0); hold.Advance(.5f, 0);
                motion.Present(hold, 0);
                Require((string)pose.GetValue(motion) == "Hold", "Support must select Hold: " + path);
                Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Hold"), "The Animator did not enter Hold: " + path);

                var tail = transforms.Where(t => IsTail(t)).ToArray();
                Require(tail.Length > 0, "No existing tail bones were found: " + path);
                var localPositions = tail.Select(t => t.localPosition).ToArray();
                var rest = tail.Select(t => t.localRotation).ToArray();
                root.transform.position += new Vector3(.3f, .65f, 0);
                visual.transform.localRotation *= Quaternion.Euler(0, 65, 0);
                step.Invoke(motion, new object[] { 1f / 60 });
                Require(tail.Where((t, i) => Quaternion.Angle(rest[i], t.localRotation) > .05f).Any(),
                    "Tail must react to vertical/turn inertia without lateral input: " + path);
                Vector3 gameplayPosition = root.transform.position;
                for (int frame = 0; frame < 180; frame++)
                {
                    float dt = frame % 30 == 0 ? .2f : frame % 2 == 0 ? 1f / 30 : 1f / 144;
                    step.Invoke(motion, new object[] { dt });
                    for (int i = 0; i < tail.Length; i++)
                    {
                        Require((tail[i].localPosition - localPositions[i]).sqrMagnitude < .0000001f,
                            "Spring changed a bone length/position: " + path);
                        Quaternion q = tail[i].localRotation;
                        Require(IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w),
                            "Spring became non-finite after a long frame: " + path);
                        Require(Quaternion.Angle(rest[i], q) <= 36,
                            "Tail exceeded its angular bound: " + path);
                    }
                }
                Require(root.transform.position == gameplayPosition, "Presentation moved gameplay root: " + path);
                ValidateTailContinuity(root, motion, tail, songs, path, step);
                hold.Advance(2, 0);
                motion.Present(hold, 0);
                Require((string)pose.GetValue(motion) == "End", "Completion must select End: " + path);
                Require(Quaternion.Angle(visual.transform.localRotation, Quaternion.identity) < .1f,
                    "Completion retained an unfinished hold turn: " + path);
                Debug.Log("CAT_PRESENTATION PASS: " + path);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (controller != null)
                {
                    foreach (var layer in controller.layers) Object.DestroyImmediate(layer.stateMachine);
                    Object.DestroyImmediate(controller);
                }
                foreach (var song in songs) Object.DestroyImmediate(song);
            }
        }

        private static void ValidateTailContinuity(GameObject root, CatVisualMotion motion, Transform[] tail,
            List<CatSong> cleanup, string path, MethodInfo step)
        {
            // Sample animation on every frame, as the real controller does. A stationary root
            // after one impulse does not expose feedback from moving short tail segments.
            var song = ScriptableObject.CreateInstance<CatSong>();
            cleanup.Add(song);
            song.duration = 8;
            song.notes.Add(new CatNote { time = 0 });
            song.notes.Add(new CatNote { time = .75f, duration = 1.25f, kind = CatNoteKind.Hold });
            song.notes.Add(new CatNote { time = 2.75f });
            song.notes.Add(new CatNote { time = 3.5f, duration = 1, kind = CatNoteKind.Hold });
            song.notes.Add(new CatNote { time = 6 });
            foreach (int rate in new[] { 30, 60, 144, 0 })
            {
                var run = new CatRunState(song);
                run.Advance(0, 0);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                motion.Present(run, 0);
                step.Invoke(motion, new object[] { 1f / 60 });
                Quaternion[] previous = tail.Select(t => t.localRotation).ToArray();
                float time = 0, maximumSpeed = 0, worstAngle = 0, worstDelta = 0;
                string worstBone = "";
                int frame = 0;
                while (time < 5.2f)
                {
                    float dt = rate > 0 ? 1f / rate : frame % 67 == 0 ? .18f : frame % 19 == 0 ? .001f : 1f / 60;
                    time += dt;
                    run.Advance(time, 0);
                    Vector3 position = new Vector3(.35f * Mathf.Sin(time * 2.1f),
                        .35f * Mathf.Sin(time * 6.2f), time * 7);
                    root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 25 * Mathf.Sin(time * 1.3f), 0));
                    motion.Present(run, 0);
                    step.Invoke(motion, new object[] { dt });
                    Require(root.transform.position == position, "Tail spring moved the gameplay root: " + path);
                    for (int i = 0; i < tail.Length; i++)
                    {
                        float angle = Quaternion.Angle(previous[i], tail[i].localRotation);
                        float speed = angle / dt;
                        if (speed > maximumSpeed)
                        {
                            maximumSpeed = speed; worstAngle = angle; worstDelta = dt; worstBone = tail[i].name;
                        }
                        previous[i] = tail[i].localRotation;
                    }
                    frame++;
                }
                Require(maximumSpeed <= 720.5f,
                    "Tail continuity exceeded 720 deg/s: " + path + ", profile=" + (rate == 0 ? "tiny/hitch" : rate.ToString()) +
                    ", bone=" + worstBone + ", speed=" + maximumSpeed + ", angle=" + worstAngle + ", dt=" + worstDelta);
            }
        }

        private static CatSong Song(List<CatSong> cleanup, bool hold)
        {
            var song = ScriptableObject.CreateInstance<CatSong>();
            cleanup.Add(song);
            song.duration = 2;
            song.notes.Add(new CatNote { time = 0, duration = hold ? 1 : 0, kind = hold ? CatNoteKind.Hold : CatNoteKind.Short });
            song.notes.Add(new CatNote { time = hold ? 1.5f : 1 });
            return song;
        }

        private static AnimationClip Clip(string path, string token)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.name.Contains(token));
            Require(clip != null, "Missing compatible clip " + path + " / " + token);
            return clip;
        }

        private static bool IsTail(Transform bone)
        {
            string name = bone.name.ToLowerInvariant();
            return !name.Contains("end") && (name == "tail" || name.StartsWith("tail.") ||
                name.StartsWith("bone") && bone.GetComponent<Rigidbody>() != null);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception("CAT_PRESENTATION FAIL: " + message); }
    }
}
