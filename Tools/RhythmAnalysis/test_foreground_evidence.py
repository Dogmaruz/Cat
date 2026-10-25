"""Integrity and native-clock attacks, independently of learned pitch accuracy."""
import copy,importlib.util,sys,unittest
from pathlib import Path
if '--deps' in sys.argv:
    i=sys.argv.index('--deps');sys.path.insert(0,sys.argv[i+1]);del sys.argv[i:i+2]
import numpy as np
spec=importlib.util.spec_from_file_location('foreground_evidence',Path(__file__).with_name('analyze_vocal_holds.py'))
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);module.np=np

def evidence(schema=1):
    return dict(schemaVersion=schema,sourcePcmSha256='0'*64,sourceFileSha256='1'*64,
        sampleRate=44100,channels=1,sampleFrames=44100,rmsStep=.01,
        pitchTimes=(np.arange(100)*.01).tolist(),pitchConfidence=[.7]*100,onset=[.1]*100,
        rms=[.1]*100,voiceStarts=[.1],pitch=[60]*100,
        separatorModelSha256=module.SEP_SHA,noteModelSha256=module.NOTE_SHA,
        attackAnalysisVersion='stem-high-flux-1',vocalAttackTimes=[.2,.6],vocalAttackConfidence=[.8,.9])

class ForegroundEvidenceTests(unittest.TestCase):
    def test_new_schema_binds_both_attack_times_and_confidence(self):
        original=evidence(2); baseline=module.evidence_digest(original)
        for field in ['vocalAttackTimes','vocalAttackConfidence']:
            changed=copy.deepcopy(original);changed[field][0]+=.01
            self.assertNotEqual(module.evidence_digest(changed),baseline,field)

    def test_new_schema_has_separate_digest_domain(self):
        self.assertNotEqual(module.evidence_digest(evidence(1)),module.evidence_digest(evidence(2)))

    def test_legacy_digest_ignores_optional_new_measurements(self):
        original=evidence(1);changed=copy.deepcopy(original);changed['vocalAttackTimes']=[.4]
        self.assertEqual(module.evidence_digest(original),module.evidence_digest(changed))

    def test_native_attack_clock_stereo_phase_and_neural_confirmation(self):
        self.assertTrue(callable(getattr(module,'foreground_attack_features',None)), 'foreground measurement entry is missing')
        rate=44100;t=np.arange(rate*3)/rate;mono=np.zeros(len(t))
        for start in [1.,1.5]:
            local=t-start;active=(local>=0)&(local<.09)
            mono[active]+=.6*np.exp(-local[active]*25)*np.sin(2*np.pi*3400*local[active])
        times=np.arange(300)*.01;onset=np.zeros((300,88));onset[100,46]=.9;onset[150,46]=.2
        notes={'times':times,'onset':onset}
        a=module.foreground_attack_features(np.stack([mono,mono]),notes)
        b=module.foreground_attack_features(np.stack([mono,-mono]),notes)
        self.assertEqual(a,b)
        near=lambda at:[q for x,q in zip(a['vocalAttackTimes'],a['vocalAttackConfidence']) if abs(x-at)<.08]
        self.assertTrue(near(1.));self.assertAlmostEqual(max(near(1.)),.9,places=6)
        self.assertTrue(near(1.5));self.assertAlmostEqual(max(near(1.5)),.2,places=6)
        empty=module.foreground_attack_features(np.zeros((2,rate)),{'times':times,'onset':onset})
        self.assertEqual(empty['vocalAttackTimes'],[])

if __name__=='__main__':unittest.main()
