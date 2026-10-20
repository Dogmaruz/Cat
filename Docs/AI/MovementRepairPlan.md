# Dancing Cat repair and reference reconstruction

> Superseded by the user's later request to create a new offline recreation. Old production scripts were not repaired. See `ReferenceRecreation.md` for the current scope. The initial diagnostic scripts were moved out of Assets into the chat's work/legacy-diagnostics folder.

Goal: repair the existing game loop, then reproduce the reference's observable rhythm, steering, rotation and tail behavior using this project's assets.

Acceptance: repeated correct landings on short, long and moving tiles; no duplicate hit or false loss with overlapping colliders; reachable tiles remain controllable in all levels; motion follows the audio timeline; correct miss/restart/finish behavior; generator uses clip frames/channels and reproducible chart positions. A successful compilation alone is insufficient.

1. Record baseline compilation, real-scene bot run and narrow regressions before production changes. Bot may steer X but must never correct Y, Z, collisions or gameplay state.
2. Repair phase/landing timing and terminal tile handling in MovementController/TileController. Evaluate jump height from distance traveled between fixed segment boundaries, apply position once, and process crossed landing boundaries without FixedUpdate latency. Keep hit identity unique and validate expected tile footprint. Preserve prefab fields and input bindings.
3. Start audio on a scheduled DSP timestamp and make player progress consume the same song clock. Keep the geometry/BPM contract (`step=1.25`, `period=60/BPM`) of existing scenes. Do not claim old charts accurately represent every musical onset.
4. Correct generator stereo/rate/grid indexing with a synthetic failing audio fixture. Preserve MonoScript GUID and existing scene references. Do not silently regenerate authored levels.
5. Inspect cat pivot, rotation and tail joints/animation and compare to available reference evidence. Fix unstable physics ownership and rotation reset only where demonstrated; keep character assets.
6. Run real-scene short/long/moving repetitions at several frame rates, negative miss, reload/restart and end-of-level checks. Record any Android/device or visual evidence gaps. Review final diff independently.

Reference acquisition and static analysis run separately from source diagnosis. Downloaded artifacts stay in chat `work/reference`, never under project Assets. No downloaded application code or assets are incorporated into the game. Report known mechanics, inferences and unavailable evidence separately.

Execution decisions: use the same repository on a new local branch, not a worktree/copy, honoring the user's explicit constraint. Continue the requested repair while reference acquisition runs. No publishing, pushing or package updates are needed.
