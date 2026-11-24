# Read-only waterfall placement review

The successful topology/shader checks do not prove that an opaque cliff leaves
the falling water visible. Native art review exposed a separate older placement
problem. This review did not modify sources, Unity Assets, routes or cameras.

`audit_body_occlusion.py` loads the final authored Coral/Moon scenes. Its BVH
contains the actual Terrain and Cliffs triangles, preserving object names for
every hit. It samples five drop fractions and three transverse U positions.

For centre samples, eight tilted horizontal rays count **signed exits minus
entries**, rather than relying solely on nearest-face normal or odd/even parity.
The signed count handles overlapping closed rock volumes: a consistent positive
count indicates containment. Eight additional horizontal exterior visibility
rays originate200m away at the *same height as the water point*, so the diagnosis
does not depend on the reviewed art cameras being above the lake.

## Findings

| Surface | Centre heights tested | Containment witness | Horizontal occlusion |
|---|---|---|---|
| Coral high | 4.751, −3.510 | Signed exit-minus-entry2 on all8rays at both heights | All8azimuths blocked |
| Coral high, lower body | −13.762, −23.710, −31.942 | Signed count0 on all8rays | 8/7/6azimuths blocked by surrounding/overhanging cliff geometry |
| Coral low | −1.144, −7.397, −15.249 | Signed count1 on all8rays at each height | All8azimuths blocked |
| Coral low, bottom quarter | −23.104, −29.538 | Signed count0 on all8rays | 4azimuths clear: body emerges below the rock |
| Moon low | −2.166, −13.945, −28.704, −43.160, −55.032 | Signed count0 on all8rays at all heights | +X direction clear throughout |
| Moon high | 10.985, −2.729, −19.789, −36.851, −50.452 | Signed count0 on all8rays at all heights | +X direction clear throughout |

Coral blockers are chiefly `RA_Coral_Cliffs_+01_-01_01` for the high fall and
`RA_Coral_Terrain_+01_-03_01`/`RA_Coral_Cliffs_+01_-03_01` for the low fall.

Representative actual distances:

- High Coral z4.751: nearest cliff1.217m; solid cap1.456m above; first +X cliff
  intersection6.991m, first −Y intersection2.550m. Because the point lies inside
  two overlapping volumes, the first exit is not necessarily the final exit.
- High Coral z−3.510: nearest cliff0.491m; cap9.717m above; +X/−Y first exits
  0.926/0.614m. Both containment rays and same-height exterior blockage agree.
- Low Coral z−1.144: terrain cap1.024m above; +X/−Y first exits13.426/8.250m.
- Low Coral z−7.397: nearest cliff3.377m, cap7.278m above; +X/−Y first exits
  9.130/5.339m.
- Moon high at its first5% drop has a small overhang0.469m above and nearest
  cliff0.133m away, but signed count0 and a clear +X view. Later body samples
  have no overhead cap. A lower exterior camera should show this fall without
  sculpture based on the present evidence.

## Conclusion and limits

Coral's upper falling portions are genuinely inside rock envelopes; this is
not explained only by an above-lake art camera. The low body emerging farther
down matches the native image. Any correction should be explicitly scoped to
Coral's shore/channel/cliff placement and validated visually afterward.

Moon's bodies are outside the sampled cliff volumes. Better front/mid-height
art views are justified before considering any change to its scenery. A signed
count0 does not certify every point of a finite-width waterfall; the detailed
report also retains its transverse samples and every exterior hit.

Detailed witnesses, source/Unity positions, normals, ray directions, blockers and
distances are in `body_occlusion_audit.json`; compact tables are in
`body_occlusion_summary.json`. No phone performance or gameplay-route change is
inferred from this geometry-only inspection.
