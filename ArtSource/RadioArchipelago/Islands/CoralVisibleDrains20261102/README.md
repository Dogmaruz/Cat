# Coral: exposed continuous water outlets

Source-only revision of the two Coral lake/outlet/waterfall meshes. The previous lake unions were continuous but their falling bodies intersected the island cliffs. This revision moves the high outlet to the nearby north rim and extends the low outlet to the actual south rim. Each lake, shallow outlet, rounded lip and falling curtain remains one indexed connected mesh with one material. There is no overlaid entry sheet or hidden renderer.

Final model: `STAGING/coral_oasis_island.fbx`

SHA256: `37e99875187a9e0e67a6405a042dba3919e8b96377111fc54ec627eaa1ccb4d0`

`STAGING/handoff.json` provides exact node names, consumed pool nodes, bounds, metric UV endpoints and native validation metadata. `build_visible_drains.py` reproduces the source; `coral_visible_drains.blend` is the authoring file. The immutable input is the Coral revision with SHA256 `f64e9bd7b0e51ff365169112c89ab5d04cc54a1e341815abaf7573ea8c81170b`.

## Local changes

The high spring flows north to lip `(67, 3.6, 6.726)`; the low outlet flows south to `(44, -82, 0.456)` in Blender coordinates. Both have rounded transitions and falling bodies outside the rock. Only shallow drain corridors and the obsolete high south-mouth bank are reshaped. The historical pool footprint is retained except at the necessary outlet transition.

Only six non-water batches change: `RA_Coral_Terrain_+01_-01_01`, `RA_Coral_Terrain_+01_-03_01`, `RA_Coral_Terrain_+01_+00_01`, and the corresponding three `RA_Coral_Cliffs_...` nodes. Their topology, UVs and RGBA are exact; all vertices outside the documented sculpt scope retain their positions exactly. Re-exported custom normals within these six batches have bounded far-component deltas, at most `0.00461194`; those normals are not claimed byte-identical. All 253 other imported objects have exact render signatures, including normals.

No music, route, gameplay, scene, Unity asset, other island, material or shader changes are made here. `channel_decor_audit.json` records no near-ground decor vertices inside either new drain corridor.

## Verification

`audit_visible_drains.py` independently imports the old and new FBX files. `export_audit.json` and `audit-final-finite.log` record the passing final result. For each of 30 falling-body samples (five drop heights and three width positions per fall), at least one direct segment from a point 200 metres away, outside the complete Terrain/Cliffs world AABB, reaches the body without any rock intersection. No culling or visibility override is used.

All signed-winding ray results are also retained. One 281-degree ray from the high fall at 75% drop crosses orientation-inconsistent/overlapping distant rock, beginning 6.8 metres from the water, and has a nonzero signed count. This is explicitly a non-certifying diagnostic on the non-Boolean scenery mesh. The same sample has unobstructed exterior rays at 0, 45 and 270 degrees. The finite exterior-segment checks, rather than that invalid closed-solid assumption, certify exposed surfaces. No unrelated rock was sculpted to suppress this anomaly.

Water meshes retain the reviewed metric UV0/UV1 lake-mask contract, physical shared mouth edges, one component per fall, and alpha one upstream with only the final three metres fading. The independent audit finds no zero-area water triangles or invalid water normals. Native Unity appearance and gameplay verification belong to the parent rollout task.
