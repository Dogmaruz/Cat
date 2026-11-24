# Amber waterfall visual repair

> **For agentic workers:** Use superpowers:executing-plans or subagent-driven-development. Track the checks below.

**Goal:** The stream must flow continuously over a rounded lip, and downward movement must be obvious at the normal portrait gameplay camera.

**Architecture:** Replace Amber's split stream/three overlapping curtains with one connected ribbon and one material per cascade. A dedicated URP shader uses continuous metric downstream UVs, irregular travelling foam and smooth slope-dependent shading. The old shared water shader and the first three islands stay unchanged. Limited ordinary particles provide impact spray.

**Tech stack:** Existing Unity 6000.5 / URP17, Blender source exports, existing local Editor queue.

**Spec:** User's screenshot 2026-11-02 101142 and explicit seamless-spill/visible-falling-water feedback. This is correction of the approved waterfall, with no new gameplay or music.

## Constraints / review focus

- No Player build, paid generation, package/pipeline migration, commit or push.
- Preserve all non-water source geometry and route/elevation/music/chart/catalog bytes.
- Capture the current failing appearance first; old numerical movement reports do not establish visual quality.
- Check lip, middle and base in short timestamped native-clock clips at actual route cameras; inspect the moving result visually.
- Use one coherent material across the lip, no abrupt opacity/normal/color switch and no stacked continuous sheets.
- Check shader compilation, old-island asset hashes, route safety and scene restoration.

## Tasks

- [x] Diagnose the mismatch between numerical checks and user-visible appearance: one opaque stream becomes three narrower transparent sheets, each resetting U; low-contrast long filaments dominate movement.
- [x] Capture before-state native clips and baseline hashes; keep them separately.
- [x] Rebuild only the three water ribbons; weld/resample shared boundaries, smooth geometric normals, retain metric downstream UVs, round the lower spill.
- [x] Implement Amber-only flow shader and targeted material/prefab authoring; add bounded base spray.
- [x] Compile, capture after-state at identical cameras, inspect clip motion and lip continuity; independently review.
- [x] Run relevant island/gameplay checks, recheck protected hashes and restore a clean Amber scene. Report actual evidence and any remaining visual limitations.

Final native visual run20261102-074355-3655427:48frames, all water present, original material/scene data restored. Independent review: continuous lips, broken downstream foam,134downward/0upward/4subpixel local matches (not artistic approval by the user). Full native gameplay20261102-074617-863 passed:346/346contacts,20sparks,0errors/actualGeometryClips/actorOffscreen, profile/input restored. Root preservation764files:761unchanged/3exactauthorisedAmberassetupdates/0unexpected. Last inspect:AmberWindsPilot,Menu,notplaying,notcompiling,sceneDirtyfalse. No phone-performance certification, builds, paid generation or commits.
