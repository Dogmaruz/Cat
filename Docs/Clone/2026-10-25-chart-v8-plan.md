# Chart v8: foreground articulation and wider evaluation

User contract: continue improving automatic charts toward the original game's
musical behavior. Preserve original maps, audio, current character motion and
the user's four pre-existing changes. No player builds.

The same original audio has two different versioned charts, so PCM cannot
uniquely determine every tile type or route. Improve measured articulation
first, keeping explicit choreography policy separate. Reference annotations
remain evaluation inputs only.

- [x] Preserve v7 maps/metrics and protected source hashes outside Assets.
- [x] Capture v7 baselines with matching vocal measurements on two additional
  calibration recordings and one frozen validation recording. Use a common
  0.6-second preroll and audio-axis metrics; do not publish benchmark maps.
- [x] Add independently measured foreground attack features with a versioned
  integrity digest and backwards-compatible legacy evidence. Prove corruption,
  timing and missing-feature behavior with counterexamples.
- [x] Improve ordinary accent selection without changing the physical attacks
  used to measure existing support boundaries. Retain instrumental passages,
  true fast rearticulations, confirmed Hold/Follow and large launches.
- [x] Add low-register large-flight recognition only if measured articulation
  distinguishes it from ordinary same-pitch syllables without reference times.
- [x] Freeze parameters before opening validation errors; compare matched,
  missing and extra entries and support boundaries for every recording.
- [x] Publish only the separate Unstoppable test and regenerated custom map;
  run Editor regressions and full input-driven playthroughs. Record actual
  gains, regressions and remaining style/duration limitations honestly.

Observed final metrics and limits: Unstoppable 7/7 BigJump, 14/14 Hold,
2/2 Follow; event F1@80ms .862275 → .884146. Dense Twinkle .674242 →
.882540; Moonlight .932271 → .931174; frozen LABUBU .509728 → .517787.
High timing F1 does not imply correct support types: Twinkle's 25 BigJump
and 9 Hold, Moonlight's 42 Hold, and LABUBU's 16 Follow remain largely or
entirely unrecognized. No short-Hold threshold reduction was accepted.
Custom published v8: 333 elements, 300 Jump, 14 Hold, 2 Follow, 5 BigJump.
Fresh actual Editor playthroughs passed for the complete 67.004-second
Unstoppable test and 157.153-second custom recording, without seeking or
teleporting. Actual contacts, supports, glides, pause/resume, results and retry
passed; the reviewed flow produced no runtime errors. Evidence, foreground,
rapid and original checks passed in Editor. All 410 protected hashes and all
nine normalized-LF algorithm freeze hashes were verified unchanged. Final
inspection returned Edit Mode and a clean scene. No player build was created.

Review focus: accompaniment peaks inside a vocal body; real silence with
instrumental rhythm; 125ms articulated notes versus duplicate detections;
legacy pitch-clock compatibility; unsupported feature digests; tuning leakage
from the frozen recording; unchanged original resources and runtime motion.
