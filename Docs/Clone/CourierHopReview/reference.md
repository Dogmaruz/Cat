# Courier hop reference review

Read-only investigation, 2026-10-26. Only this Library report is written. No product asset, model, music, map, project setting, Editor queue, build, package, download, or Git operation was changed by this worker.

## Evidence and identity

Original evidence is previously recovered static source from the signed Android build, not a fresh observation of the original running game. The cached Ghidra file declares the ELF RVA and IL2CPP metadata for each body. The asset YAML is an AssetRipper export. No new REA session or runtime was launched.

- Original clip: `C:\Users\Derek\Documents\Codex\2026-10-20\dancing-cat\work\clone-research\recovered\unity-project-complete\ExportedProject\Assets\AnimationClip\Jump.anim`, SHA256 `8A8EA0C75B4566089B676FA528A492342AA58B8796C3E32F72F062BBA93112E4`.
- Clip GUID: `11d2f3c33740037458da9edb47bb2f44`, from neighboring Jump.anim.meta.
- Original controller: same exported Assets root, `AnimatorController\Cat01.controller`, SHA256 `CBBBB260F8A2C8C5495581D8C2A2FEED309AA05E771A2BE5BA206E44247A2707`.
- Native evidence: `C:\Users\Derek\Documents\Codex\2026-10-20\dancing-cat\work\clone-research\reference-behavior\focused-native-annotated.c`, SHA256 `540D7FB5393F6922A22953A05BDEF9C1857FFD904A12A47D37695F3B99980DB1`.
- IL2CPP overload signatures: same reference-behavior root, `tools\Il2CppDumper\script.json:718084-718104` and `dump.cs:540575-540585`.
- Current comparison: `C:\Users\Derek\Games_develop\Cat\DancingCatsClone\Assets\RadioArchipelago\Runtime\CourierMotion.cs`, as read before parent implementation. The comparison below describes that initial read, not a later edited file.

## Direct original scale measurements

`Jump.anim:908-1014` contains the entire scale curve for `Animation/Position/Rotation/Scale`. Exact authored key values:

| Clip time, ms | X and Z | Y |
|---:|---:|---:|
| 0 | 1.00 | 1.00 |
| 83.333 | 0.90 | 1.10 |
| 183.333 | 1.10 | 0.90 |
| 333.333 | 0.95 | 1.10 |
| 416.667 | 1.10 | 0.90 |
| 500 | 1.00 | 1.00 |
| 583.333 | 0.90 | 1.10 |
| 683.333 | 1.10 | 0.90 |
| 833.333 | 0.95 | 1.10 |
| 916.667 | 1.10 | 0.90 |
| 1000 | 1.00 | 1.00 |

The keys are unweighted (`weightedMode: 0`); most endpoint tangents are effectively zero, so a smooth eased traversal between extrema is appropriate. The 500 ms key is a pass through rest with Y tangent about +1.2/second rather than a stop. Do not flatten that crossing by adding a held rest pose.

`Jump.anim:2326-2333` says start=0, stop=1, LoopTime=1. Its sample rate is 60. The numeric scale motion has a repeating half-second motif: two stretches and two compressions in each half-second. The first stretch and the second have different width values even though both reach Y=1.1. This is sustained airborne deformation, not only one launch stretch and one contact squash.

The original is not volume preserving: at Y=1.1 its X/Z can be .9 (volume=.891); at Y=.9 its X/Z=1.1 (volume=1.089). Current `width=1/sqrt(1+stretch)` gives only .9535 width for Y=1.1, and 1.0541 for Y=.9. Therefore at the same Y amplitude, current width silhouette changes are roughly half the original. Strict volume preservation is a courier design choice, not an original constraint.

## Original clip clock and ordinary contact

- `Cat01.controller:238-263`: Jump state has speed=1, no outgoing transitions, speed/time parameters inactive, and references the exact Jump GUID above.
- `focused-native-annotated.c:1-53`, `Ball.CallBeginJump`, ELF RVA `0x14359d4`: guards against already-jumping gameplay state, invokes BeginJump, then calls `UnityEngine_Animator__Play(animator, "Jump", 0)` when the animator exists and ball state is nonzero. There is no duration/speed scaling formula or explicit normalized-time argument here. The final 0 in this native three-argument display is MethodInfo for the string-only overload, not a request to start at normalized time zero; IL2CPP metadata separately identifies Play(string) at RVA `0x2A2727C`.
- The overload is consistent with preserving animation progress when Jump is requested while the same Animator state is active. The local recovered-animation implementation explicitly retains that interpretation at `Assets\Clone\Runtime\OriginalRoleMotion.cs:144-149`: `animator.Play(state, 0, float.NegativeInfinity)`. This is corroborating reconstruction code, not fresh native execution proof. Do not claim a measured restart at each tile from this evidence.
- `focused-native-annotated.c:58-113`, `Ball.CallEndJump`, RVA `0x1435bdc`: returns unless descending; invokes reset/end helpers, moves JumpDown=5 to state6, clears JumpUtil, and has no Animator.Play call. `ifContinue=true` skips the optional non-continuation event. No fixed support dwell or landing clip is shown in this body.
- `focused-native-annotated.c:911-1045`, `Jump.TriggerEnter`, RVA `0x143af98`: a descending ordinary contact calls CallEndJump(tileY,true). A still-rising state returns. The body then constructs and calls the next BeginJump immediately. The root sphere radius is .25 and tile top contact can precede a purely longitudinal p=1; there is no source basis here for waiting an added 60 ms before deforming.
- `focused-native-annotated.c:573-622`, `Ball.Jumping`, RVA `0x1437124`: longitudinal percent is clamped below zero, warped by the authored trajectory curve while <=1, and passed to JumpUtil height. It does not drive the Animator clip phase. The gameplay trajectory and clip clock are separate inputs.

