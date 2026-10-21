# Supplied Lumen / Nori attachment anatomy

Historical inspection notes from the initial authoring pass. The selected rigs
and corrected attachment masks are documented in [CHARACTER_RIGGING.md](CHARACTER_RIGGING.md).
References below to “current” masks/coordinates describe that initial pass,
not defects remaining in the selected production assets.

Read-only QA against the four `source-*.png` views and `source-faces.csv` in
`DancingCatsClone/Library/RadioRigging/Review/{Lumen,Nori}`. These coordinates
use the **normalized source inspection frame**, before subtracting
`source_foot_offset`. Apply that subtraction exactly once when creating bones.
The measured face-center bounds omit a small fraction of the vertex radius.

## Lumen

The sail, curved lower support, forward mast, and small hanging light form one
assembly. The current `feature_kind` omits most of the hanging light because
it ends below `z = 1.82`.

- Small hanging light: sphere fitted to 71 pale-yellow source faces:
  center **`(.4454, -.4826, 1.7560)`**, face radius **`.0900`**. Use a vertex
  radius near `.095`, not the existing center `(.43, -.39, 1.77)` / radius `.083`.
  The existing Y center is off by `.093`; spherical refinement at that location
  can bend the stalk and miss the actual light.
- Keep its entire sphere in the antenna mask (distance to center `< .115`).
- Include the short light stalk with a capsule from
  `(.21,-.23,1.95)` to `(.445,-.483,1.756)`, radius about `.055`.
- Retain `z > 1.82` for the upper assembly. For the lower gold support, use
  `z > 1.34 and x < -.27 and y > .22`; blend the root to body over the first
  `.10–.17` units. The gold radio mount should stay with the backpack/body.
- Do not simply lower the global Z cutoff: the yellow head curls up to `1.80`
  immediately beside the mast.

Suggested source-frame bow chain, following the actual support and outer bow:

```python
[(-.20,.16,1.37), (-.615,.52,1.45), (-.49,.40,1.94),
 (-.20,.11,2.31), (.21,-.25,2.53)]
```

The existing lower outer point `(-.60,.63,1.46)` is too far back: the entire
source mesh has maximum Y `.5767`. The existing mid-bow `(-.43,.47,1.99)`
also lies behind the actual bow plane. Keep the frame's deformation mild;
strong independent chain rotations will distort the triangular sail frame.

## Nori

The wrapped scarf collar must remain attached to the torso. Only the two free
rear-left streams should receive scarf-chain motion. The current `sky` mask
also accepts mint radio pixels, and its rectangular cutoff admits a few
collar/radio faces around `(x≈-.56,y≈.2,z≈1.1)`.

Safer source-frame free-tail envelope:

```python
sky = g > .25 and b > g*1.08 and b > r*1.20 and r > .10
free_tail = (sky and x < -.60 and y > .43 and y-x > 1.05
             and .50 < z < 1.15)
# Upper / lower are separated by z≈.86 after the attachment knot.
```

This selects 689 source face centers versus 767 for the current mask, removes
the mint/body contamination, and preserves both visible free streams. Blend
the first `.12–.17` length from the knot back to body so the cut cannot pull
the wrapped collar open. Leave the small unselected attachment wedge rigid.

Suggested chains:

```python
upper = [(-.60,.48,.89), (-.85,.75,.98), (-1.22,1.14,.94)]
lower = [(-.60,.48,.85), (-.85,.71,.72), (-1.10,.96,.56)]
```

Measured upper-stream face medians by successive `y-x` bands are
`(-.658,.555,.931)`, `(-.758,.671,.939)`, `(-.918,.816,.976)`,
`(-1.136,1.054,.923)`. Lower-stream medians are
`(-.663,.525,.763)`, `(-.796,.664,.732)`, `(-.950,.815,.710)`,
`(-1.067,.936,.656)`. Thus both existing chain end Y coordinates `.69/.78`
stop well before the free tips and should move rearward.

The Nori orb definition is already accurate: source faces have median
`(.709,-.921,2.195)` versus config `(.715,-.930,2.197)`. Keep radius `.202`.

For the flexible navy antenna, keep the metallic base/hinge rigid. Above
`z > 1.83` all navy arc points are safely above the head. The additional
low mask should be restricted to the actual navy tube (`x < -.55`,
`z > 1.76`, `y > .22`, navy color), instead of collecting the bronze hinge
and radio trim at `z > 1.56`. Fade bone influence at the tube's base.

Suggested arc centerline (approximate centers of source cross sections):

```python
[(-.62,.38,1.74), (-.65,.40,1.98), (-.39,.14,2.30),
 (.04,-.29,2.48), (.41,-.67,2.42), (.715,-.930,2.197)]
```

The current left mid-arc point `(-.55,.26,2.30)` lies toward the arc's
outside edge. Small motion is forgiving; rotations should still be checked
from both side views for pinching or moving the rigid hinge.

## Validation focus

Use original appearance as the bind pose, with no missing face indices or
discontinuous normals. On motion previews, inspect the collar, backpack
buckles, sail-to-head clearance, light stalk, and antenna base. A logical
mesh split alone does not prevent the wrong vertices from receiving the
attachment's weights; apply the same anatomical rule when assigning skin
weights. Preserve the shared boundary coordinates and UVs during splitting.
