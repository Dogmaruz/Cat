# Tile route choreography implementation plan

**Goal:** Apply independent left/right route composition and native sideways moving tiles to both new playable levels and future automatic levels, preserving the musical map and original resources.

**Architecture:** A pure Editor postpass operates on the compiled `MapTimeline`. The audio detector keeps responsibility for attacks, durations and tile kinds. `ChartRouteComposer` chooses horizontal motifs using rhythmic cells, available flight intervals and fixed Follow boundaries. A guarded bulk Editor command applies the same pass to CustomData only.

**Tech stack:** Unity 6000.5.4f1, C#, existing native tile contract. No new packages or player builds.

**Design:** Always alternating sides would contradict the observed same-lane repetitions; mapping pitch directly to X would assume an unsupported original rule. Use repeated rhythmic motifs with small rapid steps, occasional same-lane repeats and wider slow replies. Native autonomous oscillation (`updateType=3`) is distinct from a rider-following long support (`updateType=2`). Encode its desired contact X through the inverse sine phase. Keep existing Follow paths exact.

## Constraints

- Exactly two current playable new maps: AlwaysRememberMeowCat and UnstoppableGeneratorTest; original 58 maps and all audio untouched.
- Preserve v8 event samples, cue kinds, support boundaries, Z coordinates, jump distances and hold checkpoints; keep handwritten score untouched.
- Deterministic, repeatable and safe to reapply. No original map/song identity is an inference input.
- Mirror element/segment motion metadata. Test actual positions, rather than confusing the oscillator phase with its landing lane.
- Preserve the four existing user changes. Keep current courier models and animation.

## Tasks

- [x] Study the existing generator, native runtime and 58 original maps. Archive current maps and protected hashes.
- [x] Write failing route tests; implement the isolated compositor; verify determinism, idempotence, rapid reachability, fixed supports and true movement in both directions.
- [x] Integrate future automatic generation and a guarded bulk operation for all current custom maps. Verify musical timing and original resource preservation before publication.
- [x] Extend geometry validation to actual moving contact lanes and interval-aware lateral limits; retain authored-score behavior.
- [x] Verify moving tiles through actual Editor ticks, pause and real pointer-driven full playthroughs of both levels. Cover moving-tile retry/revival if affected.
- [x] Save numerical route comparisons, current previews and limitations.
- [x] Commit/push scoped changes and leave the custom song ready in Unity.

Implementation `4bf85cb` was pushed to `feature/radio-archipelago` after the
fresh full Editor playthroughs. Final inspection shows the clean game scene in
Play Mode at the menu with `AlwaysRememberMeowCat` selected. The four existing
user changes remain outside the implementation commit.

## Evidence baseline

The 58-map source study observed 7,257 support transitions: 31.1% stay in the same lane. Median absolute shift is .5. For intervals below .18 seconds, median shift is .25 and the 95th percentile is .5. For .18–.30 seconds, the median is .5 and 95th percentile is 1. For .30–.50 seconds the median is 1 and 95th percentile is 2. Typical static coordinates are 0, ±.5, ±1, with quarter steps for fast patterns. Native moving support runs usually contain two or three Jump/BigJump tiles; Hold remains stationary. These observations inform a new choreography policy; they do not recover a unique closed original generation algorithm.

## Current evidence

Actual Editor compositor, native moving contact and integration checks passed.
Both published maps have equal strict before/after rhythm fingerprints, unchanged
Follow fingerprints and idempotent reapplication. Fresh real-audio regeneration
reproduces all integer event samples, types and spatial layout; double fields
permit only <=1e-8-second JSON noise. Event corruption checks remain strict.

Custom: 130 left / 99 right / 90 centre logical Jump/BigJump/Hold supports,
12 native movers (6 leftward / 6 rightward at contact). Test Unstoppable:
46 left / 58 right / 34 centre, 9 movers (4 / 5). Native component checks
exercise 20 moving combinations (5 lanes x 2 flags x 2 speeds), six static
contacts and one native approach interpolation. Revive now uses the actual
contact point; its calculation is covered, not the complete revive UI flow.
Fresh actual full playthroughs passed: Unstoppable 67.004 seconds, all nine
sideways targets contacted; custom 157.153 seconds, all twelve contacted.
Actual native transforms moved >.15 units, both flags were exercised, pause
froze moving targets, three-star normal finishes and retries passed. No seeking,
teleporting, shortened maps or runtime errors in the reviewed flow. The Editor
returned to Edit Mode with a clean scene. Numerical reports, previews and
limitations are saved under Docs/Clone/ChartChoreography. Scoped commit/push
and the final custom-song preview are owned by the parent and remain pending.
