# Radioarchipelago character authoring

The supplied Tiko, Lumen and Nori FBX geometry is the source of every visible
character part. The original ZIP archives and texture images remain unchanged.
The pipeline uses Blender 5.2 in an isolated background process; it never opens
or overwrites an interactive Blender scene. It does not build the Unity player.

## Files and reproducible commands

Run from the repository root in PowerShell. Blender is installed at
`C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`.

```powershell
$radioBlender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/prepare_radio_rigs.py -- --stage inspect --character Tiko
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/prepare_radio_rigs.py -- --stage rig --character Tiko
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/validate_authoring_rigs.py
& $radioBlender --background --factory-startup --threads 4 --python-exit-code 1 --python ArtSource/RadioArchipelago/Tools/validate_continuous_source.py -- --character Tiko
```

The character argument can be `Tiko`, `Lumen` or `Nori`; omitting it processes all
three. Inspection renders front, back, left and right before authoring. `analyze`
writes a source face/colour CSV for anatomical mask work.

- `inspect_supplied_models.py`: read-only archive/FBX inventory, ordinary Python.
- `prepare_radio_rigs.py`: safe extraction, source import, targeted surface
  refinement, armature, skin weights, localized Blink
  shape key, review poses, editable Blender file and authoring manifest.
- `radio_root_caps.py`: retained support for earlier separated-shell prototypes;
  the selected continuous-source profile creates no cuts or caps.
- `validate_authoring_rigs.py`: read-only checks of saved rigs; finite geometry,
  duplicate faces, normalized skin weights and at most four influences. For the
  current profile it also rejects cap attributes; legacy prototypes have
  separate closed-hand/pinned-root checks.
- `validate_continuous_source.py`: connected source mesh, no added cuts,
  rest-surface distance and UV interpolation comparison against the original ZIP.
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

## Rig contract

The selected design keeps the original mesh connected, with short shoulder bones
and small 15–30° waves; 35° is a stress pose. `ANATOMY[character].continuous_skin`
selects this profile. Earlier split-shell files in Library are fallbacks and must
not be mistaken for the selected design. No arm/body cuts, cap geometry or
shoulder sockets are created by the continuous-source path.

Authoring coordinates are Z up, front −Y, height 2.6, root grounded at the feet.
Runtime FBX uses forward −Z/up Y. The import step must not apply a second axis
conversion. The connected source mesh has `mesh_role = Character`. The earlier
split-shell prototypes used anatomical mesh roles; bone names remain common.

The common deform bones are `root`, `body`, `head`, `arm.L/R`, `foot.L/R` and
`antenna.01...`. Tiko has `crest.01/02`; Tiko and Nori have two scarf chains.
Lumen has no invented crest or scarf. Its metal antenna/sail moves as an assembly
to keep the supplied frame and fabric intact.

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

Current reviews contain rest, closed Blink, 15/25/35° waves from front and
three-quarter, plus antenna/crest/scarf flutter. Older 105° files belong to
rejected split-shell prototypes. Review source and final renders before changing
mask coordinates. `ANATOMY` landmarks are in the normalized source frame. Legacy
`arm_planes` use the final foot-centered frame and are ignored for continuous skin.

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
