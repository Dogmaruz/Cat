# Run from DancingCatsClone after TikoTaChartAuthoring.ExportPcm() in the open Unity Editor.
$ErrorActionPreference = 'Stop'
$tikoPython = 'C:/Users/Derek/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$tikoSha = '1a6cb3cb9cb45d5cd07562318bb005804580c9db6e778c5cbb06f2bc5945ae5a'
$tikoPcm = "Library/VocalAnalysis/$tikoSha"
$tikoOutput = 'Library/TikoTaChart/Audio'
$tikoSource = 'Assets/Resources/CustomAudio/Music/TikoTa.mp3'
$tikoDeps = 'Library/OriginalChartCalibration/v4/ml-deps'
$tikoModels = 'Library/OriginalChartCalibration/v4/stem-model'

& $tikoPython ../Tools/RhythmAnalysis/analyze_vocal_holds.py --input $tikoSource --decoded-pcm "$tikoPcm/source.f32" --decoded-metadata "$tikoPcm/source.json" --output "$tikoOutput/$tikoSha.voice.json" --deps $tikoDeps --cache-dir "$tikoOutput/VocalAnalysis" --model-dir $tikoModels --diagnostics "$tikoOutput/holds.json"
if ($LASTEXITCODE -ne 0) { throw 'Vocal analysis failed' }
& $tikoPython ../Tools/RhythmAnalysis/analyze_instrumental_notes.py --input $tikoSource --decoded-pcm "$tikoPcm/source.f32" --decoded-metadata "$tikoPcm/source.json" --output "$tikoOutput/$tikoSha.notes.json" --deps $tikoDeps --cache-dir "$tikoOutput/InstrumentAnalysis" --model-dir $tikoModels
if ($LASTEXITCODE -ne 0) { throw 'Instrumental analysis failed' }
# Publish only these source-specific files into their matching Editor evidence folders.
# The shared generator validates their hashes against the actual imported PCM.
