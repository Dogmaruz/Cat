"""Causal feature and strict metric checks independent of learned labels."""
import pathlib,sys,unittest
PROJECT=pathlib.Path(__file__).resolve().parents[2]/'DancingCatsClone'
if '--deps' in sys.argv:
 i=sys.argv.index('--deps');sys.path.insert(0,sys.argv[i+1]);del sys.argv[i:i+2]
else:sys.path.insert(0,str(PROJECT/'Library/OriginalChartCalibration/v4/ml-deps'))
import numpy as np
import train_short_support_release as policy

class ShortReleaseTests(unittest.TestCase):
 def voice(self):
  return dict(rms=np.full(1000,.02),pitchTimes=np.arange(1000)*.01,pitchConfidence=np.full(1000,.7),pitch=np.full(1000,60))
 def test_later_syllable_does_not_change_selected_note_features(self):
  voice=self.voice();base=policy.release_features(voice,2,2.2)
  voice['rms'][228:242]=.04;voice['pitchConfidence'][228:242]=.1;voice['pitch'][228:242]=78
  np.testing.assert_allclose(base,policy.release_features(voice,2,2.2),atol=1e-12)
 def test_release_follows_chosen_endpoint_and_time_translation(self):
  voice=self.voice();voice['rms'][221:228]=.001
  short=policy.release_features(voice,2,2.2);long=policy.release_features(voice,2,2.4)
  self.assertLess(short[1],long[1])
  voice2=self.voice();voice2['rms'][521:528]=.001
  np.testing.assert_allclose(short,policy.release_features(voice2,5,5.2),atol=1e-12)
 def test_strict_interval_matching_uses_maximum_cardinality(self):
  truth={'sample':[dict(audioTime=0,endAudioTime=.2),dict(audioTime=.06,endAudioTime=.26)]}
  rows=[dict(song='sample',time=0,end=.2),dict(song='sample',time=-.06,end=.14)]
  result=policy.evaluate(rows,truth)['sample'];self.assertEqual((result['tp'],result['fp'],result['fn']),(2,0,0))
 def test_correct_onset_with_wrong_exit_is_not_a_true_hold(self):
  result=policy.evaluate([dict(song='sample',time=0,end=.4)],{'sample':[dict(audioTime=0,endAudioTime=.2)]})['sample']
  self.assertEqual((result['tp'],result['fp'],result['fn']),(0,1,1))
 def test_overlong_support_is_not_a_short_hold_match(self):
  result=policy.evaluate([dict(song='sample',time=0,end=.46)],{'sample':[dict(audioTime=0,endAudioTime=.45)]})['sample']
  self.assertEqual((result['tp'],result['fp'],result['fn']),(0,1,1))

if __name__=='__main__':unittest.main()
