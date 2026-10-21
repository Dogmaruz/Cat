# Radioarchipelago character source assets

The supplied Tiko, Lumen and Nori models are prepared in **Blender 5.2.1 LTS**.
The original ZIP archives remain unchanged. Rig authoring and exports run in
isolated background Blender processes; they do not open or replace a user's
interactive Blender scene and do not build a Unity player.

## Layout

- `Incoming/{Tiko,Lumen,Nori}/`: reserved model handoff folders.
- `../../Docs/Design/RadioArchipelago/ModelReferences/{Name}/{Name}.zip`:
  the actual supplied archives for this handoff. They are local source files;
  each exceeds 100 MB and is excluded from the Git handoff.
- `Tools/prepare_radio_rigs.py`: source extraction, inspection, connected-mesh
  skinning, localized shoulder/feature weights, Blink and authoring review renders.
- `Tools/export_prepared_rigs.py`: portable artist project and verified Unity FBX
  payload export from an accepted authoring review.
- `../../DancingCatsClone/Library/RadioRigging/Source/{Name}/`: extracted source
  FBX and atlases. `Library` is ignored by Git.
- `../../DancingCatsClone/Library/RadioRigging/Review/{Name}/`: intermediate
  `rig-review.blend`, `rig-manifest.json` and pose renders. These are review
  artifacts, not accepted production assets.
- `../../DancingCatsClone/Library/RadioRigging/Technical/{Name}/`: intermediate
  payloads for checking FBX import, axes, skinning and blend shapes in Unity.
- `Processed/{Tiko,Lumen,Nori}/`: destination for reviewed payloads. Each export
  contains `Model.fbx`, `{Name}_Rigged.blend`, `BaseColor.png`, `Normal.png`,
  `MetallicSmoothness.png` and `manifest.json`.

Paths beginning with `../../` are relative to this directory. Run the following
commands from the repository root. The example selects Tiko; substitute Lumen or
Nori explicitly for the other characters.

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
```

For a technical import experiment, replace `--out` with
`DancingCatsClone/Library/RadioRigging/Technical/Tiko`. The exporter also accepts
`--character` and `--source` when paths differ from the standard layout.

## Portable maps and export contract

The artist `.blend` uses three external maps in its own folder, with relative
paths. It can be moved together with those PNGs without depending on the ignored
`Library/RadioRigging/Source` extraction. The original BaseColor and tangent normal
PNG bytes are copied exactly; original UVs are preserved. The technical packed
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

The selected rig preserves one connected character mesh from each original ZIP.
Short shoulder bones follow the user's [shoulder guide](../../Docs/Design/RadioArchipelago/RigReferences/shoulder-guide-user.png),
with small arm swings and smooth local weights. Arms are not cut off the torso.
The connected-source check compares rest geometry and interpolated UVs against
the original, and distinguishes inherited source boundaries from newly added cuts.
