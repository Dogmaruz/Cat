# One surface: lake → outlet → waterfall

This revision replaces the rejected raised/overlaid water reaches. It supersedes
`UnifiedWaterfalls20261102` for lake/fall integration. The previous revision and
the approved Amber source remain immutable inputs and provenance.

## Final exports

| Island | Model, under `STAGING/<Island>/` | SHA256 |
|---|---|---|
| Coral | `coral_oasis_island.fbx` | `f64e9bd7b0e51ff365169112c89ab5d04cc54a1e341815abaf7573ea8c81170b` |
| Moon | `moon_resonance_island.fbx` | `d507cdcacc4a6288743f03513cda0b1de0496b6ffa9d7781465ced6d414a4331` |
| Amber | `amber_winds_island.fbx` | `f49534c8e60ccc133e674860c5ef91042478f3327f1956c5ce79a791e075ca43` |

Emerald has no waterfalls and is untouched.

## What changed

The lake polygons and the connecting horizontal reach are combined into a single
2D constrained triangulation of their **union**, without overlapping faces. Its
mouth is welded to an existing original ribbon row: the same vertices belong to
the lake and to the continuing channel/fall. Each complete lake/outlet/fall uses
one mesh component and one material.

All relevant old pool/channel batches are consumed. Coral's already-absent
historical tail name is explicitly included for installers migrating straight
from the original rooftop model. Exact consumed-node lists are in each handoff.
Amber's receiving basin and the prefab-authored spray are **not** consumed.

Each lake's flat surface uses its existing connected ribbon's actual level:
Coral/Moon rise only6mm, Amber lakes35mm, removing the old hydraulic offset
*between* surfaces. This is a single height function now, not an overlay or a
clearance workaround. No `_UpstreamClearance` option is required.

## Shader/UV contract

- UV0.U is across the flow; UV0.V is downstream metres. Lake U can be outside
  0..1 and its V can be negative. The lake flow map joins the original ribbon UV
  at shared vertices with no phase reset.
- UV1 (`LakeRegion`).x is0 on the broad lake and increases smoothly through the
  planar outlet. It is exactly1 from the metadata's `metric_start_v` row onward.
- Boundary UV lookups use an actual patch triangle, including a nearest-boundary
  fallback within100micrometres when a ray misses a floating-point edge. A
  clamped nearest-centreline V never grants a lake vertex full mask1. The mouth
  mask is assigned only by actual distance to the authored mouth polyline.
- UV1.y is1 on these unified surfaces and reserved for future use.
- COLOR.a is1 on the entire lake/outlet. Only the original downstream final3m
  fade remains. There is no old .6m upstream alpha fade.
- The opt-in unified-lake shader must disable ribbon-U edge clipping on lake
  regions. The existing accepted body branch should be preserved for mask1.
- Each metric row has exactly33 original columns U=j/32. An importer can split
  vertices for normals; validators must average unique U columns, not duplicated
  buffer vertices. `metric_start_unity` and `unity_bottom` use this33-column mean.

The merged mouth can be earlier than the top of the roll; its purpose is to lie
unambiguously beyond the original pool. All later stream/curved/falling body
positions, UV0 and alpha are retained from the input. Amber's long winding creek
and approved waterfall bodies therefore retain their established shape.

## Validation

Each `STAGING/<Island>/export_audit.json` independently reimports both input and
new FBX models. Results:

- Coral:259untouched imported objects retain exact full render signatures.
- Moon:269untouched imported objects retain exact full render signatures.
- Amber:263untouched imported objects retain exact full render signatures.
- Seven complete lake/fall meshes: one indexed connected component and one
  material each, shared mouth vertices, no nonmanifold internal edge, broad
  negative-V lake regions, full-alpha lake/outlet, downward vertical carrier.
- Coral/Moon body position differences after export ≤0.000001m; UV0/alpha exact.
- Amber approved body positions, UV0 and alpha remain **exact** after reimport.
  Copied custom normals recompress by at most0.0004721component; this limit is
  explicit and is not called byte-identical shading data.
- The FBX regression checks every first-row full-mask vertex against interpolation
  between the33original U columns. All7surfaces pass: no off-mouth extras;
  maximum position error0.95micrometres. This catches the earlier off-mouth
  lake vertex that incorrectly inherited the clamped final V and full body mask.
- `exact_zero_faces.json` in each island folder proves zero exact-zero-area
  triangles across all7surfaces. Moon upper originally contained two exact-zero
  lake triangles sharing one duplicated position with identical UV/mask. These
  two faces and only that exact duplicate were removed in source, producing
  21,620triangles/11,202vertices, matching Unity's native import. Nonzero small
  triangles and all approved body faces are retained; no epsilon merge is used.
- All non-water scenery, original source files and manifests remain unchanged.

Float32 coordinates along an oblique straight mouth can produce micrometre-area
CDT slivers. The builder removes only triangles formed entirely from the known
mouth-row points with negligible geometric area; it never drops real body faces
or moves the approved body. Intermediate failed diagnostic logs are retained,
and only the three pinned exports above are final.

These tests establish topology and payload preservation, not artistic approval
or mobile performance. The root integration must inspect native animated Unity
views of lakes, outlets, rolls and falling bodies before acceptance. No Player
build, package install, paid generation, Editor queue or Unity Asset writes were
performed by this authoring package.

## Tools

`build_unified_lakes.py` runs in background Blender5.2. Optional arguments after
`--` choose Coral, Moon or Amber. It writes only this revision folder.
`audit_unified_lakes.py` independently imports the old/new FBXs and records the
checks above. `audit_amber_overlap.py` records the actual old Amber lake/stream
overlap witness, including the receiving basin excluded from this merge.
