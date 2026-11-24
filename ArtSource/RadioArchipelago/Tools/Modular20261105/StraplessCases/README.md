# Strapless radio cases — 2026-11-05

`Loft/` is the accepted, staged payload directory. Each item contains the FBX,
editable Blender scene, metadata and three original PBR texture files. The
project imports the published copies in `ProcessedModular/Backpacks`.

`Before/` preserves all twenty incoming prepared assets from before this task.
The user's original ZIP archives in `ModularCast20261102/Incoming/Backpacks`
were never edited. Texture files are byte-identical to the pre-task derivatives.

The clean returns follow the actual individual case cross-section, with a
matching enamel surface and compact leather contact cushion. Original radio
facades, switches, speaker grilles and top rotating mechanisms remain. There
are no `Harness.*` mesh objects in the accepted payloads. The fixed body harness
is now a separate character-owned Unity asset, authored outside this directory.

The common authoring recipe is `loft_cases.py`. `cap_uv_only.py` performs a
cosmetic spindle-cap correction without changing geometry; this was used for
the already accepted Nori derivative. Miro_Garden, Lumen_Glider and Fira_Beacon
use inspected individual authoring recipes in the adjacent `CaseRepairStudy`
directory, and their accepted payloads are copied into `Loft/`.

Rumi has an explicit source-boundary exception: generic `holes_fill` must never
run on its retained source mesh. Its intentional grille/roof edge networks were
mistaken for holes in an earlier rejected payload, generating fans over the
front grille. The closed replacement back already seals the rear cut. The
`RumiFrontCorrection` matched views and surface proof record 2,395 incorrect
front samples in that rejected payload and zero in the corrected 23,022 samples.

Piko also preserves source winding rather than globally orienting its open
islands. Unity's normal backface culling exposed an orientation regression hidden
by Blender's double-sided preview. `PikoWindingCorrection` records 1,917 repaired
source faces, zero remaining opposed facade faces and unchanged positions/UV
corners. Source-normal lookup includes barycentric UV matching to disambiguate
nearly coincident source surfaces. The replacement loft's winding is unchanged.

`Prepared/`, `Localized/`, and `Final/` contain rejected intermediate methods.
Do not import them. Global arbitrary depth cuts and Boolean unions on the
generated source cases were rejected because they damaged radio facades.

`Review/` contains actual Blender case-only renders. The moving top is hidden
only for those inspection renders; it is present in every exported model.
`validate.py` checks exact rotating-mesh geometry, UVs and transform against
`Before`, the original pivot, preserved case height/width, unchanged source ZIP
hashes, finite mesh coordinates and absence of obsolete harness objects.
`validation-all.json` and `payload-hashes.json` record the final checked payloads.

Generated input meshes already contain small topology defects. The renderer
uses closed replacement return shells that overlap the retained source case;
these cosmetic assets do not require one unified collision manifold. No build
or paid generation service was used for this task.
