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
        static readonly string Evidence=Path.GetFullPath("../Docs/Design/RadioArchipelago/StrapFitCorrection20261105");
        [Serializable] public sealed class Observation
        {
            public string hero,backpack;
            public float previousDepthScale,depthScale,shoulderZ,targetZ,frontBefore,frontAfter,mountZ,targetX,frontXAfter,outwardOffset;
            public float packLowering, armFrontZ, upperDrop, lowerDrop;
            public bool reachesShoulder;
        }
        [Serializable] sealed class Report { public bool allReach; public Observation[] pairs; }

        public static void Pilot()
        {
            if(!File.Exists(Path.Combine(Evidence,"Before/report.json"))) ModularTailoringReview.StrapCorrectionBefore();
            OriginalStrapSkinAuthoring.Run(AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath));
            Fit(true);
            CourierStrapClearanceProbe.Run();
            ModularTailoringReview.StrapCorrectionAfter();
        }
        public static void Run() { Fit(false); }
        public static void Complete()
        {
            Run();
            ModularWardrobeChecks.Run();
            CourierStrapSkinChecks.Run();
            CourierStrapClearanceProbe.RunAllOwners();
            ModularTailoringReview.StrapCorrectionOwners();
        }
        public static void RebuildAndComplete()
        {
            OriginalStrapSkinAuthoring.Run(AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath));
            Complete();
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
                    wardrobe.motion.ManualSimulation=true;wardrobe.motion.ResetMotion();
                    var body=CourierStrapClearanceProbe.ReadArmTargets(actor);
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
                        // Derive the base pose rather than accumulating corrections on repeated runs.
                        float sourceCenter=(rig.caseBoundsMin.y+rig.caseBoundsMax.y)*.5f;
                        float baseY=shoulderHeight+.12f-sourceCenter*fit.scale;
                        // Keep the case on its original back mount. Ear clearance is
                        // provided by the upper strap controls, never by lowering the bag.
                        float lowering=0;
                        var backSection=wardrobe.profile.bodySections.OrderBy(s=>Mathf.Abs(s.height-(shoulderHeight+.12f))).First();
                        fit.position=new Vector3(0,baseY-lowering,backSection.backZ+.065f-rig.caseContactZ*fit.scale);
                        var sideFronts=rig.harness.Select(t=>t.GetComponentsInChildren<MeshFilter>(true)
                            .SelectMany(f=>f.sharedMesh.vertices.Select(p=>item.prefab.transform.InverseTransformPoint(f.transform.TransformPoint(p))))
                            .Max(p=>p.z)).ToArray();
                        float front=sideFronts.Min(),anchor=item.id=="backpack_Miro"?-.004f:strapPoints.Min(p=>p.z),distance=(front-anchor)*fit.scale;
                        float mount=fit.position.z+anchor*fit.scale,target=body.maximumArmZ+.065f;
                        float previous=fit.harnessDepthScale;
                        fit.harnessAnchorZ=anchor;
                        fit.harnessForwardOffset=Mathf.Max(0,(target-fit.position.z)/fit.scale-front);
                        float strapFront=strapPoints.Max(q=>q.z);
                        float frontX=strapPoints.Where(p=>p.z>strapFront-.025f).Average(p=>Mathf.Abs(p.x))*fit.scale;
                        float targetX=wardrobe.profile.bodyWidth*.5f+.055f;
                        fit.harnessWidthScale=1;
                        fit.harnessOutwardOffset=(targetX-frontX)/fit.scale;
                        float armOuter=Mathf.Max(Mathf.Abs(body.leftArm.minimum.x),Mathf.Abs(body.leftArm.maximum.x),
                            Mathf.Abs(body.rightArm.minimum.x),Mathf.Abs(body.rightArm.maximum.x));
                        fit.harnessLowerOutwardOffset=Mathf.Clamp(armOuter-.04f-targetX,0,.22f)/fit.scale;
                        float armHeight=Mathf.Max(body.leftArm.maximum.y-body.leftArm.minimum.y,body.rightArm.maximum.y-body.rightArm.minimum.y);
                        // The lower branch receives only part of this control's weight;
                        // allow enough travel for the visible ribbon to clear the wrist.
                        fit.harnessLowerDrop=Mathf.Clamp(armHeight*.45f,.12f,.25f)/fit.scale;
                        fit.harnessVerticalOffset=0;
                        if(item.id=="backpack_Miro"||item.id=="backpack_Miro_Garden")
                        {
                            var frontBand=points.Where(p=>p.z>=anchor+(front-anchor)*.7f).ToArray();
                            float centerHeight=(frontBand.Min(p=>p.y)+frontBand.Max(p=>p.y))*.5f;
                            fit.harnessVerticalOffset=(shoulderHeight+.12f-fit.position.y)/fit.scale-centerHeight;
                        }
                        fit.harnessDepthScale=1;
                        fit.harnessUpperDrop=UpperDrop(item.prefab,fit,wardrobe.profile,body.outerHead);
                        float after=fit.position.z+(front+fit.harnessForwardOffset)*fit.scale;
                        records.Add(new Observation{hero=hero.assetId,backpack=item.id,previousDepthScale=previous,
                            depthScale=(after-mount)/Mathf.Max(.01f,distance),shoulderZ=shoulder,targetZ=target,frontBefore=mount+distance*previous,
                            frontAfter=after,mountZ=mount,targetX=targetX,frontXAfter=frontX+fit.harnessOutwardOffset*fit.scale,
                            outwardOffset=fit.harnessOutwardOffset,packLowering=lowering,upperDrop=fit.harnessUpperDrop*fit.scale,
                            lowerDrop=fit.harnessLowerDrop*fit.scale,armFrontZ=body.maximumArmZ,reachesShoulder=after>=target-.015f});
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
        static float UpperDrop(GameObject equipment,CourierEquipmentFit fit,CourierMountProfile profile,CourierStrapClearanceProbe.Cloud ears)
        {
            if(ears.vertices==0)return .08f/fit.scale;
            float needed=0,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
            float minimumX=profile.bodyWidth*.5f*1.02f-.06f;
            float maximumX=Mathf.Max(Mathf.Abs(ears.minimum.x),Mathf.Abs(ears.maximum.x))+.05f;
            foreach(var skin in equipment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var vertices=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;
                var toFrame=equipment.transform.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++)
                {
                    var point=toFrame.MultiplyPoint3x4(vertices[i]);
                    minY=Mathf.Min(minY,point.y);maxY=Mathf.Max(maxY,point.y);
                    var w=weights[i];float moving=0,upper=0;
                    Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                    if(upper<.08f)continue;
                    var pose=fit.position+(point+new Vector3(Mathf.Sign(point.x)*(fit.harnessOutwardOffset*moving+fit.harnessLowerOutwardOffset*(moving-upper)),
                        fit.harnessVerticalOffset*moving-fit.harnessLowerDrop*(moving-upper),fit.harnessForwardOffset*moving))*fit.scale;
                    if(Mathf.Abs(pose.x)<minimumX||Mathf.Abs(pose.x)>maximumX||pose.z<ears.minimum.z-.05f||pose.z>ears.maximum.z+.05f)continue;
                    needed=Mathf.Max(needed,(pose.y-ears.minimum.y+.04f)/(fit.scale*upper));
                    void Add(int index,float value){if(index>0)moving+=value;if(index==3||index==4)upper+=value;}
                }
            }
            return Mathf.Clamp(needed,0,Mathf.Min(.50f/fit.scale,(maxY-minY)*.38f));
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
