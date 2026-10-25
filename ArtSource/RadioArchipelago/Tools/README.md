# Radioarchipelago character authoring

The supplied Tiko, Lumen and Nori models and texture atlases define the character
appearance. The original ZIP archives and texture images remain unchanged.
The pipeline uses Blender 5.2 in an isolated background process; it never opens
or overwrites an interactive Blender scene. It does not build the Unity player.

## Current pipeline: rounded paws and repaired harness contact surfaces

`prepare_radio_rigs.py --stage rounded` rebuilds the current separated, rigid
paws from the good distal source surface. It repairs the exposed body, replaces
the harness loops and buckles, and keeps the existing accessory skeleton and
Blink. The torso, paws and harness are separate skinned objects; the paws have
100% shoulder weights, so a shoulder stroke cannot stretch the body texture.

```powershell
$radioBlender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/rebuild_smooth_couriers.py -- --character Nori --torso --render
# After inspecting the candidate from both sides and below, publish its package:
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/prepare_radio_rigs.py -- --stage rounded --character Nori
```

The first command writes only ignored review candidates in
`DancingCatsClone/Library/RadioRigging/SmoothCourierReview`. The second writes the
six-file package in `Processed/Nori` after the portable FBX checks pass. Neither
command imports assets into Unity or creates a player build. Use the existing
Unity courier importer after reviewing and publishing a package. Omit
`--character` from `prepare_radio_rigs.py` to process all three characters.

The rebuild reads immutable source revisions from Git: `87bc426` for the
separated rig, weights and accessories, and `d8019f2a8b00f1c6481c1a086056ebc543c6f1e9`
for the original body surface. It verifies the cached Git blobs. Later manual
changes in `Processed` are not merged into these revision inputs. Keep those
commits available when making a shallow checkout.

Reconstruction also requires the supplied local archives below, under
`Docs/Design/RadioArchipelago/ModelReferences/`. These artist inputs are excluded
from Git; restore them separately on a clean checkout. The original three ZIPs
provide the source model and maps; `Tiko2.zip` and `Nori2.zip` supply the part
labels when the ignored inspection cache is absent. Running the already imported
Unity game does not require these archives.

| Relative archive | Bytes | SHA-256 |
|---|---:|---|
| `Lumen/Lumen.zip` | 106926955 | `02176db05a89cb439563a92655db66e0d5022ac6d341b002d5410d5de340dddd` |
| `Nori/Nori.zip` | 111496601 | `7daf1330989f6bd7826f99533a496f53fac6e6010f1353bf2af141de59802f68` |
| `Nori/Nori2.zip` | 30904910 | `dbd06e454ff9e66dcd2ff6d4fa11eed220b5fbc729945edbb445b0cb86fb4517` |
| `Tiko/Tiko.zip` | 111866610 | `58cddf894dd4e209ed8722baf4fd6a6e0d676f1fd506368216bbdf918b65ec6d` |
| `Tiko/Tiko2.zip` | 34511525 | `dfee34addb686935ca5330bc8539a6e3108c3972c40b3ecc5d8e8c53a322661c` |

- `rebuild_smooth_couriers.py`: mirrored distal paws, source preservation,
  review renders, strict FBX export and package publication.
- `repair_smooth_torso_harness.py`: local tissue reconstruction, protected source
  clothing/case regions and native atlas charts at the exposed contact surfaces;
  Lumen's closed body/back and separate Pack include a rounded leather contact
  lid, with the original radio exterior preserved.
- `repair_courier_rear_contact.py`: the local Nori contact-rim correction before
  harness fitting, with the main case, scarf, head and feet preserved.
- `repair_courier_harness.py`: closed body-weighted straps and buckles.
- `repair_courier_contact_uv.py`: local source-material cleanup, preserving
  geometry, skin weights, Blink and the original texture files.
- `compose_surface_review.py`: side-by-side sheets from the actual Unity
  before/after captures. These images complement the numeric checks.

