# Camera jitter: independent review of 2026-10-28 recording

Source: `C:/Users/Derek/Videos/Запись 2026-10-28.mp4`.
Metadata: H.264, 514×880 pixels, declared 30.0 fps, 38.49 s container duration. Decoded video yielded 1145 frames (last index 1144, at 38.133 s on the 30 fps frame grid). Audio/container duration may extend past the decoded video.

## Observations

The camera really does wobble sideways during the long straight cyan hold near the end of play. This is visible in fixed background trees/rocks, so it is more than the animated character moving on screen.

The clearest interval is 29.833–30.700 s, with the score remaining 120. The fixed scenery reverses its horizontal screen-motion direction seven times in that interval. Median fixed-tree feature displacements between adjacent video frames include:

| Time (s) | Frame | Tree-region x shift (pixels) |
| --- | --- | --- |
| 29.867 | 896 | +5.011 |
| 30.000 | 900 | −1.951 |
| 30.133 | 904 | +3.551 |
| 30.267 | 908 | −3.003 |
| 30.333 | 910 | +3.499 |
| 30.567 | 917 | −6.263 |
| 30.600 | 918 | +2.529 |

This fixed scenery moves back and forth while its vertical motion continues mainly in the same direction. Paused/repeated frames cannot explain the direction reversals. See `hold_static_scenery_wobble.png`, `hold_transition.jpg`, and `hold_tree_motion.csv`.

There is also a strong reframe at the end of that hold. At 31.333 s (frame 940), the full-scene affine estimate at screen center changes by −21.30 px x, +5.94 px y and approximately +3.01% scale in one video frame. The previous changed frame at 31.300 s is −6.47/+4.46 px and the next at 31.367 s is −8.53/+3.48 px. The 31.333 s fit contains 219 inlier static-scene feature tracks. See `event_31s.png` and `event_0936...event_0945` exact PNGs. This is a framing discontinuity around a hold exit, distinct from the recurring wobble during the hold.

The recording also contains uneven scene cadence. On 2.5–35.5 s, 265 of 990 adjacent pairs have effectively unchanged scene geometry. In 28–32 s, 36 of 120 pairs do. Around 29.567–29.667 s one scene image persists for about 133 ms; 31.200–31.267 s shows about 100 ms. This is a property of the supplied recording. It does not establish Unity's render FPS, CPU/GPU performance, or whether duplicated/stale imagery originates in the game or recording pipeline. A stricter whole-frame near-duplicate test (mean absolute grayscale difference <0.15) flags 197/990 pairs; its lower count includes differences from UI, particles, and codec noise.

## Method and limits

Read-only offline video analysis; no Unity operation or runtime source edit. The full-scene estimate uses OpenCV sparse Lucas–Kanade feature tracks, excludes HUD and player-area pixels, and fits a 2D partial affine transform with RANSAC. `motion.csv` contains per-frame results. The independent long-hold check measures median sparse-feature displacement only inside a distant-tree/rock mask (source pixels x70:390, y125:315), fully outside HUD and character, as recorded in `hold_tree_motion.csv`.

A 2D affine scale is only an image-registration approximation for a 3D perspective scene; it is not a measurement of the camera's FOV or world-space position. The sideways sign reversals are directly supported by static feature tracks. Linking them specifically to animated renderer bounds or future-target switching requires runtime/code evidence from the main investigation.

## Files

- `metadata.json`: stream metadata and decoded frame count.
- `contact_1.jpg` through `contact_4.jpg`: one-second overview.
- `tail_*.jpg`: source-size frames every 0.1 s from 25 s.
- `hold_start.jpg`, `hold_transition.jpg`, `hold_exit.jpg`: 0.1 s detailed contact sheets.
- `hold_static_scenery_wobble.png`: independent tree-region horizontal/vertical motion.
- `event_31s.png`: every-frame contact sheet of 31.200–31.500 s.
- `event_*.png`: exact source-resolution frames around the strong reframe.
- `motion.csv`, `hold_tree_motion.csv`: measured image motion.
- `tail_motion_plot.png`: approximate full-scene image-motion traces.
