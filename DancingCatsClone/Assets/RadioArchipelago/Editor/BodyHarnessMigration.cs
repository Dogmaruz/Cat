using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>One-time asset authoring: clean cases and an independent tailored harness per hero.</summary>
    public static class BodyHarnessMigration
    {
        const string CatalogPath="Assets/RadioArchipelago/Content/CourierCatalog.asset";
        [MenuItem("Radio Archipelago/Modular couriers/Install strapless cases and body harnesses")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install in Edit Mode.");
            ModularCourierAssetBuilder.SyncStraplessBackpacks();
        }

        public static void Pilot() => ModularCourierAssetBuilder.SyncStraplessBackpacks("Rumi","Rumi_Orbit");

        public static void FinalizeCatalog(CourierCatalog catalog)
        {
            foreach(var hero in catalog.entries)
            {
                string path=AssetDatabase.GetAssetPath(hero.prefab);
                var actor=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var wardrobe=actor.GetComponent<CourierWardrobeRig>();
                    wardrobe.motion.ManualSimulation=true;wardrobe.motion.ResetMotion();
                    var frame=wardrobe.bodyRig.visualPivot;
                    float shoulderHeight=(frame.InverseTransformPoint(wardrobe.bodyRig.leftArm.position).y+
                        frame.InverseTransformPoint(wardrobe.bodyRig.rightArm.position).y)*.5f;
                    foreach(var item in catalog.equipment.Where(e=>e.kind==CourierEquipmentKind.Backpack))
                    {
                        var fit=wardrobe.profile.FindFit(item.id);
                        if(fit==null)throw new InvalidDataException(hero.id+"/"+item.id+": missing equipment pose.");
                        var rig=item.prefab.GetComponent<CourierEquipmentRig>();
                        fit.scale=1.18f/Mathf.Max(.2f,rig.caseBoundsMax.y-rig.caseBoundsMin.y);
                        float sourceCenter=(rig.caseBoundsMin.y+rig.caseBoundsMax.y)*.5f;
                        var section=wardrobe.profile.bodySections.OrderBy(s=>Mathf.Abs(s.height-(shoulderHeight+.12f))).First();
                        // Keep the accepted high mount. Only the case overlaps the back slightly.
                        fit.position=new Vector3(0,shoulderHeight+.12f-sourceCenter*fit.scale,
                            section.backZ+.115f-rig.caseContactZ*fit.scale);
                        fit.euler=Vector3.zero;
                        fit.harnessWidthScale=fit.harnessDepthScale=1;
                        fit.harnessAnchorZ=fit.harnessForwardOffset=fit.harnessOutwardOffset=fit.harnessVerticalOffset=0;
                        fit.harnessUpperDrop=fit.harnessLowerDrop=fit.harnessLowerOutwardOffset=0;
                    }
                    BackpackProportionCorrection20261105.ApplyToProfile(wardrobe.profile,catalog);
                    PrefabUtility.SaveAsPrefabAsset(actor,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(actor); }
            }
            BodyHarnessAuthoring.Run(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void Validate()
        {
            ModularWardrobeChecks.Run();
            BodyHarnessChecks.Run();
        }
        public static void Capture() { BodyHarnessReview.Capture(); }
        public static void Quick() { BodyHarnessReview.Quick(); }
    }
}
