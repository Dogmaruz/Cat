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
    /// <summary>Unretouched native fixed-camera views in a disposable scene; never opens or saves a user scene.</summary>
    public static class BodyHarnessReview
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        [Serializable] public sealed class Observation
        {
            public string hero, backpack, pose;
            public string[] images;
        }
        [Serializable] public sealed class Report
        {
            public string capturedUtc, unityVersion, folder;
            public bool activeScenePreserved;
            public int images;
            public Observation[] observations;
            public string capture = "Native Unity Camera.Render with freshly CPU-baked character skins, unchanged production mesh/materials, identical cameras for every item. Close views intentionally crop the mechanism top to show the harness seating. No postprocessing or image retouching.";
        }

        public static void Capture() => Run(false);
        public static void Quick() => Run(true);
        public static void HarnessOnly() => Run(false, true);

        static void Run(bool quick, bool harnessOnly = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture body harnesses in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("The courier catalog is missing.");
            var active = SceneManager.GetActiveScene(); bool wasDirty = active.isDirty;
            string folder = Path.Combine(BodyHarnessChecks.EvidenceFolder, harnessOnly ? "NativeHarnessOnly" : quick ? "Quick" : "Native");
            Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Disposable body-harness review camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene; camera.orthographic = true;
            camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.94f, .925f, .875f);
            camera.allowHDR = false;
            AddLight(scene, "Warm neutral key", new Vector3(35, 145, 0), 1.5f, new Color(1, .95f, .87f));
            AddLight(scene, "Cool rear fill", new Vector3(25, -25, 0), 1.05f, new Color(.82f, .91f, 1));
            AddLight(scene, "Front fill", new Vector3(-12, 0, 0), .3f, new Color(1, .97f, .92f));
            var rt = RenderTexture.GetTemporary(720, 900, 24, RenderTextureFormat.ARGB32);
            var previousRT = RenderTexture.active;
            camera.targetTexture = rt;
            var pixels = new Texture2D(720, 900, TextureFormat.RGBA32, false);
            var observations = new List<Observation>(); int imageCount = 0;
            try
            {
                foreach (var hero in catalog.entries)
                {
                    if (quick && hero.assetId != "Rumi" && hero.assetId != "Tiko" && hero.assetId != "Lumen" && hero.assetId != "Nori") continue;
                    var actor = UnityEngine.Object.Instantiate(hero.prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                        var motion = actor.GetComponent<CourierMotion>(); motion.ManualSimulation = true;
                        var ownerPacks = catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Backpack &&
                            e.id.StartsWith("backpack_" + hero.assetId, StringComparison.Ordinal));
                        foreach (var pack in harnessOnly ? ownerPacks.Take(1) : ownerPacks)
                        {
                            wardrobe.Apply(pack, null, null); motion.ResetMotion();
                            if (harnessOnly)
                            {
                                wardrobe.Equipped(CourierEquipmentKind.Backpack).SetActive(false);
                                Observe("harness-only", false);
                            }
                            else Observe("rest", false);
                            if (!harnessOnly && hero.assetId == "Rumi")
                            {
                                Observe("contact-close", true);
                                var gear = wardrobe.Equipped(CourierEquipmentKind.Backpack);
                                gear.SetActive(false);
                                try { Observe("harness-without-case", true); }
                                finally { gear.SetActive(true); }
                                motion.PlayJump(1.3f);
                                for (int step = 0; step < 25; step++)
                                { motion.SetFlightProgress(step / 50f); motion.SimulateSprings(1 / 60f); }
                                Observe("flight", true);
                                motion.ResetMotion();
                            }
                            void Observe(string pose, bool close)
                            {
                                string stem = hero.assetId + "__" + pack.id + "__" + pose;
                                var files = new List<string>();
                                View("front", Vector3.forward); View("side", Vector3.right); View("back", Vector3.back);
                                observations.Add(new Observation { hero = hero.assetId, backpack = pack.id, pose = pose, images = files.ToArray() });
                                void View(string label, Vector3 direction)
                                {
                                    Vector3 target = new Vector3(0, harnessOnly ? 1.05f : close ? 1.15f : 1.8f, 0);
                                    camera.orthographicSize = harnessOnly ? 1.45f : close ? 1.25f : 2.35f;
                                    camera.transform.position = target + direction * 6;
                                    camera.transform.LookAt(target);
                                    using (var baked = new CourierPreviewSkinBake(actor)) { baked.Refresh(); camera.Render(); }
                                    RenderTexture.active = rt;
                                    pixels.ReadPixels(new Rect(0, 0, 720, 900), 0, 0); pixels.Apply();
                                    string name = stem + "__" + label + ".png";
                                    File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG());
                                    files.Add(name); imageCount++;
                                }
                            }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previousRT;
                RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            var report = new Report
            {
                capturedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                folder = folder, images = imageCount, observations = observations.ToArray(),
                activeScenePreserved = active == SceneManager.GetActiveScene() && active.isDirty == wasDirty
            };
            File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report, true));
            if (!report.activeScenePreserved) throw new InvalidDataException("The active scene state changed during the preview capture.");
            Debug.Log("BODY_HARNESS_REVIEW PASS: " + imageCount + " native fixed-camera screenshots; user's active scene preserved. " + folder);
        }

        static void AddLight(Scene scene, string name, Vector3 euler, float intensity, Color color)
        {
            var obj = new GameObject(name, typeof(Light)); SceneManager.MoveGameObjectToScene(obj, scene);
            obj.transform.rotation = Quaternion.Euler(euler);
            var light = obj.GetComponent<Light>(); light.type = LightType.Directional;
            light.intensity = intensity; light.color = color;
        }
    }
}
