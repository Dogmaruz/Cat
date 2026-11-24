#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    public static class ModularBuckleProbe
    {
        [Serializable] private sealed class Row
        {
            public string name, path, material;
            public bool enabled, active;
            public Vector3 localScale, lossyScale;
            public Bounds rendererBounds, actualWorldBounds, actualVisualBounds;
            public int vertices;
        }
        [Serializable] private sealed class Report { public float bodyHeight; public Vector3 equipmentScale; public Row[] renderers; }

        public static void Run()
        {
            string folder = Path.GetFullPath("../Docs/Design/RadioArchipelago/ModularCast20261104/BuckleProbe");
            Directory.CreateDirectory(folder);
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            var scene = EditorSceneManager.NewPreviewScene();
            var actor = UnityEngine.Object.Instantiate(catalog.entries.First(x => x.assetId == "Tiko").prefab);
            SceneManager.MoveGameObjectToScene(actor, scene);
            var cameraNode = new GameObject("Owned buckle probe camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraNode, scene);
            var camera = cameraNode.GetComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 2.2f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.89f, .88f, .82f); camera.nearClipPlane = .01f; camera.farClipPlane = 40;
            var key = new GameObject("Owned buckle probe light", typeof(Light)); SceneManager.MoveGameObjectToScene(key, scene);
            key.transform.rotation = Quaternion.Euler(35, 150, 0); key.GetComponent<Light>().type = LightType.Directional; key.GetComponent<Light>().intensity = 1.4f;
            var rim = new GameObject("Owned buckle probe rim", typeof(Light)); SceneManager.MoveGameObjectToScene(rim, scene);
            rim.transform.rotation = Quaternion.Euler(25, -30, 0); rim.GetComponent<Light>().type = LightType.Directional; rim.GetComponent<Light>().intensity = .9f;
            var rt = RenderTexture.GetTemporary(640, 800, 24, RenderTextureFormat.ARGB32); camera.targetTexture = rt;
            try
            {
                var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                wardrobe.Apply(catalog.FindEquipment("backpack_Tiko", CourierEquipmentKind.Backpack), null,
                    catalog.FindEquipment("scarf_TikoCoral", CourierEquipmentKind.Scarf));
                wardrobe.motion.ManualSimulation = true; wardrobe.motion.ResetMotion();
                var pack = wardrobe.Equipped(CourierEquipmentKind.Backpack);
                var rows = new List<Row>();
                foreach (var renderer in pack.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var row = new Row { name = renderer.name, path = PathTo(renderer.transform, pack.transform),
                        material = string.Join(";", renderer.sharedMaterials.Select(x => x != null ? x.name : "null")),
                        enabled = renderer.enabled, active = renderer.gameObject.activeInHierarchy,
                        localScale = renderer.transform.localScale, lossyScale = renderer.transform.lossyScale, rendererBounds = renderer.bounds };
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
                    {
                        var points = filter.sharedMesh.vertices; row.vertices = points.Length;
                        if (points.Length > 0)
                        {
                            row.actualWorldBounds = new Bounds(renderer.transform.TransformPoint(points[0]), Vector3.zero);
                            row.actualVisualBounds = new Bounds(wardrobe.bodyRig.visualPivot.InverseTransformPoint(renderer.transform.TransformPoint(points[0])), Vector3.zero);
                            foreach (var point in points)
                            { Vector3 world = renderer.transform.TransformPoint(point); row.actualWorldBounds.Encapsulate(world); row.actualVisualBounds.Encapsulate(wardrobe.bodyRig.visualPivot.InverseTransformPoint(world)); }
                        }
                    }
                    rows.Add(row);
                }
                File.WriteAllText(folder + "/report.json", JsonUtility.ToJson(new Report { bodyHeight = wardrobe.profile.bodyHeight, equipmentScale = pack.transform.lossyScale, renderers = rows.ToArray() }, true));
                Capture("01-all-back.png", new Vector3(0, 1.8f, -6));
                Capture("02-all-side.png", new Vector3(6, 1.8f, 0));
                var buckles = pack.GetComponentsInChildren<MeshRenderer>(false).Where(x => x.name.StartsWith("FittedBuckle", StringComparison.Ordinal)).ToArray();
                foreach (var renderer in buckles) renderer.enabled = false;
                Capture("03-no-fitted-buckles-back.png", new Vector3(0, 1.8f, -6));
                Capture("04-no-fitted-buckles-side.png", new Vector3(6, 1.8f, 0));
                foreach (var renderer in buckles) renderer.enabled = true;
                var all = actor.GetComponentsInChildren<Renderer>(false).Where(x => x.enabled).ToArray();
                foreach (var renderer in all) renderer.enabled = buckles.Contains(renderer);
                Capture("05-only-fitted-buckles.png", new Vector3(0, 1.4f, 6));
                foreach (var renderer in all) renderer.enabled = true;
                var brass = pack.GetComponentsInChildren<MeshRenderer>(false).Where(x => x.sharedMaterials.Any(m => m != null && m.name.Contains("WarmBrass"))).ToArray();
                foreach (var renderer in brass) renderer.enabled = false;
                Capture("06-no-warmbrass-back.png", new Vector3(0, 1.8f, -6));
                Debug.Log("MODULAR_BUCKLE_PROBE COMPLETE: actual renderer/vertex bounds and buckle/brass isolation views. " + folder);
            }
            finally { camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(actor); EditorSceneManager.ClosePreviewScene(scene); }

            void Capture(string name, Vector3 position)
            {
                camera.transform.position = position; camera.transform.LookAt(new Vector3(0, 1.4f, 0));
                using (var bake = new CourierPreviewSkinBake(actor)) { camera.Render(); }
                var previous = RenderTexture.active; var pixels = new Texture2D(640, 800, TextureFormat.RGB24, false);
                try { RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 640, 800), 0, 0); pixels.Apply(); File.WriteAllBytes(folder + "/" + name, pixels.EncodeToPNG()); }
                finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
            }
        }
        private static string PathTo(Transform node, Transform root)
        { var names = new List<string>(); for (var at = node; at != null; at = at.parent) { names.Add(at.name); if (at == root) break; } names.Reverse(); return string.Join("/", names); }
    }
}
#endif
