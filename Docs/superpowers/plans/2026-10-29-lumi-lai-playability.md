# Lumi-Lai Playability Implementation Plan

> **For agentic workers:** Root owns the Unity queue and freezes all source changes before native Play review. Use the existing local analysis and Editor verification workflow.

**Goal:** Make the approved Lumi-Lai course readable and fair with full-size tiles, a centered opening and slightly narrower lanes; add a complete turn during long air jumps.

**Architecture:** Keep musical source/clock and v12 analysis. Add an opt-in physical-support grouping pass for dense musical ornaments, preserving their measured scoring onsets as native checkpoints. Spatial authoring reserves normal tile radii, repairs feasible lane gaps and selects only safe movers. Courier rotation remains presentation-only.

**Spec:** Current user requests: narrow the lateral spread slightly; first2–3 landings near center; no tiny tiles; resolve dense notes in generation as connected support where appropriate; full body turn on long jumps.

## Constraints and review focus
- No builds, new Runway generations, changed song speed or collision-rule shortcuts.
- Preserve reference maps, old authoring behavior and user preferences.
- New lane scale1.8 instead of2.1; fixed ordinary radius0.65m.
- Preserve each grouped musical onset and confirm native checkpoint collection through short-support handoffs.
- Keep long-tile shape, terrain clearance, grounded finish and camera behavior.

## Tasks
- [x] Reproduce shrunken radius and unsafe rapid clusters; save current course baseline.
- [x] Implement opt-in first3 centered landings and2 gradual transition supports.
- [x] Group infeasible dense musical ornaments and preserve original scoring accents.
- [x] Author full-size clearance with safe movers and existing steering budgets; regenerate narrower island course.
- [x] Add and verify one complete turn for flights of at least0.65s, preserving short-jump and hold behavior.
- [x] Compile, run focused checks and full native Editor review; leave scene ready and document evidence.

Native review `20261029-195212-088` PASS: complete original recording, 228/228 actual scoring contacts, 34/34 HoldPoints, 20 grounded Signal Sparks, zero missed events or geometry clips, exact0.65m ordinary radius, restored profile/input. Full span-mesh audit:414coexisting pairs,0violations. Source music/scenery unchanged. Evidence: `Docs/Design/RadioArchipelago/LumiLaiPlayability20261029/Review`.
