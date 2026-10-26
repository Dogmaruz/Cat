# Local vocal evidence for rhythm charts

This optional Editor preprocessing pipeline separates the foreground voice locally,
then measures pitch, voiced confidence, vocal onsets and a 100 Hz RMS envelope.
The Unity classifier consumes those measurements and the current recording's PCM
attacks. Neither stage reads a reference level or uses song identifiers.

Install the small Python runtime dependencies into an ignored directory:

```powershell
python -m pip install --target DancingCatsClone/Library/RhythmAnalysis/deps -r Tools/RhythmAnalysis/requirements.txt
```

Generate evidence for any mono/stereo WAV. Name the output after the SHA-256 of
the source file, inside `DancingCatsClone/Assets/Clone/Editor/AudioEvidence/`:

```powershell
python Tools/RhythmAnalysis/analyze_vocal_holds.py `
  --input path/to/song.wav `
  --output DancingCatsClone/Assets/Clone/Editor/AudioEvidence/SOURCE_FILE_SHA256.voice.json `
  --deps DancingCatsClone/Library/RhythmAnalysis/deps `
  --cache-dir DancingCatsClone/Library/RhythmAnalysis `
  --download-models `
  --diagnostics DancingCatsClone/Library/RhythmAnalysis/holds.json
```

`--download-models` downloads two public models, approximately 64 MB and 230 KB,
and verifies pinned SHA-256 values. Audio never leaves the machine. Existing local
model files can instead be supplied with `--model-dir`. No Unity package, Torch,
TensorFlow, CUDA installation, paid service or ffmpeg is required.

Intermediate waveforms/notes and model weights stay in the ignored cache. Caches
are addressed by the decoded, interleaved little-endian float32 PCM SHA-256. The
portable evidence records source-file and PCM hashes, model hashes and a binary
integrity digest covering its arrays. The digest writes numeric features as
little-endian signed 64-bit integers at 1e-8 precision, avoiding JSON parser
rounding differences across Python, .NET and Unity. Unity rejects mismatched or damaged evidence.
Pitch retains native Basic Pitch frame times (approximately 86 Hz); RMS is 100 Hz.

New preprocessing uses `vocal-phrase-2-window-time`. Each retained pitch frame
is placed from its actual 22.05 kHz input-window start, native 256-sample frame
position and initial 3840-sample padding. The 43844-sample window advances by
36164 samples; trimming 15 frames at either edge retains 142 predictions. This
means a seam is not a uniform flattened-frame interval. Only predictions whose
centers fall within the source recording are retained, including its final
partial window. No duration stretching or extrapolated pitch values are used.

The corrected notes are cached as `notes-input-window-time-v2.npz`, with a
checked timing-version tag, so historical `notes.npz` cannot silently supply
the old time axis. The expensive `vocals.npz` separation cache is reusable.
The classic pitch/hold group in committed Unstoppable evidence remains the
historical v1 measurement for compatibility. Its schema2 extension described
below adds independent attacks on the corrected v2 clock; the old group is not
silently recomputed or relabeled.

Schema2 adds `attackAnalysisVersion=stem-high-flux-1`, `vocalAttackTimes` and
`vocalAttackConfidence`. Attacks come from high-band spectral flux in the actual
separated voice, with native-window note-onset confidence around each attack.
The digest has a separate schema2 domain and covers the new version and arrays.
Schema1 hashes remain byte-compatible. Unity validates finite sorted times,
array lengths, confidence ranges, model/analysis provenance and integrity.
Absent or unconfident attack features preserve conservative physical routing.

Optional `--base-evidence VERIFIED.voice.json` preserves an existing classic
pitch/RMS/voice-start group while adding newly measured attacks. It verifies
the old digest, actual source/PCM/rate/channel/frame metadata and pinned model
hashes before combining groups. This is an explicit compatibility mode, not a
claim that legacy pitch measurements were recomputed on the new clock.
Unstoppable uses `vocal-phrase-1+stem-high-flux-1`; the custom song and three v8
benchmark recordings use `vocal-phrase-2-window-time+stem-high-flux-1`.

The measured old/new interior clock difference on Unstoppable is about
−8.24 to +10.23 ms. The old tail deficit (about .460 s on Unstoppable and
1.061 s on the custom recording) mainly came from truncating row counts at
86 fps. It must not be described as that much accumulated interior drift.

For Unity audio imported with Vorbis or another lossy codec, analyze the actual
`AudioClip.GetData` PCM rather than the original WAV decoder output. The Editor
command **Assets → Dancing Cats → Export selected audio PCM for vocal analysis**
produces interleaved float32 little-endian bytes plus JSON containing
`sourceFileSha256`, `sourcePcmSha256`, `sampleRate`, `channels`, `sampleFrames`.
Supply them with `--decoded-pcm source.f32 --decoded-metadata source.json`, keeping
`--input` pointed at the original asset for lookup/provenance. Both hashes and the
byte length are verified before inference. This preserves import settings and
lets Unity strictly validate that the analyzed audio is the audio being played.

