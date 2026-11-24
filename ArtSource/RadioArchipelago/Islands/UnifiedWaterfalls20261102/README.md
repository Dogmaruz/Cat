# Unified waterfall source revision — 2 November 2026

The user approved the continuous Amber waterfall and requested the same treatment
where waterfalls exist on the other public islands. This package contains source
and exports only. It does not write Unity Assets, scenes, profiles, Editor queues,
music, charts, routes, settings, or the accepted Amber scenery.

## Inventory and scope

- Emerald: three low river-water batches, **no waterfall**. No new Emerald model.
- Coral Oasis: two coarse falls. The old low fall was offset from its outlet.
- Moon Resonance: two falls. Their old V coordinates were row numbers, not metres.
- All four old static foam ribbon batches remain unchanged; their existing
  clipped legacy material keeps them invisible. The flowing shader supplies foam.

## Export locations and pinned hashes

| Island | Export | SHA256 |
|---|---|---|
| Coral | `STAGING/Coral/coral_oasis_island.fbx` | `7b5617164bdf969d41221d9a83c5ff6c288dc91795b7c41353079ae654344ce2` |
| Moon | `STAGING/Moon/moon_resonance_island.fbx` | `fba3247c094803b1a51b519e528afc3b3b1b139b40bf728320b4ef1941cd9c5f` |

Input sources are the immutable latest rooftop revision in
`../RooftopMechanisms20261101/STAGING/{Coral,Moon}/`. Their input FBX hashes are
Coral `6fb98555cb33402282d55f41eeb7e064c4d812d72083a999d85d7c478c9da7b2`,
Moon `c2407e489e3ec0e85322e66897e8379f3828b143d09111781df8944fc38912be`.
The old sources and rooftop manifests remain unchanged.

## Geometry and shader contract

Each of the four retained primary Waterfall node names now contains one connected
32-column grid from an upstream horizontal reach, through a real quarter-circle
spill, to a gently rounded falling body. Each uses its old single
`RA_Coral_Waterfall` or `RA_Moon_Waterfall` material identity. The root integration
assigns the accepted Amber flow shader and an island-specific explicit tint.

UV0.U is across-width 0..1. UV0.V is cumulative downstream **metres**. Normals
follow the actual curve. COLOR.rgb is uniform; COLOR.a fades only the final 3m
and blends the first .6m into the unchanged upstream pool. No material/Fall branch
splits the lip from its curtain. The surface is .006m above the original flat
water in the tiny upstream overlap, preventing coplanar flicker.

Coral low has a matched-width reach following the real outlet centre `(44,y)`.
The old last outlet polygons beyond y=-68 are replaced by the same continuous
ribbon. Both Moon outlet tails replace their final ~4m without rebuilding their
large pools. The removed Coral `RA_Coral_Water_+01_-03_01` node held only six tail
faces. All affected node names and top/lip/body/base coordinates are in each
`STAGING/<island>/handoff.json`.

All four falls end far below the floating islands. **They have no actual receiving
basin.** Do not attach impact spray onto an invented floating floor at their bases.
Lip droplets or low-opacity airborne mist may be separately authored if wanted.

## Independent validation

Run `audit_export.py` in background Blender. It imports both old and new FBX files,
then checks actual exported payloads. The final `export_audits.json` passes:

- Coral: all 263 untouched imported objects retain exact full render signatures.
- Moon: all 273 untouched imported objects retain exact full render signatures.
- Source checks additionally cover 270/273 untouched source objects, including
  properties, parent identities and transforms.
- All four grids have one connected component and one material, no reversed or
  nonunit normals, manifold internal edges, and strictly downward +V derivatives
  throughout their vertical bodies.
- Rooftop manifests retain exact bytes.
- Retained pool faces outside the clipped tails keep exact positions, UVs and
  RGBA: Coral412faces, Moon207+1915faces. Copying their custom normals through a
  rebuilt target batch recompresses normals slightly. Maximum per-component
  deltas are Coral0.0004025, Moon0.00000721/0.0003401, explicitly bounded below
  0.001. This is not described as byte-identical pool shading data.

Geometry validation does not certify visual quality. The root must inspect the
animated Unity output with the normal gameplay camera, including lip continuity,
downstream motion and where the new reach joins the existing pool. Phone
performance remains unmeasured. No Player builds are part of this package.

## Reproduce

From the repository root, run Blender5.2 in background with
`--python ArtSource/RadioArchipelago/Islands/UnifiedWaterfalls20261102/build_continuous_waterfalls.py`.
Optional arguments after `--` are `Coral` and/or `Moon`. Run `audit_export.py`
afterward. Both scripts write only this revision directory.
