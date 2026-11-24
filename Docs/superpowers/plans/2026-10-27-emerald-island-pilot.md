# Emerald island playable pilot

> For agentic workers: execute incrementally with independent route/export work and final runtime review.

**Goal:** Test Always Remember Meow on the detailed first island, with winding tiles, lane changes, a turning camera and natural lighting inside Unity Editor.

**Architecture:** Preserve DSP timing, the existing chart-space movement/collision system and approved courier animation. An optional island presentation maps chart distance and lateral movement onto the canonical road. A separate pilot scene and chart copy keep existing levels unchanged. No new package or player build.

**Tech stack:** Unity 6000.5 / URP 17.5 / VContainer, Blender 5.2 background export, native FBX and URP materials.

## Constraints and decisions

- Source: V4/Output/01_emerald_ravine/island.blend, exact matching winding closed route.
- Song: AlwaysRememberMeowCat, 157.153 seconds. Preserve music and every rhythmic event, hold and flight.
- Six metre road and one metre shoulders; local left/right control follows the road frame.
- Source art is 23.6 million evaluated triangles: prepare an explicit runtime derivative, preserve original BLEND.
- Existing generated chart stays untouched. Pilot chart gets more pronounced but reachable lateral phrasing.
- No singleton, dependency installs, Git pushes or builds. Preserve unrelated dirty files.
- Installed Editor 6000.5.5f1 requires a patch upgrade from 6000.5.4f1; settings/package files backed up before opening.

## Work

- [x] Export first island, material metadata and exact road samples; verify coordinate mapping and geometry budget.
- [x] Implement allocation-free closed route sampler; verify seam, lane directions, long distances and malformed input.
- [x] Add optional scene-owned island presentation and minimal controller hooks. Original timing/collision remains authoritative.
- [x] Create the separate pilot chart, native lit materials, prefab and scene with warm sunlight, sky fill and shadows.
- [x] Compile in Editor, run route and chart preservation checks, then exercise full-song gameplay, pause, moving tiles, holds and the route seam.
- [x] Inspect actual gameplay frames, adjust camera/readability, leave the pilot ready for the user and report exact verification limits.

## Review focus

Camera and player must agree on left/right through turns; two road laps must not show overlapping future tiles; consumed holds must remain visible until ridden through; scene cleanup/retry must restore courier ownership; generated song timing and all original timelines must be unchanged.

## Evidence

- Unity Editor asset authoring passed, 301 renderers and 4,267,454 triangles before final redundant-face cleanup.
- Three imported coordinate anchors passed after a prefab wrapper correction of FBX handedness.
- Eight route math checks passed in the actual Editor, including seam continuity and zero managed allocations over 40,000 hot-path calls.
- Pilot mean absolute lane offset 0.517 m versus source 0.453 m; all 12 moving tiles retained. Audio, musical times, holds, glides and flights preserved by structural checks.
- Independent integration review found two issues: studio light layers and preview song persistence. Both fixed and re-reviewed.
- Final native Editor run `20261027-153929-392` passed the complete 157.153-second recording and finish sequence: 9,526 observed frames, zero errors, both route seams, all 12 moving tiles, holds, glides, big jumps and pause/resume. Exact user preferences/input restored.
- Maximum displayed actor/route disagreement, including spring contact offset: 0.0 m. Projected original effects observed, maximum 340 simultaneous particles; 2,559 source systems mapped with configuration pools.
- Later user steering implemented: 1.0 m tile top / 0.30 m thickness, press/rebound/shrink animation, held-support depression, native particle/trail/effect-mesh projection, real volumetric cobblestones and scene-owned automatic quality policy.
- Final island geometry: 328 renderer batches / 5,122,236 triangles including 13,390 individual 3D cobbles. Three coordinate anchors passed for both FBX imports.
- Fixed missing Forward+ cluster shader variant; actual frames now show direct sunlight and shadows. Road top normals independently measured up on 22,716 samples; no gamma adjustment was applied.
- Full native static/skinned FX readability paths verified by the final run; no original effect assets were edited.
- Source chart, MP3 and original scene hashes remain identical. Package manifest/lock remain unchanged; only required Editor patch version changed.
- Evidence and selected real GameView frames copied to `Docs/Design/RadioArchipelago/IslandPilotReview`.
- Mobile quality uses conservative memory selection and sustained frame-time downgrade; Economy removes Bloom and reduces particles/shadows/render scale/draw distance. Physical phone performance and thermal behavior were not tested. No builds made.
