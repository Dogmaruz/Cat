# Lumi-Lai Island Implementation Plan

> **For agentic workers:** Use the existing Editor authoring and native review workflow; root owns the Editor queue and freezes all sources before Play.

**Goal:** Replace the approved island's trial song with the approved Lumi-Lai recording and regenerate the musical course.

**Architecture:** Import the unchanged Runway MP3, run the existing PCM/vocal/instrumental analysis and chart compiler, and adapt the new chart to the existing island route and safe elevations. Keep the same scene, presentation, movement and finish currency.

**Tech Stack:** Unity6000.5.5f1, existing C# authoring pipeline and cached local Python audio analysis.

**Spec:** User approved Lumi-Lai and explicitly requested replacing the old trial song and regenerating tiles on the island.

## Constraints
- No player builds and no new Runway generations.
- Preserve original reference songs/maps, island geometry, character assets and user preferences.
- Remove the trial song from the active song catalog; retain source history.

## Review focus
- Scene startup must select the configured song rather than a hardcoded legacy id.
- Chart timing and source hashes must match the new85.55-second recording.
- Long supports need straight safe vertical grades; all tiles must clear terrain.
- Repeated geographic route positions must not activate the finish glade early.
- Native full-song review must reach grounded rewards without synthetic seeks or altered collision rules.

## Tasks
- [x] Save prior active catalog/definition/chart/elevation and import approved MP3.
- [x] Export native PCM, derive cached vocal/instrument evidence, generate new base timeline with existing compiler.
- [x] Bind Lumi-Lai, remove active trial entry, regenerate island chart and safe elevation profile.
- [x] Import/compile, run chart/projection/terrain/finish checks, freeze sources, run full native Editor review.
- [x] Leave current scene ready and record results and source provenance.

Native review20261029-184629-043:PASS, full LumiLai source, win,20grounded Signal Sparks, zero actual geometry clips/early active glade violations, profile/input restored. Durable reports: `Docs/Design/RadioArchipelago/LumiLai20261029/Review`.
