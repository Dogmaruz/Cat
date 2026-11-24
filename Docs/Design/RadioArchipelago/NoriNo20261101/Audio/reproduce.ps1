# Run from DancingCatsClone after NoriNoChartAuthoring.ExportPcm() in the open Unity Editor.
$ErrorActionPreference = 'Stop'
$noriPython = 'C:/Users/Derek/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$noriSha = '250a2d506d581fa11c1c290680b1ec1bfb4c5b0543abdec2170de3a65b19bbf6'
$noriPcm = "Library/VocalAnalysis/$noriSha"
$noriOutput = 'Library/NoriNoChart/Audio'
$noriSource = 'Assets/Resources/CustomAudio/Music/NoriNo.mp3'
$noriDeps = 'Library/OriginalChartCalibration/v4/ml-deps'
$noriModels = 'Library/OriginalChartCalibration/v4/stem-model'

& $noriPython ../Tools/RhythmAnalysis/analyze_vocal_holds.py --input $noriSource --decoded-pcm "$noriPcm/source.f32" --decoded-metadata "$noriPcm/source.json" --output "$noriOutput/$noriSha.voice.json" --deps $noriDeps --cache-dir "$noriOutput/VocalAnalysis" --model-dir $noriModels --diagnostics "$noriOutput/holds.json"
if ($LASTEXITCODE -ne 0) { throw 'Vocal analysis failed' }
& $noriPython ../Tools/RhythmAnalysis/analyze_instrumental_notes.py --input $noriSource --decoded-pcm "$noriPcm/source.f32" --decoded-metadata "$noriPcm/source.json" --output "$noriOutput/$noriSha.notes.json" --deps $noriDeps --cache-dir "$noriOutput/InstrumentAnalysis" --model-dir $noriModels
if ($LASTEXITCODE -ne 0) { throw 'Instrumental analysis failed' }
# Publish only these source-specific files into their matching Editor evidence folders.
# The shared generator validates their hashes against the actual imported PCM.
