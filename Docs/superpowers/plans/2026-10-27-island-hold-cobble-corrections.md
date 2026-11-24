# Island hold visuals and irregular cobbles

**Goal:** Preserve the original white Hold art, bend it continuously around the island, remove consumed geometry before later laps, hide invented exit discs, and replace uniform stones with varied natural cobbles. Unity Editor only; no builds or music/timing changes.

## Evidence and causes

- User images 184137/184145 show a straight blue ribbon after consumption and an unwanted forward exit disc.
- Stage created a visible procedural Hold surface plus discs for logical FollowEnd/HoldExit events.
- Native projection omitted original Hold/Hold_Line skins, mirrored unreadable static meshes rigidly, and gated effect-mesh lifetime only on original Renderer.enabled. This left straight geometry and stale strips on repeated laps.
- Native Hold_Light has 30 vertices and Hold_Line 61; bending their vertices alone is insufficient for a long sparse span.
- Cobbles V1 use fixed-size rows with narrow variation; the user requests broad variation of sizes, shapes and orientation.

## Work

- [x] Create readable pilot-only derivatives of native FX meshes; leave every original mesh unchanged.
- [x] Project exact native Hold body, center line, points and effects with UV-preserving subdivision and route deformation.
- [x] Share spring/visibility state between Stage and projected source art; expire consumed geometry in unwrapped chart space; hide id6/id8 exit discs while retaining gameplay triggers.
- [x] Export and visually inspect irregular mixed-size cobbles V2, preserving the road route and existing source art.
- [x] Validate the full recording and both route seams, no expired Hold strips, no invented endpoint discs, curved native materials/effects, and unchanged source music/timeline.

## Verification

Final native Editor run `20261027-161459-465` passed the complete recording and two route seams with zero errors, zero expired visible Hold ranges and zero exit discs. Native atlas geometry/UVs, line/dots and effects are retained; the body-only ivory shader removes the blue baked hue while preserving atlas value/bevel detail. Source native Mesh assets, timeline and MP3 hashes are unchanged.

An initial corrected run caught animated skin stretch exceeding the geometry-density check (0.759 m). Bind subdivision was tightened to 0.25 m with cap 128; the assertion was not relaxed. Final maximum observed animated Hold triangle chart-Z span was 0.68549 m against the existing 0.75 m acceptance bound.

Cobbles V2: 14,098 individual rounded irregular stones, six shape families, all twelve 15-degree orientation bins, broad size distribution, compact seams; 824,740 triangles in 28 batches. Combined island assets: 4,984,072 triangles / 329 renderers.

Actual Hold and Follow frames were visually reviewed; broad blue surrogate and endpoint discs are absent. Persistent evidence: `Docs/Design/RadioArchipelago/IslandPilotReview/HoldRoadFix`. No builds made.
