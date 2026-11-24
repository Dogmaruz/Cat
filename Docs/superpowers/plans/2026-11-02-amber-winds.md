# Amber Winds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement task-by-task. Root coordinates disjoint ownership and the Unity Editor queue.

**Goal:** Add a fourth playable autumn island, Amber Winds, with a user-approved full original song, winding elevated musical route, rotating windmills, large clouds, visibly flowing waterfalls and varied dressed shorelines.

**Current status:** Implemented and validated in the live Unity Editor. Accepted Radio Trio is bound to the fourth island and menu. Full native run20261101-234737-711 passed346/346 contacts,20finishsparks, fixed camera, zero errors/geometry clips. Smooth spills and the new raised side cascade are visible; actual packet motion confirms all three principal R3 angles and the side curtain upper/middle/lower. Four additional small/edge ROIs remain inconclusive and excluded from passing evidence. Final independent review: ready with explicit measurement limits. No Player builds or phone performance measurement. Two full songs cost16credits total; fresh verifiedbalance1894.

**Architecture:** Reuse the existing route/chart compiler, scene-owned stage presentation, rooftop rigid-node clock, pooled living nature and shared water shader. New scenery/route/chart/definition/scene remain separate from the three existing islands. Shared water edits are limited to making flow perceptible; old geography, maps and music remain unchanged.

**Tech Stack:** Unity6000.5.5f1, URP17.5, existing DI and offline runtime; deterministic Blender/Python authoring; existing native Editor queue; two full Runway music generations (first rejected, second accepted).

**Spec:** `Docs/Design/RadioArchipelago/Island04AmberWinds/Concept20261101/concept.json`, approved by the user on2026-11-02, plus the current request for autumn colors, rotating windmill, large billowy clouds, visible flowing water and varied stone/reed/shrub shores.

## Global Constraints

- Preserve yellow/orange/red autumn foliage with restrained green, turquoise roofs and copper details.
- No continuous solid road. Broad smooth left/right arcs, rises/descents, open ravines, small islets and distinct large cloud crossings.
- Keep long holds vertically straight and the full lateral steering/camera envelope clear. Do not shrink ordinary supports to fix overlap.
- Windmill sails and selected rooftop heads rotate about authored pivots using the existing stage-owned clock; bases stay fixed, pause and cleanup remain correct.
- Real shoreline stones vary in shape, scale and rotation. Reeds/shrubs and a few decorative shore details stay outside the flight corridor.
- No closed/low tunnels, heavy global postprocessing, new lights per effect, physics water simulation, new singleton Instances, packages, builds, commits or pushes.
- Generate a full song once, around90s, not a demo; report actual Runway expenditure and balance. Present audio before game integration according to the user's persistent preference.
- Root is sole Editor queue coordinator. Agents must explicitly hand off the queue; no concurrent model imports/native checks.

## Review Focus

- Cloud volume and separation remain obvious from the actual gameplay camera, including island transitions.
- Flow remains visible during a brief moving-camera glance, not only measurable between nearly identical frames. The user specifically requested full top-to-bottom falling motion and a smooth roll off the real rock bank, without a squared suspended water shelf.
- New raised side cascade must be visible ahead and beside the route over several seconds from the unchanged close camera; headwall/source above the route, basin below. Place scenery relative to the route where possible, preserving all existing music clocks and the three old worlds.
- Shores cover raw straight water edges with varied geometry without blocking tiles or jumps.
- New windmill sail sweeps never intersect buildings, route or camera; imported pivots and material references are valid.
- Existing three islands remain intact, including their recently improved cottage, crystal clusters, water and rooftop mechanisms.

## Task 1: Source world and route

**Files:** Create `ArtSource/RadioArchipelago/Islands/AmberWindsPilot/`; export only to `DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/AmberWinds/` when frozen.

**Interfaces:** `amber_winds_island.fbx`, `amber_winds_route.json`, `play_elevation.json`, `ground_elevation.json`, `RooftopMotion.json` in the same established coordinate schemas as MoonResonance. Rigid sail nodes named `RA_Rooftop_...`; scene-owned cloud ranges supplied to integration.

