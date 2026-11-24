# Independent native water review — 2 November 2026

**Appearance acceptance superseded by user feedback:** the directional lake bands described below as cosmetic were rejected. Their UV/shader causes and corrected native comparisons are recorded in `../LakeStripeRepair20261102/`. This earlier report remains evidence of connected geometry and exposed falling bodies, not approval of the old lake shading.

Status: all seven lake/outlet and falling-body art-camera reviews complete. The joins read as one continuous surface and both repaired Coral curtains are exposed outside the cliffs. Cosmetic and camera-scope limits remain below. This is an agent review of actual frames, not user artistic approval.

## Native evidence inspected

Evidence folders below are under `DancingCatsClone/Library/RadioArchipelago/WaterfallVisual/`. Each contains `report.json`, 24 original PNG frames per view, and native-timed GIF/MP4 packages. I visually compared frames `000`, `005` and `010` for each view; the interval column gives the entire recorded native sequence. All five are explicit side art cameras at FOV82, with full scenery, rather than normal route camera evidence.

| Run / view | Target | Native time interval (seconds) | Visual result |
|---|---|---|---|
| `moon-art-20261102-101922-5109158`, view0 / station−1 | `RA_Moon_Waterfall_+00_+03_01` | `0.837934–3.147233` | Exterior blue curtain is continuous from the visible rim to its soft lower fade. Broken white foam advances down the visible body. No duplicate curtain or flashing split seen. Upstream lake is hidden by the rim from this low camera. |
| Same run, view1 / station−2 | `RA_Moon_Waterfall_+02_+01_01` | `3.386248–5.697869` | Exterior fall remains continuous and reads against the purple rock/sky. Its changing fine streaks are visible, though less distinct at this more distant scale. Upstream lake is hidden by the rim. |
| `amber-art-20261102-102130-3018003`, view0 / station−1 | `Amber_VoluminousFallingWater_0_0` | `0.834321–3.148901` | Visible curtain has downward-moving broken foam and a soft lower fade. The existing cliff hides part of the upper reach from this angle, so this view does not establish the complete upstream lake/outlet. |
| Same run, view1 / station−2 | `Amber_VoluminousFallingWater_1_0` | `3.381622–5.692133` | Visible river, rounded spill and falling body read as one continuous flowing surface. No raised strip or separate sheet is visible at this exposed join; foam progresses downstream and down. |
| Same run, view2 / station−3 | `RA_Amber_SideCascade_RaisedFall_Layer0` | `5.927774–8.239278` | Broad teal curtain connects to the receiving pool. A bright packet visibly descends across the compared frames. No separated falling layer or flashing join seen. Upstream source lake lies behind the upper rim. |

Exact PNG names start with `view-0-station--001-`, `view-1-station--002-` and, for Amber, `view-2-station--003-`; inspected suffixes are `000.png`, `005.png`, `010.png`. Clips are `station--01-native.mp4`, `station--02-native.mp4`, and Amber `station--03-native.mp4` in the respective run folder.

Both reports record `nativeClock:true`, `savedMaterialsUnchanged:true`, `originalSceneRestored:true`, `shaderErrors:false`. The packages use their recorded `nativeTime` intervals without interpolation. Changing pixels alone are not treated as artistic acceptance or evidence that every lake is visible.

### Repaired Coral exterior bodies

Run `coral-art-20261102-104239-8167271` uses installed FBX SHA256 `37e99875187a9e0e67a6405a042dba3919e8b96377111fc54ec627eaa1ccb4d0`. Inspected frames `000`, `005`, `010` for both 24-frame views, at FOV 82 and with full scenery. Its report confirms native clock, unchanged saved materials, restored original scene and no shader errors.

| View | Target | Native interval (seconds) | Visual result |
|---|---|---|---|
| view0 / station−1 | `RA_Coral_Waterfall_+01_-01_01` | `0.841229–3.150884` | The narrow teal curtain now stands outside the red cliff from rim to lower fade. Moving white strands descend along one exposed body; the previous terrain burial is not visible. Exterior-body continuity passes. |
| view1 / station−2 | `RA_Coral_Waterfall_+01_-03_01` | `3.392720–5.703705` | The curtain is visibly outside the island's left rim, with a connected upper turn and soft lower end. Foam progresses down the exposed body; no second sheet or flashing gap appears. Exterior-body continuity passes. |

`ArtSource/RadioArchipelago/Islands/CoralVisibleDrains20261102/export_audit.json` additionally reports a finite exterior witness for all 30 body samples: each has at least one segment from a point 200 m away outside the entire rock AABB with no intervening Terrain/Cliffs intersection. It records 253 untouched imported objects with exact render signatures; the six named local shore/rock repairs and measured normal changes are explicit. The separate signed winding diagnostic is **not certified**: its farther overlapping/orientation-inconsistent rock hit and all signed counters remain in the audit. The finite witnesses and the actual exposed native images support exterior visibility; the non-certifying signed diagnostic is not relabelled as a pass.

