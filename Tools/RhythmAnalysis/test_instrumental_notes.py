"""Source-measurement regressions, independent of chart annotations and learned accuracy."""
import importlib.util,pathlib,sys,unittest
if '--deps'in sys.argv:
    at=sys.argv.index('--deps');sys.path.insert(0,sys.argv[at+1]);del sys.argv[at:at+2]
import numpy as np

class InstrumentalNotesTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        p=pathlib.Path(__file__).with_name('analyze_instrumental_notes.py')
        if not p.exists():raise AssertionError('Independent instrumental-note producer is missing')
        spec=importlib.util.spec_from_file_location('instrumental',p);cls.module=importlib.util.module_from_spec(spec);spec.loader.exec_module(cls.module)

    def test_hash_binds_feature_semantics_and_event_boundaries(self):
        import copy
        m=self.module
        e=dict(schemaVersion=1,sourceFileSha256='1'*64,sourcePcmSha256='2'*64,noteModelSha256='3'*64,
            timingVersion='input-window-time-v2',analysisVersion='mixed-note-events-1',sampleRate=44100,
            channels=2,sampleFrames=441000,featureQuantization=100000000,featureNames=list(m.FEATURE_NAMES),
            events=[dict(time=1.,endTime=1.3,midi=60,features=[0.]*len(m.FEATURE_NAMES))])
        h=m.evidence_digest(e)
        for mode in ['time','endTime','midi','semantic','feature']:
            changed=copy.deepcopy(e)
            if mode=='semantic':changed['featureNames'][0]='wrongMeaning'
            elif mode=='feature':changed['events'][0]['features'][0]=.1
            else:changed['events'][0][mode]+=.1 if mode!='midi'else 1
            self.assertNotEqual(h,m.evidence_digest(changed),mode)

    def test_fft_preserves_antiphase_and_measures_release(self):
        m=self.module;rate=11025;t=np.arange(rate*3)/rate;mono=np.zeros(len(t))
        for h in range(1,6):mono+=np.sin(2*np.pi*220*h*t)/h
        envelope=np.where((t>=.5)&(t<.85),1.,0.)
        mono*=envelope
        mt,mag=m.source_spectrum(np.stack([mono,-mono],axis=1),rate)
        _,single=m.source_spectrum(mono[:,None],rate)
        np.testing.assert_allclose(mag,single,rtol=1e-6,atol=1e-6)
        measured=m.harmonic_measurements(mt,mag,rate/1024,.5,57)
        self.assertGreaterEqual(measured['partials'],3)
        self.assertGreater(measured['duration'],.25)
        self.assertLess(measured['duration'],.43)

    def test_neural_offset_ends_on_same_pitch_reattack(self):
        m=self.module;t=np.arange(200)*.01;body=np.ones(200)*.7;on=np.zeros(200);on[50]=.8;on[80]=.9
        duration=m.neural_measurements(t,body,on,.5)['duration']
        self.assertGreater(duration,.22);self.assertLess(duration,.38)

    def test_context_never_requires_absolute_song_time(self):
        m=self.module
        notes=[dict(time=.5,endTime=.8,midi=60),dict(time=1.,endTime=1.3,midi=62),dict(time=1.5,endTime=1.8,midi=64)]
        context=m.context_features(notes,1)
        shifted=[dict(n,time=n['time']+80,endTime=n['endTime']+80)for n in notes]
        self.assertEqual(context,m.context_features(shifted,1))

    def test_short_neural_event_has_no_required_reattack_frame(self):
        t=.05+np.arange(60)*.01161;body=np.zeros(60);body[:11]=.7
        measured=self.module.neural_measurements(t,body,np.zeros(60),.05)
        self.assertGreater(measured['duration'],.12)
        self.assertEqual(measured['samePitch'],0.)

    def test_context_describes_each_pitch_candidate_and_its_own_release(self):
        notes=[dict(time=.5,endTime=.8,midi=60),dict(time=1.,endTime=1.3,midi=62),dict(time=1.5,endTime=1.8,midi=64)]
        alternate=dict(time=1.,endTime=1.7,midi=74)
        features=self.module.context_features(notes,1,alternate)
        self.assertEqual(features['nextPitchDelta'],-10)
        self.assertEqual(features['previousPitchDelta'],14)
        self.assertEqual(features['insideAttacks'],1)
        self.assertEqual(features['relativePitch'],12)

    def test_dense_chord_preserves_confident_melody_beyond_top_four(self):
        from unittest.mock import patch
        m=self.module;t=np.arange(200)*.01
        body=np.zeros((200,88));onset=np.zeros_like(body)
        for rank in range(8):
            body[50:80,27+rank]=.8
            onset[50,27+rank]=.95-.04*rank
        measured=dict(partials=2,body=.8,late=.5,tail=.2,duration=.3,novelty=.1)
        with patch.object(m,'harmonic_measurements',return_value=measured),patch.object(m,'spectral_attacks',return_value=[]):
            events=m.measure_events(np.zeros((2000,1)),1000,dict(times=t,note=body,onset=onset))
        self.assertEqual(len(events),8,'A confident fifth-to-eighth pitch cannot be dropped before phrase analysis')
        self.assertEqual(sorted(e['features'][19]for e in events),list(range(8)))
        self.assertEqual(m.ANALYSIS_VERSION,'mixed-note-events-2-candidates8')

    def test_candidate_expansion_keeps_the_confidence_floor(self):
        from unittest.mock import patch
        m=self.module;t=np.arange(200)*.01
        body=np.zeros((200,88));onset=np.zeros_like(body)
        body[50:80,27:35]=.8;onset[50,27:31]=.8;onset[50,31:35]=.20
        measured=dict(partials=2,body=.8,late=.5,tail=.2,duration=.3,novelty=.1)
        with patch.object(m,'harmonic_measurements',return_value=measured),patch.object(m,'spectral_attacks',return_value=[]):
            events=m.measure_events(np.zeros((2000,1)),1000,dict(times=t,note=body,onset=onset))
        self.assertEqual(len(events),4,'Expanding rank capacity must not lower evidence confidence')

if __name__=='__main__':unittest.main()
