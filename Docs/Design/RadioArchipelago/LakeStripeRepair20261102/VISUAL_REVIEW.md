# Independent review of the rejected lake stripe repair

The earlier classification of the visible lake stripe as an acceptable cosmetic limitation is superseded. The user's requirement is a continuous natural lake appearance without the former waterfall ribbon inside it. Geometric single-surface evidence alone does not satisfy this requirement.

Result: PASS for removal of the rejected stripe in all seven actual lake views and preservation of the visible Amber falling curtains. No new actionable seam, detached layer or shader-basis defect found in this review.

## Native appearance review

Inspected actual original PNG frames `000`, `010` and `023` in each after view below. Runs use the exact saved reference camera poses and FOV 82, with full scenery and saved materials. Each view contains 24 native frames. These are explicit art views, not normal gameplay camera proof.

| After run / view | Target | Native interval (seconds) | Image-based result |
|---|---|---|---|
| `moon-lake-reference-20261102-181510-7405321`, view0 | `RA_Moon_Waterfall_+00_+03_01` | `0.973146–3.434808` | PASS. The former straight bands at the near shore are absent. Quiet two-dimensional ripples continue across the broad lake and narrow outlet; the outlet no longer carries a separate white waterfall stripe. |
| Same run, view1 | `RA_Moon_Waterfall_+02_+01_01` | `3.674646–6.037169` | PASS. The broad parallel lines visible before are absent; the lake has a continuous ripple field. Heavy haze limits fine-detail judgment, but the same view clearly distinguishes the prior lines from their removal. |
| `coral-lake-reference-20261102-181552-9358737`, view0 | `RA_Coral_Waterfall_+01_-01_01` | `0.834376–3.145417` | PASS. The central waterfall-coloured band through the small lake is absent. Lake and horizontal outlet share the same water appearance. |
| Same run, view1 | `RA_Coral_Waterfall_+01_-03_01` | `3.389092–5.707469` | PASS. The wide near-shore parallel band is absent. Ripples cover the former band and the surrounding lake continuously. |
| `amber-lake-reference-20261102-181641-9939035`, view0 | `Amber_VoluminousFallingWater_0_0` | `0.834499–3.143721` | PASS. The broad near-shore bands and the narrow waterfall-patterned outlet patch are absent. Quiet ripples cover the entire lake and horizontal outlet. |
| Same run, view1 | `Amber_VoluminousFallingWater_1_0` | `3.387206–5.698309` | PASS. The lake and winding horizontal creek share the quiet water appearance without an internal waterfall strip. Distance and haze limit fine detail. |
| Same run, view2 | `RA_Amber_SideCascade_RaisedFall_Layer0` | `5.949159–8.269704` | PASS. Source pool and flat reach have one continuous ripple field, with no foam ribbon inside the pool. Falling streaks begin at the real downward bend. The straight transition visible in projection is discussed explicitly below. |

For Moon, directly compared the fresh before run `moon-lake-reference-20261102-181324-6715252`: camera positions, rotations and FOV match the after report exactly. Before intervals are `0.833875–3.145735` and `3.391006–5.719587`. The before shows both rejected bands; they are absent in the after frames across the sampled times. The original saved Coral reference frames also show the now-removed central and near-shore patterns.

Evidence folders are under `DancingCatsClone/Library/RadioArchipelago/WaterfallVisual/`. Exact PNG prefixes are `view-0-station--001-`, `view-1-station--002-` and Amber `view-2-station--003-`, followed by the frame number and `.png`. Each report confirms `nativeClock:true`, `savedMaterialsUnchanged:true`, `originalSceneRestored:true`, `shaderErrors:false`. No time override or interpolated frames were used.

### Amber raised-lip transition

Raised lake view2 visibly projects the quiet fixed-width reach as a rectangle, ending in a straight change toward falling streaks. I inspected this specifically as a possible regression. The source pool and the entire horizontal reach have uninterrupted quiet appearance; the change occurs on the actual bend into the falling body, rather than across a flat patch inside the pool. No detached edge, gap, raised overlay or additional stationary ribbon is visible in frames `000`, `010`, `023`.

The shader also places the blend on the real bend: a flat normal gives lake weight 1; its horizontal contribution changes smoothly as the roll tilts through approximately 5.7–20 degrees. The visible change is therefore the quiet reach becoming aerated falling water at the lip, not a new horizontal material split. This observation does not hide the conspicuous rectangular projection, and does not claim user artistic approval.

### Exterior falling-body regression

Inspected frames `000`, `010`, `023` of all three after views in `amber-body-reference-20261102-181748-7870697`, replaying the three saved exterior camera poses. Each contains 24 native frames at FOV 82. Its report confirms native clock, unchanged saved materials, restored original scene and no shader errors.

| View / target | Native interval (seconds) | Result |
|---|---|---|
| view0 / `Amber_VoluminousFallingWater_0_0` | `0.834286–3.147531` | PASS. The exposed lower curtain remains connected, with downward-moving broken white foam and its existing soft lower fade. |
| view1 / `Amber_VoluminousFallingWater_1_0` | `3.379715–5.702178` | PASS. Quiet horizontal river meets the rounded spill and the continuous falling curtain. White packets continue downward; no flashing gap or extra sheet appears. |
| view2 / `RA_Amber_SideCascade_RaisedFall_Layer0` | `5.963896–8.297713` | PASS. The broad falling body retains its descending streaks and connection to the receiving pool. No new seam is visible through the curtain. |

## Read-only shader review

Compared the updated `DancingCatsClone/Assets/RadioArchipelago/Art/AmberFlowWater.shader` with `Before-AmberFlowWater.shader`.

Reviewed shader SHA256: `cbe7820dea0ecc4831418fa8b6ace4950a52d480294a61d0ee9a71c8e1474ae7`.

- Lake ripples, variation and vertex-wave phase use world XZ; lake normal detail uses a matching world basis. Thus the clamped/singular lake UV0 no longer drives any pure-lake visible pattern.
- The horizontal weight overrides the old internal ribbon mask on flat water, removing the narrower foam strip as well as the broad UV bands. The weight transitions as the real spill bends down.
- Lake foam replaces ribbon foam before specular attenuation, closing the earlier remaining dependency.
- `_UseLakeSurface=0` preserves the original calculations. On the enabled surfaces, vertical body vertices with mask 1 and absolute normal Y at most .94 retain weight 0 and the original calculations. The flat reach and upper bend intentionally change to the continuous lake appearance.
- No actionable NaN/tangent-basis blocker found for the validated imported geometry. Where the new horizontal contribution activates, the normal's Y component keeps the projected world-X basis nonzero; pure-lake flat normals are also safe. Vertical mask-1 body vertices skip the lake basis.

This is an independent agent review, not user artistic approval or phone-performance certification. No Assets, Editor state or queue changes were made by the reviewer. Only this review document was written.