Review evidence and remaining qualifications belong in
[`RigReview/SmoothCouriers`](../../../Docs/Design/RadioArchipelago/RigReview/SmoothCouriers/README.md).
Source/FBX equivalence alone does not establish that the armpits look smooth.
Check both sides, the underside, raised shoulders and actual Unity lighting.

## Historical connected-body pipeline and commands

Run from the repository root in PowerShell. Blender is installed at
`C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`.

```powershell
$radioBlender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/prepare_radio_rigs.py -- --stage inspect --character Tiko
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/prepare_radio_rigs.py -- --stage rig --character Tiko
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/validate_authoring_rigs.py
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/validate_continuous_source.py -- --character Tiko
```

Those commands prepare and check the original connected body/arm revision.
After its reviewed export exists in `Processed/<Name>/`, run the accessory stage:

```powershell
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/prepare_radio_rigs.py -- --stage accessories --character Tiko
# Optional before/after/yaw proof without changing the exported payload:
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/prepare_radio_accessories.py -- --review-only --character Tiko
```

The stage uses a frozen `AccessoryReview/<Name>/Before` backup for retries.
On a fresh checkout with an already prepared accessory manifest it verifies the
existing rig instead of adding another joint. The fixed backup is a revision
input, not a tool for merging later manual edits to `Processed`.

The character argument can be `Tiko`, `Lumen` or `Nori`; omitting it processes all
three. Inspection renders front, back, left and right before authoring. `analyze`
writes a source face/colour CSV for anatomical mask work.

- `inspect_supplied_models.py`: read-only archive/FBX inventory, ordinary Python.
- `prepare_radio_rigs.py`: safe extraction, source import, targeted surface
  refinement, armature, skin weights, localized Blink
  shape key, review poses, editable Blender file and authoring manifest.
- `radio_root_caps.py`: retained support for earlier separated-shell prototypes;
  the first connected body/arm stage creates no arm cuts or caps.
- `validate_authoring_rigs.py`: read-only checks of saved rigs; finite geometry,
  duplicate faces, normalized skin weights and at most four influences. For the
  first connected body/arm profile it also rejects cap attributes; legacy prototypes have
  separate closed-hand/pinned-root checks.
- `validate_continuous_source.py`: connected source mesh, no added cuts,
  rest-surface distance and UV interpolation comparison against the original ZIP
  for that first revision; not a full-surface equivalence gate for accessory cuts.
- `prepare_radio_accessories.py`: local mechanical cuts with separate closed caps,
  Lumen's gold brace removal, four-bone scarf weights, yaw edge-strain checks,
  strict portable export and optional ordinary Blender review renders.
- `export_prepared_rigs.py`: reviewed-rig export and strict FBX roundtrip gates.
  Run its `--help` through Blender for its current CLI.

Working extraction, scratch reports and render reviews are in the ignored
`DancingCatsClone/Library/RadioRigging/`. Original archives are under
`Docs/Design/RadioArchipelago/ModelReferences/<Name>/<Name>.zip`.
Final artist/runtime packages are under
`ArtSource/RadioArchipelago/Processed/<Name>/`:

| File | Purpose |
|---|---|
| `<Name>_Rigged.blend` | Editable rest-pose meshes, skeleton, materials and Blink |
| `Model.fbx` | Unity skin, bones, UVs, corner normals and Blink |
| `BaseColor.png`, `Normal.png` | Unmodified original 4096² source images |
| `MetallicSmoothness.png` | Derived URP image: R = metallic, A = 1 − roughness |
| `manifest.json` | Bone/mesh roles, source integrity and roundtrip measurements |

Keep the six files together when moving the package; Blender materials reference
the adjacent image files. No textures are embedded in the runtime FBX.

## Historical connected-body rig contract

