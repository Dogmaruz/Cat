using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    /// <summary>Imports the user's modular art and authors measured attachment poses.</summary>
    public static class ModularCourierAssetBuilder
    {
        const string Root = "Assets/RadioArchipelago";
        const string Import = Root + "/Imported/Modular";
        const string Content = Root + "/Content/Modular";
        static readonly string Source = Path.GetFullPath("../ArtSource/RadioArchipelago/ProcessedModular");
        static readonly string Evidence = Path.GetFullPath("../Docs/Design/RadioArchipelago/ModularCast20261104/Unity");
        static readonly string[] Names = {"Tiko","Lumen","Nori","Fira","Miro","Rumi","Boro","Soli","Vela","Piko"};
        static readonly string[] Ids = {"signal","sail","orbit","fira","miro","rumi","boro","soli","vela","piko"};
        static readonly string[] Labels = {"Тико","Люмен","Нори","Фира","Миро","Руми","Боро","Соли","Вела","Пико"};
        static readonly Dictionary<GameObject,float> OriginalStrapReach=new Dictionary<GameObject,float>();
        [Serializable] class Eyes { public float[] center; public float radiusX, radiusZ; }
        [Serializable] class Blink { public Eyes[] eyes; }
        [Serializable] class BodyMeta { public float bodyHalfWidth, bodyDepth, armHeight; public float[] neck, headCenter; public Blink blink; }
        [Serializable] class PackMeta { public float[] caseBoundsMin,caseBoundsMax,harnessColorRGB; public float contactPlaneY,referenceBodyWidth=1,referenceBodyDepth=1; }
        [Serializable] class EntryEvidence { public string id; public int vertices, triangles, bones; public float height; }
        [Serializable] class ImportEvidence { public EntryEvidence[] bodies; public int equipment; public bool complete; }

        [MenuItem("Radio Archipelago/Modular couriers/Import supplied collection")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import in Edit Mode.");
            PointCache.Clear();
            OriginalStrapReach.Clear();
            Directory.CreateDirectory(Evidence);
            foreach (var cat in new[]{"Characters","Backpacks","Hats","Scarves"})
            {
                string source = Path.Combine(Source,cat);
                if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
                foreach (var folder in IncludedSourceFolders(cat))
                {
                    string id=Path.GetFileName(folder), target=Import+"/"+cat+"/"+id;
                    Directory.CreateDirectory(target);
                    foreach (string file in new[]{"Model.fbx","BaseColor.png","Normal.png","MetallicSmoothness.png","Meta.json"})
                    {
                        string input=Path.Combine(folder,file);
                        if(!File.Exists(input)) throw new FileNotFoundException("Incomplete modular art",input);
                        string dest=target+"/"+file;
                        if (!File.Exists(dest) || !File.ReadAllBytes(dest).SequenceEqual(File.ReadAllBytes(input))) File.Copy(input,dest,true);
                    }
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var equipment=new List<CourierEquipmentDefinition>();
            foreach (var cat in new[]{"Backpacks","Hats","Scarves"})
                foreach(var folder in IncludedSourceFolders(cat).OrderBy(p=>p,StringComparer.Ordinal))
                    equipment.Add(Equipment(cat,Path.GetFileName(folder)));
            var entries=new CourierDefinition[Names.Length];var evidence=new List<EntryEvidence>();
            for(int i=0;i<Names.Length;i++) entries[i]=Body(i,equipment,evidence);
            if(equipment.Count!=29) throw new InvalidDataException("Expected 20 backpacks,7 hats,2 scarves, got "+equipment.Count);
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>(Root+"/Content/CourierCatalog.asset");
            if(catalog==null) throw new InvalidDataException("Existing catalog is missing.");
            catalog.entries=entries;catalog.equipment=equipment.ToArray();EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            ModularHatFitRepair.Repair(catalog);
            BodyHarnessMigration.FinalizeCatalog(catalog);
            File.WriteAllText(Evidence+"/import.json",JsonUtility.ToJson(new ImportEvidence{bodies=evidence.ToArray(),equipment=equipment.Count,complete=true},true));
            PointCache.Clear();
            Debug.Log("MODULAR_IMPORT PASS:10 one-segment rigs,20 articulated backpacks,7 hats,2 scarves; stable first3 IDs.");
        }

        public static void CompileProbe() { Debug.Log("MODULAR_COMPILE PASS " + Application.unityVersion); }
        public static void RefreshIcons()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>(Root+"/Content/CourierCatalog.asset");
            foreach(var hero in catalog.entries)hero.icon=IconForPrefab(hero.prefab,"Characters_"+hero.assetId);
            foreach(var item in catalog.equipment)
            {
                string cat=item.kind==CourierEquipmentKind.Backpack?"Backpacks":item.kind==CourierEquipmentKind.Hat?"Hats":"Scarves";
                string id=item.id.Substring(item.id.IndexOf('_')+1);item.icon=IconForPrefab(item.prefab,cat+"_"+id);
            }
            if(catalog.entries.Any(e=>e.icon==null)||catalog.equipment.Any(e=>e.icon==null))throw new InvalidDataException("Modular portrait sprites failed to import.");
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();Debug.Log("MODULAR_ICONS PASS:"+(catalog.entries.Length+catalog.equipment.Length)+" rendered single-sprite portraits bound to catalog.");
        }

        /// <summary>Refreshes only the backpack portraits; other wardrobe icons keep their authored assets.</summary>
        public static void RefreshBackpackIcons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Refresh backpack icons in Edit Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<CourierCatalog>(Root + "/Content/CourierCatalog.asset");
            if (catalog == null) throw new InvalidDataException("The courier catalog is missing.");
            var backpacks = catalog.equipment.Where(item => item.kind == CourierEquipmentKind.Backpack).ToArray();
            if (backpacks.Length != 20) throw new InvalidDataException("Expected20 backpack portraits, got " + backpacks.Length);
            foreach (var item in backpacks)
            {
                if (item.prefab == null) throw new InvalidDataException(item.id + ": backpack prefab is missing.");
                string id = item.id.Substring(item.id.IndexOf('_') + 1);
                item.icon = IconForPrefab(item.prefab, "Backpacks_" + id);
                if (item.icon == null) throw new InvalidDataException(item.id + ": backpack portrait failed to import.");
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("MODULAR_BACKPACK_ICONS PASS:20 decorative-front portraits; other portraits preserved.");
        }
        static IEnumerable<string> IncludedSourceFolders(string category) =>
            Directory.GetDirectories(Path.Combine(Source,category)).Where(folder =>
                category!="Hats" || !string.Equals(Path.GetFileName(folder),"SignalDiadem",StringComparison.Ordinal));

        static Sprite IconForPrefab(GameObject prefab,string name)
        {
            var scene=EditorSceneManager.NewPreviewScene();var actor=UnityEngine.Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(actor,scene);
            try{return Icon(actor,name);}finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        public static void SyncCorrections()
        {
            PointCache.Clear();
            OriginalStrapReach.Clear();
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>(Root+"/Content/CourierCatalog.asset");
            foreach(string cat in new[]{"Backpacks","Hats","Scarves"})
                foreach(string folder in IncludedSourceFolders(cat))
                {
                    string id=Path.GetFileName(folder),target=Import+"/"+cat+"/"+id;bool changed=false;
                    foreach(string file in new[]{"Model.fbx","BaseColor.png","Normal.png","MetallicSmoothness.png","Meta.json"})
                    {
                        string input=Path.Combine(folder,file),dest=target+"/"+file;
                        if(!File.ReadAllBytes(input).SequenceEqual(File.ReadAllBytes(dest))){File.Copy(input,dest,true);changed=true;}
                    }
                    if(!changed)
                    {
                        if(cat=="Scarves")
                        {
                            var refreshed=Equipment(cat,id);int refreshedAt=Array.FindIndex(catalog.equipment,e=>e.id==refreshed.id);catalog.equipment[refreshedAt]=refreshed;continue;
                        }
                        if(cat=="Backpacks")
                        {
                            var item=catalog.FindEquipment("backpack_"+id,CourierEquipmentKind.Backpack);
                            string path=AssetDatabase.GetAssetPath(item.prefab);var instance=PrefabUtility.LoadPrefabContents(path);
                            try
                            {
                                var rig=instance.GetComponent<CourierEquipmentRig>();
                                var pm=JsonUtility.FromJson<PackMeta>(File.ReadAllText(Path.Combine(folder,"Meta.json")));
                                rig.caseBoundsMin=new Vector3(pm.caseBoundsMin[0],pm.caseBoundsMin[2],-pm.caseBoundsMax[1]);
                                rig.caseBoundsMax=new Vector3(pm.caseBoundsMax[0],pm.caseBoundsMax[2],-pm.caseBoundsMin[1]);rig.caseContactZ=-pm.contactPlaneY;
                                PrefabUtility.SaveAsPrefabAsset(instance,path);
                            }
                            finally{PrefabUtility.UnloadPrefabContents(instance);}
                        }
                        continue;
                    }
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    var entry=Equipment(cat,id);int at=Array.FindIndex(catalog.equipment,e=>e.id==entry.id);catalog.equipment[at]=entry;
                }
            foreach(var hero in catalog.entries)
            {
                string path=AssetDatabase.GetAssetPath(hero.prefab);var instance=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var wardrobe=instance.GetComponent<CourierWardrobeRig>();
                    var meta=JsonUtility.FromJson<BodyMeta>(File.ReadAllText(Path.Combine(Source,"Characters",hero.assetId,"Meta.json")));
                    wardrobe.profile.shoulderHeight=meta.armHeight+.02f;
                    wardrobe.profile.strapBottomHeight=Mathf.Max(.38f,meta.armHeight-.42f);
                    wardrobe.profile.bodySections=Sections(instance,wardrobe.bodyRig.visualPivot);
                    wardrobe.profile.leftHarnessPath=wardrobe.profile.rightHarnessPath=Array.Empty<Vector3>();
                    wardrobe.profile.leftHarnessNormals=wardrobe.profile.rightHarnessNormals=Array.Empty<Vector3>();
                    wardrobe.profile.equipmentFits=Fits(meta,wardrobe.profile,catalog.equipment.ToList());
                    PrefabUtility.SaveAsPrefabAsset(instance,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(instance);}
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();PointCache.Clear();
            ModularHatFitRepair.Repair(catalog);
            BodyHarnessMigration.FinalizeCatalog(catalog);
            Debug.Log("MODULAR_CORRECTIONS PASS: prepared payloads synchronized and attachment poses refreshed.");
        }

        public static void SyncStraplessBackpacks(params string[] ids)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Synchronize wardrobe assets in Edit Mode.");
            PointCache.Clear();
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>(Root+"/Content/CourierCatalog.asset");
            foreach (string folder in Directory.GetDirectories(Path.Combine(Source,"Backpacks")).OrderBy(p=>p,StringComparer.Ordinal))
            {
                string id=Path.GetFileName(folder),target=Import+"/Backpacks/"+id;
                if(ids.Length>0 && !ids.Contains(id))continue;
                foreach(string file in new[]{"Model.fbx","BaseColor.png","Normal.png","MetallicSmoothness.png","Meta.json"})
                {
                    string source=Path.Combine(folder,file),dest=target+"/"+file;
                    if(!File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(dest))) File.Copy(source,dest,true);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var entry=Equipment("Backpacks",id);
                if(entry.prefab.GetComponent<CourierEquipmentRig>().harness.Length!=0)
                    throw new InvalidDataException(id+": the prepared source still contains straps.");
                int index=Array.FindIndex(catalog.equipment,e=>e.id==entry.id);
                if(index<0)throw new InvalidDataException("Missing saved equipment ID: "+entry.id);
                catalog.equipment[index]=entry;
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();PointCache.Clear();
            BodyHarnessMigration.FinalizeCatalog(catalog);
            Debug.Log("STRAPLESS_BACKPACK_IMPORT PASS: prepared case selection replaced; other equipment and scenes preserved.");
        }

        static Material Material(string cat,string id)
        {
            string folder=Import+"/"+cat+"/"+id;
            foreach(string file in new[]{"BaseColor.png","Normal.png","MetallicSmoothness.png"})
            {
                var ti=(TextureImporter)AssetImporter.GetAtPath(folder+"/"+file);
                ti.textureType=file=="Normal.png"?TextureImporterType.NormalMap:TextureImporterType.Default;
                ti.sRGBTexture=file=="BaseColor.png";ti.alphaIsTransparency=false;ti.isReadable=false;ti.mipmapEnabled=true;
                ti.maxTextureSize=cat=="Hats"||cat=="Scarves"?1024:2048;ti.textureCompression=TextureImporterCompression.CompressedHQ;
                foreach(string platform in new[]{"Android","iPhone"})
                {
                    var ps=ti.GetPlatformTextureSettings(platform);ps.overridden=true;ps.maxTextureSize=ti.maxTextureSize;
                    ps.format=TextureImporterFormat.ASTC_6x6;ps.compressionQuality=100;ti.SetPlatformTextureSettings(ps);
                }
                ti.SaveAndReimport();
            }
            string path=Content+"/Materials/"+cat+"_"+id+".mat";Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/BaseColor.png"));mat.SetColor("_BaseColor",Color.white);
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Normal.png"));mat.SetFloat("_BumpScale",.7f);
            mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/MetallicSmoothness.png"));
            mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",cat=="Scarves"?.12f:.75f);mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(mat);return mat;
        }
        static GameObject Model(string cat,string id,Transform parent)
        {
            string path=Import+"/"+cat+"/"+id+"/Model.fbx";
            var mi=(ModelImporter)AssetImporter.GetAtPath(path);mi.importAnimation=false;mi.animationType=ModelImporterAnimationType.Generic;
            mi.avatarSetup=ModelImporterAvatarSetup.NoAvatar;mi.importBlendShapes=true;mi.importBlendShapeNormals=ModelImporterNormals.None;
            mi.importNormals=ModelImporterNormals.Import;mi.importTangents=ModelImporterTangents.CalculateMikk;mi.isReadable=true;
            mi.materialImportMode=ModelImporterMaterialImportMode.None;mi.optimizeGameObjects=false;mi.SaveAndReimport();
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent,false);instance.name="SuppliedModel";
            foreach(var a in instance.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(a);
            var mat=Material(cat,id);
            foreach(var r in instance.GetComponentsInChildren<Renderer>(true))
            {r.sharedMaterials=Enumerable.Repeat(mat,Math.Max(1,r.sharedMaterials.Length)).ToArray();r.receiveShadows=true;r.shadowCastingMode=ShadowCastingMode.On;}
            return instance;
        }
        static CourierEquipmentDefinition Equipment(string cat,string id)
        {
            var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject(cat+"_"+id);SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                var model=Model(cat,id,root.transform);var rig=root.AddComponent<CourierEquipmentRig>();
                if(cat=="Scarves")model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(1,.68f,1));
                rig.topPivot=Find(model,"TopPivot");rig.topRotor=Find(model,"TopRotor");
                rig.spinAxis=id=="Fira_Beacon"?Vector3.forward:Vector3.up;
                rig.harness=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Harness",StringComparison.Ordinal)).ToArray();
                rig.antennaChain=Chain(model,"antenna.");rig.scarfChain=Chain(model,"scarf.").Where(t=>t.name!="scarf.root").ToArray();
                var b=BoundsOf(model);rig.boundsMin=b.min;rig.boundsMax=b.max;
                rig.referenceBodyWidth=1;rig.referenceBodyDepth=.9f;
                rig.harnessMaterial=PlainMaterial("Harness_"+id,HarnessColor(id),.12f,.3f);
                rig.buckleMaterial=PlainMaterial("WarmBrass",new Color(.65f,.37f,.10f),.75f,.55f);
                if(cat=="Backpacks")
                {
                    var pm=JsonUtility.FromJson<PackMeta>(File.ReadAllText(Path.Combine(Source,cat,id,"Meta.json")));
                    rig.referenceBodyWidth=pm.referenceBodyWidth>0?pm.referenceBodyWidth:1;
                    rig.referenceBodyDepth=pm.referenceBodyDepth>0?pm.referenceBodyDepth:1;
                    rig.caseBoundsMin=new Vector3(pm.caseBoundsMin[0],pm.caseBoundsMin[2],-pm.caseBoundsMax[1]);
                    rig.caseBoundsMax=new Vector3(pm.caseBoundsMax[0],pm.caseBoundsMax[2],-pm.caseBoundsMin[1]);
                    rig.caseContactZ=-pm.contactPlaneY;
                    if(pm.harnessColorRGB!=null&&pm.harnessColorRGB.Length==3)
                        rig.harnessMaterial=PlainMaterial("Harness_"+id,new Color(pm.harnessColorRGB[0],pm.harnessColorRGB[1],pm.harnessColorRGB[2]),.1f,.3f);
                }
                if(cat=="Backpacks" && (rig.topPivot==null||rig.topRotor==null)) throw new InvalidDataException(id+": top joint missing.");
                string path=Content+"/"+cat+"/"+id+".prefab";Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                var kind=cat=="Backpacks"?CourierEquipmentKind.Backpack:cat=="Hats"?CourierEquipmentKind.Hat:CourierEquipmentKind.Scarf;
                string prefix=kind==CourierEquipmentKind.Backpack?"backpack_":kind==CourierEquipmentKind.Hat?"hat_":"scarf_";
                return new CourierEquipmentDefinition{id=prefix+id,displayName=EquipmentLabel(id),kind=kind,prefab=prefab,icon=Icon(root,cat+"_"+id)};
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }
        static CourierDefinition Body(int index,List<CourierEquipmentDefinition> equipment,List<EntryEvidence> evidence)
        {
            string id=Names[index];var meta=JsonUtility.FromJson<BodyMeta>(File.ReadAllText(Path.Combine(Source,"Characters",id,"Meta.json")));
            var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Courier_"+Ids[index]);SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                var visual=new GameObject("VisualPivot").transform;visual.SetParent(root.transform,false);
                var model=Model("Characters",id,visual);var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(skins.Length==0)throw new InvalidDataException(id+": no skinned body.");
                var body=Find(model,"body");var head=Find(model,"head");var left=Find(model,"arm.L");var right=Find(model,"arm.R");
                foreach(var limb in new[]{left,right})
                {
                    if(limb==null)throw new InvalidDataException(id+": missing shoulder.");
                    float side=Mathf.Sign(limb.position.x);
                    limb.rotation=Quaternion.AngleAxis(-side*55,visual.forward)*limb.rotation;
                }
                var blink=skins.FirstOrDefault(s=>s.sharedMesh.GetBlendShapeIndex("Blink")>=0);
                if(blink!=null)blink.SetBlendShapeWeight(blink.sharedMesh.GetBlendShapeIndex("Blink"),0);
                var binding=new CourierRigBinding{visualPivot=visual,headPivot=head,leftArm=left,rightArm=right,leftFoot=Find(model,"foot.L"),rightFoot=Find(model,"foot.R"),crestChain=Chain(model,"crest."),blinkRenderer=blink,blinkShapeName="Blink"};
                var motion=root.AddComponent<CourierMotion>();motion.ConfigureRig(binding);
                var wardrobe=root.AddComponent<CourierWardrobeRig>();wardrobe.Configure(motion,binding,body,head);
                float width=meta.bodyHalfWidth*2,depth=meta.bodyDepth,neckY=meta.neck!=null?meta.neck[2]:1.3f;
                wardrobe.profile=new CourierMountProfile{bodyWidth=width,bodyDepth=depth,bodyHeight=2.1f,headWidth=width*.94f,neckWidth=width*.83f,
                    backpackPosition=new Vector3(0,.62f,-depth*.5f),backpackScale=width*.74f,
                    hatPosition=new Vector3(0,1.85f,-.08f),hatEuler=new Vector3(-14,0,0),hatScale=width*1.04f,
                    scarfPosition=new Vector3(0,neckY-.3f,0),scarfScale=width};
                wardrobe.profile.shoulderHeight=meta.armHeight+.02f;
                wardrobe.profile.strapBottomHeight=Mathf.Max(.38f,meta.armHeight-.42f);
                wardrobe.profile.bodySections=Sections(model,visual);
                wardrobe.profile.equipmentFits=Fits(meta,wardrobe.profile,equipment);
                string path=Content+"/Characters/"+id+".prefab";Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Bounds bounds=BoundsOf(model);
                evidence.Add(new EntryEvidence{id=id,vertices=skins.Sum(s=>s.sharedMesh.vertexCount),triangles=skins.Sum(s=>s.sharedMesh.triangles.Length/3),bones=model.GetComponentsInChildren<Transform>().Count(t=>new[]{"root","body","head","arm.L","arm.R","foot.L","foot.R"}.Contains(t.name)),height=bounds.size.y});
                return new CourierDefinition{id=Ids[index],assetId=id,displayName=Labels[index],prefab=prefab,icon=Icon(root,"Characters_"+id),defaultBackpackId="backpack_"+id,defaultScarfId=id=="Tiko"?"scarf_TikoCoral":id=="Nori"?"scarf_NoriBlue":""};
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static Color HarnessColor(string id)
        {
            if(id.StartsWith("Tiko",StringComparison.Ordinal)||id.StartsWith("Miro",StringComparison.Ordinal))return new Color(.16f,.31f,.25f);
            if(id.StartsWith("Nori",StringComparison.Ordinal)||id.StartsWith("Vela",StringComparison.Ordinal))return new Color(.18f,.24f,.32f);
            if(id.StartsWith("Rumi",StringComparison.Ordinal))return new Color(.31f,.22f,.38f);
            return new Color(.32f,.16f,.075f);
        }
        static Material PlainMaterial(string name,Color color,float metallic,float smooth)
        {
            string path=Content+"/Materials/"+name+".mat";Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(mat);return mat;
        }
        static CourierBodySection[] Sections(GameObject model,Transform frame)
        {
            var points=new List<Vector3>();
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var scratch=new Mesh();
                try
                {
                    skin.BakeMesh(scratch,true);var vertices=scratch.vertices;var counts=skin.sharedMesh.GetBonesPerVertex();var weights=skin.sharedMesh.GetAllBoneWeights();int cursor=0;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        float weight=0;
                        for(int j=0;j<counts[i];j++){var w=weights[cursor++];string name=skin.bones[w.boneIndex].name;if(name=="body"||name=="head"||name=="root")weight+=w.weight;}
                        if(weight>.7f)points.Add(frame.InverseTransformPoint(skin.transform.TransformPoint(vertices[i])));
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(scratch);}
            }
            var result=new List<CourierBodySection>();
            for(float y=.3f;y<=1.81f;y+=.075f)
            {
                var band=points.Where(p=>Mathf.Abs(p.y-y)<.055f).ToArray();if(band.Length<4)continue;
                result.Add(new CourierBodySection{height=y,halfWidth=band.Max(p=>Mathf.Abs(p.x)),frontZ=band.Max(p=>p.z),backZ=band.Min(p=>p.z)});
            }
            return result.ToArray();
        }
        static void ProjectHarness(GameObject model,Transform frame,CourierMountProfile profile)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var scratch=new Mesh();
                try
                {
                    skin.BakeMesh(scratch,true);int offset=vertices.Count;var vs=scratch.vertices;var counts=skin.sharedMesh.GetBonesPerVertex();var weights=skin.sharedMesh.GetAllBoneWeights();var eligible=new bool[vs.Length];int cursor=0;
                    for(int i=0;i<vs.Length;i++)
                    {
                        vertices.Add(frame.InverseTransformPoint(skin.transform.TransformPoint(vs[i])));float weight=0;
                        for(int k=0;k<counts[i];k++){var w=weights[cursor++];string bone=skin.bones[w.boneIndex].name;if(bone=="body"||bone=="head"||bone=="root")weight+=w.weight;}
                        eligible[i]=weight>.65f;
                    }
                    var ts=scratch.triangles;
                    for(int i=0;i<ts.Length;i+=3)if(eligible[ts[i]]&&eligible[ts[i+1]]&&eligible[ts[i+2]])
                    {triangles.Add(offset+ts[i]);triangles.Add(offset+ts[i+1]);triangles.Add(offset+ts[i+2]);}
                }
                finally{UnityEngine.Object.DestroyImmediate(scratch);}
            }
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            var temp=new GameObject("Disposable harness contact surface");temp.transform.SetParent(frame,false);var collider=temp.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
            try
            {
                for(int side=-1;side<=1;side+=2)
                {
                    var path=new Vector3[72];var normals=new Vector3[72];
                    for(int i=0;i<72;i++)
                    {
                        float angle=i*Mathf.PI*2/72;float h=Mathf.Lerp(profile.strapBottomHeight,profile.shoulderHeight+.32f,.5f+.5f*Mathf.Cos(angle));
                        var a=profile.bodySections[0];var b=a;
                        for(int j=1;j<profile.bodySections.Length;j++){b=profile.bodySections[j];if(h<=b.height)break;a=b;}
                        float t=Mathf.InverseLerp(a.height,b.height,h),half=Mathf.Lerp(a.halfWidth,b.halfWidth,t),front=Mathf.Lerp(a.frontZ,b.frontZ,t),back=Mathf.Lerp(a.backZ,b.backZ,t);
                        float ratio=.50f+.50f*Mathf.Pow(Mathf.Abs(Mathf.Cos(angle)),1.4f),middle=(front+back)*.5f;
                        float radius=(Mathf.Sin(angle)>=0?front:back)-middle,zWeight=Mathf.Sqrt(Mathf.Max(0,1-ratio*ratio));
                        Vector3 normal=new Vector3(side*ratio/Mathf.Max(.05f,half),0,Mathf.Sign(radius)*zWeight/Mathf.Max(.05f,Mathf.Abs(radius))).normalized;
                        Vector3 target=new Vector3(side*half*ratio,h,middle+radius*zWeight);
                        var ray=new Ray(frame.TransformPoint(target+normal*3),frame.TransformDirection(-normal));
                        if(collider.Raycast(ray,out RaycastHit hit,6))
                        {target=frame.InverseTransformPoint(hit.point);normal=frame.InverseTransformDirection(hit.normal).normalized;}
                        path[i]=target+normal*.020f;normals[i]=normal;
                    }
                    if(side<0){profile.leftHarnessPath=path;profile.leftHarnessNormals=normals;}
                    else{profile.rightHarnessPath=path;profile.rightHarnessNormals=normals;}
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(temp);UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static readonly Dictionary<GameObject,Vector3[]> PointCache=new Dictionary<GameObject,Vector3[]>();
        static Vector3[] Points(GameObject prefab)
        {
            if(PointCache.TryGetValue(prefab,out var result))return result;
            var points=new List<Vector3>();
            foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if(mf.sharedMesh!=null)foreach(var v in mf.sharedMesh.vertices)points.Add(prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var scratch=new Mesh();try{skin.BakeMesh(scratch,true);foreach(var v in scratch.vertices)points.Add(prefab.transform.InverseTransformPoint(skin.transform.TransformPoint(v)));}
                finally{UnityEngine.Object.DestroyImmediate(scratch);}
            }
            result=points.ToArray();PointCache.Add(prefab,result);return result;
        }
        static CourierEquipmentFit[] Fits(BodyMeta meta,CourierMountProfile profile,List<CourierEquipmentDefinition> gear)
        {
            var result=new List<CourierEquipmentFit>();
            float eyeTop=meta.blink?.eyes!=null&&meta.blink.eyes.Length>0?meta.blink.eyes.Max(e=>e.center[2]+e.radiusZ):1.55f;
            float eyeBottom=meta.blink?.eyes!=null&&meta.blink.eyes.Length>0?meta.blink.eyes.Min(e=>e.center[2]-e.radiusZ):1.3f;
            foreach(var item in gear)
            {
                var fit=new CourierEquipmentFit{equipmentId=item.id};
                if(item.kind==CourierEquipmentKind.Backpack)
                {
                    string id=item.id.Substring("backpack_".Length);
                    var pm=JsonUtility.FromJson<PackMeta>(File.ReadAllText(Path.Combine(Source,"Backpacks",id,"Meta.json")));
                    float height=pm.caseBoundsMax!=null?pm.caseBoundsMax[2]-pm.caseBoundsMin[2]:1;
                    // The accepted Nori case is1.18m tall. Equipment has the same physical
                    // size on every courier; only straps and attachment positions follow anatomy.
                    fit.scale=1.18f/Mathf.Max(.2f,height);
                    float back=profile.bodySections.Length>0?profile.bodySections.OrderBy(s=>Mathf.Abs(s.height-(meta.armHeight+.12f))).First().backZ:-profile.bodyDepth*.48f;
                    fit.position=new Vector3(0,meta.armHeight+.12f-height*fit.scale*.5f,
                        back+.025f+pm.contactPlaneY*fit.scale);
                    float reach=StrapReach(item.prefab)*fit.scale;
                    fit.harnessWidthScale=Mathf.Clamp((profile.bodyWidth*.5f+.05f)/Mathf.Max(.05f,reach),.85f,1);
                }
                else if(item.kind==CourierEquipmentKind.Hat)
                {
                    float opening=item.id=="hat_AviatorCap"?.74f:item.id=="hat_ExplorerHat"?.58f:
                        item.id=="hat_NavigatorCap"?.72f:item.id=="hat_AmberBeret"?.78f:
                        item.id=="hat_SailorBeret"?.76f:item.id=="hat_SignalDiadem"?.92f:.86f;
                    fit.scale=profile.headWidth/opening*1.08f;
                    fit.euler=new Vector3(-14,0,0);
                    var rotation=Quaternion.Euler(fit.euler);var pts=Points(item.prefab);
                    var forehead=pts.Where(p=>Mathf.Abs(p.x)<.28f&&p.z>.04f).Select(p=>(rotation*p).y).ToArray();
                    float brim=forehead.Length>0?forehead.Min():0;
                    fit.position=new Vector3(0,eyeTop+.085f-brim*fit.scale,-profile.bodyDepth*.06f);
                }
                else
                {
                    var pts=Points(item.prefab);float top=pts.Max(p=>p.y),bottom=pts.Min(p=>p.y);
                    var loop=pts.Where(p=>p.y>top-(top-bottom)*.26f).ToArray();
                    fit.euler=new Vector3(0,-125,0);var rotation=Quaternion.Euler(fit.euler);
                    var rotated=loop.Select(p=>rotation*p).ToArray();
                    float centerZ=(loop.Min(p=>p.z)+loop.Max(p=>p.z))*.5f;
                    float centerX=(loop.Min(p=>p.x)+loop.Max(p=>p.x))*.5f;
                    float centerY=top-(top-bottom)*.15f;
                    float loopWidth=rotated.Max(p=>p.x)-rotated.Min(p=>p.x),loopDepth=rotated.Max(p=>p.z)-rotated.Min(p=>p.z);
                    fit.scale=Mathf.Max((profile.bodyWidth+.26f)/Mathf.Max(.1f,loopWidth),(profile.bodyDepth+.20f)/Mathf.Max(.1f,loopDepth));
                    float desiredY=eyeBottom-.075f-(top-centerY)*fit.scale;
                    var section=profile.bodySections.OrderBy(s=>Mathf.Abs(s.height-desiredY)).First();
                    fit.position=new Vector3(0,desiredY,(section.frontZ+section.backZ)*.5f)-rotation*new Vector3(centerX,centerY,centerZ)*fit.scale;
                }
                result.Add(fit);
            }
            return result.ToArray();
        }
        static float StrapReach(GameObject prefab)
        {
            if(OriginalStrapReach.TryGetValue(prefab,out float reach))return reach;
            var rig=prefab.GetComponent<CourierEquipmentRig>();var points=new List<Vector3>();
            foreach(var strap in rig.harness)
                foreach(var filter in strap.GetComponentsInChildren<MeshFilter>(true))
                    if(filter.sharedMesh!=null)
                        foreach(var point in filter.sharedMesh.vertices)
                            points.Add(prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(point)));
            if(points.Count==0)return 1;
            float front=points.Max(p=>p.z);
            reach=points.Where(p=>p.z>=front-.05f).Average(p=>Mathf.Abs(p.x));
            OriginalStrapReach[prefab]=reach;return reach;
        }
        static string EquipmentLabel(string id)
        {
            string[] keys={"AmberBeret","AviatorCap","ExplorerHat","NavigatorCap","ObservatoryCap","SailorBeret","SignalBeanie","SignalDiadem","TikoCoral","NoriBlue"};
            string[] values={"Янтарный берет","Шлем авиатора","Панама исследователя","Кепка навигатора","Шапочка обсерватории","Морской берет","Шапка радиста","Диадема сигнала","Коралловый шарф","Голубой шарф"};
            int at=Array.IndexOf(keys,id);if(at>=0)return values[at];
            int hero=Array.IndexOf(Names,id.Split('_')[0]);return (hero>=0?Labels[hero]:id)+(id.Contains("_")?" · Экспедиция":" · Классика");
        }
        static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        static Transform[] Chain(GameObject root,string prefix)=>root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith(prefix,StringComparison.Ordinal)).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray();
        static Bounds BoundsOf(GameObject root)
        {
            var rs=root.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)return new Bounds(Vector3.zero,Vector3.one);
            return CourierVisualBounds.Measure(rs);
        }
        static Sprite Icon(GameObject root,string name)
        {
            var scene=root.scene;var b=BoundsOf(root);
            // Backpack +Z is the body-contact panel. View its decorative outer face
            // from the opposite side without rotating the model or its attachment rig.
            var viewRotation=name.StartsWith("Backpacks_",StringComparison.Ordinal)?Quaternion.Euler(0,180,0):Quaternion.identity;
            var go=new GameObject("IconCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);var cam=go.GetComponent<Camera>();
            cam.scene=scene;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.95f,.93f,.87f,1);cam.orthographic=true;
            cam.orthographicSize=Mathf.Max(b.size.y,b.size.x)*.61f;cam.transform.position=b.center+viewRotation*new Vector3(2,1.1f,5);cam.transform.LookAt(b.center);
            cam.nearClipPlane=.01f;cam.farClipPlane=30;
            var light=new GameObject("IconLight",typeof(Light));SceneManager.MoveGameObjectToScene(light,scene);light.transform.rotation=viewRotation*Quaternion.Euler(35,145,0);light.GetComponent<Light>().type=LightType.Directional;light.GetComponent<Light>().intensity=1.5f;
            var rt=RenderTexture.GetTemporary(256,256,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try
            {
                cam.targetTexture=rt;using(var baked=new CourierPreviewSkinBake(root)){cam.Render();}RenderTexture.active=rt;var image=new Texture2D(256,256,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                string path=Content+"/Icons/"+name+".png";Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.isReadable=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally{RenderTexture.active=previous;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(light);}
        }
    }
}
