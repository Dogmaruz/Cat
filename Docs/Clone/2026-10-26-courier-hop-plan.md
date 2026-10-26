# Alternating courier hop implementation plan

**Goal:** Make ordinary tile-to-tile movement read as alternating left-foot and right-foot hops from the actual gameplay camera, with visible squash/stretch during the complete flight. No player builds.

**Design:** Keep the session's actor root, colliders, musical clock, tile contact and flight trajectory authoritative. Each `PlayJump` advances one presentation-side sequence; `PlayLanding`, progress samples and support checkpoints do not. Lean, modest torso yaw, lateral visual weight and independent feet express the active side. A phase-shaped, volume-preserving deformation follows actual normalized flight progress rather than a brief generic takeoff pulse. Preserve the existing continuous support bounce, alternating full supported turns, long-flight arm strokes and secondary-chain inertia.

**Approach:** Extend `CourierMotion` using existing explicit rig controls; no model re-export, Animator rewrite, new dependency or generated animation clips. Compare old/new captures with identical camera/framing and actual imported couriers. Original cat curves are read-only reference evidence; alternating-foot choreography is our requested design.

**Constraints:** Existing checkout `feature/radio-archipelago`, baseline `bcbbd81`. Preserve all original and custom charts/audio, models/materials, scenes, calibration and the 512 audio buffer. Exclude the four pre-existing user modifications from commits. No network generation, package changes, singletons, player builds or animation-authoritative hit events.

- [x] Capture current source, protected asset hashes and all-three-courier baseline poses at gameplay scale.
- [x] Add actual-transform counterexamples for alternating lean/feet, visible in-flight shape changes, root isolation, pause/reset/rebind and frame rates. Record RED before changing the runtime.
- [x] Implement alternating hop/foot poses and stronger phase-shaped volume-preserving body deformation. Keep exact musical/root behavior and existing supported-turn sequence.
- [x] Run actual Unity compilation, new and existing motion/accessory/rig checks. Render matched candidate pose sheets, inspect all couriers and refine any clipping or unreadable motion.
- [x] Run full real-contact gameplay in Unity, including short/long flights and supported turns, pause/resume/result/retry. No builds.
- [ ] Review the scoped code and final visuals, verify protected assets, document measured changes, commit/push and leave a useful Unity preview ready.
