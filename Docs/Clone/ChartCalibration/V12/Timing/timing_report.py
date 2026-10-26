"""Summarize passive software trace; never infer physical audio latency from DSP callbacks."""
import json
import math
import statistics
import sys
from pathlib import Path


def visual_impact_peaks(contacts, frames):
    landings = sorted((r for r in contacts if r["kind"] in
                       {"Jump", "BigJump", "Theme", "Hold", "Follow", "FollowEnd", "FinishJump"}),
                      key=lambda r: r["realTime"])
    result = []
    for index, contact in enumerate(landings):
        start = contact["realTime"]
        end = min(start + .12, landings[index + 1]["realTime"] if index + 1 < len(landings) else float("inf"))
        nearby = [f for f in frames if start <= f["realTime"] < end and "pose" in f]
        if not nearby:
            continue
        minimum = min(nearby, key=lambda f: f["pose"]["scaleYRatio"])
        impulse = max(nearby, key=lambda f: f["pose"].get("impactCompression", 0))
        result.append(dict(source_index=contact["sourceIndex"], kind=contact["kind"],
                           render_after_contact_ms=(minimum["realTime"] - start) * 1000,
                           internal_landing_age_ms=minimum["pose"]["landingTime"] * 1000,
                           scale_y_ratio=minimum["pose"]["scaleYRatio"],
                           impulse_peak_after_contact_ms=(impulse["realTime"] - start) * 1000))
    return result


def stats(values):
    values = sorted(v for v in values if math.isfinite(v))
    if not values:
        return {"count": 0}

    def percentile(p):
        where = (len(values) - 1) * p
        low = int(where)
        high = min(low + 1, len(values) - 1)
        return values[low] + (values[high] - values[low]) * (where - low)

    return {"count": len(values), "min": values[0], "p10": percentile(.1),
            "median": statistics.median(values), "mean": statistics.mean(values),
            "p90": percentile(.9), "p95": percentile(.95), "max": values[-1]}


def summarize_capture(capture):
    rows = [row for row in capture["contacts"] if row.get("hasTile")]
    contacts = [row for row in rows if row["stage"] == "score-contact"]
    if not contacts:
        raise ValueError("No real score contacts: empty/aborted/wrong-song capture is not timing evidence.")
    submits = [row for row in rows if row["stage"] == "effect-submit"]
    requests = {}
    loads = []
    for row in rows:
        key = (row["frame"], row["sourceIndex"], row["nominalGameTime"], row.get("resource"))
        if row["stage"] == "effect-request":
            requests[key] = row
        elif row["stage"] == "effect-submit" and key in requests:
            loads.append((row["realTime"] - requests[key]["realTime"]) * 1000)

    def dsp_game(row):
        return row["clockOffset"] + max(0, row["dspTime"] - row["dspStart"]) * row["rate"]

    def callback_after_nominal(row):
        nominal_dsp = row["dspStart"] + (row["nominalGameTime"] - row["clockOffset"]) / row["rate"]
        return (row["dspTime"] - nominal_dsp) * 1000

    frames = capture.get("frames", [])
    peaks = visual_impact_peaks(contacts, frames)
    dsp_steps = [(b["dspTime"] - a["dspTime"]) * 1000 for a, b in zip(frames, frames[1:])
                 if b["dspTime"] > a["dspTime"]]
    return {
        "song_id": capture.get("songId"), "contact_count": len(contacts), "submit_count": len(submits),
        "contact_lateness_ms": stats((r["gameTime"] - r["nominalGameTime"]) / r["rate"] * 1000 for r in contacts),
        "contact_callback_after_nominal_dsp_ms": stats(callback_after_nominal(r) for r in contacts),
        "effect_submit_after_nominal_dsp_ms": stats(callback_after_nominal(r) for r in submits),
        "substep_backlog_ms": stats((dsp_game(r) - r["gameTime"]) / r["rate"] * 1000 for r in contacts),
        "music_minus_nominal_audio_ms": stats((r["musicCursor"] - r["nominalAudioTime"]) / r["rate"] * 1000 for r in contacts),
        "resource_load_ms": stats(loads),
        "frame_delta_ms": stats(r["frameDelta"] * 1000 for r in frames),
        "positive_dsp_steps_between_render_frames_ms": stats(dsp_steps),
        "user_delay_values_seconds": sorted(set(r["userDelay"] for r in contacts)),
        "visual_minimum_scale_after_contact_ms": stats(p["render_after_contact_ms"] for p in peaks),
        "visual_impulse_peak_after_contact_ms": stats(p["impulse_peak_after_contact_ms"] for p in peaks),
        "visual_peaks": peaks,
        "settings": capture.get("settings"), "measures_physical_audio_latency": False,
        "by_kind": {kind: stats((r["gameTime"] - r["nominalGameTime"]) / r["rate"] * 1000
                                for r in contacts if r["kind"] == kind)
                    for kind in sorted(set(r["kind"] for r in contacts))},
        "callback_by_kind_ms": {kind: stats(callback_after_nominal(r) for r in contacts if r["kind"] == kind)
                                for kind in sorted(set(r["kind"] for r in contacts))},
        "music_clock_residual_ms": stats((r["musicCursor"] - (dsp_game(r) - r["effectiveDelay"]
                                          - min(0, r["userDelay"]))) / r["rate"] * 1000
                                         for r in contacts if r["lapOffset"] == 0),
        "worst_contact_callbacks": sorted(contacts, key=callback_after_nominal, reverse=True)[:8],
    }


if __name__ == "__main__":
    paths = [Path(p) for p in sys.argv[1:]]
    if not paths:
        paths = sorted(Path(__file__).parent.glob("jingle-*-before.json"))
    summaries = [summarize_capture(json.loads(p.read_text(encoding="utf-8-sig"))) for p in paths]
    target = Path(__file__).with_name("runtime-summary.json")
    target.write_text(json.dumps(summaries, indent=2), encoding="utf-8")
    print(json.dumps([{k: v for k, v in s.items() if k not in {"worst_contact_callbacks", "visual_peaks"}} for s in summaries], indent=2))
