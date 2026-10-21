"""Evidence/geometry regression checks. No Unity, audio model or third-party deps."""
import copy
import json
from pathlib import Path
import unittest
import author_score


class MusicalAuthoringChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        path = Path(__file__).resolve().parents[2] / 'DancingCatsClone/Assets/Clone/Editor/MusicalScores/AlwaysRememberMeowCat.score.json'
        cls.score = json.loads(path.read_text(encoding='utf-8'))

    def test_retained_evidence_reproduces_current_score(self):
        self.assertEqual(author_score.author(self.score), self.score)

    def test_real_syllable_attack_survives_as_hold_entry(self):
        cue = next(c for c in self.score['cues'] if c['time'] == 15.1136)
        self.assertEqual(cue['kind'], 'Hold')
        self.assertAlmostEqual(cue['endTime'], 16.07)
        self.assertFalse(any(c['time'] == 15.578 for c in self.score['cues']))

    def test_no_fake_event_can_bridge_recording_tail(self):
        score = copy.deepcopy(self.score)
        score['authoring']['manualCues'] = score['authoring']['manualCues'][:1]
        with self.assertRaisesRegex(ValueError, 'No evidenced support'):
            author_score.author(score)

    def test_follow_cannot_teleport(self):
        score = copy.deepcopy(self.score)
        follow = next(c for c in score['cues'] if c['kind'] == 'Follow')
        follow['points'][1]['time'] = follow['time'] + .00001
        with self.assertRaisesRegex(ValueError, 'Follow exceeds'):
            author_score.validate(score)

    def test_finish_uses_native_forward_speed_policy(self):
        score = copy.deepcopy(self.score)
        score['authoring'].update(forwardSpeed=2, delayTime=.8)
        delay, finish = author_score.timing(score)
        self.assertEqual(delay, .8)
        self.assertAlmostEqual(finish, round(score['audioDuration'] - 2.75, 6))


if __name__ == '__main__':
    unittest.main()
