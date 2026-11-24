# Miro Garden — strapless case candidate

Use the **nested `Miro_Garden/` directory**, not the older experimental files in this directory.

The nested directory contains the final `Model.fbx`, `Meta.json`, three unchanged PBR PNGs, and `editable.blend`. Four native Blender renders (`radio`, `contact`, `side`, `top`) show the finished asset without its moving top part obscuring the case.

The original leaf, wooden radio speakers, corner metalwork and their source faces/UVs on `Y >= -0.025` are preserved. The whole wearer-facing return, its straps, mounting roots, tabs and old padding were removed. A new rounded enclosure follows the original case cross-section. It has a new closed leather cushion and saddle stitching. No Boolean union is used.

The original central roof bearing is retained; a small enamel seat supports it. The reconstructed housing stops at `Z = 0.755`, below the raised original roof mechanisms. Thirty-nine triangles on the old horizontal spindle cut cap at `Z = 0.808086` intentionally receive a clean brass atlas texel, replacing their averaged black/striped UVs. Their positions are unchanged. This small bearing-cap UV repair is the only intentional exception to source-front UV preservation.

The moving `TopRotor`, its pivot, the original textures, and the case reference height `0.83` are unchanged. The measured new contact pad is `contactPlaneY = -0.253`; fixed mesh bounds and triangle counts are updated in metadata.

Reproduce with `../miro_loft.py`, which reads the immutable `StraplessCases/Before/Miro_Garden` backup and only writes this candidate. The script is a Miro Garden repair study, not a generic authoring command for the whole collection.

The parent agent owns copying an approved candidate into prepared/processed assets and refreshing Unity. This study never modifies those locations or starts Unity.
