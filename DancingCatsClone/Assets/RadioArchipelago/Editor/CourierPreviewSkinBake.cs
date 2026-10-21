using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>
    /// Synchronous Editor captures need a fresh mesh buffer even when several poses render in one tick.
    /// Owns temporary CPU-baked proxies in a disposable preview scene; never replaces production skins.
    /// </summary>
    public sealed class CourierPreviewSkinBake : IDisposable
    {
        private sealed class Entry
        {
            public SkinnedMeshRenderer source;
            public bool wasEnabled, wasUpdatingOffscreen;
            public GameObject node;
            public Mesh mesh;
            public MeshRenderer renderer;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private bool disposed;
        public MeshRenderer[] Renderers { get; private set; } = Array.Empty<MeshRenderer>();

        public CourierPreviewSkinBake(GameObject previewRoot)
        {
            if (previewRoot == null) throw new ArgumentNullException(nameof(previewRoot));
            if (!EditorSceneManager.IsPreviewScene(previewRoot.scene))
                throw new InvalidOperationException("Baked courier capture proxies may only be created inside a disposable Editor preview scene.");
            try
            {
                var block = new MaterialPropertyBlock();
                foreach (var source in previewRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (source.sharedMesh == null) continue;
                    var entry = new Entry { source = source, wasEnabled = source.enabled, wasUpdatingOffscreen = source.updateWhenOffscreen };
                    entries.Add(entry);
                    entry.node = new GameObject("__BakedPreview_" + source.name) { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
                    SceneManager.MoveGameObjectToScene(entry.node, source.gameObject.scene);
                    entry.node.transform.SetParent(source.transform, false);
                    entry.mesh = new Mesh { name = "Owned preview buffer: " + source.name, hideFlags = HideFlags.DontSave, indexFormat = source.sharedMesh.indexFormat };
                    entry.mesh.MarkDynamic();
                    entry.node.AddComponent<MeshFilter>().sharedMesh = entry.mesh;
                    entry.renderer = entry.node.AddComponent<MeshRenderer>();
                    entry.renderer.sharedMaterials = source.sharedMaterials;
                    entry.renderer.shadowCastingMode = source.shadowCastingMode;
                    entry.renderer.receiveShadows = source.receiveShadows;
                    entry.renderer.lightProbeUsage = source.lightProbeUsage;
                    entry.renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                    entry.renderer.renderingLayerMask = source.renderingLayerMask;
                    entry.renderer.sortingLayerID = source.sortingLayerID;
                    entry.renderer.sortingOrder = source.sortingOrder;
                    source.GetPropertyBlock(block); entry.renderer.SetPropertyBlock(block); block.Clear();
                    for (int index = 0; index < source.sharedMaterials.Length; index++)
                    {
                        source.GetPropertyBlock(block, index); entry.renderer.SetPropertyBlock(block, index); block.Clear();
                    }
                    source.updateWhenOffscreen = true;
                    source.enabled = false;
                }
                Renderers = new MeshRenderer[entries.Count];
                for (int i = 0; i < entries.Count; i++) Renderers[i] = entries[i].renderer;
                Refresh();
            }
            catch { Dispose(); throw; }
        }

        /// <summary>Call after changing bones/blend shapes and immediately before Camera.Render.</summary>
        public void Refresh()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CourierPreviewSkinBake));
            foreach (var entry in entries)
            {
                if (entry.source == null) continue;
                // The proxy shares the source Transform, so bake renderer-local vertices.
                entry.source.BakeMesh(entry.mesh, true);
                entry.mesh.RecalculateBounds();
                entry.mesh.UploadMeshData(false);
                entry.node.layer = entry.source.gameObject.layer;
                entry.renderer.enabled = entry.wasEnabled;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var entry in entries)
            {
                if (entry.source != null)
                {
                    entry.source.enabled = entry.wasEnabled;
                    entry.source.updateWhenOffscreen = entry.wasUpdatingOffscreen;
                }
                if (entry.node != null) UnityEngine.Object.DestroyImmediate(entry.node);
                if (entry.mesh != null) UnityEngine.Object.DestroyImmediate(entry.mesh);
            }
            entries.Clear(); Renderers = Array.Empty<MeshRenderer>();
        }
    }
}