Thus a .30 s hop and a 1.20 s hop are not documented to replay the same complete 1 s body sequence at different speeds. Current CourierMotion resets elapsed=0 in PlayJump and combines a p-normalized launch Gaussian with an elapsed-time ripple. That is an adaptation. A new per-hop left/right gesture can deliberately remain normalized to the real flight for readable landings while the sustained deformation keeps a seconds-based rhythm.

## What the original does not establish

- `Jump.anim:333-357`: Animation/Position/Rotation is zero rotation at t=0 and t=1.
- `Jump.anim:358-407`: both Jiao01_AN and Jiao02_AN foot rotation curves are constant zero.
- `Jump.anim:858-907`: each foot position curve is constant across the clip.
- The shoulders are mirrored, simultaneous strokes: left Z=50 at0, -25 at133.333 ms, 50 at250 ms; right has opposite signed angles (`Jump.anim:16-192`). This is approximately four strokes/second, not alternate-foot locomotion.

The user's requested left-foot/left-lean, then right-foot/right-lean animation is new authored behavior. It should not be presented as recovered original alternating steps. Original evidence supports the amount and persistence of body deformation and the independent clip clock.

## Initial CourierMotion diagnosis

At the initial read, Jump sets both feet to `sin(pi*p)*.05`, so there is no left/right jump identity. Ordinary flights have only ambient roll of about1.3 degrees; their extra2.5 degree sine roll is multiplied by longFlight, which is zero below .6 s. The courier is therefore nearly bilaterally symmetric from the gameplay rear camera.

Its stretch target is a single +.09 Gaussian centered at p=.13 with width .17, a terminal -.025 ramp after p=.72, and a .045*sin(elapsed*11)*sin(pi*p) ripple. Body scale then follows a19/second exponential response. These inputs do not reproduce the reference's repeated +.10/- .10 traversal.

A scalar numerical evaluation of that exact initial stretch equation, 120 Hz substeps, rest initial stretch=0, no landing overlay, gives:

| Flight duration | Peak filtered Y | Minimum filtered Y | Minimum occurs |
|---:|---:|---:|---:|
| .30 s | 1.0667 | .9972 | at end |
| .45 s | 1.0813 | .9752 | at end |
| .75 s | 1.0958 | .9638 | .4667 s |
| 1.20 s | 1.1003 | .9720 | 1.0583 s |

These are equation-model calculations, not live Unity observations. Externally timed p held constant per rendered frame and inherited previous-hop stretch can shift them. They show the main imbalance: a normal .30 s hop hardly compresses at all before its landing overlay, and even longer hops do not reach the original .90 airborne compression.

The initial working file's ImpactCompression is already `.16*Gaussian(center=.035,width=.05) - .055*Gaussian(center=.14,width=.055)`, landingStrength scaled. It is not the earlier small contact-only version. At contact t=0 this already contributes about.098 compression; it peaks around35 ms, so a claimed60 ms delayed onset would be inaccurate. Flight readability still requires its own scale motion.

## Numerical recommendations for the new requested gesture

These are design recommendations for the courier, not original measurements:

1. Give each ordinary PlayJump a stable alternating support side. Reset to the same first side on ResetMotion; do not derive side from tile X or drag. Keep supported rides and their existing rotation identity separate.
2. Use a visible signed10-14 degree body roll throughout most of the hop, rising over roughly p=0-.18, held/enveloped through p=.75, easing toward the selected landing side at p=.9-1. A6-10 degree pitch swing and a3-5 degree opposite head roll help the rear silhouette. If a small yaw is desired,4-6 degrees is enough; it is new design, not original drag-facing behavior.
3. For the selected supporting foot, keep lift near0-.025 at push-off/landing and at most.03-.05 in flight; tuck the opposite foot .12-.16 local presentation units around the apex. The initial symmetric .05 lift is too small to read as one-foot support. Match it with opposite shoulder emphasis of about8-12 degrees without removing the existing four-Hz mirrored stroke.
4. Sustain at least approximately +/- .10 Y deformation through flight; +/- .12 can compensate for the courier's broad torso and distant camera. Sample the recovered seconds-based curve directly or use a deliberate repeated4 Hz eased breathing curve. Use a dedicated45-60/second scale response or a directly eased analytic curve; the19/second body response noticeably erases100 ms extrema. At4 Hz the continuous first-order19/second filter transmits about.60 of the amplitude;45/second transmits about.87.
5. For a strictly per-flight authored alternative, use clearly marked new phase targets around p=.16 (Y1.12), p=.45 (Y.91), p=.67 (Y1.10), p=.88 (Y.94), then let contact squash own p=1. Eased segments retain deformation across the hop. For long gaps, repeat the smaller seconds-based breathing rather than stretching one pulse across the whole gap. This is a design option, not a source timing claim.
6. To approach the original width readability, use X/Z about.90-.94 at full stretch and1.08-1.10 at full compression, or a tunable exaggeration atop volume-preserving scale. Evaluate body, feet and ground clearance together in the actual rear camera. Root gameplay position/collider and musical trajectory should remain owned by the session.

## Validation boundary

This report answers scale keys, clock separation, contact/support sequence, and absence of original alternating-foot animation from the selected cached source. It does not prove the final courier looks correct from the gameplay camera. Parent-owned live Editor capture should compare two consecutive normal hops at contact, p=.2, apex and descending, plus a long flight, across all courier rigs. It should confirm visible left/right identity, full-flight deformation, pause/resume, reset determinism and foot clearance. No Editor queue was touched here.
