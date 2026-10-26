# V11 short Hold research: no model adopted

Production `PcmShortSupportAnalysis.cs` and its validation remain byte-for-byte unchanged by this work. The V10 experimental short-Hold policy and its default-off setting are retained. No evidence producer/schema, model, audio, reference, map, catalog, Unity queue, scene or build was changed. All experiments live in ignored Library. No new held-out Bajo El Sol or Baby Shark measurement arrays, events, errors or results were opened.

## Failure boundary and calibration source coverage

All ten V10 recordings and the three explicitly selected V11 calibration recordings are known tuning material. The strict metric remains both interval durations <=450 ms, start/end <=80 ms, IoU>=.5, and maximum-cardinality one-to-one matching. Every study candidate/reference satisfies the exact 450 ms duration bound, so the inherited matcher's small numeric tolerance does not affect any result.

| Known recording | Reference short Holds | Covered by source endpoints | After independent-repeat guard |
|---|---:|---:|---:|
| Unstoppable | 1 | 1 | 1 |
| Twinkle Little Star | 2 | 2 | 2 |
| Moonlight | 42 | 41 | 41 |
| LABUBU | 4 | 4 | 4 |
| Disconnect | 41 | 31 | 30 |
| Can Can | 5 | 4 | 4 |
| I Just Wanna Dance | 26 | 26 | 26 |
| Moonlight Clouds | 4 | 3 | 3 |
| Für Elise | 32 | 31 | 31 |
| Jingle Bells | 0 | 0 | 0 |
| Ode to Joy | 33 | 33 | 33 |
| Canon in D | 3 | 3 | 3 |
| Aaj Ki Raat | 7 | 7 | 7 |
| Total | 200 | 186 | 185 |

Für Elise's dominant loss is classification: 31 of 32 reference intervals have source candidates, all 31 survive repeat rejection, and 0 pass the frozen V10 score. This coverage is before candidate competition, protected legacy supports/launches and native compilation; it is not a claim that 186 Holds could be safely published.

## Rejected classification ablations

All supplemental models use the same CART depth 8, minimum 16 candidate rows, >=.90 raw leaf precision, and at least two positive candidate rows from at least two training recordings. Standard training uses inverse per-recording row weights. A final objective ablation balances each class within each training recording, while retaining the same raw precision/support acceptance rule. No recording identity, absolute time, absolute MIDI, requested count or reference timing enters inference.

| Thirteen-recording supplemental inputs | Calibration raw TP/FP | Supplemental leave-one-recording-out TP/FP |
|---|---:|---:|
| Existing 24 non-voice features | 0/0 | 2/5 |
| Non-voice + relative release/simultaneous competition, 36 features | 9/2 | 0/1 |
| Non-voice + all-source melody context, 44 features | 0/0 | 0/3 |
| Voice+source + all-source melody context, 54 features | 36/5 | 12/21 |
| Class-balanced existing 24 | 0/0 | 0/1 |
| Class-balanced relative 36 | 0/0 | 0/0 |
| Class-balanced full context 54 | 0/0 | 0/10 |

The nine-correct source-relative result includes 7 Ode to Joy, 1 Disconnect and 1 I Just Wanna Dance, but adds ordinary attacks at Canon 20.80725624→20.99882086 and Für Elise 8.10448980→8.36752835. These remain source counterexamples, never inference overrides. A ten-recording source-only tree has a 146/157=.930 precision leaf supported only by Für Elise; the multi-recording guard correctly rejects it. The wider study and earlier ten-recording ablations are detailed in `INVESTIGATION.md`.

## Bounded selected-interval mixed-NN pilot

The final pilot directly tested the measurement-window hypothesis. It reused full 88-pitch note/onset arrays for only the allowed 13 PCM hashes from V10 ExpandedInstrumentCache and V11 CacheInstrumentEvidence. PCM identity and exact `input-window-time-v2` timing are checked; pitch index is `midi-21`. No producer/schema/assets changed.

Twenty-four new source-only measurements describe the selected pitch's body (`start+30ms..end-25ms`), release (`end+15ms..end+75ms`), peak, early/late decay, interior/release onset, preceding energy, simultaneous upper/lower pitch dominance and relative preceding/following lead context. Flat spectra have no lead, avoiding a false absolute-register offset from array index0.

| Source-only variant | Calibration raw TP/FP | Supplemental LOO TP/FP | Piano LOO TP |
|---|---:|---:|---:|
| Replace six fixed NN/harmonic body/late/tail inputs with interval 24 | 0/0 | 3/8 | 0 |
| Keep original 24 and append interval 24 | **3/0** | **3/8** | **0** |

The three calibration true proposals are **2 Ode to Joy + 1 Disconnect**. There are two in-sample piano recognitions; there are no correct piano LOO proposals. Merging these three additions after unchanged V10 proposals gives 47 TP/3 FP source proposals across known 13, compared with 44 TP/3 FP for the frozen V10 source policy. These are source-policy results before native protection/compilation, not published-map metrics. The three clean calibration additions do not justify adoption given 3 TP/8 FP LOO and no piano LOO success. Hold experiments stop with no production change.

## Jingle legacy Hold continuity

Jingle's protected 21.328→22.615 long Hold contains fresh leading notes 70→68→63, confirmed by mixed-NN onsets, harmonic durations and high-confidence separated voice. MIDI 70 at 21.328 has NN/harmonic durations .249/.259 s; MIDI 68 at 21.588 has onset .679/peak .773 and durations .279/.289 s; MIDI 63 at 21.948 has onset .670/peak .692 and durations .662/.687 s. This contradicts a single sustained pitch, but a multi-pitch phrase can still be authored as Hold. No general legacy-rejection rule was established against Unstoppable's 14 accepted Holds; priority remains unchanged.

## Verification and artifacts

Fresh checks passed: all 22 existing `Tools/RhythmAnalysis/test_*.py` tests and 5 research interval-NN checks. The latter cover selected-exit ownership, later-note independence outside release+75 ms, clock translation, register translation and other-pitch dominance. Three deterministic grouped fixtures give exact original/optimized CART full-tree equality. `git diff --exit-code` confirms no edits to either owned production C# file.

Key proof files in this directory:

- `final-hold-study-summary.json`: calibration input identities, coverage, aggregate rejected-model metrics, NN cache identities/hashes, source hashes and verification summary.
- `known13-interval-neural-proof.json`: final pilot feature names, source cache provenance, full models/rules, per-recording calibration/LOO and proposals.
- `known13-supplement-proof.json`, `known13-context-proof.json`, `known13-balanced-proof.json`: rejected classification models and full per-recording scores.
- `known10-diagnosis.json`: original V10 loss boundary and source feature quantiles.
- `interval_neural_v11.py`, `pilot_interval_neural_v11.py`, `test_interval_neural_v11.py`: throwaway pilot implementation, offline training/evaluation and checks. These are research artifacts, not an unused production API to commit.

No instrumental Hold improvement is claimed. Source candidate coverage is strong; transferable type/style classification remains unresolved. The frozen voice branch is never counted as supplemental LOO evidence because it was trained on some of the excluded recordings.
