# Final native water direction review

Capture run: `20261101-234211-5093157` (final c565 source / e5595 shader, as pinned by integration). Four sets contain 14 actual-runtime frames each, exact frame times and independent imported-UV downstream projections. Reports pin all input PNG hashes. No source, shader, scene or queue actions were performed by this review.

**Confirmed:** downward packet motion at all three R3 lip angles and all three heights of the side waterfall. **No sample is confirmed upstream.** Four additional small/edge ROIs remain inconclusive; they are not counted as passes.

| Feature/sample | Result | Downstream / recognizable pairs | Median vs expected px/s |
| --- | --- | --- | --- |
| R3 roll 35 degrees | Downstream | 16 / 16 | 65.2 / 70.7 |
| R3 roll 48 degrees | Downstream | 14 / 14 | 70.0 / 74.0 |
| R3 roll 65 degrees | Downstream | 26 / 29 | 55.4 / 71.9 |
| Side curtain upper | Downstream | 43 / 43 | 38.9 / 40.8 |
| Side curtain middle | Downstream | 41 / 41 | 39.0 / 39.6 |
| Side curtain lower | Downstream | 42 / 42 | 32.6 / 36.7 |
| Flat stream near bank | Inconclusive | Standard patch outside mask; smaller patch 4 / 5 positive | 155.5 / 89.8 in smaller patch |
| Side roll 35 degrees | Inconclusive | Even 5x5 patch has 88% mask coverage, below 90% gate | — |
| Side roll 48 degrees | Inconclusive carrier identity | Smaller patch 8 / 8 positive, but motion far too slow | 3.75 / 30.2 |
| Side roll 65 degrees | Inconclusive | Smaller patch yields no recognizable pairs | — |

The R3 65-degree sample includes three upstream pair matches among 29 accepted pairs; its 26 downstream matches over 13 distinct starting frames meet the unchanged 75% sample-consistency rule. These pair matches are retained in the report, not suppressed.

## Method and limits

The tracker matches normalized local texture packets along the independently projected downstream tangent in both directions. Original confidence gates remain unchanged: NCC >= .65, improvement over stationary >= .03, separate-peak margin >= .035, travel >= .75px, visible-water coverage >= 90%, at least three recognizable pairs from three distinct starting frames, and at least 75% directional agreement.

One tracker defect was corrected before the final reports: the initial implementation compared the shoulder of the same broad correlation peak as if it were a separate competing peak. A known three-pixel single-packet fixture reproduced the false rejection, then passed after actual local peak grouping. Negative-direction, static, blank and repeating-texture fixtures continue to pass their expected outcomes. Initial reports are preserved as `*-direction-initial.json`; final standard reports are `*-direction.json`.

Smaller ROIs were predeclared from static mask geometry only, at exactly the supplied positions and tangents, with no offsets or selection based on direction. Their dimensions and reasoning are in `native-c565-footprints.json` under the design documentation and in `*-direction-roi.json`. The formal positive sign votes for flat stream and side48 are **not accepted as full carrier validation**: their measured rates do not convincingly identify the intended carrier. High correlation alone can follow shading or deformation.

The tiny side lip occupies only about 20px of image height at 540x960, and its category mask excludes the adjacent curtain. To resolve that remaining feature rigorously, use the same camera/FOV at 1080x1920 with predeclared source U=.25/.5/.75 positions at the same angles. A clear interior flat-stream source sample would likewise be preferable to shrinking the bank-edge patch further. No such new capture was requested or performed by this reviewer.

These are local visible-motion measurements. They do not prove every pixel or measure mobile performance. The complete native gameplay run and geometry/preservation checks are separate integration evidence.
