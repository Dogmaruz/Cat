# Emerald island Android test

APK: `Builds/Android/EmeraldIslandPilot.apk`.

- Unity 6000.5.5f1, ARM64 / IL2CPP, Android 8.0 (API 26) or newer.
- Package: `com.radioarchipelago.emeraldislandpilot`, version 0.1.0 (1).
- One scene: EmeraldIslandPilot; one full song: Always Remember Meow.
- GLES3 / URP Forward; SSAO, post-processing, bloom and platform emission off.
- Courier scale 0.64, broad white Hold overlay opacity 0.24; central line retains native Alpha/fade.
- 297,274,921 APK bytes (283.50 MiB), V2 signature verified, ZIP CRC verified.
- SHA256: `7896f19864c31fbb12d947f1127aaed7ba242960c275742597d6aa7a372e0b85`.

BuildReport reports Succeeded, zero errors and three TextMeshPro IL2CPP splitting warnings. Its summary totalSize includes build artifacts; use the validation JSON's actual file byte count for the APK size. The packed source audit found the approved song and 41 effects/other clips, no excluded music, and all three required island/Hold shaders. The source project stayed on Windows; only a marked staging copy was pruned/configured for Android. No physical Android device was connected, so installation, touch, GPU output and frame rate on a phone remain untested.

The adjacent APK report, validation, aapt and signature files are copied from the final frozen build. The APK itself remains in Builds/Android rather than being duplicated in this documentation folder. Copy it to a compatible Android phone, install and press ИГРАТЬ.

Final full-song Editor review `20261027-175025-418` passed: 8,045 frames, score 60,031, both route seams, all 12 moving tiles, holds, glides, big jumps and pause/resume. Actual courier scale 0.64, broad band opacity <=0.24, no post-processing, no stale Hold strips or extra exit discs, 60 mapped finish coins and 20 real collections were verified. No runtime errors; input and saved preferences restored. Latest actual GameView frames and `play-review.json` are adjacent.

`scale-before-fix.json` records the cold-launch scale regression before its correction. `capture-clock-sampling-before-fix.json` records the automatic review measuring DSP after a blocking framebuffer readback; the review now samples chart and DSP clocks together before captures, with the same 150 ms assertion limit. This changes Editor instrumentation only, not the APK's gameplay clock.
