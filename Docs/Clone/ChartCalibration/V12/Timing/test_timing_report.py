import unittest
from timing_report import summarize_capture, visual_impact_peaks


class TimingReportCounterexamples(unittest.TestCase):
    def sample(self, *, game=1.0, nominal=1.0, dsp=10.0, start=9.0, cursor=0.4, stage="score-contact", real=10.0):
        return dict(stage=stage, hasTile=True, kind="Jump", tileId=0, frame=60, sourceIndex=1,
                    gameTime=game, nominalGameTime=nominal, nominalAudioTime=0.4, dspTime=dsp,
                    dspStart=start, clockOffset=0.0, rate=1.0, userDelay=0.0, effectiveDelay=0.6,
                    lapOffset=0.0, jumpDistance=3.25, musicCursor=cursor, realTime=real)

    def test_frame_backlog_is_not_mislabeled_as_late_chart_contact(self):
        s = self.sample(dsp=10.2, cursor=0.6)
        result = summarize_capture(dict(contacts=[s], frames=[]))
        self.assertAlmostEqual(result["contact_lateness_ms"]["median"], 0)
        self.assertAlmostEqual(result["contact_callback_after_nominal_dsp_ms"]["median"], 200)
        self.assertAlmostEqual(result["substep_backlog_ms"]["median"], 200)

    def test_negative_user_calibration_is_separate_from_chart_error(self):
        s = self.sample(cursor=0.48)
        s.update(userDelay=-0.08)
        result = summarize_capture(dict(contacts=[s], frames=[]))
        self.assertAlmostEqual(result["contact_lateness_ms"]["median"], 0)
        self.assertAlmostEqual(result["music_minus_nominal_audio_ms"]["median"], 80)
        self.assertEqual(result["user_delay_values_seconds"], [-0.08])

    def test_resource_load_time_requires_request_and_submit_not_audible_onset(self):
        rows = [self.sample(), self.sample(stage="effect-request", real=10.001),
                self.sample(stage="effect-submit", real=10.026)]
        result = summarize_capture(dict(contacts=rows, frames=[]))
        self.assertAlmostEqual(result["resource_load_ms"]["median"], 25)
        self.assertFalse(result["measures_physical_audio_latency"])

    def test_empty_capture_cannot_pass_as_a_timing_measurement(self):
        with self.assertRaisesRegex(ValueError, "No real score contacts"):
            summarize_capture(dict(contacts=[], frames=[]))

    def test_visible_peak_uses_render_wall_time_not_internal_landing_age(self):
        contact = self.sample(real=10.0)
        frames = [dict(realTime=10 + i / 60, pose=dict(scaleYRatio=scale, landingTime=(i + 1) / 60))
                  for i, scale in enumerate([.96, .85, .90])]
        peaks = visual_impact_peaks([contact], frames)
        self.assertEqual(len(peaks), 1)
        peak = peaks[0]
        self.assertAlmostEqual(peak["render_after_contact_ms"], 1000 / 60)
        self.assertAlmostEqual(peak["internal_landing_age_ms"], 2000 / 60)


if __name__ == "__main__":
    unittest.main()