- [x] Snapshot the three existing islands and capture the accepted concept as reference.
- [x] Author the irregular autumn archipelago, broad inner S-route, safe flat hold grades, windmills and nearby islets.
- [x] Dress streams/pools with varied solid stones, plants and restrained shore details; model waterfall volume/edges for visible flow.
- [x] Export manifests, route/height contracts and source proof; inspect source views and actual geometric corridor before handing off. Frozen source SHA c5656b305295a74bffc93d0428b111a95e6768e0ef42b9cd956ec612ac88fc3a; source checks pass. Native checks remain separate.

## Task 2: Song, chart and fourth scene

**Files:** New `AmberWindsIslandJobs.cs`, `RadioTrioChartAuthoring.cs` and focused native world checks under `Assets/RadioArchipelago/Editor/`; content `Content/Islands/AmberWinds/`, scene `Scenes/AmberWindsPilot.unity`, new accepted audio/base chart; explicit fourth menu catalog entry. The previously verified Library candidate is now approved for publication.

**Interfaces:** Consume frozen Task1 exports and the actual generated recording. Reuse existing full-song musical chart/compiler and terrain conditioning; the scene and menu must select the fourth definition and the same song. Existing three songs and records stay byte-identical; source catalog update adds one record only.

- [x] Prepare integration against existing schemas without writing fake charts or binding an unreviewed song.
- [x] Present generated full MP3 and actual cost/balance. Respect the preview-before-integration preference. Radio Trio was accepted by the user on2November2026.
- [x] Analyze the actual recording, generate full-size supports/holds/rides/big jumps and condition them against the real route/terrain.
- [x] Create distinct prefab/materials/definition/scene, connect rotating sails, large separated clouds, finish glade and fourth menu entry.
- [x] Run native geometry, metadata, reference and menu-switch validation.

## Task 3: Perceptible living water

**Files:** Existing `Art/CoralWater.shader` and only necessary explicit water properties; current three FBXs/scenes/maps/music stay untouched. New Amber materials are configured explicitly by Task2.

**Interfaces:** Keep current material/property compatibility and `_WaterPreviewTime=-1` default; communicate flow and waterfall rendering requirements to Task1/2. Do not mask stationary water with a test-only clock.

- [x] Inspect the user-observed weak movement from actual gameplay-scale views.
- [x] Strengthen readable directional advection, waterfall streak/spray movement and shore foam while retaining mobile bounds.
- [x] Verify actual Play Mode time with side-by-side temporal captures, including current islands and the new source world when ready. Old3 route-camera/live-clock checks PASS; new Amber UV/flow check pending import.

## Task 4: Joint acceptance

- [x] Compile in the live Editor; inspect missing references/shader errors.
- [x] Check actual imported triangles under supports and through the swept camera/steering corridor; inspect clouds, shore banks, sail motion and water from gameplay views.
- [x] Run one full native fourth-level review with real input/clock, collectible finish and preserved preferences; do not substitute an invincible or shortened chart.
- [x] Verify existing scene/map/music/scenery preservation and the minimal intentional shared-water/catalog changes.
- [x] Leave the fourth scene available in a clean Editor state; report artifacts, Runway credits and validation limits. No builds.

## Final evidence and limits

`Docs/Design/RadioArchipelago/Island04AmberWinds/Implementation20261102/FinalAcceptance/` contains native gameplay, menu, terrain, visibility, temporal/directional reports and images; `root-preservation.json` independently verifies404/412byte-identical plus8preciselyauthorized differences and0unexpectedchanges after the full native run. All25 previous custom song rows remain verbatim. `FINAL_REVIEW.md` independently records resolved helper/frame-count and scene-load findings, current hashes, confirmed motion and remaining diagnostic limits.

Final source/import FBX c5656b305295a74bffc93d0428b111a95e6768e0ef42b9cd956ec612ac88fc3a; shader e5595cb7296dfb46056edf00727f290127ba817871a78869e7737291d9ea3337. Root fresh `inspect` returned AmberWindsPilot, Edit Mode, no compilation/pause/dirtyscene/pendingqueue. No builds, commits or pushes.
