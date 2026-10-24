using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Disposable Editor stage showing the actual imported skins and live runtime motion.</summary>
    public static class CourierAnimationReel
    {
        private const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        private const int Layer = 29;
        private const int Width = 1200, Height = 600, FrameCount = 120;
        private const float Duration = 3, CourierHeight = 2.55f, Spacing = 3;
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/RadioRigging/UnityReel"));

        [MenuItem("Radio Archipelago/Imported couriers/Render flight and riding reel")]
        public static void Render()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Render the courier reel in Edit Mode after imports and compilation finish.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null || catalog.entries == null || catalog.entries.Length != 3 || catalog.entries.Any(entry => entry == null || entry.prefab == null))
                throw new InvalidOperationException("The three supplied courier prefabs are required for the animation reel.");
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            RenderTexture target = null; Texture2D pixels = null;
            var bakes = new CourierPreviewSkinBake[3];
            try
            {
                var stages = new Transform[3]; var motions = new CourierMotion[3]; var renderers = new Renderer[3][];
                for (int i = 0; i < 3; i++)
                {
                    stages[i] = Node(scene, "Disposable flight stage " + catalog.entries[i].id).transform;
                    stages[i].localPosition = new Vector3((i - 1) * Spacing, 0, 0);
                    var actor = UnityEngine.Object.Instantiate(catalog.entries[i].prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene); actor.transform.SetParent(stages[i], false);
                    actor.transform.localRotation = Quaternion.Euler(0, -18, 0);
                    foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                    foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                    foreach (var source in actor.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
                    motions[i] = actor.GetComponentInChildren<CourierMotion>();
                    if (motions[i] == null) throw new InvalidOperationException("No CourierMotion on supplied " + catalog.entries[i].id);
                    motions[i].ManualSimulation = true; motions[i].ResetMotion();
                    renderers[i] = actor.GetComponentsInChildren<Renderer>(true);
                    if (renderers[i].Length == 0) throw new InvalidOperationException("No actual rendered geometry on " + catalog.entries[i].id);
                    var bounds = CourierVisualBounds.Measure(renderers[i]);
                    actor.transform.localScale *= CourierHeight / Mathf.Max(.001f, bounds.size.y);
                    bounds = CourierVisualBounds.Measure(renderers[i]);
                    actor.transform.position += new Vector3(stages[i].position.x - bounds.center.x, -bounds.min.y, -bounds.center.z);
                    bakes[i] = new CourierPreviewSkinBake(actor);
                }
                var camera = Node(scene, "Courier reel camera").AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << Layer;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.957f, .949f, .925f, 1);
                camera.orthographic = true; camera.orthographicSize = 2.95f; camera.aspect = (float)Width / Height;
                camera.nearClipPlane = .05f; camera.farClipPlane = 40; camera.allowHDR = false; camera.allowMSAA = true;
                camera.transform.position = new Vector3(0, 2.55f, 10); camera.transform.LookAt(new Vector3(0, 1.8f, 0));
                AddLight(scene, "Warm reel key", new Vector3(28, 145, 0), new Color(1, .96f, .9f), 1.2f);
                AddLight(scene, "Cool reel fill", new Vector3(18, -25, 0), new Color(.83f, .92f, 1), .6f);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                target.Create(); camera.targetTexture = target; pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);

                FitMotionEnvelope(camera, stages, motions, renderers);
                CaptureSequence("flight", 2.4f, true, stages, motions, renderers, bakes, camera, target, pixels);
                CaptureSequence("ride", 2.6f, false, stages, motions, renderers, bakes, camera, target, pixels);
                Directory.CreateDirectory(Output);
                File.WriteAllText(Path.Combine(Output, "manifest.txt"),
                    "Actual Unity skinning, supplied textures and CourierMotion.ManualSimulation.\n" +
                    "Characters screen left to right: " + string.Join(", ", catalog.entries.Reverse().Select(entry => entry.displayName + " (" + entry.id + ")")) + "\n" +
                    "flight: 2.4 second long flight + .6 second landing/settling.\n" +
                    "ride: 2.6 second turn + .4 second settling.\n" +
                    "Each sequence: frame-000.png through frame-119.png, " + Width + "x" + Height + ", 3 seconds at 40fps.\n" +
                    "Only the disposable preview stages follow the illustrative flight arc and small lateral turn. Movement is sampled at every simulation substep. Game scene, catalog and player data remain untouched.\n");
                Debug.Log("RADIO_COURIER_REEL PASS: 240 actual Unity frames at " + Output);
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                foreach (var bake in bakes) bake?.Dispose();
                // Closing a preview scene disposes its temporary actors, bones, camera and lights.
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void FitMotionEnvelope(Camera camera, Transform[] stages, CourierMotion[] motions, Renderer[][] renderers)
        {
            // A rotating curved antenna can extend past either side of its rest silhouette.
            // Measure both complete sequences before capture, keeping one fixed camera for the GIF.
            float halfHeight = 0;
            foreach (bool flight in new[] { true, false })
            {
                float transition = flight ? 2.4f : 2.6f;
                foreach (var motion in motions)
                {
                    motion.ResetMotion();
                    if (flight) motion.PlayJump(transition); else motion.PlayHold(false, transition, true);
                }
                float clock = 0; bool settled = false;
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    float targetTime = Duration * frame / (FrameCount - 1);
                    while (clock < targetTime - .000001f)
                    {
                        if (!settled && clock >= transition - .000001f)
                        {
                            settled = true;
                            foreach (var motion in motions) { motion.PlayIdle(); if (flight) motion.PlayLanding(1); }
                        }
                        float next = Mathf.Min(clock + 1f / 120, targetTime);
                        if (!settled) next = Mathf.Min(next, transition);
                        PoseStages(stages, motions, next, transition, flight);
                        foreach (var motion in motions)
                        {
                            if (flight && !settled) motion.SetFlightProgress(Mathf.Clamp01(next / transition));
                            motion.SimulateSprings(next - clock);
                        }
                        clock = next;
                    }
                    PoseStages(stages, motions, targetTime, transition, flight);
                    for (int i = 0; i < stages.Length; i++)
                    {
                        var bounds = CourierVisualBounds.Measure(renderers[i]);
                        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                        {
                            var point = camera.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(point.y), Mathf.Abs(point.x) / camera.aspect);
                        }
                    }
                }
            }
            camera.orthographicSize = Mathf.Max(1, halfHeight / .94f);
            for (int i = 0; i < stages.Length; i++)
            { stages[i].localPosition = new Vector3((i - 1) * Spacing, 0, 0); motions[i].ResetMotion(); }
        }

        private static void CaptureSequence(string name, float transition, bool flight, Transform[] stages, CourierMotion[] motions,
            Renderer[][] renderers, CourierPreviewSkinBake[] bakes, Camera camera, RenderTexture target, Texture2D pixels)
        {
            string folder = Path.Combine(Output, name); Directory.CreateDirectory(folder);
            for (int i = 0; i < motions.Length; i++)
            {
                stages[i].localPosition = new Vector3((i - 1) * Spacing, 0, 0); motions[i].ResetMotion();
                if (flight) motions[i].PlayJump(transition); else motions[i].PlayHold(false, transition, true);
            }
            float clock = 0; bool settled = false;
            for (int frame = 0; frame < FrameCount; frame++)
            {
                float targetTime = Duration * frame / (FrameCount - 1);
                while (clock < targetTime - .000001f)
                {
                    if (!settled && clock >= transition - .000001f)
                    {
                        settled = true;
                        foreach (var motion in motions) { motion.PlayIdle(); if (flight) motion.PlayLanding(1); }
                    }
                    float next = Mathf.Min(clock + 1f / 120, targetTime);
                    if (!settled) next = Mathf.Min(next, transition);
                    PoseStages(stages, motions, next, transition, flight);
                    foreach (var motion in motions)
                    {
                        if (flight && !settled) motion.SetFlightProgress(Mathf.Clamp01(next / transition));
                        motion.SimulateSprings(next - clock);
                    }
                    clock = next;
                }
                PoseStages(stages, motions, targetTime, transition, flight);
                for (int i = 0; i < stages.Length; i++)
                {
                    RequireVisible(camera, CourierVisualBounds.Measure(renderers[i]), name, frame, i);
                    bakes[i].Refresh();
                }
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, "frame-" + frame.ToString("000") + ".png"), pixels.EncodeToPNG());
            }
        }

        private static void PoseStages(Transform[] stages, CourierMotion[] motions, float time, float transition, bool flight)
        {
            float p = time / transition;
            float lift = flight && p < 1 ? Mathf.Sin(Mathf.PI * p) * .62f : 0;
            float lateral = flight && p < 1 ? Mathf.Sin(Mathf.PI * 2 * p) * .22f : 0;
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i].localPosition = new Vector3((i - 1) * Spacing + lateral, lift, 0);
                motions[i].SetFacing(flight ? lateral * 100 : 0);
            }
        }

        private static void RequireVisible(Camera camera, Bounds bounds, string sequence, int frame, int courier)
        {
            // Actual baked-skin bounds must fit; padded import culling bounds are intentionally not used.
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                var point = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                if (point.x < .025f || point.x > .975f || point.y < .025f || point.y > .975f || point.z <= camera.nearClipPlane)
                    throw new InvalidOperationException("Courier reel would clip visible geometry: " + sequence + " frame " + frame + " courier " + courier + ". Increase the disposable review camera framing.");
            }
        }

        private static GameObject Node(Scene scene, string name)
        {
            var node = new GameObject(name); SceneManager.MoveGameObjectToScene(node, scene); return node;
        }
        private static void AddLight(Scene scene, string name, Vector3 rotation, Color color, float intensity)
        {
            var node = Node(scene, name); var light = node.AddComponent<Light>();
            light.type = LightType.Directional; light.color = color; light.intensity = intensity;
            light.cullingMask = 1 << Layer; light.shadows = LightShadows.None; node.transform.rotation = Quaternion.Euler(rotation);
        }
    }
}
