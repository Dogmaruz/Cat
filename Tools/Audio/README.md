# Musical chart authoring

The playable game remains offline. These Python tools run outside Unity to produce editable evidence. Unity compiles the resulting score into its existing timeline schema. No Python, ONNX model or separated audio is included in the player.

The current score is `DancingCatsClone/Assets/Clone/Editor/MusicalScores/AlwaysRememberMeowCat.score.json`. It retains all307 estimated lead intervals,430 measured full-mix attacks, source/model hashes and the explicit authoring choices. JSON `notes` and `authoring` retain evidence; `cues` are the225 chosen musical actions. Unity ignores the extra analysis fields. The20 Holds include two explicit instrumental chords; three Follow passages are editorial choices. Confidence is a heuristic, not a probability.

To reproduce the accepted structure, from the repository root:

```powershell
python Tools/Audio/author_score.py --evidence DancingCatsClone/Assets/Clone/Editor/MusicalScores/AlwaysRememberMeowCat.score.json --audio DancingCatsClone/Assets/Resources/CustomAudio/Music/AlwaysRememberMeowCat.mp3 --output Tools/Audio/work/reproduced.score.json
python -m unittest discover -s Tools/Audio -p "test_*.py" -v
```

This step uses Python's standard library only and checks the source audio hash. Copy reviewed changes to the Editor score and use `Dancing Cats > Generate Always Remember Meow Chart`. Explicit `authoring.manualCues` and `followSpans` allow listening edits without rerunning learned models. A score edit followed by the Unity menu regenerates tile positions, lengths, native path markers and beat checkpoints. The compiler preserves audio/game time mapping and runtime movement.

The authoring policy selects strong note attacks without snapping them to a beat grid, retains stable estimated notes as Holds, and translates pitch to lanes. It uses actual accompaniment attacks for necessary traversal through lead gaps, refusing a gap if no measured support exists. It never inserts periodic scored beats into silence. Custom air policy is .18–1.95s; Follow points in this score use at least.12s and at most2.2 lane units/s. These are chosen easier authoring constraints, not claimed engine limits. Timing comes from `authoring.forwardSpeed` and `delayTime`; changing them can require revising explicit supports near the finish.

## Re-analyzing a recording

Use a separate Python3.12 environment (tested with3.12.14):

```powershell
python -m venv Tools/Audio/.venv
Tools/Audio/.venv/Scripts/python -m pip install -r Tools/Audio/requirements-analysis.txt
Tools/Audio/.venv/Scripts/python -m pip install --no-deps basic-pitch==0.4.0
```

The tested path uses ONNX Runtime. TensorFlow and Torch are unnecessary. BasicPitch's dependency metadata may report TensorFlow/resampy constraints; the explicitly selected ONNX execution path was tested with the versions above. `ANALYSIS_EVIDENCE.txt` records the actual session measurements and limitations.

Download the [UVR Voc_FT model](https://github.com/TRvlvr/model_repo/releases/download/all_public_uvr_models/UVR-MDX-NET-Voc_FT.onnx) separately into ignored `models/`. Expected full SHA-256 is `534b2070fcc7df514b13ef660dc8cbb328679c2374d04354a5c42bb14ecce111`. The checked-in metadata is the matching entry from [UVR model metadata](https://github.com/TRvlvr/application_data/blob/main/mdx_model_data/model_data_new.json). The adapter checks UVR's tail-MD5, input shape and a full-bin STFT round trip before inference. Audio input must be44.1kHz stereo WAV; decode/resample explicitly without changing its timeline.

```powershell
python Tools/Audio/separate_onnx.py --audio full.wav --model Tools/Audio/models/UVR-MDX-NET-Voc_FT.onnx --model-data Tools/Audio/uvr_model_data.json --out Tools/Audio/work/stems
python Tools/Audio/analyze_stem.py --audio Tools/Audio/work/stems/vocals_mdx.wav --out Tools/Audio/work/notes --source-score source.score.json
python Tools/Audio/support_onsets.py --audio full.wav --residual Tools/Audio/work/stems/instrumental_residual.wav --out Tools/Audio/work/support_onsets.json
```

`source.score.json` supplies the exact imported source id/SHA-256, duration, sample rate, measured `bpm`/`beats` and optional coarse `phrases`; do not reuse another song's header. For this song that header is retained in its committed score. New recordings still require measuring/checking their pulse and choosing phrase actions. The analyzer emits candidates, not a complete automatically accepted level. Its source-identity, vocal-source, note and phrase estimates require review. No automatic chorus recognition is claimed.

The current musical comparison was prepared from14–44s with distinct markers for landings, Hold checkpoints and Follow markers. Full-song autoplay verifies contacts and completion; it cannot establish whether the phrasing feels natural. This session did not have direct audio input, so final musical taste must be judged by listening.

Primary algorithm references: [UVR MDX inference](https://github.com/Anjok07/ultimatevocalremovergui/blob/master/separate.py), [UVR STFT](https://github.com/Anjok07/ultimatevocalremovergui/blob/master/lib_v5/tfc_tdf_v3.py), [Spotify BasicPitch](https://github.com/spotify/basic-pitch), [librosa pYIN](https://librosa.org/doc/0.11.0/generated/librosa.pyin.html).
