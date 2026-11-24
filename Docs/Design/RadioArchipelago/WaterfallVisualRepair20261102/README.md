# Waterfall visual repair — 2 November 2026

The user rejected the previous Amber waterfalls: a visible stream/curtain join and vertical water that appeared static. The previous numerical motion checks did **not** establish convincing visual quality.

## Cause and correction

The old opaque stream ended where three narrower transparent curtains began. Each curtain reset its across-stream UV phase. Even matching selected vertex pairs did not make this composited material boundary continuous. Long, low-contrast vertical detail obscured the animated movement.

The replacement uses three connected ribbons (one per cascade), one material/render mode across each complete stream, rounded lip and curtain, smooth physical normals and metric downstream UVs. Two secondary continuous curtains per cascade were removed. The formerly square lower spill is rounded to radius1.5m; the existing R3 and raised R1.5 lips remain. A separate Amber shader advects broken foam threads and recognisable packets downstream at4.8m/s. It uses ordinary URP rendering, two shared normal-map samples, no screen-space refraction, bloom, extra lights or fluid simulation. Two bounded conventional particle emitters at the raised cascade's actual receiving basin provide droplets and soft mist (maximum48+24particles).

The shared `CoralWater.shader` and all existing earlier-island materials remain byte-identical. Their water implementation has not been replaced. The legacy split-material Amber seam probe is historical; the source-scenery water-clock entry now routes to the new native visual review.

## Evidence

- `Before/`: two unchanged-camera native clips of the rejected version. The previous cold first submission is retained, not silently deleted.
- `After/`: same stations185/210, actual portrait route camera/FOV82,48native timestamped PNG frames packaged into two variable-timestamp MP4 clips. Full scenery, no actor/UI, shader-time override, image interpolation or saved-material mutation. Two actual uncaptured renderer warmup submissions precede each view. The first warmup artifact was not geometry occlusion: all subsequent23frames of that first experimental run contained both waterfalls.
- Final visual run: `20261102-074355-3655427`, native time.9267–5.8136s; each view spans at least2.3s. Human visual review is required in addition to capture completion.
- `FINAL_VISUAL_REVIEW.md` / `motion-review.json`: independent appearance and upper/middle/lower downstream checks, including subpixel matches rather than calling them motion.
- `preservation.json`:764pre-existing protected files,761byte-identical, exactly3authorised changes (Amber FBX, prefab and waterfall material), zero unexpected changes. All existing scenes, music, charts, routes, elevations, old island assets and project settings unchanged.
- Source and independent FBX-reimport audit: `ArtSource/RadioArchipelago/Islands/AmberWindsPilot/WaterfallRework20261102/`. All270non-target objects retain exact render signatures. Five gameplay sidecars retain exact bytes. Three single-material connected water grids, no reversed/nonunit normals, +V downward throughout the curtains.

Final FBX SHA256: `18cfb754ba06d63a01a95c069ff5d7dbc5142e7415b42c6f19bf48187e45305f`.

## Native checks and limits

`amber-checks` passed after import: actual route, terrain clearance, chart timing, moving supports, shaders, rooftop nodes, clouds and earlier-island preservation. Full native gameplay run20261102-074617-863 passed:85.76seconds,346/346score contacts,20sparks,0errors/actualGeometryClips/offscreen actor or next-landing frames, input/profile restored. See `native-play-review.json` and `After/gameplay-waterfalls.png`. Fresh final inspect:AmberWindsPilot,Menu,playingfalse,compilingfalse,sceneDirtyfalse. No Player builds, package/pipeline changes, new music generations, commits or pushes.

This is a stylised moving-water effect, not a physical-fluid simulation. Its mobile-device GPU/frame-rate cost remains unmeasured. Editor-capture timing is not phone performance. A URP camera-stack warning was observed during gameplay; no water shader compilation errors were observed.

Suitable built-in tools do exist: URP shaders/Shader Graph plus ordinary Particle Systems. HDRP's Water System is not a reason to migrate this Android/iOS project: HDRP does not target those platforms. See Unity's [render-pipeline feature comparison](https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/choose-a-render-pipeline/feature-comparison) and [UV tiling/offset node](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Tiling-And-Offset-Node.html).
