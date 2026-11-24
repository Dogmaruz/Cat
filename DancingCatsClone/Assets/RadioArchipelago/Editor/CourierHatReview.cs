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
    /// <summary>Fixed-camera native evidence for every supplied hat; preview scenes only.</summary>
    public static class CourierHatReview
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        public static string EvidenceFolder => Path.GetFullPath("../Docs/Design/RadioArchipelago/HatsAndMotion20261105/Hats");
        [Serializable] public sealed class Pair
        {
            public string hero, hat;
            public string[] images;
            public Vector3 position, euler;
            public float scale;
        }
        [Serializable] sealed class Report
        {
            public string capturedUtc, unityVersion, folder;
            public int pairs, images;
            public bool activeScenePreserved;
            public Pair[] observations;
            public string capture = "Unretouched native Unity Camera.Render with fresh CPU-baked skins. Same front/side/close cameras and lights before and after. Tall backpack mechanisms may be cropped to keep head and hat readable.";
        }

        public static void Baseline() => Run("Before", false);
        public static void Capture() => Run("After", false);
        public static void QuickBaseline() => Run("BeforeQuick", true);
        public static void Quick() => Run("AfterQuick", true);

        static void Run(string phase, bool quick)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture hat fits in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("The courier catalog is missing.");
            var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
            string folder = Path.Combine(EvidenceFolder, phase); Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            var node = new GameObject("Disposable hat-fit camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(node, scene);
            var camera = node.GetComponent<Camera>(); camera.scene = scene;
            camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.94f, .925f, .875f);
            camera.allowHDR = false; camera.allowMSAA = false;
            Light(scene, "Warm key", new Vector3(35, 145, 0), 1.5f, new Color(1, .95f, .87f));
            Light(scene, "Cool rear fill", new Vector3(25, -25, 0), 1.05f, new Color(.82f, .91f, 1));
            Light(scene, "Soft front fill", new Vector3(-12, 0, 0), .3f, new Color(1, .97f, .92f));
            var rt = RenderTexture.GetTemporary(480, 640, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active; camera.targetTexture = rt;
            var pixels = new Texture2D(480, 640, TextureFormat.RGBA32, false);
            var observations = new List<Pair>(); int count = 0;
            try
            {
                foreach (var hero in catalog.entries)
                {
                    if (quick && hero.assetId != "Lumen" && hero.assetId != "Tiko" && hero.assetId != "Nori" && hero.assetId != "Rumi") continue;
                    var actor = UnityEngine.Object.Instantiate(hero.prefab); SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                        var motion = actor.GetComponent<CourierMotion>(); motion.ManualSimulation = true; motion.ResetMotion();
                        var pack = catalog.FindEquipment(hero.defaultBackpackId, CourierEquipmentKind.Backpack);
                        wardrobe.Apply(pack, null, null); motion.ResetMotion();
                        View(hero.assetId + "__bare__front.png", Vector3.forward, false);
                        View(hero.assetId + "__bare__side.png", Vector3.right, false);
                        foreach (var hat in catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Hat))
                        {
                            if (quick && hat.id != "hat_AmberBeret" && hat.id != "hat_ExplorerHat") continue;
                            wardrobe.Apply(pack, hat, null); motion.ResetMotion();
                            var names = new List<string>();
                            foreach (var view in new[] { "front", "side" })
                            {
                                string file = hero.assetId + "__" + hat.id + "__" + view + ".png";
                                View(file, view == "front" ? Vector3.forward : Vector3.right, false); names.Add(file);
                            }
                            if (hero.assetId == "Lumen" && (hat.id == "hat_AmberBeret" || hat.id == "hat_ExplorerHat"))
                            {
                                foreach (var view in new[] { "front", "side" })
                                {
                                    string file = hero.assetId + "__" + hat.id + "__close-" + view + ".png";
                                    View(file, view == "front" ? Vector3.forward : Vector3.right, true); names.Add(file);
                                }
                            }
                            var fit = wardrobe.profile.FindFit(hat.id);
                            observations.Add(new Pair { hero = hero.assetId, hat = hat.id, images = names.ToArray(),
                                position = fit.position, euler = fit.euler, scale = fit.scale });
                        }
                        void View(string name, Vector3 direction, bool close)
                        {
                            var target = new Vector3(0, close ? 1.77f : 1.35f, 0);
                            camera.orthographicSize = close ? .88f : 1.55f;
                            camera.transform.position = target + direction * 6; camera.transform.LookAt(target);
                            using (var baked = new CourierPreviewSkinBake(actor)) { baked.Refresh(); camera.Render(); }
                            RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 480, 640), 0, 0); pixels.Apply();
                            File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG()); count++;
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            var report = new Report { capturedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                folder = folder, pairs = observations.Count, images = count, observations = observations.ToArray(),
                activeScenePreserved = active == SceneManager.GetActiveScene() && active.isDirty == dirty };
            File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report, true));
            if (!report.activeScenePreserved) throw new InvalidDataException("The active scene changed during hat review.");
            Debug.Log("COURIER_HAT_REVIEW: " + report.pairs + " hat fits, " + count + " native views; active scene preserved. " + folder);
        }

        static void Light(Scene scene, string name, Vector3 euler, float intensity, Color color)
        {
            var node = new GameObject(name, typeof(Light)); SceneManager.MoveGameObjectToScene(node, scene);
            node.transform.rotation = Quaternion.Euler(euler); var light = node.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = intensity; light.color = color;
        }
    }
}
