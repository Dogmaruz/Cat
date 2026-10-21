# Always Remember Meow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Add a full meow song and a playable chart generated from its actual audio.

**Architecture:** Optional custom resources extend the recovered catalog. An editor-only audio analyzer emits the existing timeline schema; gameplay continues through OriginalPlayController.

**Tech Stack:** Unity 6000.5.4f1, C#, existing uGUI and offline Resources.

**Spec:** Docs/Clone/2026-10-21-meow-level-design.md

## Global Constraints

- Preserve all original 58 songs and original data checks.
- Preserve the old The Cat project and its current local changes.
- No ads, purchases, online runtime dependency, or new runtime package.
- Song id AlwaysRememberMeowCat; catalogId 20001; custom resources in CustomData and CustomAudio.
- Report every Runway charge and balance; full generation already submitted for 8 credits, remaining 1985.

## Review Focus

- Missing optional custom catalog keeps original game playable.
- Duplicate ids or catalog ids fail with a useful error.
- Audio silence or missing rhythm cannot produce invalid zero-distance jumps.
- Holds exit onto reachable jumps and do not double-score.
- Song survives selection, pause, completion and persistence without changing original validation counts.

### Task 1: Optional custom catalog

**Files:** RecoveredContent.cs; OriginalUIPresenter.cs; RecoveredDataValidation.cs; new CustomContentValidation.cs.
**Interfaces:** RecoveredCatalog.LoadOriginal() returns the original 58 songs; Load() merges optional CustomData/songs; LoadMap(id) resolves original then custom map paths. Custom metadata uses existing SongRecord fields.

- [x] Add meaningful validation for merged uniqueness, missing custom catalog and recovered baseline; observe missing implementation failure.
- [x] Implement optional custom catalog and map resolution, leaving original resources intact.
- [x] Make new unlocked custom row easy to find; verify compilation and the original data checks.

### Task 2: Audio-derived map generator

**Files:** new Assets/Clone/Editor/AudioChartGenerator.cs and focused validation; new CustomData/Maps/AlwaysRememberMeowCat.timeline.json and song metadata.
**Interfaces:** Editor method AudioChartGenerator.GenerateAlwaysRemember() reads CustomAudio/Music/AlwaysRememberMeowCat, analyzes decoded PCM and writes custom chart/catalog metadata. Existing map timing: gameTime = audioTime + delayTime; z = gameTime * forwardSpeed. Keep audio analysis provenance and source digest.

- [x] Validate silence, pulse detection, minimum jump spacing and deterministic output using synthetic audio before implementing analysis.
- [x] Generate a chart from the complete actual recording, with jumps, sustained sections, safe lane changes and finish.
- [x] Validate decoded duration/sample rate, all chart intervals and map-resource loading.

### Task 3: Integrated playback and handoff

**Files:** focused editor QA harness, outputs/MeowCat-AlwaysRemember release files, documentation.
**Interfaces:** Use actual runtime SongRecord, map and playback; all previous original checks remain applicable.

- [x] Verify original checks and custom content together.
- [x] Run a complete actual custom-song playthrough, pause/resume, scoring and finish settlement; capture screenshot evidence.
- [x] Review final diff, fix material issues, build current Windows and Android players, document exact checks and credit usage.

## Execution ledger

Ruling: reuse the existing feature/dancing-cats-clone checkout and isolated DancingCatsClone project; all unrelated The Cat edits are explicitly excluded from staging and editing. This preserves the user's active checkout and Unity import cache.

Pre-flight: Tasks 1 and 2 share only the declared resource schema and ids; Task 3 consumes their runtime data. Catalog implementation is owned separately from generator implementation. The root owns audio files, build execution and final integration. No overlapping file edits are delegated.

User approved the audio direction and authorized the song/level work; continue execution without another approval round.

### Progress

- Task 1: Unity RED observed, then custom validation 5/5 GREEN in `work/meow-level/generate-verify-01.log`. Original baseline remains 58/10015/12668.
- Task 2: Generated actual full recording (157.152653 decoded seconds), measured 65.2174 BPM, 287 elements, 17 holds, two moving ordinary jumps. Generator synthetic/phase/determinism/safety and full original core checks passed in the same log.
- Task 3: Playtest01 completed the actual full audio, holds, both moving tiles and the real platform/final collider without death. It then failed on the recovered settlement Particle AudioSource, whose serialized enabled flag is false. Enable that source at its explicit reward cue; repeat full route.
- Final review: reject case-only chart identity collisions on Windows; strengthen actual playback to require all 38 hold checkpoints rather than any checkpoint. Generator guard regression RED then standalone GREEN; merged-catalog casing regression Unity RED (`case-catalog-red.log`) observed before the OrdinalIgnoreCase fix. Full combined GREEN and Playtest02 remain to run.
- User duration follow-up: all 58 originals median57s, mean56.69s, 49/58 shorter than75s, Meow Songs median64.51s. Future game edits target60–75s while full generations remain in Runway collection. Current full157s level is unchanged because the user specified this preference for future tracks.
- Final combined GREEN: `generate-verify-02.log`, custom5/5, original data6/6 and all core checks, including casing safety regressions. Scoped independent fix review found no remaining material issue.
- Playtest02 GREEN: actual complete157.153s playback, actual list/StartBtn input, pause/resume DSP alignment, all38 hold checkpoints/17 holds/exits/two moving tiles, real end platform/final collider, three stars/52650 best score persisted, settlement paid once and repeated claims idempotent; no runtime error or disabled-audio warning. Actual GameView screenshots reviewed. `work/meow-level/playtest-02/integrated-result.txt`.
- Windows/Android builds PASS through an owned helper that starts only after Playtest02 PASS. Windows271042699 build bytes; ARM64 APK128021481 physical bytes. Both builds report two existing obsolete-API warnings. Windows player startup READY59/63/618 without C# errors. New Android build has not been installed or exercised on a device/emulator in this run.
- After those builds completed the user requested Unity-only work. No further player builds, installation or device/emulator runs are authorized without a new explicit request. Stop release packaging and continue future testing in Unity Editor.
