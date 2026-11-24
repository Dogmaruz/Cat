# Coral Oasis — second playable island

**Goal:** Build the approved coral-oasis overview into a separate Unity scene with a playable winding, ascending/descending floating tile course; preserve the first island exactly and make no Player builds.

**Approved reference:** `Docs/Design/RadioArchipelago/Island02CoralOasis/Concept20261029/coral-oasis-overview.png` and the user's explicit instruction to proceed. This is a new level in the existing project/checkout, not a new project or worktree; the open Editor and existing uncommitted art are required.

**Architecture:** New owned Blender scene, FBX, route, height profile, prefab, definition, derived chart and Unity scene. Reuse native rhythm/input/camera/DI systems and approved Lumi-Lai only as the test recording. A per-definition finish exit heading preserves the first island's -53° behavior and gives the new level an independent clear finale. No old asset is overwritten. No music generation or new packages.

## Ownership and tasks

- [x] Preserve first island content/imports/scene/audio/catalog and project render/build settings hashes.
- [x] Geometry agent: detailed solid layered terracotta mesas/islets, turquoise oasis and falls, palms/gardens, two adobe radio houses, ring tower, independent Blender/FBX export and coordinate anchors.
- [x] Route agent: closed broad S-curves ~710m, smooth elevation4..17m, clear flat intro/finale, native route/camera tests.
- [x] Root: separate scene/definition/material authoring, derived full-size musical chart and terrain-safe Hold profile, warm daylight, flowing-water shader, own capture/open commands.
- [x] Validation agent: fresh independent imported triangle clearance report and opt-in full native review for new scene; retain old review defaults.
- [x] Import and inspect actual Unity previews, fix any rendering/route failures; run full native song with real pointer input, all228scorecontacts/34HoldPoints, final20sparks, hover/rotation/pause and camera checks.
- [x] Verify every protected hash, save evidence, leave CoralOasisPilot open in Edit Mode, report no builds and temporary music.

Proof: native full PASS `20261029-215326-373`; all56protectedfiles unchanged. Export winding bug reproduced and corrected in source (16,192 inverted terrain faces→0), temporary native probe removed. Evidence in `Docs/Design/RadioArchipelago/Island02CoralOasis/Implementation20261030/Review/`.

## Acceptance / review focus

Rich stylized detail should match the approved warm/cool palette and radio architecture. No continuous ground road or crude flat island undersides. Full safe lateral corridor and ample airborne headroom; no houses, palms or roofs in it. Long holds retain straight vertical profiles. Finale never intersects scenery or wraps prematurely. New serialized defaults retain existing first-island behavior without saving its assets. A conceptual image does not prove native rendering or physical playability: both must be checked on real imported geometry.
