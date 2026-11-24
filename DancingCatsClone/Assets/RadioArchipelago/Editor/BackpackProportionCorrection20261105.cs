using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Tailors five oversized cases without changing the other fifteen styles or body-owned harnesses.</summary>
    public static class BackpackProportionCorrection20261105
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        const float TargetCaseWidth = 1.22f;
        const float BackContactOverlap = .115f;
        static readonly string Evidence = Path.GetFullPath("../Docs/Design/RadioArchipelago/BackpackProportions20261105");
        static readonly HashSet<string> Selected = new HashSet<string>(StringComparer.Ordinal)
        {
            "backpack_Boro", "backpack_Miro", "backpack_Miro_Garden",
            "backpack_Piko_Voyager", "backpack_Vela_Cloud"
        };

        [Serializable] public sealed class StyleRecord
        {
            public string equipmentId;
            public bool selected;
            public Vector3 authoredCaseSize, oldHeightNormalizedSize, targetSize;
            public float targetScale, reductionFromHeightNormalization;
        }

        [Serializable] public sealed class FitRecord
        {
            public string hero, equipmentId;
            public float previousScale, targetScale, coefficient;
            public Vector3 previousPosition, targetPosition, previousCenter, targetCenter;
            public float bodyBackZ, targetContactZ, targetContactOverlap;
        }

        [Serializable] public sealed class Report
        {
            public string capturedUtc, unityVersion, action;
            public bool passed, activeScenePreserved;
            public int heroes, correctedFits, unchangedBackpackFits, unchangedOtherEquipmentFits;
            public float targetCaseWidth = TargetCaseWidth;
            public StyleRecord[] styles;
            public FitRecord[] fits;
            public string method = "Only the five user-identified cases use a 1.22-unit width with one uniform scale. Their existing case-center height and horizontal center remain fixed; each hero's back-contact plane retains the accepted 0.115-unit overlap. Original meshes, PBR, body harnesses, rotor bindings and all other equipment poses remain unchanged.";
        }

        sealed class Actor : IDisposable
        {
            public string hero, path;
            public GameObject root;
            public CourierWardrobeRig wardrobe;
            public Dictionary<string, string> protectedFits;
            public GameObject harness;

            public void Dispose()
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Radio Archipelago/Modular couriers/Measure five oversized backpacks")]
        public static void Measure() => Process(false);

        [MenuItem("Radio Archipelago/Modular couriers/Correct five oversized backpacks")]
        public static void Run() => Process(true);

        public static void Apply() => Run();

        /// <summary>Optional importer hook after the standard case fits have been authored; never saves assets itself.</summary>
        public static void ApplyToProfile(CourierMountProfile profile, CourierCatalog catalog)
        {
            foreach (var item in Cases(catalog).Where(item => Selected.Contains(item.id)))
            {
                var fit = RequiredFit(profile, item.id);
                var plan = Plan("", profile, item, fit);
                fit.scale = plan.targetScale;
                fit.position = plan.targetPosition;
            }
        }

        static void Process(bool apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Measure or author backpack proportions in Edit Mode.");
            var scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty;
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("The courier catalog is missing.");
            var cases = Cases(catalog);
            if (cases.Length != 20 || Selected.Any(id => cases.All(item => item.id != id)))
                throw new InvalidDataException("Expected the twenty current backpack styles and all five selected IDs.");
            var actors = new List<Actor>(); var records = new List<FitRecord>();
            int untouchedBackpacks = 0, untouchedOther = 0;
            try
            {
                // Validate every hero before saving any prefab. Each actor lives in an isolated prefab-content scene.
                foreach (var hero in catalog.entries)
                {
                    var actor = new Actor { hero = hero.assetId, path = AssetDatabase.GetAssetPath(hero.prefab) };
                    actors.Add(actor);
                    actor.root = PrefabUtility.LoadPrefabContents(actor.path);
                    actor.wardrobe = actor.root.GetComponent<CourierWardrobeRig>();
                    if (actor.wardrobe == null || actor.wardrobe.profile == null)
                        throw new InvalidDataException(hero.assetId + ": missing wardrobe profile.");
                    var profile = actor.wardrobe.profile;
                    actor.harness = profile.bodyHarnessPrefab;
                    actor.protectedFits = profile.equipmentFits.Where(fit => fit != null && !Selected.Contains(fit.equipmentId))
                        .ToDictionary(fit => fit.equipmentId, JsonUtility.ToJson, StringComparer.Ordinal);
                    foreach (var item in cases)
                    {
                        var fit = RequiredFit(profile, item.id);
                        if (Selected.Contains(item.id)) records.Add(Plan(hero.assetId, profile, item, fit));
                        else untouchedBackpacks++;
                    }
                    untouchedOther += actor.protectedFits.Keys.Count(id => !id.StartsWith("backpack_", StringComparison.Ordinal));
                }
                if (actors.Count != 10 || records.Count != 50 || untouchedBackpacks != 150)
                    throw new InvalidDataException("Expected fifty corrected fits and 150 untouched backpack fits across ten heroes.");
                if (apply)
                {
                    foreach (var actor in actors)
                    {
                        var profile = actor.wardrobe.profile;
                        foreach (var plan in records.Where(plan => plan.hero == actor.hero))
                        {
                            var fit = RequiredFit(profile, plan.equipmentId);
                            fit.scale = plan.targetScale; fit.position = plan.targetPosition;
                        }
                        if (profile.bodyHarnessPrefab != actor.harness || actor.protectedFits.Any(pair =>
                            JsonUtility.ToJson(RequiredFit(profile, pair.Key)) != pair.Value))
                            throw new InvalidOperationException(actor.hero + ": unrelated equipment or body harness changed.");
                    }
                    foreach (var actor in actors) PrefabUtility.SaveAsPrefabAsset(actor.root, actor.path);
                    AssetDatabase.SaveAssets();
                }
            }
            finally { foreach (var actor in actors) actor.Dispose(); }
            var report = new Report
            {
                capturedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                action = apply ? "applied" : "measured", heroes = actors.Count, correctedFits = records.Count,
                unchangedBackpackFits = untouchedBackpacks, unchangedOtherEquipmentFits = untouchedOther,
                styles = cases.Select(Style).ToArray(), fits = records.ToArray(),
                activeScenePreserved = scene == SceneManager.GetActiveScene() && scene.isDirty == dirty
            };
            report.passed = report.activeScenePreserved && report.heroes == 10 && report.correctedFits == 50 && report.unchangedBackpackFits == 150;
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Path.Combine(Evidence, report.action + ".json"), JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException("Backpack proportion correction changed the active scene.");
            Debug.Log("BACKPACK_PROPORTIONS " + report.action + ": five styles / fifty fits; 150 other backpack fits preserved. Native review is required.");
        }

        static CourierEquipmentDefinition[] Cases(CourierCatalog catalog)
        {
            if (catalog == null || catalog.equipment == null) throw new InvalidDataException("The equipment catalog is missing.");
            return catalog.equipment.Where(item => item != null && item.kind == CourierEquipmentKind.Backpack).ToArray();
        }

        static CourierEquipmentFit RequiredFit(CourierMountProfile profile, string id)
        {
            var fit = profile.FindFit(id);
            if (fit == null) throw new InvalidDataException("Missing equipment fit: " + id);
            return fit;
        }

        static CourierEquipmentRig RequiredRig(CourierEquipmentDefinition item)
        {
            var rig = item.prefab != null ? item.prefab.GetComponent<CourierEquipmentRig>() : null;
            if (rig == null || rig.caseBoundsMax.x <= rig.caseBoundsMin.x || rig.caseBoundsMax.y <= rig.caseBoundsMin.y)
                throw new InvalidDataException("Missing authored case bounds: " + item.id);
            return rig;
        }

        static StyleRecord Style(CourierEquipmentDefinition item)
        {
            var rig = RequiredRig(item); Vector3 size = rig.caseBoundsMax - rig.caseBoundsMin;
            float old = 1.18f / size.y, target = Selected.Contains(item.id) ? TargetCaseWidth / size.x : old;
            return new StyleRecord { equipmentId = item.id, selected = Selected.Contains(item.id),
                authoredCaseSize = size, oldHeightNormalizedSize = size * old, targetSize = size * target,
                targetScale = target, reductionFromHeightNormalization = target / old };
        }

        static FitRecord Plan(string hero, CourierMountProfile profile, CourierEquipmentDefinition item, CourierEquipmentFit fit)
        {
            if (!Finite(fit.scale) || fit.scale <= 0 || !Finite(fit.position) || !Finite(fit.euler))
                throw new InvalidDataException(hero + "/" + item.id + ": nonfinite equipment pose.");
            if (profile.bodySections == null || profile.bodySections.Length == 0)
                throw new InvalidDataException(hero + ": missing authored body cross sections.");
            var rig = RequiredRig(item); var rotation = Quaternion.Euler(fit.euler);
            Vector3 localCenter = (rig.caseBoundsMin + rig.caseBoundsMax) * .5f;
            Vector3 oldCenter = fit.position + rotation * localCenter * fit.scale;
            float targetScale = TargetCaseWidth / (rig.caseBoundsMax.x - rig.caseBoundsMin.x);
            var section = profile.bodySections.OrderBy(s => Mathf.Abs(s.height - oldCenter.y)).First();
            Vector3 position = oldCenter - rotation * localCenter * targetScale;
            Vector3 localContact = new Vector3(localCenter.x, localCenter.y, rig.caseContactZ);
            position.z = section.backZ + BackContactOverlap - (rotation * localContact).z * targetScale;
            Vector3 newCenter = position + rotation * localCenter * targetScale;
            float contact = position.z + (rotation * localContact).z * targetScale;
            if (!Finite(position) || !Finite(targetScale) || targetScale <= 0 ||
                Mathf.Abs(newCenter.y - oldCenter.y) > .00001f || Mathf.Abs(newCenter.x - oldCenter.x) > .00001f ||
                Mathf.Abs(contact - section.backZ - BackContactOverlap) > .00001f)
                throw new InvalidOperationException(hero + "/" + item.id + ": invalid corrected mount.");
            return new FitRecord { hero = hero, equipmentId = item.id,
                previousScale = fit.scale, targetScale = targetScale, coefficient = targetScale / fit.scale,
                previousPosition = fit.position, targetPosition = position, previousCenter = oldCenter, targetCenter = newCenter,
                bodyBackZ = section.backZ, targetContactZ = contact, targetContactOverlap = contact - section.backZ };
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
