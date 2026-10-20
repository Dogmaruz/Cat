# Dancing Cats 0.5.8 reconstruction evidence

The independent Unity project is `DancingCatsClone/`. The former `The Cat/` project is retained as historical material and is not a runtime dependency.

## Preserved history

- Archive branch: `archive/cat-before-clone-2026-10-21`.
- Archive commit: `a6c331a257a6de5d587882fabc9ea141aa5ff00e`.
- Reconstruction branch: `feature/dancing-cats-clone`.
- A verified complete Git bundle was also saved in the task's local research directory.

## Reference

Android package `com.rhythmdance.taptile.dancingcats`, version 0.5.8, build113. The acquired XAPK SHA256 is `f304e12ed6881b84d3d83daa6be3db0e6cc7c478c97730ee126ff91240fbdd83`. Original engine: Unity2022.3.62f2c1, ARM64 IL2CPP. The fresh project uses installed Unity6000.5.4f1 and URP17.5.

The reference app was installed in an isolated Android emulator. Its consent screen was accepted with the user's explicit permission. Its UI renders; original 3D shaders exceed this emulator's GLES vertex-uniform limit. Reference 3D playback therefore cannot currently serve as visual proof of equality. Native disassembly and recovered prefab/material/animation data supply the behavior evidence.

## Recovered content

The active Android catalog contains58 full songs,58 previews,23 effects,58 authored maps,10015 map elements and12668 Koreography events. Original remote content was retrieved using the production URL recovered from the binary. Inactive alternate-version/iOS catalog entries are not substituted into the active song list.

`Assets/Resources/OriginalData/` retains normalized map positions, timing, source fields and content hashes. Map JSON is a portable representation of the original serialized maps; it is not an automatically generated approximation. Main audio is the recovered source audio. Vorbis packets were reconstructed from original FSB banks without a lossy encode pass.

`Assets/Recovered/` contains the original meshes, textures, sprites, animation clips, Animator Controllers, prefabs and scene assets. Application/advertising/payment MonoBehaviours and native plugins are excluded from the copied project. Supported engine/uGUI/TMP script references are remapped to installed packages. A complete untouched export, removed component documents and import logs remain in the local research cache.

`Assets/Clone/Shaders/` ports recovered compiled shader formulas to the current render pipeline. Original material property names are preserved for animation bindings. `Assets/Clone/Editor/RecoveryMetadata/` records shader and asset-import provenance. This is reconstructed shader source, not the unavailable original ShaderGraph authoring project.

## Independently reconstructed runtime

Production code is under `Assets/Clone/Runtime/`. No production code from the former offline prototype is copied into this project. Evidence includes the original Hermite-warped parabola, sphere/box contact geometry, frame-based lateral damping, streak scoring, Hold checkpoints plus separate unscored exit, Follow movement, original animation states, and exact UnityChan spring bindings for63 rigs/305 bones.

Offline state is stored under the new `DancingCatsClone.v1` PlayerPrefs namespace. Reward actions execute locally; real advertisement/payment SDKs are not integrated. Online-only selection policies, where no original deterministic local rule exists, must be described explicitly rather than called native behavior.

The daily challenge uses the recovered endless route, best partial run marks and three reward checkpoints at3/6/9. Its original song-selection policy has not been fully recovered; the offline adapter explicitly chooses a stable active catalog song per local date. Reward thresholds, amounts, maximum and daily reset use recovered native behavior.

The generated library includes618 sprites, including the separate native Sprite assets as well as TextureImporter subassets. All166 configured cat/trail/reward/furniture sprite names are validated. Unused raw recovery documents remain outside the product and Git repository; this excludes recovered SDK configuration and keys. The two Unity catalog entries share an identical source music bank and one canonical OGG resource, avoiding duplicate extensionless Resources paths.

## Validation entry points

