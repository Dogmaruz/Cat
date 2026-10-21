# Tripo part exports: Tiko2 and Nori2

Keep the accepted connected rigs and original 4K textures. The new files provide
useful semantic part labels, but do not add finished shoulder joints or repair
the model underneath a detached hand. No new rig or Unity import was made.

| Source | Parts / materials | Vertices / triangles | PNG textures | Arms: character R / L |
|---|---:|---:|---:|---|
| `Tiko/Tiko2.zip` | 26 / 26 | 5,113 / 9,298 | 130; 32–1024² | part 7 / part 9 |
| `Nori/Nori2.zip` | 10 / 10 | 4,984 / 9,514 | 50; 128–1024² | part 7 / part 6 |

Both contain one binary FBX7200 with UVs, normals and five RGBA maps per part.
Neither contains skin deformers or animation stacks. The FBX parent transforms
were baked in the isolated inspection before comparing scale and appearance.

**All original triangles are accounted for:** 9,298/9,298 Tiko faces and
9,514/9,514 Nori faces match uniquely. There are no ambiguous or unmatched faces.
Minor FBX coordinate quantization is at most about 0.001972 cm for Tiko and
0.00136 cm for Nori. The silhouette is the same original surface.

**Every part remains open.** Tiko has 998 part-boundary edges; Nori has 466.
No caps or new interior shoulder surfaces were created. The body-without-arms
renders expose openings behind the straps. Arm boundaries extend down the inner
hand/armpit; their centroids must not become low elbow-like pivots.

The upper semantic attachments agree with the current upper shoulder bones and
the user's diagonal hand-axis drawing. Tiko's arm tops are at Z≈1.005/0.966,
against current shoulder Z≈0.990/0.980. Nori's are at Z≈0.851/0.865, against
shoulder Z≈0.880/0.890, slightly inside the body under the scarf. The new export
does not justify moving those accepted bones or returning to split/cap rigs.

Nori's scarf collar is part4 and both free tails are part5. Those are open
surface patches too. Tiko's harness is in parts2/13; hands are separate semantic
patches, which may help a future targeted weight adjustment without cutting the
original connected mesh.

The small new textures use different UV layouts. Original 4096² maps cannot be
substituted directly. Two safe uses of the correspondence are:

- Keep the original mesh, UVs and textures; use the new face labels only as
  references for localized weights.
- If actual part meshes are needed later, transfer the original per-corner UV
  coordinates using the unique triangle matches, retaining the original atlas.

The first option is preferred for the current game. This audit does not claim
that new semantic labels have already improved the accepted weights.

Exact hashes, dimensions, topology, root measurements and correspondence counts
are in [`tripo-parts-audit.json`](tripo-parts-audit.json). Full label arrays remain
in `Library/RadioRigging/Review/Tiko2/` and `Nori2/`; their indices are zero-based
**original raw FBX polygon order**, not assumed Blender/Unity imported indices.
Inspection renders are in the same folders. Lumen remains on its existing
connected source; no additional generation or credits are needed.
