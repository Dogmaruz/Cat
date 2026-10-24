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

For Unity audio imported with Vorbis or another lossy codec, analyze the actual
`AudioClip.GetData` PCM rather than the original WAV decoder output. The Editor
command **Assets → Dancing Cats → Export selected audio PCM for vocal analysis**
produces interleaved float32 little-endian bytes plus JSON containing
`sourceFileSha256`, `sourcePcmSha256`, `sampleRate`, `channels`, `sampleFrames`.
Supply them with `--decoded-pcm source.f32 --decoded-metadata source.json`, keeping
`--input` pointed at the original asset for lookup/provenance. Both hashes and the
byte length are verified before inference. This preserves import settings and
lets Unity strictly validate that the analyzed audio is the audio being played.

The classifier conservatively recognizes a vocal plateau followed by downward
release, or a continuous descending legato phrase. It does not attempt to recover
every artistic decision in a manually authored rhythm map. The first calibration
recording produced 7 candidates matching 7 of 14 reference Hold intervals, with
start/end tolerances 80/150 ms and no extra candidates. This is a single-recording
calibration result, not an accuracy claim for unseen songs. The reference map was
used only in a separate evaluation; it is not an inference input.

CPU preprocessing took approximately 31 seconds for separation plus 1.2 seconds
for note inference on the 67-second calibration recording. Unity consumes the
compact evidence without any ML inference at runtime. See THIRD_PARTY_NOTICES.md.
