# Island-specific finish worlds — 2 November 2026

The user approved the fourth island's repaired waterfall, then requested both its rollout and finish grounds that belong to each island instead of reusing the first green meadow. This directory covers the finish-world portion; waterfall rollout is separate.

## Runtime design

`IslandStageDefinition.FinishTheme` explicitly selects one `IslandFinishTheme`. Enum zero is `EmeraldMeadow`, preserving all older definitions. `IslandStagePresentation` passes that value into the existing scene-owned `IslandFinishGlade`, together with its own island materials. Moon additionally borrows its existing Crystal material for its crystal batch. There are no new runtime material instances or singleton lookups.

| Theme | Ground and rock | Actual batched regional geometry |
|---|---|---|
| EmeraldMeadow | Original green meadow, unchanged | Original grass, cornflowers, daisies and dandelions, same seed and geometry |
| CoralShore | Warm sand and limestone | Seagrass, branched salmon coral clusters and ribbed fan shells |
| MoonCrystalGarden | Cool blue-grey stone and pale rock | Sparse silver-blue flowers and hexagonal, flat-faceted crystal druses with3–7members, outward lean and varied height |
| AmberAutumn | Ochre ground and warm brown rock | Golden straw tufts, amber/orange flowers and individual curled fallen leaves |

All themes retain the same anchor, footprint, deep solid rock underside, planar9mground, coin/spark heights, courier hover, camera, music, route, finish trajectory and departure. Three new themes keep high decoration outside the9mcentral strip. Low Amber leaves remain below.1m. No decorative colliders or new Update loops are added. Four shared-material mesh renderers per finish; all created meshes are owned and released by the existing component.

The old Emerald's tall flower beds intentionally overlap the edge of the9mplanar ground (their roots start atX3.35m). Ground planarity does not mean the entire old9mstrip was mown. Checks preserve its original geometry exactly, then inspect its original central low-bloom trailX±2.3m plus.2mmargin and the established `MaxCorridorPlantHeight`. The new worlds independently enforce their widerX±4.5m low-decoration region. This distinction corrects an initially overbroad new test rather than modifying or silently accepting altered old geometry.

## Files and authoring

- `Runtime/World/IslandFinishTheme.cs`: stable enum.
- `Runtime/World/IslandFinishGlade.Themes.cs`: regional palette and actual geometry builders.
- `IslandFinishGlade.cs`: existing generator with preserved default path and themed branch.
- `IslandStageDefinition.cs` / `IslandStagePresentation.cs`: explicit theme data and existing lifecycle integration.
- `Editor/IslandFinishThemeJobs.cs`: baseline capture, shared fingerprint/material helpers.
- `Editor/IslandFinishThemeChecks.cs`: install only3newdefinitionfields; actual generated-mesh validation.

Public authoring methods: `IslandFinishThemeJobs.Install()` and `RunChecks()`. Root coordinates the existing local Editor queue. Future Coral/Moon/Amber authoring must set its theme explicitly; the first Emerald definition should not be rewritten just to save a zero.

## Evidence

`Before/emerald-before.json` was captured in native Unity **before runtime edits**. Original full mesh SHA (positions, colors, UV, normals and all indices in generation order):

`f30548408e85d62d1c9aaeeb687e66e7950c1eaf3bd65a9b210757f46cace09c`

Original112238triangles,4renderers,77cornflowers/74daisies/78dandelions. Source backups are in `Before/`.

Runtime checks compare that actual fingerprint, verify themed geometry/counts and finite vertices, sample663actualtoptriangles perworld under a rotated frame, enforce appropriate low-decoration clearance, ensure no colliders or borrowed-material writes, and confirm complete mesh disposal. Native full gameplay/finish screenshots and final actual results are supplied by root after water/prefab integration. Geometry checks are not artistic approval or phone performance validation. No builds or paid generations were initiated by this subtask.
