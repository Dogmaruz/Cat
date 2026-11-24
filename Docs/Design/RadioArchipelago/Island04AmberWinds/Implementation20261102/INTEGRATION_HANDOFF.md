# Fourth-island integration handoff — 2026-11-02

The approved RadioTrio song and fourth island «Янтарные ветра» are integrated in Unity. The full native gameplay run passed on the final source. The Editor is left in clean Edit Mode on `Assets/RadioArchipelago/Scenes/AmberWindsPilot.unity`. No build was created.

## Delivered

- New scene, definition, prefab, materials, route/elevation/rotor/cloud contracts under `AmberWinds`; no earlier island scene, route, map, music, definition, prefab or material was replaced.
- Autumn yellow/orange/red scenery, three visibly rotating windmills, organic shore dressing, large separated clouds, stepped floating-island crossing, and a raised mountain waterfall beside the approach. Its upper/middle/base are visible from the normal fixed camera at155/170/185m. The formerly squared projected lip is a smooth roll; its animation seam was corrected through authored normals/colors without changing geometry or UVs.
- User-approved original RadioTrio,85.76s. Published catalog entry20005 and original native-PCM-based v12 generator output; no rejected WayoWa audio is used. Chart has317 full-size supports,18 holds and346 score contacts. The route-conditioned chart has97 moving platforms and preserves every scoring timestamp.
- Four visible menu/Atlas entries in order: LumiLai, TikoTa, NoriNo, RadioTrio. All80 other catalog records remain hidden; all25 previous custom raw song rows are preserved verbatim. Static and actual live menu navigation checks pass.

## Fixed interfaces and provenance

- Final imported/source FBX SHA256 `c5656b305295a74bffc93d0428b111a95e6768e0ef42b9cd956ec612ac88fc3a`;3,140,774triangles/278mesh renderers. All5 gameplay sidecars stayed byte-identical through the bounded shading correction.
- RadioTrio MP3 SHA256 `b3a7249d3f28984bf190d6498a2cadee7335d3bf61f6c22679e69e911d8759f6`; decoded PCM SHA256 `e238c81e97f83bd2e7c1889eb1f377769161dcb1f57fa96b04f1b8e08715c50e`,3,782,016frames/44,100Hz stereo. User acceptance explicitly forwarded by root before publish/binding.
- Shared water shader SHA256 `e5595cb7296dfb46056edf00727f290127ba817871a78869e7737291d9ea3337`. New Stream AND Waterfall use authored downstream-metre UVs, current4.2, flow.65, runtime clock−1. Fall fade is below the authored body. Old7 material files remain unchanged.
- Route774.995m, StartDistance10m, DistanceScale1.2, LateralScale1.8. Final takeoff675.028m/landing681.028m/end715.828m; exact straight begins662m. Cloud scale1.6 is new-island-only; old default1 retains earlier behavior.
- Eleven staged C# files match their installed versions. Existing IslandPilotPlayReview is in `RadioArchipelago/Editor`, not `Clone/Editor`. The bridge remains `Clone/Editor/MusicalChartEditorSession.cs`. Root owns the precise RadioTrio0-or1 exceptions in old Coral/Moon guards and shared cloud-size fields; water agent owns the shader and direction analyzer.

## Native verification

- `FinalAcceptance/full-native-play-review.json`: run20261101-234737-711, full unsimplified85.76s audio/map, InputSystem pointer only;346/346 score contacts,18 required holds,97 moving tiles,2 big jumps including finish, win,20 signal sparks collected above ground, complete departure. Profile/input restored, errors[], zero actual geometry clips, zero actor or next-landing offscreen frames. Fixed-camera distance error below8µm. Instrumented Editor frame timings are not phone performance measurements.
- `terrain-clearance.json`:55,488 corridor samples,64,697 tile samples,33,915 structure samples, zero violations;2,788 actual finish-floor samples. Both earlier-island preservation guards pass. Derived map/elevation hashes remained identical through the final source shading correction.
- `imported-seam-continuity.json`:15 matched endpoint pairs across3joints have normaldot1, color error0 and position error0; actual frozen vertex animation maximum endpoint deviation about15.73µm. Real alpha material is retained; the diagnostic opaque clone was never saved.
- `native-water-clock.json`: four sets of14 actual-time frames, exact timestamps and one current runId20261101-234211-5093157; preview clock−1 and original scene restored. GIFs use those frame intervals. Occlusion-aware `side-cascade-visibility.json` verifies full-height waterfall visibility from the normal camera.
- `direction-aggregate.md/json`: recognizable packets confirm downstream motion at all3 R3 roll slopes35/48/65 degrees and new side-curtain upper/middle/lower. No sample is confirmed upstream. Four additional flat/small-lip ROIs remain explicitly inconclusive because of mask coverage or carrier-speed/texture ambiguity. They are not counted as passes; see the standard and smaller-ROI reports. Six tracker fixtures pass, and the two principal native reports were independently reproduced after review.
- `root-preservation.json`: root audit404 byte-identical files/8 authorized changes/0 unexpected changes within412-file baseline; all25 old raw song rows preserved. Parent will perform the final audit after queue handoff.

## Evidence and operation

`FinalAcceptance/acceptance-summary.json` is the compact machine-readable handoff. Adjacent files include final actual route-camera views, representative actual gameplay frames, full reports, current source hashes, and temporal GIFs. PreviousNativeRevision folders are explicitly rejected/prior revisions, not final evidence.

Queue actions: `amber-open`, `amber-checks`, `amber-capture`, `amber-playtest`; chart-free `amber-scenery-create`/`amber-scenery-capture`; transient `amber-scenery-water-live`; cloned-only `amber-roll-alpha-compare`; `island-menu-playtest`. The scenery paths use an unsaved source-backed definition with Chart=null and no song binding. The gameplay action runs the actual approved scene. No further paid generation, build, commit or push was made by this task.
