using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Lengthens the original loops towards the shoulder, keeping the case size and mounting plane.</summary>
    public static class CourierStrapReachFitter
    {
        const string CatalogPath="Assets/RadioArchipelago/Content/CourierCatalog.asset";
        static readonly string Evidence=Path.GetFullPath("../Docs/Design/RadioArchipelago/StrapReach20261105");
        [Serializable] public sealed class Observation
        {
            public string hero,backpack;
            public float previousDepthScale,depthScale,shoulderZ,targetZ,frontBefore,frontAfter,mountZ,targetX,frontXAfter,outwardOffset;
            public bool reachesShoulder;
        }
        [Serializable] sealed class Report { public bool allReach; public Observation[] pairs; }

        public static void Pilot()
        {
            OriginalStrapSkinAuthoring.Run(AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath));
            Fit(true);
            ModularTailoringReview.StrapReachAfter();
        }
        public static void Run() { Fit(false); }
        public static void Complete()
        {
            Run();
            ModularWardrobeChecks.Run();
            CourierStrapSkinChecks.Run();
            ModularTailoringReview.StrapReachOwners();
        }
        static void Fit(bool pilot)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            var records=new List<Observation>();
            foreach(var hero in catalog.entries)
            {
                if(pilot&&hero.assetId!="Rumi")continue;
                string path=AssetDatabase.GetAssetPath(hero.prefab);
                var actor=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var wardrobe=actor.GetComponent<CourierWardrobeRig>();
                    var frame=wardrobe.bodyRig.visualPivot;
                    float shoulder=(frame.InverseTransformPoint(wardrobe.bodyRig.leftArm.position).z+
                        frame.InverseTransformPoint(wardrobe.bodyRig.rightArm.position).z)*.5f;
                    float shoulderHeight=(frame.InverseTransformPoint(wardrobe.bodyRig.leftArm.position).y+
                        frame.InverseTransformPoint(wardrobe.bodyRig.rightArm.position).y)*.5f;
                    foreach(var item in catalog.equipment.Where(e=>e.kind==CourierEquipmentKind.Backpack))
                    {
                        if(pilot&&item.id!="backpack_Rumi"&&item.id!="backpack_Rumi_Orbit")continue;
                        var fit=wardrobe.profile.FindFit(item.id);var rig=item.prefab.GetComponent<CourierEquipmentRig>();
                        var points=StaticPoints(item.prefab,rig).ToArray();
                        var strapPoints=rig.harness.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>(true))
                            .SelectMany(f=>f.sharedMesh.vertices.Select(p=>item.prefab.transform.InverseTransformPoint(f.transform.TransformPoint(p)))).ToArray();
                        float front=points.Max(p=>p.z),anchor=item.id=="backpack_Miro"?-.004f:strapPoints.Min(p=>p.z),distance=(front-anchor)*fit.scale;
                        float mount=fit.position.z+anchor*fit.scale,target=shoulder+.16f;
                        float previous=fit.harnessDepthScale;
                        fit.harnessAnchorZ=anchor;
                        fit.harnessForwardOffset=Mathf.Max(0,(target-fit.position.z)/fit.scale-front);
                        float strapFront=strapPoints.Max(q=>q.z);
                        float frontX=strapPoints.Where(p=>p.z>strapFront-.025f).Average(p=>Mathf.Abs(p.x))*fit.scale;
                        float targetX=wardrobe.profile.bodyWidth*.5f+.055f;
                        fit.harnessWidthScale=1;
                        fit.harnessOutwardOffset=(targetX-frontX)/fit.scale;
                        fit.harnessVerticalOffset=0;
                        if(item.id=="backpack_Miro"||item.id=="backpack_Miro_Garden")
                        {
                            var frontBand=points.Where(p=>p.z>=anchor+(front-anchor)*.7f).ToArray();
                            float centerHeight=(frontBand.Min(p=>p.y)+frontBand.Max(p=>p.y))*.5f;
                            fit.harnessVerticalOffset=(shoulderHeight+.12f-fit.position.y)/fit.scale-centerHeight;
                        }
                        fit.harnessDepthScale=1;
                        float after=fit.position.z+(front+fit.harnessForwardOffset)*fit.scale;
                        records.Add(new Observation{hero=hero.assetId,backpack=item.id,previousDepthScale=previous,
                            depthScale=(after-mount)/Mathf.Max(.01f,distance),shoulderZ=shoulder,targetZ=target,frontBefore=mount+distance*previous,
                            frontAfter=after,mountZ=mount,targetX=targetX,frontXAfter=frontX+fit.harnessOutwardOffset*fit.scale,
                            outwardOffset=fit.harnessOutwardOffset,reachesShoulder=after>=target-.015f});
                    }
                    PrefabUtility.SaveAsPrefabAsset(actor,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(actor);}
            }
            AssetDatabase.SaveAssets();Directory.CreateDirectory(Evidence);
            File.WriteAllText(Path.Combine(Evidence,pilot?"pilot-fit.json":"fit-report.json"),
                JsonUtility.ToJson(new Report{allReach=records.All(r=>r.reachesShoulder),pairs=records.ToArray()},true));
            Debug.Log("COURIER_STRAP_REACH "+records.Count+" authored fits; intact original strap meshes.");
        }
        static IEnumerable<Vector3> StaticPoints(GameObject prefab,CourierEquipmentRig rig)
        {
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null|| (filter.name!="Case"&&!rig.harness.Any(t=>filter.transform==t||filter.transform.IsChildOf(t))))continue;
                foreach(var point in filter.sharedMesh.vertices)
                    yield return prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(point));
            }
        }
    }
}
