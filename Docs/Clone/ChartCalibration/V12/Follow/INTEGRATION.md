# Frozen candidate integration

Copy only the four files named in `READY.json` from `Candidate/` into `Assets/Clone/Editor/`. The authoring methods contain no song identities, reference-map reads, annotation times, absolute MIDI constants, target counts, or label-derived lookups. All15 recordings are known tuning/regression material.

The long-Hold method is:

```csharp
public static bool PcmLegacySupportAnalysis.RejectsArticulatedHold(
    PcmInstrumentalEvidence.Evidence instrumental,
    PcmVocalAnalysis.Evidence voice, MusicalCue cue)
```

Apply it to vocal Hold proposals before they reserve support or flight intervals. It retains unavailable optional evidence and supports at or below450ms. Two distinct interior pitched bodies require strong independent mixed neural/harmonic evidence, agreeing physical/neural durations, and at least60ms matching high-confidence separated voice. One interior release alone remains compatible with a valid Hold. The selected policy removes5 false long Holds on the15 known recordings, preserving Unstoppable's13 generated long Holds and its one generated short Hold (the final strict all-Hold metric is12TP/2FP/2FN; this is preservation, not14correct matches). An ablation accepting one interior body loses3 correct Unstoppable Holds. The duration-agreement ablation additionally rejects one uncertain Baby Hold; the adopted policy retains it.

The new Follow method is:

```csharp
public static MusicalCue[] PcmMelodicFollowAnalysis.AnalyzeVoicedResponses(
    PcmInstrumentalEvidence.Evidence evidence,
    PcmVocalAnalysis.Evidence voice, MusicalCue[] protectedSupports)
```

The existing `Analyze` method remains behaviorally unchanged. After creating legacy Hold/Follow supports and before calling `PcmFlightAnalysis.Analyze`, insert:

```csharp
var legacySupports = movingPhrases.Concat(vocalHolds).ToArray();
if (instrumentalEvidence != null && vocalEvidence != null && options.includeMovingJumps)
{
    var risingResponses = PcmMelodicFollowAnalysis.AnalyzeVoicedResponses(
        instrumentalEvidence, vocalEvidence, legacySupports)
        .Where(cue => cue.time >= 0 && cue.endTime <= finishTime - MinGap).ToArray();
    movingPhrases = MergeProtectedSupports(movingPhrases, risingResponses);
    legacySupports = movingPhrases.Concat(vocalHolds).ToArray();
}
// Existing flight Analyze/EligibleFlights/reservation code follows unchanged.
```

This deliberately gives independently supported recurring rising bodies precedence over newly proposed optional flights. Legacy Hold/Follow priority remains protected, and every unrelated flight retains its previous protection. It does not compare confidence numbers across detectors; those are not calibrated probabilities. Existing late instrumental Follow analysis still honors the reserved flights.

The response requires a measured rising pickup, independent separated-voice agreement on both pickup and plateau, agreeing physical/neural body duration, recurrence of relative interval and timing, and no fresh independent articulation inside the body. Release follows the measured physical note body or a confirmed subsequent contrast. The original motif at14.248 is an authored Hold, while similar motifs at29.732 and55.534 are Follow. The adopted family classifies all three as Follow. That tradeoff replaces three already-wrong BigJumps, recovers two true Follow intervals, and makes one Hold/Follow type confusion.

Validation entry points:

```csharp
PcmLegacySupportValidation.RunChecks();
PcmVoicedResponseValidation.RunChecks();
PcmMelodicFollowValidation.RunChecks();
```

The new response fixtures cover real foreground agreement versus resonant background, weak source/voice, mismatched durations, isolated occurrence, fresh interior notes, repeated pitch, protected Hold and Follow, exact protected coordinates, only overlapping flight exclusion, unrelated flight retention, immutability and clock/register translation. Legacy validation has16 assertions; the stale17-assertion log wording is corrected.

## Full source-pipeline evidence

`FullPipeline/` uses sealedV11 source snapshots from `Library/ChartGenerationV12/before/algorithm/`, the frozen candidate methods, unchanged PCM in `Library/VocalAnalysis/<source-hash>/source.f32`, and the verified source caches. It executes actual spectral analysis, vocal supports, flight detection, foreground selection, experimental short supports, sparse completion, native compilation and route composition. Unity I/O is stubbed. Reference maps enter only evaluation.

All45 source-pipeline passes (15songs ×3modes) pass the actual native geometry validator. All15 baseline cue types, timings, indices and event/tempo data agree with sealedUnity baselines. Full bit equality is **not** claimed: the .NET harness and Unity differ in a few float path coordinates by at most1.18e-7 and in native float-string formatting; the raw typedJSON parity check recordsDIFF. Actual Unity verification remains required before publication.

| Policy | All15 Follow TP/FP/FN | Follow F1 | Jingle TP/FP/FN | Jingle F1 |
|---|---:|---:|---:|---:|
| V11 baseline |25/0/53|.4854|4/0/4|.6667|
| Long-Hold veto only |26/0/52|.5000|5/0/3|.7692|
| Veto + targeted rising-body priority |28/1/50|.5234|7/1/1|.8750|

Follow matching is one-to-one maximum-cardinality with start≤80ms, end≤150ms and IoU≥.5. The final policy preserves all19 CanCan Follow cues and Unstoppable14Hold/2Follow/7BigJump. Aaj long-Hold rejection removes four false supports; its total Hold count changes11→7. No other recording gains a new response proposal. Jingle BigJumps change7→4; all three removed launches occur at the two original Follow intervals and the one original Hold interval above. Jingle has42 original BigJumps elsewhere; no blanket BigJump rejection is claimed.

`FullPipeline/metrics.json` contains all per-song counts, aggregate scores and native parity differences. `final-full-pipeline-all15.txt` records all45 geometry passes. `final-fixture-green.txt` verifies the final frozen APIs and both new fixture suites. `red.txt` records the failing preimplementation articulated-Hold counterexample. All proposals and paths are gameplay policy, not reconstruction of authored choreography.

The remaining Jingle Follow at27.149→27.795 has mismatched neural/physical duration and continued same-pitch voicing. No duration nudge or annotation endpoint is used to force that match. Baby, LABUBU, Clouds and Canon unresolved families remain documented research gaps.
