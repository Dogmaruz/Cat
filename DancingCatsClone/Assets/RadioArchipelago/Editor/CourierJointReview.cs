using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Targeted imported-skin review for radio lips, the rotating bearing and shoulder placement.</summary>
    public static class CourierJointReview
    {
        private const int Layer = 27, Size = 900;
        [Serializable] private sealed class Clearance
        {
            public int shoulderDegrees;
            public float trialForwardShift;
            public float leftRear, rightRear, packFront, minimumGap, minimumGapOverBodyHeight;
            public bool pawsAheadOfPack;
            public float leftStrapMargin, rightStrapMargin;
            public bool pawsInsideHarness;
        }
        [Serializable] private sealed class Input { public string prefab, dependencyHash; }
        [Serializable] private sealed class Report
        {
            public string stage, capturedUtc, unityVersion;
            public Input[] inputs;
            public Clearance[] shoulderClearance;
            public string[] frames;
        }

        public static void Baseline() => Render("baseline-straps", false);
        public static void Candidate() => Render("candidate", true);

        public static void ShoulderTrials()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Shoulder trials require Edit Mode.");
            using (var digest = System.Security.Cryptography.SHA256.Create())
            {
                string sourceHash = BitConverter.ToString(digest.ComputeHash(File.ReadAllBytes(
                    "Assets/RadioArchipelago/Imported/Lumen/Model.fbx"))).Replace("-", "").ToLowerInvariant();
                if (sourceHash != "9a3bf7bfa081ca65d4bc9827f9c1c5bd1ebaeb2f0e1177f28c948e1b0016246e")
                    throw new InvalidOperationException("Translation trials require the preserved 50caaf5 Lumen input; use the frozen trial report after importing its replacement.");
            }
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            var entry = catalog.entries.Single(e => e.id == "sail");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var actor = UnityEngine.Object.Instantiate(entry.prefab); SceneManager.MoveGameObjectToScene(actor, scene);
                var motion = actor.GetComponentInChildren<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                var rig = (CourierRigBinding)typeof(CourierMotion).GetField("rig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motion);
                float height = CourierVisualBounds.MeasureBody(actor.GetComponentsInChildren<Renderer>(true)).size.y;
                Vector3 leftPosition = rig.leftArm.position, rightPosition = rig.rightArm.position;
                Quaternion leftRest = rig.leftArm.localRotation, rightRest = rig.rightArm.localRotation;
                Vector3 leftAxis = rig.leftArm.InverseTransformDirection(rig.visualPivot.forward).normalized;
                Vector3 rightAxis = rig.rightArm.InverseTransformDirection(rig.visualPivot.forward).normalized;
                float authoringScale = Vector3.Distance(leftPosition, rightPosition) / new Vector3(.54f, .01f, .01f).magnitude;
                var rows = new List<Clearance>();
                foreach (float shift in new[] { 0, .04f, .06f, .08f, .10f, .12f, .22f }) foreach (int angle in new[] { -10, 30, 60 })
                {
                    // Translation of these rigid bones in a disposable old rig has the same
                    // skin result as translating the authored rest bone and complete paw together.
                    rig.leftArm.position = leftPosition + rig.visualPivot.forward * (shift * authoringScale);
                    rig.rightArm.position = rightPosition + rig.visualPivot.forward * (shift * authoringScale);
                    rig.leftArm.localRotation = leftRest * Quaternion.AngleAxis(-angle, leftAxis);
                    rig.rightArm.localRotation = rightRest * Quaternion.AngleAxis(angle, rightAxis);
                    var row = MeasureClearance(actor, rig, height, angle); row.trialForwardShift = shift; rows.Add(row);
                }
                string folder = Path.GetFullPath("Library/RadioRigging/ShoulderPivotReview/Unity"); Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "placement-trials.json"), JsonUtility.ToJson(new Report {
                    stage = "old-rig-disposable-translation-trials", capturedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                    shoulderClearance = rows.ToArray(), inputs = new[] { new Input { prefab = AssetDatabase.GetAssetPath(entry.prefab),
                        dependencyHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(entry.prefab)).ToString() } }, frames = Array.Empty<string>() }, true));
                Debug.Log("COURIER_SHOULDER_TRIALS captured actual imported skin against unchanged harness; no prefab or scene modified.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Render(string stage, bool requirePlacement)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Joint review requires Edit Mode.");
            string folder = Path.GetFullPath("Library/RadioRigging/ShoulderPivotReview/Unity/" + stage);
            if (stage.StartsWith("baseline", StringComparison.Ordinal) && File.Exists(Path.Combine(folder, "manifest.json")))
                throw new InvalidOperationException("The pre-fix joint baseline is already frozen.");
            Directory.CreateDirectory(folder);
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            if (catalog?.entries == null) throw new InvalidOperationException("Missing courier catalog.");
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            var target = new RenderTexture(Size, Size, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            var clay = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            clay.SetColor("_BaseColor", new Color(.55f, .59f, .63f)); clay.SetFloat("_Smoothness", .2f);
            var frames = new List<string>(); var inputs = new List<Input>(); var clearances = new List<Clearance>();
            try
            {
                var camera = Node(scene, "Joint review camera").AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << Layer;
                camera.orthographic = true; camera.aspect = 1; camera.nearClipPlane = .03f; camera.farClipPlane = 40;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.96f, .95f, .92f);
                target.Create(); camera.targetTexture = target;
                Lamp(scene, new Vector3(28, 145, 0), new Color(1, .95f, .87f), 1.2f);
                Lamp(scene, new Vector3(18, -25, 0), new Color(.82f, .91f, 1), .6f);
                foreach (var entry in catalog.entries.Where(e => e.id == "signal" || e.id == "sail"))
                {
                    string path = AssetDatabase.GetAssetPath(entry.prefab);
                    inputs.Add(new Input { prefab = path, dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString() });
                    var actor = UnityEngine.Object.Instantiate(entry.prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                        foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                        var motion = actor.GetComponentInChildren<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                        var rig = (CourierRigBinding)typeof(CourierMotion).GetField("rig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motion);
                        float height = CourierVisualBounds.MeasureBody(actor.GetComponentsInChildren<Renderer>(true)).size.y;
                        Quaternion leftRest = rig.leftArm.localRotation, rightRest = rig.rightArm.localRotation;
                        Vector3 leftAxis = rig.leftArm.InverseTransformDirection(rig.visualPivot.forward).normalized;
                        Vector3 rightAxis = rig.rightArm.InverseTransformDirection(rig.visualPivot.forward).normalized;
                        using (var bake = new CourierPreviewSkinBake(actor))
                        {
                            var materials = bake.Renderers.Select(r => r.sharedMaterials).ToArray();
                            void Shoulders(int angle)
                            {
                                rig.leftArm.localRotation = leftRest * Quaternion.AngleAxis(-angle, leftAxis);
                                rig.rightArm.localRotation = rightRest * Quaternion.AngleAxis(angle, rightAxis);
                            }
                            void Capture(string label, Vector3 center, float zoom, float yaw, float pitch = 0)
                            {
                                bake.Refresh(); camera.orthographicSize = zoom;
                                camera.transform.position = center + Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * 5;
                                camera.transform.LookAt(center);
                                foreach (bool untextured in new[] { false, true })
                                {
                                    for (int i = 0; i < bake.Renderers.Length; i++) bake.Renderers[i].sharedMaterials = untextured
                                        ? Enumerable.Repeat(clay, materials[i].Length).ToArray() : materials[i];
                                    camera.Render(); RenderTexture.active = target;
                                    pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); pixels.Apply();
                                    string file = entry.id + "-" + label + (untextured ? "-clay" : "-beauty") + ".png";
                                    File.WriteAllBytes(Path.Combine(folder, file), pixels.EncodeToPNG()); frames.Add(file);
                                }
                            }
                            if (entry.id == "sail")
                            {
                                foreach (int angle in new[] { -10, 30, 60 })
                                {
                                    Shoulders(angle);
                                    clearances.Add(MeasureClearance(actor, rig, height, angle));
                                    Vector3 center = (rig.leftArm.position + rig.rightArm.position) * .5f;
                                    Capture("arms-" + angle + "-right", center, height * .52f, 90);
                                    Capture("arms-" + angle + "-left", center, height * .52f, 270);
                                    Capture("arms-" + angle + "-front", center, height * .52f, 0);
                                }
                            }
                            else
                            {
                                foreach (int angle in new[] { 30, 60 })
                                {
                                    Shoulders(angle);
                                    Vector3 center = (rig.leftArm.position + rig.rightArm.position) * .5f
                                        - Vector3.up * height * .1f - rig.visualPivot.forward * height * .12f;
                                    Capture("case-" + angle + "-right", center, height * .34f, 90);
                                    Capture("case-" + angle + "-left", center, height * .34f, 270);
                                }
                                var yawBone = rig.antennaYawPivot; var first = rig.antennaChain[0];
                                Quaternion yawRest = yawBone.localRotation, leanRest = first.localRotation;
                                Vector3 yawAxis = yawBone.InverseTransformDirection(rig.visualPivot.up).normalized;
                                Vector3 leanAxis = first.InverseTransformDirection(rig.visualPivot.right).normalized;
                                // Keep one fixed camera through every yaw: tracking the turning
                                // coupler would conceal an incorrectly placed rotation center.
                                Vector3 jointCenter = (yawBone.position + first.position) * .5f;
                                foreach (int lean in new[] { 0, 10 }) foreach (int yaw in new[] { 0, 45, 90, 145, 180, 270, 360 })
                                {
                                    yawBone.localRotation = yawRest * Quaternion.AngleAxis(yaw, yawAxis);
                                    first.localRotation = leanRest * Quaternion.AngleAxis(lean, leanAxis);
                                    Capture("hinge-" + yaw + "-lean-" + lean + "-front", jointCenter, height * .15f, 0);
                                    Capture("hinge-" + yaw + "-lean-" + lean + "-side", jointCenter, height * .15f, 90);
                                }
                            }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
                File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(new Report {
                    stage = stage, capturedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                    inputs = inputs.ToArray(), shoulderClearance = clearances.ToArray(), frames = frames.ToArray() }, true));
                if (clearances.Count != 3 || (requirePlacement && clearances.Any(row => !row.pawsInsideHarness)))
                    throw new InvalidOperationException("Lumen's paw depth must fit the actual strap aperture, rather than covering its front or rear edge. See joint manifest.");
                Debug.Log("COURIER_JOINT_REVIEW PASS " + stage + ": " + frames.Count + " captures; placement required=" + requirePlacement);
            }
            finally
            {
                RenderTexture.active = previous; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(clay);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Clearance MeasureClearance(GameObject actor, CourierRigBinding rig, float height, int angle)
        {
            var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var scratch = new Mesh();
            try
            {
                float Extreme(string name, bool minimum)
                {
                    var skin = skins.Single(s => s.name.Contains(name)); skin.BakeMesh(scratch, true);
                    var values = scratch.vertices.Select(v => Vector3.Dot(skin.transform.TransformPoint(v), rig.visualPivot.forward));
                    return minimum ? values.Min() : values.Max();
                }
                float left = Extreme("Arm.L_Rounded", true), right = Extreme("Arm.R_Rounded", true), front = Extreme("IndependentPack", false);
                float gap = Mathf.Min(left, right) - front;
                float StrapMargin(Transform shoulder, string pawName)
                {
                    var harness = skins.Single(s => s.name.Contains("Harness")); harness.BakeMesh(scratch, true);
                    var vertices = scratch.vertices.Select(v => harness.transform.TransformPoint(v)).ToArray();
                    var triangles = scratch.triangles; var crossings = new List<float>();
                    float sign = Mathf.Sign(Vector3.Dot(shoulder.position - rig.visualPivot.position, rig.visualPivot.right));
                    for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                    {
                        for (int edge = 0; edge < 3; edge++)
                        {
                            Vector3 a = vertices[triangles[triangle + edge]], b = vertices[triangles[triangle + (edge + 1) % 3]];
                            float da = a.y - shoulder.position.y, db = b.y - shoulder.position.y;
                            if (da * db >= 0 || Mathf.Abs(da - db) < 1e-7f) continue;
                            Vector3 hit = Vector3.LerpUnclamped(a, b, da / (da - db));
                            if (sign * Vector3.Dot(hit - rig.visualPivot.position, rig.visualPivot.right) < height * .04f) continue;
                            crossings.Add(Vector3.Dot(hit, rig.visualPivot.forward));
                        }
                    }
                    crossings.Sort();
                    float largest = 0, rear = 0, ahead = 0;
                    for (int i = 1; i < crossings.Count; i++)
                        if (crossings[i] - crossings[i - 1] > largest)
                        { largest = crossings[i] - crossings[i - 1]; rear = crossings[i - 1]; ahead = crossings[i]; }
                    if (largest < height * .025f) throw new InvalidOperationException("Could not find both sides of the imported strap aperture.");
                    return Mathf.Min(Extreme(pawName, true) - rear, ahead - Extreme(pawName, false));
                }
                float marginL = StrapMargin(rig.leftArm, "Arm.L_Rounded"), marginR = StrapMargin(rig.rightArm, "Arm.R_Rounded");
                return new Clearance { shoulderDegrees = angle, leftRear = left, rightRear = right, packFront = front,
                    minimumGap = gap, minimumGapOverBodyHeight = gap / height, pawsAheadOfPack = gap > height * .005f,
                    leftStrapMargin = marginL, rightStrapMargin = marginR, pawsInsideHarness = Mathf.Min(marginL, marginR) >= -height * .005f };
            }
            finally { UnityEngine.Object.DestroyImmediate(scratch); }
        }
        private static GameObject Node(Scene scene, string name)
        { var node = new GameObject(name); SceneManager.MoveGameObjectToScene(node, scene); return node; }
        private static void Lamp(Scene scene, Vector3 angle, Color color, float intensity)
        {
            var light = Node(scene, "Joint review light").AddComponent<Light>(); light.type = LightType.Directional;
            light.cullingMask = 1 << Layer; light.color = color; light.intensity = intensity; light.transform.rotation = Quaternion.Euler(angle);
        }
    }
}
