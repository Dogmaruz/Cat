"""Read-only patch tracking on native water frames, with explicit ambiguity.

Input metadata is independent camera/source evidence, not inferred from frames:
{
  "feature": "side-roll", "frameTimes": [0.25, 0.35, ...],
  "mask": "side-roll-mask.png", "frames": ["side-roll-flow-00.png", ...],
  "samples": [{"label":"48deg", "x":270, "y":480,
               "tangentX":0, "tangentY":1, "expectedPixelsPerSecond":20}]
}
PNG coordinates have top-left origin. The camera must stay fixed during capture.
No changed-pixel statistic is used as evidence of direction.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

if "--deps" in sys.argv:
    at = sys.argv.index("--deps")
    sys.path.insert(0, sys.argv[at + 1])
    del sys.argv[at:at + 2]

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, map_coordinates, shift


def track_patch(first, second, mask, center, tangent, max_shift=12,
                half_width=10, half_length=16):
    """Return signed displacement or an honest untrackable classification."""
    direction = np.asarray(tangent, dtype=float)
    if not np.isfinite(direction).all() or np.linalg.norm(direction) < 1e-6:
        return {"status": "ambiguous", "reason": "invalid projected tangent"}
    direction /= np.linalg.norm(direction)
    across = np.array([-direction[1], direction[0]])
    lateral, longitudinal = np.meshgrid(np.arange(-half_width, half_width + 1),
                                         np.arange(-half_length, half_length + 1))
    points = (np.asarray(center)[:, None] + across[:, None] * lateral.ravel()
              + direction[:, None] * longitudinal.ravel())

    def sample(image, coordinates, fill):
        return map_coordinates(image, [coordinates[1], coordinates[0]], order=1,
                               mode="constant", cval=fill)

    mask_float = np.asarray(mask, dtype=float)
    reference = sample(first, points, np.nan)
    reference_mask = sample(mask_float, points, 0) > .99
    if reference_mask.mean() < .90:
        return {"status": "ambiguous", "reason": "patch mostly outside visible target water",
                "coverage": float(reference_mask.mean())}
    if reference[reference_mask].std() < .0025:
        return {"status": "ambiguous", "reason": "insufficient recognizable texture"}
    # Include exactly zero even when expected speed yields a fractional bound.
    bound = int(np.floor(max_shift * 4))
    displacements = np.arange(-bound, bound + 1, dtype=float) / 4
    correlations = np.full(len(displacements), np.nan)
    for index, distance in enumerate(displacements):
        candidate_points = points + direction[:, None] * distance
        candidate = sample(second, candidate_points, np.nan)
        valid = (reference_mask & (sample(mask_float, candidate_points, 0) > .99)
                 & np.isfinite(reference) & np.isfinite(candidate))
        if valid.mean() < .90:
            continue
        a, b = reference[valid], candidate[valid]
        a, b = a - a.mean(), b - b.mean()
        denominator = np.linalg.norm(a) * np.linalg.norm(b)
        if denominator > 1e-12:
            correlations[index] = np.dot(a, b) / denominator
    if not np.isfinite(correlations).any():
        return {"status": "ambiguous", "reason": "no visible translated patch"}
    winner = int(np.nanargmax(correlations))
    distance, correlation = displacements[winner], correlations[winner]
    zero = correlations[int(np.argmin(abs(displacements)))]
    # Compare separate correlation peaks, not the shoulder of this SAME broad
    # peak. Smooth packets legitimately have a broad unique maximum; calling
    # every nearby high score a repeated feature is a false ambiguity.
    finite_scores = np.where(np.isfinite(correlations), correlations, -2)
    peaks = np.flatnonzero((finite_scores[1:-1] > finite_scores[:-2])
                           & (finite_scores[1:-1] >= finite_scores[2:])) + 1
    rivals = [index for index in peaks if abs(displacements[index] - distance) >= 1.5]
    rival = float(max(correlations[index] for index in rivals)) if rivals else -1
    result = {"status": "ambiguous", "pixels": float(distance), "correlation": float(correlation),
              "zeroCorrelation": float(zero) if np.isfinite(zero) else None,
              "separatedPeakMargin": float(correlation - rival), "competingPeakCount": len(rivals)}
    if correlation < .65:
        result["reason"] = "appearance did not retain an identifiable packet"
    elif abs(distance) < .75:
        result["reason"] = "stationary or subpixel maximum"
    elif abs(distance) >= max_shift - .5:
        result["reason"] = "maximum at search boundary"
    elif not np.isfinite(zero) or correlation - zero < .03:
        result["reason"] = "moving match does not clearly beat stationary match"
    elif correlation - rival < .035:
        result["reason"] = "multiple plausible translations/repeating texture"
    else:
        result["status"] = "downstream" if distance > 0 else "upstream"
    return result


def analyze(metadata_path, footprints=None):
    metadata = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
    directory = metadata_path.parent
    tag = metadata.get("feature", metadata_path.name.removesuffix("-clock.json"))
    frames = metadata.get("frames")
    paths = [directory / f for f in frames] if isinstance(frames, list) else sorted(directory.glob(f"{tag}-flow-[0-9][0-9].png"))
    if len(paths) < 4:
        raise ValueError(f"Need at least four actual frames for {tag}")
    times = metadata.get("frameTimes")
    if not times or len(times) != len(paths):
        raise ValueError("Exact frameTimes must match frames; inferred capture timing is not accepted")
    if any(b <= a for a, b in zip(times, times[1:])):
        raise ValueError("Frame times must increase")
    mask_path = directory / metadata.get("mask", f"{tag}-visible-mask.png")
    mask = np.asarray(Image.open(mask_path).convert("L")) > 127
    images = []
    for file in paths:
        rgb = np.asarray(Image.open(file).convert("RGB"), dtype=float) / 255
        luminance = rgb @ np.array([.2126, .7152, .0722])
        # Remove broad static lighting; retain recognizable wave/foam packets.
        images.append(gaussian_filter(luminance, .7) - gaussian_filter(luminance, 3.5))
    if any(image.shape != mask.shape for image in images):
        raise ValueError("Frame/mask sizes disagree")
    samples = metadata.get("flowSamples", metadata.get("samples", []))
    if not samples:
        raise ValueError("Independent projected downstream flowSamples are required")
    reports = []
    for sample in samples:
        records = []
        footprint = (footprints or {}).get(f"{tag}/{sample.get('label', 'unnamed')}", {})
        half_width = int(footprint.get("halfWidth", 10))
        half_length = int(footprint.get("halfLength", 16))
        expected = abs(float(sample.get("expectedPixelsPerSecond", 0)))
        for lag in (1, 2, 3, 4):
            for index in range(len(images) - lag):
                elapsed = times[index + lag] - times[index]
                search = min(36, max(4, expected * elapsed * 1.8 + 2)) if expected else 16
                result = track_patch(images[index], images[index + lag], mask,
                                     (sample["x"], sample["y"]),
                                     (sample["tangentX"], sample["tangentY"]), max_shift=search,
                                     half_width=half_width, half_length=half_length)
                result.update(firstFrame=index, lastFrame=index + lag, elapsed=elapsed)
                if result["status"] != "ambiguous":
                    result["pixelsPerSecond"] = result["pixels"] / elapsed
                records.append(result)
        reliable = [row for row in records if row["status"] != "ambiguous"]
        positive = sum(row["status"] == "downstream" for row in reliable)
        negative = len(reliable) - positive
        distinct_starts = len({row["firstFrame"] for row in reliable})
        if len(reliable) >= 3 and distinct_starts >= 3 and positive / len(reliable) >= .75:
            status = "confirmed downstream"
        elif len(reliable) >= 3 and distinct_starts >= 3 and negative / len(reliable) >= .75:
            status = "confirmed upstream"
        else:
            status = "ambiguous"
        speeds = [row["pixelsPerSecond"] for row in reliable]
        reports.append({"label": sample.get("label", "unnamed"), "status": status,
                        "position": [sample["x"], sample["y"]],
                        "footprint": {"halfWidth": half_width, "halfLength": half_length,
                                      "pixelCount": (2 * half_width + 1) * (2 * half_length + 1),
                                      "selection": footprint.get("selection", "default fixed footprint")},
                        "expectedPixelsPerSecond": expected,
                        "recognizablePairs": len(reliable), "downstreamPairs": positive,
                        "distinctStartingFrames": distinct_starts,
                        "upstreamPairs": negative,
                        "medianPixelsPerSecond": float(np.median(speeds)) if speeds else None,
                        "pairs": records})
    confirmed = all(row["status"] == "confirmed downstream" for row in reports)
    failure = any(row["status"] == "confirmed upstream" for row in reports)
    return {"pass": True if confirmed else False if failure else None,
            "scope": "Fixed-camera native-frame local packet correlation along independently projected downstream tangents; ambiguous texture is not a pass",
            "feature": tag, "metadata": str(metadata_path.resolve()),
            "frameHashes": [{"name": file.name, "sha256": hashlib.sha256(file.read_bytes()).hexdigest()} for file in paths],
            "thresholds": {"minimumNCC": .65, "minimumGainOverStationary": .03,
                           "minimumSeparatedPeakMargin": .035, "minimumPixelTravel": .75,
                           "minimumVisibleCoverage": .90, "minimumRecognizablePairs": 3,
                           "requiredDirectionalVoteFraction": .75},
            "samples": reports}


def self_test():
    rng = np.random.default_rng(731)
    texture = gaussian_filter(rng.normal(size=(160, 160)), 1.1)
    texture /= texture.std()
    mask = np.ones_like(texture, dtype=bool)
    for distance, status in [(3, "downstream"), (-3, "upstream")]:
        found = track_patch(texture, shift(texture, (distance, 0), order=1), mask,
                            (80, 80), (0, 1), max_shift=8)
        assert found["status"] == status, found
        assert abs(found["pixels"] - distance) <= .5, found
    static = track_patch(texture, texture, mask, (80, 80), (0, 1), max_shift=8)
    assert static["status"] == "ambiguous", static
    flat = track_patch(texture * 0, texture * 0, mask, (80, 80), (0, 1))
    assert flat["status"] == "ambiguous", flat
    periodic = np.tile(np.sin(np.arange(160)[:, None] * np.pi / 3), (1, 160))
    repeats = track_patch(periodic, shift(periodic, (2, 0)), mask, (80, 80), (0, 1), max_shift=10)
    assert repeats["status"] == "ambiguous", repeats
    y, x = np.mgrid[:160, :160]
    single_packet = np.exp(-((y - 76) ** 2 + (x - 78) ** 2) / (2 * 6 ** 2))
    broad_peak = track_patch(single_packet, shift(single_packet, (3, 0)), mask,
                             (80, 80), (0, 1), max_shift=10)
    assert broad_peak["status"] == "downstream", broad_peak
    print(json.dumps({"selfTest": "pass", "cases": ["downstream", "upstream", "static", "no texture", "repeating/ambiguous", "single broad correlation peak"]}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("metadata", nargs="?", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--footprints", type=Path,
                        help="Predeclared per tag/label patch dimensions, selected using visibility geometry only")
    args = parser.parse_args()
    if args.self_test:
        self_test()
    else:
        if args.metadata is None:
            parser.error("metadata path is required")
        footprints = json.loads(args.footprints.read_text(encoding="utf-8-sig")) if args.footprints else None
        report = analyze(args.metadata, footprints)
        text = json.dumps(report, indent=2, allow_nan=False)
        if args.output:
            args.output.write_text(text + "\n", encoding="utf-8")
        else:
            print(text)
