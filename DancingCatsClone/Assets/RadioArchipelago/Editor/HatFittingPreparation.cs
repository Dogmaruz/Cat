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
    /// <summary>Preserves game poses while a separate human-authored fitting reference is being prepared.</summary>
    public static class HatFittingPreparation
    {
        const string CatalogPath="Assets/RadioArchipelago/Content/CourierCatalog.asset";
        static string Evidence=>Path.GetFullPath("../Docs/Design/RadioArchipelago/WardrobePolish20261105");
        [Serializable] sealed class Pose
        {
            public string hero,hat,geometryGuid;
            public Vector3 position,euler;
            public float scale;
            public long geometryFileId;
        }
        [Serializable] sealed class Poses { public Pose[] poses; }
        [Serializable] sealed class AxisReport { public bool passed,activeScenePreserved; public float xyzError,legacyError; public Vector3 requested; }
        [Serializable] sealed class SceneReport { public bool passed,heroSelectionSynchronized; public int beforePairs,afterPairs; public float maximumTransformError; public string scene; }

        public static void ReopenAndVerify()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=HatFittingWorkbench.ScenePath||scene.isDirty)
                throw new InvalidOperationException("Reopen verification requires the saved fitting scene, with no unsaved user edits.");
            string scenePath=scene.path;
            var before=HatFittingWorkbench.CaptureCurrent();
            EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            bool selectionMatched=true;
            foreach(string hero in new[]{"Nori","Fira"})
            {
                HatFittingWorkbench.SelectHero(hero);
                var selected=Selection.activeGameObject;
                var actor=selected!=null?selected.GetComponentInParent<CourierWardrobeRig>():null;
                selectionMatched &= selected!=null&&selected.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("__EditableHatId__",StringComparison.Ordinal))&&
                    actor!=null&&actor.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="__WorkbenchHero__"+hero);
            }
            var after=HatFittingWorkbench.CaptureCurrent();
            var report=new SceneReport{scene=scenePath,beforePairs=before.hats.Length,afterPairs=after.hats.Length,heroSelectionSynchronized=selectionMatched};
            var rows=after.hats.ToDictionary(p=>p.hero+"/"+p.hat);
            foreach(var pose in before.hats)
            {
                if(!rows.TryGetValue(pose.hero+"/"+pose.hat,out var restored))throw new InvalidDataException("A fitting station was lost on reopening.");
                var first=Matrix4x4.TRS(pose.position,pose.rotation,pose.axisScale);
                var second=Matrix4x4.TRS(restored.position,restored.rotation,restored.axisScale);
                for(int i=0;i<16;i++)report.maximumTransformError=Mathf.Max(report.maximumTransformError,Mathf.Abs(first[i]-second[i]));
            }
            report.passed=report.beforePairs==70&&report.afterPairs==70&&report.maximumTransformError<.001f&&selectionMatched;
            Directory.CreateDirectory(Evidence);File.WriteAllText(Path.Combine(Evidence,"hat-fitting-scene-reopen.json"),JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidDataException("The fitting scene did not retain all70 poses on disk.");
            HatFittingWorkbench.FrameStation("Fira",before.hats.First(p=>p.hero=="Fira").hat);
        }

        public static void RestorePriorGameHats()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Restore game poses in Edit Mode.");
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            var saved=JsonUtility.FromJson<Poses>(File.ReadAllText(Path.Combine(Evidence,"prior-game-hat-poses.json")));
            if(saved?.poses==null||saved.poses.Length!=70)throw new InvalidDataException("Expected the70 preserved active hat poses.");
            var actors=new List<(string path,GameObject root,CourierWardrobeRig wardrobe)>();
            var references=new Dictionary<string,GameObject>();
            try
            {
                foreach(var pose in saved.poses)
                {
                    GameObject source=null;
                    if(!string.IsNullOrEmpty(pose.geometryGuid))
                    {
                        source=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(pose.geometryGuid));
                        if(source==null||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId)||guid!=pose.geometryGuid||localId!=pose.geometryFileId)
                            throw new InvalidDataException("A preserved hat geometry reference changed: "+pose.hero+"/"+pose.hat);
                    }
                    references[pose.hero+"/"+pose.hat]=source;
                }
                foreach(var hero in catalog.entries)
                {
                    string path=AssetDatabase.GetAssetPath(hero.prefab);var root=PrefabUtility.LoadPrefabContents(path);
                    var wardrobe=root.GetComponent<CourierWardrobeRig>();actors.Add((path,root,wardrobe));
                    foreach(var pose in saved.poses.Where(p=>p.hero==hero.assetId))
                    {
                        var fit=wardrobe.profile.FindFit(pose.hat);
                        if(fit==null)throw new InvalidDataException("Missing preserved hat slot: "+hero.assetId+"/"+pose.hat);
                        fit.position=pose.position;fit.euler=pose.euler;fit.scale=pose.scale;fit.axisScale=Vector3.one;
                        fit.prefabOverride=references[pose.hero+"/"+pose.hat];
                    }
                }
                foreach(var actor in actors)PrefabUtility.SaveAsPrefabAsset(actor.root,actor.path);
                AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(Evidence,"game-hats-retained.txt"),
                    "Restored70priorgamehatposes only. Backpacks, scarves, bodies, animations and removed-diadem catalog unchanged. Manual workbench poses are separate until Apply to game is explicitly chosen.");
            }
            finally {foreach(var actor in actors)PrefabUtility.UnloadPrefabContents(actor.root);}
        }

        public static void CheckAxisScale()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
            var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;
            var scene=EditorSceneManager.NewPreviewScene();var actor=UnityEngine.Object.Instantiate(catalog.entries.First(h=>h.assetId=="Fira").prefab);
            SceneManager.MoveGameObjectToScene(actor,scene);
            var report=new AxisReport{requested=new Vector3(1.12f,.91f,1.05f)};
            try
            {
                var wardrobe=actor.GetComponent<CourierWardrobeRig>();wardrobe.motion.ManualSimulation=true;wardrobe.motion.ResetMotion();
                var item=catalog.equipment.First(h=>h.id=="hat_AmberBeret");var fit=wardrobe.profile.FindFit(item.id);
                fit.position=new Vector3(.03f,1.61f,-.08f);fit.euler=new Vector3(18,-26,9);fit.scale=.83f;fit.prefabOverride=null;
                fit.axisScale=report.requested;wardrobe.Apply(null,item,null);report.xyzError=Measure(report.requested);
                fit.axisScale=Vector3.zero;wardrobe.Apply(null,item,null);report.legacyError=Measure(Vector3.one);
                float Measure(Vector3 axes)
                {
                    var gear=wardrobe.Equipped(CourierEquipmentKind.Hat);
                    var actual=wardrobe.bodyRig.visualPivot.worldToLocalMatrix*gear.transform.localToWorldMatrix;
                    var expected=Matrix4x4.TRS(fit.position,Quaternion.Euler(fit.euler),Vector3.Scale(item.prefab.transform.localScale,axes*fit.scale));
                    float error=0;for(int i=0;i<16;i++)error=Mathf.Max(error,Mathf.Abs(actual[i]-expected[i]));return error;
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(actor);EditorSceneManager.ClosePreviewScene(scene);}
            report.activeScenePreserved=SceneManager.GetActiveScene()==active&&active.isDirty==dirty;
            report.passed=report.activeScenePreserved&&report.xyzError<.001f&&report.legacyError<.001f;
            Directory.CreateDirectory(Evidence);File.WriteAllText(Path.Combine(Evidence,"hat-axis-runtime-checks.json"),JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidDataException("Hat XYZ or legacy scale matrix did not round-trip.");
        }
    }
}
