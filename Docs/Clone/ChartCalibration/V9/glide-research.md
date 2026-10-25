# V9 short Follow research

Inspected calibration audio only: UnstoppableCat054, TwinkleLittleStar,
MoonlightPop, LABUBUSongsPop, DisconnectPop, CanCanPiano. No new frozen
recording's features, per-event labels or errors were inspected.

The legacy detector needs exactly five mixed attacks, 1.05–1.6 seconds,
an ensuing 450ms rest, irregular IOI, unusually strong continuity and an
almost identical recurrence. The compiler also rejected supports shorter
than one second. Most LABUBU Follow intervals are .61–.92 seconds; seven
CanCan intervals last .344–.375 seconds and have only native entry/exit.

Rejected prototypes:

- Rapid attack bursts alone detect ordinary phrases and accompaniment too.
- Raw mixed-NN descending pairs make many false proposals, with some pitch
  activations persisting long beyond the source's true harmonic release.
- Strict three-growing-partial requirements miss high piano notes with a
  dominant fundamental and very weak upper partials.

Accepted conservative policy uses verified compact instrumental evidence:
audible independently articulated high-register descending two-note cells,
at least one growing tonal partial, a reliable neural attack, a source-specific
harmonic body and measured release. A cell must belong to at least three
repetitions with stable measured call/response periods. Pairs inside a larger
uninterrupted run are withheld. End is the earlier measured second-note release
or following independent articulation; it is not an invented grid duration.
Existing native Hold/Follow supports always take priority. Short supports use
only two path points, avoiding an invented score checkpoint on the internal
pitch change. Spatial direction is an independent alternating policy.

Actual C# against the final six compact source files returns five proposals
on CanCan, all five matching start within 80ms and end within 150ms. It returns
zero new proposals on the other five calibration recordings. This is an
in-calibration gain, not a claim of full Follow recognition or generalization.
Fifteen CanCan Follow gestures and all LABUBU Follow gestures still need other
phrase families. The legacy two Unstoppable glides remain unaffected.

Files: compact_follows.py is a Python prototype; *.cs-proposals.json records
actual production C# proposals; actual-source-green.txt confirms both synthetic
counterexamples and six verified evidence files. red.txt captures the missing-
detector failure before production implementation. The harness links production
sources and provides only Unity/Editor infrastructure stubs. Root owns actual
Unity compilation, publication, full play tests and held-out evaluation.

Disconnect's invalid 2.618821-second final arc runs from its last ordinary
attack at 46.540771 to a finish at 49.159592. The gap contains no newly
confirmed melodic entry. The source has a final sound fading from RMS .219
around 46.7s to .004 around 47.6s and near silence after 49.4s. Neither an
arbitrary Follow nor a periodic filler tile is justified by these measurements.