The classifier recognizes continuous legato, plateau releases, returning ornaments
and brief pickups into the measured upper voice register. Brief pitch uncertainty
is bridged only when the separated voice is still audible. A weakening semitone
release ends the main note instead of following its uncertain tail.
Pickups and falling-glide supports also require measured vocal energy up to their
endpoint: a continuous 60 ms below 10% of the phrase RMS peak rejects them. A
0.005 peak floor prevents silent evidence from supporting a confident pitch trace.

Two additional musical support policies use the generator's measured pulse period:
an upper cadence may fill a genuine rest before a lower-register answer; a falling
glide reserves an airborne handoff to an independently confirmed following support.
Those endpoints are game phrasing decisions, not claims of continuous vocal sound.
They are disabled when no period is provided. Unity passes `analysis.period`
automatically. To reproduce those proposals in standalone `--diagnostics`, also
pass `--beat-period MEASURED_SECONDS` (0.52 for the current calibration recording).
This argument changes proposals only; the source measurements and cache remain
the same.

The v6 calibration recording produced 14 candidates matching all 14 reference Hold
intervals, with start/end tolerances 80/150 ms and no extra candidates. The previous
v5 found 7 of 14. This is repeated tuning on a single recording, not an accuracy
claim for unseen songs or a claim that every tile type/path is reconstructed.
The reference map is used only in a separate evaluation, not as an inference input.

Run shared classifier counterexamples with:

```powershell
python Tools/RhythmAnalysis/test_vocal_holds.py --deps DancingCatsClone/Library/RhythmAnalysis/deps -v
python Tools/RhythmAnalysis/test_transcribe_timing.py --deps DancingCatsClone/Library/RhythmAnalysis/deps -v
python Tools/RhythmAnalysis/test_foreground_evidence.py --deps DancingCatsClone/Library/RhythmAnalysis/deps -v
```

The timing tests use known native-frame coordinates to check an entire
157-second recording, a marker in a late input window, every stitching boundary
and a short final partial window. They test transform timing independently of
the learned model's pitch or onset accuracy.

The 26 cases in `gesture_fixtures.json` are also checked by Unity's
`PcmVocalGestureValidation.RunChecks()`. They contrast real silence with pitch
ambiguity, ornaments with vibrato, pickups with ordinary high notes, and measured
rests/handoffs with ambiguous alternatives, including real 180 ms pauses and
fully silent RMS traces in pickups and glides. They test classification rules using
synthetic measurements; they do not measure neural source separation quality.

V8 adds physical tests of actual FFT attacks, anti-phase stereo, native-window
confidence timing, silence and schema integrity. Unity also checks conservative
foreground filtering, rapid vocal/instrumental runs and 28 flight counterexamples.
The wider paired/frozen evaluation and its substantial remaining support-type
errors are recorded in `Docs/Clone/ChartCalibration/V8/README.md`.

CPU preprocessing took approximately 31 seconds for separation plus 1.2 seconds
for note inference on the 67-second calibration recording. Unity consumes the
compact evidence without any ML inference at runtime. See THIRD_PARTY_NOTICES.md.

The independent spatial route study is reproducible with
`python Tools/RhythmAnalysis/study_chart_routes.py` from the repository root.
It reads the 58 original chart assets, verifies their hashes and writes only
`DancingCatsClone/Library/ChartChoreographyV1/Reference/study.json`.
Its aggregate transition measurements inform the separate `ChartRouteComposer`
policy; no reference chart coordinates or song identity are read by that composer.
Current published routes and actual Editor checks are documented in
`Docs/Clone/ChartChoreography/README.md`.

## V9 mixed-note evidence and experimental short supports

Export the selected Unity audio with **Assets → Dancing Cats → Export selected
audio PCM for vocal analysis** first. Use its actual `AudioClip.GetData` bytes and
metadata, keeping `--input` pointed at the original asset. From the repository
root, this example uses the existing dependencies and pinned local model:

```powershell
$sourceAsset = 'DancingCatsClone/Assets/Resources/OriginalAudio/Music/CanCan__-6816999517090791341.ogg'
$sourceSha = (Get-FileHash -LiteralPath $sourceAsset -Algorithm SHA256).Hash.ToLowerInvariant()
$decodedFolder = "DancingCatsClone/Library/VocalAnalysis/$sourceSha"
python Tools/RhythmAnalysis/analyze_instrumental_notes.py `
  --input $sourceAsset `
  --decoded-pcm "$decodedFolder/source.f32" `
  --decoded-metadata "$decodedFolder/source.json" `
  --output "DancingCatsClone/Assets/Clone/Editor/InstrumentEvidence/$sourceSha.notes.json" `
  --cache-dir DancingCatsClone/Library/ChartGenerationV9/InstrumentAnalysis `
  --model-dir DancingCatsClone/Library/OriginalChartCalibration/v4/stem-model `
  --deps DancingCatsClone/Library/OriginalChartCalibration/v4/ml-deps