The earlier design kept body and arms connected, with short shoulder bones
and small 15–30° waves; 35° is a stress pose. `ANATOMY[character].continuous_skin`
selects this profile. Earlier split-shell files in Library are fallbacks and must
not be mistaken for the selected design. No arm/body cuts, cap geometry or
shoulder sockets are created by the continuous-source path. The separate
accessory stage intentionally cuts and caps the antenna mounting seam; it does
not cut a hand from the torso. Lumen's lower backpack-to-sail brace is removed.

Authoring coordinates are Z up, front −Y, height 2.6, root grounded at the feet.
Runtime FBX uses forward −Z/up Y. The import step must not apply a second axis
conversion. The connected source mesh has `mesh_role = Character`. The earlier
split-shell prototypes used anatomical mesh roles; bone names remain common.

The common deform bones are `root`, `body`, `head`, `arm.L/R`, `foot.L/R` and
`antenna.yaw → antenna.01...`. The yaw bone is parented to `body` at the true
mounting joint. Tiko has `crest.01/02`; Tiko and Nori have two independent
four-bone scarf chains (`scarf.L/R.01...04`).
Lumen has no invented crest or scarf. Its metal antenna/sail moves as an assembly
to keep the supplied frame and fabric intact, pivoting above the head.

That revision exported Tiko 12,203 vertices / 24,454 triangles / 23 bones;
Lumen 9,431 / 18,878 / 12; Nori 7,344 / 14,704 / 21. Each remains one skinned mesh
object, with body and rotating assembly separated topologically at sealed caps.
Revision v2 recesses each cap 0.004 source units and closes the resulting thin
sidewalls. Exterior mounting rings, antenna silhouette and all UVs outside the
joint are unchanged; newly added joint vertices have zero Blink displacement.
The current manifest's `accessory_qa` checks yaw 0/90/180/270°, boundary counts,
finite geometry and Blink. `verification` checks the edited rig against an
independent FBX import. Neither substitutes for Unity runtime validation.

The actual source hands remain joined to the body. Short bones start at the upper
shoulder under the scarf and follow the diagonal of the original flipper, as in
the user's marked reference. Arm weights form a local harmonic field with the
harness held on the body and the outer hand following the arm. Full arm anchors
are excluded from a 14 cm geodesic buffer around fixed hardware and an 8 cm buffer
around other fixed-zero field boundaries; adjacent fixed 0/1 weights can stretch
even a 6 mm edge severely during a small wave. The armature modifier uses linear
skinning to match Unity rather than Blender's preserve-volume skinning.

The source eyes are painted in the texture atlas. `Character` has one local `Blink`
shape key which compresses the actual eye UV surface and projects it onto the
source face; it does not squash the head or add replacement eyes. Runtime
procedural motion drives bones and Blink; source action clips are not required.

## Review and limitations

The body/arm revision reviews contain rest, closed Blink, 15/25/35° waves from front and
three-quarter, plus antenna/crest/scarf flutter. Older 105° files belong to
rejected split-shell prototypes. Review source and final renders before changing
mask coordinates. `ANATOMY` landmarks are in the normalized source frame. Legacy
`arm_planes` use the final foot-centered frame and are ignored for continuous skin.
The accessory review adds its own frozen before-image, yaw views and mechanical
strain report. Historical connected-source surface measurements remain evidence
for the earlier body/arm revision, not for the newly added seam caps.

There is no global decimation or wholesale surface replacement. Local refinement
supports eyes and moving joints; original exterior UVs/corner normals are
retained or interpolated. Tiko's four isolated source triangle flecks are removed.
Blender's evaluated morph normals produce vertical shading around Lumen's
fully closed eyes, including with the normal map disabled. The Unity importer
uses the authored rest normals for Blink (`importBlendShapeNormals = None`),
removing those shading creases on the same vertices. The imported rig check
verifies that eye vertices deform while morph normal deltas remain zero.
The soft antenna/scarf skin is an artistic rig, not a cloth physics simulation.

Validation of Blender geometry and FBX equivalence does not replace Unity pose,
camera, renderer-bound and gameplay review. Those checks run separately in the
Editor. Music and level data are outside this pipeline.
