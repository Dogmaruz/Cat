# Amber Winds final independent review — 2026-11-02

## Current assessment

**Ready with explicit measurement limits.** No remaining important static code defect was identified. The capture-completeness and scene-open asset-lifecycle defects are fixed. Fresh native evidence supports the full playable fourth island, menu switching, preservation, fixed camera, clear terrain and visible raised side cascade. Final packet analysis confirms downstream motion at all three R3 spill angles and all three heights of the raised side curtain. Together with source/import continuity and viewed native imagery, this supports the core smooth-spill and visible full-height falling-water requirements.

Four additional small/edge measurement regions remain unproved and are not counted as passes. This is readiness for the implemented feature with those diagnostic limits, not a claim that every pixel or all requested ROIs were tracked. No sample is confirmed upstream. Phone performance remains unmeasured.
This assessment supersedes earlier provisional statuses for the absent scene/definition and d0b36 source. Final imported FBX is c5656b305295a74bffc93d0428b111a95e6768e0ef42b9cd956ec612ac88fc3a; shader is e5595cb7296dfb46056edf00727f290127ba817871a78869e7737291d9ea3337. Both installed-file hashes were independently rechecked during this review.

## Resolved findings

- **P2, LivingWaterReview.cs:579–586:** capture previously completed after1.3s even when Editor load prevented collecting all14 frames. It now waits for both duration and all14 frames, requires14 timestamps in the pass condition, deletes stale phase flow PNGs before capture, and checks a common unique runId in the aggregate result. Fresh run20261101-234211-5093157 contains14 distinct recorded times for each of all four phases, closing the original defect with native evidence.
- **AmberWindsIslandJobs.cs:70–77:** integration discovered that OpenScene could unload the unreferenced local definition. The saved definition is now reloaded and null-checked after scene opening and before stage.Configure. This narrow correction changes no source geometry, chart or camera settings. Subsequent native preparation and gameplay passed.

## Fresh evidence independently read

### Gameplay and menu

`DancingCatsClone/Library/RadioArchipelago/AmberWinds/PlayReview/play-review.json`, run20261101-234737-711, records passed=true, completedRecording=true, won=true and boundedReview=false for RadioTrio. It records346/346 score contacts,20 collected ground-finish rewards, zero logged errors, zero landing overlaps, zero actualGeometryClips, pause verification and exact profile/input restoration. Its seven required distributed terrain captures accompany a fresh independent terrain report. Fixed camera is true, FOV delta is0, and maximum camera distance error is approximately0.00000763m. This is the full native run, not a shortened or invincible substitute. Native hold95% fixture coverage is explicitly separate from held-state sampling; the report does not pretend to have captured native95% holds after early handoffs.

`PublicIslandMenu/live-menu-checks.json` and `menu-review-state.json` both pass: exactly four public library/Atlas entries, clear card layout, real forward/back/results callbacks, correct chart/definition/sky for all four islands, hidden catalog retention, profile/guide/courier restoration and clean scene state. Underlying catalog retains84 songs.

### World, terrain and fixed camera

`AmberWinds/island-checks.json`, `route-checks.json` and `terrain-clearance.json` pass. The final world has278 renderers,13 materials,3,140,774 triangles,97 moving supports,18 holds and four clouds. Cloud minimum gap25.0799m, minimum clearance0.7600m, volume ratio7.7002. Native terrain proof contains55,488 corridor and64,697 support samples with zero violations;33,915 structure rays also have zero violations. The route is774.9949m, with conditioned height range4..23.0937m; actual finale reserve and unchanged fixed camera envelope pass. Definition hashes agree between route, terrain and full-song reports.

### Final source and visible waterfall

Read `seam_shading_patch_report.json`, `fbx_shading_roundtrip.json` and `curtain_body_normals_fbx.json` in `ArtSource/RadioArchipelago/Islands/AmberWindsPilot`. The shading-only revision records unchanged positions/UV associations across all278 meshes relative to d0b36, with final c5656 hash. Fifteen sampled source/export joins have coincident positions/UVs/colors and normal dot approximately1, with only floating-point displacement residuals. The curtain body audit reports PASS and no reversed/zero corner normals. These establish source/import continuity, not perceived motion.

Read `WaterNative/side-cascade-visibility.json`: unchanged fixed-route cameras at155/170/185m show4811/8116/19785 visible target-water pixels respectively. Top/middle/base landmarks are inside the portrait and have occlusion-aware visible-pixel witnesses at each station. Independently viewed fresh `side-cascade-route-185.png` and `roll210-clock-early.png`: the raised side cascade is visibly beside the route, and the stream rolls over a curved bank. No open black seam is visible at the210 join; a remaining material-tone transition is visible. Root separately reviewed gameplay/cloud/finale images and accepted the residual tone as style after geometry/normal checks. This review does not relabel that style decision as a new user approval.

### Final real-clock directional evidence

Read the final `WaterNative/direction-aggregate.json` and `.md`, final standard direction reports, and packaged `FinalAcceptance/acceptance-summary.json`. These supersede the initial partial reports preserved as `*-direction-initial.json`.

