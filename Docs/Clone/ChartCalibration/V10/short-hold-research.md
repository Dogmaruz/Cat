# V10 short-Hold diagnosis and implementation

Only eight previously inspected recordings were read as tuning material:
UnstoppableCat054, TwinkleLittleStar, MoonlightPop, LABUBUSongsPop,
DisconnectPop, CanCanPiano, IJustWannaDancePop and MoonlightCloudsPiano.
No new held-out measurements or annotations were opened.

## Concrete defects

V9 measured voice body at onset+40..220ms and tail at onset+250..420ms,
irrespective of a candidate's selected exit. For the common 200–230ms
short supports that tail was actually the following articulation. A real
C# RED counterexample starts with an accepted 200ms support; changing
unrelated voice RMS only after exit+80ms changes the previous decision.
The new GREEN counterexample preserves the same start and selected exit.
The global 95th-percentile normalization is unchanged by that perturbation.

The old reattack gate also used aggregate features across the original
note's full measured duration, before choosing a short endpoint. A second
RED example rejects a valid short interval because a same-pitch repeat
occurs later. The new gate only uses actual same-MIDI, physically confirmed
and neural-onset-confirmed events inside start+80ms to exit-40ms. A true
interior repeat remains separate; a later repeat cannot erase the earlier
short support. Same-MIDI bounding alone fixed interval ownership without
changing the known-eight metric, because an octave alias still produced
the same wrong interval. An isolated follow-up handles equal/stronger
same-chroma hypotheses at the identical onset: a weak octave alias cannot
hide its stronger note's real internal repeat. That removes one LABUBU
false interval without losing or moving any known-eight true interval.
Unrelated pitches, later repeats and weaker octave anchors cannot veto.

## Final policy

The existing source-supported endpoints remain: actual NN/harmonic releases,
nearby measured articulations and detected-pulse subdivisions only within
120ms of a measured release. The producer now supplies up to eight pitch
hypotheses, with the same 22 measured feature semantics. The short classifier
receives no recording identity, absolute MIDI or reference timestamp.

Voice body is measured from onset+30ms to exit-25ms; release is observed only
between exit+15ms and exit+75ms. Peak and pitch confidence are measured inside
the selected interval. A regularized tree uses 34 source features, depth8,
minimum16rows and inverse per-recording row weights. Only leaves with
training precision >=.90 and >=2positive rows from >=2recordings survive;
there are five such positive leaves. The pruned C# conditions match the
reproducible Python trainer.

This is an offline classifier, not a wholly causal live recognizer. The
newly added voice-window features have the stated bounds; existing mixed-note
harmonic tails/durations, following context and whole-recording RMS scaling
still use offline measurements.

## Evidence and limitations

Actual linked production C# proposals on the known eight recordings:

| Recording | Correct | Extra | Proposed |
| --- | ---: | ---: | ---: |
| Unstoppable | 0 | 0 | 0 |
| Twinkle | 0 | 0 | 0 |
| Moonlight | 26 | 0 | 26 |
| LABUBU | 0 | 0 | 0 |
| Disconnect | 7 | 1 | 8 |
| Can Can | 0 | 0 | 0 |
| I Just Wanna Dance | 11 | 1 | 12 |
| Moonlight Clouds | 0 | 0 | 0 |

These 44correct/2extra are proposals before legacy-support protection and
the full native chart compiler, not final published-map metrics. All eight
recordings are calibration data now. The earlier six-song V9 classifier
was not trained on I Just Wanna Dance or Moonlight Clouds, so its previous
held-out result must not be compared to the new in-sample result as if both
were unseen evaluations.

Leave-one-recording-out gives 7correct/17extra: Moonlight5/0,
Disconnect2/3, LABUBU0/13 and MoonlightClouds0/1, with0/0 for the other
four recordings. It remains weak; the classifier stays experimental,
default-off. Final LOO values and input/source hashes are in
training-proof/short-support-release-v2.json. Root owns the new independent
evaluation, final native integration and complete Unity playthroughs.

The remaining known extras are wrong Hold types at source-confirmed ordinary
Jump articulations: Disconnect11.431111→11.666111 and
IJustWannaDance2.476009→2.660499. They are not saved as overrides or rules.

Rejected/limited alternatives: merely lowering global duration/RMS gates;
retaining fixed future voice windows as contextual training features;
seeding exact-MIDI recurring-cell propagation (no extra coverage with the
tested conservative source-matching criteria); broad rank0-only and NN-peak
cuts (lose many genuine lower-rank short-note candidates).

Tests: red.txt / red-reattack.txt / red-octave.txt, green.txt; five independent Python checks
cover feature translation and unrelated later syllables, chosen-endpoint
release alignment, maximum-cardinality interval matching, wrong exits and
overlong proposals. Existing silence, unavailable evidence, protected legacy
supports/launches, immutability and corrupt-feature tests remain in C#.
The octave gate is additionally checked with true internal and external
repeats and a weaker hypothetical octave that must not suppress its source.
