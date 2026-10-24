# Radioarchipelago character source assets

The supplied Tiko, Lumen and Nori models are prepared in **Blender 5.2.1 LTS**.
The original ZIP archives remain unchanged. Rig authoring and exports run in
isolated background Blender processes; they do not open or replace a user's
interactive Blender scene and do not build a Unity player.

## Current revision: separate rigid paws, 24 October 2026

The latest user request replaces the earlier connected-shoulder design. Each
original paw must be a separate imported mesh, with its closed root buried
inside the body at the real shoulder. Every paw vertex is weighted **100% to
its own `arm.L` or `arm.R` bone**; body vertices have no arm influence. There
must be no shared triangle strip or soft skinning transition between paw and
body. The supplied exterior shape and painted UVs outside repaired contacts are retained; local hidden
caps are new geometry. These are source-model parts, not primitive substitutes.

The imported roles are `Character`, `Arm.L` and `Arm.R`. All three final exports
passed source/FBX roundtrip and actual Unity rigid-paw, rig, accessory, motion,
menu and full 67-second gameplay checks. Current counts, source commands,
post-export topology and exact manifests are recorded in
[source-summary.md](../../Docs/Design/RadioArchipelago/RigReview/Paws/source-summary.md).

See [PAW_MOTION.md](../../Docs/Design/RadioArchipelago/PAW_MOTION.md) for the
measured reference behavior and its independent implementation: simultaneous
mirrored shoulder strokes at 4 Hz, eased endpoints, a long-flight target range
of −10…60°, and a smaller short-jump range. Small idle/greeting gestures remain
separate states. Free accessories now respond more visibly to turns and actual
root velocity/acceleration; each scarf branch shares one bend budget up to 68°.

## Layout

- `Incoming/{Tiko,Lumen,Nori}/`: reserved model handoff folders.
- `../../Docs/Design/RadioArchipelago/ModelReferences/{Name}/{Name}.zip`:
  the actual supplied archives for this handoff. They are local source files;
  each exceeds 100 MB and is excluded from the Git handoff.
- `Tools/prepare_radio_rigs.py`: source extraction, inspection, the earlier
  connected-mesh base rig, localized feature weights, Blink and review renders.
- `Tools/export_prepared_rigs.py`: portable artist project and verified Unity FBX
  payload export from an accepted authoring review.
- `Tools/prepare_radio_accessories.py`: local sealed antenna joints, Lumen's
  lower brace removal and four-bone scarf tails on the reviewed body/arm rigs.
- `../../DancingCatsClone/Library/RadioRigging/Source/{Name}/`: extracted source
  FBX and atlases. `Library` is ignored by Git.
- `../../DancingCatsClone/Library/RadioRigging/Review/{Name}/`: intermediate
  `rig-review.blend`, `rig-manifest.json` and pose renders. These are review
  artifacts, not accepted production assets.
- `../../DancingCatsClone/Library/RadioRigging/AccessoryReview/{Name}/`: accessory
  review, frozen `Before` backup, mechanical checks and ordinary Blender renders.
- `../../DancingCatsClone/Library/RadioRigging/Technical/{Name}/`: intermediate
  payloads for checking FBX import, axes, skinning and blend shapes in Unity.
- `Processed/{Tiko,Lumen,Nori}/`: destination for reviewed payloads. Each export
  contains `Model.fbx`, `{Name}_Rigged.blend`, `BaseColor.png`, `Normal.png`,
  `MetallicSmoothness.png` and `manifest.json`.

Paths beginning with `../../` are relative to this directory. Run the following
commands from the repository root. The example selects Tiko; substitute Lumen or
Nori explicitly for the other characters.

The commands below reproduce the base rig and accessory pipeline. Finish with
`--stage paws` using the same `prepare_radio_rigs.py` entry point to author and
strictly verify the three-part models. The complete command and review-only
variant are in the linked source summary.

