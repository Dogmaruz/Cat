using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DancingCats.Clone.Editor
{
    public static class PcmShortSupportAnalysisValidation
    {
        public static void RunChecks()
        {
            var type = typeof(PcmShortSupportAnalysisValidation).Assembly.GetType("DancingCats.Clone.Editor.PcmShortSupportAnalysis");
            Require(type != null, "The optional short-support source policy is missing.");
            var method = type.GetMethod("Analyze", BindingFlags.Public | BindingFlags.Static);
            Require(method != null, "The optional policy needs an isolated Analyze interface.");
            MusicalCue[] Analyze(PcmInstrumentalEvidence.Evidence e, PcmVocalAnalysis.Evidence v, MusicalCue[] protect = null)
                => (MusicalCue[])method.Invoke(null, new object[] { e, v, .6, protect });
            var evidence = Notes(2); var voice = Voice(2);
            var result = Analyze(evidence, voice);
            Require(result.Length == 1 && result[0].kind == "Hold" && Math.Abs(result[0].time-2)<1e-8
                && result[0].endTime-result[0].time >= .18 && result[0].endTime-result[0].time <= .45,
                "A measured short note and bounded pulse phrase should compile once as a short support.");
            Require(Analyze(null, voice).Length == 0 && Analyze(evidence, null).Length == 0,
                "Unavailable optional source evidence must not invent supports.");
            var silent = Voice(2); Array.Clear(silent.rms, 0, silent.rms.Length);
            Require(Analyze(evidence, silent).Length == 0, "Silence cannot satisfy a learned support style.");
            var repeated = Notes(2); repeated.events[0].features[PcmInstrumentalEvidence.Index("samePitchReattack")]=.9;
            repeated.events[0].features[PcmInstrumentalEvidence.Index("insideAttacks")]=1;
            Require(Analyze(repeated, voice).Length == 0, "A confirmed same-note reattack must remain separate articulation.");
            foreach (var support in new[] {
                new MusicalCue {kind="Hold",time=1.9,endTime=2.4},
                new MusicalCue {kind="Follow",time=2.1,endTime=2.8},
                new MusicalCue {kind="BigJump",time=2.1,endTime=2.1} })
                Require(Analyze(evidence,voice,new[]{support}).Length==0, "Existing supports and a reserved launch inside the interval retain priority.");
            var originalFeatures = evidence.events[0].features.ToArray(); double originalEnd=evidence.events[0].endTime;
            Analyze(evidence,voice);
            Require(originalFeatures.SequenceEqual(evidence.events[0].features) && evidence.events[0].endTime==originalEnd,
                "Classifying a source event must not mutate its cached features or measured release.");
            var shifted=Analyze(Notes(32),Voice(32));
            Require(shifted.Length==result.Length && Math.Abs(shifted[0].time-result[0].time-30)<1e-8
                && Math.Abs(shifted[0].endTime-result[0].endTime-30)<1e-8,
                "Moving the whole source in time must preserve the relative phrase decision.");
            var malformed=Notes(2);malformed.events[0].features[0]=double.NaN;bool rejected=false;
            try {Analyze(malformed,voice);}catch(TargetInvocationException error){rejected=error.InnerException is System.IO.InvalidDataException;}
            Require(rejected,"Non-finite optional note features must fail clearly.");
            Debug.Log("PCM_SHORT_SUPPORT PASS: optional source-only short phrase, absence/silence, real reattack, protected supports and launches, immutable inputs, time translation and corrupt-feature rejection. Experimental calibration policy; not unseen-song accuracy.");
        }
        private static PcmInstrumentalEvidence.Evidence Notes(double time)
        {
            var values=new double[PcmInstrumentalEvidence.FeatureCount];
            void Set(string key,double value)=>values[PcmInstrumentalEvidence.Index(key)]=value;
            Set("neuralOnset",.8);Set("neuralPeak",.8);Set("neuralBody",.9);Set("neuralLate",.6);Set("neuralDuration",.3);
            Set("harmonicPartials",4);Set("harmonicBody",.8);Set("harmonicLate",.55);Set("harmonicDuration",.3);Set("harmonicNovelty",.2);
            Set("nextGap",.6);Set("previousGap",.6);Set("localGap",.3);Set("sourceAttack",1);
            return new PcmInstrumentalEvidence.Evidence {events=new[]{new PcmInstrumentalEvidence.NoteEvent {time=time,endTime=time+.3,midi=60,features=values}},sampleRate=1000,sampleFrames=40000};
        }
        private static PcmVocalAnalysis.Evidence Voice(double time)
        {
            const int count=5000;var rms=Enumerable.Repeat(.02,count).ToArray();
            for(int i=(int)Math.Ceiling(time*100);i<(int)Math.Ceiling((time+.3)*100);i++)rms[i]=.05;
            for(int i=(int)Math.Ceiling((time+.25)*100);i<(int)Math.Ceiling((time+.42)*100);i++)rms[i]=.008;
            return new PcmVocalAnalysis.Evidence {rms=rms,rmsStep=.01,pitchTimes=Enumerable.Range(0,count).Select(i=>i*.01).ToArray(),
                pitchConfidence=Enumerable.Repeat(.7,count).ToArray(),pitch=Enumerable.Repeat(60,count).ToArray()};
        }
        private static void Require(bool value,string message)
        {if(!value)throw new Exception("PCM_SHORT_SUPPORT RED/FAIL: "+message);}
    }
}
