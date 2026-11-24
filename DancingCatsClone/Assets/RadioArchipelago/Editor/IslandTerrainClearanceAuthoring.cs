using System;
using System.Collections.Generic;
using System.IO;
using DancingCats.Clone;
using RadioArchipelago.World;
using UnityEngine;

namespace RadioArchipelago.Editor
{
    /// <summary>Height-only local corrections from actual scenery. Never moves or retimes musical elements.</summary>
    public static class IslandTerrainClearanceAuthoring
    {
        private const float TargetBodyClearance=.50f, ConeGrade=.30f, RoundedPeak=2f, Sampling=.20f;
        [Serializable] private sealed class RouteDocument { public Vector3[] points=Array.Empty<Vector3>(); }
        [Serializable] public sealed class Report
        {
            public int scenerySamples, constraints, outputKeys;
            public float targetBodyClearance=TargetBodyClearance, maxLift, maxGrade, minimumConstraintReserve=float.MaxValue;
            public string policy="Only positive actual-triangle clearance deficits; local periodic rounded-cone upper envelope, then full-support chord authoring. Musical layout is already compiled and remains unchanged.";
        }
        private struct Constraint { internal float distance,height; }

        public static IslandElevationProfile.Document BuildFloor(GameObject prefab,string routeJson,
            IslandElevationProfile.Document source,MapTimeline authoredChart,float startDistance,float distanceScale,
            float lateralScale,float tileElevation,float thickness)
        {
            if(prefab==null)throw new ArgumentNullException(nameof(prefab));
            var report=new Report();
            var old=new IslandElevationProfile(source.length,source.closed,source.keys);
            var route=new IslandRoute(JsonUtility.FromJson<RouteDocument>(routeJson).points);
            var index=new IslandTerrainClearanceDiagnostics.HeightIndex(prefab,new IslandTerrainClearanceDiagnostics.Report());
            var constraints=new List<Constraint>();
            float length=source.length;
            void Probe(float distance,float lane,bool rocks)
            {
                var frame=route.Evaluate(distance);
                Vector3 p=frame.Position+frame.Right*lane;
                p.y=old.Evaluate(distance)+tileElevation;
                report.scenerySamples++;
                if(!index.TryHeight(p,rocks,out float ground,out _,out _))return;
                float needed=ground+TargetBodyClearance+thickness-tileElevation;
                if(needed<=old.Evaluate(distance)+.0001f)return;
                constraints.Add(new Constraint {distance=Wrap(distance,length),height=needed+.012f});
            }
            float width=NativeRhythmRules.HorizontalLimit*lateralScale+.67f;
            int count=Mathf.CeilToInt(length/Sampling);
            for(int i=0;i<count;i++)
            {
                float s=length*i/count;
                // Structural clearance spans the whole playable corridor. Decorative rocks
                // are considered only where an actual support can be, avoiding scenery-wide lifts.
                for(int lane=0;lane<=16;lane++)Probe(s,Mathf.Lerp(-width,width,lane/16f),false);
            }
            var segments=new Dictionary<int,MapSegment>();
            foreach(var segment in authoredChart.segments)
                if(segment!=null&&(segment.kind=="Hold"||segment.kind=="Follow"))segments[segment.sourceIndex]=segment;
            foreach(var e in authoredChart.elements)
            {
                if(e==null||e.id==7||e.id==8||e.id==10)continue;
                if((e.id==2||e.id==3||e.id==4)&&segments.TryGetValue(e.sourceIndex,out var segment))
                {
                    float a=startDistance+e.pos.z*distanceScale-.67f,b=startDistance+segment.endZ*distanceScale+.67f;
                    int rows=Mathf.Max(2,Mathf.CeilToInt((b-a)/.15f));
                    for(int row=0;row<=rows;row++)for(int side=-1;side<=1;side++)
                        Probe(Mathf.Lerp(a,b,row/(float)rows),e.pos.x*lateralScale+side*.65f,true);
                }
                else
                {
                    int phases=e.updateType==3?33:1;
                    for(int phase=0;phase<phases;phase++)
                    {
                        float x=phases>1?Mathf.Sin(2*Mathf.PI*phase/(phases-1)):e.pos.x;
                        float s=startDistance+e.pos.z*distanceScale;
                        var frame=route.Evaluate(s);
                        // Rims are projected back into route-distance/lateral offsets;
                        // sub-metre route curvature is also covered by the structural corridor.
                        for(int rim=0;rim<9;rim++)
                        {
                            float angle=(rim-1)*Mathf.PI/4;
                            float longitudinal=rim==0?0:Mathf.Sin(angle)*.65f;
                            float lateral=rim==0?0:Mathf.Cos(angle)*.65f;
                            Probe(s+longitudinal,x*lateralScale+lateral,true);
                        }
                    }
                }
            }
            report.constraints=constraints.Count;
            if(constraints.Count==0)return source;
            float Height(float s)
            {
                float h=old.Evaluate(s);
                foreach(var c in constraints)
                {
                    float d=Mathf.Abs(s-c.distance);d=Mathf.Min(d,length-d);
                    // Rounded peaks avoid a cusp at a local bridge/rock constraint.
                    // The max envelope has no influence once it falls below the old route.
                    h=Mathf.Max(h,c.height-ConeGrade*(Mathf.Sqrt(d*d+RoundedPeak*RoundedPeak)-RoundedPeak));
                }
                return h;
            }
            var stations=new SortedSet<float>{0,length};
            for(int i=0;i<count;i++)stations.Add(length*i/count);
            foreach(var k in source.keys)stations.Add(k.distance);
            foreach(var c in constraints)stations.Add(c.distance);
            IslandElevationProfile.Document result=null;
            IslandElevationProfile profile=null;
            for(int pass=0;pass<=7;pass++)
            {
                var keys=new List<IslandElevationProfile.Key>(stations.Count);
                foreach(float s in stations)if(s<length)keys.Add(new IslandElevationProfile.Key(s,Height(s)));
                keys.Add(new IslandElevationProfile.Key(length,keys[0].height));
                result=new IslandElevationProfile.Document{closed=true,length=length,keys=keys.ToArray()};
                profile=new IslandElevationProfile(length,true,result.keys);
                var refine=new List<float>();
                for(int i=0;i+1<keys.Count;i++)for(int q=1;q<4;q++)
                {
                    float s=Mathf.Lerp(keys[i].distance,keys[i+1].distance,q/4f);
                    if(profile.Evaluate(s)<old.Evaluate(s)-.0001f)refine.Add(s);
                }
                if(refine.Count==0)break;
                int before=stations.Count;foreach(float s in refine)stations.Add(s);if(stations.Count==before)break;
            }
            foreach(var c in constraints)
                report.minimumConstraintReserve=Mathf.Min(report.minimumConstraintReserve,profile.Evaluate(c.distance)-c.height+.012f);
            for(float s=0;s<length;s+=.025f)
            {report.maxLift=Mathf.Max(report.maxLift,profile.Evaluate(s)-old.Evaluate(s));report.maxGrade=Mathf.Max(report.maxGrade,Mathf.Abs(profile.Grade(s)));}
            report.outputKeys=result.keys.Length;
            Directory.CreateDirectory(IslandTerrainClearanceDiagnostics.Folder);
            File.WriteAllText(IslandTerrainClearanceDiagnostics.Folder+"/terrain-elevation-correction.json",JsonUtility.ToJson(report,true));
            if(report.minimumConstraintReserve<-.001f||report.maxGrade>.75f)
                throw new InvalidDataException("Actual-terrain source floor failed clearance/grade constraints: "+JsonUtility.ToJson(report));
            return result;
        }
        private static float Wrap(float s,float length){s%=length;if(s<0)s+=length;return s;}
    }
}
