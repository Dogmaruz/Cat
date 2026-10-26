# V12 short Hold: one calibrated source-motif addition

All fifteen recordings are known calibration/regression material. Strict native results improve **44 TP / 3 FP / 179 FN to 45 TP / 3 FP / 178 FN** across 223 reference short Holds. Both interval durations remain <=450 ms, start/end <=80 ms, IoU>=.5, with maximum-cardinality one-to-one matching. No source identity, absolute pitch/time, reference coordinate, path or target count enters inference.

The only semantic change is Disconnect's ordinary Jump at 6.252336 becoming Hold 6.252336–6.487336. Its reference is 6.258117814–6.492494223. The recording retains 218 elements, identical decoded-PCM identity, and all other interval types/boundaries. The other fourteen recordings have identical segment kind/time/end sets. Für Elise and other instrumental misses remain unresolved; no general instrumental recognition improvement is claimed.

## Adoptable candidate

`PcmShortSupportAnalysis-v12-frozen.cs` SHA-256 `610100ec5f5147b095b96973bbcd546d62cdcc2e019c3c9c1d1fd943894a356c`.

`PcmShortMotifValidation-v12-frozen.cs` SHA-256 `e79e25cb40c0874c63a753b1177e9e31ad0bcb00a4a9271276729f04ec98a153`.

The frozen V10 StyleScore, existing accepted supports, optional-evidence behavior and default-off setting are retained. The added recurrence branch observes only mixed-source note evidence around a fixed snapshot of already accepted >=.95 V10 seeds. It compares seven attacks: pitches relative to the selected pitch and six gaps normalized by their local median. Two seed instances separated by more than three measured pulses are required. Projected exits must agree with the selected measured endpoint within 1/12 pulse. Candidate NN and harmonic measurements must independently support that exit within 1/4 pulse; a harmonic ringing tail alone cannot create a support. Seed/candidate measured durations remain comparable within .35 pulse. New inferred candidates never join the seed snapshot, so inference cannot feed back recursively. Existing protection and timestamped same-pitch/octave repeat rejection still run first.

The new source proposal matches independently accepted motifs at 17.50675737 and 28.75120181. Its NN/harmonic durations are .2933/.3093 seconds. Without dual-release confirmation the same broad motif matcher also proposes an ordinary articulation at I Just Wanna Dance 15.38158730–15.59111111: its NN duration is only .0524 seconds despite a .2095-second harmonic tail. That counterexample is rejected by source measurements, without an identity/time exception. One-seed motif propagation gives additional extras and is rejected.

The .35-pulse duration comparison is material: a diagnostic replacement by 1/3 pulse loses the sole addition because one seed/target NN-duration difference is about .334 pulse. The delivered frozen policy restores the already tested .35 comparison. This sensitivity is a calibration limitation, not evidence of generalization.

## Production-linked validation

`Harness/Check.csproj` independently compiles the exact two frozen candidates, actual existing V10 validation, and pinned production generator, source/evidence validators, native compiler, route composer and geometry validator. Unity loading/import/export APIs alone are stubs; source PCM comes from the original `Library/VocalAnalysis/<sourceSha>/source.f32` exports. Actual `GenerateWithInstrumentalSamples` checks decoded PCM/evidence identities and runs the full source-to-native pipeline. All fifteen generated maps pass actual `AudioChartGeneratorValidation.ValidateMap`.

Original V10 code is RED on a missing third repeated motif. The frozen candidate is GREEN on the same input and on one-seed rejection, protected overlap, weak NN/harmonic-tail rejection, incompatible projected exits, immutable source, no recursive feedback, time/register translation, tempo scaling inside short-duration eligibility, and actual native Hold entry/exit/tick compilation. The source-only helper is tested separately for tempo invariance because the preserved V10 voice model intentionally observes fixed millisecond windows.

Proofs: `production-linked-proof.json`, `csharp-frozen-full-pcm.log`, `red-frozen-baseline.txt`, `green-frozen.txt`, `geometry-all15.txt`, and per-recording `*.full-pcm-generated.json`. The production-linked proof records exact linked-source hashes and every baseline/candidate/PCM identity. Root owns Unity queue/runtime validation and copying these frozen candidates into Assets.

## Other hypotheses and reference semantics

Source candidate coverage is 208/223 after independent-repeat rejection, including 31/32 Für Elise and 23/23 Bajo El Sol. Foreground-register/accompaniment plus relative note-length rules are insufficient: the tested strong lower-entry/length-contrast variant has 9 TP/43 FP. Separating attack-type recognition from endpoint selection removes contradictory endpoint labels but still fails transfer: the best tested leading-source event model gives 2 TP/37 FP grouped leave-recording-out, even with diagnostic oracle exits. Sparse physical conjunction search also finds no adoptable source-only classifier. These rejected results are retained in `phrase-direct-proof.json`, `event-type-proof.json`, and `beam-rule-proof.json`.

Tempo/transposition-invariant source-motif exploration, without dual release, includes false ordinary attacks; stricter motifs abstain. Separated-voice contour recurrence with confidence/energy gates yields no additions (`vocal-motif-proof.json`). The adopted branch is the narrower two-instance source motif plus independent release confirmation, with native validation of its single clean calibration gain.

`reference-semantics-proof.json` checks all 223 reference short Holds against retained raw MapData: every one is native id2; duration equals `-se.y / forwardSpeed`, and exit equals `(pos.z - se.y) / forwardSpeed - delayTime`. There are 2–6 authored tick entries per short Hold. No `isend` flag is being mistaken for an interval boundary. Measured pulse-relative reference durations vary substantially: Für Elise median .684 pulse, Ode to Joy median .370, Can Can median 1.056, Canon median 1.004. A universal rule equating a Hold with one long acoustic note is therefore unsupported; these are authored support intervals.

## Generalization limit

Motif inference uses source-only descriptors but depends on the frozen voice-dependent V10 seed detector, which was trained on eight of these recordings. All15 thresholds/variants are exploratory calibration. The 45/3 result is **not independent grouped LOO or unseen-song accuracy**. Rejected standalone event-model grouped LOO must not be conflated with the adopted seed-and-recurrence branch. No model/package/audio/settings/map/catalog changes or downloads were made by this subtask; production adoption remains the root's responsibility.
