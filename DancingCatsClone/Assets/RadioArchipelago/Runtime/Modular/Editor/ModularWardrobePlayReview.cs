#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using RadioArchipelago.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RadioArchipelago.Editor
{
    /// <summary>Exercises the actual scene startup, native Canvas buttons and equipped game/preview actors.</summary>
    [InitializeOnLoad]
    public static class ModularWardrobePlayReview
    {
        private const string Key = "RadioArchipelago.ModularWardrobePlayReview";
        private const string GuideKey = "DancingCatsClone.v1.howToPlaySeen";
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,
            SessionState.GetBool(Key + ".keepEditor", false)
                ? "../../Docs/Design/RadioArchipelago/BodyHarness20261105/PlayMode"
                : "../../Docs/Design/RadioArchipelago/ModularCast20261104/UnityUI"));
        private static RadioMenuPresenter ui;
        private static OriginalPlayController game;
        private static Camera captureCamera;
        private static RenderTexture captureTarget;
        private static int frameWait;
        private static Report report;

        [Serializable] private sealed class Preference { public string key; public bool exists; public int value; }
        [Serializable] private sealed class SceneRecord { public string path; public bool loaded, active; }
        [Serializable] private sealed class SceneBackup { public SceneRecord[] setup; }
        [Serializable] private sealed class Backup { public Preference[] integers; public bool hadProfile; public string profile; }
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string details;
            public string capture = "Actual Play Mode UI Canvas, native camera render texture; no HTML or reconstructed UI.";
            public string backpack, hat, scarf;
            public int errors;
            public string[] screenshots = Array.Empty<string>();
            public string[] checks = Array.Empty<string>();
            public string[] errorMessages = Array.Empty<string>();
            public bool preferencesRestored;
        }

        static ModularWardrobePlayReview()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnMode;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Radio Archipelago/Modular couriers/Review real workshop in Play Mode and exit")]
        public static void Run() => Begin(false);

        public static void RunInOpenEditor() => Begin(true);

        private static void Begin(bool keepEditor)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Start modular workshop review in compiled Edit Mode.");
            if (SessionState.GetBool(Key, false)) throw new InvalidOperationException("A modular wardrobe review is already active.");
            if (keepEditor)
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty ||
                        string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path))
                        throw new InvalidOperationException("The native UI review preserves clean scenes; save pending scene edits first.");
                SessionState.SetString(Key + ".scenes", JsonUtility.ToJson(new SceneBackup { setup = EditorSceneManager.GetSceneManagerSetup()
                    .Select(s => new SceneRecord { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() }));
            }
            SessionState.SetBool(Key + ".keepEditor", keepEditor);
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            if (catalog == null || catalog.entries.Length != 10) throw new InvalidOperationException("Import all ten couriers before the Play review.");
            Directory.CreateDirectory(Output);
            var keys = new List<string> { CourierSelection.PreferenceKey, GuideKey };
            foreach (var hero in catalog.entries)
                foreach (CourierEquipmentKind kind in Enum.GetValues(typeof(CourierEquipmentKind)))
                    keys.Add("RadioArchipelago.v1.outfit." + hero.id + "." + kind);
            var backup = new Backup
            {
                integers = keys.Select(key => new Preference { key = key, exists = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key) }).ToArray(),
                hadProfile = PlayerPrefs.HasKey(OriginalProgression.SaveKey), profile = PlayerPrefs.GetString(OriginalProgression.SaveKey, "")
            };
            SessionState.SetString(Key + ".backup", JsonUtility.ToJson(backup));
            report = new Report
            {
                backpack = catalog.equipment.First(x => x.kind == CourierEquipmentKind.Backpack && x.id != catalog.entries[0].defaultBackpackId).id,
                hat = catalog.equipment.First(x => x.kind == CourierEquipmentKind.Hat).id,
                scarf = catalog.equipment.First(x => x.kind == CourierEquipmentKind.Scarf).id
            };
            // Isolate every tested cosmetic slot and restore its original presence/value afterwards.
            foreach (var key in keys.Where(key => key.StartsWith("RadioArchipelago.v1.outfit.", StringComparison.Ordinal))) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt(CourierSelection.PreferenceKey, 0); PlayerPrefs.SetInt(GuideKey, 1); PlayerPrefs.Save();
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".finishing", false);
            SessionState.SetInt(Key + ".stage", 0);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            StoreReport();
            EditorSceneManager.OpenScene("Assets/RadioArchipelago/Scenes/AmberWindsPilot.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || SessionState.GetBool(Key + ".finishing", false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", float.MaxValue))
            { Finish(false, "The real workshop review timed out."); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                LoadReport();
                if (report.errors > 0) throw new InvalidOperationException("Unity reported an error during the native UI review.");
                if (++frameWait < 18) return;
                if (ui == null) ui = UnityEngine.Object.FindFirstObjectByType<RadioMenuPresenter>();
                if (game == null) game = UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
                if (ui == null || ui.UICanvas == null || game == null || game.Phase != ClonePhase.Menu) return;
                switch (SessionState.GetInt(Key + ".stage", 0))
                {
                    case 0:
                        SetupCapture();
                        Check(game.Actor != null && GameplayRig() != null, "VContainer scene startup installed the modular courier under the island route anchor.");
                        Next(); break;
                    case 1:
                        Capture("01-home.png"); Click("Navigation_Workshop"); Next(); break;
                    case 2:
                        Check(ui.CurrentView == "Workshop", "The real workshop navigation button opens the workshop.");
                        Capture("02-workshop-heroes.png"); Click("OutfitCategory_1"); Next(); break;
                    case 3:
                        Check(ui.WorkshopCategory == 1, "The backpack category is selectable.");
                        Capture("03-workshop-backpacks.png"); Click("WorkshopItem_" + report.backpack); Next(); break;
                    case 4:
                        CheckEquipped(CourierEquipmentKind.Backpack, report.backpack);
                        Click("OutfitCategory_2"); Next(); break;
                    case 5:
                        Capture("04-workshop-hats.png"); Click("WorkshopItem_" + report.hat); Next(); break;
                    case 6:
                        CheckEquipped(CourierEquipmentKind.Hat, report.hat);
                        Click("OutfitCategory_3"); Next(); break;
                    case 7:
                        Capture("05-workshop-scarves.png"); Click("WorkshopItem_" + report.scarf); Next(); break;
                    case 8:
                        CheckEquipped(CourierEquipmentKind.Scarf, report.scarf);
                        Capture("06-workshop-dressed.png"); Click("OutfitCategory_0"); Next(); break;
                    case 9:
                        Click("WorkshopItem_Hero_2"); Next(); break;
                    case 10:
                        Check(GameplayRig()!=null && GameplayRig().gameObject.name == "orbit", "The actual hero button switches the gameplay courier to Nori.");
                        var other = GameplayRig();
                        Check(other.Equipped(CourierEquipmentKind.Hat) == null, "A different hero keeps its own independent hat choice.");
                        Capture("07-workshop-nori.png"); Click("WorkshopItem_Hero_0"); Next(); break;
                    case 11:
                        CheckEquipped(CourierEquipmentKind.Backpack, report.backpack);
                        CheckEquipped(CourierEquipmentKind.Hat, report.hat);
                        CheckEquipped(CourierEquipmentKind.Scarf, report.scarf);
                        Capture("08-workshop-restored-tiko.png");
                        Check(ui.UICanvas.GetComponentsInChildren<ScrollRect>(true).Any(x => x.name == "OutfitScroll" && x.viewport != null && x.content != null), "All cosmetic categories use a masked native ScrollRect.");
                        Finish(true, "Actual Play Mode initialization, five native UI views, three equipped slots and per-hero outfit restoration passed."); break;
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void SetupCapture()
        {
            var node = new GameObject("Owned modular UI proof camera", typeof(Camera));
            node.transform.position = new Vector3(3000, 3000, -10);
            captureCamera = node.GetComponent<Camera>();
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = new Color(.98f, .95f, .89f, 1);
            captureCamera.orthographic = true; captureCamera.orthographicSize = 5;
            captureCamera.nearClipPlane = .1f; captureCamera.farClipPlane = 50;
            captureCamera.allowHDR = false; captureCamera.allowMSAA = false;
            captureTarget = new RenderTexture(540, 1168, 24, RenderTextureFormat.ARGB32) { name = "Owned modular native UI proof" };
            captureTarget.Create(); captureCamera.targetTexture = captureTarget;
            var canvas = ui.UICanvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = captureCamera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
        }

        private static void Capture(string name)
        {
            if (captureTarget == null || !captureTarget.IsCreated()) throw new InvalidOperationException("The native capture target is not available.");
            Canvas.ForceUpdateCanvases();
            foreach (var preview in ui.UICanvas.GetComponentsInChildren<CourierMenuPreview>(false))
                if (preview.PreviewCamera != null && preview.PreviewCamera.isActiveAndEnabled) preview.PreviewCamera.Render();
            captureCamera.Render();
            var previous = RenderTexture.active;
            var pixels = new Texture2D(captureTarget.width, captureTarget.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = captureTarget;
                pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32(); var distinct = new HashSet<int>();
                for (int i = 0; i < colors.Length; i += 127)
                    distinct.Add(colors[i].r << 16 | colors[i].g << 8 | colors[i].b);
                if (distinct.Count < 24) throw new InvalidOperationException("The native Canvas capture appears blank: " + name);
                File.WriteAllBytes(Path.Combine(Output, name), pixels.EncodeToPNG());
                report.screenshots = report.screenshots.Concat(new[] { name }).ToArray(); StoreReport();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
        }

        private static void Click(string name)
        {
            var button = ui.UICanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .FirstOrDefault(x => x.name == name && x.gameObject.activeInHierarchy);
            if (button == null || !button.interactable) throw new InvalidOperationException("The native UI button is unavailable: " + name);
            button.onClick.Invoke();
        }

        private static void CheckEquipped(CourierEquipmentKind kind, string id)
        {
            var gameplay = GameplayRig();
            Check(gameplay != null && gameplay.Equipped(kind) != null && gameplay.Equipped(kind).name == "Equipped_" + id, "Gameplay has equipped " + id + ".");
            var preview = ui.UICanvas.GetComponentsInChildren<CourierMenuPreview>(false).Single();
            var previewWardrobe = preview.PreviewCamera.transform.parent.GetComponentInChildren<CourierWardrobeRig>(false);
            Check(previewWardrobe != null && previewWardrobe.Equipped(kind) != null && previewWardrobe.Equipped(kind).name == "Equipped_" + id, "The live workshop preview has equipped " + id + ".");
            if (kind == CourierEquipmentKind.Backpack)
            {
                Check(gameplay.BodyHarness != null && gameplay.BodyHarness.activeInHierarchy &&
                    previewWardrobe.BodyHarness != null && previewWardrobe.BodyHarness.activeInHierarchy,
                    "Both the gameplay hero and live workshop preview use the permanent body harness.");
                Check(gameplay.Equipped(kind).GetComponent<CourierEquipmentRig>().harness.Length == 0 &&
                    previewWardrobe.Equipped(kind).GetComponent<CourierEquipmentRig>().harness.Length == 0,
                    "The actual equipped backpack cases have no original straps.");
            }
        }
        private static CourierWardrobeRig GameplayRig()
        {
            // The island reparents presentation onto its curved route; Actor remains
            // the logical rhythm/trigger transform rather than the rendered courier.
            var island = game.IslandStage != null ? game.IslandStage.GetComponentInChildren<CourierWardrobeRig>(true) : null;
            return island != null ? island : game.Actor.GetComponentInChildren<CourierWardrobeRig>(true);
        }

        private static void Check(bool value, string description)
        {
            if (!value) throw new InvalidOperationException(description);
            report.checks = report.checks.Concat(new[] { description }).ToArray(); StoreReport();
        }

        private static void Next() { SessionState.SetInt(Key + ".stage", SessionState.GetInt(Key + ".stage", 0) + 1); frameWait = 0; }
        private static void StoreReport() => SessionState.SetString(Key + ".report", JsonUtility.ToJson(report));
        private static void LoadReport() { if (report == null) report = JsonUtility.FromJson<Report>(SessionState.GetString(Key + ".report", "{}")); }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Key, false) || type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            LoadReport(); report.errors++; report.errorMessages = report.errorMessages.Concat(new[] { message + "\n" + trace }).ToArray(); StoreReport();
        }

        private static void Finish(bool success, string details)
        {
            if (SessionState.GetBool(Key + ".finishing", false)) return;
            LoadReport(); report.passed = success && report.errors == 0; report.details = details; StoreReport();
            SessionState.SetBool(Key + ".finishing", true);
            CleanupCamera();
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            else RestoreAndExit();
        }

        private static void OnMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { frameWait = 0; ui = null; game = null; report = null; }
            if (state == PlayModeStateChange.ExitingPlayMode) CleanupCamera();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!SessionState.GetBool(Key + ".finishing", false))
                { LoadReport(); report.passed = false; report.details = "Play Mode stopped before the native review finished."; StoreReport(); }
                RestoreAndExit();
            }
        }

        private static void CleanupCamera()
        {
            if (ui != null && ui.UICanvas != null) { ui.UICanvas.renderMode = RenderMode.ScreenSpaceOverlay; ui.UICanvas.worldCamera = null; }
            if (captureCamera != null) { captureCamera.targetTexture = null; UnityEngine.Object.DestroyImmediate(captureCamera.gameObject); captureCamera = null; }
            if (captureTarget != null) { captureTarget.Release(); UnityEngine.Object.DestroyImmediate(captureTarget); captureTarget = null; }
        }

        private static void RestoreAndExit()
        {
            LoadReport();
            try
            {
                var backup = JsonUtility.FromJson<Backup>(SessionState.GetString(Key + ".backup", "{}"));
                foreach (var entry in backup.integers)
                    if (entry.exists) PlayerPrefs.SetInt(entry.key, entry.value); else PlayerPrefs.DeleteKey(entry.key);
                if (backup.hadProfile) PlayerPrefs.SetString(OriginalProgression.SaveKey, backup.profile); else PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
                PlayerPrefs.Save(); report.preferencesRestored = true;
            }
            catch (Exception error) { report.passed = false; report.details += "\nPreferences restoration failed: " + error; }
            SessionState.SetBool(Key, false); StoreReport();
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("RADIO_MODULAR_NATIVE_UI " + (report.passed ? "PASS" : "FAIL") + ": " + report.details);
            if (SessionState.GetBool(Key + ".keepEditor", false))
            {
                var scenes = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + ".scenes", "{}"));
                if (scenes?.setup != null && scenes.setup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(scenes.setup.Select(s => new SceneSetup
                        { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
            }
            else EditorApplication.delayCall += () => EditorApplication.Exit(report.passed ? 0 : 1);
        }
    }
}
#endif