```powershell
$blender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'

# Author and render intermediate review poses. Originals are read-only.
& $blender --background --factory-startup --python-exit-code 1 `
  --python ArtSource/RadioArchipelago/Tools/prepare_radio_rigs.py -- `
  --stage rig --character Tiko

# Export only after reviewing the anatomy, skinning and Blink poses.
& $blender --background --factory-startup --python-exit-code 1 `
  --python ArtSource/RadioArchipelago/Tools/export_prepared_rigs.py -- `
  --blend DancingCatsClone/Library/RadioRigging/Review/Tiko/rig-review.blend `
  --out ArtSource/RadioArchipelago/Processed/Tiko

# Add the v2 mechanical joints and export that base accessory payload.
& $blender --background --factory-startup --python-exit-code 1 `
  --python ArtSource/RadioArchipelago/Tools/prepare_radio_rigs.py -- `
  --stage accessories --character Tiko
```

For a technical import experiment, replace `--out` with
`DancingCatsClone/Library/RadioRigging/Technical/Tiko`. The exporter also accepts
`--character` and `--source` when paths differ from the standard layout.

## Portable maps and export contract

The artist `.blend` uses three external maps in its own folder, with relative
paths. It can be moved together with those PNGs without depending on the ignored
`Library/RadioRigging/Source` extraction. The original BaseColor and tangent normal
PNG bytes are copied exactly; exterior source UVs are preserved. New hidden
cap faces use local samples from the same atlas. The technical packed
map stores metallic in **R** and **1 − roughness** in **A**, with zero G/B and
linear sampling. The Blender shader unpacks those same channels.

The FBX contains only the prepared rig and skinned mesh parts. It retains bone
names, skin weights, custom normals, UVs and the `Blink` shape key. It has no
embedded textures, generated leaf bones, baked animation or applied Armature
modifier. The source frame is Z up, facing −Y, with feet at the origin; the FBX
uses forward −Z and up Y for Unity import.

The exporter decodes the packed PNG, reopens the artist `.blend` and reimports the
FBX before promoting a payload. The manifest records actual mesh/bone counts,
geometry/UV verification, image dimensions, SHA-256 hashes and texture packing.
Successful technical export does not substitute for visual approval of the rig
or an animation playtest in Unity.

Short shoulder bones follow the user's
[shoulder guide](../../Docs/Design/RadioArchipelago/RigReferences/shoulder-guide-user.png).
The new rigid paws rotate as whole surfaces from those shoulders, with their
attachments hidden inside the torso in both stroke extremes. Their actual edge
lengths must stay invariant after removing the global visual squash, eliminating
the former painted shoulder stretch. The connected-source check belongs to the
first body/arm revision. Later mechanical seams and the new paw cuts/caps need
their own edited-source-to-FBX roundtrip checks; earlier source-surface error
measurements must not be attributed to newly created geometry.

Historical `mechanical-yaw-and-four-bone-scarves-v2` payload counts, before paw
separation (new revision counts and hashes pending):

| Character | Vertices | Triangles | Bones |
|---|---:|---:|---:|
| Tiko | 12,203 | 24,454 | 23 |
| Lumen | 9,431 | 18,878 | 12 |
| Nori | 7,344 | 14,704 | 21 |

The accessory structure retained for the new revision is
`body → antenna.yaw → antenna.01` on all three. Tiko/Nori have two independent
four-bone scarf branches; Lumen's rigid sail turns above the head. The fixed and
moving sides of each mechanical seam are separately capped. Their cap surfaces
are recessed 0.004 source units with closed sidewalls, avoiding coincident
triangles while preserving the exterior rings and assembly shape. In v2, original
open-edge counts remained 40/0/0. That revision's per-file hashes, strict export
checks and mechanical QA are recorded in its manifests. Its portable proof is in
[RigReview/Accessories](../../Docs/Design/RadioArchipelago/RigReview/Accessories).
Those reports predate the detached paws. New source, Unity import and animation
validation must be repeated, including both shoulders, rigid weights, no body
triangle bridges, full-cycle geometry/mirroring, hidden roots and culling bounds.
