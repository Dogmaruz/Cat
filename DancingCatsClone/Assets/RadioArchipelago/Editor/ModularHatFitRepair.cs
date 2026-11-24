using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Curated pose entry points and read-only surface diagnostics for supplied hats.</summary>
    public static class ModularHatFitRepair
    {
        const string CatalogPath = "Assets/RadioArchipelago/Content/CourierCatalog.asset";
        static readonly string Source = Path.GetFullPath("../ArtSource/RadioArchipelago/ProcessedModular/Characters");
        static readonly string Evidence = Path.GetFullPath("../Docs/Design/RadioArchipelago/HatsAndMotion20261105");
        [Serializable] sealed class Eye { public float[] center; public float radiusZ; }
        [Serializable] sealed class Blink { public Eye[] eyes; }
        [Serializable] sealed class BodyMeta { public Blink blink; }
        [Serializable] public sealed class FitObservation
        {
            public string hero, hat;
            public float scale, previousScale, crownWidth, eyeTop, eyeClearance, rearClearance, backpackRoof, headSeatingOverlap, domeClearance;
            public float supportTargetHeight, rimHeight, measuredOpeningWidth, measuredOpeningDepth;
            public float minimumSupportGap, medianSupportGap, maximumSupportGap, supportedFraction, eyeLift;
            public float requiredRoofLift, maximumHeadRoofPenetration, maximumLateralFeaturePenetration;
            public int roofSamples, roofMisses, lateralFeatureSamples;
            public bool sideWallEnforced;
            public bool openHelmetFace;
            public int sideWallSamples, sideWallMisses, sideWallOutside;
            public float sideWallLift, maximumSideWallOverhang, maximumFeatureAboveOpening, sideWallCoverage;
            public float crownHeightScale=1, crownRadialScale=1;
            public int localSculptControls;
            public float maximumLocalSculpt, minimumSculptJacobian=1;
            public int physicalClothContacts;
            public float maximumPhysicalContactDepth;
            public string physicalContactScope;
            public string geometryVariant;
            public int supportSamples;
            public string fittingMethod = "Broad skull seating with real triangle contacts; full upper-head envelope, including ears and forelocks, must fit below the closed hat roof.";
            public Vector3 position;
            public bool passed;
        }

        [Serializable] sealed class ProbeBounds
        {
            public Vector3 min,max,size,center;
            public ProbeBounds(Bounds bounds){min=bounds.min;max=bounds.max;size=bounds.size;center=bounds.center;}
        }
        [Serializable] sealed class SkinScaleProbe
        {
            public string hero,renderer,trueFrameMesh,falseFrameMesh;
            public Vector3 localScale,lossyScale;
            public int vertices;
            public ProbeBounds trueRaw,trueFrame,falseRaw,falseFrame,rendererFrame;
        }
        [Serializable] sealed class SkinScaleReport { public SkinScaleProbe[] skins;public bool activeScenePreserved; }
        [Serializable] sealed class ProbeMesh { public Vector3[] vertices;public int[] triangles; }

        public static void ProbeSkinScale()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Probe skins in Edit Mode.");
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=active.isDirty;
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var records=new List<SkinScaleProbe>();string folder=Path.Combine(Evidence,"SkinScaleProbe");Directory.CreateDirectory(folder);
            try
            {
                foreach(var hero in catalog.entries.Where(h=>h.assetId=="Miro"||h.assetId=="Piko"))
                {
                    var actor=UnityEngine.Object.Instantiate(hero.prefab);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,scene);
                    try
                    {
                        var wardrobe=actor.GetComponent<CourierWardrobeRig>();
                        wardrobe.motion.ManualSimulation=true;wardrobe.motion.ResetMotion();
                        var frame=wardrobe.bodyRig.visualPivot;int index=0;
                        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            var withScale=new Mesh();var withoutScale=new Mesh();
                            try
                            {
                                skin.BakeMesh(withScale,true);skin.BakeMesh(withoutScale,false);
                                withScale.RecalculateBounds();withoutScale.RecalculateBounds();
                                var matrix=frame.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                                var a=withScale.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                                var b=withoutScale.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                                var world=skin.bounds;var corners=new List<Vector3>();
                                for(int corner=0;corner<8;corner++)corners.Add(frame.InverseTransformPoint(world.center+
                                    Vector3.Scale(world.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));
                                string prefix=hero.assetId+"_"+index++;
                                string truePath=Path.Combine(folder,prefix+"_true.json"),falsePath=Path.Combine(folder,prefix+"_false.json");
                                File.WriteAllText(truePath,JsonUtility.ToJson(new ProbeMesh{vertices=a,triangles=withScale.triangles}));
                                File.WriteAllText(falsePath,JsonUtility.ToJson(new ProbeMesh{vertices=b,triangles=withoutScale.triangles}));
                                records.Add(new SkinScaleProbe{hero=hero.assetId,renderer=skin.name,vertices=withScale.vertexCount,
                                    localScale=skin.transform.localScale,lossyScale=skin.transform.lossyScale,
                                    trueRaw=new ProbeBounds(withScale.bounds),falseRaw=new ProbeBounds(withoutScale.bounds),
                                    trueFrame=BoundsOf(a),falseFrame=BoundsOf(b),rendererFrame=BoundsOf(corners.ToArray()),
                                    trueFrameMesh=truePath,falseFrameMesh=falsePath});
                            }
                            finally{UnityEngine.Object.DestroyImmediate(withScale);UnityEngine.Object.DestroyImmediate(withoutScale);}
                        }
                    }
                    finally{UnityEngine.Object.DestroyImmediate(actor);}
                }
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
            var report=new SkinScaleReport{skins=records.ToArray(),activeScenePreserved=
                active==UnityEngine.SceneManagement.SceneManager.GetActiveScene()&&active.isDirty==dirty};
            File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));
            if(!report.activeScenePreserved)throw new InvalidOperationException("Skin scale probe changed the active scene.");
            Debug.Log("HAT_SKIN_SCALE_PROBE: "+records.Count+" renderer comparisons written; active scene preserved.");
        }

        static ProbeBounds BoundsOf(Vector3[] points)
        {
            var bounds=new Bounds(points.Length>0?points[0]:Vector3.zero,Vector3.zero);
            foreach(var point in points)bounds.Encapsulate(point);return new ProbeBounds(bounds);
        }

        public static void Run() => Repair(AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath));
        public static void ReloadGeneratedMeshes() => HatCrownVariantAuthoring.ReloadAll();

        public static void Repair(CourierCatalog catalog)
        {
            // Manual fitting is authoritative. Opening/importing a collection
            // must never apply unapproved prototype rows or run a shape solver.
            Debug.Log("HAT_FITTING_MANUAL_ONLY: game hat poses unchanged. Use the Hat Fitting Workbench and explicit Apply for one hero.");
        }

        /// <summary>Read-only check used by the native tailoring review.</summary>
        public static FitObservation Analyze(CourierDefinition hero, CourierEquipmentDefinition hat, CourierCatalog catalog)
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var actor=UnityEngine.Object.Instantiate(hero.prefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,scene);
            try
            {
                var wardrobe=actor.GetComponent<CourierWardrobeRig>();
                wardrobe.motion.ManualSimulation=true;wardrobe.motion.ResetMotion();
                var frame=wardrobe.bodyRig.visualPivot;var fit=wardrobe.profile.FindFit(hat.id);
                var body=BodyPoints(actor,frame);float eyes=EyeTop(hero.assetId);
                var head=ReadHead(body,wardrobe.profile,eyes);
                var activePrefab=fit.prefabOverride!=null?fit.prefabOverride:hat.prefab;
                var points=EquipmentPoints(activePrefab);
                // The anchored lower opening retains the source descriptor;
                // only the crown above it is tailored.
                var opening=ReadOpening(hat.id,EquipmentPoints(hat.prefab));
                var result=Observe(hero.assetId,hat,fit,points,body,head.width,eyes,
                    RearContact(wardrobe.profile,catalog),BackpackRoof(wardrobe.profile,catalog),fit.scale,head);
                using(var scalp=BodySurface(actor,frame))
                using(var underside=EquipmentSurface(activePrefab,frame,Quaternion.Euler(fit.euler),fit.scale))
                {
                    var contact=new Contacts();
                    foreach(var point in SupportPoints(head))
                        if(scalp.Height(point.x,point.z,true,out float top)&&top>eyes+.05f&&
                            underside.Height(point.x-fit.position.x,point.z-fit.position.z,false,out float bottom))
                            contact.gaps.Add(bottom+fit.position.y-top);
                    contact.openHelmetFace=IsOpenHelmetFace(hero.assetId,hat.id);
                    MeasureRoof(contact,hat.id,body,head,eyes,wardrobe.profile,fit,opening,underside);
                    if(RequiresSideWallFit(hero.assetId,hat.id))
                        MeasureSideWalls(contact,body,eyes,wardrobe.profile,fit,opening,underside);
                    if(fit.prefabOverride!=null)
                    {
                        var recipe=HatCrownVariantAuthoring.Read(hero.assetId,hat.id);
                        if(recipe!=null)
                        {
                            contact.crownHeight=recipe.heightScale;contact.crownRadius=recipe.radialScale;
                            contact.sculptControls=recipe.controls?.Length??0;
                            contact.sculptMaximum=recipe.maximumDisplacement;
                            contact.sculptJacobian=recipe.minimumJacobianDeterminant;
                            SculptControls(hero.assetId,hat.id,ClothSamples(hat.prefab,opening,recipe.anchorY,IsPatchFit(hero.assetId,hat.id)),eyes,fit,opening,scalp,
                                recipe.controls??Array.Empty<HatCrownVariantAuthoring.Control>(),recipe.anchorY,0,
                                out contact.physicalContacts,out contact.physicalDepth);
                            contact.physicalScope=ContactScope(hero.assetId,hat.id);
                        }
                        result.geometryVariant=AssetDatabase.GetAssetPath(fit.prefabOverride);
                    }
                    ContactEvidence(result,contact,head,opening,fit);
                }
                return result;
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
        }

        public sealed class OpeningDescriptor
        {
            public float width, depth, referenceHeight;
            public Vector3 center;
        }
        sealed class HeadSection { public float width, depth, height; public Vector3 center; }
        sealed class Contacts
        {
            public readonly List<float> gaps = new List<float>();
            public float eyeLift, roofLift, roofPenetration, lateralPenetration;
            public int roofSamples, roofMisses, lateralSamples;
            public bool sideEnforced;
            public bool openHelmetFace;
            public int sideSamples, sideMisses, sideOutside;
            public float sideLift, sideOverhang, featureAboveOpening;
            public float crownHeight=1, crownRadius=1;
            public int sculptControls;
            public float sculptMaximum,sculptJacobian=1;
            public int physicalContacts;
            public float physicalDepth;
            public string physicalScope="full outward cuff samples";
        }

        public static OpeningDescriptor ReadOpening(CourierEquipmentDefinition hat) => ReadOpening(hat.id, EquipmentPoints(hat.prefab));
        static OpeningDescriptor ReadOpening(string id, Vector3[] points)
        {
            // This chooses the supplied object's structural band, not a prescribed
            // head height. Actual seating below uses triangle surfaces, not this Y.
            float level = id == "hat_AviatorCap" ? .55f : id == "hat_ExplorerHat" ? .16f : .10f;
            var band = points.Where(p => p.y > level - .04f && p.y < level + .13f).ToArray();
            if (band.Length < 12) band = points;
            float x0 = Quantile(band.Select(p => p.x), .025f), x1 = Quantile(band.Select(p => p.x), .975f);
            float z0 = Quantile(band.Select(p => p.z), .025f), z1 = Quantile(band.Select(p => p.z), .975f);
            return new OpeningDescriptor { width = Mathf.Max(.2f, x1-x0), depth = Mathf.Max(.2f, z1-z0),
                center = new Vector3((x0+x1)*.5f, level, (z0+z1)*.5f), referenceHeight = level };
        }

        static Contacts Fit(string hero, CourierEquipmentDefinition item, Vector3[] points, Vector3[] body, HeadSection head, float eyes,
            CourierMountProfile profile, CourierEquipmentFit fit, Surface scalp, Transform frame)
        {
            var opening = ReadOpening(item.id, points);
            float sourceWidth = points.Max(p => p.x)-points.Min(p => p.x);
            float minWidth = profile.bodyWidth * (item.id == "hat_ExplorerHat" ? 1.18f : .94f);
            float maxWidth = profile.bodyWidth * (item.id == "hat_ExplorerHat" ? 1.90f : 1.60f);
            float wantedScale = Mathf.Max(head.width*1.06f/opening.width, head.depth*1.02f/opening.depth,
                minWidth/Mathf.Max(.1f,sourceWidth));
            float startScale=Mathf.Min(wantedScale,maxWidth/Mathf.Max(.1f,sourceWidth));
            fit.euler = new Vector3(-8, 0, 0);var rotation=Quaternion.Euler(fit.euler);
            var core=body.Where(p=>p.y>head.height+.07f&&Mathf.Abs(p.x)<profile.bodyWidth*.43f&&
                Mathf.Abs(p.z-head.center.z)<head.depth*.62f).ToArray();
            // The central skull decides the wearing position. Containment must
            // nevertheless include lateral ears and forelocks: excluding those
            // points allowed them to emerge through otherwise well-seated bands.
            bool openHelmetFace=IsOpenHelmetFace(hero,item.id);
            var roofPoints=UpperHeadSamples(body,eyes)
                .Where(p=>!openHelmetFace||p.z<=head.center.z+head.depth*.05f).ToArray();
            var upper=core.Where(p=>p.y>head.height+.22f).ToArray();
            var peakCenter=upper.Length>0?new Vector3(upper.Average(p=>p.x),0,upper.Average(p=>p.z)):head.center;
            Vector3 shift=new Vector3(Mathf.Clamp(peakCenter.x*.45f,-.05f,.05f),0,
                Mathf.Clamp((peakCenter.z-head.center.z)*.45f,-.06f,.06f));
            Contacts best=null;float bestScore=float.PositiveInfinity;float bestScale=startScale;Vector3 bestPosition=Vector3.zero;
            for(int candidate=0;candidate<7;candidate++)
            {
                fit.scale=Mathf.Min(startScale*Mathf.Pow(1.085f,candidate),maxWidth/Mathf.Max(.1f,sourceWidth));
                var bandCenter=rotation*opening.center*fit.scale;
                fit.position=new Vector3(-bandCenter.x+shift.x,0,head.center.z-bandCenter.z-.015f+shift.z);
                var rotated=points.Select(p=>rotation*p*fit.scale).ToArray();
                using(var underside=EquipmentSurface(item.prefab,frame,rotation,fit.scale))
                {
                    var differences=new List<float>();
                    foreach(var point in SupportPoints(head))
                        if(scalp.Height(point.x,point.z,true,out float top)&&top>eyes+.05f&&
                            underside.Height(point.x-fit.position.x,point.z-fit.position.z,false,out float bottom))differences.Add(top-bottom);
                    fit.position.y=differences.Count>=8?Quantile(differences,.5f)-.035f:head.height-bandCenter.y-.06f;
                    float eyePlane=head.center.z+head.depth*.30f;
                    var forehead=rotated.Where(p=>Mathf.Abs(p.x+fit.position.x)<head.width*.23f&&p.z+fit.position.z>eyePlane).ToArray();
                    float frontLow=forehead.Length>0?forehead.Min(p=>p.y)+fit.position.y:eyes+.08f;
                    float lift=Mathf.Max(0,eyes+.045f-frontLow);fit.position.y+=lift;
                    var result=new Contacts{eyeLift=lift,openHelmetFace=openHelmetFace};
                    if(item.id!="hat_SignalDiadem")
                    {
                        float required=fit.position.y;
                        foreach(var point in roofPoints)
                            if(point.y>fit.position.y+bandCenter.y+.035f)
                            {
                                if(underside.Height(point.x-fit.position.x,point.z-fit.position.z,true,out float roof))
                                {result.roofSamples++;required=Mathf.Max(required,point.y+.018f-roof);}
                                else result.roofMisses++;
                            }
                        result.roofLift=Mathf.Max(0,required-fit.position.y);fit.position.y=required;
                    }
                    if(RequiresSideWallFit(hero,item.id)&&!NeedsCrownVariant(hero,item.id))
                    {
                        // These native-review exceptions cross a vertical cuff,
                        // despite lying below a different roof triangle. Test the
                        // actual horizontal shell at each feature's own height.
                        float seatedHeight=fit.position.y, selectedHeight=seatedHeight;
                        float selectedPenalty=float.PositiveInfinity;
                        for(int step=0;step<=20;step++)
                        {
                            fit.position.y=seatedHeight+step*.015f;
                            MeasureSideWalls(result,body,eyes,profile,fit,opening,underside);
                            float penalty=result.sideMisses+result.sideOutside+result.sideOverhang*10;
                            if(penalty<selectedPenalty){selectedPenalty=penalty;selectedHeight=fit.position.y;}
                            if(result.sideMisses==0&&result.sideOutside==0)break;
                        }
                        fit.position.y=selectedHeight;
                        MeasureSideWalls(result,body,eyes,profile,fit,opening,underside);
                        result.sideLift=selectedHeight-seatedHeight;
                    }
                    foreach(var point in SupportPoints(head))
                        if(scalp.Height(point.x,point.z,true,out float top)&&top>eyes+.05f&&
                            underside.Height(point.x-fit.position.x,point.z-fit.position.z,false,out float bottom))result.gaps.Add(bottom+fit.position.y-top);
                    MeasureRoof(result,item.id,body,head,eyes,profile,fit,opening,underside);
                    float rim=fit.position.y+bandCenter.y;
                    float floatPenalty=Mathf.Max(0,rim-(head.height+.12f));
                    float sidePenalty=result.sideMisses+result.sideOutside>0 ? 25+result.sideOverhang*10 : 0;
                    float score=floatPenalty*8+result.roofMisses*.015f+sidePenalty+(fit.scale/startScale-1)*.04f;
                    if(score<bestScore){bestScore=score;best=result;bestScale=fit.scale;bestPosition=fit.position;}
                    if(floatPenalty<.015f&&result.roofMisses==0&&result.sideMisses==0&&result.sideOutside==0)break;
                }
            }
            fit.scale=bestScale;fit.position=bestPosition;return best;
        }

        static bool IsOpenHelmetFace(string hero,string hat) => hero=="Miro"&&hat=="hat_AviatorCap";

        static bool NeedsCrownVariant(string hero,string hat) =>
            hero=="Piko"&&(hat=="hat_AmberBeret"||hat=="hat_SailorBeret") ||
            hero=="Miro"&&hat=="hat_AmberBeret" ||
            (hero=="Vela"||hero=="Fira")&&hat=="hat_NavigatorCap";

        static Contacts TailorCrown(string hero,CourierEquipmentDefinition item,Vector3[] body,HeadSection head,float eyes,
            CourierMountProfile profile,CourierEquipmentFit fit,Surface scalp,Transform frame,OpeningDescriptor opening,Contacts seated)
        {
            var original=EquipmentPoints(item.prefab);
            float anchor=original.Min(p=>p.y)+(hero=="Fira"&&item.id=="hat_NavigatorCap"?-.05f:.003f),bestScore=float.PositiveInfinity;
            var clothSamples=ClothSamples(item.prefab,opening,anchor,IsPatchFit(hero,item.id));
            var controls=new List<HatCrownVariantAuthoring.Control>();
            HatCrownVariantAuthoring.Control[] best=Array.Empty<HatCrownVariantAuthoring.Control>();
            for(int pass=0;pass<=6;pass++)
            {
                var current=controls.ToArray();
                using(var surface=EquipmentSurface(item.prefab,frame,Quaternion.Euler(fit.euler),fit.scale,
                    p=>HatCrownVariantAuthoring.Deform(p,opening,1,1,current,anchor)))
                {
                    var check=new Contacts();
                    MeasureRoof(check,item.id,body,head,eyes,profile,fit,opening,surface);
                    MeasureSideWalls(check,body,eyes,profile,fit,opening,surface);
                    var added=SculptControls(hero,item.id,clothSamples,eyes,fit,opening,scalp,current,anchor,pass,
                        out int physicalContacts,out float physicalDepth);
                    float score=(physicalContacts>0?100+physicalContacts*.0005f:0)+physicalDepth*100+
                        check.roofPenetration*100+controls.Count*.0001f;
                    if(score<bestScore){bestScore=score;best=CopyControls(current);}
                    if(pass==6||(physicalContacts==0&&check.roofPenetration<=.005f))break;
                    if(added.Length==0)break;
                    controls.AddRange(added);
                }
                // Broaden the local falloff rather than permit folded cloth.
                for(int attempt=0;attempt<6;attempt++)
                {
                    var safeControls=controls.ToArray();
                    float minimum=original.Min(p=>HatCrownVariantAuthoring.Jacobian(p,opening,1,1,safeControls,anchor).determinant);
                    if(minimum>=.25f)break;
                    foreach(var control in controls){control.radius*=1.18f;control.verticalRadius*=1.18f;}
                }
            }
            // Rebuild from the untouched source on every repair. The fit's
            // natural broad-skull pose remains fixed while the crown is tailored.
            fit.prefabOverride=HatCrownVariantAuthoring.Save(hero,item,opening,1,1,best,anchor);
            using(var surface=EquipmentSurface(fit.prefabOverride,frame,Quaternion.Euler(fit.euler),fit.scale))
            {
                var result=new Contacts { eyeLift=seated.eyeLift,roofLift=seated.roofLift,
                    sculptControls=best.Length,
                    sculptMaximum=original.Max(p=>(HatCrownVariantAuthoring.Deform(p,opening,1,1,best,anchor)-p).magnitude),
                    sculptJacobian=original.Min(p=>HatCrownVariantAuthoring.Jacobian(p,opening,1,1,best,anchor).determinant) };
                foreach(var point in SupportPoints(head))
                    if(scalp.Height(point.x,point.z,true,out float top)&&top>eyes+.05f&&
                        surface.Height(point.x-fit.position.x,point.z-fit.position.z,false,out float bottom))
                        result.gaps.Add(bottom+fit.position.y-top);
                MeasureRoof(result,item.id,body,head,eyes,profile,fit,opening,surface);
                MeasureSideWalls(result,body,eyes,profile,fit,opening,surface);
                SculptControls(hero,item.id,clothSamples,eyes,fit,opening,scalp,best,anchor,0,out result.physicalContacts,out result.physicalDepth);
                result.physicalScope=ContactScope(hero,item.id);
                return result;
            }
        }

        static HatCrownVariantAuthoring.Control[] CopyControls(IEnumerable<HatCrownVariantAuthoring.Control> controls) =>
            controls.Select(c=>new HatCrownVariantAuthoring.Control{center=c.center,displacement=c.displacement,
                radius=c.radius,verticalRadius=c.verticalRadius,pass=c.pass}).ToArray();

        sealed class ClothSample { public Vector3 point,normal; }
        static bool IsPatchFit(string hero,string hat) => hero=="Piko"&&hat=="hat_SailorBeret" || hero=="Fira"&&hat=="hat_NavigatorCap";
        static string ContactScope(string hero,string hat) => IsPatchFit(hero,hat)?
            (hero=="Piko"?"Localized bilateral horn cuff patches, radius 0.12; horizontal X projection only":"Localized bilateral lower rolled lip patches, radius 0.14; horizontal X projection only"):
            "Full outward cuff vertices and triangle midpoint samples";

        static ClothSample[] ClothSamples(GameObject prefab,OpeningDescriptor opening,float anchor,bool lowerLip=false)
        {
            var samples=new List<ClothSample>();
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(mesh==null)continue;
                var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var normalMatrix=matrix.inverse.transpose;
                var vertices=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();var normals=mesh.normals;
                for(int i=0;i<vertices.Length;i++)if(normals.Length==vertices.Length)Add(vertices[i],normalMatrix.MultiplyVector(normals[i]).normalized);
                var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                    var normal=Vector3.Cross(b-a,c-a).normalized;
                    Add((a+b+c)/3,normal);Add((a+b)*.5f,normal);Add((b+c)*.5f,normal);Add((c+a)*.5f,normal);
                }
                void Add(Vector3 point,Vector3 normal)
                {
                    Vector3 radial=new Vector3(point.x-opening.center.x,0,point.z-opening.center.z).normalized;
                    if(point.y>anchor+.006f&&Mathf.Abs(normal.y)<(lowerLip ? .98f : .8f)&&Vector3.Dot(normal,radial)>(lowerLip ? .05f : .25f))
                        samples.Add(new ClothSample{point=point,normal=normal});
                }
            }
            return samples.ToArray();
        }

        static HatCrownVariantAuthoring.Control[] SculptControls(string hero,string hat,ClothSample[] samples,float eyes,
            CourierEquipmentFit fit,OpeningDescriptor opening,Surface body,HatCrownVariantAuthoring.Control[] previous,float anchor,int pass,
            out int contacts,out float deepest)
        {
            if(!IsPatchFit(hero,hat))return PhysicalSculptControls(samples,eyes,fit,opening,body,previous,anchor,pass,out contacts,out deepest);
            var rotation=Quaternion.Euler(fit.euler);var inverse=Quaternion.Inverse(rotation);
            var selected=new Dictionary<string,HatCrownVariantAuthoring.Control>();contacts=0;deepest=0;
            bool sailor=hero=="Piko";float y=sailor ? 1.863438f : 1.481f,z=sailor ? .014531f : -.165f;
            Vector3 focus=new Vector3(sailor ? .643644f : 0,y,z);
            if(!sailor&&body.Boundary(focus,Vector3.right,out var found))focus=found;
            float patchRadius=sailor ? .12f : .14f;
            foreach(var sample in samples)
            {
                Vector3 deformed=HatCrownVariantAuthoring.Deform(sample.point,opening,1,1,previous,anchor);
                Vector3 world=rotation*deformed*fit.scale+fit.position;
                Vector3 target=focus;
                if(world.x<0)target.x=-target.x;
                if((world-target).sqrMagnitude>patchRadius*patchRadius)continue;
                Vector3 outward=world.x>=0?Vector3.right:Vector3.left;
                if(!body.Boundary(world,outward,out var hit))continue;
                float depth=Vector3.Dot(hit-world,outward);
                if(depth<=.001f||depth>.17f)continue;
                contacts++;deepest=Mathf.Max(deepest,depth);
                // Keep the silhouette height and cord shape. This patch needs
                // only a small sideways wrap over the visible horn/ear surface.
                Vector3 displacement=inverse*(outward*(depth+.018f))/fit.scale;
                displacement=Vector3.ClampMagnitude(displacement,.03f);
                string key=(world.x<0?"L":"R")+"/"+Mathf.RoundToInt(sample.point.y/.025f)+"/"+Mathf.RoundToInt(sample.point.z/.025f);
                var control=new HatCrownVariantAuthoring.Control{center=sample.point,displacement=displacement,
                    radius=.085f,verticalRadius=.07f,pass=pass};
                if(!selected.TryGetValue(key,out var old)||old.displacement.sqrMagnitude<displacement.sqrMagnitude)selected[key]=control;
            }
            return selected.Values.OrderByDescending(c=>c.displacement.sqrMagnitude).Take(24).ToArray();
        }

        static HatCrownVariantAuthoring.Control[] PhysicalSculptControls(ClothSample[] samples,float eyes,CourierEquipmentFit fit,
            OpeningDescriptor opening,Surface body,HatCrownVariantAuthoring.Control[] previous,float anchor,int pass,
            out int contacts,out float deepest)
        {
            var rotation=Quaternion.Euler(fit.euler);var inverse=Quaternion.Inverse(rotation);
            var selected=new Dictionary<string,HatCrownVariantAuthoring.Control>();contacts=0;deepest=0;
            foreach(var sample in samples)
            {
                Vector3 deformed=HatCrownVariantAuthoring.Deform(sample.point,opening,1,1,previous,anchor);
                Vector3 world=rotation*deformed*fit.scale+fit.position;
                if(world.y<eyes+.045f)continue;
                Vector3 normal=rotation*HatCrownVariantAuthoring.Jacobian(sample.point,opening,1,1,previous,anchor)
                    .inverse.transpose.MultiplyVector(sample.normal).normalized;
                if(!body.Boundary(world,normal,out var hit))continue;
                float depth=Vector3.Dot(hit-world,normal);
                if(depth<=.001f||depth>.22f)continue;
                contacts++;deepest=Mathf.Max(deepest,depth);
                Vector3 displacement=inverse*(hit+normal*.022f-world)/fit.scale;
                displacement=Vector3.ClampMagnitude(displacement,.045f);
                float angle=Mathf.Atan2(sample.point.z-opening.center.z,sample.point.x-opening.center.x);
                string key=Mathf.RoundToInt(angle*32/Mathf.PI)+"/"+Mathf.RoundToInt(sample.point.y/.035f);
                var control=new HatCrownVariantAuthoring.Control{center=sample.point,displacement=displacement,
                    radius=.14f,verticalRadius=.11f,pass=pass};
                if(!selected.TryGetValue(key,out var old)||old.displacement.sqrMagnitude<displacement.sqrMagnitude)selected[key]=control;
            }
            return selected.Values.OrderByDescending(c=>c.displacement.sqrMagnitude).Take(48).ToArray();
        }

        static bool RequiresSideWallFit(string hero,string hat)
        {
            // The all-80 native review identified these closed cuffs. Restrict
            // this additional constraint to the confirmed cases, preserving
            // already accepted open helmets, diadems and other wearing poses.
            return hero=="Piko"&&(hat=="hat_AmberBeret"||hat=="hat_NavigatorCap"||hat=="hat_SailorBeret") ||
                hero=="Miro"&&hat=="hat_AmberBeret" ||
                (hero=="Vela"||hero=="Soli"||hero=="Fira")&&hat=="hat_NavigatorCap";
        }

        static void MeasureSideWalls(Contacts contact,Vector3[] body,float eyes,CourierMountProfile profile,
            CourierEquipmentFit fit,OpeningDescriptor opening,Surface surface)
        {
            contact.sideEnforced=true;contact.sideSamples=contact.sideMisses=contact.sideOutside=0;
            contact.sideOverhang=0;contact.featureAboveOpening=float.NegativeInfinity;
            var inverse=Quaternion.Inverse(Quaternion.Euler(fit.euler));
            foreach(var point in UpperHeadSamples(body,eyes).Where(p=>Mathf.Abs(p.x)>profile.bodyWidth*.30f))
            {
                Vector3 relative=point-fit.position;
                float above=((inverse*relative).y/fit.scale-opening.referenceHeight)*fit.scale;
                contact.featureAboveOpening=Mathf.Max(contact.featureAboveOpening,above);
                if(above<=.025f)continue; // A feature below the tilted lower edge can remain visible.
                contact.sideSamples++;
                bool xHit=surface.HorizontalSpan(relative,true,out float x0,out float x1);
                bool zHit=surface.HorizontalSpan(relative,false,out float z0,out float z1);
                if(!xHit||!zHit){contact.sideMisses++;continue;}
                float overhang=Mathf.Max(x0+.012f-relative.x,relative.x-(x1-.012f),
                    z0+.012f-relative.z,relative.z-(z1-.012f));
                if(overhang>.002f){contact.sideOutside++;contact.sideOverhang=Mathf.Max(contact.sideOverhang,overhang);}
            }
            if(float.IsNegativeInfinity(contact.featureAboveOpening))contact.featureAboveOpening=0;
        }

        static void MeasureRoof(Contacts contact,string id,Vector3[] body,HeadSection head,float eyes,CourierMountProfile profile,
            CourierEquipmentFit fit,OpeningDescriptor opening,Surface surface)
        {
            if(id=="hat_SignalDiadem")return;
            contact.roofSamples=contact.roofMisses=contact.lateralSamples=0;
            contact.roofPenetration=contact.lateralPenetration=0;
            float rim=fit.position.y+(Quaternion.Euler(fit.euler)*opening.center*fit.scale).y;
            foreach(var point in UpperHeadSamples(body,eyes).Where(p=>p.y>rim+.035f&&
                (!contact.openHelmetFace||p.z<=head.center.z+head.depth*.05f)))
                if(surface.Height(point.x-fit.position.x,point.z-fit.position.z,true,out float roof))
                {
                    contact.roofSamples++;
                    float penetration=point.y-(roof+fit.position.y);
                    contact.roofPenetration=Mathf.Max(contact.roofPenetration,penetration);
                    if(Mathf.Abs(point.x)>profile.bodyWidth*.43f)
                    {contact.lateralSamples++;contact.lateralPenetration=Mathf.Max(contact.lateralPenetration,penetration);}
                }
                else contact.roofMisses++;
        }

        static IEnumerable<Vector3> UpperHeadSamples(Vector3[] body,float eyes)
        {
            var upper=body.Where(p=>p.y>eyes+.10f).ToArray();
            // Keep a regular surface sample and each side's highest feature.
            // A single global peak misses a shorter ear on an asymmetric head.
            return upper.Where((p,i)=>i%12==0)
                .Concat(upper.Where(p=>p.x<0).OrderByDescending(p=>p.y).Take(16))
                .Concat(upper.Where(p=>p.x>=0).OrderByDescending(p=>p.y).Take(16));
        }

        static IEnumerable<Vector3> SupportPoints(HeadSection head)
        {
            // Sample a broad annulus rather than the central tuft. A peak touching
            // one hat vertex is not evidence of a worn cap.
            foreach(float radius in new[]{.28f,.40f})
                for(int i=0;i<20;i++)
                {
                    float angle=i*Mathf.PI*2/20;
                    yield return new Vector3(Mathf.Cos(angle)*head.width*radius,0,
                        head.center.z+Mathf.Sin(angle)*head.depth*radius);
                }
        }
        static HeadSection ReadHead(Vector3[] body, CourierMountProfile profile, float eyes)
        {
            var baseline=body.Where(p=>Mathf.Abs(p.y-(eyes+.10f))<.035f).ToArray();
            if(baseline.Length<20)baseline=body.Where(p=>p.y>eyes&&p.y<eyes+.2f).ToArray();
            float centerZ=(Quantile(baseline.Select(p=>p.z),.03f)+Quantile(baseline.Select(p=>p.z),.97f))*.5f;
            float depth=Quantile(baseline.Select(p=>p.z),.97f)-Quantile(baseline.Select(p=>p.z),.03f);
            float baselineWidth=FrontWidth(baseline,centerZ+depth*.1f,profile.bodyWidth);
            float height=eyes+.18f, previous=baselineWidth;
            for(float y=eyes+.10f;y<=Mathf.Min(profile.bodyHeight-.06f,eyes+.34f);y+=.02f)
            {
                var band=body.Where(p=>Mathf.Abs(p.y-y)<.025f).ToArray();
                float width=FrontWidth(band,centerZ+depth*.1f,profile.bodyWidth);
                // Lateral crests/ears cannot reinflate the cranial envelope.
                previous=Mathf.Min(previous,width);
                if(previous<baselineWidth*.83f)break;
                height=y;
            }
            var section=body.Where(p=>Mathf.Abs(p.y-height)<.035f && p.z>centerZ-depth*.12f &&
                Mathf.Abs(p.x)<Mathf.Min(baselineWidth*.60f,profile.bodyWidth*.43f)).ToArray();
            float sectionWidth=Quantile(section.Select(p=>p.x),.98f)-Quantile(section.Select(p=>p.x),.02f);
            var full=body.Where(p=>Mathf.Abs(p.y-height)<.035f&&Mathf.Abs(p.x)<Mathf.Max(.1f,sectionWidth*.45f)).ToArray();
            float z0=Quantile(full.Select(p=>p.z),.025f),z1=Quantile(full.Select(p=>p.z),.975f);
            return new HeadSection { width=Mathf.Clamp(sectionWidth,.35f,baselineWidth*1.12f),
                depth=Mathf.Clamp(z1-z0,.3f,depth*1.10f),height=height,center=new Vector3(0,height,(z0+z1)*.5f) };
        }
        static float FrontWidth(Vector3[] band,float z,float bodyWidth)
        {
            var a=band.Where(p=>p.z>z&&Mathf.Abs(p.x)<bodyWidth*.43f).Select(p=>p.x).ToArray();
            return a.Length>8 ? Quantile(a,.97f)-Quantile(a,.03f) : bodyWidth*.70f;
        }
        static float Quantile(IEnumerable<float> values,float q)
        {var a=values.OrderBy(x=>x).ToArray();return a.Length>0?a[Mathf.Clamp(Mathf.RoundToInt((a.Length-1)*q),0,a.Length-1)]:0;}
        static void ContactEvidence(FitObservation observation, Contacts contact, HeadSection head, OpeningDescriptor opening, CourierEquipmentFit fit)
        {
            observation.supportTargetHeight=head.height;
            observation.rimHeight=fit.position.y+(Quaternion.Euler(fit.euler)*opening.center*fit.scale).y;
            observation.measuredOpeningWidth=opening.width*fit.scale;observation.measuredOpeningDepth=opening.depth*fit.scale;
            observation.supportSamples=contact.gaps.Count;observation.eyeLift=contact.eyeLift;
            observation.requiredRoofLift=contact.roofLift;observation.roofSamples=contact.roofSamples;observation.roofMisses=contact.roofMisses;observation.maximumHeadRoofPenetration=contact.roofPenetration;
            observation.lateralFeatureSamples=contact.lateralSamples;observation.maximumLateralFeaturePenetration=contact.lateralPenetration;
            observation.sideWallEnforced=contact.sideEnforced;observation.sideWallSamples=contact.sideSamples;
            observation.openHelmetFace=contact.openHelmetFace;
            observation.sideWallMisses=contact.sideMisses;observation.sideWallOutside=contact.sideOutside;
            observation.sideWallLift=contact.sideLift;observation.maximumSideWallOverhang=contact.sideOverhang;
            observation.maximumFeatureAboveOpening=contact.featureAboveOpening;
            observation.sideWallCoverage=contact.sideSamples>0 ? (contact.sideSamples-contact.sideMisses)/(float)contact.sideSamples : 1;
            observation.crownHeightScale=contact.crownHeight;observation.crownRadialScale=contact.crownRadius;
            observation.localSculptControls=contact.sculptControls;observation.maximumLocalSculpt=contact.sculptMaximum;
            observation.minimumSculptJacobian=contact.sculptJacobian;
            observation.physicalClothContacts=contact.physicalContacts;observation.maximumPhysicalContactDepth=contact.physicalDepth;
            observation.physicalContactScope=contact.physicalScope;
            observation.minimumSupportGap=contact.gaps.Count>0?contact.gaps.Min():1;
            observation.maximumSupportGap=contact.gaps.Count>0?contact.gaps.Max():1;
            observation.medianSupportGap=contact.gaps.Count>0?Quantile(contact.gaps,.5f):1;
            observation.supportedFraction=contact.gaps.Count>0?contact.gaps.Count(x=>Mathf.Abs(x)<.09f)/(float)contact.gaps.Count:0;
            // Rear/dome values remain diagnostics. Old guards inferred contact from
            // highest vertices and forced hats to float above narrow crests.
            observation.passed=fit.scale>.1f&&observation.eyeClearance>=.035f&&contact.gaps.Count>=12&&
                observation.medianSupportGap<=.065f&&observation.supportedFraction>=.25f&&contact.roofMisses==0&&contact.roofPenetration<=.005f&&
                contact.sideMisses==0&&contact.sideOutside==0;
        }
        sealed class Surface : IDisposable
        {
            readonly Transform frame;readonly GameObject node;readonly Mesh mesh;readonly MeshCollider collider;
            public Surface(Transform frame,List<Vector3> vertices,List<int> triangles)
            {
                this.frame=frame;mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
                node=new GameObject("Disposable hat fit surface");node.transform.SetParent(frame,false);
                collider=node.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
            }
            public bool Height(float x,float z,bool fromTop,out float height)
            {
                var origin=frame.TransformPoint(new Vector3(x,fromTop?6:-4,z));
                var direction=frame.TransformDirection(fromTop?Vector3.down:Vector3.up).normalized;
                if(collider.Raycast(new Ray(origin,direction),out var hit,20*frame.lossyScale.magnitude))
                {height=frame.InverseTransformPoint(hit.point).y;return true;}
                height=0;return false;
            }
            public bool HorizontalSpan(Vector3 point,bool xAxis,out float low,out float high)
            {
                Vector3 axis=xAxis?Vector3.right:Vector3.forward;
                Vector3 start=point-axis*6,end=point+axis*6;
                var direction=frame.TransformDirection(axis).normalized;
                bool a=collider.Raycast(new Ray(frame.TransformPoint(start),direction),out var left,20*frame.lossyScale.magnitude);
                bool b=collider.Raycast(new Ray(frame.TransformPoint(end),-direction),out var right,20*frame.lossyScale.magnitude);
                if(a&&b)
                {
                    var p0=frame.InverseTransformPoint(left.point);var p1=frame.InverseTransformPoint(right.point);
                    low=xAxis?p0.x:p0.z;high=xAxis?p1.x:p1.z;
                    return high>low+.001f;
                }
                low=high=0;return false;
            }
            public bool Boundary(Vector3 center,Vector3 direction,out Vector3 boundary)
            {
                direction.Normalize();
                var origin=frame.TransformPoint(center+direction*6);
                var inward=frame.TransformDirection(-direction).normalized;
                if(collider.Raycast(new Ray(origin,inward),out var hit,20*frame.lossyScale.magnitude))
                {boundary=frame.InverseTransformPoint(hit.point);return Vector3.Dot(boundary-center,direction)>0;}
                boundary=Vector3.zero;return false;
            }
            public void Dispose(){UnityEngine.Object.DestroyImmediate(node);UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static Surface BodySurface(GameObject actor,Transform frame)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(skin.GetComponentInParent<CourierEquipmentRig>()!=null)continue;
                var scratch=new Mesh();try
                {
                    skin.BakeMesh(scratch,true);int start=vertices.Count;
                    var matrix=frame.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                    vertices.AddRange(scratch.vertices.Select(matrix.MultiplyPoint3x4));
                    triangles.AddRange(scratch.triangles.Select(t=>t+start));
                }finally{UnityEngine.Object.DestroyImmediate(scratch);}
            }
            return new Surface(frame,vertices,triangles);
        }
        static Surface EquipmentSurface(GameObject prefab,Transform frame,Quaternion rotation,float scale,Func<Vector3,Vector3> deform=null)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;int start=vertices.Count;
                var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                vertices.AddRange(filter.sharedMesh.vertices.Select(p=>
                {
                    var point=matrix.MultiplyPoint3x4(p);
                    return rotation*(deform!=null?deform(point):point)*scale;
                }));
                triangles.AddRange(filter.sharedMesh.triangles.Select(t=>t+start));
            }
            return new Surface(frame,vertices,triangles);
        }

        static FitObservation Observe(string hero, CourierEquipmentDefinition hat, CourierEquipmentFit fit, Vector3[] points,
            Vector3[] body, float crown, float eyes, float rear, float roof, float before, HeadSection section)
        {
            Quaternion rotation = Quaternion.Euler(fit.euler);
            Vector3[] transformed = points.Select(p => rotation * p * fit.scale + fit.position).ToArray();
            // Front check uses the central forehead band, away from hanging earflaps.
            float front = transformed.Where(p => Mathf.Abs(p.x) < section.width*.23f && p.z > section.center.z+section.depth*.30f).Select(p => p.y).DefaultIfEmpty(eyes + .12f).Min();
            // Horizontal gap is only required below the case roof; higher parts
            // can naturally overhang the back of the head.
            float gap = transformed.Where(p => p.y < roof + .04f && Mathf.Abs(p.x) < .32f).Select(p => p.z - rear).DefaultIfEmpty(1).Min();
            // Check that the crown really enters the cap's opening. Height alone
            // can pass for a cap hovering behind a tapered head or tall crest.
            float halfWidth = (transformed.Max(p => p.x) - transformed.Min(p => p.x)) * .26f;
            float halfDepth = (transformed.Max(p => p.z) - transformed.Min(p => p.z)) * .26f;
            float openingLevel = OpeningLevel(hat.id, points, rotation) * fit.scale + fit.position.y;
            float scalp = body.Where(p => Mathf.Abs(p.x - fit.position.x) < halfWidth && Mathf.Abs(p.z - fit.position.z) < halfDepth)
                .Select(p => p.y).DefaultIfEmpty(float.NegativeInfinity).Max();
            float overlap = scalp - openingLevel;
            float dome = transformed.Where(p => Mathf.Abs(p.x - fit.position.x) < halfWidth && Mathf.Abs(p.z - fit.position.z) < halfDepth)
                .Select(p => p.y).DefaultIfEmpty(openingLevel).Max() - scalp;
            return new FitObservation { hero = hero, hat = hat.id, scale = fit.scale, previousScale = before, crownWidth = crown,
                eyeTop = eyes, eyeClearance = front - eyes, rearClearance = gap, backpackRoof = roof, headSeatingOverlap = overlap, domeClearance = dome, position = fit.position,
                passed = front >= eyes + .045f && gap >= -.01f && overlap >= .025f && overlap <= .48f && (hat.id == "hat_SignalDiadem" || dome >= -.015f) && fit.scale > .1f };
        }

        static float EyeTop(string hero)
        {
            var meta = JsonUtility.FromJson<BodyMeta>(File.ReadAllText(Path.Combine(Source, hero, "Meta.json")));
            return meta.blink?.eyes != null && meta.blink.eyes.Length > 0 ? meta.blink.eyes.Max(e => e.center[2] + e.radiusZ) : 1.55f;
        }
        static float OpeningLevel(string id, Vector3[] points, Quaternion rotation)
        {
            float level = id == "hat_AviatorCap" ? .55f : id == "hat_ExplorerHat" ? .06f :
                id == "hat_NavigatorCap" ? .10f : id == "hat_AmberBeret" ? .10f :
                id == "hat_SailorBeret" ? .05f : id == "hat_SignalDiadem" ? .40f :
                id == "hat_SignalBeanie" ? .20f : .03f;
            float[] ys = points.Where(p => Mathf.Abs(p.y - level) < .025f).Select(p => (rotation * p).y).OrderBy(y => y).ToArray();
            return ys.Length > 0 ? Percentile(ys, .5f) : level;
        }
        static float Percentile(float[] sorted, float t) => sorted[Mathf.Clamp(Mathf.RoundToInt((sorted.Length - 1) * t), 0, sorted.Length - 1)];
        static float BackpackRoof(CourierMountProfile profile, CourierCatalog catalog)
        {
            float roof = 0;
            foreach (var item in catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Backpack))
            {
                var rig = item.prefab.GetComponent<CourierEquipmentRig>(); var fit = profile.FindFit(item.id);
                if (rig != null && fit != null) roof = Mathf.Max(roof, fit.position.y + rig.caseBoundsMax.y * fit.scale);
            }
            return roof;
        }
        static float RearContact(CourierMountProfile profile, CourierCatalog catalog)
        {
            // The head tapers above the shoulders. Its upper back section is not
            // the front of the backpack: comparing hats against that section
            // falsely rejected correctly worn brims on Boro, Soli and Piko.
            float front = float.NegativeInfinity;
            foreach (var item in catalog.equipment.Where(e => e.kind == CourierEquipmentKind.Backpack))
            {
                var rig = item.prefab.GetComponent<CourierEquipmentRig>(); var fit = profile.FindFit(item.id);
                if (rig == null || fit == null) continue;
                var contact = Quaternion.Euler(fit.euler) * new Vector3(0, 0, rig.caseContactZ) * fit.scale + fit.position;
                front = Mathf.Max(front, contact.z);
            }
            if (!float.IsNegativeInfinity(front)) return front;
            var sections = profile.bodySections;
            return sections != null && sections.Length > 0 ? sections.OrderBy(s => Mathf.Abs(s.height - profile.shoulderHeight)).First().backZ : -profile.bodyDepth * .48f;
        }
        static Vector3[] BodyPoints(GameObject actor, Transform frame)
        {
            var points = new List<Vector3>();
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                try { skin.BakeMesh(mesh, true); foreach (var p in mesh.vertices) points.Add(frame.InverseTransformPoint(skin.transform.TransformPoint(p))); }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            return points.ToArray();
        }
        static Vector3[] EquipmentPoints(GameObject prefab)
        {
            var points = new List<Vector3>();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    foreach (var p in filter.sharedMesh.vertices) points.Add(prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(p)));
            if (points.Count == 0) throw new InvalidOperationException(prefab.name + ": supplied hat has no readable vertices.");
            return points.ToArray();
        }
    }
}
