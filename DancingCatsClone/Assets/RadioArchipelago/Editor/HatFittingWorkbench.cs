using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>A manual, scene-based fitting desk. Opening, editing or exporting never applies game poses.</summary>
    public sealed class HatFittingWorkbench : EditorWindow
    {
        public const string ScenePath="Assets/RadioArchipelago/Scenes/Tools/HatFittingWorkbench.unity";
        const string CatalogPath="Assets/RadioArchipelago/Content/CourierCatalog.asset";
        const string RootName="__HatFittingWorkbench";
        const string HeroMarker="__WorkbenchHero__";
        const string HatMarker="__EditableHatId__";
        static string TablePath => Path.Combine(Application.dataPath,"RadioArchipelago/Editor/CuratedHatPoses.json");
        static string Evidence => Path.GetFullPath("../Docs/Design/RadioArchipelago/HatFittingWorkbench");
        [SerializeField] string chosenHero="Fira",selectedHat,status,lastExport;
        Vector2 scroll,outerScroll;
        bool selectionRedirecting;
        static GameObject cachedRoot;
        static List<Station> cachedStations;

        [Serializable] public sealed class HatPose
        {
            public string hero,hat,sourceHatPrefab,prefabOverridePath;
            public Vector3 position,euler,localScale,axisScale;
            public float scale=1;
            public Quaternion rotation;
            public ChildPose[] children;
        }
        [Serializable] public sealed class ChildPose
        {
            public string path;
            public Vector3 position,euler,scale;
            public Quaternion rotation;
        }
        [Serializable] public sealed class Snapshot
        {
            public string hero,scene,createdUtc;
            public string coordinates="Hat root under VisualPivot plus complete descendant local TRS; original prefab root scale factored out.";
            public bool appliedToGame;
            public int expectedPairs;
            public bool complete;
            public HatPose[] hats;
        }
        [Serializable] sealed class ProbeReport
        {
            public bool passed,gameFilesUnchanged,activeScenePreserved,otherRowsPreserved,sourceScale100Preserved;
            public Vector3 expectedPosition,exportedPosition,expectedAxes,exportedAxes;
            public float rotationError,matrixError;
            public string beforeHash,afterHash,snapshot;
        }
        [Serializable] sealed class InventoryReport
        {
            public bool passed,gameFilesUnchanged,toolsSceneExcludedFromBuild;
            public string scene;
            public int stations,heroes,hatsPerHero,editorBehaviours,directVisualParents,unpackedActors;
            public string[] stationKeys;
        }
        sealed class Station
        {
            public string hero,hat;
            public Transform root,frame;
            public GameObject actor;
            public CourierEquipmentDefinition definition;
        }
        struct ObjectRange { public int start,length;public string json; }

        [MenuItem("Radio Archipelago/Manual hat fitting")]
        public static void Open()
        {
            var window=GetWindow<HatFittingWorkbench>("Примерка шапок");
            window.minSize=new Vector2(380,580);window.Show();
        }

        public static void CreateDefault()
        {
            EnsureEditMode();
            if(FindRoot(out _,out _)){Open();return;}
            EnsureScenesSaved();
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            else BuildAll();
            Open();FrameStation("Fira",Hats(Catalog())[0].id);
        }

        void OnEnable()
        {
            Selection.selectionChanged+=SelectionChanged;
            SceneView.duringSceneGui+=DrawSceneLabels;
            EditorApplication.hierarchyChanged+=InvalidateCache;
        }
        void OnDisable()
        {
            Selection.selectionChanged-=SelectionChanged;
            SceneView.duringSceneGui-=DrawSceneLabels;
            EditorApplication.hierarchyChanged-=InvalidateCache;
        }

        void OnGUI()
        {
            outerScroll=EditorGUILayout.BeginScrollView(outerScroll);
            try{DrawWorkbenchGUI();}
            finally{EditorGUILayout.EndScrollView();}
        }
        void DrawWorkbenchGUI()
        {
            EditorGUILayout.LabelField("Ручная примерка",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Выбери шапку ниже. W — перемещение, E — поворот, R — масштаб по X/Y/Z. F в Scene — приблизить. Local/Global выбирается на панели Scene. Сохраняются только твои значения.",MessageType.Info);
            var catalog=Catalog();
            if(catalog==null){EditorGUILayout.HelpBox("Каталог героев не найден.",MessageType.Error);return;}
            var heroes=catalog.entries;int selected=Array.FindIndex(heroes,h=>h.assetId==chosenHero);if(selected<0)selected=0;
            int next=EditorGUILayout.Popup("Герой для примерки",selected,heroes.Select(h=>h.displayName).ToArray());
            if(chosenHero!=heroes[next].assetId)ChooseHero(heroes[next].assetId);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("Открыть сцену"))Run(CreateDefault);
                if(GUILayout.Button("Создать все 70 станций"))Run(()=>{EnsureScenesSaved();BackupExistingScene();BuildAll();FrameStation(chosenHero,Hats(Catalog())[0].id);});
                EditorGUILayout.EndHorizontal();
                if(!FindRoot(out var scene,out var root))
                {EditorGUILayout.HelpBox("Открой сцену примерки или создай стенд. Изменённая сцена не будет закрыта без сохранения.",MessageType.None);DrawStatus();return;}
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Стенд: "+Stations(root,catalog).Count+" станций · 10 героев × 7 шапок",EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Только сценовые копии; игровые позы меняет отдельная кнопка «Применить».",EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("Ряд героя"))FrameHeroRow(chosenHero);
                if(GUILayout.Button("Весь стенд"))FrameAll();
                EditorGUILayout.EndHorizontal();
                scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.MinHeight(170));
                foreach(var station in Stations(root,catalog).Where(s=>s.hero==chosenHero))
                {
                    EditorGUILayout.BeginHorizontal();
                    bool active=selectedHat==station.hat;
                    if(GUILayout.Button((active?"● ":"")+station.definition.displayName))Select(station,false);
                    if(GUILayout.Button("F",GUILayout.Width(30)))Select(station,true);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
                var picked=Stations(root,catalog).FirstOrDefault(s=>s.hero==chosenHero&&s.hat==selectedHat);
                if(picked!=null)DrawTransform(picked,scene);
                EditorGUILayout.Space();
                if(GUILayout.Button("Экспортировать ВСЕ 70 поз в JSON"))Run(()=>{lastExport=ExportCurrent();status="Экспорт сохранён. Игровые позы не применялись.";});
                if(GUILayout.Button("Сохранить только сцену (Ctrl+S)"))Run(()=>{EditorSceneManager.SaveScene(scene);status="Сцена примерки сохранена. Игровые позы не применялись.";});
                EditorGUILayout.Space();
                if(GUILayout.Button("Применить 7 поз выбранного героя «"+heroes[next].displayName+"»"))Run(()=>ApplyHeroToGame(chosenHero));
                if(GUILayout.Button("Применить ВСЕ 70 поз в игру"))Run(ApplyAllToGame);
                if(GUILayout.Button("Сбросить ряд героя к таблице (снимок перед сбросом)"))Run(()=>ResetHeroFromTable(chosenHero));
            }
            DrawStatus();
        }

        void DrawTransform(Station station,Scene scene)
        {
            EditorGUILayout.LabelField(station.definition.displayName,EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var position=EditorGUILayout.Vector3Field("Положение",station.root.localPosition);
            var rotation=EditorGUILayout.Vector3Field("Поворот",Signed(station.root.localEulerAngles));
            var scale=EditorGUILayout.Vector3Field("Масштаб XYZ",station.root.localScale);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(station.root,"Manual hat transform");
                station.root.localPosition=position;station.root.localRotation=Quaternion.Euler(rotation);station.root.localScale=scale;
                EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();
            }
        }
        void DrawStatus()
        {
            if(!string.IsNullOrEmpty(status))EditorGUILayout.HelpBox(status,MessageType.None);
            if(!string.IsNullOrEmpty(lastExport))
            {EditorGUILayout.SelectableLabel(lastExport,EditorStyles.wordWrappedMiniLabel,GUILayout.Height(42));if(GUILayout.Button("Скопировать путь"))EditorGUIUtility.systemCopyBuffer=lastExport;}
        }
        void Run(Action action)
        {try{action();}catch(Exception error){status=error.Message;Debug.LogException(error);}Repaint();}

        static void BuildAll()
        {
            EnsureEditMode();EnsureScenesSaved();var catalog=Catalog();
            var hats=Hats(catalog);var table=CuratedHatPoseAuthoring.ReadTable();
            foreach(var hero in catalog.entries)
            {
                if(hero.prefab==null||hero.prefab.GetComponent<CourierWardrobeRig>()?.bodyRig?.visualPivot==null)
                    throw new InvalidOperationException("Не найден риг героя "+hero.assetId);
                foreach(var hat in hats)PoseFor(table,hero.assetId,hat.id);
            }
            EnsureFolder("Assets/RadioArchipelago/Scenes/Tools");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject(RootName);
            for(int heroIndex=0;heroIndex<catalog.entries.Length;heroIndex++)
            {
                var hero=catalog.entries[heroIndex];var row=new GameObject("РЯД · "+hero.displayName);row.transform.SetParent(root.transform,false);
                row.transform.localPosition=new Vector3(0,(catalog.entries.Length-1-heroIndex)*3.6f,0);
                for(int i=0;i<hats.Length;i++)
                {
                    var slot=new GameObject((i+1).ToString("00")+" · "+hero.displayName+" · "+hats[i].displayName);slot.transform.SetParent(row.transform,false);
                    slot.transform.localPosition=new Vector3((i-(hats.Length-1)*.5f)*3.6f,0,0);
                    var actor=Unpacked(hero.prefab,scene);actor.name="Герой · "+hero.assetId;actor.transform.SetParent(slot.transform,false);
                    actor.transform.localPosition=Vector3.zero;actor.transform.localRotation=Quaternion.identity;
                    new GameObject(HeroMarker+hero.assetId).transform.SetParent(actor.transform,false);
                    var wardrobe=actor.GetComponent<CourierWardrobeRig>();
                    wardrobe.motion.ManualSimulation=true;wardrobe.motion.ResetMotion();wardrobe.Apply(null,null,null);
                    foreach(var behavior in actor.GetComponentsInChildren<MonoBehaviour>(true))behavior.enabled=false;
                    foreach(var animator in actor.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                    foreach(var audio in actor.GetComponentsInChildren<AudioSource>(true))audio.enabled=false;
                    foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
                    var hat=Unpacked(hats[i].prefab,scene);hat.name="ШАПКА · "+hero.displayName+" · "+hats[i].displayName;
                    hat.transform.SetParent(wardrobe.bodyRig.visualPivot,false);
                    new GameObject(HatMarker+hats[i].id).transform.SetParent(hat.transform,false);
                    SetFromTable(hat.transform,hats[i],PoseFor(table,hero.assetId,hats[i].id));
                    foreach(var behavior in hat.GetComponentsInChildren<MonoBehaviour>(true))behavior.enabled=false;
                }
            }
            AddLight("Мягкий основной свет",new Vector3(35,145,0),1.2f,new Color(1,.95f,.86f));
            AddLight("Заполняющий свет",new Vector3(20,-25,0),.75f,new Color(.8f,.91f,1));
            RenderSettings.skybox=null;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.8f,.83f,.87f);RenderSettings.ambientEquatorColor=new Color(.63f,.65f,.68f);RenderSettings.ambientGroundColor=new Color(.38f,.4f,.43f);
            var cameraNode=new GameObject("Камера стенда",typeof(Camera));var camera=cameraNode.GetComponent<Camera>();
            float firaY=(catalog.entries.Length-1-Array.FindIndex(catalog.entries,h=>h.assetId=="Fira"))*3.6f;
            cameraNode.transform.position=new Vector3(0,firaY+1.2f,18);cameraNode.transform.rotation=Quaternion.Euler(0,180,0);
            camera.orthographic=true;camera.orthographicSize=7;camera.backgroundColor=new Color(.94f,.925f,.875f);camera.clearFlags=CameraClearFlags.SolidColor;
            EditorSceneManager.SaveScene(scene,ScenePath);
            InvalidateCache();Selection.activeGameObject=Stations(root,Catalog()).First(s=>s.hero=="Fira").root.gameObject;
            Debug.Log("HAT_WORKBENCH_CREATED: "+catalog.entries.Length+" heroes × "+hats.Length+" hats; unpacked scene copies only. No game pose, mesh, material or BuildSettings writes.");
        }

        static GameObject Unpacked(GameObject prefab,Scene scene)
        {
            var result=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            if(PrefabUtility.IsPartOfPrefabInstance(result))PrefabUtility.UnpackPrefabInstance(result,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            return result;
        }
        static void AddLight(string name,Vector3 euler,float intensity,Color color)
        {var node=new GameObject(name,typeof(Light));node.transform.rotation=Quaternion.Euler(euler);var light=node.GetComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.color=color;}

        void SelectionChanged()
        {
            if(selectionRedirecting||!FindRoot(out _,out var root))return;
            var selection=Selection.activeTransform;if(selection==null)return;
            foreach(var station in Stations(root,Catalog()))
                if(selection==station.root||selection.IsChildOf(station.root))
                {
                    selectedHat=station.hat;chosenHero=station.hero;
                    if(selection!=station.root)
                    {selectionRedirecting=true;try{Selection.activeGameObject=station.root.gameObject;}finally{selectionRedirecting=false;}}
                    Repaint();return;
                }
        }
        void Select(Station station,bool frame)
        {selectedHat=station.hat;chosenHero=station.hero;Selection.activeGameObject=station.root.gameObject;if(frame)FrameStation(station.hero,station.hat);}
        void ChooseHero(string hero)
        {
            chosenHero=hero;
            if(!FindRoot(out _,out var root))return;
            var candidates=Stations(root,Catalog()).Where(s=>s.hero==hero).ToArray();
            var station=candidates.FirstOrDefault(s=>s.hat==selectedHat)??candidates.FirstOrDefault();
            if(station!=null)Select(station,true);
        }
        public static void SelectHero(string hero) => GetWindow<HatFittingWorkbench>().ChooseHero(hero);

        void DrawSceneLabels(SceneView view)
        {
            if(!FindRoot(out _,out var root))return;
            var style=new GUIStyle(EditorStyles.boldLabel){fontSize=12};style.normal.textColor=new Color(.96f,.97f,1);
            foreach(var station in Stations(root,Catalog()))
            {
                Vector3 point=station.frame.position+Vector3.up*2.85f;
                float spacing=Vector2.Distance(HandleUtility.WorldToGUIPoint(point),HandleUtility.WorldToGUIPoint(point+Vector3.right*3.6f));
                float required=style.CalcSize(new GUIContent(station.definition.displayName)).x+24;
                if(spacing>=required&&view.camera.WorldToViewportPoint(point).z>0)
                    Handles.Label(point,station.definition.displayName,style);
            }
            var heroStyle=new GUIStyle(EditorStyles.boldLabel){fontSize=15};heroStyle.normal.textColor=new Color(1,.94f,.7f);
            foreach(var group in Stations(root,Catalog()).GroupBy(s=>s.hero))
            {
                var first=group.First();string name=Catalog().entries.Single(h=>h.assetId==group.Key).displayName;
                Handles.Label(first.frame.position+new Vector3(-2.1f,1.3f,0),name,heroStyle);
            }
        }
        public static void FrameAll()
        {
            if(!FindRoot(out _,out var root))return;
            var renderers=root.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            FrameBounds(bounds);
        }
        public static void FrameHeroRow(string hero)
        {
            if(!FindRoot(out _,out var root))return;
            var renderers=Stations(root,Catalog()).Where(s=>s.hero==hero).SelectMany(s=>s.actor.GetComponentsInChildren<Renderer>(true)).ToArray();
            if(renderers.Length==0)return;var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);FrameBounds(bounds);
        }
        public static void FrameStation(string hero,string hat)
        {
            if(!FindRoot(out _,out var root))return;var station=Stations(root,Catalog()).FirstOrDefault(s=>s.hero==hero&&s.hat==hat);if(station==null)return;
            Selection.activeGameObject=station.root.gameObject;
            var renderers=station.actor.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);FrameBounds(bounds);
        }
        static void FrameBounds(Bounds bounds)
        {
            var view=SceneView.lastActiveSceneView;if(view==null)return;
            float aspect=view.camera!=null?Mathf.Max(.25f,view.camera.aspect):1;
            float size=Mathf.Max(bounds.extents.y,bounds.extents.x/aspect)*1.22f;
            view.orthographic=true;view.sceneLighting=true;view.LookAt(bounds.center,Quaternion.Euler(0,180,0),Mathf.Max(.8f,size));view.Repaint();
        }

        public static Snapshot CaptureCurrent() => CaptureSubset(null);
        static Snapshot CaptureSubset(string[] heroes)
        {
            EnsureEditMode();if(!FindRoot(out var scene,out var root))throw new InvalidOperationException("Сцена примерки не открыта.");
            var catalog=Catalog();var stations=Stations(root,catalog).Where(s=>heroes==null||heroes.Contains(s.hero)).ToArray();
            if(stations.Length==0)throw new InvalidOperationException("На стенде не найдены шапки выбранного героя.");
            int expected=(heroes?.Length??catalog.entries.Length)*Hats(catalog).Length;
            var poses=stations.Select(s=>Capture(s.hero,s)).ToArray();
            if(poses.Select(p=>p.hero+"/"+p.hat).Distinct().Count()!=poses.Length)throw new InvalidOperationException("На стенде есть дубликаты шапок.");
            return new Snapshot{hero=heroes==null?"All":string.Join("_",heroes),scene=scene.path,createdUtc=DateTime.UtcNow.ToString("O"),
                expectedPairs=expected,complete=poses.Length==expected,hats=poses};
        }
        static HatPose Capture(string hero,Station station)
        {
            if(station.root.parent!=station.frame)
                throw new InvalidOperationException("Корень шапки должен оставаться непосредственно под VisualPivot: "+station.hat);
            var axes=Divide(station.root.localScale,station.definition.prefab.transform.localScale);
            if(!Finite(station.root.localPosition)||!Finite(station.root.localEulerAngles)||!Positive(axes))
                throw new InvalidOperationException("Проверь Transform шапки "+station.hat+": масштаб X/Y/Z должен быть конечным и положительным.");
            var pose=new HatPose{hero=hero,hat=station.hat,sourceHatPrefab=AssetDatabase.GetAssetPath(station.definition.prefab),
                position=station.root.localPosition,euler=Signed(station.root.localEulerAngles),rotation=station.root.localRotation,
                localScale=station.root.localScale,scale=1,axisScale=axes,
                children=Descendants(station.root).Select(p=>new ChildPose{path=p.Key,position=p.Value.localPosition,
                    rotation=p.Value.localRotation,euler=Signed(p.Value.localEulerAngles),scale=p.Value.localScale}).ToArray()};
            ValidateChildPoses(pose,station.definition);return pose;
        }
        public static string ExportCurrent()
        {
            var snapshot=CaptureCurrent();string folder=Path.Combine(Evidence,"Snapshots");Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,Stamp()+"_"+snapshot.hero+".json");File.WriteAllText(path,JsonUtility.ToJson(snapshot,true));
            if(!snapshot.complete)Debug.LogWarning("Workbench exported available "+snapshot.hats.Length+"/"+snapshot.expectedPairs+" poses; Apply requires a complete set.");
            Debug.Log("HAT_WORKBENCH_EXPORTED: "+path+"; game poses unchanged.");return path;
        }
        public static string ExportAll() => ExportCurrent();

        public static void ResetCurrentFromTable() => ResetHeroFromTable(GetWindow<HatFittingWorkbench>().chosenHero);
        public static void ResetHeroFromTable(string hero)
        {
            EnsureEditMode();ExportCurrent();FindRoot(out var scene,out var root);
            var table=CuratedHatPoseAuthoring.ReadTable();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Reset manual hat stand from table");
            foreach(var station in Stations(root,Catalog()).Where(s=>s.hero==hero))
            {
                Undo.RecordObjects(Descendants(station.root).Values.Concat(new[]{station.root}).Cast<UnityEngine.Object>().ToArray(),"Reset hat pose");
                SetFromTable(station.root,station.definition,PoseFor(table,station.hero,station.hat));
            }
            Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();
        }

        public static void ApplyCurrentToGame() => ApplyHeroToGame(GetWindow<HatFittingWorkbench>().chosenHero);
        public static void ApplyHeroToGame(string hero) => ApplySnapshot(CaptureSubset(new[]{hero}));
        public static void ApplyAllToGame() => ApplySnapshot(CaptureCurrent());
        /// <summary>Applies the saved export as-is; current workbench transforms are never recaptured.</summary>
        public static void ApplyExportToGame(string path)
        {
            EnsureEditMode();
            if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))throw new FileNotFoundException("Не найден экспорт примерочной.",path);
            Snapshot snapshot;
            try{snapshot=JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));}
            catch(ArgumentException error){throw new InvalidDataException("Некорректный JSON экспорта примерочной.",error);}
            ValidateExportSnapshot(snapshot,Catalog());
            ApplySnapshot(snapshot);
            Debug.Log("HAT_WORKBENCH_EXPORT_APPLIED: exact saved XYZ/TRS from "+Path.GetFullPath(path));
        }

        static void ValidateExportSnapshot(Snapshot snapshot,CourierCatalog catalog)
        {
            if(catalog?.entries==null||catalog.equipment==null)throw new InvalidDataException("Каталог героев не найден.");
            var heroes=catalog.entries;
            var hats=catalog.equipment.Where(h=>h!=null&&h.kind==CourierEquipmentKind.Hat).ToArray();
            if(heroes.Length!=10||hats.Length!=7||heroes.Any(h=>h==null||string.IsNullOrEmpty(h.assetId)||h.prefab==null)||
                hats.Any(h=>string.IsNullOrEmpty(h.id)||h.prefab==null)||heroes.Select(h=>h.assetId).Distinct(StringComparer.Ordinal).Count()!=10||
                hats.Select(h=>h.id).Distinct(StringComparer.Ordinal).Count()!=7)
                throw new InvalidDataException("Для этого экспорта нужны ровно10 героев и7 активных шапок.");
            if(snapshot==null||!string.Equals(snapshot.hero,"All",StringComparison.Ordinal)||!snapshot.complete||
                snapshot.expectedPairs!=70||snapshot.hats==null||snapshot.hats.Length!=70)
                throw new InvalidDataException("Нужен полный экспорт All со всеми70 сочетаниями.");
            var expected=new HashSet<string>(heroes.SelectMany(h=>hats.Select(a=>h.assetId+"/"+a.id)),StringComparer.Ordinal);
            var definitions=hats.ToDictionary(h=>h.id,StringComparer.Ordinal);
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var pose in snapshot.hats)
            {
                if(pose==null)throw new InvalidDataException("В экспорте есть пустая поза.");
                string key=pose.hero+"/"+pose.hat;
                if(!expected.Contains(key)||!seen.Add(key))throw new InvalidDataException("Неизвестное или повторное сочетание: "+key);
                var definition=definitions[pose.hat];
                if(!string.Equals(pose.sourceHatPrefab,AssetDatabase.GetAssetPath(definition.prefab),StringComparison.Ordinal))
                    throw new InvalidDataException("Исходная модель шапки не соответствует каталогу: "+key);
                if(!Finite(pose.position)||!Finite(pose.euler)||!Finite(pose.scale)||pose.scale<=0||!Positive(pose.axisScale)||!Finite(pose.localScale))
                    throw new InvalidDataException("Некорректный сохранённый Transform: "+key);
                Vector3 expectedScale=Vector3.Scale(definition.prefab.transform.localScale,pose.axisScale*pose.scale);
                if(!Finite(expectedScale)||MaxComponentAbs(expectedScale-pose.localScale)>.0001f)
                    throw new InvalidDataException("Масштаб XYZ не совпадает с исходной моделью, axisScale и scale: "+key);
                var rotation=pose.rotation;
                float length=rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w;
                if(!Finite(rotation.x)||!Finite(rotation.y)||!Finite(rotation.z)||!Finite(rotation.w)||!Finite(length)||Mathf.Abs(length-1)>.0001f)
                    throw new InvalidDataException("Некорректный сохранённый quaternion: "+key);
                var saved=Matrix4x4.TRS(pose.position,rotation,pose.localScale);
                var fromEuler=Matrix4x4.TRS(pose.position,Quaternion.Euler(pose.euler),expectedScale);
                for(int element=0;element<16;element++)
                    if(!Finite(saved[element])||!Finite(fromEuler[element])||Mathf.Abs(saved[element]-fromEuler[element])>.0001f)
                        throw new InvalidDataException("Quaternion, Euler и масштаб задают разные Transform: "+key);
                ValidateChildPoses(pose,definition);
            }
            if(!seen.SetEquals(expected))throw new InvalidDataException("В экспорте отсутствуют сочетания героев и шапок.");
        }

        static void ValidateChildPoses(HatPose pose,CourierEquipmentDefinition definition)
        {
            // Missing children is the original, root-only export format.
            if(pose.children==null)return;
            var source=Descendants(definition.prefab.transform);
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var child in pose.children)
            {
                string key=pose.hero+"/"+pose.hat+"/"+child?.path;
                if(child==null||string.IsNullOrEmpty(child.path)||!source.ContainsKey(child.path)||!seen.Add(child.path))
                    throw new InvalidDataException("Неизвестный или повторный путь внутри шапки: "+key);
                ValidateLocalTrs(child.position,child.rotation,child.euler,child.scale,key);
            }
            if(!seen.SetEquals(source.Keys))throw new InvalidDataException("В экспорте отсутствуют дочерние Transform: "+pose.hero+"/"+pose.hat);
        }

        static void ValidateLocalTrs(Vector3 position,Quaternion rotation,Vector3 euler,Vector3 scale,string key)
        {
            float length=rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w;
            if(!Finite(position)||!Finite(euler)||!Positive(scale)||!Finite(rotation.x)||!Finite(rotation.y)||
                !Finite(rotation.z)||!Finite(rotation.w)||!Finite(length)||Mathf.Abs(length-1)>.0001f)
                throw new InvalidDataException("Некорректный локальный Transform: "+key);
            // Child Euler angles are informational only. Exact localQuaternion
            // is the authoritative value exported and applied to descendants.
        }
        static float MaxComponentAbs(Vector3 value) => Mathf.Max(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));
        static void ApplySnapshot(Snapshot snapshot)
        {
            if(!snapshot.complete)throw new InvalidOperationException("Для применения нужны все станции выбранного набора. Сначала восстанови отсутствующие шапки.");
            var catalog=Catalog();var ids=snapshot.hats.Select(h=>h.hero).Distinct().ToArray();
            var heroes=ids.Select(id=>catalog.entries.Single(h=>h.assetId==id)).ToArray();
            var stage=PrefabStageUtility.GetCurrentPrefabStage();
            foreach(var hero in heroes)
            {
                string path=AssetDatabase.GetAssetPath(hero.prefab);
                if(stage!=null&&stage.assetPath==path&&stage.scene.isDirty)
                    throw new InvalidOperationException("Сначала сохрани изменения открытого prefab героя.");
                if(hero.prefab.GetComponentsInChildren<Component>(true).Any(c=>c!=null&&EditorUtility.IsDirty(c)))
                    throw new InvalidOperationException("У prefab героя есть несохранённые изменения. Сначала сохрани их.");
            }
            foreach(var pose in snapshot.hats)ValidateChildPoses(pose,Hats(catalog).Single(h=>h.id==pose.hat));
            string before=File.ReadAllText(TablePath);
            string backup=Path.Combine(Evidence,"Backups",Stamp()+"_"+snapshot.hero);Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup,"CuratedHatPoses.before.json"),before);
            foreach(var hero in heroes)
            {
                string path=AssetDatabase.GetAssetPath(hero.prefab);File.Copy(path,Path.Combine(backup,hero.assetId+".before.prefab"));
                if(File.Exists(path+".meta"))File.Copy(path+".meta",Path.Combine(backup,hero.assetId+".before.prefab.meta"));
            }
            if(File.Exists(ScenePath))File.Copy(ScenePath,Path.Combine(backup,"Workbench.last-saved.unity"));
            File.WriteAllText(Path.Combine(backup,"ManualPoses.json"),JsonUtility.ToJson(snapshot,true));
            try
            {
                AuthorChildVariants(snapshot,catalog);
                string patched=PatchHeroRows(before,snapshot);
                File.WriteAllText(TablePath,patched,new UTF8Encoding(false));
                CuratedHatPoseAuthoring.Apply(catalog,ids);
            }
            catch
            {
                File.WriteAllText(TablePath,before,new UTF8Encoding(false));
                foreach(var hero in heroes)
                {
                    string path=AssetDatabase.GetAssetPath(hero.prefab);File.Copy(Path.Combine(backup,hero.assetId+".before.prefab"),path,true);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                }
                throw;
            }
            snapshot.appliedToGame=true;File.WriteAllText(Path.Combine(backup,"AppliedPoses.json"),JsonUtility.ToJson(snapshot,true));
            Debug.Log("HAT_WORKBENCH_APPLIED: "+snapshot.hero+", "+snapshot.hats.Length+" poses only. Backup: "+backup);
        }

        static void AuthorChildVariants(Snapshot snapshot,CourierCatalog catalog)
        {
            var definitions=Hats(catalog).ToDictionary(h=>h.id,StringComparer.Ordinal);
            string folder="Assets/RadioArchipelago/Content/AuthoredHatFits/User"+Stamp()+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
            bool createdFolder=false;
            foreach(var pose in snapshot.hats)
            {
                // Rebuild from the catalog source, never from a previous fit or
                // the live scene. Root-only files retain their original semantics.
                pose.prefabOverridePath=null;
                if(pose.children==null)continue;
                var definition=definitions[pose.hat];
                var source=Descendants(definition.prefab.transform);
                bool changed=pose.children.Any(p=>!SameLocalTrs(source[p.path],p));
                if(!changed)continue;
                if(!createdFolder){EnsureFolder(folder);createdFolder=true;}
                var preview=EditorSceneManager.NewPreviewScene();
                try
                {
                    var actor=(GameObject)PrefabUtility.InstantiatePrefab(definition.prefab,preview);
                    if(actor==null)throw new InvalidOperationException("Не удалось открыть исходную шапку: "+pose.hat);
                    var root=actor.transform;var original=definition.prefab.transform;
                    root.localPosition=original.localPosition;root.localRotation=original.localRotation;root.localScale=original.localScale;
                    var targets=Descendants(root);
                    foreach(var child in pose.children)
                    {
                        var target=targets[child.path];
                        target.localPosition=child.position;target.localRotation=child.rotation;target.localScale=child.scale;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    }
                    string path=folder+"/"+pose.hero+"_"+pose.hat+".prefab";
                    if(PrefabUtility.SaveAsPrefabAsset(actor,path)==null)
                        throw new InvalidOperationException("Не удалось сохранить индивидуальную шапку: "+path);
                    pose.prefabOverridePath=path;
                }
                finally{EditorSceneManager.ClosePreviewScene(preview);}
            }
        }

        static bool SameLocalTrs(Transform source,ChildPose pose)
        {
            var a=source.localRotation;var b=pose.rotation;
            bool sameRotation=a.Equals(b)||a.Equals(new Quaternion(-b.x,-b.y,-b.z,-b.w));
            return source.localPosition.Equals(pose.position)&&source.localScale.Equals(pose.scale)&&sameRotation;
        }

        // Replace only the selected hero's object spans. Other rows and unknown
        // metadata remain byte-for-byte intact rather than being reserialized.
        static string PatchHeroRows(string json,Snapshot snapshot)
        {
            var wanted=snapshot.hats.ToDictionary(p=>p.hero+"/"+p.hat,StringComparer.Ordinal);var replacements=new List<KeyValuePair<ObjectRange,string>>();
            foreach(var range in PoseRanges(json))
            {
                var row=JsonUtility.FromJson<CuratedHatPoseAuthoring.Pose>(range.json);
                if(!wanted.TryGetValue(row.hero+"/"+row.hat,out var pose))continue;
                row.position=pose.position;row.euler=pose.euler;row.scale=pose.scale;row.axisScale=pose.axisScale;row.prefabOverridePath=pose.prefabOverridePath;
                replacements.Add(new KeyValuePair<ObjectRange,string>(range,JsonUtility.ToJson(row,true).Replace("\n","\n    ")));
            }
            if(replacements.Count!=wanted.Count)throw new InvalidDataException("В таблице не найдены все шапки выбранного героя.");
            var result=new StringBuilder(json);
            foreach(var replace in replacements.OrderByDescending(r=>r.Key.start))result.Remove(replace.Key.start,replace.Key.length).Insert(replace.Key.start,replace.Value);
            return result.ToString();
        }
        static IEnumerable<ObjectRange> PoseRanges(string json)
        {
            int key=json.IndexOf("\"poses\"",StringComparison.Ordinal),array=key<0?-1:json.IndexOf('[',key);
            if(array<0)throw new InvalidDataException("В таблице не найден массив poses.");
            int depth=0,start=-1;bool quoted=false,escaped=false;
            for(int i=array+1;i<json.Length;i++)
            {
                char c=json[i];
                if(quoted){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c=='\"')quoted=false;continue;}
                if(c=='\"'){quoted=true;continue;}
                if(c=='{'){if(depth++==0)start=i;}
                else if(c=='}'&&--depth==0){yield return new ObjectRange{start=start,length=i-start+1,json=json.Substring(start,i-start+1)};start=-1;}
                else if(c==']'&&depth==0)yield break;
            }
        }

        public static void RoundTripProbe()
        {
            EnsureEditMode();var catalog=Catalog();string before=ProtectedHash(catalog);
            var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var scene=EditorSceneManager.NewPreviewScene();
            var report=new ProbeReport{beforeHash=before,expectedPosition=new Vector3(.11f,1.72f,-.07f),expectedAxes=new Vector3(1.12f,.91f,1.05f)};
            try
            {
                var hero=catalog.entries.Single(h=>h.assetId=="Fira");var definition=Hats(catalog)[0];
                var actor=Unpacked(hero.prefab,scene);actor.transform.position=new Vector3(7,3,-4);actor.transform.rotation=Quaternion.Euler(0,31,0);actor.transform.localScale=Vector3.one*1.3f;
                var frame=actor.GetComponent<CourierWardrobeRig>().bodyRig.visualPivot;
                var hat=Unpacked(definition.prefab,scene);hat.transform.SetParent(frame,false);
                hat.transform.localPosition=report.expectedPosition;hat.transform.localRotation=Quaternion.Euler(7,23,-5);
                hat.transform.localScale=Vector3.Scale(definition.prefab.transform.localScale,report.expectedAxes);
                var captured=Capture("Fira",new Station{hat=definition.id,root=hat.transform,frame=frame,definition=definition});
                string json=JsonUtility.ToJson(captured,true);var restored=JsonUtility.FromJson<HatPose>(json);
                string table=File.ReadAllText(TablePath);
                string patched=PatchHeroRows(table,new Snapshot{hero="Fira",hats=new[]{captured},complete=true,expectedPairs=1});
                var originalRows=PoseRanges(table).ToArray();var patchedRows=PoseRanges(patched).ToArray();
                report.otherRowsPreserved=originalRows.Length==patchedRows.Length;
                for(int i=0;i<originalRows.Length;i++)
                {
                    var row=JsonUtility.FromJson<CuratedHatPoseAuthoring.Pose>(originalRows[i].json);
                    if((row.hero!="Fira"||row.hat!=definition.id)&&originalRows[i].json!=patchedRows[i].json)report.otherRowsPreserved=false;
                }
                report.exportedPosition=restored.position;report.exportedAxes=restored.axisScale;report.rotationError=Quaternion.Angle(hat.transform.localRotation,Quaternion.Euler(restored.euler));
                var a=Matrix4x4.TRS(hat.transform.localPosition,hat.transform.localRotation,hat.transform.localScale);
                var b=Matrix4x4.TRS(restored.position,Quaternion.Euler(restored.euler),Vector3.Scale(definition.prefab.transform.localScale,restored.axisScale*restored.scale));
                for(int i=0;i<16;i++)report.matrixError=Mathf.Max(report.matrixError,Mathf.Abs(a[i]-b[i]));
                var scaleSource=Unpacked(definition.prefab,scene);scaleSource.transform.localScale=Vector3.one*100;
                var factoredDefinition=new CourierEquipmentDefinition{id=definition.id,prefab=scaleSource,kind=CourierEquipmentKind.Hat};
                hat.transform.localScale=report.expectedAxes*100;
                var factored=Capture("Fira",new Station{hat=definition.id,root=hat.transform,frame=frame,definition=factoredDefinition});
                report.sourceScale100Preserved=(factored.axisScale-report.expectedAxes).sqrMagnitude<.0000001f;
                string folder=Path.Combine(Evidence,"Validation");Directory.CreateDirectory(folder);report.snapshot=Path.Combine(folder,"SyntheticPose.json");File.WriteAllText(report.snapshot,json);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            report.afterHash=ProtectedHash(catalog);report.gameFilesUnchanged=before==report.afterHash;
            report.activeScenePreserved=active==SceneManager.GetActiveScene()&&active.isDirty==dirty;
            report.passed=report.matrixError<.0001f&&report.rotationError<.01f&&(report.expectedPosition-report.exportedPosition).sqrMagnitude<.0000001f&&
                (report.expectedAxes-report.exportedAxes).sqrMagnitude<.0000001f&&report.gameFilesUnchanged&&report.activeScenePreserved&&report.otherRowsPreserved&&report.sourceScale100Preserved;
            string path=Path.Combine(Evidence,"Validation","RoundTrip.json");File.WriteAllText(path,JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Manual hat roundtrip failed: "+path);
            Debug.Log("HAT_WORKBENCH_ROUNDTRIP PASS: exact XYZ(1.12,.91,1.05), visual-frame TRS, game/table hashes unchanged; "+path);
        }
        public static void ProbeNoGameWrites() => RoundTripProbe();
        public static void ProbeStationInventory()
        {
            EnsureEditMode();if(!FindRoot(out var scene,out var root))throw new InvalidOperationException("Сцена примерки не открыта.");
            var catalog=Catalog();string before=ProtectedHash(catalog);InvalidateCache();var stations=Stations(root,catalog);
            var report=new InventoryReport{scene=scene.path,stations=stations.Count,heroes=stations.Select(s=>s.hero).Distinct().Count(),
                hatsPerHero=Hats(catalog).Length,stationKeys=stations.Select(s=>s.hero+"/"+s.hat).ToArray(),
                directVisualParents=stations.Count(s=>s.root.parent==s.frame),unpackedActors=stations.Count(s=>!PrefabUtility.IsPartOfPrefabInstance(s.actor)),
                toolsSceneExcludedFromBuild=!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath)};
            foreach(var behavior in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var script=MonoScript.FromMonoBehaviour(behavior);string path=AssetDatabase.GetAssetPath(script).Replace('\\','/');
                if(path.Contains("/Editor/"))report.editorBehaviours++;
            }
            report.gameFilesUnchanged=before==ProtectedHash(catalog);
            report.passed=report.stations==catalog.entries.Length*Hats(catalog).Length&&report.stationKeys.Distinct().Count()==report.stations&&
                stations.GroupBy(s=>s.hero).All(g=>g.Count()==Hats(catalog).Length)&&report.directVisualParents==report.stations&&
                report.unpackedActors==report.stations&&report.editorBehaviours==0&&report.gameFilesUnchanged&&report.toolsSceneExcludedFromBuild;
            string folder=Path.Combine(Evidence,"Validation");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"Inventory.json"),JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Workbench inventory validation failed.");
            Debug.Log("HAT_WORKBENCH_INVENTORY PASS: "+report.stations+" unique stations; metadata, direct visual-frame parents and no Editor scene components.");
        }

        static string ProtectedHash(CourierCatalog catalog)
        {
            using(var hash=SHA256.Create())
            {
                var text=new StringBuilder();
                foreach(string path in catalog.entries.Select(h=>AssetDatabase.GetAssetPath(h.prefab)).Concat(new[]{TablePath}).OrderBy(p=>p,StringComparer.Ordinal))
                    text.Append(path).Append(':').Append(Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)))).Append('\n');
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
            }
        }
        static void BackupExistingScene()
        {
            if(FindRoot(out _,out _))ExportCurrent();
            if(!File.Exists(ScenePath))return;
            string folder=Path.Combine(Evidence,"SceneBackups");Directory.CreateDirectory(folder);File.Copy(ScenePath,Path.Combine(folder,Stamp()+".unity"));
        }
        static void SetFromTable(Transform hat,CourierEquipmentDefinition definition,CuratedHatPoseAuthoring.Pose pose)
        {
            var template=string.IsNullOrEmpty(pose.prefabOverridePath)?definition.prefab:AssetDatabase.LoadAssetAtPath<GameObject>(pose.prefabOverridePath);
            if(template==null)throw new InvalidDataException("Не найдена исходная индивидуальная шапка: "+pose.prefabOverridePath);
            var source=Descendants(template.transform);var targets=Descendants(hat);
            if(!new HashSet<string>(source.Keys,StringComparer.Ordinal).SetEquals(targets.Keys))
                throw new InvalidDataException("Иерархия индивидуальной шапки отличается от исходной: "+definition.id);
            // Only explicit Build/Reset calls this method. Opening or exporting
            // the user's saved stand never restores or normalizes its children.
            foreach(var item in source)
            {
                var target=targets[item.Key];var original=item.Value;
                target.localPosition=original.localPosition;target.localRotation=original.localRotation;target.localScale=original.localScale;
            }
            hat.localPosition=pose.position;hat.localRotation=Quaternion.Euler(pose.euler);
            hat.localScale=Vector3.Scale(definition.prefab.transform.localScale,CuratedHatPoseAuthoring.EffectiveAxisScale(pose.axisScale)*pose.scale);
        }
        static Dictionary<string,Transform> Descendants(Transform root)
        {
            var result=new Dictionary<string,Transform>(StringComparer.Ordinal);
            foreach(var child in root.GetComponentsInChildren<Transform>(true).Where(t=>t!=root&&!t.name.StartsWith(HatMarker,StringComparison.Ordinal)))
            {
                string path=AnimationUtility.CalculateTransformPath(child,root);
                if(string.IsNullOrEmpty(path)||result.ContainsKey(path))throw new InvalidDataException("Неоднозначный путь внутри модели шапки: "+path);
                result.Add(path,child);
            }
            return result.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        }
        static CuratedHatPoseAuthoring.Pose PoseFor(CuratedHatPoseAuthoring.Table table,string hero,string hat) =>
            table.poses.FirstOrDefault(p=>p.hero==hero&&p.hat==hat)??throw new InvalidDataException("Нет исходной позы "+hero+"/"+hat);
        static CourierCatalog Catalog() => AssetDatabase.LoadAssetAtPath<CourierCatalog>(CatalogPath);
        static CourierEquipmentDefinition[] Hats(CourierCatalog catalog) => catalog.equipment.Where(h=>h.kind==CourierEquipmentKind.Hat).ToArray();
        static List<Station> Stations(GameObject root,CourierCatalog catalog)
        {
            if(cachedRoot==root&&cachedStations!=null)return cachedStations;
            var result=new List<Station>();if(catalog==null)return result;
            foreach(var marker in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith(HatMarker,StringComparison.Ordinal)))
            {
                string id=marker.name.Substring(HatMarker.Length);var definition=Hats(catalog).FirstOrDefault(h=>h.id==id);if(definition==null)continue;
                var wardrobe=marker.GetComponentInParent<CourierWardrobeRig>();if(wardrobe?.bodyRig?.visualPivot==null)continue;
                var identity=wardrobe.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.StartsWith(HeroMarker,StringComparison.Ordinal));
                if(identity==null)continue;
                result.Add(new Station{hero=identity.name.Substring(HeroMarker.Length),hat=id,root=marker.parent,
                    frame=wardrobe.bodyRig.visualPivot,actor=wardrobe.gameObject,definition=definition});
            }
            cachedRoot=root;cachedStations=result.OrderBy(s=>Array.FindIndex(catalog.entries,h=>h.assetId==s.hero)).ThenBy(s=>Array.FindIndex(Hats(catalog),h=>h.id==s.hat)).ToList();
            return cachedStations;
        }
        static void InvalidateCache(){cachedRoot=null;cachedStations=null;}
        static bool FindRoot(out Scene scene,out GameObject root)
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var candidate=SceneManager.GetSceneAt(i);if(!candidate.isLoaded)continue;
                root=candidate.GetRootGameObjects().FirstOrDefault(g=>g.name==RootName);
                if(root!=null){scene=candidate;return true;}
            }
            scene=default;root=null;return false;
        }
        static void EnsureEditMode(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Выйди из Play Mode для ручной примерки.");}
        static void EnsureScenesSaved()
        {
            EnsureEditMode();var stage=PrefabStageUtility.GetCurrentPrefabStage();
            if(stage!=null&&stage.scene.isDirty)
                throw new InvalidOperationException("Сначала сохрани изменения в Prefab Mode. Стенд не создавался, правки не закрывались.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Сначала сохрани открытую сцену (Ctrl+S). Несохранённые изменения сохранены в памяти; сцена не закрывалась.");
        }
        static void EnsureFolder(string path)
        {var parts=path.Split('/');string current=parts[0];for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}}
        static Vector3 Divide(Vector3 a,Vector3 b)
        {if(b.x==0||b.y==0||b.z==0)throw new InvalidOperationException("У исходной шапки нулевой масштаб.");return new Vector3(a.x/b.x,a.y/b.y,a.z/b.z);}
        static Vector3 Signed(Vector3 angle) => new Vector3(Mathf.DeltaAngle(0,angle.x),Mathf.DeltaAngle(0,angle.y),Mathf.DeltaAngle(0,angle.z));
        static bool Finite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static bool Positive(Vector3 value) => Finite(value)&&value.x>0&&value.y>0&&value.z>0;
        static string Stamp() => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
    }
}
