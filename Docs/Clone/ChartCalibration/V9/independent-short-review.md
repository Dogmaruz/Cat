# Independent short-Hold audit

Read-only research on exactly six known calibration recordings:
UnstoppableCat054, TwinkleLittleStar, MoonlightPop, LABUBUSongsPop,
DisconnectPop and CanCanPiano. Neither frozen recording was opened.
No producer, model, core code, shared data or Unity queue was changed.

The local `style_probe_snapshot.py` freezes the concurrent research helper
at SHA-256 `6c6bb49d55b978173eb8b0b693dddb9b931673f629cc0b15b8b8baa982a16b68`.
Earlier `run.txt` describes the older endpoint/label helper; the reproducible
final experiment is `run-snapshot.txt`, `cart-audit.json` and `diagnostic.json`.

## Findings

- Old training labels used Hold <=.6s and end tolerance150ms, whereas the
  agreed short-Hold metric requires <=.45s, end80ms and IoU>=.5.
  Aligning labels is necessary, but does not solve cross-song generalization.
- With the fixed strict helper, depth5/minLeaf12 CART gives zero LOO true
  positives and24 false positives. Requiring at least two positive training
  songs per positive leaf still gives zero TP and18 FP. Added vocal MIDI
  agreement gives zero TP/23FP, or zero TP/17FP with that guard. These are
  calibration-only leave-one-song-out diagnostics, not frozen test results.
- True Disconnect candidates largely lack reliable separated-vocal pitch.
  Absolute MIDI agreement and octave-invariant chroma agreement both have
  zero median on the true short-Hold candidate rows. The proposed missing
  feature was counterchecked and does not help with the current measurements.
- Coverage already limits achievable recall. Disconnect has candidate
  onsets near35/41 reference short Holds; the union of existing neural and
  harmonic measured endings can cover22/41 within the strict metric,
  only17/41 using rank0. Moonlight has42/42 onset coverage and38/42 possible
  measured endings (28/42 rank0). CanCan has5/5 starts but only3/5 endings.
  These are oracle coverage ceilings, not recognizer accuracy.
- A per-pitch cached-neural-onset probe did not recover any of the missing
  Disconnect starts. It covers22/41 starts versus35/41 for the current union
  with physical attacks. Thus simply replacing the global maximum onset
  selection with per-pitch peaks is unsupported by this countercheck.
- Predeclared conservative neural/harmonic duration-agreement + decay gates
  gave0TP/4FP over the six songs. Requiring rank0 and a physical source
  attack abstained completely. A sustained-until-next-attack rule gave
 6TP/139FP; stricter rank/source variants remained dominated by false positives.

## Recommendation

The final bounded release/grid countercheck supports extending the candidate
endpoint set without declaring a universal Hold classifier. All42 Moonlight
short annotations lie within25ms of half the independently detected .58s
pulse; all41 Disconnect shorts lie within25ms of half its .47s pulse.
For the nearest-onset primary note candidate, the annotated exit occurs a
median94ms after its neural release and93ms after its harmonic release on
Disconnect; Moonlight medians are48ms and70ms. These are model-measured
release estimates, not human-verified exact acoustic note-offs.

Allowing only simple detected-pulse endpoints within120ms of an existing
measured release raises oracle boundary coverage from22 to31 of41 Disconnect
shorts and38 to40 of42 Moonlight shorts. CanCan4/5 shorts fit a full pulse,
while LABUBU's four shorts do not fit these fractions. Thus a bounded,
source-supported pulse-end proposal is justified for testing; globally
forcing half-beat Holds or reporting this oracle ceiling as accuracy is not.

No defensible generic positive short-Hold rule was found here. Keep the new
style classifier explicitly experimental/opt-in on the requested test maps;
do not promote training improvements to default cross-song behavior. A
two-song positive-leaf guard is useful as an abstention condition but is
not sufficient evidence of a recognizer. Keep exact onset/end/IoU reporting,
preserve accepted long supports, and expose missing measurement coverage
separately from classification errors. Do not lower global thresholds just
to reproduce the reference count.

Files: `cart-audit.json`, `coverage-chroma.json`, `per-pitch-coverage.json`,
`physical-rules.json`, `release-vs-grid.json`. All probes use explicit known-song allowlists.
