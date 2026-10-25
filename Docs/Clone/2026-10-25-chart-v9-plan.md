# Short supports and broader glides implementation plan

**Goal:** Improve short Hold and Follow recognition on several additional original recordings, publishing only separate test levels.

**Architecture:** Retain the acoustic generator and spatial route composer as separate stages. Diagnose note-specific attack/body/release evidence before extending recognition. Preserve supported legacy gestures, and assess type and interval accuracy separately from ordinary event timing.

**Tech stack:** Unity 6000.5.4f1, existing C# Editor pipeline, local pinned audio-analysis models and NumPy/SciPy tools. No new network service, music generation, package migration or player builds.

## Constraints and evidence contract

- Preserve all original maps and audio, both currently published new levels and their catalog records, handwritten score, courier assets/animation, and the four existing user changes.
- Create only additional test IDs under CustomData; source audio stays in place. No original map annotation enters inference.
- Calibration recordings: DisconnectPop and CanCanPiano. Frozen recordings: IJustWannaDancePop and MoonlightCloudsPiano. Selection uses aggregate labels for stratification only. Do not inspect frozen signal features or candidate errors until the algorithm is fixed.
- Capture baseline at `8a376c5` before recognition changes, with matching PCM/vocal measurements/speed/preroll for candidate comparison. V9 additionally measures mixed-note evidence; do not claim that the complete feature set is identical. The earlier four recordings are known calibration/regression material, not independent validation.
- Short Hold: duration at most .45 s, correct type, onset within 80 ms, end within 80 ms and interval IoU at least .5. Follow: onset within 80 ms, end within 150 ms and IoU at least .5. Use one-to-one matches and report missing/extra supports.
- Full Editor validation and input-driven playthroughs must use the actual published test maps. No seeks, shortened songs or simulated scoring as a substitute for contacts.

## Tasks

- [x] Snapshot protected resources/current levels and export actual decoded PCM for four new recordings.
- [x] Capture v8/route-v1 baselines and typed interval metric fixtures before editing recognition.
- [x] Diagnose short-note body/release and missing short/long glide proposals; record experiments and rejected hypotheses.
- [x] Write failing counterexamples, implement measured recognition with independent support boundaries, and preserve regression fixtures.
- [x] Fix compiler constraints only where original/native evidence demonstrates supported short glides; validate entry, markers, exit and spatial reachability.
- [x] Compare calibration and regression results, fix parameters, then evaluate the two frozen recordings without tuning to their errors.
- [x] Publish only the four separate test levels with transactional guards; preserve earlier playable maps and raw catalog records.
- [x] Complete fresh full Editor checks/input-driven playthroughs on all four published test levels.
- [x] Package honest results, limitations, paired reports, training/freeze provenance, runtime captures and integrity evidence.
- [x] Commit/push scoped changes and leave a test song ready in Unity.

## Initial hypotheses

The existing global mixed-pitch sustain requires at least .55 s, and the vocal sustain policy generally requires .45 s. Neither directly measures shorter polyphonic instrumental note bodies. Lowering a duration/RMS threshold alone previously produced false supports.

The current Follow detector requires exactly five mixed attacks spanning 1.05–1.6 s, an irregular rhythm, continuous tonal energy, a following rest and a nearly identical repeated phrase. The compiler additionally rejects Follow below one second. Existing LABUBU maps contain many shorter gestures, and the newly selected Can Can has both short and much longer native Follow intervals. These are hypotheses to validate against independent source measurements, not permission to copy their times into inference.

## Current evidence

The isolated short-Follow family adds five correct Can Can proposals with no
extra proposals on the other five known recordings. The compiler now accepts
.25–6-second Follow while retaining native point-spacing and lateral bounds.
The frozen algorithm adds 2/41 correct Disconnect short Holds and 8/42 on known
Moonlight without extra short Holds, but adds one false Hold on LABUBU. The
leave-one-song-out result is 0 true positives and 1 false positive. On the two
previously frozen recordings, short Holds total 2 true positives, 6 false
positives and 28 false negatives; neither receives a new correct Follow.
Experimental short Holds fail to establish generalization and remain default-off.
Can Can event F1 decreases .842857 → .836923 despite five correct new gestures.

All eight candidate geometries validate. Baseline Disconnect fails only the final
Finish approach, now handled without inventing a note or support. Four test maps
are published, with original resources, the two existing maps/metas and their raw
catalog records unchanged. Fresh full runtime validation passes for all four
published maps: 117.995, 50.260, 35.448 and 66.011 seconds, with real contacts,
pause/resume, finish, three stars and retry; no seeks, teleports, shortened charts
or runtime errors. Clean Edit Mode restored. All 17 algorithm/17 measurement
freeze hashes, 412 protected file hashes and the two old raw catalog records
remain unchanged. Runtime success does not establish musical classification
accuracy. The two control recordings have now been inspected and cannot be reused
as unseen evidence for future tuning. See `ChartCalibration/V9/README.md`.

Delivered on `feature/radio-archipelago`: trainer `9f9af68`, implementation,
four test levels and evidence `82c9da4`, pushed to origin. Final Editor preview
is Play Mode, Menu, `CanCanPianoGeneratorTestV9`, clock zero, no compilation
and no unsaved scene changes. See `ChartCalibration/V9/unity-preview.json`.
The four pre-existing user changes remain outside these commits.
