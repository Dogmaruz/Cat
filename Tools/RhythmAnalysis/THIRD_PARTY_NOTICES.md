# Third-party models and transform contracts

## Ultimate Vocal Remover / Kim Vocal 2

Credit: Ultimate Vocal Remover, Anjok07, aufr33 and UVR model contributors.
Kim Vocal 2 is downloaded from UVR's public model repository, referenced by the
official UVR application model list. The official README states that UVR code is
MIT licensed and asks third-party developers using its models to credit UVR and
its developers. No model weights are committed in this repository.

Primary sources inspected before downloading:

- https://github.com/Anjok07/ultimatevocalremovergui
- https://raw.githubusercontent.com/TRvlvr/application_data/main/filelists/download_checks.json
- https://raw.githubusercontent.com/TRvlvr/application_data/main/mdx_model_data/model_data_new.json
- https://github.com/TRvlvr/model_repo/releases/tag/all_public_uvr_models

Pinned model: `Kim_Vocal_2.onnx`, 66,759,214 bytes.
SHA-256: `ce74ef3b6a6024ce44211a07be9cf8bc6d87728cc852a68ab34eb8e58cde9c8b`.
UVR configuration hash (MD5 of trailing 10,240,000 bytes):
`970b3f9492014d18fefeedfe4773cb42`.
Model contract: FFT 7680, hop 1024, 3072 frequency bins, 256 frames,
stereo real/imaginary channels, vocal output compensation 1.009.

The local NumPy STFT/overlap-add implementation follows the published input/output
contract also documented by the MIT-licensed python-audio-separator project:

- https://github.com/nomadkaraoke/python-audio-separator
- https://raw.githubusercontent.com/nomadkaraoke/python-audio-separator/main/audio_separator/separator/architectures/mdx_separator.py
- https://raw.githubusercontent.com/nomadkaraoke/python-audio-separator/main/audio_separator/separator/uvr_lib_v5/stft.py

## Spotify Basic Pitch

Basic Pitch, Copyright 2022 Spotify AB. Apache License 2.0.
Official source: https://github.com/spotify/basic-pitch
The complete upstream LICENSE and NOTICE are included beside this document.

Pinned ICASSP 2022 ONNX model: `nmp.onnx`, 230,444 bytes.
SHA-256: `2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec`.
Inference follows the official 22.05 kHz input, overlapping audio windows,
15-frame edge trimming and frame-time correction contract. The Basic Pitch
Python package is not imported; only the official ONNX graph is executed.

## Dependency licenses

NumPy and SciPy use BSD licenses. ONNX Runtime uses the MIT license. Dependency
installations retain their own license files in the isolated pip target directory.
