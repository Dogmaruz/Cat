using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadioArchipelago.Editor
{
    public static class ModularCourierVisualReview
    {
        static bool rearSidesOnly;
        static bool scarfShowcaseOnly;
        static readonly string Folder=Path.GetFullPath("../Docs/Design/RadioArchipelago/ModularCast20261104/Unity");
        public static void ImportAndReview()
        {
            ModularCourierAssetBuilder.Run();
            ModularWardrobeChecks.Run();
            Run();
        }
        public static void CorrectionsAndReview()
        {
            ModularCourierAssetBuilder.SyncCorrections();
            ModularWardrobeChecks.Run();ModularCourierSkinDiagnostics.Run();Run();
        }
        public static void RearSides()
        {
            rearSidesOnly=true;try{Run();}finally{rearSidesOnly=false;}
        }
        public static void FitAndRearSides()
        {
            ModularCourierAssetBuilder.SyncCorrections();ModularWardrobeChecks.Run();RearSides();
            scarfShowcaseOnly=true;try{Run();}finally{scarfShowcaseOnly=false;}
        }
        public static void ReviewRealMenu()
        {
            ModularCourierAssetBuilder.RefreshIcons();RearSides();ModularWardrobePlayReview.Run();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var catalog=AssetDatabase.LoadAssetAtPath<CourierCatalog>("Assets/RadioArchipelago/Content/CourierCatalog.asset");
            var scene=EditorSceneManager.NewPreviewScene();
            var cameraGO=new GameObject("Modular review camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGO,scene);
            var camera=cameraGO.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=2.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.89f,.88f,.82f);
            camera.nearClipPlane=.01f;camera.farClipPlane=40;
            var rt=RenderTexture.GetTemporary(640,800,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
            camera.targetTexture=rt;var pixels=new Texture2D(640,800,TextureFormat.RGBA32,false);
            var previousAmbient=RenderSettings.ambientLight;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
            Light(scene,"Warm key",new Vector3(35,145,0),1.4f,new Color(1,.94f,.83f));
            Light(scene,"Cool rim",new Vector3(25,-25,0),.9f,new Color(.75f,.89f,1));
            int images=0;
            GameObject sample=null;
            try
            {
                foreach(var hero in catalog.entries)
                {
                    var actor=UnityEngine.Object.Instantiate(hero.prefab);SceneManager.MoveGameObjectToScene(actor,scene);
                    sample=actor;
                    try
                    {
                        var wardrobe=actor.GetComponent<CourierWardrobeRig>();var motion=actor.GetComponent<CourierMotion>();motion.ManualSimulation=true;
                        var packs=catalog.equipment.Where(e=>e.kind==CourierEquipmentKind.Backpack&&e.id.StartsWith("backpack_"+hero.assetId,StringComparison.Ordinal)).ToArray();
                        var scarf=catalog.FindEquipment(hero.defaultScarfId,CourierEquipmentKind.Scarf);
                        if(scarfShowcaseOnly)
                        {
                            foreach(var cloth in catalog.equipment.Where(e=>e.kind==CourierEquipmentKind.Scarf))
                            {
                                wardrobe.Apply(packs.FirstOrDefault(),null,cloth);motion.ResetMotion();
                                Capture(hero.assetId+"_"+cloth.id+"_front",new Vector3(0,1.7f,6));
                                Capture(hero.assetId+"_"+cloth.id+"_side",new Vector3(6,1.7f,0));
                            }
                            continue;
                        }
                        wardrobe.Apply(packs.FirstOrDefault(),null,scarf);
                        motion.PlayIdle();motion.SimulateSprings(.08f);
                        if(!rearSidesOnly)
                        {
                            Capture(hero.assetId+"_default_front",new Vector3(0,1.55f,6));
                            Capture(hero.assetId+"_default_side",new Vector3(5,1.8f,2));
                        }
                        foreach(var pack in packs)
                        {
                            if(rearSidesOnly)
                            {
                                wardrobe.Apply(pack,null,scarf);motion.ResetMotion();
                                Capture(hero.assetId+"_"+pack.id+"_back",new Vector3(0,1.7f,-6));
                                Capture(hero.assetId+"_"+pack.id+"_side",new Vector3(6,1.7f,0));continue;
                            }
                            wardrobe.Apply(pack,null,scarf);motion.PlayJump(1.4f);
                            for(int i=0;i<30;i++){motion.SetFlightProgress(i/60f);motion.SimulateSprings(1/60f);}
                            Capture(hero.assetId+"_"+pack.id+"_flight",new Vector3(4,1.8f,4));
                            motion.ResetMotion();
                            Capture(hero.assetId+"_"+pack.id+"_back",new Vector3(0,1.7f,-6));
                        }
                        if(rearSidesOnly)continue;
                        foreach(var hat in catalog.equipment.Where(e=>e.kind==CourierEquipmentKind.Hat))
                        {
                            wardrobe.Apply(packs.FirstOrDefault(),hat,scarf);motion.PlayIdle();motion.ResetMotion();
                            Capture(hero.assetId+"_"+hat.id,new Vector3(0,1.55f,6));
                        }
                    }
                    finally {UnityEngine.Object.DestroyImmediate(actor);}
                }
                File.WriteAllText(Folder+(scarfShowcaseOnly?"/scarf-review.json":rearSidesOnly?"/rear-side-review.json":"/visual-review.json"),"{\"nativeUnity\":true,\"heroes\":10,\"images\":"+images+",\"scope\":\""+(scarfShowcaseOnly?"Both scarves on all10 heroes front/side":rearSidesOnly?"20 backpacks rear and side in rest pose":"Default/side,20 owners' articulated packs in flight/back,80 hat fits")+"\"}");
                Debug.Log("MODULAR_VISUAL_CAPTURE PASS: "+images+" native Unity model views.");
            }
            finally
            {
                RenderSettings.ambientLight=previousAmbient;RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);EditorSceneManager.ClosePreviewScene(scene);
            }
            void Capture(string name,Vector3 location)
            {
                var bounds=CourierVisualBounds.Measure(sample.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray());
                camera.orthographicSize=Mathf.Max(2.05f,bounds.size.y*.56f,bounds.size.x/camera.aspect*.57f);
                float centerY=Mathf.Max(1.45f,bounds.max.y-camera.orthographicSize*.88f);
                if(rearSidesOnly){camera.orthographicSize=2.6f;centerY=2.0f;}
                camera.transform.position=location;camera.transform.LookAt(new Vector3(0,centerY,0));
                using(var baked=new CourierPreviewSkinBake(sample)){baked.Refresh();camera.Render();}
                RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,640,800),0,0);pixels.Apply();File.WriteAllBytes(Folder+"/"+name+".png",pixels.EncodeToPNG());images++;
            }
        }
        static void Light(Scene scene,string name,Vector3 euler,float intensity,Color color)
        {
            var obj=new GameObject(name,typeof(UnityEngine.Light));SceneManager.MoveGameObjectToScene(obj,scene);obj.transform.rotation=Quaternion.Euler(euler);
            var light=obj.GetComponent<UnityEngine.Light>();light.type=LightType.Directional;light.intensity=intensity;light.color=color;
        }
    }
}
