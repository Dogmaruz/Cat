#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DancingCats.Clone;
using RadioArchipelago.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RadioArchipelago.Editor
{
    /// <summary>Real workshop Canvas proofs, with cosmetic preferences restored on every exit.</summary>
    [InitializeOnLoad]
    public static class WardrobeFitWorkshopReview
    {
        const string Key = "RadioArchipelago.WardrobeFitReview20261105";
        const string Guide = "DancingCatsClone.v1.howToPlaySeen";
        static string Root => Path.GetFullPath("../Docs/Design/RadioArchipelago/WardrobePolish20261105/Workshop");
        static RadioMenuPresenter ui;
        static CourierSelection selection;
        static CourierMenuPreview portrait;
        static Camera camera;
        static RenderTexture target;
        static Canvas canvas;
        static RenderMode oldMode;
        static Camera oldCamera;
        static float oldPlane;
        static int wait, index, angle;
        static List<Case> cases;
        static Report report;
        static readonly string[] Packs = {"backpack_Boro", "backpack_Miro", "backpack_Miro_Garden", "backpack_Piko_Voyager", "backpack_Vela_Cloud"};
        [Serializable] sealed class Preference { public string key; public bool exists; public int value; }
        [Serializable] sealed class Backup { public Preference[] integers; public bool hadProfile; public string profile; }
        [Serializable] sealed class Case { public int hero; public string heroId, kind, item; }
        [Serializable] sealed class LiveStatus { public bool playing, gesturePresent; public string view; public int rotationArrows, hats; }
        [Serializable] sealed class Report
        {
            public string phase, capturedUtc, details;
            public bool passed, preferencesRestored, mouseDragPassed, touchDragPassed, arrowsRemoved;
            public List<string> images = new List<string>();
            public string capture = "Unretouched real Play Mode Workshop Canvas, production CourierMenuPreview and item icons.";
        }
        public static void BeforePilot() => Begin("BeforePilot", false);
        public static void AfterPilot() => Begin("AfterPilot", false);
        public static void AfterAll() => Begin("AfterAll", true);
        public static void OpenForUser()
        {
            SessionState.SetBool(Key+".openForUser",true);
            if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.EnterPlaymode();
        }
        public static void WriteLiveStatus()
        {
            var presenter=UnityEngine.Object.FindAnyObjectByType<RadioMenuPresenter>();
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            var status=new LiveStatus{playing=EditorApplication.isPlaying,view=presenter!=null?presenter.CurrentView:"Unavailable",
                hats=catalog.equipment.Count(e=>e.kind==CourierEquipmentKind.Hat)};
            if(presenter!=null&&presenter.UICanvas!=null)
            {
                status.rotationArrows=presenter.UICanvas.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="RotateOutfitLeft"||t.name=="RotateOutfitRight");
                status.gesturePresent=presenter.UICanvas.GetComponentsInChildren<CourierPreviewOrbitGesture>(true).Length==1;
            }
            Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,"live-status.json"),JsonUtility.ToJson(status,true));
        }

        static WardrobeFitWorkshopReview()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Mode;
            EditorApplication.quitting += Restore;
        }
        static void Begin(string phase, bool all)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Start the workshop proof in compiled Edit Mode.");
            if (SessionState.GetBool(Key, false)) throw new InvalidOperationException("A workshop proof is already active.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            var keys = new List<string> {CourierSelection.PreferenceKey, Guide};
            foreach (var hero in catalog.entries)
                foreach (CourierEquipmentKind kind in Enum.GetValues(typeof(CourierEquipmentKind)))
                    keys.Add("RadioArchipelago.v1.outfit." + hero.id + "." + kind);
            var backup = new Backup {integers = keys.Select(k => new Preference {key=k, exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray(),
                hadProfile=PlayerPrefs.HasKey(OriginalProgression.SaveKey),profile=PlayerPrefs.GetString(OriginalProgression.SaveKey, "")};
            SessionState.SetString(Key+".backup", JsonUtility.ToJson(backup));
            SessionState.SetString(Key+".phase", phase); SessionState.SetBool(Key+".all", all);
            SessionState.SetFloat(Key+".deadline", (float)EditorApplication.timeSinceStartup+600);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key+".finishing", false);
            PlayerPrefs.SetInt(Guide,1); PlayerPrefs.Save();
            EditorApplication.EnterPlaymode();
        }
        static void Mode(PlayModeStateChange mode)
        {
            if (mode == PlayModeStateChange.EnteredPlayMode)
            { wait=index=angle=0; ui=null; selection=null; portrait=null; cases=null; report=null; }
            if (mode == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false)) Restore();
        }
        static void Tick()
        {
            if(SessionState.GetBool(Key+".openForUser",false)&&EditorApplication.isPlaying&&!EditorApplication.isCompiling)
            {
                var presenter=UnityEngine.Object.FindAnyObjectByType<RadioMenuPresenter>();
                if(presenter!=null&&presenter.UICanvas!=null)
                {
                    var navigation=presenter.UICanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                        .FirstOrDefault(b=>b.name=="Navigation_Workshop");
                    if(navigation!=null){navigation.onClick.Invoke();SessionState.SetBool(Key+".openForUser",false);}
                }
            }
            if (!SessionState.GetBool(Key,false) || SessionState.GetBool(Key+".finishing",false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",float.MaxValue)) throw new TimeoutException("Workshop proof timed out.");
                if (++wait<8) return; wait=0;
                if (ui==null) ui=UnityEngine.Object.FindAnyObjectByType<RadioMenuPresenter>();
                var game=UnityEngine.Object.FindAnyObjectByType<OriginalPlayController>();
                if (ui==null || ui.UICanvas==null || game==null || game.Phase!=ClonePhase.Menu) return;
                if (report==null) { Initialize(); return; }
                if (index>=cases.Count) { Finish(true,"Real workshop outfit proofs and pointer gestures completed."); return; }
                var current=cases[index];
                if (angle==0)
                {
                    ui.SelectCourier(current.hero);
                    var def=selection.SelectedDefinition;
                    selection.Equip(CourierEquipmentKind.Backpack,current.kind=="pack"?current.item:
                        (current.heroId=="Fira"||current.heroId=="Tiko"?"backpack_Vela_Cloud":def.defaultBackpackId));
                    selection.Equip(CourierEquipmentKind.Scarf,def.defaultScarfId);
                    selection.Equip(CourierEquipmentKind.Hat,current.kind=="hat"?current.item:null);
                    ui.ShowWorkshopCategory(current.kind=="hat"?2:1);
                    portrait=ui.UICanvas.GetComponentsInChildren<CourierMenuPreview>(false).First(p=>p.name=="OutfitPreview");
                    portrait.RotatePreview((current.kind=="hat"?0:180)-portrait.PreviewYaw);
                    angle=1; return;
                }
                if (angle==1)
                {
                    Capture(current.heroId+"__"+current.item+"__"+(current.kind=="hat"?"front":"back")+".png");
                    portrait.RotatePreview(90-portrait.PreviewYaw); angle=2; return;
                }
                if(angle==2)
                {
                    Capture(current.heroId+"__"+current.item+"__side.png");
                    if(current.kind=="hat"&&SessionState.GetBool(Key+".all",false))
                    {portrait.RotatePreview(180-portrait.PreviewYaw);angle=3;return;}
                }
                else if(angle==3)
                {
                    Capture(current.heroId+"__"+current.item+"__back.png");
                    portrait.RotatePreview(270-portrait.PreviewYaw);angle=4;return;
                }
                else Capture(current.heroId+"__"+current.item+"__left.png");
                index++; angle=0;
            }
            catch(Exception e) { Finish(false,e.ToString()); }
        }
        static void Initialize()
        {
            report=new Report {phase=SessionState.GetString(Key+".phase","Unknown"),capturedUtc=DateTime.UtcNow.ToString("O")};
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            selection=(CourierSelection)typeof(RadioMenuPresenter).GetField("courierSelection",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
            var button=ui.UICanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="Navigation_Workshop");
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
            canvas=ui.UICanvas; oldMode=canvas.renderMode;oldCamera=canvas.worldCamera;oldPlane=canvas.planeDistance;
            var node=new GameObject("Owned wardrobe proof camera",typeof(Camera));node.transform.position=new Vector3(3000,3000,-10);
            camera=node.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.98f,.95f,.89f);
            camera.nearClipPlane=.1f;camera.farClipPlane=50;camera.allowHDR=false;
            target=new RenderTexture(540,1168,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();
            portrait=canvas.GetComponentsInChildren<CourierMenuPreview>(false).First(p=>p.name=="OutfitPreview");
            CheckGestures();
            cases=new List<Case>();bool all=SessionState.GetBool(Key+".all",false);
            for(int i=0;i<catalog.entries.Length;i++)
            {
                var hero=catalog.entries[i];
                if (all||hero.assetId=="Fira"||hero.assetId=="Tiko")
                    foreach(var hat in catalog.equipment.Where(e=>e.kind==CourierEquipmentKind.Hat))
                        cases.Add(new Case{hero=i,heroId=hero.assetId,kind="hat",item=hat.id});
                if (all||hero.assetId=="Fira")
                    foreach(var pack in Packs.Concat(hero.assetId=="Fira"?new[]{"backpack_Fira"}:Array.Empty<string>()))
                        cases.Add(new Case{hero=i,heroId=hero.assetId,kind="pack",item=pack});
            }
            ui.ShowWorkshopCategory(1); Capture("00-backpack-icons.png");
        }
        static void CheckGestures()
        {
            report.arrowsRemoved=!canvas.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="RotateOutfitLeft"||t.name=="RotateOutfitRight");
            if(!report.arrowsRemoved)throw new InvalidDataException("Obsolete rotation arrows remain.");
            var rt=(RectTransform)portrait.transform;
            var center=RectTransformUtility.WorldToScreenPoint(camera,rt.TransformPoint(rt.rect.center));
            var right=RectTransformUtility.WorldToScreenPoint(camera,rt.TransformPoint(rt.rect.center+Vector2.right*rt.rect.width*.25f));
            report.mouseDragPassed=Drag(-1,center,right);
            report.touchDragPassed=Drag(7,right,center);
            if(!report.mouseDragPassed||!report.touchDragPassed)throw new InvalidDataException("The actual preview gesture did not rotate for mouse/touch.");
            bool Drag(int pointer,Vector2 from,Vector2 to)
            {
                float before=portrait.PreviewYaw;
                var data=new PointerEventData(EventSystem.current){pointerId=pointer,button=PointerEventData.InputButton.Left,position=from,pressPosition=from,
                    pointerPressRaycast=new RaycastResult{gameObject=portrait.gameObject,module=canvas.GetComponent<GraphicRaycaster>()}};
                ExecuteEvents.Execute(portrait.gameObject,data,ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(portrait.gameObject,data,ExecuteEvents.beginDragHandler);
                data.delta=to-from;data.position=to;
                ExecuteEvents.Execute(portrait.gameObject,data,ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(portrait.gameObject,data,ExecuteEvents.endDragHandler);
                ExecuteEvents.Execute(portrait.gameObject,data,ExecuteEvents.pointerUpHandler);
                return Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(before,portrait.PreviewYaw))-90)<.5f;
            }
        }
        static void Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var preview in canvas.GetComponentsInChildren<CourierMenuPreview>(false))
                if(preview.PreviewCamera!=null)preview.PreviewCamera.Render();
            camera.Render();var previous=RenderTexture.active;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                var folder=Path.Combine(Root,report.phase);Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,name),pixels.EncodeToPNG());report.images.Add(name);
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);}
        }
        static void Finish(bool passed,string details)
        {
            if(SessionState.GetBool(Key+".finishing",false))return;
            SessionState.SetBool(Key+".finishing",true);
            if(report==null)report=new Report{phase=SessionState.GetString(Key+".phase","Unknown")};
            report.passed=passed;report.details=details;
            if(canvas!=null){canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;}
            if(camera!=null){camera.targetTexture=null;UnityEngine.Object.Destroy(camera.gameObject);}
            if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}
            SessionState.SetString(Key+".report",JsonUtility.ToJson(report));
            EditorApplication.ExitPlaymode();
        }
        static void Restore()
        {
            if(!SessionState.GetBool(Key,false))return;
            var backup=JsonUtility.FromJson<Backup>(SessionState.GetString(Key+".backup","{}"));
            if(backup!=null)
            {
                foreach(var item in backup.integers)
                    if(item.exists)PlayerPrefs.SetInt(item.key,item.value);else PlayerPrefs.DeleteKey(item.key);
                if(backup.hadProfile)PlayerPrefs.SetString(OriginalProgression.SaveKey,backup.profile);else PlayerPrefs.DeleteKey(OriginalProgression.SaveKey);
                PlayerPrefs.Save();
            }
            var saved=JsonUtility.FromJson<Report>(SessionState.GetString(Key+".report","{}"));
            if(saved==null)saved=new Report{passed=false,details="Proof interrupted."};
            saved.phase=SessionState.GetString(Key+".phase","Unknown");
            saved.preferencesRestored=backup!=null&&backup.integers.All(p=>PlayerPrefs.HasKey(p.key)==p.exists&&(!p.exists||PlayerPrefs.GetInt(p.key)==p.value))&&
                PlayerPrefs.HasKey(OriginalProgression.SaveKey)==backup.hadProfile&&(!backup.hadProfile||PlayerPrefs.GetString(OriginalProgression.SaveKey)==backup.profile);
            if(!saved.preferencesRestored){saved.passed=false;saved.details+=" Cosmetic preferences did not restore exactly.";}
            var folder=Path.Combine(Root,saved.phase);Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(saved,true));
            SessionState.SetBool(Key,false);
            Debug.Log("WARDROBE_WORKSHOP_REVIEW "+(saved.passed?"PASS":"FAIL")+": "+saved.details);
        }
    }
}
#endif
