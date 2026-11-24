using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Explicit artist-authored poses. No geometric enlargement or lift solver runs here.</summary>
    public static class CuratedHatPoseAuthoring
    {
        const string CatalogPath="Assets/RadioArchipelago/Content/CourierCatalog.asset";
        const string AuthoredHatRoot="Assets/RadioArchipelago/Content/AuthoredHatFits/";
        static string TablePath => Path.Combine(Application.dataPath,"RadioArchipelago/Editor/CuratedHatPoses.json");
        static string Evidence => Path.GetFullPath("../Docs/Design/RadioArchipelago/CuratedHatFits20261105");

        [Serializable] public sealed class Pose
        {
            public string hero,hat;
            public string prefabOverridePath;
            public float scale;
            public Vector3 axisScale=Vector3.one;
            public Vector3 position,euler;
        }
        [Serializable] public sealed class Table
        {
            public int version;
            public string coordinateSpace,reviewStatus;
            public Pose[] poses;
        }
        [Serializable] sealed class AppliedPose
        {
            public string hero,hat,previousGeometry,newGeometry;
            public float previousScale,scale;
            public Vector3 previousAxisScale,axisScale;
            public Vector3 previousPosition,previousEuler,position,euler;
        }
        [Serializable] sealed class ApplyReport
        {
            public string sourceTable,scope;
            public int pairs;
            public bool activeScenePreserved;
            public AppliedPose[] observations;
        }

        public static Table ReadTable() => JsonUtility.FromJson<Table>(File.ReadAllText(TablePath));
        public static void Validate() => Validate(ReadTable(),AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath));
        public static void ApplyPilot() => Apply(AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath),new[]{"Fira","Tiko"});
        public static void ApplyAll() => Apply(AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath));

        public static void Apply(CourierCatalog catalog,string[] heroes=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply curated hat poses in Edit Mode.");
            var table=ReadTable();Validate(table,catalog);
            var rows=table.poses.ToDictionary(p=>Key(p.hero,p.hat),StringComparer.Ordinal);
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=active.isDirty;
            var observations=new List<AppliedPose>();
            foreach(var hero in catalog.entries)
            {
                if(heroes!=null&&!heroes.Contains(hero.assetId))continue;
                string path=AssetDatabase.GetAssetPath(hero.prefab);
                var actor=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var wardrobe=actor.GetComponent<CourierWardrobeRig>();
                    foreach(var hat in catalog.equipment.Where(e=>e.kind==CourierEquipmentKind.Hat))
                    {
                        var fit=wardrobe.profile.FindFit(hat.id);var pose=rows[Key(hero.assetId,hat.id)];
                        var geometry=ResolvePrefabOverride(pose,hat);
                        observations.Add(new AppliedPose{hero=hero.assetId,hat=hat.id,previousScale=fit.scale,
                            previousPosition=fit.position,previousEuler=fit.euler,previousAxisScale=fit.axisScale,previousGeometry=AssetDatabase.GetAssetPath(fit.prefabOverride),
                            axisScale=EffectiveAxisScale(pose.axisScale),
                            scale=pose.scale,position=pose.position,euler=pose.euler,newGeometry=AssetDatabase.GetAssetPath(geometry!=null?geometry:hat.prefab)});
                        // Optional authored prefabs preserve edited child transforms;
                        // their root axes stay identical to the catalog source.
                        fit.prefabOverride=geometry;
                        fit.scale=pose.scale;fit.position=pose.position;fit.euler=pose.euler;fit.axisScale=EffectiveAxisScale(pose.axisScale);
                    }
                    EditorUtility.SetDirty(wardrobe);
                    PrefabUtility.SaveAsPrefabAsset(actor,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(actor);}
            }
            bool preserved=active==UnityEngine.SceneManagement.SceneManager.GetActiveScene()&&active.isDirty==dirty;
            Directory.CreateDirectory(Evidence);
            var report=new ApplyReport{sourceTable=TablePath,scope=heroes==null?"All active explicit hero/hat poses":string.Join(", ",heroes),
                pairs=observations.Count,activeScenePreserved=preserved,observations=observations.ToArray()};
            File.WriteAllText(Path.Combine(Evidence,heroes==null?"all-apply.json":"pilot-apply.json"),JsonUtility.ToJson(report,true));
            if(!preserved)throw new InvalidOperationException("Curated hat authoring changed the active scene.");
            Debug.Log("CURATED_HAT_POSES: "+report.pairs+" explicit poses applied with validated authored geometry references; native Workshop review required.");
        }

        static void Validate(Table table,CourierCatalog catalog)
        {
            if(catalog==null||table?.poses==null)throw new InvalidDataException("Curated hat table or catalog is missing.");
            var hats=catalog.equipment.Where(h=>h.kind==CourierEquipmentKind.Hat).ToArray();
            var hatsById=hats.ToDictionary(h=>h.id,StringComparer.Ordinal);
            int expected=catalog.entries.Length*hats.Length;
            if(catalog.entries.Length==0||hats.Length==0||table.poses.Length!=expected)
                throw new InvalidDataException("Expected "+expected+" explicit poses for "+catalog.entries.Length+" heroes and "+hats.Length+" active hats.");
            var rows=new Dictionary<string,Pose>(StringComparer.Ordinal);
            foreach(var pose in table.poses)
            {
                if(pose==null||!Finite(pose.scale)||pose.scale<.3f||pose.scale>1.6f||!Finite(pose.position)||!Finite(pose.euler)||!ValidAxisScale(pose.axisScale))
                    throw new InvalidDataException("A curated hat pose contains an invalid transform.");
                string key=Key(pose.hero,pose.hat);
                if(rows.ContainsKey(key))throw new InvalidDataException("Duplicate curated pose: "+key);
                if(!hatsById.TryGetValue(pose.hat,out var hat))
                    throw new InvalidDataException("Unknown curated hat: "+key);
                ResolvePrefabOverride(pose,hat);
                rows.Add(key,pose);
            }
            // Validate every persistent binding before the first isolated
            // prefab is opened, so missing rows never cause a partial apply.
            foreach(var hero in catalog.entries)
            {
                var wardrobe=hero.prefab!=null?hero.prefab.GetComponent<CourierWardrobeRig>():null;
                if(wardrobe==null)throw new InvalidDataException("Missing wardrobe for "+hero.assetId);
                foreach(var hat in hats)
                    if(!rows.ContainsKey(Key(hero.assetId,hat.id))||wardrobe.profile.FindFit(hat.id)==null)
                        throw new InvalidDataException("Missing individual hat pose/binding: "+Key(hero.assetId,hat.id));
            }
        }

        static GameObject ResolvePrefabOverride(Pose pose,CourierEquipmentDefinition hat)
        {
            if(string.IsNullOrEmpty(pose.prefabOverridePath))return null;
            string key=Key(pose.hero,pose.hat);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(pose.prefabOverridePath);
            string actualPath=AssetDatabase.GetAssetPath(prefab);
            // Check the loaded asset's canonical path as well as its type, so
            // a relative traversal cannot escape the authored-hat folder.
            if(prefab==null||!actualPath.StartsWith(AuthoredHatRoot,StringComparison.Ordinal)||
                !actualPath.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(key+": authored hat prefab must exist inside "+AuthoredHatRoot);
            if(hat.prefab==null)
                throw new InvalidDataException(key+": catalog hat prefab is missing.");
            Vector3 actualScale=prefab.transform.localScale,sourceScale=hat.prefab.transform.localScale;
            if(!Finite(actualScale)||!Finite(sourceScale)||
                Mathf.Abs(actualScale.x-sourceScale.x)>.0001f||Mathf.Abs(actualScale.y-sourceScale.y)>.0001f||
                Mathf.Abs(actualScale.z-sourceScale.z)>.0001f)
                throw new InvalidDataException(key+": authored prefab root scale must match the catalog source; keep XYZ multipliers in the pose.");
            if(prefab.GetComponentsInChildren<Renderer>(true).Length==0)
                throw new InvalidDataException(key+": authored hat prefab has no renderers.");
            return prefab;
        }

        static string Key(string hero,string hat) => hero+"/"+hat;
        public static Vector3 EffectiveAxisScale(Vector3 axis) => axis.x==0&&axis.y==0&&axis.z==0?Vector3.one:axis;
        static bool ValidAxisScale(Vector3 axis)
        {axis=EffectiveAxisScale(axis);return Finite(axis)&&axis.x>0&&axis.y>0&&axis.z>0;}
        static bool Finite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x)&&Finite(value.y)&&Finite(value.z);
    }
}