## Complementary elevated lake/outlet review

Inspected original PNG frames `000` and `010` from the following additional native runs. Each view records 24 frames at FOV 82, full scenery, saved materials and an explicit elevated art camera. All three reports confirm native clock, unchanged saved materials, restored original scene and no shader errors. These views complement the low body cameras above.

| Run / view | Target | Native interval (seconds) | Observation / acceptance |
|---|---|---|---|
| `moon-lakes-20261102-103712-4289039`, view0 / station−1 | `RA_Moon_Waterfall_+00_+03_01` | `0.838632–3.145717` | The round lake narrows into its outlet at the same water level, with one shoreline outline and no raised ribbon/second stacked profile. Join passes. Straight flow striations are visible toward the near shore; this is a cosmetic shading pattern, not a raised surface. |
| Same run, view1 / station−2 | `RA_Moon_Waterfall_+02_+01_01` | `3.386771–5.699212` | Broad lake and the far outlet form one flat silhouette without an elevated strip. Join passes at this scale. Strong haze and camera distance limit detailed ripple/shore-quality judgment; linear flow shading remains visible. |
| `amber-lakes-20261102-103740-4869623`, view0 / station−1 | `Amber_VoluminousFallingWater_0_0` | `0.835097–3.151103` | Broad lake continues into its narrow far outlet without a raised overlay. Join passes. Directional striations are visible across the near part of the lake; haze softens distant detail. |
| Same run, view1 / station−2 | `Amber_VoluminousFallingWater_1_0` | `3.381031–5.690534` | Lake, stone-banked river and the far rounded spill form one continuous water route. No stacked strip or height step is visible. Join passes. |
| Same run, view2 / station−3 | `RA_Amber_SideCascade_RaisedFall_Layer0` | `5.926812–8.238002` | The source pool, curved spill and exposed curtain can be read together. No extra sheet sits over the source pool. Join passes; the appearance changes from quieter pool ripples to descending white strands over the same continuous shape. |
| `coral-lakes-20261102-104322-3306603`, view0 / station−1 | `RA_Coral_Waterfall_+01_-01_01` | `0.835055–3.146873` | The small round lake and its far drain meet at the same surface without an elevated strip. The repaired outward course can be read with the complementary exposed-body view. Join passes; the lake retains a moving ripple pattern. |
| Same run, view1 / station−2 | `RA_Coral_Waterfall_+01_-03_01` | `3.390172–5.699078` | The broad lake narrows into its far outlet without a stacked ribbon or visible height step. Join passes. Straight striations across the near part of the lake remain a cosmetic shading limitation. |

These run folders use the same `view-N-station--00M-000.png` / `010.png` and `station--0M-native.mp4` naming as the body runs. The visible directional lake pattern is retained as a cosmetic limitation; passing the requested geometric join does not claim user approval of every shading choice.

## Shader/source review

Compared `DancingCatsClone/Assets/RadioArchipelago/Art/AmberFlowWater.shader` with `Before/AmberFlowWater.shader`. Reviewed current shader SHA256: `a8e58d8a4f963f5af051ffbad19bbc60fe6247bd02290ba41c1b88daf227714a`.

- `_UseLakeSurface` defaults to 0. Vertex UV0 remains `TEXCOORD0`; the second source UV layer `LakeRegion` is read from vertex `TEXCOORD1`. With the option enabled, source UV1.x=1 gives exact `lake=0` on the retained falling body; UV1.x=0 selects lake appearance.
- Default 0 and body mask 1 skip every lake branch. Original displacement, downstream foam coordinates, original two ripple coordinates, falling-body shading, edge fade and alpha expressions remain intact. No actionable default/body regression found by static comparison.
- The latest ripple refinement blends only lake fragments toward lower-frequency, rotated ripple samples and a quieter lake tint. It adds no second geometric surface. Shared mouth vertices use the same UV0/normal and one displacement function; the joined source weld therefore cannot open a displacement gap there.
- Lake opacity and side-edge handling blend continuously into the original flow behavior. The complementary elevated native views above now establish the visible single-surface joins; source connectivity and conditional shader logic alone were not used to pass them.

## Limits

Four low side-camera views hide their broad upstream lake; their joins were accepted only after the complementary elevated views were inspected. The final repaired Coral acceptance uses the new exterior and elevated runs above. The earlier Coral station142 frame established only a flat lake join and is superseded for final exterior-body acceptance.

These art-camera checks do not assert that all falls are visible from the normal route camera. Existing route-camera occlusion failures must remain reported separately. No phone-performance or user-approval claim follows from this review. No Assets, Editor state or queues were changed for it.
