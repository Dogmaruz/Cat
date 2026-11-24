# Detailed canonical islands implementation plan

> **For agentic workers:** Execute independent native Blender art modules, integrate and visually verify the reference-quality scene before batch output.

**Goal:** Restore the rich stylized V2 concepts while keeping every view a render of identical, traversable V4 geometry.

**Architecture:** Reuse the verified winding road and orthographic camera system. Author detailed real geometry/materials for landscape, foliage and architecture in isolated V4 modules. All production views render the same frozen scene; no generative repainting of individual views.

**Tech stack:** Blender 5.2 / Cycles CUDA, deterministic Python mesh authoring, existing geometry validator and PNG/BLEND/GLB packager.

**User requirements:** Richer islands matching the old concepts; all projections agree; road remains free. Preserve music/gameplay; no Unity builds.

## Global constraints

- Preserve V2/V3 and Unity assets.
- Exact same primary road centerline, 6m width plus 1m free shoulders per side, no overhead objects.
- Bridge decks remain at road Z=0; finite water below it.
- Every visual addition must exist in actual saved Blender geometry/materials.
- Five shared-scale axial orthographic views, plus isometric view, separate files.
- Preserve portable GLB base colors and full procedural source materials in BLEND.

## Review focus

- High visual quality is independent from passing geometry tests: inspect matched V2/V4 previews.
- Dense vegetation and cliffs must not intrude on the protected corridor.
- Building details must not be tiny repetitive boxes on display plinths.
- Natural terrain must not resemble a flat plate or golf-course trench.
- Materials and warm studio lighting must reveal relief and color, not hide plain geometry.

## Tasks

- [x] Detailed architecture: distinctive forms, crafted roofs, frames, terraces, radio mechanisms and warm window glass.
- [x] Detailed nature: multi-scale leaves/branches, rock fractures, moss, biome flora and readable water details.
- [x] Sculpted terrain: irregular rocky rim/roots, geological masses, natural banks; preserve road and clearances.
- [x] Integrate first forest island and render a matched quality preview; correct concrete shortcomings before full run.
- [x] Apply biome-specific population and material/lighting design to all ten worlds.
- [x] Run actual scene checks, all cameras and geometry/material invariance; render 60 separate 2048 PNGs.
- [x] Export BLEND/GLB, check portable colors, audit package/ZIP and gallery, show actual results with limits.

## Ruling: route proportions

The original 8m+2m+2m corridor was our implementation choice, not a user constraint. It consumed too much of the island and visibly prevented matching the V2 composition (whose guide road was5.4m). Use6m+1m+1m: the actual measured visual character envelope including side travel is4.581m; the road alone retains1.419m of total reserve, and the full protected corridor3.419m. Revalidate every scene at these actual values. Unity remains untouched.

## Verified delivery

All10production scenes pass14/14checks each;60separate2048PNGs,10BLEND,10GLB. Full ZIP CRC audited; smaller60-PNGZIP separatelyCRC audited. Independentcurrent6m/1mterrain/native-riveraudit161/161andvalidatorregressions4/4; actualGLBroundtrip01/02passed. All10top/isopairsvisuallyreviewed. LiveV4galleryshownandviewselectorchecked; Unity not modified.
