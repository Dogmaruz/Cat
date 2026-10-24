# Character source and animation review

## Current accessory authoring: 24 October 2026

The current models keep the arms and body connected while adding a sealed
mechanical antenna seam. `body → antenna.yaw → antenna.01` gives each assembly
its actual mounting joint. Tiko/Nori have four bones in each scarf tail. Lumen's
lower backpack brace is removed and the sail joint is above the head.

| Character | Vertices / triangles | Bones |
|---|---:|---:|
| Tiko | 12,203 / 24,454 | 23 |
| Lumen | 9,431 / 18,878 | 12 |
| Nori | 7,344 / 14,704 | 21 |

Every file matches its manifest's SHA-256 and size. Existing 4K maps are byte
unchanged. Independent FBX roundtrip checks preserve the edited geometry, UVs,
weights, normals, bone rest frames and Blink. Joint rotations 0/90/180/270° pass
with maximum edge stretch 1.000272× and no new open boundaries. This is authoring
and export evidence; it does not assert a new Unity playtest result.

Revision v2 recesses each mechanical cap 0.004 source units into its own side.
Closed sidewalls preserve the exterior rings and silhouette. Exact coincident
fixed/moving cap triangle pairs fall from 28/14/23 to zero for Tiko/Lumen/Nori;
joint vertices retain zero Blink displacement. See
[cap clearance A/B](Accessories/mechanical-cap-clearance.json).

- Mechanical reports: [Tiko](Accessories/Tiko-authoring-checks.json),
  [Lumen](Accessories/Lumen-authoring-checks.json),
  [Nori](Accessories/Nori-authoring-checks.json).
- Lumen: [before](Accessories/lumen-before.png),
  [brace removed](Accessories/lumen-yaw-000.png),
  [joint turned 180°](Accessories/lumen-yaw-180.png).

## Historical connected-source review: 21 October 2026

The earlier rigs kept each original character in one connected mesh. Short
shoulder bones follow the user's marked hand axes. Working waves are 15–30°;
35° is a stress pose. No cuts, arm caps or replacement shoulder patches are used.

| Character | Vertices / triangles | Bones | Rest error / UV error |
|---|---:|---:|---:|
| Tiko | 11,947 / 23,962 | 18 | 1.35e−7 / 4.27e−7 |
| Lumen | 9,370 / 18,764 | 11 | 1.24e−7 / 2.67e−7 |
| Nori | 7,244 / 14,508 | 16 | 1.34e−7 / 2.15e−7 |

All three passed finite geometry, duplicate-face, normalized skin weight and
four-influence checks. Each has one connected component and a local Blink shape
key. Lumen/Nori are closed; Tiko retains only original source boundary segments,
within 4.22e−8 distance, and no new cuts. Original 4K colour/normal images remain
unchanged.

Tiko's former hard 0/1 neighbouring weights stretched a 6 mm edge about 34.54×
at 25°. A geodesic anchor buffer and harmonic weight field removed those needles:
the two tracked edges now stretch 1.0067× and 1.0554×. The mixed-UV corner is
2.142× instead of 5.607×. No hard binary pair remains among 7,166 sampled arm
edges. Some soft inner shoulder folds remain on the connected source surface.

Root visually accepted all three at the working angles. Blender source QA is
separate from the subsequent Unity renderer/gameplay checks.

Exact per-character proof, frozen source digests, authoring checks and complete
Tiko 15/25/35° strain measurements for that earlier revision:
[`source-checks.json`](source-checks.json).

Authoring commands and rig contract:
[`Tools/README.md`](../../../../ArtSource/RadioArchipelago/Tools/README.md).
The earlier separated/pinned-shell/high-angle prototypes in Library are rejected
fallbacks; the canonical `prepare_radio_rigs.py --stage rig` now creates one
connected mesh for every character.

## Historical Unity validation: 21–22 October 2026

The imported production prefabs passed the real-skin checks in
[`unity-rig-checks.txt`](unity-rig-checks.txt): normalized weights, owned 4K
materials, shoulder-weighted vertex motion, feet/accessory controls, actual
Blink deformation, pause/reset and sampled animation culling bounds.

