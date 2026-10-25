using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class ChartRouteComposerValidation
    {
        [MenuItem("Dancing Cats/Validate phrase route composer")]
        public static void RunChecks()
        {
            var composer = typeof(ChartRouteComposerValidation).Assembly.GetType("DancingCats.Clone.Editor.ChartRouteComposer");
            Require(composer != null, "A spatial route composer is required; central-only PCM output is not choreography.");
            void Apply(MapTimeline map) => composer.GetMethod("Apply").Invoke(null, new object[] { map });
            float Landing(MapElement element) => (float)composer.GetMethod("LandingX").Invoke(null, new object[] { element });
            float Limit(double gap) => (float)composer.GetMethod("MaxLateralShift").Invoke(null, new object[] { gap });

            var map = Fixture();
            string acoustic = AcousticFingerprint(map), follow = JsonUtility.ToJson(map.segments.Single(s => s.kind == "Follow"));
            var followElements = map.segments.Single(s => s.kind == "Follow").elementIndices.Select(i => JsonUtility.ToJson(map.elements[i])).ToArray();
            var renamed = JsonUtility.FromJson<MapTimeline>(JsonUtility.ToJson(map)); renamed.id = "UnrelatedIdentity";
            Apply(map); Apply(renamed);
            Require(AcousticFingerprint(map) == acoustic, "Layout must preserve every event, cue boundary, Z, arc, tick and tile type.");
            Require(JsonUtility.ToJson(map.segments.Single(s => s.kind == "Follow")) == follow,
                "The complete accepted Follow segment and its measured path must remain exact.");
            Require(followElements.SequenceEqual(map.segments.Single(s => s.kind == "Follow").elementIndices.Select(i => JsonUtility.ToJson(map.elements[i]))),
                "Native Follow entry, midpoint and exit elements must not acquire a different route or motion.");
            Require(Layout(map) == Layout(renamed), "Song identity cannot select or seed a lateral route. " + FirstDifference(map,renamed));
            var precision=Fixture();
            foreach(var element in precision.elements) { double epsilon=(element.sourceIndex%2==0?1:-1)*.0000001;element.audioTime+=epsilon;element.gameTime+=epsilon; }
            foreach(var segment in precision.segments) { double epsilon=(segment.sourceIndex%2==0?1:-1)*.0000001;segment.audioTime+=epsilon;segment.endAudioTime+=epsilon;segment.gameTime+=epsilon;segment.endGameTime+=epsilon; }
            Apply(precision);
            Require(Layout(map)==Layout(precision), "Submicrosecond serializer rounding must not select another rhythmic motif. "+FirstDifference(map,precision));
            string once = JsonUtility.ToJson(map); Apply(map);
            Require(JsonUtility.ToJson(map) == once, "Reapplying layout must be idempotent, including phase encoding and provenance.");
            var landing = map.segments.Where(s => s.kind == "Jump" || s.kind == "BigJump" || s.kind == "Hold")
                .Select(s => Landing(map.elements[s.sourceIndex])).ToArray();
            Require(landing.Count(x => x < -.45f) >= 10 && landing.Count(x => x > .45f) >= 10,
                "Both sides must be visible instead of a central route.");
            Require(landing.Zip(landing.Skip(1), (a,b) => Math.Abs(a-b) < .001).Count(same => same) >= 8,
                "Real rhythmic cells must retain repeated positions instead of mandatory zigzags.");
            Require(map.elements.First().pos.x == 0 && map.elements.Last().pos.x == 0,
                "Start and the final five-unit platform approach stay centered.");
            var movers = map.elements.Where(e => e.updateType == 3).ToArray();
            Require(movers.Length >= 4 && movers.Any(e => e.msLeft == 0) && movers.Any(e => e.msLeft == 1),
                "Spaced regular phrases must expose moving tile runs in both directions.");
            foreach (var element in movers)
            {
                Require(element.kind == "Jump" || element.kind == "BigJump", "Holds and Follow markers must not become cycle movers.");
                Require(Math.Abs(element.pos.x) <= 1.001f && Math.Abs(Landing(element)) <= .90001f,
                    "The phase parameter and actual impact lane both remain bounded.");
                double atImpact = Math.Sin(2 * Math.PI * element.pos.x / 5);
                Require(Math.Abs(atImpact-Landing(element)) < .000001, "Contact X is the native sine, not its phase parameter.");
                double before = Math.Sin(2 * Math.PI * (element.pos.x/5.0 + (element.msLeft == 1 ? -2.0 : 2.0)/30));
                double after = Math.Sin(2 * Math.PI * (element.pos.x/5.0 + (element.msLeft == 1 ? 2.0 : -2.0)/30));
                Require(Math.Abs(before-after) > .10, "A moving tile must actually change position while approaching its impact.");
            }
            for (int i = 0; i + 1 < map.segments.Length; i++)
            {
                var segment = map.segments[i]; var next = map.segments[i+1];
                float exit = Landing(map.elements[segment.elementIndices.Last()]);
                float entry = Landing(map.elements[next.sourceIndex]);
                Require(Math.Abs(entry-exit) <= Limit(next.audioTime-segment.endAudioTime)+.0001,
                    "Effective landing lanes must respect time available after the real support exit.");
                var element = map.elements[segment.sourceIndex];
                Require(segment.x == element.pos.x && segment.motion.updateType == element.updateType
                    && segment.motion.msLeft == element.msLeft, "Element and segment movement metadata must agree.");
            }
            Require(map.elements.Where(e => e.kind == "Hold").All(e => e.updateType == 0), "Held supports stay stable.");
            Require(Limit(.125) <= .5f && Limit(.25) <= 1 && Limit(.4) <= 2,
                "Rapid articulation must not require a wide cross-lane swipe.");
            var stationaryMethod=composer.GetMethod("ApplyStationary");
            Require(stationaryMethod!=null,"Disabling moving jumps needs a stationary lateral composition mode.");
            var stationary=Fixture();string stationaryAcoustic=AcousticFingerprint(stationary);
            stationaryMethod.Invoke(null,new object[]{stationary});
            Require(stationary.elements.All(e=>e.updateType!=3),"Stationary mode must not emit any side-to-side tile cycles.");
            Require(stationary.elements.Any(e=>e.pos.x<-.45f)&&stationary.elements.Any(e=>e.pos.x>.45f),
                "Disabling moving tiles must retain the two-sided static choreography.");
            Require(AcousticFingerprint(stationary)==stationaryAcoustic,"Stationary routing must preserve acoustic timing and tile types too.");
            string stationaryOnce=JsonUtility.ToJson(stationary);stationaryMethod.Invoke(null,new object[]{stationary});
            Require(JsonUtility.ToJson(stationary)==stationaryOnce,"Stationary composition must remain idempotent.");
            var displacedStart=Fixture();var startAt=displacedStart.elements[0].pos;startAt.x=1;displacedStart.elements[0].pos=startAt;
            displacedStart.segments[0].audioTime=.29;displacedStart.segments[0].endAudioTime=.29;
            Apply(displacedStart);string centered=JsonUtility.ToJson(displacedStart);Apply(displacedStart);
            Require(JsonUtility.ToJson(displacedStart)==centered,
                "The centered Start must be the effective previous exit immediately, even when replacing a displaced old route.");
            Debug.Log("CHART_ROUTE_COMPOSER PASS: identity-independent rhythmic cells, repeated and two-sided lanes, "
                + "time-aware reachability, native moving phase and both directions, exact Follow, unchanged acoustic data and idempotence.");
        }

        private static string Layout(MapTimeline map) => string.Join(";", map.elements.Select(e => e.pos.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)+","+e.updateType+","+e.msLeft));
        private static string FirstDifference(MapTimeline a,MapTimeline b)
        {
            for(int i=0;i<a.elements.Length;i++)
                if(a.elements[i].pos.x!=b.elements[i].pos.x||a.elements[i].updateType!=b.elements[i].updateType||a.elements[i].msLeft!=b.elements[i].msLeft)
                    return "First difference at "+i+": x "+a.elements[i].pos.x.ToString("R")+" vs "+b.elements[i].pos.x.ToString("R")+", times "+a.elements[i].audioTime.ToString("R")+" vs "+b.elements[i].audioTime.ToString("R")+".";
            return "";
        }
        private static string AcousticFingerprint(MapTimeline map) => string.Join("|",
            JsonUtility.ToJson(new EventSnapshot { events = map.events }),
            string.Join(";",map.elements.Select(e => e.sourceIndex+":"+e.id+":"+e.kind+":"+e.pos.z.ToString("R")+":"+e.gameTime.ToString("R")+":"+e.audioTime.ToString("R")+":"+e.jumpDistance.ToString("R")+":"+JsonUtility.ToJson(new TickSnapshot { ticks=e.holdTicks }))),
            string.Join(";",map.segments.Select(s=>s.audioTime.ToString("R")+":"+s.endAudioTime.ToString("R")+":"+s.endZ.ToString("R"))));
        [Serializable] private sealed class EventSnapshot { public KoreographyEvent[] events; }
        [Serializable] private sealed class TickSnapshot { public HoldTick[] ticks; }

        private static MapTimeline Fixture()
        {
            var elements = new System.Collections.Generic.List<MapElement>();
            var segments = new System.Collections.Generic.List<MapSegment>();
            double clock = -.6;
            void Add(string kind, double time, double end, float x=0)
            {
                int index=elements.Count; int id=kind=="Hold"?2:kind=="Follow"?4:kind=="FollowMid"?5:kind=="FollowEnd"?6:kind=="BigJump"||kind=="FinishJump"?1:0;
                var ticks=kind=="Hold"?new[]{new HoldTick{audioTime=time,gameTime=time+.6,z=(float)((time+.6)*7)},new HoldTick{audioTime=end,gameTime=end+.6,z=(float)((end+.6)*7)}}:Array.Empty<HoldTick>();
                var element=new MapElement{sourceIndex=index,id=id,kind=kind,name=kind,audioTime=time,gameTime=time+.6,
                    pos=new Vector3(x,0,(float)((time+.6)*7)),jumpDistance=2.8f,holdTicks=ticks,begin=50,end=5,se=new Vector2(.775f,-.775f),ext=""};
                elements.Add(element);
                segments.Add(new MapSegment{sourceIndex=index,id=id,kind=kind,name=kind,x=x,z=element.pos.z,endZ=(float)((end+.6)*7),audioTime=time,endAudioTime=end,
                    gameTime=time+.6,endGameTime=end+.6,duration=end-time,elementIndices=new[]{index},holdTicks=ticks,path=Array.Empty<ChartPoint>(),motion=new MotionParameters{jumpDistance=2.8f,begin=50,end=5,se=element.se,ext=""}});
            }
            Add("Start",clock,clock); clock=.4;
            for(int i=0;i<128;i++)
            {
                if(i==44) { Add("Hold",clock,clock+.8);clock+=1.2;continue; }
                if(i==73)
                {
                    Add("Follow",clock,clock+2.4,-.5f);
                    var segment=segments.Last();var entry=elements.Last();entry.updateType=2;entry.mfendPos=16.8f;entry.jumpDistance=0;
                    segment.motion.updateType=2;segment.motion.mfendPos=16.8f;segment.motion.jumpDistance=0;
                    int midpoint=elements.Count;Add("FollowMid",clock+1.2,clock+1.2,.5f);segments.RemoveAt(segments.Count-1);elements.Last().jumpDistance=0;
                    int exit=elements.Count;Add("FollowEnd",clock+2.4,clock+2.4,.8f);segments.RemoveAt(segments.Count-1);
                    segment.elementIndices=new[]{entry.sourceIndex,midpoint,exit};
                    segment.path=segment.elementIndices.Select(k=>new ChartPoint{sourceIndex=k,kind=elements[k].kind,x=elements[k].pos.x,z=elements[k].pos.z,audioTime=elements[k].audioTime,gameTime=elements[k].gameTime,relativeTime=elements[k].audioTime-clock,updateType=elements[k].updateType}).ToArray();
                    clock+=2.8;continue;
                }
                Add(i%19==0?"BigJump":"Jump",clock,clock);
                clock+=i%16>=10&&i%16<=13?.125:.4;
            }
            Add("FinishJump",clock+.8,clock+.8);
            return new MapTimeline{id="SyntheticRhythm",schemaVersion=1,sampleRate=1000,forwardSpeed=7,delayTime=.6f,
                elements=elements.ToArray(),segments=segments.ToArray(),events=new[]{new KoreographyEvent{mStartSample=400,mEndSample=400,audioTime=.4,gameTime=1}},
                tempoSections=new[]{new TempoSection{samplesPerBeat=400}},normalizationNotes=new[]{"Original acoustic provenance"}};
        }
        private static void Require(bool condition,string message) { if(!condition)throw new Exception("CHART_ROUTE_COMPOSER RED/FAIL: "+message); }
    }
}
