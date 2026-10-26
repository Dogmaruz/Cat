# V11 source-only Follow verification and chordal responses

The two production files owned by this subtask are `PcmMelodicFollowAnalysis.cs` and `PcmMelodicFollowValidation.cs`. Production contains no recording names, reference-map reads, annotation times, absolute MIDI pitches, target counts or label-derived lookups. All proposals use verified note measurements. Reference maps enter only Library evaluation scripts.

Thirteen known/calibration recordings were inspected: the ten opened through v10 plus Ode to Joy, Canon in D and Aaj Ki Raat. The new v11 heldout recordings remain closed to this subtask. These results show calibration/regression performance, not independent generalization.

## Changes

1. Neighbor trills and wider tremolos require independently resolved neural note durations in at least 60% of their adjacent articulation intervals. A single short first note cannot validate the remaining run. Reused activations spanning their next same-pitch return no longer suffice. A few uncertain entries remain possible in an otherwise articulated phrase. The two new v10 Fur Elise false trills fail this measured articulation check; the CanCan trill and articulated tremolo remain.
2. `RejectsLegacyArticulation(evidence, cue)` challenges the legacy spectral detector only when six or more strong fresh tonal groups disclose regular subdivisions over at least 75% of its proposed interval. The old detector requires five irregular mixed attacks; those omitted subdivisions contradict its claimed signature. This does not assert that regular music cannot be a Follow: separate melodic/chordal analysis may justify one. The actual two Fur Elise legacy proposals fail; the actual two Unstoppable legacy proposals survive. The parent integrates this verification before legacy proposals reserve Hold intervals.
3. A sustained chordal response family recognizes a foreground note with an independently sustained second pitch class, matched tonal and neural body duration, and recurring relative harmonic support/duration. New independent notes inside the body veto the sustained interpretation, preventing resonant accompaniment under a moving melody from acquiring a false Follow. The nearest connected measured pickup and subsequent confirmed contrast delimit the native interval. Constant lead pitch can therefore form a moving gameplay phrase when its multi-voice context is supported. Lateral movement remains independent choreography.

The chordal prototype without its inner-articulation veto produces additional CanCan and Fur Elise false candidates. The counterexamples are retained in `chord-inner-articulation-ablation.json`; source and synthetic checks support keeping the veto. No instrument-name blanket exclusion is used.

## Actual production-linked C# checks

`Harness/Review.csproj` links the actual two production files, evidence verifier, score, MusicalChartCompiler, ChartRouteComposer and recovered runtime map types. Unity/Editor/resource/options infrastructure is stubbed. `actual-production-green.txt` records synthetic checks, validation of all thirteen source caches, native Compile+Apply for fixtures and all nonempty source proposals, and legacy-verifier decisions.

Synthetic coverage includes sustained-trill rejection; first-note-only tremolo rejection; valid sustained chord voices; isolated, monophonic, weak, ambiguous-upper-voice and inner-articulation counterexamples; nearest pickup; measured contrast; protected support priority; legacy regular/irregular/reused-note cases; pitch/time translation; endpoint corridors; and actual native route compilation.

`articulation-chord-red.txt` records the failing sustained-activation counterexample before implementation. Production was copied from the research candidate only after the parent sealed all fifteen v10 baselines.

## Raw proposal metrics, before parent support merging

`known-thirteen-source-metrics.json` uses one-to-one maximum-cardinality matching with unchanged criteria: correct Follow type, start within 80 ms, end within 150 ms, IoU at least 0.5. Timing and lateral-path errors are separate.

| Recording | Raw source proposals | Correct / false / missed | Start MAE / max | End MAE / max |
| --- | ---: | ---: | ---: | ---: |
| CanCan | 19 | 19 / 0 / 1 | 6.893 / 22.602 ms | 5.850 / 21.291 ms |
| Jingle Bells | 5 | 5 / 0 / 3 | 25.946 / 53.718 ms | 8.599 / 24.331 ms |
| Fur Elise | 0 | 0 / 0 / 1 | — | — |

All other ten recordings receive no new proposals. Existing Unstoppable supports are evaluated separately through legacy retention. LABUBU's 16, Clouds' 11, Canon's 4 and Aaj Ki Raat's 7 originals remain unresolved. No missing gestures are filled from annotation timings, periodic occurrence counts or reference path samples.

All nineteen CanCan cue objects, including every time, end, lane, point, reason and phrase identifier, remain exactly equal to the v10 source proposals (`can-nineteen-unchanged.json`). Normalized-phase path MAE remains 0.51275 lanes, or 0.35199 allowing a mirror. Jingle path MAE is 0.71460, or 0.57019 allowing a mirror. The timing recovery is stronger than path similarity; paths are not claimed to reconstruct the original choreography.

One raw Jingle proposal is initially masked by a pre-existing false long Hold from 21.328 to 22.615. The measured source inside that Hold has independently articulated pitches 70, 68 and 63 rather than a continuous initial pitch body. The parent owns long-Hold verification and final protected-support merging. Without any change there, four of the five raw Jingle proposals survive, and the report must use the integrated count. Valid legacy Hold priority has not been relaxed by this subtask.

No Unity queue, player build, map/catalog/audio/model/user-state edit, stage or commit was performed by this subtask.
