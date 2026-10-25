using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    /// <summary>Independent, spatial-only phrasing of an already measured chart. No reference map or song identity is read.</summary>
    public static class ChartRouteComposer
    {
        public const string Version = "phrase-route-v1";
        private const string NotePrefix = "Route policy: ";
        private static readonly float[][] Motifs =
        {
            new[] { -1f, -1f, 1f, 1f, 0f, -.5f, -.5f, .5f },
            new[] { -1f, -.5f, 0f, .5f, 1f, .5f, 0f, -.5f },
            new[] { 0f, .75f, .75f, 0f, -.75f, -.75f, 0f, .5f },
            new[] { -.5f, 0f, .5f, 0f, -.5f, -.5f, .5f, .5f },
            new[] { -.75f, -.75f, .75f, .75f, .25f, .25f, -.5f, -.5f }
        };

        private sealed class Node
        {
            public MapSegment segment;
            public MapElement element, exit;
            public bool fixedLane;
            public float desired, lower, upper;
            public double Time => segment.audioTime;
            public double End => segment.endAudioTime;
            public float Exit => fixedLane && segment.kind != "Start" && segment.kind != "FinishJump" ? LandingX(exit) : desired;
        }
        private sealed class MovingRun { public int first, count; }

        /// <summary>The cycle's stored X is phase; its contact X is the sine at distanceAhead=0.</summary>
        public static float LandingX(MapElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            return element.updateType == 3 ? (float)Math.Sin(2 * Math.PI * element.pos.x / 5) : element.pos.x;
        }

        /// <summary>Conservative authoring envelope informed by source-chart transition distributions, not an engine limit.</summary>
        public static float MaxLateralShift(double gap)
        {
            if (double.IsNaN(gap) || double.IsInfinity(gap) || gap < -.00001)
                throw new InvalidDataException("Lateral choreography requires a finite non-overlapping support gap.");
            return gap < .18 - .00001 ? .5f : gap < .30 - .00001 ? 1f : 2f;
        }

        public static void Apply(MapTimeline map) => Compose(map, true);

        /// <summary>Keep spatial phrasing when the caller explicitly disables moving jump tiles.</summary>
        public static void ApplyStationary(MapTimeline map) => Compose(map, false);

        private static void Compose(MapTimeline map, bool includeSideways)
        {
            if (map == null || map.elements == null || map.segments == null || map.elements.Length == 0)
                throw new ArgumentException("A compiled chart is required for spatial phrasing.", nameof(map));
            var nodes = map.segments.Where(s => s != null && s.kind != "Theme").OrderBy(s => s.audioTime).Select(s =>
            {
                if (s.elementIndices == null || s.elementIndices.Length == 0 || s.sourceIndex < 0 || s.sourceIndex >= map.elements.Length
                    || s.elementIndices.Any(i => i < 0 || i >= map.elements.Length))
                    throw new InvalidDataException("A route segment does not address its native elements.");
                var first=map.elements[s.sourceIndex];var exit=map.elements[s.elementIndices.Last()];
                bool fixedLane=s.kind=="Start"||s.kind=="FinishJump"||s.kind=="Follow"||!IsAssignable(first);
                float lane=s.kind=="Start"||s.kind=="FinishJump"?0:LandingX(first);
                return new Node { segment=s,element=first,exit=exit,fixedLane=fixedLane,
                    desired=fixedLane?lane:0,lower=fixedLane?lane:-1,upper=fixedLane?lane:1 };
            }).ToArray();
            if (nodes.Length < 2) throw new InvalidDataException("A spatial chart needs at least two support segments.");
            double beat=map.tempoSections!=null&&map.tempoSections.Length>0&&map.sampleRate>0
                ? map.tempoSections[0].samplesPerBeat/map.sampleRate : .5;
            if(double.IsNaN(beat)||double.IsInfinity(beat)||beat<.15||beat>2)beat=.5;
            PhraseTargets(nodes,beat);
            Constrain(nodes);
            // All constraints are solved before changing serialized objects. A fixed native
            // Follow path is never bent to repair an incompatible approach.
            foreach(var node in nodes.Where(n=>n.segment.kind!="Follow"))
            {
                if(!node.fixedLane||node.segment.kind=="Start"||node.segment.kind=="FinishJump")
                {
                    var at=node.element.pos;at.x=node.desired;node.element.pos=at;
                    node.element.updateType=0;node.element.msLeft=0;node.element.mstartPos=Vector3.zero;
                }
            }
            if(includeSideways)AddMovingRuns(nodes,beat);
            foreach(var node in nodes.Where(n=>n.segment.kind!="Follow"))Sync(node);
            map.normalizationNotes=(map.normalizationNotes??Array.Empty<string>()).Where(n=>n==null||!n.StartsWith(NotePrefix,StringComparison.Ordinal))
                .Concat(new[]{NotePrefix+Version+"; rhythmic cells reuse lateral motifs with mirrored replies, rapid notes retain small/repeated steps, "
                    +"wide turns require available flight time. "
                    +(includeSideways?"Native sine-moving Jump/BigJump runs use inverse phase encoding; ":"Sideways-moving jump tiles are disabled; ")
                    +"existing Follow paths and all musical times remain unchanged."}).ToArray();
        }

        private static void PhraseTargets(Node[] nodes,double beat)
        {
            var occurrences=new Dictionary<string,int>(StringComparer.Ordinal);
            int start=0;
            while(start<nodes.Length)
            {
                if(nodes[start].fixedLane){start++;continue;}
                int end=start+1;
                while(end<nodes.Length&&!nodes[end].fixedLane&&nodes[end].Time-nodes[start].Time<4*beat-.00001
                    &&nodes[end].Time-nodes[end-1].End<1.6*beat)end++;
                string signature=string.Join(",",Enumerable.Range(start,end-start).Select(i=>
                    nodes[i].element.kind+":"+RhythmBin((nodes[i].Time-nodes[start].Time)/beat*4)+":"+RhythmBin((nodes[i].End-nodes[i].Time)/beat*4)));
                int occurrence=occurrences.TryGetValue(signature,out int seen)?seen:0;occurrences[signature]=occurrence+1;
                uint hash=2166136261;
                foreach(char value in signature)hash=unchecked((hash^value)*16777619);
                var motif=Motifs[hash%(uint)Motifs.Length];float mirror=occurrence%2==0?1:-1;
                for(int i=start;i<end;i++)
                {
                    int local=i-start;float target=motif[local%motif.Length]*mirror;
                    float previous=i==0?0:nodes[i-1].Exit;
                    double gap=i==0?beat:nodes[i].Time-nodes[i-1].End;
                    if(gap<.18-.00001)target=previous+(local%3==2?Math.Sign(target-previous)*.25f:0);
                    else if(gap<.30-.00001)target=local%2==1?previous:previous+Math.Sign(target-previous)*.5f;
                    // Give the first few real notes a readable opening before broad turns.
                    if(i<=3)target=Clamp(target,-.5f,.5f);
                    nodes[i].desired=Clamp(target,-1,1);
                }
                start=end;
            }
        }

        private static void Constrain(Node[] nodes)
        {
            for(int i=nodes.Length-2;i>=0;i--)
            {
                float shift=MaxLateralShift(nodes[i+1].Time-nodes[i].End);
                float lower=nodes[i+1].lower-shift,upper=nodes[i+1].upper+shift;
                if(nodes[i].fixedLane)
                {
                    float exit=nodes[i].segment.kind=="Start"||nodes[i].segment.kind=="FinishJump"?0:LandingX(nodes[i].exit);
                    if(exit<lower-.0001f||exit>upper+.0001f)
                        throw new InvalidDataException("Fixed support paths leave no reachable lateral approach; preserve the path and review its neighboring cues.");
                }
                else
                {
                    nodes[i].lower=Math.Max(nodes[i].lower,lower);nodes[i].upper=Math.Min(nodes[i].upper,upper);
                    if(nodes[i].lower>nodes[i].upper+.0001f)throw new InvalidDataException("No feasible lateral route exists between fixed supports.");
                }
            }
            for(int i=0;i<nodes.Length;i++)
            {
                float lower=nodes[i].lower,upper=nodes[i].upper;
                if(i>0)
                {
                    float shift=MaxLateralShift(nodes[i].Time-nodes[i-1].End),previous=nodes[i-1].Exit;
                    lower=Math.Max(lower,previous-shift);upper=Math.Min(upper,previous+shift);
                }
                if(lower>upper+.0001f)throw new InvalidDataException("Fixed support entry is unreachable in the available flight interval.");
                nodes[i].desired=Clamp(nodes[i].desired,lower,upper);
            }
        }

        private static void AddMovingRuns(Node[] nodes,double beat)
        {
            var runs=new List<MovingRun>();double prior=-100;
            for(int i=2;i+4<nodes.Length;i++)
            {
                if(nodes[i].Time<4-.00001||nodes[i].Time>nodes.Last().Time-3+.00001||nodes[i].Time-prior<4*beat-.00001)continue;
                if(!IsJump(nodes[i-1])||!IsJump(nodes[i])||!IsJump(nodes[i+1])||!IsJump(nodes[i+2]))continue;
                double interval=nodes[i].Time-nodes[i-1].End;
                if(interval<.30-.00001||interval>.8+.00001)continue;
                int length=2;
                bool Regular(int at)=>IsJump(nodes[at])&&Math.Abs(nodes[at].Time-nodes[at-1].End-interval)<=.055+.00001;
                if(!Regular(i+1)||!Regular(i+2))continue;
                if(Regular(i+3)&&i+4<nodes.Length&&Regular(i+4))length=3;
                runs.Add(new MovingRun{first=i,count=length});prior=nodes[i].Time;i+=length;
            }
            int supports=nodes.Count(n=>IsJump(n));
            int count=Math.Min(runs.Count,Math.Max(0,(int)Math.Round(supports*.14/2.5)));
            for(int k=0;k<count;k++)
            {
                int index=count==1?runs.Count/2:(int)Math.Round((double)k*(runs.Count-1)/(count-1));
                var run=runs[index];
                for(int j=0;j<run.count;j++)
                {
                    var node=nodes[run.first+j];float impact=Clamp(node.desired,-.9f,.9f);
                    var at=node.element.pos;at.x=(float)(Math.Asin(impact)*5/(2*Math.PI));node.element.pos=at;
                    node.element.updateType=3;node.element.msLeft=k%2;
                }
            }
        }

        private static void Sync(Node node)
        {
            var e=node.element;var segment=node.segment;segment.x=e.pos.x;
            if(segment.motion==null)segment.motion=new MotionParameters();
            segment.motion.updateType=e.updateType;segment.motion.msLeft=e.msLeft;segment.motion.mstartPos=e.mstartPos;
            segment.motion.begin=e.begin;segment.motion.end=e.end;segment.motion.mfendPos=e.mfendPos;
            segment.motion.se=e.se;segment.motion.ext=e.ext;
        }
        private static bool IsAssignable(MapElement element)=>element.kind=="Jump"||element.kind=="BigJump"||element.kind=="Hold";
        private static bool IsJump(Node node)=>node.element.kind=="Jump"||node.element.kind=="BigJump";
        // Exact half-grid positions occur in real subdivisions. Keep a serializer's
        // submicrosecond rounding from selecting a different motif or moving run.
        private static int RhythmBin(double position)=>(int)Math.Floor(position+.5+.00001);
        private static float Clamp(float value,float minimum,float maximum)=>Math.Max(minimum,Math.Min(maximum,value));
    }
}
