using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using System.Reflection;
using DancingCats.Clone;
using DancingCats.Clone.Editor;
namespace DancingCats.Clone.Editor
{
public static class PcmShortMotifValidation
{
 static void Require(bool ok,string message){if(!ok)throw new Exception("PCM_SHORT_MOTIF RED/FAIL: "+message);}
 static PcmInstrumentalEvidence.NoteEvent Note(double time,int midi,double duration=.2)
 {
  var f=new double[22];f[0]=.8;f[1]=.8;f[2]=.9;f[3]=.6;f[5]=duration;f[6]=4;f[7]=.8;f[8]=.55;f[10]=duration;f[11]=.2;f[12]=.11;f[13]=.1;f[14]=.1;f[21]=1;
  return new PcmInstrumentalEvidence.NoteEvent{time=time,endTime=time+duration,midi=midi,features=f};
 }
 static PcmVocalAnalysis.Evidence Voice(params double[] centers)
 {
  const int count=12000;var rms=Enumerable.Repeat(.02,count).ToArray();
  foreach(double t in centers){for(int i=(int)Math.Ceiling((t-.09)*100);i<(int)Math.Ceiling((t-.025)*100);i++)rms[i]=.01;for(int i=(int)Math.Ceiling(t*100);i<(int)Math.Ceiling((t+.2)*100);i++)rms[i]=.0028;for(int i=(int)Math.Ceiling((t+.01)*100);i<(int)Math.Ceiling((t+.035)*100);i++)rms[i]=.05;for(int i=(int)Math.Ceiling((t+.2)*100);i<(int)Math.Ceiling((t+.28)*100);i++)rms[i]=.001;for(int i=(int)Math.Ceiling((t+.28)*100);i<(int)Math.Ceiling((t+.42)*100);i++)rms[i]=.008;}
  return new PcmVocalAnalysis.Evidence{rms=rms,rmsStep=.01,pitchTimes=Enumerable.Range(0,count).Select(i=>i*.01).ToArray(),pitchConfidence=Enumerable.Repeat(.2,count).ToArray(),pitch=Enumerable.Repeat(60,count).ToArray()};
 }
 static PcmInstrumentalEvidence.Evidence Fixture(double shift=0,int transpose=0,double scale=1,int extra=0)
 {
  var ns=new List<PcmInstrumentalEvidence.NoteEvent>();int[] contour={-3,-2,-1,0,1,2,3};
  for(int instance=0;instance<3+extra;instance++)for(int j=0;j<7;j++)
  {
   int delta=extra>0&&instance>=2?(instance==4?6:3):0;
   var n=Note(shift+(2+instance*4+(j-3)*.2)*scale,60+transpose+contour[j]+(j==3?0:delta),j==3?.2*scale:.07*scale);
   foreach(int k in new[]{12,13,14})n.features[k]*=scale;ns.Add(n);
  }
  return new PcmInstrumentalEvidence.Evidence{events=ns.ToArray(),sampleRate=1000,sampleFrames=120000};
 }
 public static void RunChecks()
 {
  PcmShortSupportAnalysisValidation.RunChecks();
  var e=Fixture();var v=Voice(2,6);string before=UnityEngine.JsonUtility.ToJson(e);
  var got=PcmShortSupportAnalysis.Analyze(e,v,.6);
  Require(got.Any(c=>Math.Abs(c.time-10)<1e-8&&Math.Abs(c.endTime-10.2)<1e-8),"two independent frozen source motifs must recover the missing third support");
  Require(before==UnityEngine.JsonUtility.ToJson(e),"recurrence must preserve source evidence");
  Require(!PcmShortSupportAnalysis.Analyze(e,Voice(2),.6).Any(c=>c.time>8),"one seed cannot propagate");
  var protect=new[]{new MusicalCue{kind="Hold",time=9.9,endTime=10.4}};
  Require(!PcmShortSupportAnalysis.Analyze(e,v,.6,protect).Any(c=>c.time>8),"protected support retains priority");
  e=Fixture();e.events.Single(n=>Math.Abs(n.time-10)<1e-8).features[5]=.052;
  Require(!PcmShortSupportAnalysis.Analyze(e,v,.6).Any(c=>c.time>8),"a harmonic tail alone cannot propagate a weak short neural attack");
  // Isolate source recurrence from the frozen voice model's fixed millisecond
  // observation windows. Real source timings and the pulse scale together.
  var original=SourceOnly(Fixture(),0,1);var translated=SourceOnly(Fixture(30),30,1);var transposed=SourceOnly(Fixture(0,5),0,1);var stretched=SourceOnly(Fixture(0,0,1.2),0,1.2);
  Require(original.SequenceEqual(translated)&&original.SequenceEqual(transposed)&&original.SequenceEqual(stretched),"source recurrence must preserve time translation, register translation and tempo scaling inside the short-duration eligibility range");
  Require(SourceOnly(Fixture(extra:2),0,1).Length==2,"inferred motifs cannot feed back as seeds to complete a second generation");
  var ambiguous=Fixture();Require(SourceOnly(ambiguous,0,1,ambiguousExit:true).Length==0,"two incompatible projected exits cannot form one support");
  var map=MusicalChartCompiler.Compile(new MusicalScore{id="MotifFixture",sampleRate=1000,audioDuration=16,bpm=100,cues=got},new AudioChartGenerator.Options(),1000,16);
  var held=map.segments.Single(s=>s.kind=="Hold"&&Math.Abs(s.audioTime-10)<1e-8);
  Require(held.id==2&&Math.Abs(held.endAudioTime-10.2)<1e-8&&held.holdTicks.Length==2,"actual native compiler must preserve inferred Hold type, entry, exit and separate tick markers");
  Debug.Log("PCM_SHORT_MOTIF PASS: missing recurrence, one-seed veto, protected overlap, dual-release negative, no feedback, ambiguous exit, immutable source, time/register/tempo invariants, actual native Hold compilation.");
 }
 static double[] SourceOnly(PcmInstrumentalEvidence.Evidence e,double shift,double scale,bool ambiguousExit=false)
 {
  var type=typeof(PcmShortSupportAnalysis);var ct=type.GetNestedType("Candidate",BindingFlags.NonPublic);var lt=typeof(List<>).MakeGenericType(ct);var pool=(IList)Activator.CreateInstance(lt);var accepted=(IList)Activator.CreateInstance(lt);
  object Candidate(PcmInstrumentalEvidence.NoteEvent n,double end){var c=Activator.CreateInstance(ct);ct.GetField("note").SetValue(c,n);ct.GetField("end").SetValue(c,end);ct.GetField("score").SetValue(c,1d);return c;}
  foreach(var n in e.events.Where(n=>Math.Abs(n.features[5]-.2*scale)<1e-8))
  {
   double center=(n.time-shift)/scale;var c=Candidate(n,n.time+(ambiguousExit&&Math.Abs(center-6)<1e-8?.4:.2)*scale);pool.Add(c);if(center<8)accepted.Add(c);
  }
  type.GetMethod("AddRecurrentSupports",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{e.events,pool,accepted,.6*scale});
  return accepted.Cast<object>().Where(c=>(bool)ct.GetField("recurrent").GetValue(c)).Select(c=>Math.Round((((PcmInstrumentalEvidence.NoteEvent)ct.GetField("note").GetValue(c)).time-shift)/scale,6)).ToArray();
 }
}

}
