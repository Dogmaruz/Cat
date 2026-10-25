"""Test stitching with known native-frame coordinates, independently of pitch accuracy."""
import importlib.util
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

if '--deps' in sys.argv:
    at = sys.argv.index('--deps')
    sys.path.insert(0, sys.argv[at + 1])
    del sys.argv[at:at + 2]
import numpy as np

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('timing_analysis', HERE / 'analyze_vocal_holds.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.np = np


class NativeCoordinateProbe:
    """Known model-shaped rows encode where each native frame observes its input.

    This replaces neural predictions only; transcribe's window loop, trimming,
    concatenation and time construction are production code under test.
    """
    def __init__(self):
        self.window = 0

    def run(self, outputs, inputs):
        self.asserted_input_shape = next(iter(inputs.values())).shape
        if self.asserted_input_shape != (1, 43844, 1):
            raise AssertionError(self.asserted_input_shape)
        native = np.zeros((1, 172, 88), dtype=np.float32)
        native[0, :, 0] = self.window * 36164 + np.arange(172) * 256 - 3840
        native[0, :, 1] = self.window
        self.window += 1
        return [native, native.copy()]


def transcribe_probe(seconds):
    samples = round(seconds * 22050)
    # Deterministic downsampling avoids testing scipy's anti-alias filter here.
    with patch.object(module, 'resample_poly', lambda values, up, down: values[::2], create=True), \
         patch.object(module, 'session', lambda model: NativeCoordinateProbe()):
        return module.transcribe(np.zeros((1, samples * 2), dtype=np.float32), 'probe.onnx'), samples


class TranscribeTimingTests(unittest.TestCase):
    def test_long_song_covers_tail_without_extrapolated_or_padded_rows(self):
        result, samples = transcribe_probe(157.1526530612245)
        times = result['times']
        self.assertGreaterEqual(times[0], 0)
        self.assertLess(times[-1], samples / 22050)
        self.assertLessEqual(samples / 22050 - times[-1], 256 / 22050)
        self.assertEqual(len(times), len(result['note']))
        self.assertEqual(len(times), len(result['onset']))
        self.assertTrue(np.all(result['note'][:, 0] < samples))

    def test_each_row_uses_actual_input_window_coordinates(self):
        result, _ = transcribe_probe(8)
        np.testing.assert_allclose(result['times'] * 22050, result['note'][:, 0], atol=1e-8, rtol=0)
        self.assertTrue(np.all(np.diff(result['times']) > 0))

    def test_late_window_marker_has_no_accumulated_drift(self):
        result, _ = transcribe_probe(157.1526530612245)
        target_sample = 70 * 36164 + 100 * 256 - 3840
        marker = np.flatnonzero(result['note'][:, 0] == target_sample)
        self.assertEqual(len(marker), 1)
        self.assertAlmostEqual(result['times'][marker[0]], target_sample / 22050, places=12)

    def test_short_clip_tail_drops_padded_model_frames(self):
        result, samples = transcribe_probe(.41)
        expected = np.arange(0, samples, 256)
        np.testing.assert_array_equal(result['note'][:, 0], expected)
        np.testing.assert_allclose(result['times'], expected / 22050, atol=1e-12, rtol=0)


if __name__ == '__main__':
    unittest.main()
