using System;
using System.IO;
using System.Security.Cryptography;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RadioArchipelago.Editor
{
    /// <summary>Explicit finish-presentation installation and independent generated-mesh checks.</summary>
    public static partial class IslandFinishThemeJobs
    {
        private const string ReviewFolder = "Library/RadioArchipelago/FinishThemes";
        [Serializable] private sealed class Baseline
        {
            public string meshSha256;
            public int triangles, renderers, cornflowers, daisies, dandelions;
        }

        public static void CaptureLegacyBaseline()
        {
            RequireEditMode();
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            var scene = EditorSceneManager.NewPreviewScene();
            var node = new GameObject("Original Emerald finish mesh baseline");
            SceneManager.MoveGameObjectToScene(node,scene);
            try
            {
                var glade = node.AddComponent<IslandFinishGlade>();
                Configure(glade,definition);
                Directory.CreateDirectory(ReviewFolder);
                File.WriteAllText(ReviewFolder + "/emerald-before.json",JsonUtility.ToJson(new Baseline {
                    meshSha256 = MeshFingerprint(glade),triangles = glade.GeneratedTriangleCount,renderers = glade.GeneratedRendererCount,
                    cornflowers = glade.CornflowerCount,daisies = glade.DaisyCount,dandelions = glade.DandelionCount},true));
            }
            finally { Object.DestroyImmediate(node); EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("FINISH_THEME_EMERALD_BASELINE captured from the original runtime generator.");
        }

        private static void Configure(IslandFinishGlade glade,IslandStageDefinition definition)
        {
            glade.Configure(new Vector3(21,0,-8),Quaternion.Euler(0,37,0)*Vector3.forward,5.2f,34,14,
                Borrow(definition,"Terrain"),Borrow(definition,"Cliffs"),Borrow(definition,"Grass"),Borrow(definition,"Foliage"),71237);
        }
        private static Material Borrow(IslandStageDefinition definition,string suffix)
        {
            foreach (var renderer in definition.Prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.name.IndexOf("_"+suffix,StringComparison.Ordinal)>=0) return material;
            throw new InvalidOperationException("The finish theme needs its own island's " + suffix + " material.");
        }
        private static string MeshFingerprint(IslandFinishGlade glade)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                foreach (var mesh in glade.OwnedMeshes)
                {
                    writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount);
                    foreach (Vector3 p in mesh.vertices) { writer.Write(p.x);writer.Write(p.y);writer.Write(p.z); }
                    foreach (Color c in mesh.colors) { writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a); }
                    foreach (Vector2 uv in mesh.uv) { writer.Write(uv.x);writer.Write(uv.y); }
                    foreach (Vector3 n in mesh.normals) { writer.Write(n.x);writer.Write(n.y);writer.Write(n.z); }
                    for (int sub=0;sub<mesh.subMeshCount;sub++) foreach (int index in mesh.GetIndices(sub)) writer.Write(index);
                }
                writer.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Finish theme authoring needs stable Edit Mode.");
        }
    }
}
