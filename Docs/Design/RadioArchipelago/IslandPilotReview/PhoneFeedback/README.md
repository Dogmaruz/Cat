# Phone recording corrections — 2026-10-27

Input recording: `C:/Users/Derek/Videos/Запись 2026-10-27 211113.mp4`, 20.14 seconds, 302×502, H.264/30 fps. Frames/contact sheets are evidence extracted from the supplied recording, not a new device run.

Confirmed causes and changes:

- Round bodies previously overlapped at rapid subdivisions. Native-sized preferred radius is now 0.65 m; world travel scale 1.18 and reachable lateral replies leave 13 close pairs requiring smaller quick-note bodies. Static closed-route checks: 309 landings, zero adjusted intersections, minimum radius 0.416717 m. Future-lap aliases are excluded. Mean lateral step rose from 0.4679 to 0.5482 m. Native colliders and musical times are preserved; the smallest bodies therefore retain a forgiving original hit area.
- The transverse Hold bars were two long additive mesh flashes, Light1/Light2, under Effect_Point_*. A/B captures in HoldBeforeFixProbe show that disabling those flashes removes the bars while the ride centerline and note remain. Only their island mirrors for Hold/HoldPoint are excluded. The fixture verifies two exclusions, four retained emitters and unchanged normal-jump effects.
- Actual native note particles using Lizi_Yinfu are rendered at twice their previous size. Emission/rewards are unchanged: normal Jump notes begin on the fifth consecutive centered Perfect; Great resets that series. Collectible checkpoints have separate effects.
- FollowMid's Sphere marker is a normal play-on-awake looping Glow particle, not a flat extra platform. StopParticles during configuration had stopped it permanently. Configuration now restarts only those intended idle loops, and native collection hides the marker. The first Follow passage occurs at audio 64.150748–65.607438 s (about 41%); the second at 123.256417–124.683175 s. Before these passages, ordinary round plates do not show a collectible marker.
- Touch drag gain changed from a full lane span taking 45.5% screen width to 24%. Clamping and reanchoring at either edge remove accumulated overscroll travel on reversal. Mouse gain is unchanged; elapsed-time steering remains stable at 20/30/60/120 fps.

Validation:

- Final real full-song Editor review `20261027-184125-800` passed: 7,666 observed frames, score 60,031, both route seams, all 12 moving tiles, holds/glides/big jumps, pause/resume and 20 authoritative finish coin collections. Zero runtime errors, overlapping visible round bodies, stale Hold strips or extra exit discs; 22 mapped collectible marker sources, up to four visible, ten live native note particles and 56 excluded Hold flash sources. Preferences/input restored.
- Real Touchscreen input integration passed at 540 px: +8% drag -> +1 lane metre; edge -> 1.5, immediate 2% reversal -> 1.25; tap anchored and input/controller/actor/clock state restored. This synchronous input-method probe is not physical-phone gameplay.
- `minLandingRadius` in the final play report becomes zero after all visible round supports expire at the finish; the static spacing report and active-course observations establish the 0.416717 m minimum. No zero-radius landing surface is authored.
- Editor timing includes framebuffer readbacks and concurrent Android compilation. It does not establish phone FPS or touch feel. The MP3's BPM/pitch and DSP hit accents are unchanged; 1.18 changes movement through the world.

The aborted diagnostic review was intentionally stopped after A/B isolation; it is not the qualification pass. `final-play-review.json` is the final completed pass. Original maps/music and the original game scene remain protected.

Android handoff: `Builds/Android/EmeraldIslandPilot-v2.apk`, version 0.1.1/code 2, 297,284,641 bytes (283.51 MiB). SHA256 `e4ff026456e39fe0c0c30903cba507efb8ea17e06a46ad483141b5fc12b614fa`. Build succeeded with zero errors and three existing TMP IL2CPP splitting warnings. All 14 APK checks passed: ARM64, Android 8.0+, correct version/package, one scene, approved song, required shaders, signature and ZIP integrity. The signing certificate matches v1, so the APK is compatible with installing over that version. V1 remains unchanged. Staging checked all 6,777 source files and 148 runtime-code/shader hashes against the approved snapshot. No physical-device execution was performed.
