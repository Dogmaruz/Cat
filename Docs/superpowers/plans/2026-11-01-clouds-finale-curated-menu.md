# Natural Clouds, Departure Finale and Three-Island Menu

> Execute in the existing Editor checkout with disjoint cloud/finish/menu workers. Root alone operates the Unity queue. No commits, pushes or Player builds.

**Goal:** Apply the user's approved polish: natural coherent clouds, forward-facing reward collection followed by a final leap into the distance, and a menu containing only the three original islands.

**Spec:** Explicit chat instructions2026-11-01: improve the current sparse clouds' spherical appearance; stop spinning during sphere collection, keep the courier's back toward the camera, then leap/fly/disappear after the last collected sphere; hide other levels instead of deleting them.

**Architecture:** Extend the existing scene-owned cloud mesh pool and courier/stage presentation. Keep native collection/win authority, clocks, rewards and colliders. Curate UI through a three-entry policy and new resource containing direct stage references, so selecting a song activates its actual island. Preserve the complete underlying song catalog and all existing assets.

**Tech:** Unity6000.5.5f1, URP, existing DI/presentation and allowlisted Editor queue.

## Constraints and decisions

- All three current islands receive the new cloud/finish presentation. The former continuous finish spin is explicitly superseded. Gameplay Hold/air spins stay intact.
- Clouds stay separate and differently sized. Use shared closed, smoothly shaded asymmetric cloud volumes; no obvious sphere chain, continuous white road, runtime volumetric raymarching or new dynamic lights. Keep current cloud stations/counts and prove actual drift/clearance again.
- Reward collection remains gameplay-authoritative. Start departure on the real final win trigger after collection. Use the existing two-second win window; target1.55s visual departure with upward/forward travel, late scale-down and disappearance. Pause, replay, scene switch and results must restore state correctly. No gameplay actor warp or extra reward settlement.
- UI shows LumiLai/EmeraldAbyss, TikoTa/CoralOasis, NoriNo/MoonResonance only, in that order. The25-row source catalog remains intact. Song selection, carousel, atlas and results-next use the same policy; switching must bind the real selected island before rebuilding the track.
- A valid authored scene remains its initial island; a legacy selection without a valid current stage falls back to LumiLai. No preference mutation just for menu visibility.
- Capture baseline of405 existing island/scene/import/audio/map/catalog/settings files in `Docs/Design/RadioArchipelago/CloudsFinishMenu20261101/Before/preserved-assets.json`. All remain byte-identical; new menu resource is an addition.

## Tasks

- [x] Cloud worker: replace current joined-ball meshes in `IslandLivingNature.cs`, update/add cloud geometry/spacing/allocation checks, freeze for Editor rendering.
- [x] Finish worker: update `CourierMotion.cs` and `IslandStagePresentation.cs`, add bounded departure helper and behavioral checks; expose minimal read-only state for native review.
- [x] Menu worker: own `RadioMenuPresenter.cs`, new curated policy/resource and Editor authoring/checks. Root creates only the new resource via allowlisted job.
- [x] Root: queue integration; native review expects stable forward collection, real post-collection departure/hide and frozen departure camera; preserve existing route/contact/clearance assertions.
- [x] Root: compile/check, inspect actual cloud renders, exercise all three menu selections and full-song runs, review preservation and final diff, archive receipts, leave clean Unity scene open.

## Review focus

Departure must not begin while an uncollected reachable sphere remains. No rotation, root/collider movement or camera chase during collection/departure; all modifications belong to the visual proxy. Native results remain timely. Menu-hidden songs are preserved, no accidental flat-map launch, and all three selected island definitions/sky/light agree. Cloud proof must use actual new meshes rather than assume old ellipsoid topology/bounds.

## Ledger

- Native finish behavior fixtures PASS: stable forward hover at30/60/120fps, single real win-trigger start, pause/reset/rebind/deactivation, late disappearance, zero warmed tick allocations. Independent review found trigger-frame delta incorrectly included the pre-trigger interval; fixed with a begin-frame guard and an actual stage boundary hitch fixture, which passes.
- UI static and live checks PASS:83merged source songs retained, exactly3visible library/atlas entries, arrows/results-next wrap only through them, hidden IDs rejected, actual chart/route/sky bindings verified for all3. Pre-Play profile/guide/courier preferences restored exactly; scene remains clean. The initial live test wrongly assumed every route exceeded600m; corrected to compare each route with its actual source (Emerald514.926m), without production changes.
- Cloud V1 radial shell passed topology/clearance but failed root visual inspection: too smooth, like a pillow. Replaced with a true smooth-union implicit volume of37 unequal lobes and a welded surface; another native render proof is required before acceptance.
- All405 protected existing asset hashes still match after menu/finish integration. New menu resource is an addition only. No source catalog, scene, map, music or settings changed.
- Cloud visual iteration: exact old-envelope filling stretched the raw natural proportions vertically. Changed to isotropic containment with aligned top, kept station sections/counts and strict clearance/gaps. Added a protruding middle tier and field-gradient normals; tiny-face verification now normalizes explicitly because Unity's Vector3.normalized zeroes valid cross-products below its epsilon. Original face-alignment threshold unchanged. Root inspected actual previews.
- User further specified exactly five strongly different cloud forms and five size classes, beyond the initial three variations/four sizes. New checker-first native RED confirmed FIVE_CLOUD_FORMS. Five structural recipes and balanced class assignments are being implemented; actual preview and placement checks remain required.
- First native Nori polish run175432335 preserved238contacts/31HoldPoints and217stable-forward finish samples (yaw error≈2e−8deg), but exposed a real takeoff scale pop: restoration binding.47 was used instead of bound presentation.64. Fixed by capturing current scale at real departure trigger; mixed-scale advance/apply/reset/restore regression now passes. No controller, map or reward edits.
- All final checks PASS: five structural forms/five size classes, balanced sky8/Moon7 and retained Coral4/Emerald13, shared opaque material, bounded mesh budget and tick allocations0. Root inspected five-form preview plus actual in-game clouds and all four departure phases.
- Final native full runs PASS: Moon181155547 (238/238,31HoldPoint), Coral181541209 (222/222,12HoldPoint), Emerald181903580 (228/228,34HoldPoint). Each collects20sparks, faces forward without finish spin, leaps7.2m/up and24m/forward, disappears with fixed camera, restores input/profile;0clips/landing overlaps. All receipts/captures archived. Final405asset hashes match, new menu resource added only, Moon scene open clean in Edit Mode. No Player builds.
- Final visual refinement: first island's intermediate departure was partly obscured by tree crowns. Increased the same1.55s presentation arc to18m rise/100m forward so it clears the canopy and remains framed by the fixed camera. Exact unit and native bounds assertions updated. Full three-song rerun PASS: Emerald183118543, Coral183303947, Moon183448675; all contacts/HoldPoints/20sparks, stable forward collection, complete18m/100m departure+hide, fixed camera,0clipping/overlap and restored preferences. Native-summary and latest per-island receipts now refer to these final IDs.
