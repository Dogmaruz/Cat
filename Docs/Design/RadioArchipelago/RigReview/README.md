# Connected-source character review

The selected rigs keep each original character in one connected mesh. Short
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
final Tiko 15/25/35° strain measurements:
[`source-checks.json`](source-checks.json).

Authoring commands and rig contract:
[`Tools/README.md`](../../../../ArtSource/RadioArchipelago/Tools/README.md).
The earlier separated/pinned-shell/high-angle prototypes in Library are rejected
fallbacks; the canonical `prepare_radio_rigs.py --stage rig` now creates one
connected mesh for every character.

## Actual Unity validation

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
