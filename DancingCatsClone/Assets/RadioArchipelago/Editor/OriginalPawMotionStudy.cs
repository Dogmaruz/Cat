using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RadioArchipelago;
using RadioArchipelago.Editor;

namespace DancingCats.Clone.Editor
{
    /// <summary>Samples shipped paw curves and geometry in a disposable scene; never edits recovered assets.</summary>
    public static class OriginalPawMotionStudy
    {
        [Serializable] private sealed class Study
        {
            public string clip, prefab, controller;
            public float length, sampleRate;
            public bool loop;
            public Curve[] curves;
            public Sample[] samples;
        }
        [Serializable] private sealed class Curve { public string path, property; public Key[] keys; }
        [Serializable] private sealed class Key { public float time, value, incoming, outgoing; }
        [Serializable] private sealed class Sample
        {
            public float time;
            public Vector3 leftEuler, rightEuler, leftPosition, rightPosition;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Study the original paws in Edit Mode.");
            const string clipPath = "Assets/Recovered/AnimationClip/Jump.anim";
            const string prefabPath = "Assets/Recovered/GameObject/Role1_main.prefab";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (clip == null || prefab == null) throw new FileNotFoundException("The recovered reference cat and Jump clip are required.");
            string output = Path.GetFullPath("Library/RadioRigging/OriginalPawStudy");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewPreviewScene();
            var ownedMaterials = new List<Material>();
            RenderTexture target = null;
            Texture2D pixels = null;
            var previous = RenderTexture.active;
            try
            {
                var actor = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(actor, scene);
                var animator = actor.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("The original Animator is missing.");
                string controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                animator.enabled = false;
                foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 28;
                foreach (var source in actor.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
                var renderers = actor.GetComponentsInChildren<Renderer>(true);
                // Only the disposable reference lighting is adapted to URP; original meshes, UVs and curves remain intact.
                var materials = new Dictionary<Material, Material>();
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    {
                        if (original == null) return original;
                        if (materials.TryGetValue(original, out var existing)) return existing;
                        var copy = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        copy.SetTexture("_BaseMap", original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") :
                            original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : null);
                        copy.SetColor("_BaseColor", Color.white);
                        copy.SetFloat("_Smoothness", .15f);
                        materials.Add(original, copy); ownedMaterials.Add(copy); return copy;
                    }).ToArray();
                }
                var left = animator.transform.Find("Animation/Position/Rotation/Scale/Shou01/s001_AN");
                var right = animator.transform.Find("Animation/Position/Rotation/Scale/Shou02/s002_AN");
                if (left == null || right == null) throw new InvalidOperationException("The shipped shoulder controls are missing.");
                clip.SampleAnimation(animator.gameObject, 0);
                var bounds = CourierVisualBounds.Measure(renderers);
                actor.transform.localScale *= 2.6f / bounds.size.y;
                bounds = CourierVisualBounds.Measure(renderers);
                actor.transform.position -= bounds.center - Vector3.up * 1.3f;
                var cameraObject = new GameObject("Original paw camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << 28;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.96f, .95f, .92f);
                camera.orthographic = true; camera.orthographicSize = 1.85f;
                camera.transform.position = new Vector3(0, 1.45f, 7); camera.transform.LookAt(Vector3.up * 1.3f);
                var lamp = new GameObject("Reference studio light"); SceneManager.MoveGameObjectToScene(lamp, scene);
                var light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
                light.cullingMask = 1 << 28; light.transform.rotation = Quaternion.Euler(25, 150, 0);
                target = new RenderTexture(600, 600, 24); target.Create(); camera.targetTexture = target;
                pixels = new Texture2D(600, 600, TextureFormat.RGB24, false);
                var samples = new List<Sample>();
                using (var bake = new CourierPreviewSkinBake(actor))
                    for (int frame = 0; frame < 60; frame++)
                    {
                        float time = frame / 60f;
                        clip.SampleAnimation(animator.gameObject, time);
                        samples.Add(new Sample { time = time, leftEuler = left.localEulerAngles, rightEuler = right.localEulerAngles,
                            leftPosition = left.localPosition, rightPosition = right.localPosition });
                        bake.Refresh(); camera.Render(); RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, 600, 600), 0, 0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(output, "frame-" + frame.ToString("000") + ".png"), pixels.EncodeToPNG());
                    }
                var study = new Study { clip = clipPath, prefab = prefabPath, controller = controller, length = clip.length,
                    sampleRate = clip.frameRate, loop = clip.isLooping, samples = samples.ToArray(),
                    curves = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.path.Contains("Shou"))
                        .Select(binding => new Curve { path = binding.path, property = binding.propertyName,
                            keys = AnimationUtility.GetEditorCurve(clip, binding).keys.Select(key => new Key
                            { time = key.time, value = key.value, incoming = FiniteTangent(key.inTangent), outgoing = FiniteTangent(key.outTangent) }).ToArray() }).ToArray() };
                File.WriteAllText(Path.Combine(output, "original-paw-study.json"), JsonUtility.ToJson(study, true));
                Debug.Log("ORIGINAL_PAW_STUDY PASS: shipped clip and shoulder geometry sampled at 60 Hz; " + output);
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                foreach (var material in ownedMaterials) UnityEngine.Object.DestroyImmediate(material);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static float FiniteTangent(float value) => float.IsInfinity(value) || float.IsNaN(value) ? 0 : value;
    }
}
