using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RadioArchipelago.Editor
{
    /// <summary>Reproduces phase-aliasing of the generated final meadow over earlier visits to the same world.</summary>
    public static class IslandFinishGladeOverlapChecks
    {
        [Serializable] private sealed class RouteDocument { public Vector3[] points = Array.Empty<Vector3>(); }
        [Serializable] public sealed class Coverage
        {
            public int sourceIndex;
            public string kind;
            public float chartZ, routeDistance, tileTop, tileBottom, gladeHeight, penetration;
            public double gameTime;
            public bool bodyBuried, topBuried;
            public Vector3 position;
        }
        [Serializable] public sealed class Obstruction
        {
            public int sourceIndex;
            public float actorChartZ, actorLateral, fraction;
            public Vector3 camera, target, intersection;
        }
        [Serializable] public sealed class LegacySource64Regression
        {
            public string scope = "Frozen original source64 and finale frame from Docs/Design/RadioArchipelago/IslandPilotReview/HoldTerrain20261029/Baseline/finish-glade-earlier-overlap.json; current generated meadow triangles and current unwrapped-distance visibility policy.";
            public bool passed, earlierHidden, finalJumpVisible, landingVisible, collectionVisible,
                thresholdBeforeHidden, thresholdAtVisible, thresholdAfterVisible;
            public int sourceIndex = 64, samples, groundCovered, bodyBuried, activeEarlierBuriedBodySamples;
            public float chartZ, showThresholdZ, centerPenetration;
        }
        [Serializable] public sealed class Report
        {
            public string checkedUtc, definitionDependencyHash;
            public string scope = "Actual scene-owned IslandFinishGlade meshes and top-triangle sampling for every earlier epoch in the current chart. Potential intersections are reported even while hidden; no intersection is required. A separate frozen original source64 fixture retains the old failure as a positive control. Main FBX is not altered.";
            public bool constructedActive, firstWindowBodyBuried, firstWindowTopBuried, secondWindowBodyBuried,
                firstWindowSightlineBlocked, secondWindowSightlineBlocked;
            public bool passed, finalJumpVisible, landingVisible, collectionVisible;
            public bool thresholdBeforeHidden, thresholdAtVisible, thresholdAfterVisible;
            public LegacySource64Regression legacySource64;
            public float lastCoveredChartZ, showThresholdZ;
            public int activeEarlierBuriedBodySamples;
            public float takeoffZ, landingZ, groundHeight, length;
            public Vector3 anchor, forward;
            public int priorTopSamples, priorGroundCovered, priorBuriedBodySamples, priorBuriedTopSamples, rays, obstructedRays;
            public List<Coverage> covered = new List<Coverage>();
            public List<Obstruction> obstructions = new List<Obstruction>();
        }
        private struct Triangle { internal Vector3 a,b,c; }

        [MenuItem("Radio Archipelago/Floating archipelago/Check finish meadow earlier-lap overlaps")]
        public static void RunGladeOverlap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("The meadow overlap fixture requires stable Edit Mode.");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
            MapElement takeoff = null, landing = null;
            foreach (var e in map.elements)
            { if (e.kind == "FinishJump") takeoff=e; if (e.id==10) landing=e; }
            if (takeoff==null) throw new InvalidOperationException("The real finishing jump is missing.");
            // id10 is instantiated by BuildTrack from the native finish prefab;
            // it is deliberately absent from the authored musical timeline.
            float landingZ=landing!=null?landing.pos.z:takeoff.pos.z+takeoff.jumpDistance;
            var path = new IslandGroundFinishPath(route, definition.StartDistance, definition.DistanceScale, definition.TileElevation,
                takeoff.pos.z, landingZ, landingZ+29, IslandGroundFinishPath.MeadowExitYaw);
            var frame=path.LandingFrame;
            var report=new Report { checkedUtc=DateTime.UtcNow.ToString("O"),
                definitionDependencyHash=AssetDatabase.GetAssetDependencyHash(IslandAbyssJobs.DefinitionPath).ToString(),
                takeoffZ=path.TakeoffChartZ,landingZ=path.LandingChartZ,groundHeight=path.GroundHeight,
                anchor=frame.Position,forward=frame.Tangent,length=Mathf.Max(34,path.CoinRunLength) };
            report.showThresholdZ=path.TakeoffChartZ-definition.VisibleAhead/definition.DistanceScale;
            report.finalJumpVisible=path.ShouldPresentGlade(path.TakeoffChartZ,definition.VisibleAhead);
            report.landingVisible=path.ShouldPresentGlade(path.LandingChartZ,definition.VisibleAhead);
            report.collectionVisible=path.ShouldPresentGlade(path.EndChartZ,definition.VisibleAhead);
            report.thresholdBeforeHidden=!path.ShouldPresentGlade(report.showThresholdZ-.01f,definition.VisibleAhead);
            report.thresholdAtVisible=path.ShouldPresentGlade(report.showThresholdZ,definition.VisibleAhead);
            report.thresholdAfterVisible=path.ShouldPresentGlade(report.showThresholdZ+.01f,definition.VisibleAhead);
            var scene=EditorSceneManager.NewPreviewScene();
            GameObject node=null;
            try
            {
                node=new GameObject("Independent final meadow overlap fixture");SceneManager.MoveGameObjectToScene(node,scene);
                var glade=node.AddComponent<IslandFinishGlade>();
                glade.Configure(frame.Position,frame.Tangent,path.GroundHeight,report.length,14,
                    Borrow(definition.Prefab,"Terrain"),Borrow(definition.Prefab,"Cliffs"),
                    Borrow(definition.Prefab,"Grass"),Borrow(definition.Prefab,"Foliage"),71237);
                report.constructedActive=node.activeInHierarchy;
                foreach(var e in map.elements)
                {
                    if(e.pos.z>=path.TakeoffChartZ || e.id==7 || e.id==8)continue;
                    var f=route.Evaluate(definition.StartDistance+e.pos.z*definition.DistanceScale);
                    float x=e.updateType==3?NativeRhythmRules.MoveCycleX(e.pos.x,0,e.msLeft!=0):e.pos.x;
                    var center=f.Position+f.Right*(x*definition.LateralScale)+Vector3.up*(e.pos.y+definition.TileElevation);
                    for(int rim=0;rim<9;rim++)
                    {
                        float angle=(rim-1)*Mathf.PI/4;
                        var p=rim==0?center:center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.65f;
                        report.priorTopSamples++;
                        if(!glade.TrySampleGround(p,out float y))continue;
                        report.priorGroundCovered++;
                        report.lastCoveredChartZ=Mathf.Max(report.lastCoveredChartZ,e.pos.z);
                        var c=new Coverage {sourceIndex=e.sourceIndex,kind=e.kind,chartZ=e.pos.z,gameTime=e.gameTime,
                            routeDistance=definition.StartDistance+e.pos.z*definition.DistanceScale,
                            position=p,tileTop=p.y,tileBottom=p.y-definition.TileThickness,gladeHeight=y,
                            penetration=y-(p.y-definition.TileThickness),bodyBuried=y>p.y-definition.TileThickness+.001f,topBuried=y>p.y+.001f};
                        if(c.bodyBuried)report.priorBuriedBodySamples++;
                        if(c.bodyBuried&&path.ShouldPresentGlade(e.pos.z,definition.VisibleAhead))report.activeEarlierBuriedBodySamples++;
                        if(c.topBuried)report.priorBuriedTopSamples++;
                        if(e.sourceIndex>=60&&e.sourceIndex<=68)
                        {report.firstWindowBodyBuried|=c.bodyBuried;report.firstWindowTopBuried|=c.topBuried;}
                        if(e.sourceIndex>=146&&e.sourceIndex<=151)report.secondWindowBodyBuried|=c.bodyBuried;
                        report.covered.Add(c);
                    }
                }
                var geometry=ReadTriangles(glade);
                foreach(var e in map.elements)
                {
                    bool first=e.sourceIndex>=60&&e.sourceIndex<=68,second=e.sourceIndex>=146&&e.sourceIndex<=151;
                    if(!first&&!second)continue;
                    var f=route.Evaluate(definition.StartDistance+e.pos.z*definition.DistanceScale);
                    for(float ahead=.5f;ahead<=12.01f;ahead+=.5f)
                    {
                        float x=e.updateType==3?NativeRhythmRules.MoveCycleX(e.pos.x,ahead,e.msLeft!=0):e.pos.x;
                        var target=f.Position+f.Right*(x*definition.LateralScale)+Vector3.up*(e.pos.y+definition.TileElevation+.025f);
                        foreach(float lateral in new[]{-1.3f,0,1.3f})
                        {
                            float actorZ=e.pos.z-ahead;
                            var rig=new IslandCloseCameraRig();
                            var pose=rig.Step(route,definition.StartDistance+actorZ*definition.DistanceScale,
                                lateral*definition.LateralScale*definition.CameraLateralFollow,definition.CameraBackDistance,
                                definition.CameraHeight,definition.CameraLookAhead,definition.CameraFocusHeight,
                                definition.CameraHeadingLead,definition.CameraSmoothing,0,true);
                            report.rays++;
                            if(!RayHit(geometry,pose.Position,target,out float fraction))continue;
                            report.obstructedRays++;
                            if(first)report.firstWindowSightlineBlocked=true;if(second)report.secondWindowSightlineBlocked=true;
                            if(report.obstructions.Count<600)report.obstructions.Add(new Obstruction{sourceIndex=e.sourceIndex,
                                actorChartZ=actorZ,actorLateral=lateral*definition.LateralScale,fraction=fraction,
                                camera=pose.Position,target=target,intersection=Vector3.Lerp(pose.Position,target,fraction)});
                        }
                    }
                }
                report.legacySource64=CheckLegacySource64(route,definition.Prefab,scene);
                Directory.CreateDirectory(IslandTerrainClearanceDiagnostics.Folder);
                report.passed=report.priorTopSamples>0&&report.activeEarlierBuriedBodySamples==0&&
                    report.finalJumpVisible&&report.landingVisible&&report.collectionVisible&&
                    report.thresholdBeforeHidden&&report.thresholdAtVisible&&report.thresholdAfterVisible&&report.legacySource64.passed;
                File.WriteAllText(IslandTerrainClearanceDiagnostics.Folder+"/finish-glade-earlier-overlap.json",JsonUtility.ToJson(report,true));
                Debug.Log("ISLAND_FINISH_GLADE_OVERLAP active="+report.constructedActive+" covered="+report.priorGroundCovered+
                    " bodyBuried="+report.priorBuriedBodySamples+" topBuried="+report.priorBuriedTopSamples+" blockedRays="+report.obstructedRays+
                    " legacySource64="+report.legacySource64.passed);
                if(!report.passed)throw new InvalidOperationException("The final meadow lifetime overlaps an earlier physical tile or misses the true finale.");
            }
            finally {if(node!=null)Object.DestroyImmediate(node);EditorSceneManager.ClosePreviewScene(scene);}
        }

        private static LegacySource64Regression CheckLegacySource64(IslandRoute route,GameObject prefab,Scene scene)
        {
            // Frozen measured geometry provides a positive control even when a new
            // song's final meadow has no footprint over any of its earlier notes.
            const float sourceZ=148.49891662597657f,takeoffZ=1018.2422485351563f,landingZ=1023.2422485351563f;
            const float scale=1.18f,visibleAhead=34,thickness=.30f,groundHeight=1.006160020828247f;
            var center=new Vector3(-29.81938934326172f,1.0117669105529786f,-49.162498474121097f);
            var anchor=new Vector3(-28.740711212158204f,.006160065531730652f,-51.31685256958008f);
            var forward=new Vector3(.5910242795944214f,0,-.8066537380218506f);
            // Only the visibility policy is evaluated on this path. The glade's
            // frame and all sampled geometry use the archived original values.
            var path=new IslandGroundFinishPath(route,0,scale,1,takeoffZ,landingZ,landingZ+29);
            var result=new LegacySource64Regression {chartZ=sourceZ,showThresholdZ=takeoffZ-visibleAhead/scale};
            result.earlierHidden=!path.ShouldPresentGlade(sourceZ,visibleAhead);
            result.finalJumpVisible=path.ShouldPresentGlade(takeoffZ,visibleAhead);
            result.landingVisible=path.ShouldPresentGlade(landingZ,visibleAhead);
            result.collectionVisible=path.ShouldPresentGlade(path.EndChartZ,visibleAhead);
            result.thresholdBeforeHidden=!path.ShouldPresentGlade(result.showThresholdZ-.01f,visibleAhead);
            result.thresholdAtVisible=path.ShouldPresentGlade(result.showThresholdZ,visibleAhead);
            result.thresholdAfterVisible=path.ShouldPresentGlade(result.showThresholdZ+.01f,visibleAhead);
            var node=new GameObject("Original source64 final meadow regression");SceneManager.MoveGameObjectToScene(node,scene);
            try
            {
                var glade=node.AddComponent<IslandFinishGlade>();
                glade.Configure(anchor,forward,groundHeight,34.21992492675781f,14,
                    Borrow(prefab,"Terrain"),Borrow(prefab,"Cliffs"),Borrow(prefab,"Grass"),Borrow(prefab,"Foliage"),71237);
                for(int rim=0;rim<9;rim++)
                {
                    float angle=(rim-1)*Mathf.PI/4;
                    var p=rim==0?center:center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.65f;
                    result.samples++;
                    if(!glade.TrySampleGround(p,out float y))continue;
                    result.groundCovered++;
                    float penetration=y-(p.y-thickness);
                    if(rim==0)result.centerPenetration=penetration;
                    if(penetration<=.001f)continue;
                    result.bodyBuried++;
                    if(path.ShouldPresentGlade(sourceZ,visibleAhead))result.activeEarlierBuriedBodySamples++;
                }
                result.passed=result.samples==9&&result.groundCovered>0&&result.bodyBuried>0&&
                    Mathf.Abs(result.centerPenetration-.2943931221961975f)<.002f&&result.activeEarlierBuriedBodySamples==0&&
                    result.earlierHidden&&result.finalJumpVisible&&result.landingVisible&&result.collectionVisible&&
                    result.thresholdBeforeHidden&&result.thresholdAtVisible&&result.thresholdAfterVisible;
                return result;
            }
            finally {Object.DestroyImmediate(node);}
        }

        private static Material Borrow(GameObject prefab,string suffix)
        {
            foreach(var r in prefab.GetComponentsInChildren<MeshRenderer>(true))foreach(var material in r.sharedMaterials)
                if(material!=null&&material.name.IndexOf("_"+suffix,StringComparison.Ordinal)>=0)return material;
            throw new InvalidOperationException("Missing borrowed meadow material "+suffix);
        }
        private static List<Triangle> ReadTriangles(IslandFinishGlade glade)
        {
            var result=new List<Triangle>();
            foreach(var filter in glade.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;
                if(renderer==null||mesh==null)continue;
                var vertices=mesh.vertices;
                for(int i=0;i<vertices.Length;i++)vertices[i]=filter.transform.TransformPoint(vertices[i]);
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var material=renderer.sharedMaterials[sub];
                    if(!material.name.Contains("Terrain")&&!material.name.Contains("Cliffs"))continue;
                    var ids=mesh.GetTriangles(sub);
                    for(int i=0;i<ids.Length;i+=3)result.Add(new Triangle {a=vertices[ids[i]],b=vertices[ids[i+1]],c=vertices[ids[i+2]]});
                }
            }
            return result;
        }
        private static bool RayHit(List<Triangle> geometry,Vector3 origin,Vector3 target,out float fraction)
        {
            fraction=float.MaxValue;Vector3 direction=target-origin;
            foreach(var f in geometry)
            {
                Vector3 e1=f.b-f.a,e2=f.c-f.a,p=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,p);
                if(Mathf.Abs(det)<1e-9f)continue;
                float inverse=1/det;Vector3 t=origin-f.a;
                float u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)continue;
                Vector3 q=Vector3.Cross(t,e1);float v=Vector3.Dot(direction,q)*inverse;if(v<0||u+v>1)continue;
                float hit=Vector3.Dot(e2,q)*inverse;if(hit<=.0001f||hit>=.9999f||hit>=fraction)continue;
                fraction=hit;
            }
            return fraction<float.MaxValue;
        }
    }
}