Blink now retains the authored rest normals. Recalculating geometric morph
normals produced false vertical creases around the almost collapsed painted
eyes; a scratch A/B on identical vertices isolated that cause. The imported
Blink frames have no added normal deltas and still deform the actual eye
vertices. Technical evidence: [`blink-normals-audit.json`](blink-normals-audit.json),
final Unity image: [`lumen-unity-blink.png`](lumen-unity-blink.png).

The complete **157.153 second** recording passed with real UI/pointer input,
DSP continuity after pause, actual sustain/glide contacts, three stars, retry
and return home. All three selected couriers were checked in the live game.
Player profile, input, GameView and the clean scene were restored to Edit Mode.
See [`unity-playtest.txt`](unity-playtest.txt). The final shading-only importer
change was then reimported, rig checked and rendered again; no runtime motion
or musical timing changed. No player build was created.

The following are 48 actual Unity-rendered frames per sequence, encoded into
3 second GIFs. Screen left to right: Nori, Lumen, Tiko. The disposable preview
stage illustrates the flight arc; `CourierMotion` only moves visuals/bones.

- [`couriers-flight.gif`](couriers-flight.gif): long flight and landing.
- [`couriers-ride.gif`](couriers-ride.gif): balance and full turn on a long tile.
- [`tiko-unity-wave.png`](tiko-unity-wave.png): small shoulder wave.
- [`nori-unity-flight.png`](nori-unity-flight.png): long-flight pose.

Interactive inspection is available in **Radio Archipelago > Animation Preview**
inside Unity. It uses disposable Editor geometry and does not save the game
scene or modify the player's profile.

The later supplied Tripo part exports were inspected separately; their open
shoulder boundaries agree with the selected bone roots and do not justify
replacing the connected rigs or original 4K atlases. See
[`TRIPO_PARTS.md`](TRIPO_PARTS.md) for the comparison and source hashes.

## Menu framing and landing pad: 22 October 2026

The follow-up fixes double-applied skin scale, root-bone culling bounds and
UI ancestor scaling. All three couriers now share a feet-to-head display size
and stand on the first turquoise platform. The live review passed nine cases
across 540×960, 390×844 and 1080×1920, checking every baked vertex, body height
and the platform anchor. See [MENU_PREVIEW.md](../MENU_PREVIEW.md),
[numeric proof](Menu/skin-space-proof.txt) and [live results](Menu/after-framing.txt).

Actual GameView frames:
[Tiko](Menu/menu-signal-540x960.png), [Lumen](Menu/menu-sail-540x960.png),
[Nori](Menu/menu-orbit-540x960.png).

## Courier lighting in the menu and game: 22 October 2026

The shared studio profile adds a warm key, sky fill and soft rim, plus a static
HDR reflection map for the supplied URP Lit materials. No realtime probe capture
or shadow maps are added. The direct lights address the registered `Courier Studio`
rendering layer. The nine live menu cases check lighting as well as framing.

- [Before lighting](Menu/before-lighting-nori.png) and [after](Menu/menu-orbit-540x960.png).
- Actual gameplay: [jump](Menu/gameplay-lit.png), [sustain](Menu/sustain-lit.png), [glide](Menu/glide-lit.png).
- [Layer registration proof](Menu/lighting-layer-proof.txt): the previous defined
  mask `0x1` stripped bit 7; registered mask `0x81` preserves the light mask `0x80`.
- [Lighting setup and authoring](../COURIER_LIGHTING.md).

## Final Unity accessory review, 24 October

All three revised imports passed actual weighted-skin, shoulder, Blink and world
culling checks. Accessory checks verify turning lag, short/long flight, opposite
world rotation on both riding directions, finite deformation, pause, reset and
hitch stability. Mechanical cap clearance was reimported and checked again.

- [Flight GIF](Accessories/couriers-flight.gif) and [riding GIF](Accessories/couriers-ride.gif): 48 actual Unity frames each, 1200×600.
- [Accessory checks](Accessories/unity-accessory-checks.txt), [skin checks](Accessories/unity-rig-checks.txt), [nine menu cases](Accessories/unity-menu-checks.txt).
- [Full 157.153-second playtest](Accessories/unity-full-song-playtest.txt).
- [Separate Unstoppable comparison](../../../Clone/ChartCalibration/README.md), including its complete 67.004-second gameplay review.
