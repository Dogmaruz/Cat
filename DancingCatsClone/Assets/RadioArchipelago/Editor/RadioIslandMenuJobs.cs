using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DancingCats.Clone;
using RadioArchipelago.UI;
using RadioArchipelago.World;
using RhythmGame.Services;
using UnityEditor;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    public static class RadioIslandMenuJobs
    {
        public const string AssetPath = "Assets/Resources/RadioArchipelago/IslandMenuCatalog.asset";
        public const string ReviewFolder = "Library/RadioArchipelago/PublicIslandMenu";
        [Serializable] private sealed class Report
        {
            public bool passed, onlyPublicVisible, originalCatalogUnchanged, hiddenSelectionsFallBack, navigationWraps, definitionsMatch;
            public int underlyingCatalogCount, hiddenCount;
            public string[] visibleIds, definitions;
            public string catalogSha256;
        }

        [MenuItem("Radio Archipelago/Menu/Prepare public island selection")]
        public static void Prepare()
        {
            RequireEditMode();
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var catalog = AssetDatabase.LoadAssetAtPath<RadioIslandMenuCatalog>(AssetPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<RadioIslandMenuCatalog>(); AssetDatabase.CreateAsset(catalog, AssetPath); }
            catalog.Islands = new[]
            {
                Entry(IslandAbyssJobs.DefinitionPath, IslandAbyssJobs.ContentPath+"/ArchipelagoSky.mat",
                    new Color(.57f,.72f,.88f),new Color(.39f,.49f,.54f),new Color(.25f,.32f,.35f),new Color(.72f,.83f,.9f),
                    new Color(1,.94f,.82f),2.3f,new Quaternion(.38791165f,-.27470842f,.12230807f,.8712638f)),
                Entry(CoralOasisIslandJobs.DefinitionPath,CoralOasisIslandJobs.ContentPath+"/CoralOasisSky.mat",
                    new Color(.60f,.73f,.86f),new Color(.60f,.52f,.46f),new Color(.36f,.28f,.25f),new Color(.79f,.85f,.89f),
                    new Color(1,.87f,.71f),1.45f,Quaternion.Euler(44,-34,0)),
                Entry(MoonResonanceIslandJobs.DefinitionPath,MoonResonanceIslandJobs.ContentPath+"/MoonResonanceSky.mat",
                    new Color(.62f,.64f,.84f),new Color(.60f,.55f,.67f),new Color(.32f,.29f,.40f),new Color(.75f,.73f,.85f),
                    new Color(1,.88f,.84f),1.35f,Quaternion.Euler(44,-34,0)),
                Entry(AmberWindsIslandJobs.DefinitionPath,AmberWindsIslandJobs.ContentPath+"/AmberWindsSky.mat",
                    new Color(.64f,.74f,.81f),new Color(.61f,.54f,.43f),new Color(.31f,.34f,.31f),new Color(.80f,.84f,.82f),
                    new Color(1,.90f,.73f),1.65f,Quaternion.Euler(38,-34,0))
            };
            catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            RunChecks();
        }

        private static RadioIslandMenuCatalog.Entry Entry(string definition,string sky,Color ambientSky,Color equator,Color ground,Color fog,
            Color sun,float intensity,Quaternion rotation)
            => new RadioIslandMenuCatalog.Entry { Definition=AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(definition),
                Skybox=AssetDatabase.LoadAssetAtPath<Material>(sky),AmbientSky=ambientSky,AmbientEquator=equator,AmbientGround=ground,
                FogColor=fog,SunColor=sun,SunIntensity=intensity,SunRotation=rotation };

        [MenuItem("Radio Archipelago/Menu/Check public island selection")]
        public static void RunChecks()
        {
            RequireEditMode();
            var content=AssetDatabase.LoadAssetAtPath<RadioIslandMenuCatalog>(AssetPath);
            Require(content!=null,"The direct-reference public island catalog is missing."); content.Validate();
            const string catalogFile="Assets/Resources/CustomData/songs.json", originalCatalogFile="Assets/Resources/OriginalData/songs.json";
            string before=Digest(catalogFile), originalBefore=Digest(originalCatalogFile);
            SongRecord[] all=RecoveredCatalog.LoadFrom(new UnityGameResources());
            string original=JsonUtility.ToJson(new SongArray { songs=all });
            SongRecord[] visible=RadioIslandMenuPolicy.VisibleSongs(all);
            var report=new Report { underlyingCatalogCount=all.Length,hiddenCount=all.Length-visible.Length,
                visibleIds=visible.Select(s=>s.id).ToArray(), definitions=content.Islands.Select(e=>AssetDatabase.GetAssetPath(e.Definition)).ToArray(),
                catalogSha256=before };
            Require(all.Length>RadioIslandMenuPolicy.IslandCount && visible.Length==RadioIslandMenuPolicy.IslandCount,"Public selection must hide, rather than delete, historical songs.");
            for(int i=0;i<visible.Length;i++)
            {
                Require(visible[i].id==RadioIslandMenuPolicy.SongId(i),"Visible island order changed.");
                Require(content.ForSong(visible[i].id)?.Definition?.SongId==visible[i].id,"Song-to-island binding is wrong.");
                var map=JsonUtility.FromJson<MapTimeline>(content.ForSong(visible[i].id).Definition.Chart.text);
                Require(map.id==visible[i].id,"Public island chart is bound to a different recording.");
            }
            foreach(var hidden in all.Where(s=>RadioIslandMenuPolicy.IndexOf(s.id)<0))
                Require(RadioIslandMenuPolicy.ResolveSelection(visible,hidden.id)==visible[0],"Hidden historical selection must safely resolve to Lumi-Lai.");
            Require(RadioIslandMenuPolicy.ResolveSelection(visible,null)==visible[0],"An absent selection must safely resolve to Lumi-Lai.");
            Require(RadioIslandMenuPolicy.Next(visible,"RadioTrio",1,_=>true)==visible[0] &&
                RadioIslandMenuPolicy.Next(visible,"LumiLai",-1,_=>true)==visible[3],"Navigation must wrap only through the public islands.");
            Require(RadioIslandMenuPolicy.Next(visible,"LumiLai",1,id=>id!="TikoTa")==visible[2],"Locked public entries must be skipped.");
            Require(RadioIslandMenuPolicy.Next(visible,"LumiLai",1,id=>id=="LumiLai")==null,"A single unlocked entry must not invent a different next island.");
            ExpectFailure(()=>RadioIslandMenuPolicy.VisibleSongs(all.Where(s=>s.id!="NoriNo").ToArray()));
            ExpectFailure(()=>RadioIslandMenuPolicy.VisibleSongs(all.Concat(new[]{visible[0]}).ToArray()));
            Require(JsonUtility.ToJson(new SongArray {songs=all})==original && Digest(catalogFile)==before && Digest(originalCatalogFile)==originalBefore,"Menu policy changed the underlying source catalogs.");
            report.onlyPublicVisible=report.originalCatalogUnchanged=report.hiddenSelectionsFallBack=report.navigationWraps=report.definitionsMatch=report.passed=true;
            Directory.CreateDirectory(ReviewFolder); File.WriteAllText(ReviewFolder+"/menu-checks.json",JsonUtility.ToJson(report,true));
            Debug.Log("PUBLIC_ISLAND_MENU_CHECKS PASS: exactly4public islands, fullcatalog preserved, correct directrefs and navigation.");
        }
        [Serializable] private sealed class SongArray { public SongRecord[] songs; }
        private static void ExpectFailure(Action action)
        { bool rejected=false; try {action();} catch(InvalidOperationException){rejected=true;} Require(rejected,"Invalid public catalog was not rejected."); }
        private static string Digest(string path)
        { using(var stream=File.OpenRead(path)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant(); }
        private static void Require(bool condition,string message) {if(!condition) throw new InvalidOperationException(message);}
        private static void RequireEditMode()
        {if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Public island authoring requires stable Edit Mode.");}
    }
}
