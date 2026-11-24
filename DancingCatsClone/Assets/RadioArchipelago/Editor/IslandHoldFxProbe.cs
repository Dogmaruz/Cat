using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RadioArchipelago.Editor
{
    /// <summary>One-shot view-only isolation of native Hold meshes and flashes.</summary>
    [InitializeOnLoad]
    public static class IslandHoldFxProbe
    {
        private const string Armed = "RadioArchipelago.HoldFxProbe.Armed";
        private static readonly string Folder = Path.Combine("Library", "IslandPilotReview", "hold-fx-probe");
        [Serializable] private sealed class Snapshot
        {
            public float chartTime, holdProgress;
            public string source, notes;
            public Entry[] renderers;
        }
        [Serializable] private sealed class Entry
        {
            public string name, type, material, shader, mesh;
            public bool enabled;
            public int particles;
            public Vector3 boundsCenter, boundsSize, localScale, particleSize;
        }

        static IslandHoldFxProbe() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        public static void Begin()
        {
            if (!EditorApplication.isPlaying) RunChecks();
            SessionState.SetBool(Armed, true); Debug.Log("HOLD_FX_PROBE_ARMED: first active Hold will be isolated once.");
        }

        public static void RunChecks() { CheckCollectibleMarker(); CheckHoldFlashSelection(); }

        private static void CheckHoldFlashSelection()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/Hold.prefab");
            var policy = typeof(IslandParticleProjection).GetMethod("IsHoldCrossFlash", BindingFlags.NonPublic | BindingFlags.Static);
            if (prefab == null || policy == null) throw new InvalidOperationException("The native Hold flash fixture or selection policy is missing.");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var owner = instance.AddComponent<OriginalTile>(); owner.element = new MapElement { id = 2 };
                int omitted = 0, retained = 0;
                foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    bool skip = (bool)policy.Invoke(null, new object[] { system, renderer, owner });
                    bool expected = system.name == "Light1" || system.name == "Light2";
                    if (skip != expected) throw new InvalidOperationException("Hold flash filtering changed a different native effect: " + system.name);
                    if (skip) omitted++; else retained++;
                    owner.element.id = 0;
                    if ((bool)policy.Invoke(null, new object[] { system, renderer, owner }))
                        throw new InvalidOperationException("Normal jump effects were filtered by the Hold-only policy.");
                    owner.element.id = 2;
                }
                if (omitted != 2 || retained == 0) throw new InvalidOperationException("The Hold fixture did not isolate exactly its two decorative flashes.");
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "flash-selection-checks.json"), "{\"passed\":true,\"holdFlashSystemsOmitted\":" + omitted + ",\"otherSystemsRetained\":" + retained + ",\"normalJumpEffectsUnchanged\":true}");
                Debug.Log("HOLD_FLASH_SELECTION_PASS omitted=" + omitted + "; retained=" + retained);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void CheckCollectibleMarker()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/FollowMid.prefab");
            if (prefab == null) throw new InvalidOperationException("The native FollowMid fixture is missing.");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var marker = instance.transform.Find("Sphere");
                if (marker == null) throw new InvalidOperationException("The native FollowMid Sphere fixture is missing.");
                OriginalTileVisuals.Configure(instance, new MapElement { id = 5 }, null);
                int restarted = 0, leftStopped = 0;
                foreach (var particle in marker.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    if (main.loop && main.playOnAwake)
                    {
                        if (!particle.isPlaying) throw new InvalidOperationException("The native idle marker was not restarted: " + particle.name);
                        restarted++;
                    }
                    else
                    {
                        if (particle.isPlaying) throw new InvalidOperationException("A non-idle marker burst was started before collection: " + particle.name);
                        leftStopped++;
                    }
                }
                if (restarted == 0) throw new InvalidOperationException("The native marker fixture did not exercise a play-on-awake loop.");
                OriginalTileVisuals.OnHit(instance, false, false, 1);
                if (marker.gameObject.activeSelf) throw new InvalidOperationException("The collectible marker remained active after its real hit callback.");
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "marker-restart-checks.json"), "{\"passed\":true,\"idleLoopsRestarted\":" + restarted + ",\"nonIdleSystemsLeftStopped\":" + leftStopped + ",\"hiddenOnHit\":true}");
                Debug.Log("FOLLOW_MARKER_RESTART_PASS idleLoops=" + restarted + "; nonIdleStopped=" + leftStopped);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Armed, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            var game = UnityEngine.Object.FindAnyObjectByType<OriginalPlayController>();
            if (game == null || game.Phase != ClonePhase.Playing || game.Support == null ||
                (game.Support.element.id != 2 && game.Support.element.id != 3)) return;
            var hold = game.Support;
            float end = hold.segment != null ? hold.segment.endZ : hold.initialZ + hold.element.mfendPos;
            float progress = Mathf.InverseLerp(hold.initialZ, end, game.Actor.position.z);
            if (progress < .18f || progress > .65f) return;
            SessionState.SetBool(Armed, false);
            try { Capture(game, progress); }
            catch (Exception error) { Debug.LogError("HOLD_FX_PROBE_FAILED: " + error); }
        }

        private static void Capture(OriginalPlayController game, float progress)
        {
            var projection = game.IslandStage.ParticleProjection;
            var renderers = projection.GetComponentsInChildren<Renderer>(true);
            var enabled = new bool[renderers.Length]; var entries = new List<Entry>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i]; enabled[i] = renderer.enabled;
                if (!renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;
                var entry = new Entry { name = renderer.name, type = renderer.GetType().Name, enabled = renderer.enabled,
                    material = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "",
                    shader = renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null ? renderer.sharedMaterial.shader.name : "",
                    boundsCenter = renderer.bounds.center, boundsSize = renderer.bounds.size, localScale = renderer.transform.lossyScale };
                if (renderer is ParticleSystemRenderer particles)
                {
                    entry.mesh = particles.mesh != null ? particles.mesh.name : "";
                    var system = particles.GetComponent<ParticleSystem>(); entry.particles = system.particleCount;
                    var buffer = new ParticleSystem.Particle[Mathf.Max(1, system.particleCount)];
                    if (system.GetParticles(buffer) > 0) entry.particleSize = buffer[0].GetCurrentSize3D(system);
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>(); entry.mesh = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : "";
                }
                entries.Add(entry);
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "snapshot.json"), JsonUtility.ToJson(new Snapshot { chartTime = (float)game.GameTime,
                holdProgress = progress, source = game.Support.name, renderers = entries.ToArray(),
                notes = "View-only render isolation. No colliders, source animation, particles, music or chart time are changed. Readback can stall the Editor; do not use this diagnostic pass as a performance run." }, true));
            try
            {
                Render(game.IslandStage.Camera, "all");
                Isolate(renderers, enabled, r => r is ParticleSystemRenderer && (r.name.EndsWith(" Light1", StringComparison.Ordinal) || r.name.EndsWith(" Light2", StringComparison.Ordinal)));
                Render(game.IslandStage.Camera, "without-light-flashes");
                Isolate(renderers, enabled, r => r is ParticleSystemRenderer);
                Render(game.IslandStage.Camera, "without-particles");
                Isolate(renderers, enabled, r => r is MeshRenderer && r.name.EndsWith(" Hold_Light", StringComparison.Ordinal));
                Render(game.IslandStage.Camera, "without-hold-band");
                Isolate(renderers, enabled, r => r is MeshRenderer && r.name.EndsWith(" Hold_Line", StringComparison.Ordinal));
                Render(game.IslandStage.Camera, "without-center-line");
            }
            finally { for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i]; }
            Debug.Log("HOLD_FX_PROBE_CAPTURED " + Path.GetFullPath(Folder));
        }

        private static void Isolate(Renderer[] renderers, bool[] enabled, Func<Renderer, bool> excluded)
        { for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i] && !excluded(renderers[i]); }

        private static void Render(Camera camera, string label)
        {
            var target = RenderTexture.GetTemporary(540, 960, 24, RenderTextureFormat.ARGB32);
            var oldActive = RenderTexture.active;
            var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
            try
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Folder, label + ".png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
