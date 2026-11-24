using System;
using System.IO;
using System.Linq;
using DancingCats.Clone;
using RadioArchipelago.UI;
using RadioArchipelago.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RadioArchipelago.Editor
{
    /// <summary>Runs against the real composed menu in Play Mode; the root Editor review owns entering/leaving Play.</summary>
    [InitializeOnLoad]
    public static class RadioIslandMenuLiveChecks
    {
        private const string ReviewKey="RadioArchipelago.ThreeIslandMenuReview", GuideKey="DancingCatsClone.v1.howToPlaySeen", CourierKey="RadioArchipelago.v1.courier";
        [Serializable] private sealed class SavedPreferences
        {
            public bool hadProfile,hadGuide,hadCourier;
            public string profile;
            public int guide,courier;
        }
        [Serializable] private sealed class ReviewState
        {
            public string state,error;
            public bool prePlayProfileRestored,guideRestored,courierRestored,sceneClean;
        }
        static RadioIslandMenuLiveChecks()
        {
            EditorApplication.playModeStateChanged+=OnMode;
            if(SessionState.GetBool(ReviewKey,false) && EditorApplication.isPlaying)
            {EditorApplication.update-=AwaitMenu;EditorApplication.update+=AwaitMenu;}
        }

        public static void BeginReview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || SessionState.GetBool(ReviewKey,false))
                throw new InvalidOperationException("Begin the public-menu review in stable Edit Mode with no other menu review running.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                Require(!SceneManager.GetSceneAt(i).isDirty && !string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path),"Save the current scene before reviewing its menu.");
            RadioIslandMenuJobs.RunChecks();
            var snapshot=new SavedPreferences {hadProfile=PlayerPrefs.HasKey(OriginalProgression.SaveKey),profile=PlayerPrefs.GetString(OriginalProgression.SaveKey,""),
                hadGuide=PlayerPrefs.HasKey(GuideKey),guide=PlayerPrefs.GetInt(GuideKey,0),hadCourier=PlayerPrefs.HasKey(CourierKey),courier=PlayerPrefs.GetInt(CourierKey,0)};
            SessionState.SetString(ReviewKey+".snapshot",JsonUtility.ToJson(snapshot));
            SessionState.SetBool(ReviewKey,true);SessionState.SetBool(ReviewKey+".executed",false);
            SessionState.SetFloat(ReviewKey+".deadline",(float)EditorApplication.timeSinceStartup+30);
            SessionState.SetString(ReviewKey+".state","running");SessionState.SetString(ReviewKey+".error","");
            WriteReviewState(new ReviewState {state="running"});
            PlayerPrefs.SetInt(GuideKey,1);PlayerPrefs.Save();
            EditorApplication.isPlaying=true;
        }

        private static void OnMode(PlayModeStateChange mode)
        {
            if(!SessionState.GetBool(ReviewKey,false)) return;
            if(mode==PlayModeStateChange.EnteredPlayMode)
            {EditorApplication.update-=AwaitMenu;EditorApplication.update+=AwaitMenu;}
            else if(mode==PlayModeStateChange.ExitingPlayMode)
            {EditorApplication.update-=AwaitMenu;RestoreSavedPreferences();}
            else if(mode==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=AwaitMenu;
                var result=RestoreSavedPreferences();
                result.state=SessionState.GetString(ReviewKey+".state","failed");result.error=SessionState.GetString(ReviewKey+".error","");
                if(result.state=="running") {result.state="failed";result.error="Menu review was stopped before its checks completed.";}
                result.sceneClean=true;
                for(int i=0;i<SceneManager.sceneCount;i++) result.sceneClean&=!SceneManager.GetSceneAt(i).isDirty;
                if(!result.prePlayProfileRestored || !result.guideRestored || !result.courierRestored || !result.sceneClean) result.state="failed";
                WriteReviewState(result);SessionState.SetBool(ReviewKey,false);SessionState.EraseString(ReviewKey+".snapshot");
                Debug.Log("PUBLIC_ISLAND_MENU_REVIEW "+result.state.ToUpperInvariant()+": checks and pre-Play preferences restored; "+RadioIslandMenuJobs.ReviewFolder);
            }
        }

        private static void AwaitMenu()
        {
            if(!SessionState.GetBool(ReviewKey,false) || SessionState.GetBool(ReviewKey+".executed",false)) {EditorApplication.update-=AwaitMenu;return;}
            var game=UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            var menu=game?.Presenter as RadioMenuPresenter;
            bool ready=game!=null && menu?.UICanvas!=null && game.Phase==ClonePhase.Menu && menu.VisibleSongs.Count==RadioIslandMenuPolicy.IslandCount;
            if(!ready && EditorApplication.timeSinceStartup<SessionState.GetFloat(ReviewKey+".deadline",0)) return;
            SessionState.SetBool(ReviewKey+".executed",true);EditorApplication.update-=AwaitMenu;
            try
            {
                Require(ready,"Timed out waiting for the initialized public menu.");
                RunChecks();SessionState.SetString(ReviewKey+".state","passed");
            }
            catch(Exception error)
            {SessionState.SetString(ReviewKey+".state","failed");SessionState.SetString(ReviewKey+".error",error.ToString());Debug.LogException(error);}
            finally {EditorApplication.isPlaying=false;}
        }

        private static ReviewState RestoreSavedPreferences()
        {
            var saved=JsonUtility.FromJson<SavedPreferences>(SessionState.GetString(ReviewKey+".snapshot","{}"));
            if(saved.hadProfile) PlayerPrefs.SetString(OriginalProgression.SaveKey,saved.profile); else PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
            if(saved.hadGuide) PlayerPrefs.SetInt(GuideKey,saved.guide); else PlayerPrefs.DeleteKey(GuideKey);
            if(saved.hadCourier) PlayerPrefs.SetInt(CourierKey,saved.courier); else PlayerPrefs.DeleteKey(CourierKey);
            PlayerPrefs.Save();
            return new ReviewState {prePlayProfileRestored=PlayerPrefs.HasKey(OriginalProgression.SaveKey)==saved.hadProfile && (!saved.hadProfile || PlayerPrefs.GetString(OriginalProgression.SaveKey)==saved.profile),
                guideRestored=PlayerPrefs.HasKey(GuideKey)==saved.hadGuide && (!saved.hadGuide || PlayerPrefs.GetInt(GuideKey)==saved.guide),
                courierRestored=PlayerPrefs.HasKey(CourierKey)==saved.hadCourier && (!saved.hadCourier || PlayerPrefs.GetInt(CourierKey)==saved.courier)};
        }
        private static void WriteReviewState(ReviewState state)
        {Directory.CreateDirectory(RadioIslandMenuJobs.ReviewFolder);File.WriteAllText(RadioIslandMenuJobs.ReviewFolder+"/menu-review-state.json",JsonUtility.ToJson(state,true));}

        [Serializable] private sealed class StageProof
        {
            public string song, definition, chart, currentSky, expectedSky;
            public float routeLength, expectedRouteLength;
            public bool activeIsland, correctLighting;
        }
        [Serializable] private sealed class RouteDocument { public Vector3[] points; }
        [Serializable] private sealed class Report
        {
            public bool passed, exactLibrary, exactAtlas, atlasCardsClear, forwardWrap, backwardWrap, resultsNextPolicy, hiddenApiRejected;
            public bool rawCatalogRetained, stateRestored, savedProfileRestored;
            public int underlyingSongs;
            public string[] libraryTitles, atlasTitles;
            public StageProof[] selectedStages;
        }

        public static void RunChecks()
        {
            if(!Application.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Live menu checks require the initialized menu in stable Play Mode.");
            var game=UnityEngine.Object.FindFirstObjectByType<OriginalPlayController>();
            var menu=game?.Presenter as RadioMenuPresenter;
            Require(menu!=null && menu.UICanvas!=null && game.Phase==ClonePhase.Menu,"Open the fully initialized public menu before these checks.");
            var content=Resources.Load<RadioIslandMenuCatalog>(RadioIslandMenuCatalog.ResourcePath); content.Validate();
            string stateJson=game.Progression.ToJson(), originalId=game.Selected.id, originalView=menu.CurrentView;
            bool hadSave=PlayerPrefs.HasKey(OriginalProgression.SaveKey);
            string saved=PlayerPrefs.GetString(OriginalProgression.SaveKey,"");
            SongRecord[] underlying=game.Songs;
            string[] underlyingIds=underlying.Select(s=>s.id).ToArray();
            var report=new Report {underlyingSongs=underlying.Length};
            try
            {
                Require(menu.VisibleSongs.Count==RadioIslandMenuPolicy.IslandCount && menu.VisibleSongs.Select(s=>s.id).SequenceEqual(Enumerable.Range(0,RadioIslandMenuPolicy.IslandCount).Select(RadioIslandMenuPolicy.SongId)),"Public presentation does not expose exactly the public ordered songs.");
                menu.Navigate("Songs"); Canvas.ForceUpdateCanvases();
                var rows=menu.UICanvas.GetComponentsInChildren<Button>(false).Where(b=>b.name.StartsWith("SongRow_",StringComparison.Ordinal)).ToArray();
                report.libraryTitles=rows.Select(b=>b.GetComponentsInChildren<TMP_Text>(false).Single(t=>t.name=="Title").text).ToArray();
                Require(rows.Length==RadioIslandMenuPolicy.IslandCount && report.libraryTitles.SequenceEqual(menu.VisibleSongs.Select(s=>s.title)),"Song library contains extra/missing/misordered titles.");
                report.exactLibrary=true;
                menu.Navigate("Atlas"); Canvas.ForceUpdateCanvases();
                var stations=menu.UICanvas.GetComponentsInChildren<Button>(false).Where(b=>b.name.StartsWith("Station_",StringComparison.Ordinal)).OrderBy(b=>b.name).ToArray();
                report.atlasTitles=stations.Select(b=>b.GetComponentsInChildren<TMP_Text>(false).Single(t=>t.name=="Name").text).ToArray();
                Require(stations.Length==RadioIslandMenuPolicy.IslandCount && report.atlasTitles.SequenceEqual(Enumerable.Range(0,RadioIslandMenuPolicy.IslandCount).Select(RadioIslandMenuPolicy.IslandName)),"Atlas contains placeholder or unrelated island headings.");
                report.exactAtlas=true;
                var footer=menu.UICanvas.GetComponentsInChildren<Button>(false).Single(b=>b.name=="AtlasPlayButton");
                var footerRect=(RectTransform)footer.transform;
                for(int i=0;i<stations.Length;i++)
                {
                    var rect=(RectTransform)stations[i].transform;
                    Require(rect.anchorMin.y>footerRect.anchorMax.y+.02f && rect.anchorMax.y<=.75f+.001f,"An Atlas island card overlaps the footer or summary.");
                    if(i>0) Require(rect.anchorMax.y<((RectTransform)stations[i-1].transform).anchorMin.y-.01f,"Atlas island cards overlap.");
                }
                report.atlasCardsClear=true;
                var proofs=new StageProof[RadioIslandMenuPolicy.IslandCount];
                // The first atlas card and the actual Home navigation buttons
                // exercise the same callbacks available to a player.
                stations[0].onClick.Invoke();
                for(int i=0;i<proofs.Length;i++)
                {
                    if(i>0) Invoke(menu,"NextSongButton");
                    proofs[i]=CheckStage(game,content,RadioIslandMenuPolicy.SongId(i));
                }
                Invoke(menu,"NextSongButton"); Require(game.Selected.id=="LumiLai","Forward navigation escaped the public menu."); report.forwardWrap=true;
                Invoke(menu,"PreviousSongButton"); Require(game.Selected.id==RadioIslandMenuPolicy.SongId(RadioIslandMenuPolicy.IslandCount-1),"Backward navigation escaped the public menu."); report.backwardWrap=true;
                // Invoke the Results button's real callback, even while its
                // overlay is inactive. It must use the curated policy too.
                Invoke(menu,"NextTrackButton"); Require(game.Selected.id=="LumiLai","Results Next selected a historical song.");
                report.resultsNextPolicy=true; report.selectedStages=proofs;
                foreach(var hidden in underlying.Where(s=>RadioIslandMenuPolicy.IndexOf(s.id)<0))
                {
                    string selected=game.Selected.id;
                    Require(!menu.SelectSong(hidden.catalogId) && game.Selected.id==selected,"A hidden title remained accessible through the menu API.");
                }
                report.hiddenApiRejected=true;
                Require(ReferenceEquals(game.Songs,underlying) && game.Songs.Select(s=>s.id).SequenceEqual(underlyingIds),"Public menu changed the full catalog.");
                report.rawCatalogRetained=true; report.passed=true;
            }
            finally
            {
                // Keep both the in-memory profile and the exact stored bytes,
                // including absence, intact after exercising real menu saves.
                var original=menu.VisibleSongs.First(s=>s.id==originalId);
                menu.SelectSong(original.catalogId);
                JsonUtility.FromJsonOverwrite(stateJson,game.Progression.State);
                if(hadSave) PlayerPrefs.SetString(OriginalProgression.SaveKey,saved); else PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
                PlayerPrefs.Save(); menu.Navigate(originalView);
                report.stateRestored=game.Progression.ToJson()==stateJson;
                report.savedProfileRestored=PlayerPrefs.HasKey(OriginalProgression.SaveKey)==hadSave && (!hadSave || PlayerPrefs.GetString(OriginalProgression.SaveKey)==saved);
                report.passed=report.passed && report.stateRestored && report.savedProfileRestored;
                Directory.CreateDirectory(RadioIslandMenuJobs.ReviewFolder);
                File.WriteAllText(RadioIslandMenuJobs.ReviewFolder+"/live-menu-checks.json",JsonUtility.ToJson(report,true));
                Require(report.stateRestored && report.savedProfileRestored,"Live menu check did not restore the user's profile.");
            }
            Debug.Log("PUBLIC_ISLAND_MENU_LIVE PASS: real library/atlas, both arrow wraps, ResultsNext, hidden API and all public scene-owned island bindings; profile restored.");
        }

        private static StageProof CheckStage(OriginalPlayController game,RadioIslandMenuCatalog content,string id)
        {
            var entry=content.ForSong(id);
            string actualDefinition=AssetDatabase.GetAssetPath(game.IslandStage?.Definition), expectedDefinition=AssetDatabase.GetAssetPath(entry.Definition);
            bool active=game.IslandStage!=null && game.IslandStage.IsActiveFor(id);
            Require(game.Selected.id==id && game.Map.id==id && game.IslandStage!=null && game.IslandStage.Definition==entry.Definition && active,
                "Selected song did not activate its real island chart and scenery: expectedSong="+id+", actualSong="+game.Selected?.id+", map="+game.Map?.id+", actualDefinition="+actualDefinition+", expectedDefinition="+expectedDefinition+", active="+active);
            float expectedLength=new IslandRoute(JsonUtility.FromJson<RouteDocument>(entry.Definition.RouteJson.text).points).Length;
            float actualLength=game.IslandStage.Route!=null?game.IslandStage.Route.Length:0;
            Require(expectedLength>0 && Mathf.Abs(actualLength-expectedLength)<.01f,
                "Selected island route differs from its actual source geometry: "+id+", actualLength="+actualLength+", expectedLength="+expectedLength);
            string actualSky=AssetDatabase.GetAssetPath(RenderSettings.skybox), expectedSky=AssetDatabase.GetAssetPath(entry.Skybox);
            Require(RenderSettings.skybox==entry.Skybox,"Selected island lighting did not switch: "+id+", actualSky="+actualSky+", expectedSky="+expectedSky);
            return new StageProof {song=id,definition=AssetDatabase.GetAssetPath(entry.Definition),chart=AssetDatabase.GetAssetPath(entry.Definition.Chart),
                routeLength=actualLength,expectedRouteLength=expectedLength,currentSky=actualSky,expectedSky=expectedSky,activeIsland=true,correctLighting=true};
        }
        private static void Invoke(RadioMenuPresenter menu,string name)
        {
            var buttons=menu.UICanvas.GetComponentsInChildren<Button>(true).Where(b=>b.name==name).ToArray();
            Require(buttons.Length==1,"Menu button not uniquely found: "+name); buttons[0].onClick.Invoke();
        }
        private static void Require(bool condition,string message) {if(!condition) throw new InvalidOperationException(message);}
    }
}