`WaterNative/live-clock.json` passes and restores the original scene. All four phases share run20261101-234211-5093157, have14 actual timestamps and default runtime clock; saved Stream/Waterfall properties are UseFlowUV1, CurrentSpeed4.2, FlowSpeed.65 and previewTime-1. Frame hashes bind the analysis to the recorded native images. No shader, source or camera changes were made for the final analysis.

The tracker correction groups actual distinct local correlation peaks instead of treating the shoulder of one broad peak as a rival peak. The review inspected the corresponding code and known3px broad-packet regression fixture. The evidence records RED before/GREEN after, with positive/negative/static/blank/repeating fixtures retaining their expected results. Confidence thresholds remain unchanged: NCC.65, gain over stationary.03, separate-peak margin.035, travel.75px, mask coverage90%, at least three recognizable pairs from three starting frames, and75% directional agreement. This is a corrected measurement defect, not relaxed acceptance thresholds.

Final confirmed samples:

| Sample | Downstream / recognizable pairs | Median / expected px/s |
| --- | --- | --- |
| R3 roll35° |16/16|65.2/70.7|
| R3 roll48° |14/14|70.0/74.0|
| R3 roll65° |26/29|55.4/71.9|
| Side curtain upper |43/43|38.9/40.8|
| Side curtain middle |41/41|39.0/39.6|
| Side curtain lower |42/42|32.6/36.7|

All six have13 distinct starting frames. R3-65 retains three upstream pair matches among29;26 downstream matches meet the unchanged75% sample rule. These are not suppressed, and no whole sample is classified upstream.

Four regions remain explicitly inconclusive:

- Near-bank flat stream: original patch is outside the valid mask; the smaller same-center patch has positive votes but155.5px/s versus expected89.8, so it is not accepted as the intended carrier.
- Side lip35°: even5x5 coverage is88%, below the unchanged90% gate.
- Side lip48°: smaller patch has positive votes but only3.75px/s versus expected30.2; carrier identity is unproved.
- Side lip65°: smaller patch has no recognizable translations.

Smaller footprints were selected from static visibility geometry at unchanged centers/tangents, not moved to obtain favorable motion. The aggregate therefore correctly retains pass=null and allRequestedROIsConfirmed=false while setting r3Lip35_48_65Confirmed=true and fullCurtainUpperMiddleLowerConfirmed=true. Its full-height curtain result supersedes the earlier statement that middle/lower direction remained open. The remaining limits concern four diagnostic ROIs, not a demonstrated runtime or source defect. No new renders or tuning are required by this review merely to make every diagnostic pass.

`FinalAcceptance/acceptance-summary.json` records integrationPassed=true, the same final source/run identities,346/346 contacts, zero errors/geometry clips, input/profile restoration, native menu pass and clean Amber Edit Mode. It explicitly preserves the four unproved regions and unmeasured phone performance. The review's ready-with-limits assessment agrees with this bounded evidence, not with an all-pixels claim.
### Preservation

Read root's independent `Implementation20261102/root-preservation.json`:412 baseline files,404 byte-identical, eight authorized changes, zero unexpected changes. Authorized changes are the single-row custom catalog addition, four-entry serialized menu catalog, and six intended menu/play-review code files. All25 previous raw catalog rows remain verbatim, with exactly one RadioTrio addition. Earlier review independently verified the raw row equality and baseline before the serialized menu update. The current audit also verifies source/import final FBX identity, unchanged shader identity, and accepted audio b3a7249d3f28984bf190d6498a2cadee7335d3bf61f6c22679e69e911d8759f6. Old source geography/maps/music remain protected.

## Static review scope

Reviewed installed Amber authoring/route/terrain/song code, four-menu policy/catalog/jobs/live checks/presenter, Amber PlayReview routing and restoration, runtime cloud multiplier, CoralWater shader and native water helper. New content is independently owned; runtime stage/cloud/rooftop lifecycle is reused. Cloud multiplier defaults/clamps safely to1 for old content and opts Amber into1.6. No unbounded per-frame search, new singleton, physics water, new per-effect runtime lights or packages were found. Full-size supports and genuine audio timings remain enforced by existing authoring plus native checks. Legacy UseFlowUV0 and default fade remain compatible; authored flow uses one coordinate and geometric tangent basis throughout the bank roll.

## Limits / declined to judge

- Four small/edge numerical ROIs remain inconclusive as detailed above; they are excluded from the passing evidence. Core R3 spill and upper/middle/lower side-curtain direction are confirmed. No claim of exhaustive per-pixel motion or exact speed everywhere is made.
- Phone performance is unverified.3.14million scenery triangles and Editor offscreen timings do not establish target-device frame pacing or memory performance.
- Historical uncommitted backlog and rejected music are outside the requested fourth-island scope.
- Reviewer performed no Unity queue action, build, source/asset edit, commit, push or music generation. Existing reports were inspected; no native test was rerun by this reviewer. Only this requested review document was written.

