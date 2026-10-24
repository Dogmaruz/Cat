# Radioarchipelago: selected character rigs

The latest user request, on 24 October 2026, supersedes the connected-arm
revision: each supplied paw is separated into a rigid imported part, with its
closed root buried inside the torso at the shoulder. Each paw has **100% weight
on its exact `arm.L` or `arm.R` bone**, with no fractional body influence or
triangle bridge to the torso. The body has no shoulder-bone influence. The
original exterior shape and painted UVs outside the repaired shoulder contact are retained; hidden caps are new
geometry. No replacement character primitives are used.

Short shoulder bones still follow the diagonal of the original hands, matching
the user's marked [Tiko reference](ModelReferences/Tiko/arm-axis-user-reference.png).
Flight uses simultaneous mirrored strokes at 4 Hz with eased endpoints; the
long-flight target range is −10…60° relative to the rest pose. Short jumps use
a smaller range, and idle/greeting remains a separate, restrained gesture.
See [PAW_MOTION.md](PAW_MOTION.md) for the measured reference timing and our
independent runtime implementation. Original cat animation assets are not
attached to the courier rigs.

The separate-paw exports passed strict source/FBX roundtrip and actual Unity
rigid-paw, rig, accessory, 240-frame motion, nine-menu and full 67-second gameplay
checks. Current source counts follow; earlier connected-arm proof remains historical.
See [source-summary.md](RigReview/Paws/source-summary.md) for the exact payloads.

| Character | Meshes | Vertices | Triangles | Bones | Blink |
|---|---:|---:|---:|---:|---|
| Tiko | 3 | 13,398 | 26,856 | 23 | One local shape key |
| Lumen | 3 | 10,052 | 20,114 | 12 | One local shape key |
| Nori | 3 | 7,831 | 15,670 | 21 | One local shape key |

All characters retain the supplied 4096² colour and normal texture files.
Only a derived URP metallic/smoothness image is added. Existing exterior UVs
remain painted as supplied outside repaired contacts; hidden cap/contact UVs sample clean regions of the same atlas. Local
eyelid deformation is retained, while the former soft shoulder deformation is
replaced by whole-paw rotation. No global decimation is part of this update.

## Motion contract

The source is Blender Z up, facing −Y, 2.6 units high, with the root at the feet.
Runtime FBX uses forward −Z/up Y. The new imported mesh roles are `Character`,
`Arm.L` and `Arm.R`; the `Blink` shape key belongs to the body/face surface.
The eye change stays local to the painted eyes and is projected back
onto the source face. The head is not compressed to imitate blinking.

Common bones are `root`, `body`, `head`, `arm.L/R`, `foot.L/R` and an antenna
chain under `body → antenna.yaw → antenna.01`. Tiko also has its original
forelock chain; Tiko and Nori have two four-bone scarf chains. Lumen has its
original sail/antenna assembly, with no invented scarf or forelock. Its lower
brace to the backpack is removed; the sail joint is above the head. The radio
stays on the body. Runtime animation drives bone transforms
and Blink; separately authored action clips are unnecessary for procedural motion.

The new paw roots overlap the body surface in the lower and upper stroke
positions. A rigid paw has one shoulder transform, so its own edge lengths must
remain constant when measured without the global visual squash. The torso and
fixed harness remain on body weights instead of being pulled by the paw.

Free accessories respond to actual root velocity/acceleration and heading
changes. Each four-bone scarf branch shares a total bend budget up to 68°;
softer springs and a travelling wave make its ends move more visibly. Antennas
retain their mounting hinges, rotate in long flight, and turn against the
character's world rotation on a sustained ride. Animation still changes only
the presentation hierarchy, never the gameplay trajectory or contact timing.

`CourierRigidPawChecks.RunChecks()` is the Editor acceptance check for imported
faces: exclusive shoulder weights, no paw/body triangle bridges, real edge
lengths, signed mirroring of both paws over four strokes, visible excursion and
culling bounds. Raw mesh boundary counts are diagnostic because UV/normal
splits prevent a fair closedness test on unmerged indices. Source cap/hidden-root
inspection and a Unity visual review remain necessary in addition to that test.

## Historical mechanical accessory revision v2: 24 October 2026

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
original surface. The v2 hashes and roundtrip results were recorded in its
`Processed/<Name>/manifest.json`; new exports must record their own results.
Portable mechanical reports and Lumen views
are in [RigReview/Accessories](RigReview/Accessories).

These reports predate the separate paws. They do not establish that the new
source geometry, Unity prefabs or gameplay have passed; export, import, pose,
camera and runtime validation must be repeated for the new revision.

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
4.22 × 10⁻⁸ units. In that earlier revision, four isolated source triangle
flecks were removed and no new surface cuts were introduced. Boundary counts
increase when an existing edge is subdivided, which does not introduce a hole.

The connected revision used a local harmonic field with soft shoulder/harness
weight transitions. That approach is now superseded by separate rigid paws.
Tiko's historical 25° strain audit found no hard 0/1 adjacent pair among 7,166 sampled
arm edges. Two former needle edges improved from about 34× stretch to 1.0067×
and 1.0554×. A mixed-UV corner improved from 5.607× to 2.142× after smoothing.
Some soft inner shoulder folds remained on that fused source mesh; the 15/25°
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

Earlier split-shell prototypes and high-angle PNGs in Library remain superseded
experiments; they are not the newly reviewed separate-paw revision. Do not
reimport those old experiments as a shortcut. The final source command and
accepted payload manifests for the current revision will be recorded after its
authoring/export review; existing base-pipeline commands do not complete it.

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
