# Radioarchipelago: selected character rigs

The selected approach starts from each supplied original ZIP. The body and hands
remain connected; the antenna now has a separate sealed mechanical seam so its
mounting joint can turn without twisting body vertices. Short shoulder
bones follow the diagonal of the original hands, matching the user's marked
[Tiko reference](ModelReferences/Tiko/arm-axis-user-reference.png). Working waves
are approximately 15–30°; 35° is a stress pose. There are no arm/body cuts,
replacement shoulders, arm cap meshes or static hand stumps in these rigs.

| Character | Meshes | Vertices | Triangles | Bones | Blink |
|---|---:|---:|---:|---:|---|
| Tiko | 1 | 12,203 | 24,454 | 23 | One local shape key |
| Lumen | 1 | 9,431 | 18,878 | 12 | One local shape key |
| Nori | 1 | 7,344 | 14,704 | 21 | One local shape key |

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
chain under `body → antenna.yaw → antenna.01`. Tiko also has its original
forelock chain; Tiko and Nori have two four-bone scarf chains. Lumen has its
original sail/antenna assembly, with no invented scarf or forelock. Its lower
brace to the backpack is removed; the sail joint is above the head. The radio
stays on the body. Runtime animation drives bone transforms
and Blink; separately authored action clips are unnecessary for procedural motion.

Arm weights use a local harmonic field with soft transitions around the fused
source shoulder and harness. Distal anchors are kept away from fixed weight
boundaries. This prevents a hard 0/1 jump across a few millimetres from pulling
the hand into spikes during even a small wave. Linear skinning matches Unity.

## Mechanical accessory authoring: 24 October 2026

Revision `mechanical-yaw-and-four-bone-scarves-v2` preserves the three existing
texture files byte for byte. Mechanical cuts receive separate sealed caps on
the fixed and rotating sides. Each cap is recessed 0.004 source units into its
own side, with closed sidewalls and unchanged exterior rings; opposing cap
triangles no longer occupy the same plane. Lumen's removed gold brace accounts for 183
deleted triangles before local cuts/caps are triangulated; its fabric remains.
Open-edge counts are unchanged: Tiko 40 inherited edges, Lumen/Nori zero.

Rotating the joint by 0/90/180/270° keeps all vertices finite and changes edge
lengths by at most 0.0272%. The strict FBX roundtrip reopens each portable artist
project, independently imports the FBX, and checks bones, geometry, UVs, normals,
weights and Blink. Maximum vertex error is below 5.4 × 10⁻⁷; UV and weight errors
are zero. These compare the edited rig with its export, not with the untouched
original surface. Current hashes and roundtrip results are in each
`Processed/<Name>/manifest.json`. Portable mechanical reports and Lumen views
are in [RigReview/Accessories](RigReview/Accessories).

These authoring checks do not establish that the new Unity prefabs or gameplay
have passed; Unity import, pose, camera and runtime validation are separate.

## Historical connected-source evidence: 21 October 2026

Each saved rig passed finite geometry, duplicate-face, normalized-weight and
four-influence checks before mechanical seams were introduced. That earlier
revision had one connected component and no edges with more than two faces.
Its original surface/UV comparison measured:

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
The earlier connected-source review files live in the ignored
`DancingCatsClone/Library/RadioRigging/Review/<Name>/` directory:

- `rig-review.blend`, `rig-manifest.json`;
- `rig-rest.png`, `rig-blink.png`, `rig-flutter.png`;
- `rig-arms-15/25/35.png` and matching `-three-quarter.png` images;
- `continuous-source-check.json`.

Earlier split-shell prototypes and high-angle PNGs in Library are fallback
experiments, not the selected character design. Do not reimport them.

Historical reviewed source Blender SHA-256, before the accessory revision:

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
