import hashlib
import json
import wave
from pathlib import Path
import numpy as np

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
RESOURCES = PROJECT / "Assets/Resources"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sfx_evidence():
    path = RESOURCES / "OriginalAudio/Sfx/lantern2__985.wav"
    with wave.open(str(path)) as reader:
        rate, channels, width, count = reader.getframerate(), reader.getnchannels(), reader.getsampwidth(), reader.getnframes()
        assert width == 2
        audio = np.frombuffer(reader.readframes(count), dtype="<i2").reshape(-1, channels).astype(float) / 32768
    absolute = np.max(np.abs(audio), axis=1)
    onsets = {str(db): float(np.flatnonzero(absolute >= 10 ** (db / 20))[0]) / rate * 1000
              for db in [-90, -70, -60, -50, -40, -30, -20]}
    return dict(path=str(path), sha256=sha(path), sample_rate=rate, channels=channels, sample_width=width,
                samples=count, duration_seconds=count / rate, amplitude_threshold_first_crossing_ms=onsets,
                peak_ms=float(absolute.argmax()) / rate * 1000, peak_amplitude=float(absolute.max()),
                limitation="Decoded WAV samples only; these are amplitude crossings, not a measured perceived or physical audio onset.")


def map_comparison(name):
    original_path = RESOURCES / "OriginalData/Maps" / (name + ".timeline.json")
    generated_path = RESOURCES / "CustomData/Maps" / (name + "GeneratorTestV11.timeline.json")
    original = json.loads(original_path.read_text(encoding="utf-8-sig"))
    generated = json.loads(generated_path.read_text(encoding="utf-8-sig"))
    kinds = {"Jump", "BigJump", "Hold", "Follow"}
    original_times = np.array([e["audioTime"] for e in original["segments"] if e["kind"] in kinds])
    generated_times = np.array([e["audioTime"] for e in generated["segments"] if e["kind"] in kinds])
    nearest = np.argmin(np.abs(generated_times[:, None] - original_times), axis=1)
    differences = generated_times - original_times[nearest]
    close = differences[np.abs(differences) <= .08] * 1000
    position_error = max(abs(e["pos"]["z"] / generated["forwardSpeed"] - e["gameTime"]) for e in generated["elements"])
    axis_error = max(abs(e["gameTime"] - generated["delayTime"] - e["audioTime"]) for e in generated["elements"])
    return dict(id=name, original_sha256=sha(original_path), generated_sha256=sha(generated_path),
                original_preroll=original["delayTime"], generated_preroll=generated["delayTime"],
                original_supports=len(original_times), generated_supports=len(generated_times), close_candidates=len(close),
                close_candidate_signed_ms_p10_median_p90=np.quantile(close, [.1, .5, .9]).tolist(),
                close_candidate_signed_mean_ms=float(close.mean()), max_generated_position_time_error_us=position_error * 1e6,
                max_generated_axis_relation_error_us=axis_error * 1e6,
                limitation="Exploratory nearest-event comparison within 80ms, possibly many-to-one; does not establish semantic event identity or absolute acoustic onset truth.")


if __name__ == "__main__":
    evidence = dict(sfx=sfx_evidence(), maps=[map_comparison(name) for name in
                    ["JingleBells", "CanCanPiano", "FurElisePiano", "OdetoJoyPiano"]])
    (HERE / "static-evidence.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
    print(json.dumps(evidence, indent=2))
