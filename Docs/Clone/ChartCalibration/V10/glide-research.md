# V10 measured Follow-family recovery

Only the eight already-opened v9 recordings were used for tuning. The two newly selected v10 held-out recordings were not opened. The production detector reads only verified instrumental note events. Reference annotations are read exclusively by Library evaluation/research scripts. Production contains no song identities, absolute event times, absolute MIDI pitches, source-map reads or periodic tile fillers.

Inputs are the verified `mixed-note-events-2-candidates8` cache, retaining the same 22 onset/body/release/context features. The wider candidate list restores melody notes displaced by bass notes and stale octave activations. The reader verifies PCM/model/feature provenance and the feature digest before inference.

The two owned production files now:

- Retain plausible two-note responses until recurrence verification. A locally stronger delayed activation no longer discards the correct reply. Quiet first attacks and missing mixed-flux reply flags require independent tonal evidence within a recurring neighbor-note response. Competing neighboring-semitone activations are rejected. Actual harmonic release or a subsequent confirmed articulation sets the endpoint.
- Recognize recurring isolated scalar turns, strong scalar ascents, alternating pedal ornaments, broken-chord call/answer figures, upper-register stepwise answers and continuous neighbor-note trills. All path times come from measured source articulations.
- Recognize articulated tremolos over 1-5 semitones with at least seven note entries when the first measured neural duration is no more than 1.5 internal articulation intervals. Sustained activation and weak-pickup counterexamples are rejected. A verified source-dominant pickup may enter the phrase even when mixed-flux onset detection misses it.
- Retain octave ambiguity, register salience, tonal growth/body, timing regularity, neighboring pulse and recurrence guards. Single, irregular, weak and bass/octave-alias counterexamples are rejected.
- Keep independent spatial choreography. New endpoint corridors use the actual `ChartRouteComposer.MaxLateralShift` contract. Legacy Follow paths remain fixed. Bounded interior curve scaling preserves native speed; infeasible optional proposals are withheld.

Actual linked production C# against all eight verified V2 inputs returns **19 CanCan proposals, 19 matches and zero false proposals against 20 originals: 7 short and 12 long**. Start MAE is 6.893 ms, maximum 22.602 ms. End MAE is 5.850 ms, maximum 21.291 ms. All prior eighteen time/end/lane/path/phraseId objects remain exactly unchanged after adding the articulated tremolo. The old five short supports retain their timings and 0-to-alternating-0.8 paths.

The other seven recordings receive zero new proposals. Their existing unresolved Follow phrases remain: LABUBU 16, Clouds 11, Disconnect 1 and IJustWannaDance 1. The parent's legacy-priority merge preserves the existing two Unstoppable glides. This is CanCan family recovery, not universal Follow recognition.

Paths are independently choreographed. Normalized-phase lateral MAE is 0.513 native lane units, or 0.352 after allowing a mirror. Short supports intentionally use two native points; some original short supports contain three native elements. Full path/count and boundary details are in `known-eight-source-metrics.json`.

One CanCan original remains unresolved: 67.013..67.920. Its weak repeated melodic articulation resembles two earlier ordinary Jump phrases in the same recording. No annotation times or phrase-occurrence count rule supplies it.

Verification artifacts:

- `short-responses-red.txt` and `long-families-red.txt` record failing behavior before implementation.
- `articulated-tremolo-red.txt` and `tremolo-pickup-red.txt` record the later extension's failures before implementation.
- `articulated-tremolo-green.txt` records the complete synthetic suite and the final nineteen/zero source results.
- `previous-eighteen-unchanged.json` confirms exact prior-support preservation.
- `Harness/Review.csproj` links the actual detector, schema validator, musical score, validation, MusicalChartCompiler, ChartRouteComposer and recovered runtime map types. Only Unity/Editor/options/resource infrastructure is stubbed. `actual-compiler-route-green.txt` confirms actual Compile+Apply for tight endpoint fixtures, every long-family fixture and all nineteen actual-source CanCan proposals.
- `evaluate.py` evaluates previously generated C# proposals against reference annotations and writes `known-eight-source-metrics.json`.
- The parent reported its actual Unity final batch passed, with 19 CanCan Follows / 7 short and valid full-map geometry on all eight known recordings. The parent owns full integration, protected-support merging, publication and fresh held-out evaluation.

No original map, catalog, runtime behavior, model, scene or prefab was edited by this subtask. No Unity queue, build, network audio upload, stage or commit was performed by this subtask.