- `RecoveredDataValidation.Run`: active maps/audio and original timing/data relationships.
- `NativeRulesValidation.Run`: recovered curve, height, accuracy thresholds and scoring.
- `OriginalRoleMotionValidation.Run`: all original rigs, states, spring paths and bone lengths.
- `ProgressionValidation.Run`: original prices, unlocks, reward ledgers, board traversal, furniture and persistence.
- `OriginalTileVisualValidation.Run`: authored Hold geometry, external checkpoint prefabs and animation/FX states.
- `OriginalThemeVisualValidation.Run`: exact renderer/material palette mappings for seven gameplay scenes.
- `OriginalAssetLibraryValidation.Run`: original dynamic UI images and all configured sprite references.
- `CloneSceneBuilder.Run`: regenerate the original-content library and independent game scene.
- `IntegratedPlayValidation.Run`: real Input System pointer events, UI raycasts and timed gameplay through actual colliders.
- `IntegratedPlayValidation.RunInterfaceAndRecovery`: consumed Hold-exit revival without duplicate point scores, protected misses near the real finish, native pointer-down start and original panels.
- `IntegratedPlayValidation.RunMenus`: real pointer clicks through tutorial/start, pause/quit/cancel/resume, seven original panels, dice/modal claim, daily card, shop equip and login claim. Also runs progression and sprite-library checks.
- `IntegratedPlayValidation.RunChallenge`: actual challenge selection and pointer-down start, three complete endless laps, nine-mark result overlay and one claim of all 3/6/9 checkpoints.
- `ClonePlayerBuild.Verify`: aggregate native rules, rigs, progression, tile visuals, themes, sprite library and recovered-data checks.
- `ClonePlayerBuild.Windows` / `.Android`: build the single independent scene. Set `DANCING_CATS_BUILD_OUTPUT` to a deliverable directory.

Each entry is a Unity Editor `-executeMethod` target in namespace `DancingCats.Clone.Editor`, except the global `RecoveredAssetImporter`. Validation results and remaining differences are recorded separately after running them; the presence of a test does not imply it has passed.

## Validation record, 2026-10-21

| Evidence | Verified scope |
| --- | --- |
| `final-verify.log` core checks | Native jump/scoring rules; 63 rigs/305 spring bones; progression; all seven authored Hold variants and checkpoint visuals; seven themes/114 materials; recovered data 6 passed, 0 failed, including loading all 58 active songs/maps/previews. |
| `integrated-qa-10.log` gameplay assertions | Complete UnityPop054 starter and AllAlonePop Hold/Follow charts; actual tile landings/final trigger; pause/resume DSP alignment; miss/revive; FlyingPop three-lap endless result with 9 marks. |
| `integrated-qa-12.log` recovery assertions | Consumed Hold exit is rearmed after revive without rearming scored checkpoints; its replay launches a real arc. An invincible miss cannot bypass the final platform; completion still requires its actual trigger and win sequence. |
| **Menus16**, `integrated-qa-16.log`, final PASS | Real Input System mouse events and EventSystem raycasts, start while held, pause confirmation blocking Resume, seven panels, animated dice roll/modal claim, daily cat 9 unlock and original Pick animation, actual shop equip updating the actor, login +100 once. Latest progression, 618 sprites and all 166 configured heads also pass here. |
| **Challenge18**, `integrated-qa-18.log`, final PASS | Actual challenge selection/start, three complete FlyingPop laps, 9-mark overlay, all 3/6/9 reward checkpoints and a single 500 + 1000 + 1500 claim with ordinary settlement retained underneath; no product runtime errors in the tested flow. |
| Windows, `windows-build-2.log` / `windows-player-smoke.log`, PASS | Build succeeds, 267,207,227 bytes, two obsolete-API warnings. The standalone process reaches READY with 58 songs, 63 cats and 618 sprites without product runtime errors. This smoke covers startup/resource loading; the complete gameplay/input checks above ran in Editor. |

The QA10/QA12 rows describe their passed gameplay assertions, not successful completion of those older combined runs. Final menu/reward coverage is Menus16. Logs and screenshots are retained in the task's local research directory. The pointer tests exercise button routing rather than invoking `Button.onClick` directly; their fixture saves are restored afterward.

All 58 catalog entries have automated data/resource coverage. Full scene playthroughs cover the named fixtures, not every song or every input/device combination. Full visual/pixel or audio-perceptual equality is not established: reference 3D rendering is unavailable in the emulator, shader source is reconstructed for URP, and the unavailable online challenge-song policy has the explicit offline replacement described above. Android build/device results and final commit/push remain pending; a full standalone gameplay/device comparison has not been established by the Windows startup smoke.
