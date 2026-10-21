# Supplied character models

Read-only archive audit, 2026-10-21. The original ZIPs were neither extracted nor modified by this audit. Metadata was parsed directly from ZIP entries and binary FBX records; no Blender or Unity import was performed.

| Character | ZIP bytes | Vertices | Triangles | Meshes / materials | Textures |
|---|---:|---:|---:|---:|---|
| Tiko | 111,866,610 | 4,613 | 9,298 | 1 / 1 | Five 4096 × 4096 RGBA8 PNGs |
| Lumen | 106,926,955 | 4,824 | 9,672 | 1 / 1 | Five 4096 × 4096 RGBA8 PNGs |
| Nori | 111,496,601 | 4,747 | 9,514 | 1 / 1 | Five 4096 × 4096 RGBA8 PNGs |

All three models are binary FBX 7200 with UVs, normals, smoothing and material assignments. There are no armatures, skin deformers, blend shapes or animation stacks. They are already triangulated. Mesh counts are small enough that destructive global decimation is unnecessary for the requested rigging.

Lumen and Nori each have one connected mesh component. Tiko has one 4,601-vertex component plus four isolated triangles (three vertices each). Splitting loose parts alone will not separate hands, scarf, head curl or antenna. Those features need intentional boundary selection, separation and/or bone weighting while retaining the supplied UV atlas.

The FBX files themselves are 53,926,752–56,404,976 bytes because each also embeds its five PNGs. Embedded texture content is 53,565,000–56,057,170 bytes; this is not evidence of dense geometry.

## Original archive members

| Character | FBX member | Texture folder |
|---|---|---|
| Tiko | `tripo_convert_3fe63071-4a9b-4eab-9c2e-1ed68c5da764.fbx` | `tripo_convert_3fe63071-4a9b-4eab-9c2e-1ed68c5da764.fbm/` |
| Lumen | `tripo_convert_899d6dab-f1b7-493e-8219-5b659bff8f54.fbx` | `tripo_convert_899d6dab-f1b7-493e-8219-5b659bff8f54.fbm/` |
| Nori | `tripo_convert_eda09ebe-ce08-4a62-b246-315921b0f0f1.fbx` | `tripo_convert_eda09ebe-ce08-4a62-b246-315921b0f0f1.fbm/` |

Texture names are `<Character>_basecolor.PNG`, `_normal.PNG`, `_metallic.PNG`, `_roughness.PNG`, `_rm.PNG`. All have an alpha channel; sampled basecolor alpha was fully opaque. The packed RM image is not an immediately compatible Unity URP metallic/smoothness texture: sampled green follows roughness, blue follows metallic, red is almost white, alpha is white. Use the separate maps as the explicit inputs and pack metallic into red and inverted roughness into alpha for a derived URP texture. Keep source maps unchanged and data maps linear. The packed map's channel interpretation is an inference from sampled pixel comparisons, not supplied documentation.

Internal FBX texture paths include `textures/<name>.PNG` and stale `../../../../../../../<name>.PNG` references. Resolve by basename within the matching extracted `.fbm` folder rather than following those relative paths.

## Scale and axes

All three FBX files declare Z-up and a unit scale factor of 1 (centimetres), with a model-local X rotation of 90 degrees. Their raw geometry height is on local Y:

| Character | Raw geometry bounds size X / Y / Z (cm) |
|---|---|
| Tiko | 84.952 / 97.980 / 76.114 |
| Lumen | 50.986 / 98.007 / 43.640 |
| Nori | 92.377 / 97.858 / 86.609 |

Confirm import axes visually and normalize the root once before rigging. Do not both bake a coordinate conversion and compensate a second time in Unity. Ground the final root at the feet and preserve relative proportions between the three characters.

## Safe working layout

Recommended extraction destinations are `ArtSource/RadioArchipelago/Source/Tiko/`, `Source/Lumen/` and `Source/Nori/`, retaining each FBX's sibling `.fbm` folder. These are working copies; retain the original archives under `Docs/Design/RadioArchipelago/ModelReferences/<Character>/<Character>.zip` unchanged. Keep `.blend` authoring files under `ArtSource/RadioArchipelago/Blender/`, and put only final exported runtime FBX/textures in `DancingCatsClone/Assets/RadioArchipelago/`.

Before any archive extraction, validate every member's resolved destination remains under its explicitly selected character directory. The current archives contain no absolute paths or parent traversal in ZIP member names.

## Integrity and reproducibility

| Character | Original archive SHA-256 |
|---|---|
| Tiko | `58cddf894dd4e209ed8722baf4fd6a6e0d676f1fd506368216bbdf918b65ec6d` |
| Lumen | `02176db05a89cb439563a92655db66e0d5022ac6d341b002d5410d5de340dddd` |
| Nori | `7daf1330989f6bd7826f99533a496f53fac6e6010f1353bf2af141de59802f68` |

Full parsed metadata: [supplied-model-inventory.json](supplied-model-inventory.json).

Inspection script: `ArtSource/RadioArchipelago/Tools/inspect_supplied_models.py` (Python standard library only). Re-run from the repository root:

```powershell
python ArtSource/RadioArchipelago/Tools/inspect_supplied_models.py Docs/Design/RadioArchipelago/ModelReferences --output Docs/Design/RadioArchipelago/supplied-model-inventory.json
```
