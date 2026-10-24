"""Counterfactual measurement fixtures; these do not claim source-separation accuracy."""
import importlib.util,json,sys,unittest
from pathlib import Path

HERE=Path(__file__).resolve().parent
if '--deps' in sys.argv:
    at=sys.argv.index('--deps');sys.path.insert(0,sys.argv[at+1]);del sys.argv[at:at+2]
import numpy as np
from scipy.signal import find_peaks
spec=importlib.util.spec_from_file_location('vocal_analysis',HERE/'analyze_vocal_holds.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);module.np=np;module.find_peaks=find_peaks
FIXTURES=json.loads((HERE/'gesture_fixtures.json').read_text())

def render(case):
    step=FIXTURES['stepSeconds'];count=round(FIXTURES['durationSeconds']/step)
    pitch=np.full(count,60,int);confidence=np.full(count,.1);rms=np.zeros(count);onset=np.full(count,.1)
    for segment in case['segments']:
        a=round(segment['start']/step);b=round(segment['end']/step)
        pitch[a:b]=segment['pitch'];confidence[a:b]=segment['confidence'];rms[a:b]=segment['rms']
    for item in case['onsets']:onset[round(item['time']/step)]=item['strength']
    return dict(pitchTimes=(np.arange(count)*step).tolist(),pitch=pitch.tolist(),pitchConfidence=confidence.tolist(),rms=rms.tolist(),onset=onset.tolist(),voiceStarts=[])

class VocalGestureTests(unittest.TestCase):
    def test_shared_counterfactual_gestures(self):
        for case in FIXTURES['cases']:
            with self.subTest(case=case['name']):
                result=module.classify(render(case),case['attacks'],case['beatPeriod'])
                self.assertEqual(case['expectedHolds'],len(result),result)
                self.assertTrue(all(np.isfinite(c['time'])and np.isfinite(c['endTime'])and c['endTime']>c['time']for c in result))
                self.assertTrue(all(result[i]['endTime']<=result[i+1]['time']for i in range(len(result)-1)))

if __name__=='__main__':unittest.main()
