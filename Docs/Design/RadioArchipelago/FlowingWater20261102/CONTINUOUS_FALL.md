# Continuous authored stream, bank roll and waterfall

This is a narrow correction after the shared-water pass. Native compilation and final rendering are delegated to the integration worker; no Unity queue actions were performed for this correction.

## Root cause and change

The normal-derived fall blend previously interpolated *coordinates* between downstream `V - time * CurrentSpeed` and `worldY + time * 10`. Both endpoints move downstream on their own meshes, but their coordinate axes have opposite signs. The first normal-map layer has zero temporal phase near a 48.2563-degree tilt on the actual R3 bank roll. Other layers/foam could still move; this did not mean every pixel froze.

Only `_UseFlowUV=1` now selects one carrier for every slope. The same downstream-metre coordinate drives both ripple samples and foam through the horizontal stream, bank roll and curtain. The fall weight blends normal strength and foam appearance instead of advected coordinates. Both UV tangents are derived throughout the roll and perturb the actual geometric normal, with no `fall=.5` basis cutoff. The UV path also omits the extra lateral time drift, so the same carrier can cross a material seam without phase reset.

The `_UseFlowUV=0` branch retains its previous coordinate expressions, normal calculation, foam decisions and drift. Existing material files have not been resaved. Seven earlier water/foam material SHA-256 values are unchanged; see `continuous-fall-material-preservation.json`.

## Material/source contract

For new Amber **Stream and Waterfall** materials:

| Property | Value |
| --- | --- |
| `_UseFlowUV` | `1` |
| `_CurrentSpeed` | `4.2` |
| `_FlowSpeed` | `.65` |
| `_WaterPreviewTime` | `-1` |
| `_Fall` | Stream `0`, Waterfall `1` |
| `_FallFadeRange` | Waterfall `(-90,-80,0,0)` |

Actual downstream speed is **2.73 metres per real second**. UV0.U runs from 0 to 1 across the stream and UV0.V increases in physical metres downstream, with a continuing origin through roll and curtain. U alignment and seam width must also agree. A new cascade with a separate source can have its own V origin, but its joined stream/roll/curtain must agree with each other.

`_FallFadeRange` is a new optional Vector. X is the world height below which the waterfall is invisible; Y is the world height above which it is fully visible. Its default `(-55,-33,0,0)` reproduces the old three-island fade. New Amber uses `(-90,-80,0,0)` so the long curtain extending below -55 is not cut off. This changes opacity visibility, not the movement clock. Existing fall alpha/edge/blend render states otherwise stay intact.

## Offline directional check

Run:

```powershell
node Docs/Design/RadioArchipelago/FlowingWater20261102/check-continuous-fall.cjs
```

The small script evaluates the scalar expression extracted from the production HLSL `AuthoredFlowCoordinate` against an independent physical fixture: horizontal bank, R3 quarter-circle and vertical curtain. It tracks the same carrier identity forward by 0.6825m over .25 real seconds at the former cancellation angle, on a horizontal segment, on a vertical segment and crossing the roll-curtain seam. It also reproduces the original zero temporal derivative. The test was first run before the change and failed because the continuous production carrier did not exist, then passed after the change. Result: `continuous-fall-direction.json`.

After integration updates the new saved Amber materials, this optional read-only check also verifies their real-clock values and matching seam phase:

```powershell
node Docs/Design/RadioArchipelago/FlowingWater20261102/check-continuous-fall.cjs --materials
```

The offline check establishes the advection contract, not HLSL compilation, actual mesh UV correctness, bank geometry, or visible rendered direction. Final native proof must use the actual imported Amber meshes and runtime clock, particularly the middle of the rounded lip, both material joins, and the full curtain bottom. No phone performance or full-song validation is claimed here.

Shader freeze SHA-256: `e5595cb7296dfb46056edf00727f290127ba817871a78869e7737291d9ea3337`.

## Native-frame direction analysis

`check-rendered-water-direction.py` consumes the integration worker's actual captured PNGs, exact per-frame runtime times, target-water visibility mask and independently projected downstream tangents. It compares normalized local texture patches translated in both directions, across one- to four-frame intervals. It rejects untextured water, static/subpixel maxima, weak correlations, repetitive/competing peaks, insufficient visible-water coverage and search-boundary maxima. A sample needs recognizable translations from at least three distinct starting frames, with at least 75% directional agreement. Results can be **confirmed downstream**, **confirmed upstream** or **ambiguous**; ambiguity never counts as a pass. File hashes bind every report to its input frames.

This is local projected-motion evidence. Specular appearance changes, foam blending, repeated narrow filaments, perspective/curvature over the patch and occlusion can make a physically correct flow untrackable. It does not replace visual review, nor prove camera/player performance. A passing direction check therefore needs the corresponding animation viewed as well.

The script uses existing numpy/scipy/Pillow dependencies; it installs nothing. Its synthetic self-test first failed with the missing tracker, then passed known positive/negative translations and rejected static, blank and periodically ambiguous fixtures.

```powershell
& 'C:/Users/Derek/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' `
  Docs/Design/RadioArchipelago/FlowingWater20261102/check-rendered-water-direction.py `
  --deps DancingCatsClone/Library/OriginalChartCalibration/v4/ml-deps `
  DancingCatsClone/Library/RadioArchipelago/AmberWinds/WaterNative/side-roll-clock.json `
  --output Docs/Design/RadioArchipelago/FlowingWater20261102/side-roll-direction.json
```

Input `*-clock.json` needs `feature`, `frameTimes`, and `flowSamples` containing `label`, top-left PNG `x/y`, projected `tangentX/tangentY` and optional `expectedPixelsPerSecond`. Defaults are `{feature}-flow-00.png` through `13.png` and `{feature}-visible-mask.png`. Exact frame times are mandatory, not interpolated from first/last times. Final native reports are generated only after the updated source and shader are imported together; older captures are not acceptance evidence for this correction.
