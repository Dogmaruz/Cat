# Continuous waterfall source rework — 2 November 2026

This source revision addresses the visible waterfall join reported by the user.
It is separate from the preserved original `c5656b…` source. It does not modify
Unity Assets, scenes, music, maps, routes, the other islands, or Editor state.

## Geometry contract

- Three complete ribbons include the upstream stream, curved spill and falling
  curtain. A join is shared grid vertices, not separate overlapping objects.
- One `RA_Amber_Waterfall` material per ribbon. Root integration supplies the
  independent Amber water shader; there is no material-dependent Fall switch.
- Existing primary curtain names remain, now covering the entire flow.
- Old Stream and secondary Layer1/Layer2 sheets are removed. This avoids their
  transparent stacking, narrower-width bands and different shading branches.
- Uniform RGB; COLOR.a fades the final 3 downstream metres to zero.
- UV0.U is width 0..1, UV0.V is cumulative downstream metres. The true geometric
  smooth normal follows the spill continuously; no horizontal normal override.
- Upper canyon R3 and raised R1.5 spills retain their original centreline arcs.
  The lower curtain's previously square lip receives a physical R1.5 arc.
- Longitudinal spacing is at most 0.5m; 32 columns across. An open sheet's outer
  rim is expected, with all internal edges adjacent to exactly two faces.

## Export

`STAGING/amber_winds_island.fbx`:

`18cfb754ba06d63a01a95c069ff5d7dbc5142e7415b42c6f19bf48187e45305f`

116001564 bytes, 269 mesh nodes. The original scene had 278 meshes: twelve water
meshes have been replaced by three complete water ribbons.

The exporter converts RGB to linear during FBX output. Authored
`(.15,.55,.57)` imports as approximately `(.01938,.26225,.28315)`.
COLOR.a remains a linear 0..1 fade. Explicit shader tint plus COLOR.a is advised.

## Verification

`continuous_sheet_handoff.json` records the frozen source hashes, semantic
signatures, sheet dimensions, UV ranges and actual Unity anchors.

`export_audit.json` compares independently imported old and new FBX assets.
All 270 non-target objects match exactly, including matrices, positions,
topology, normals, colours, UVs and material slots. All five gameplay sidecars
are byte-identical. All three sheets have zero reversed/nonunit normals and
minimum face/corner normal agreement >= .9971. Actual world-space derivatives
of curtain position along +V point down throughout the vertical sections.

These geometric tests do not establish visual quality. Root integration must
review actual animated gameplay-camera output before declaring this accepted.

## Reproduce

Run `build_continuous_sheets.py` with Blender 5.2 in background, from repository
root. It expects the original frozen c565 model and original matching blend in
the parent folder, verifies their hashes, and writes only this revision folder.
Then run `audit_export.py`, which independently imports both FBX exports.
