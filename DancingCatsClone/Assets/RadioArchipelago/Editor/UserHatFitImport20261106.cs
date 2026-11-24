using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Imports the pinned user export and checks production attachment against its exact XYZ transforms.</summary>
    public static class UserHatFitImport20261106
    {
        static string Folder => Path.GetFullPath("../Docs/Design/RadioArchipelago/HatFittingWorkbench/Applied20261106");
        static bool HasCompleteExport => File.Exists(Path.Combine(Folder,"complete-source.json"));
        static string Input => Path.Combine(Folder,HasCompleteExport?"user-export-complete.json":"user-export.json");
        static CourierCatalog Catalog => AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
        [Serializable] sealed class Source { public string originalExport,sha256; }
        [Serializable] sealed class PreflightReport
        {
            public bool passed,scenePresent;
            public int pairs,customizedChildPairs;
            public float sceneRootError;
            public List<string> uncapturedSceneChanges=new List<string>();
            public List<string> customizedPairs=new List<string>();
        }
        [Serializable] sealed class Pair
        {
            public string hero,hat;
            public Vector3 requestedAxes,storedAxes;
            public float storedError,runtimeMatrixError,childTransformError,rendererMatrixError;
            public bool originalMeshReferencesPreserved,originalMaterialsPreserved;
            public string geometryPrefab;
        }
        [Serializable] sealed class VerifyReport
        {
            public bool passed,activeScenePreserved;
            public string sourceExport;
            public int pairs,nonUniformPairs,authoredVariants;
            public float maximumStoredError,maximumRuntimeError,maximumChildError,maximumRendererError;
            public List<Pair> observations=new List<Pair>();
        }

        static HatFittingWorkbench.Snapshot ReadPinned()
        {
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Folder,HasCompleteExport?"complete-source.json":"import-source.json")));
            using(var sha=SHA256.Create())
            {
                string actual=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Input))).Replace("-","").ToLowerInvariant();
                if(actual!=source.sha256)throw new InvalidDataException("Pinned user export changed after inspection.");
            }
            return JsonUtility.FromJson<HatFittingWorkbench.Snapshot>(File.ReadAllText(Input));
        }

        public static void Preflight()
        {
            EnsureEditMode();var saved=ReadPinned();var report=new PreflightReport{pairs=saved.hats.Length};
            var scene=SceneManager.GetActiveScene();
            if(scene.path==HatFittingWorkbench.ScenePath)
            {
                report.scenePresent=true;
                var current=HatFittingWorkbench.CaptureCurrent().hats.ToDictionary(p=>p.hero+"/"+p.hat);
                var markers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                    .Where(t=>t.name.StartsWith("__EditableHatId__",StringComparison.Ordinal)).ToArray();
                foreach(var pose in saved.hats)
                {
                    string key=pose.hero+"/"+pose.hat;
                    var live=current[key];report.sceneRootError=Mathf.Max(report.sceneRootError,Error(Matrix(pose),Matrix(live)));
                    var marker=markers.Single(m=>m.name=="__EditableHatId__"+pose.hat&&
                        m.GetComponentInParent<CourierWardrobeRig>().GetComponentsInChildren<Transform>(true).Any(t=>t.name=="__WorkbenchHero__"+pose.hero));
                    var edited=marker.parent;var original=AssetDatabase.LoadAssetAtPath<GameObject>(pose.sourceHatPrefab);
                    bool customized=false;
                    foreach(var child in original.GetComponentsInChildren<Transform>(true))
                    {
                        if(child==original.transform)continue;
                        string path=AnimationUtility.CalculateTransformPath(child,original.transform);
                        var actual=edited.Find(path);
                        var exported=pose.children?.SingleOrDefault(c=>c.path==path);
                        var requested=exported==null?Local(child):Matrix4x4.TRS(exported.position,exported.rotation,exported.scale);
                        customized |= Error(Local(child),requested)>.0001f;
                        if(actual==null||Error(requested,Local(actual))>.0001f)report.uncapturedSceneChanges.Add(key+"/"+path);
                    }
                    if(customized){report.customizedChildPairs++;report.customizedPairs.Add(key);}
                    var actor=marker.GetComponentInParent<CourierWardrobeRig>();
                    var sourceActor=Catalog.entries.Single(h=>h.assetId==pose.hero).prefab;
                    var sourceRig=sourceActor.GetComponent<CourierWardrobeRig>();
                    if(Vector3.Distance(actor.transform.localScale,sourceActor.transform.localScale)>.0001f||
                        Vector3.Distance(actor.bodyRig.visualPivot.localScale,sourceRig.bodyRig.visualPivot.localScale)>.0001f)
                        report.uncapturedSceneChanges.Add(key+"/actor-or-visual-scale");
                }
            }
            report.passed=report.pairs==70&&report.sceneRootError<.0001f&&report.uncapturedSceneChanges.Count==0;
            File.WriteAllText(Path.Combine(Folder,"preflight.json"),JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidDataException("Some edited scene transforms are not represented in the exported hat roots. See preflight.json.");
            Debug.Log("USER_HAT_PREFLIGHT PASS: exact export, no uncaptured child/actor scale changes.");
        }

        public static void Apply()
        {
            Preflight();ReadPinned();HatFittingWorkbench.ApplyExportToGame(Input);Verify();
        }

        public static void Verify()
        {
            EnsureEditMode();
            if(!HasCompleteExport)throw new InvalidDataException("Production verification requires the recovered complete hierarchy export.");
            var saved=ReadPinned();
            foreach(var pose in saved.hats)
            {
                var item=Catalog.equipment.Single(e=>e.id==pose.hat);
                var expected=item.prefab.GetComponentsInChildren<Transform>(true).Where(t=>t!=item.prefab.transform)
                    .Select(t=>AnimationUtility.CalculateTransformPath(t,item.prefab.transform)).ToHashSet(StringComparer.Ordinal);
                if(pose.children==null||pose.children.Length!=expected.Count||!expected.SetEquals(pose.children.Select(c=>c.path)))
                    throw new InvalidDataException("Complete child transforms missing for "+pose.hero+"/"+pose.hat);
            }
            var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;
            var preview=EditorSceneManager.NewPreviewScene();
            var report=new VerifyReport{sourceExport=Input};
            try
            {
                foreach(var hero in Catalog.entries)
                {
                    var actor=UnityEngine.Object.Instantiate(hero.prefab);SceneManager.MoveGameObjectToScene(actor,preview);
                    try
                    {
                        var wardrobe=actor.GetComponent<CourierWardrobeRig>();wardrobe.motion.ManualSimulation=true;wardrobe.motion.ResetMotion();
                        foreach(var pose in saved.hats.Where(p=>p.hero==hero.assetId))
                        {
                            var item=Catalog.equipment.Single(e=>e.id==pose.hat);var fit=wardrobe.profile.FindFit(pose.hat);
                            var row=new Pair{hero=pose.hero,hat=pose.hat,requestedAxes=pose.axisScale,storedAxes=fit.axisScale,
                                geometryPrefab=AssetDatabase.GetAssetPath(fit.prefabOverride),originalMeshReferencesPreserved=true,originalMaterialsPreserved=true};
                            row.storedError=Mathf.Max(Vector3.Distance(fit.position,pose.position),Vector3.Distance(fit.euler,pose.euler),
                                Vector3.Distance(fit.axisScale,pose.axisScale),Mathf.Abs(fit.scale-pose.scale));
                            wardrobe.Apply(null,item,null);var hat=wardrobe.Equipped(CourierEquipmentKind.Hat);
                            var actual=wardrobe.bodyRig.visualPivot.worldToLocalMatrix*hat.transform.localToWorldMatrix;
                            row.runtimeMatrixError=Error(actual,Matrix(pose));report.observations.Add(row);
                            foreach(var child in pose.children??Array.Empty<HatFittingWorkbench.ChildPose>())
                            {
                                var actualChild=hat.transform.Find(child.path);
                                var sourceChild=item.prefab.transform.Find(child.path);
                                if(actualChild==null||sourceChild==null)throw new InvalidDataException("Lost imported child: "+pose.hero+"/"+pose.hat+"/"+child.path);
                                row.childTransformError=Mathf.Max(row.childTransformError,Error(Local(actualChild),Matrix4x4.TRS(child.position,child.rotation,child.scale)));
                                var requested=Matrix(pose);
                                string path="";
                                foreach(string segment in child.path.Split('/'))
                                {
                                    path=path.Length==0?segment:path+"/"+segment;
                                    var part=pose.children.Single(c=>c.path==path);
                                    requested*=Matrix4x4.TRS(part.position,part.rotation,part.scale);
                                }
                                var rendererFrame=wardrobe.bodyRig.visualPivot.worldToLocalMatrix*actualChild.localToWorldMatrix;
                                row.rendererMatrixError=Mathf.Max(row.rendererMatrixError,Error(rendererFrame,requested));
                                var sourceFilter=sourceChild.GetComponent<MeshFilter>();var actualFilter=actualChild.GetComponent<MeshFilter>();
                                if(sourceFilter!=null)row.originalMeshReferencesPreserved &= actualFilter!=null&&actualFilter.sharedMesh==sourceFilter.sharedMesh;
                                var sourceSkin=sourceChild.GetComponent<SkinnedMeshRenderer>();var actualSkin=actualChild.GetComponent<SkinnedMeshRenderer>();
                                if(sourceSkin!=null)row.originalMeshReferencesPreserved &= actualSkin!=null&&actualSkin.sharedMesh==sourceSkin.sharedMesh;
                                var sourceRenderer=sourceChild.GetComponent<Renderer>();var actualRenderer=actualChild.GetComponent<Renderer>();
                                if(sourceRenderer!=null)row.originalMaterialsPreserved &= actualRenderer!=null&&sourceRenderer.sharedMaterials.SequenceEqual(actualRenderer.sharedMaterials);
                            }
                            if(fit.prefabOverride!=null)report.authoredVariants++;
                            report.maximumStoredError=Mathf.Max(report.maximumStoredError,row.storedError);
                            report.maximumRuntimeError=Mathf.Max(report.maximumRuntimeError,row.runtimeMatrixError);
                            report.maximumChildError=Mathf.Max(report.maximumChildError,row.childTransformError);
                            report.maximumRendererError=Mathf.Max(report.maximumRendererError,row.rendererMatrixError);
                            if(Mathf.Max(pose.axisScale.x,pose.axisScale.y,pose.axisScale.z)-Mathf.Min(pose.axisScale.x,pose.axisScale.y,pose.axisScale.z)>.00001f)report.nonUniformPairs++;
                        }
                    }
                    finally{UnityEngine.Object.DestroyImmediate(actor);}
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            report.pairs=report.observations.Count;report.activeScenePreserved=SceneManager.GetActiveScene()==active&&active.isDirty==dirty;
            report.passed=report.pairs==70&&report.maximumStoredError<.000001f&&report.maximumRuntimeError<.0001f&&
                report.maximumChildError<.0001f&&report.maximumRendererError<.001f&&
                report.observations.All(p=>p.originalMeshReferencesPreserved&&p.originalMaterialsPreserved)&&report.activeScenePreserved;
            File.WriteAllText(Path.Combine(Folder,"runtime-verification.json"),JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidDataException("Imported user hat poses failed exact production XYZ verification.");
            Debug.Log("USER_HAT_FITS PASS: "+report.pairs+" exact saved fits; runtime matrix maxerror="+report.maximumRuntimeError+"; independent XYZ retained.");
        }

        static Matrix4x4 Matrix(HatFittingWorkbench.HatPose pose)=>Matrix4x4.TRS(pose.position,Quaternion.Euler(pose.euler),pose.localScale);
        static Matrix4x4 Local(Transform t)=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale);
        static float Error(Matrix4x4 a,Matrix4x4 b){float error=0;for(int i=0;i<16;i++)error=Mathf.Max(error,Mathf.Abs(a[i]-b[i]));return error;}
        static void EnsureEditMode(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Import user hats in Edit Mode.");}
    }
}
