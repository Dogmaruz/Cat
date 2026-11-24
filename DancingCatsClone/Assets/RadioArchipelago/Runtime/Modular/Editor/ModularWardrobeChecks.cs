#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using RhythmGame.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    public static class ModularWardrobeChecks
    {
        [MenuItem("Radio Archipelago/Modular couriers/Check selection and imported outfits")]
        public static void Run()
        {
            RunRuntimeContracts();
            RunImportedCatalog();
        }

        public static void RunRuntimeContracts()
        {
            var catalog = ScriptableObject.CreateInstance<CourierCatalog>();
            try
            {
                catalog.entries = new[]
                {
                    new CourierDefinition { id = "signal", defaultBackpackId = "pack_tiko" },
                    new CourierDefinition { id = "sail", defaultBackpackId = "pack_lumen" }
                };
                catalog.equipment = new[]
                {
                    new CourierEquipmentDefinition { id = "pack_tiko", kind = CourierEquipmentKind.Backpack },
                    new CourierEquipmentDefinition { id = "pack_lumen", kind = CourierEquipmentKind.Backpack },
                    new CourierEquipmentDefinition { id = "hat_one", kind = CourierEquipmentKind.Hat }
                };
                var store = new MemoryPreferences();
                var selection = new CourierSelection(catalog, store);
                Require(selection.SelectedEquipment(CourierEquipmentKind.Backpack)?.id == "pack_tiko", "A new hero must start with its authored backpack.");
                int notifications = 0;
                selection.Changed += () => notifications++;
                selection.Equip(CourierEquipmentKind.Hat, "hat_one");
                selection.Equip(CourierEquipmentKind.Hat, "hat_one");
                Require(notifications == 1, "An unchanged outfit must not rebuild the character.");
                selection.Equip(CourierEquipmentKind.Backpack, null);
                selection.Select(1);
                Require(selection.SelectedEquipment(CourierEquipmentKind.Backpack)?.id == "pack_lumen", "A hero's outfit must not leak into another hero.");
                Require(selection.SelectedEquipment(CourierEquipmentKind.Hat) == null, "New heroes must not inherit another hero's hat.");
                selection.Select(0);
                Require(selection.SelectedEquipment(CourierEquipmentKind.Backpack) == null, "The explicit unequipped choice must survive hero switching.");
                Require(selection.SelectedEquipment(CourierEquipmentKind.Hat)?.id == "hat_one", "A hero must recover its own saved outfit.");
                Array.Reverse(catalog.equipment);
                var restored = new CourierSelection(catalog, store);
                Require(restored.SelectedEquipment(CourierEquipmentKind.Hat)?.id == "hat_one", "Catalog reordering must not replace saved equipment.");
                Require(restored.SelectedEquipment(CourierEquipmentKind.Backpack) == null, "The unequipped choice must survive service recreation.");
                bool rejected = false;
                try { restored.Equip(CourierEquipmentKind.Scarf, "hat_one"); } catch (ArgumentException) { rejected = true; }
                Require(rejected && restored.SelectedEquipment(CourierEquipmentKind.Hat)?.id == "hat_one", "An equipment request for the wrong socket must not corrupt the outfit.");
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
            CheckEquipmentRebinding();
            CheckImportedUnitFootLift();
            Debug.Log("RADIO_MODULAR_CONTRACTS PASS: per-hero outfit persistence, stable IDs, empty slots, no duplicate changes, rebind, pause and custom rotor axes.");
        }

        private static void CheckImportedUnitFootLift()
        {
            foreach (float parentScale in new[] { .01f, 1f, 100f })
            foreach (float actorScale in new[] { .47f, 1f })
            {
                var actor = new GameObject("Imported foot unit conversion");
                try
                {
                    actor.transform.localScale = Vector3.one * actorScale;
                    var visual = Child(actor.transform, "VisualPivot", Vector3.zero);
                    visual.localScale = new Vector3(1.2f, .9f, 1.1f);
                    var importedParent = Child(visual, "FBX bone unit conversion", Vector3.zero);
                    importedParent.localScale = Vector3.one * parentScale;
                    importedParent.localRotation = Quaternion.Euler(90, 0, 0);
                    var foot = Child(importedParent, "foot.L", new Vector3(.2f, .1f, 0) / parentScale);
                    var rest = foot.localPosition;
                    var motion = actor.AddComponent<CourierMotion>(); motion.ManualSimulation = true;
                    motion.ConfigureRig(new CourierRigBinding { visualPivot = visual, leftFoot = foot });
                    motion.PlayIdle(); motion.SimulateSprings(.08f);
                    float requested = motion.ReadEditorTimingPose().leftFootLift;
                    Vector3 actual = importedParent.TransformVector(foot.localPosition - rest);
                    Vector3 expected = visual.TransformVector(Vector3.up * requested);
                    Require(requested > .001f && Vector3.Distance(actual, expected) < .00001f,
                        "A short foot lift must retain visual-local distance under FBX parent scale " + parentScale + " and actor scale " + actorScale + ".");
                }
                finally { UnityEngine.Object.DestroyImmediate(actor); }
            }
        }

        private static void CheckEquipmentRebinding()
        {
            var actor = new GameObject("Modular rebind test");
            var equipment = new GameObject("Synthetic rigid rotor");
            try
            {
                var visual = Child(actor.transform, "VisualPivot", Vector3.zero);
                var head = Child(visual, "head", Vector3.up);
                var left = Child(visual, "arm.L", new Vector3(.4f, .8f, 0));
                var right = Child(visual, "arm.R", new Vector3(-.4f, .8f, 0));
                var motion = actor.AddComponent<CourierMotion>();
                motion.ManualSimulation = true;
                var binding = new CourierRigBinding { visualPivot = visual, headPivot = head, leftArm = left, rightArm = right };
                var wardrobe = actor.AddComponent<CourierWardrobeRig>();
                wardrobe.Configure(motion, binding, visual, head);
                var equipmentRig = equipment.AddComponent<CourierEquipmentRig>();
                equipmentRig.topPivot = Child(equipment.transform, "TopPivot", Vector3.up);
                equipmentRig.topRotor = Child(equipmentRig.topPivot, "TopRotor", Vector3.zero);
                equipmentRig.spinAxis = Vector3.forward;
                var definition = new CourierEquipmentDefinition { id = "test", kind = CourierEquipmentKind.Backpack, prefab = equipment };
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    wardrobe.Apply(definition, null, null);
                    var attached = wardrobe.Equipped(CourierEquipmentKind.Backpack).GetComponent<CourierEquipmentRig>();
                    Quaternion rest = attached.topPivot.localRotation;
                    motion.PlayHold(false, 2, true);
                    for (int i = 0; i < 60; i++) motion.SimulateSprings(1f / 60);
                    Require(Quaternion.Angle(attached.topPivot.localRotation, rest) > 20, "An equipped mechanism must actually rotate.");
                    Vector3 axis = attached.topPivot.localRotation * Vector3.forward;
                    Require(Vector3.Dot(axis, rest * Vector3.forward) > .999f, "A horizontal rotor must spin around its authored axis.");
                    var frozen = attached.topPivot.localRotation;
                    motion.SetPaused(true); motion.SimulateSprings(.2f);
                    Require(Quaternion.Angle(frozen, attached.topPivot.localRotation) < .001f, "An equipped rotor must pause with its courier.");
                    motion.SetPaused(false);
                    wardrobe.Apply(null, null, null);
                    Require(wardrobe.Equipped(CourierEquipmentKind.Backpack) == null, "Removing equipment must clear the socket.");
                    motion.PlayJump(.4f); motion.SimulateSprings(.05f);
                    Require(visual != null && left != null && right != null, "Removing equipment must keep body controls alive.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(equipment); }
        }

        public static void RunImportedCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            Require(catalog != null && catalog.entries.Length == 10, "The imported catalog must contain all ten heroes.");
            Require(catalog.entries[0].id == "signal" && catalog.entries[1].id == "sail" && catalog.entries[2].id == "orbit", "The original three saved indices and gameplay IDs must remain stable.");
            Require(catalog.equipment.Count(x => x.kind == CourierEquipmentKind.Backpack) == 20, "All twenty backpacks must be selectable.");
            Require(catalog.equipment.Count(x => x.kind == CourierEquipmentKind.Hat) == 7, "The seven retained hats must be selectable.");
            Require(catalog.equipment.All(x => x.id != "hat_SignalDiadem"), "The removed diadem must not return to the game catalog.");
            var retiredPreferences = new MemoryPreferences();
            retiredPreferences.SetInt("RadioArchipelago.v1.outfit.signal.Hat", CourierSelection.StableToken("hat_SignalDiadem"));
            Require(new CourierSelection(catalog, retiredPreferences).SelectedEquipment(CourierEquipmentKind.Hat) == null,
                "A saved retired diadem must resolve to an empty slot, not another hat.");
            Require(catalog.equipment.Count(x => x.kind == CourierEquipmentKind.Scarf) == 2, "Both scarves must be selectable.");
            Require(catalog.equipment.Select(x => CourierSelection.StableToken(x.id)).Distinct().Count() == catalog.equipment.Length, "Equipment save tokens must be unique.");
            var scene = EditorSceneManager.NewPreviewScene();
            int combinations = 0;
            try
            {
                foreach (var hero in catalog.entries)
                {
                    Require(hero.prefab != null, hero.id + ": missing prefab.");
                    var actor = UnityEngine.Object.Instantiate(hero.prefab);
                    SceneManager.MoveGameObjectToScene(actor, scene);
                    try
                    {
                        var wardrobe = actor.GetComponent<CourierWardrobeRig>();
                        Require(wardrobe != null && wardrobe.bodyRig != null, hero.id + ": missing modular rig.");
                        Require(wardrobe.bodyRig.leftArm != null && wardrobe.bodyRig.rightArm != null && wardrobe.bodyRig.leftFoot != null && wardrobe.bodyRig.rightFoot != null, hero.id + ": missing single-segment limb controls.");
                        foreach (var item in catalog.equipment)
                        {
                            Require(item.prefab != null, item.id + ": missing prefab.");
                            wardrobe.Apply(item.kind == CourierEquipmentKind.Backpack ? item : null,
                                item.kind == CourierEquipmentKind.Hat ? item : null,
                                item.kind == CourierEquipmentKind.Scarf ? item : null);
                            var gear = wardrobe.Equipped(item.kind);
                            Require(gear != null && gear.GetComponentsInChildren<Renderer>().Length > 0, hero.id + "/" + item.id + ": invisible equipment.");
                            Require(Finite(gear.transform.position) && Finite(gear.transform.lossyScale), hero.id + "/" + item.id + ": invalid fitting pose.");
                            if (item.kind == CourierEquipmentKind.Backpack)
                            {
                                var rig = gear.GetComponent<CourierEquipmentRig>();
                                Require(rig != null && rig.topPivot != null && rig.topRotor != null && rig.topRotor.IsChildOf(rig.topPivot), item.id + ": missing independent rotating top.");
                                Require(rig.harness.Length == 0 && rig.harnessFrontControls.Length == 0 && rig.harnessUpperControls.Length == 0,
                                    hero.id + "/" + item.id + ": backpack must have no legacy straps or deformation controls.");
                                Require(wardrobe.profile.bodyHarnessPrefab != null && wardrobe.BodyHarness != null && wardrobe.BodyHarness.activeInHierarchy,
                                    hero.id + "/" + item.id + ": missing hero's tailored harness.");
                            }
                            combinations++;
                        }
                        wardrobe.Apply(null, null, null);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("RADIO_MODULAR_CATALOG PASS: ten heroes, 20/7/2 equipment and " + combinations + " fitting/rebinding combinations. Visual silhouette and collision checks remain a separate review.");
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        { var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child; }
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x + value.y + value.z) && !float.IsInfinity(value.x + value.y + value.z);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private sealed class MemoryPreferences : IPlayerPreferences
        {
            private readonly Dictionary<string, int> values = new Dictionary<string, int>();
            public int GetInt(string key, int fallback = 0) => values.TryGetValue(key, out int value) ? value : fallback;
            public void SetInt(string key, int value) => values[key] = value;
        }
    }
}
#endif