```

All six path arguments above are required; `--model-dir` must contain the existing
SHA-pinned `nmp.onnx`. The producer verifies source/PCM hashes and metadata before
inference. Model inference runs separately per channel and combines activations;
spectral analysis combines magnitudes, preserving anti-phase stereo evidence.
The output has 22 named note/context features on the native input-window clock.
Unity checks their names/order, provenance, values and quantized integrity digest
against the actual source PCM. No reference map enters measurement or inference.

To reproduce the separate offline short-Hold experiment, the six calibration
recordings' committed instrument/vocal evidence and reference labels must exist:

```powershell
python Tools/RhythmAnalysis/train_short_support_style.py `
  --deps DancingCatsClone/Library/OriginalChartCalibration/v4/ml-deps `
  --output DancingCatsClone/Library/ChartGenerationV9/training-proof `
  --leave-one-song-out
```

Only this training step reads calibration labels. It writes its recipe and
leave-one-song-out report inside ignored `Library`; it does not publish maps or
enable a policy. Generalization was insufficient, so
`includeExperimentalShortSupports` remains `false` by default. The four v9 test
maps explicitly opt in. See the [v9 results and limitations](../../Docs/Clone/ChartCalibration/V9/README.md)
for the first frozen evaluation, typed interval metrics and complete Editor runs.

## V10 candidate coverage and interval-relative release experiment

The algorithm was frozen before the first heldout comparison. All six published
test maps passed fresh full real-contact Unity playthroughs; Can Can v10 is left
selected in the Play Mode menu. The current producer emits `mixed-note-events-2-candidates8`:
up to eight pitch candidates per attack instead of v1's four, with the same 22
feature names and native-window timing. Source PCM, voice measurements and pinned
note model are paired; the resulting candidate events/digests are not identical.
Archived v1 measurements remain in
`DancingCatsClone/Library/ChartGenerationV10/before/measurements`, paired by
`measurement-transition.json`. The v9 example above describes the historical v1
iteration; rerunning the current producer creates v2, not an unchanged v1 replay.

Use the same Unity PCM export and local dependencies as above. A separate v10
cache keeps this run's intermediates apart:

```powershell
$sourceAsset = 'DancingCatsClone/Assets/Resources/OriginalAudio/Music/CanCan__-6816999517090791341.ogg'
$sourceSha = (Get-FileHash -LiteralPath $sourceAsset -Algorithm SHA256).Hash.ToLowerInvariant()
$decodedFolder = "DancingCatsClone/Library/VocalAnalysis/$sourceSha"
python Tools/RhythmAnalysis/analyze_instrumental_notes.py `
  --input $sourceAsset `
  --decoded-pcm "$decodedFolder/source.f32" `
  --decoded-metadata "$decodedFolder/source.json" `
  --output "DancingCatsClone/Assets/Clone/Editor/InstrumentEvidence/$sourceSha.notes.json" `
  --cache-dir DancingCatsClone/Library/ChartGenerationV10/InstrumentAnalysis `
  --model-dir DancingCatsClone/Library/OriginalChartCalibration/v4/stem-model `
  --deps DancingCatsClone/Library/OriginalChartCalibration/v4/ml-deps
```

The new trainer uses the eight already studied recordings only. It reads their
labels offline, verifies paired provenance, and writes proof inside `Library`:

```powershell
python Tools/RhythmAnalysis/train_short_support_release.py `
  --deps DancingCatsClone/Library/OriginalChartCalibration/v4/ml-deps `
  --output DancingCatsClone/Library/ChartGenerationV10/ShortHolds/training-proof `
  --leave-one-song-out
python Tools/RhythmAnalysis/test_short_support_release.py `
  --deps DancingCatsClone/Library/OriginalChartCalibration/v4/ml-deps -v
```

The release helper measures vocal body/release relative to the selected endpoint;
its release window ends at exit +75 ms. This is not a claim that the entire
classifier is causal or bounded to that window: wider instrumental/context
features and whole-recording RMS normalization still apply. The tests cover this
helper and strict interval matching, not universal musical classification.
References remain absent from source measurement and Unity inference. The policy
stays experimental/default-off: the known-song gain does not establish unseen
generalization, and the leave-one-song-out result still contains excess false
proposals. See the [v10 results](../../Docs/Clone/ChartCalibration/V10/README.md)
for paired typed metrics, first heldout limitations and six complete runtime reports.
