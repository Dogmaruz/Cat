using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone;
using RadioArchipelago.World;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RadioArchipelago.Editor
{
    /// <summary>Editor-only evidence from actual imported solid triangles, not the nominal route height.</summary>
    public static class IslandTerrainClearanceDiagnostics
    {
        public const string Folder = "Library/RadioArchipelago/TerrainClearance";
        [Serializable] private sealed class RouteDocument { public Vector3[] points = Array.Empty<Vector3>(); }
        [Serializable] public sealed class Hit
        {
            public int sourceIndex;
            public string kind, material, mesh;
            public float chartZ, distance, lateral, clearance, surfaceHeight, tileBottom;
            public double gameTime;
            public Vector3 position;
        }
        [Serializable] public sealed class FaceOrientation
        {
            public string mesh, material;
            public int upward, downward, steep;
        }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string checkedUtc, prefabDependencyHash, definitionDependencyHash;
            public string scope = "Actual imported terrain/cliff/bridge and rock triangles; full-width route and exact authored tile surfaces, including curved full Hold bodies and native moving-lane envelopes. Foliage, grass, water and view-clear shaders are not used as ground evidence.";
            public int solidMeshes, triangles, corridorSamples, tileSamples, corridorSurfaceHits, tileSurfaceHits, corridorViolations, tileViolations;
            public int upwardFaces, downwardFaces, steepFaces;
            public float minCorridorClearance = float.MaxValue, minTileClearance = float.MaxValue;
            public List<Hit> worstTiles = new List<Hit>(), worstCorridor = new List<Hit>();
            public List<FaceOrientation> meshOrientations = new List<FaceOrientation>();
        }

        [Serializable] public sealed class Sightline
        {
            public int sourceIndex;
            public float actorChartZ, actorLateral, targetChartZ, fraction;
            public string material, mesh;
            public Vector3 camera, target, intersection;
        }
        [Serializable] public sealed class SightlineReport
        {
            public bool passed;
            public string checkedUtc, definitionDependencyHash;
            public string scope = "Full solid triangle orientations, two-sided ray intersections. Exact fixed camera orbit on both user failure windows: source60..68 and source146..151; camera lateral extremes/centre, actual source X, no collider or foliage assumption.";
            public int rays, obstructed;
            public List<Sightline> evidence = new List<Sightline>();
        }

        [MenuItem("Radio Archipelago/Floating archipelago/Diagnose Follow sight lines")]
        public static void RunSightlines()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Sight-line diagnostics require stable Edit Mode.");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
            var index = new HeightIndex(definition.Prefab, new Report());
            var result = new SightlineReport { checkedUtc = DateTime.UtcNow.ToString("O"),
                definitionDependencyHash = AssetDatabase.GetAssetDependencyHash(IslandAbyssJobs.DefinitionPath).ToString() };
            foreach (var target in map.elements)
            {
                if ((target.sourceIndex < 146 || target.sourceIndex > 151) &&
                    (target.sourceIndex < 60 || target.sourceIndex > 68)) continue;
                var targetFrame = route.Evaluate(definition.StartDistance + target.pos.z * definition.DistanceScale);
                Vector3 point = targetFrame.Position + targetFrame.Right * (target.pos.x * definition.LateralScale) +
                    Vector3.up * (target.pos.y + definition.TileElevation + .025f);
                for (float ahead = .5f; ahead <= 12.01f; ahead += .5f)
                    foreach (float lateral in new[] { -1.3f, 0, 1.3f })
                    {
                        float actorZ = target.pos.z - ahead;
                        var rig = new IslandCloseCameraRig();
                        var pose = rig.Step(route, definition.StartDistance + actorZ * definition.DistanceScale,
                            lateral * definition.LateralScale * definition.CameraLateralFollow, definition.CameraBackDistance,
                            definition.CameraHeight, definition.CameraLookAhead, definition.CameraFocusHeight,
                            definition.CameraHeadingLead, definition.CameraSmoothing, 0, true);
                        result.rays++;
                        if (!index.TryRayHit(pose.Position, point, out float fraction, out string material, out string mesh)) continue;
                        result.obstructed++;
                        if (result.evidence.Count < 400) result.evidence.Add(new Sightline { sourceIndex = target.sourceIndex,
                            actorChartZ = actorZ, actorLateral = lateral * definition.LateralScale,
                            targetChartZ = target.pos.z, fraction = fraction, material = material, mesh = mesh,
                            camera = pose.Position, target = point, intersection = Vector3.Lerp(pose.Position, point, fraction) });
                    }
            }
            result.passed = result.rays > 0 && result.obstructed == 0;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/follow-sightlines.json", JsonUtility.ToJson(result, true));
            Debug.Log("ISLAND_FOLLOW_SIGHTLINES rays=" + result.rays + " obstructed=" + result.obstructed);
        }

        [MenuItem("Radio Archipelago/Floating archipelago/Diagnose actual terrain clearance")]
        public static void RunBaseline()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Terrain clearance requires a stable Edit Mode editor.");
            var definition = AssetDatabase.LoadAssetAtPath<IslandStageDefinition>(IslandAbyssJobs.DefinitionPath);
            var route = new IslandRoute(JsonUtility.FromJson<RouteDocument>(definition.RouteJson.text).points,
                IslandElevationProfile.FromJson(definition.ElevationJson.text));
            var map = JsonUtility.FromJson<MapTimeline>(definition.Chart.text);
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O"),
                prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(definition.Prefab)).ToString(),
                definitionDependencyHash = AssetDatabase.GetAssetDependencyHash(IslandAbyssJobs.DefinitionPath).ToString() };
            var index = new HeightIndex(definition.Prefab, report);
            // Includes an ordinary disc rim outside the most extreme native input lane.
            float width = NativeRhythmRules.HorizontalLimit * definition.LateralScale + .67f;
            for (float s = 0; s < route.Length; s += .25f)
            {
                var frame = route.Evaluate(s);
                for (int laneSample = 0; laneSample <= 16; laneSample++)
                {
                    float lane = Mathf.Lerp(-width, width, laneSample / 16f);
                    Observe(index, report, frame.Position + frame.Right * lane + Vector3.up * definition.TileElevation,
                        definition.TileThickness, -1, "corridor", 0, s, lane, 0, true);
                }
            }
            foreach (var e in map.elements)
            {
                if (e == null || e.id == 7 || e.id == 8 || e.id == 10) continue;
                float end = e.pos.z;
                foreach (var segment in map.segments)
                    if (segment.sourceIndex == e.sourceIndex && (segment.kind == "Hold" || segment.kind == "Follow"))
                    { end = segment.endZ; break; }
                if ((e.id == 2 || e.id == 3 || e.id == 4) && end > e.pos.z + .02f)
                {
                    var support = new IslandHoldSurface(route, definition, e.pos.z, end, e.pos.x, e.pos.y, .65f);
                    try
                    {
                        var vertices = support.Mesh.vertices;
                        var uv = support.Mesh.uv;
                        int contour = (vertices.Length - 4) / 6;
                        for (int v = contour * 5; v < contour * 6; v++)
                        {
                            float z = Mathf.LerpUnclamped(e.pos.z, end, uv[v].y);
                            Observe(index, report, support.Pivot + vertices[v], definition.TileThickness, e.sourceIndex, e.kind,
                                z, definition.StartDistance + z * definition.DistanceScale, e.pos.x * definition.LateralScale,
                                e.gameTime, false);
                        }
                        for (float z = e.pos.z; z <= end; z += .15f)
                        {
                            var frame = route.Evaluate(definition.StartDistance + z * definition.DistanceScale);
                            float y = Mathf.Lerp(route.Evaluate(definition.StartDistance + e.pos.z * definition.DistanceScale).Position.y,
                                route.Evaluate(definition.StartDistance + end * definition.DistanceScale).Position.y, (z-e.pos.z)/(end-e.pos.z));
                            var p = frame.Position + frame.Right * (e.pos.x * definition.LateralScale);
                            p.y = y + e.pos.y + definition.TileElevation;
                            Observe(index, report, p, definition.TileThickness, e.sourceIndex, e.kind, z,
                                definition.StartDistance + z * definition.DistanceScale, e.pos.x * definition.LateralScale, e.gameTime, false);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(support.Mesh); }
                }
                else
                {
                    // Static landing and the entire native sine-lane envelope while approaching/being ridden.
                    int phases = e.updateType == 3 ? 33 : e.updateType == 1 ? 2 : 1;
                    for (int phase = 0; phase < phases; phase++)
                    {
                        float x = e.updateType == 3 ? Mathf.Sin(2*Mathf.PI*phase/(phases-1)) :
                            e.updateType == 1 && phase == 0 ? e.mstartPos.x : e.pos.x;
                        var frame = route.Evaluate(definition.StartDistance + e.pos.z * definition.DistanceScale);
                        var p = frame.Position + frame.Right * (x * definition.LateralScale) + Vector3.up * (e.pos.y + definition.TileElevation);
                        for (int rim = 0; rim < 9; rim++)
                        {
                            float angle = (rim-1) * Mathf.PI/4;
                            Vector3 q = rim == 0 ? p : p + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)) * .65f;
                            Observe(index, report, q, definition.TileThickness, e.sourceIndex, e.kind, e.pos.z,
                                definition.StartDistance + e.pos.z * definition.DistanceScale, x * definition.LateralScale, e.gameTime, false);
                        }
                    }
                }
            }
            report.worstTiles.Sort((a,b)=>a.clearance.CompareTo(b.clearance));
            report.worstCorridor.Sort((a,b)=>a.clearance.CompareTo(b.clearance));
            report.passed = report.tileViolations == 0 && report.corridorViolations == 0 &&
                report.solidMeshes > 0 && report.triangles > 1000 && report.corridorSurfaceHits > 100 && report.tileSurfaceHits > 100;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/terrain-clearance-baseline.json", JsonUtility.ToJson(report, true));
            Debug.Log("ISLAND_TERRAIN_CLEARANCE baseline tile violations=" + report.tileViolations +
                " min=" + report.minTileClearance + " corridor=" + report.corridorViolations + " meshes=" + report.solidMeshes);
        }

        private static void Observe(HeightIndex index, Report report, Vector3 top, float thickness, int source, string kind,
            float chartZ, float distance, float lane, double gameTime, bool corridor)
        {
            if (corridor) report.corridorSamples++; else report.tileSamples++;
            if (!index.TryHeight(top, !corridor, out float y, out string material, out string mesh)) return;
            if (corridor) report.corridorSurfaceHits++; else report.tileSurfaceHits++;
            float bottom = top.y - thickness;
            float clearance = bottom - y;
            if (corridor) report.minCorridorClearance = Mathf.Min(report.minCorridorClearance, clearance);
            else report.minTileClearance = Mathf.Min(report.minTileClearance, clearance);
            if (clearance >= .15f) return;
            if (corridor) report.corridorViolations++; else report.tileViolations++;
            var hits = corridor ? report.worstCorridor : report.worstTiles;
            var hit = new Hit { sourceIndex = source, kind = kind, chartZ = chartZ, distance = distance,
                lateral = lane, gameTime = gameTime, position = top, tileBottom = bottom,
                surfaceHeight = y, clearance = clearance, material = material, mesh = mesh };
            if (hits.Count < 400) hits.Add(hit);
            else
            {
                int worst = 0;
                for (int i = 1; i < hits.Count; i++) if (hits[i].clearance > hits[worst].clearance) worst = i;
                if (clearance < hits[worst].clearance) hits[worst] = hit;
            }
        }

        internal sealed class HeightIndex
        {
            private const float Cell = 3f;
            private struct Face { public Vector3 a,b,c; public string material,mesh; public bool terrain, upper; }
            private readonly List<Face> faces = new List<Face>();
            private readonly Dictionary<long,List<int>> bins = new Dictionary<long,List<int>>();
            public HeightIndex(GameObject prefab, Report report, bool includeStructures = false)
            {
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null) continue;
                    var materials = renderer.sharedMaterials;
                    bool any = false;
                    foreach (var material in materials) if (Solid(material?.name) || includeStructures && Structure(material)) { any = true; break; }
                    if (!any) continue;
                    report.solidMeshes++;
                    var mesh = filter.sharedMesh;
                    using (var all = MeshUtility.AcquireReadOnlyMeshData(mesh))
                    using (var vertices = new NativeArray<Vector3>(all[0].vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                    {
                        var data = all[0]; data.GetVertices(vertices);
                        var world = new Vector3[vertices.Length];
                        for (int i=0;i<vertices.Length;i++) world[i] = filter.transform.TransformPoint(vertices[i]);
                        for (int sub=0;sub<data.subMeshCount;sub++)
                        {
                            string mat = sub<materials.Length ? materials[sub]?.name : null;
                            if (!Solid(mat) && !(includeStructures && Structure(sub < materials.Length ? materials[sub] : null))) continue;
                            var orientation=new FaceOrientation {mesh=filter.name,material=mat};report.meshOrientations.Add(orientation);
                            var descriptor = data.GetSubMesh(sub);
                            for (int i=descriptor.indexStart;i<descriptor.indexStart+descriptor.indexCount;i+=3)
                            {
                                int ia,ib,ic;
                                if (data.indexFormat == IndexFormat.UInt16)
                                { var ids=data.GetIndexData<ushort>();ia=ids[i];ib=ids[i+1];ic=ids[i+2]; }
                                else { var ids=data.GetIndexData<int>();ia=ids[i];ib=ids[i+1];ic=ids[i+2]; }
                                Vector3 a=world[ia+descriptor.baseVertex],b=world[ib+descriptor.baseVertex],c=world[ic+descriptor.baseVertex];
                                Vector3 normal=Vector3.Cross(b-a,c-a);
                                if (normal.sqrMagnitude < 1e-14f) continue;
                                bool horizontalEnough=normal.y*normal.y>=normal.sqrMagnitude*.01f;
                                if(!horizontalEnough){report.steepFaces++;orientation.steep++;}
                                else if(normal.y>0){report.upwardFaces++;orientation.upward++;}
                                else {report.downwardFaces++;orientation.downward++;}
                                // Imported mirrored FBX wrappers can invert world triangle winding.
                                // Height must depend on physical intersections, never winding sign.
                                int id=faces.Count;faces.Add(new Face {a=a,b=b,c=c,material=mat,mesh=filter.name,terrain=Terrain(mat),
                                    upper=horizontalEnough});
                                int minX=Coord(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),maxX=Coord(Mathf.Max(a.x,Mathf.Max(b.x,c.x)));
                                int minZ=Coord(Mathf.Min(a.z,Mathf.Min(b.z,c.z))),maxZ=Coord(Mathf.Max(a.z,Mathf.Max(b.z,c.z)));
                                for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
                                {long key=Key(x,z);if(!bins.TryGetValue(key,out var bin))bins.Add(key,bin=new List<int>());bin.Add(id);}
                            }
                        }
                    }
                }
                report.triangles=faces.Count;
            }
            public bool TryHeight(Vector3 point, bool includeRocks, out float height, out string material, out string mesh)
            {
                height=float.NegativeInfinity;material=mesh=null;
                if(!bins.TryGetValue(Key(Coord(point.x),Coord(point.z)),out var bin))return false;
                foreach(int id in bin)
                {
                    Face f=faces[id];if(!f.upper||!includeRocks&&!f.terrain)continue;
                    double bx=f.b.x-f.a.x,bz=f.b.z-f.a.z,cx=f.c.x-f.a.x,cz=f.c.z-f.a.z;
                    double dx=point.x-f.a.x,dz=point.z-f.a.z,den=bx*cz-bz*cx;
                    if(Math.Abs(den)<1e-10)continue;
                    double u=(dx*cz-dz*cx)/den,v=(bx*dz-bz*dx)/den;
                    if(u < -.000001 || v < -.000001 || u+v > 1.000001)continue;
                    float y=f.a.y+(float)u*(f.b.y-f.a.y)+(float)v*(f.c.y-f.a.y);
                    if(y>point.y+30 || y<height)continue;
                    height=y;material=f.material;mesh=f.mesh;
                }
                return !float.IsNegativeInfinity(height);
            }
            public bool TryRayHit(Vector3 origin, Vector3 target, out float fraction, out string material, out string mesh)
            {
                fraction=float.MaxValue;material=mesh=null;
                Vector3 direction=target-origin;
                var visited=new HashSet<int>();
                for(int x=Coord(Mathf.Min(origin.x,target.x));x<=Coord(Mathf.Max(origin.x,target.x));x++)
                    for(int z=Coord(Mathf.Min(origin.z,target.z));z<=Coord(Mathf.Max(origin.z,target.z));z++)
                    {
                        if(!bins.TryGetValue(Key(x,z),out var bin))continue;
                        foreach(int id in bin)
                        {
                            if(!visited.Add(id))continue;
                            Face f=faces[id];Vector3 e1=f.b-f.a,e2=f.c-f.a;
                            Vector3 p=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,p);
                            if(Mathf.Abs(det)<1e-9f)continue;
                            float inverse=1/det;Vector3 t=origin-f.a;
                            float u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)continue;
                            Vector3 q=Vector3.Cross(t,e1);float v=Vector3.Dot(direction,q)*inverse;
                            if(v<0||u+v>1)continue;
                            float hit=Vector3.Dot(e2,q)*inverse;
                            if(hit<=.0001f||hit>=.9999f||hit>=fraction)continue;
                            fraction=hit;material=f.material;mesh=f.mesh;
                        }
                    }
                return material!=null;
            }
            private static bool Terrain(string name)=>name!=null&&(name.Contains("Terrain")||name.Contains("Cliffs")||name.Contains("BridgeDeck"));
            private static bool Solid(string name)=>Terrain(name)||name!=null&&name.Contains("Rocks");
            private static bool Structure(Material material) => material != null && material.renderQueue < 3000 &&
                !material.name.Contains("Grass") && !material.name.Contains("Foliage") &&
                !material.name.Contains("Leaves") && !material.name.Contains("Water") &&
                !material.name.Contains("Cloud") && !material.name.Contains("Glow");
            private static int Coord(float value)=>Mathf.FloorToInt(value/Cell);
            private static long Key(int x,int z)=>((long)x<<32)|(uint)z;
        }
    }
}
