# Independent final waterfall visual review

**Result:** practical visual pass for the reported horizontal/vertical seam and apparently static falling curtain. This is an independent implementation review, **not the user's artistic approval** or a phone performance certification.

Final native run: `20261102-074355-3655427`.

Final evidence folder: `DancingCatsClone/Library/RadioArchipelago/AmberWinds/WaterfallVisual/after-20261102-074355-3655427/`.

Compared against the original native `before-20261102-073625-7573009` at the same stations 185/210, camera position/rotation, 540x960 resolution and 82-degree FOV. Evidence uses native Play Mode timestamps. No assets, scene, profile, camera settings or Unity queue were changed by this reviewer.

## Appearance and movement

- Both views now have a single continuous rounded flow surface. The old straight horizontal material boundary/cut between the stream and vertical curtain is absent in the inspected final images.
- White detail is now narrow, interrupted flowing threads and aerated heads, replacing the first replacement's large isolated white blobs. The result reads as a stylized waterfall. It is not a claim of physical fluid simulation or photorealism.
- Recognizable highlights/threads move downstream through the lip and down the falling body. They do not merely blink at fixed positions. Upper, middle and lower regions were inspected and given a supporting local-motion diagnostic below.
- All 24 station 185 frames and all 24 station 210 frames contain visible water, including both `000` frames. The original missing-first-render artifact is absent in this final set.

Representative images visually inspected:

- `station-185-000.png`, `station-185-012.png`, `station-185-023.png`: both canyon and raised side falls, including their lips and full visible curtains.
- `station-210-000.png`, `station-210-012.png`, `station-210-023.png`: close view of horizontal stream, rounded spill and falling body.

Timestamped sequence derivatives are `station-185-native.mp4`/`.gif` and `station-210-native.mp4`/`.gif` in the same folder. Original PNGs/report are the measurement inputs; the JSON pins all 48 PNG hashes.

## Supporting native-frame measurements

`motion-review.json` contains exact inputs, ROIs, per-pair native time differences, individual best shifts, correlations and caveats. A changing-pixel count alone was not treated as a visual pass.

| Region, station 185 | Downward | Upward | Subpixel/static | Median Y shift per adjacent frame | Median NCC |
|---|---:|---:|---:|---:|---:|
| left-upper | 23 | 0 | 0 | 7.00px | 0.974 |
| left-middle | 23 | 0 | 0 | 5.75px | 0.972 |
| left-lower | 23 | 0 | 0 | 3.50px | 0.938 |
| side-upper | 23 | 0 | 0 | 8.50px | 0.940 |
| side-middle | 19 | 0 | 4 | 5.75px | 0.883 |
| side-lower | 23 | 0 | 0 | 6.00px | 0.867 |

These are 23 adjacent comparisons per region, 138 in total: 134 downward, 0 upward, 4 subpixel/static. **The four subpixel/static matches in the side-middle region remain explicit**, not recategorized as downward or removed. No examined region produced an upstream best match. Manual image-space patches/tangents and local correlation are supporting evidence, not a new physical-projection/carrier-identity proof or a guarantee for every pixel.

Fixed water-presence ROIs measured 39,299–39,339 cyan pixels at 185 (first 39,309) and 28,447–28,500at 210 (first 28,486), with no absent-water frame. These counts establish surface presence only.

## First-render evidence and capture change

The first replacement run `after-20261102-073926-0612166` is retained, including its bad `station-185-000.png`: both falls were absent in the first submission, then present in all 23 subsequent frames. Geometry did not need another change for that symptom. The final capture performs **two actual native render submissions before saving each view**. No bad saved frame was silently removed from that earlier run.

The exact first-render mechanism was not logged. Asynchronous first-use shader compilation/resource readiness is a plausible explanation, not a confirmed universal cause. The two-draw warmup passed the present final 48 frames; it is not a readiness guarantee on every uncached/slower Editor. A later capture-tool refinement may check compiled-pass readiness explicitly.

## Static code review

The Amber-specific shader has one downstream-metre UV carrier, one material/render mode and a smooth geometric-slope appearance blend. There is no stream/curtain `_Fall` material switch. The connected grid eliminates the old overlapping three-sheet transition. No blocking shader or capture-lifecycle defect was found in this inspected revision. The shader intentionally approximates light and omits physical refraction/fluid simulation; it should be described as stylized flowing water.

The review report records no shader errors, real native clock, unchanged saved materials, original scene restored, 24 distinct timestamped frames per view. This review does not certify phone FPS, thermal behavior, transparency-overdraw cost, or full gameplay; the root agent's separate full native gameplay run is independent evidence.

## Tools and official references

Unity has suitable tools: URP shaders/Shader Graph plus conventional Particle System effects can create this mobile-oriented effect. Its built-in Water System is HDRP-specific; HDRP is not supported on Android/iOS. A pipeline migration was not required.

- [Unity 6.5 render pipeline feature comparison](https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/choose-a-render-pipeline/feature-comparison)
- [Shader Graph Tiling And Offset: UV scrolling over time](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Tiling-And-Offset-Node.html)
- [ShaderUtil.allowAsyncCompilation: variants compile on first required use](https://docs.unity3d.com/ja/current/ScriptReference/ShaderUtil-allowAsyncCompilation.html)
