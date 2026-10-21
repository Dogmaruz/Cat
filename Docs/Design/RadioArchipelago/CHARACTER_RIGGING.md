# Radioarchipelago: selected character rigs

The selected approach starts from each supplied original ZIP and retains one
connected character mesh. The hands remain joined to the body. Short shoulder
bones follow the diagonal of the original hands, matching the user's marked
[Tiko reference](ModelReferences/Tiko/arm-axis-user-reference.png). Working waves
are approximately 15–30°; 35° is a stress pose. There are no arm/body cuts,
replacement shoulders, cap meshes or static hand stumps in these rigs.

| Character | Meshes | Vertices | Triangles | Bones | Blink |
|---|---:|---:|---:|---:|---|
| Tiko | 1 | 11,947 | 23,962 | 18 | One local shape key |
| Lumen | 1 | 9,370 | 18,764 | 11 | One local shape key |
| Nori | 1 | 7,244 | 14,508 | 16 | One local shape key |

All characters use the supplied 4096² colour and normal textures unchanged.
Only a derived URP metallic/smoothness image is added. Local subdivision of the
existing surface supports eyelids and shoulder deformation; no global decimation
or replacement character primitives are used.

## Motion contract

The source is Blender Z up, facing −Y, 2.6 units high, with the root at the feet.
Runtime FBX uses forward −Z/up Y. Each mesh has role `Character` and one `Blink`
shape key. The eye change stays local to the painted eyes and is projected back
onto the source face. The head is not compressed to imitate blinking.

Common bones are `root`, `body`, `head`, `arm.L/R`, `foot.L/R` and an antenna
chain. Tiko also has its original forelock chain; Tiko and Nori have two scarf
chains. Lumen has its original sail/antenna assembly, with no invented scarf or
forelock. The radio stays on the body. Runtime animation drives bone transforms
and Blink; separately authored action clips are unnecessary for procedural motion.

Arm weights use a local harmonic field with soft transitions around the fused
source shoulder and harness. Distal anchors are kept away from fixed weight
boundaries. This prevents a hard 0/1 jump across a few millimetres from pulling
the hand into spikes during even a small wave. Linear skinning matches Unity.

## Read-only authoring evidence

Each saved rig passed finite geometry, duplicate-face, normalized-weight and
four-influence checks. Each has one connected component and no edges with more
than two faces. Original surface/UV comparison measured:

| Character | Max rest-surface distance | Max UV interpolation error | Boundary |
|---|---:|---:|---|
| Tiko | 1.35 × 10⁻⁷ | 4.27 × 10⁻⁷ | Existing source borders retained |
| Lumen | 1.24 × 10⁻⁷ | 2.67 × 10⁻⁷ | Closed |
| Nori | 1.34 × 10⁻⁷ | 2.15 × 10⁻⁷ | Closed |

Tiko's subdivided boundary lies on the original ZIP boundary segments within
4.22 × 10⁻⁸ units. Its four isolated source triangle flecks are removed; no new
surface cuts are introduced. Boundary counts increase when an existing edge is
subdivided, which does not introduce a hole.

Tiko's final 25° strain audit found no hard 0/1 adjacent pair among 7,166 sampled
arm edges. Two former needle edges improved from about 34× stretch to 1.0067×
and 1.0554×. A mixed-UV corner improved from 5.607× to 2.142× after smoothing.
Some soft inner shoulder folds remain on the fused source mesh; the 15/25°
front and three-quarter review was accepted. Lumen's evaluated Blender morph
normals create vertical eyelid shading, even without normal mapping. The Unity
importer retains the authored rest normals during Blink, removing those creases
without changing the source vertices. Actual Unity evidence is in
[RigReview](RigReview/README.md).

## Files and reproducibility

The authoring guide and commands are in
[`Tools/README.md`](../../../ArtSource/RadioArchipelago/Tools/README.md).
Current review files live in the ignored
`DancingCatsClone/Library/RadioRigging/Review/<Name>/` directory:

- `rig-review.blend`, `rig-manifest.json`;
- `rig-rest.png`, `rig-blink.png`, `rig-flutter.png`;
- `rig-arms-15/25/35.png` and matching `-three-quarter.png` images;
- `continuous-source-check.json`.

Earlier split-shell prototypes and high-angle PNGs in Library are fallback
experiments, not the selected character design. Do not reimport them.

Frozen reviewed source Blender SHA-256:

| Character | SHA-256 |
|---|---|
| Tiko | `11736e110f3e66d3f9f8376a2b920a43f8b37b0da8385c4104b3e9a85ff6bbcd` |
| Lumen | `06200f75bb959b586b219e893224e0f4395844122c411c9a5ac22c34f6f7aa0b` |
| Nori | `ed28016405d5e83f764fea7edd488ff3ffc64ca61913a50d80828c63a10829a9` |

The exporter promotes only a complete payload after FBX roundtrip geometry, UV,
normal, weight, bone and Blink checks. Canonical editable/runtime packages are
`ArtSource/RadioArchipelago/Processed/<Name>/`. Their manifests contain the exact
reviewed input digest and exported file digests. Editor pose, renderer-bound and
gameplay checks remain a separate Unity validation step. No player builds or
music changes are part of this authoring pipeline.
