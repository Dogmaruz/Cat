# Independent v10 evidence audit

All diagnostics are evaluation-only on the eight recordings already inspected in
v9. They are now calibration/regression material. Neither newly selected held-out
recording's per-event times, audio features or generator errors were inspected.
The initial diagnostic phase changed no tracked files, playable levels, source
audio or Unity state. A later delegated guard implementation changed only
`PcmChartStudyV10.cs`, as recorded below.

## New held-out proposal

`../selection-proposal.json` selects FurElisePiano and JingleBells by aggregate
counts/duration density, excluding eight known IDs and every source hash already
represented in committed vocal/instrumental evidence. Selection hashes source
audio bytes for identity only; it never decodes audio. It aggregates only gesture
kind/duration fields. The full selector is `select_heldout.py`.

FurElisePiano has 32 short Holds and the highest remaining short-Hold density
(33.9806/min). JingleBells has eight Follow gestures, all .5–1 seconds, and the
highest remaining Follow density (7.5/min). Density-first selection does not
maximize duration diversity. Other ranked candidates are retained transparently
as aggregates. The two sources have different audio hashes.

## Measurement versus classifier loss

`v9-hold-diagnostic.json` separates onset coverage, source-supported endpoint
coverage and coverage by the current v9 style rules. These are oracle upper
bounds without competing support protection or final greedy proposal selection;
they are not recognizer precision/recall.

| Known recording | Reference shorts | Onset coverage | Bounded endpoint coverage | V9 style coverage |
| --- | ---: | ---: | ---: | ---: |
| Unstoppable | 1 | 1 | 1 | 0 |
| Twinkle | 2 | 2 | 2 | 0 |
| Moonlight | 42 | 42 | 40 | 8 |
| LABUBU | 4 | 4 | 4 | 1 |
| Disconnect | 41 | 35 | 31 | 2 |
| Can Can | 5 | 5 | 3 | 0 |
| I Just Wanna Dance | 26 | 26 | 26 | 2 |
| Moonlight Clouds | 4 | 4 | 3 | 0 |

I Just Wanna Dance loses nearly all recoverable candidates at style selection.
Disconnect also has a real source-candidate/boundary ceiling. Five Can Can
reference starts exist, but only three existing endpoint candidates pass the
strict boundary criterion. Counting attacks or matching Hold onset alone would
hide these differences.

Current false-proposal families are not homogeneous. Two I Just Wanna Dance
shorts lie inside an unrecognized reference Follow; one has the right Hold start
but wrong exit, one is near a Jump. Both Moonlight Clouds shorts coincide with
reference BigJump starts. A short-Hold-only classifier cannot be evaluated as
though neighboring gesture-family recognition were already complete.

The fixed v9 `voiceTail` window is .25–.42 seconds after onset. For .198–.230s
candidates, that entire feature comes after the selected support exit and can be
dominated by a later articulation. Its `peak` window likewise ignores the
candidate-specific endpoint. Duration-relative body/release features and explicit
separation from subsequent attacks are a justified next hypothesis; simply
lowering energy or duration thresholds is not.

## Missing-onset countercheck

`missing-onset-probe.json` inspects the cached full-range note model only on the
eight known songs. All six missing short-Hold onsets belong to Disconnect. Four
have strong restricted-range peaks 65–77ms before the reference attack, near the
80ms evaluation boundary. Widening MIDI 48–84 is not a general solution: the
strongest full-range peak is often unrelated bass MIDI36. Two other misses have
weak note activations .134/.181. Do not turn these evaluation times into fixed
onset offsets or relax the metric to make them pass.

## Metric safeguards

Production evaluation uses maximum-cardinality one-to-one matches, not greedy
nearest onset assignment. Both durations must be <=.45s, start/end errors <=80ms,
IoU>=.5. Keep training labels and experiment evaluation aligned with that
contract. Old offline trainer selection/evaluation used greedy nearest-reference
assignment; its candidate positives should instead check any eligible reference,
and proposal counts should use augmenting-path matching like production.

Suggested counterexample: keep a short candidate's body/exit unchanged, perturb
an unrelated following articulation inside the old .25–.42s tail window and
verify classification is not spuriously driven by that later note. Also count
reattacks only up to the selected candidate endpoint when neural notes outlast
that endpoint. These are hypotheses for implementer validation, not completed
fixes in this read-only audit.

## Delegated evaluation-guard implementation

The parent later authorized a focused implementation in `PcmChartStudyV10.cs`.
Cached baseline reuse now validates the exact v9 generator, stage, source map,
audio/actual decoded PCM, vocal digest, archived V1 note digest, speed/preroll,
typed report identity and generated-map SHA. A failure marker blocks reuse.
Candidate comparisons use the same validation; cached baselines are never
silently regenerated. Actual baseline files were not changed.

V2 note measurements are accepted only through the explicit source/PCM/version/
feature-digest-bound transition manifest; V1 measurements remain archived under
`before/measurements`. Freeze permits now bind selection, baseline provenance and
measurement-transition manifests, and reject null entries before enumeration.

`GuardHarness/green.txt` links the real production C# source with infrastructure
stubs and exercises fixture-only file guards. `red-existence-only.txt` confirms
that reverting the cache check to the former existence-only behavior fails on a
missing generated map. This is a local harness countercheck, not a substitute for
the parent's real Unity compilation. No Unity queue or builds were run here.
