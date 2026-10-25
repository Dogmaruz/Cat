using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Close views of real imported shoulder surfaces; beauty and untextured geometry are reviewed separately.</summary>
    public static class CourierSurfaceReview
    {
        private const int Layer = 27, Size = 1000;
        [Serializable] private sealed class Input { public string prefab, dependencyHash; }
        [Serializable] private sealed class Manifest
        { public string stage, capturedUtc, unityVersion; public Input[] inputs; public string[] frames; }

        public static void RenderBaseline() => Render("baseline");
        public static void RenderCandidate() => Render("candidate");

        private static void Render(string stage)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Surface review requires Edit Mode.");
            string folder = Path.GetFullPath("Library/RadioRigging/SmoothCourierReview/Unity/" + stage);
            if (stage == "baseline" && File.Exists(Path.Combine(folder, "manifest.json")))
                throw new InvalidOperationException("The surface baseline is already preserved; render a candidate instead.");
            Directory.CreateDirectory(folder);
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            if (catalog == null || catalog.entries == null || catalog.entries.Length != 3) throw new InvalidOperationException("Three imported couriers are required.");
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            var frames = new List<string>();
            var inputs = new List<Input>();
            var target = new RenderTexture(Size, Size, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            var clay = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            clay.SetColor("_BaseColor", new Color(.55f, .59f, .63f)); clay.SetFloat("_Smoothness", .2f);
            try
            {
                var camera = Node(scene, "Surface camera").AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << Layer;
                camera.orthographic = true; camera.aspect = 1; camera.nearClipPlane = .05f; camera.farClipPlane = 40;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.96f, .95f, .92f);
                target.Create(); camera.targetTexture = target;
                Lamp(scene, new Vector3(28, 145, 0), new Color(1, .95f, .87f), 1.2f);
                Lamp(scene, new Vector3(18, -25, 0), new Color(.82f, .91f, 1), .6f);
                foreach (var entry in catalog.entries)
                {
                    if (entry == null || entry.prefab == null) throw new InvalidOperationException("Every courier needs an imported prefab.");
                    string prefabPath = AssetDatabase.GetAssetPath(entry.prefab);
                    inputs.Add(new Input { prefab = prefabPath, dependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString() });
                    var actor = UnityEngine.Object.Instantiate(entry.prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                        foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                        var motion = actor.GetComponentInChildren<CourierMotion>();
                        motion.ManualSimulation = true; motion.ResetMotion();
                        var rig = (CourierRigBinding)typeof(CourierMotion).GetField("rig", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motion);
                        var skins = actor.GetComponentsInChildren<Renderer>(true);
                        float height = CourierVisualBounds.MeasureBody(skins).size.y;
                        var center = (rig.leftArm.position + rig.rightArm.position) * .5f - Vector3.up * height * .1f;
                        camera.orthographicSize = height * .48f;
                        Quaternion leftRest = rig.leftArm.localRotation, rightRest = rig.rightArm.localRotation;
                        Vector3 leftAxis = rig.leftArm.InverseTransformDirection(rig.visualPivot.forward).normalized;
                        Vector3 rightAxis = rig.rightArm.InverseTransformDirection(rig.visualPivot.forward).normalized;
                        using (var bake = new CourierPreviewSkinBake(actor))
                        {
                            var originals = new Material[bake.Renderers.Length][];
                            for (int i = 0; i < originals.Length; i++) originals[i] = bake.Renderers[i].sharedMaterials;
                            foreach (int angle in new[] { -10, 30, 60 })
                            {
                                rig.leftArm.localRotation = leftRest * Quaternion.AngleAxis(-angle, leftAxis);
                                rig.rightArm.localRotation = rightRest * Quaternion.AngleAxis(angle, rightAxis);
                                bake.Refresh();
                                foreach (bool untextured in new[] { false, true })
                                {
                                    for (int i = 0; i < originals.Length; i++)
                                    {
                                        if (!untextured) bake.Renderers[i].sharedMaterials = originals[i];
                                        else
                                        {
                                            var replacement = new Material[originals[i].Length];
                                            for (int material = 0; material < replacement.Length; material++) replacement[material] = clay;
                                            bake.Renderers[i].sharedMaterials = replacement;
                                        }
                                    }
                                    string[] names = { "front", "right", "back", "left", "under-right", "under-left" };
                                    float[] yaw = { 0, 90, 180, 270, 65, 295 };
                                    for (int view = 0; view < names.Length; view++)
                                    {
                                        Vector3 direction = Quaternion.Euler(view >= 4 ? 28 : 0, yaw[view], 0) * Vector3.forward;
                                        camera.transform.position = center + direction * 5;
                                        camera.transform.LookAt(center);
                                        camera.Render(); RenderTexture.active = target;
                                        pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); pixels.Apply();
                                        string filename = entry.id + "-" + angle + "-" + names[view] + (untextured ? "-clay" : "-beauty") + ".png";
                                        File.WriteAllBytes(Path.Combine(folder, filename), pixels.EncodeToPNG()); frames.Add(filename);
                                    }
                                }
                            }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
                File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(new Manifest {
                    stage = stage, capturedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                    inputs = inputs.ToArray(), frames = frames.ToArray() }, true));
                Debug.Log("COURIER_SURFACE_REVIEW CAPTURED " + stage + ": " + frames.Count + " actual close views at " + folder);
            }
            finally
            {
                RenderTexture.active = previous; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(clay);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static GameObject Node(Scene scene, string name)
        { var node = new GameObject(name); SceneManager.MoveGameObjectToScene(node, scene); return node; }
        private static void Lamp(Scene scene, Vector3 angle, Color color, float intensity)
        {
            var light = Node(scene, "Surface light").AddComponent<Light>();
            light.type = LightType.Directional; light.cullingMask = 1 << Layer; light.color = color;
            light.intensity = intensity; light.transform.rotation = Quaternion.Euler(angle);
        }
    }
}
