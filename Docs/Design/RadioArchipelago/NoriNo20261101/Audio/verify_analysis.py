"""Validate only Nori-no's fresh, local audio evidence and retain its provenance."""
from pathlib import Path
import hashlib
import importlib.util
import json
import sys
import time

ROOT = next(parent / 'DancingCatsClone' for parent in Path(__file__).resolve().parents
            if (parent / 'DancingCatsClone/Assets').is_dir())
sys.path.insert(0, str(ROOT / 'Library/OriginalChartCalibration/v4/ml-deps'))
SOURCE_SHA = '250a2d506d581fa11c1c290680b1ec1bfb4c5b0543abdec2170de3a65b19bbf6'
OUT = ROOT / 'Library/NoriNoChart/Audio'
source = ROOT / 'Assets/Resources/CustomAudio/Music/NoriNo.mp3'
pcm_folder = ROOT / 'Library/VocalAnalysis' / SOURCE_SHA
metadata = json.loads((pcm_folder / 'source.json').read_text())

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def load_tool(name):
    path = ROOT.parent / 'Tools/RhythmAnalysis' / (name + '.py')
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

voice_path = OUT / (SOURCE_SHA + '.voice.json')
instrument_path = OUT / (SOURCE_SHA + '.notes.json')
voice = json.loads(voice_path.read_text())
instrument = json.loads(instrument_path.read_text())
voice_tool = load_tool('analyze_vocal_holds')
instrument_tool = load_tool('analyze_instrumental_notes')
assert sha(source) == SOURCE_SHA == metadata['sourceFileSha256']
assert sha(pcm_folder / 'source.f32') == metadata['sourcePcmSha256']
for evidence in (voice, instrument):
    assert evidence['audioUploaded'] is False
    for key in ('sourceFileSha256', 'sourcePcmSha256', 'sampleRate', 'channels', 'sampleFrames'):
        assert evidence[key] == metadata[key], key
assert voice['featureSha256'] == voice_tool.evidence_digest(voice)
assert instrument['featureSha256'] == instrument_tool.evidence_digest(instrument)
models = ROOT / 'Library/OriginalChartCalibration/v4/stem-model'
assert sha(models / 'Kim_Vocal_2.onnx') == voice_tool.SEP_SHA == voice['separatorModelSha256']
assert sha(models / 'nmp.onnx') == voice_tool.NOTE_SHA == voice['noteModelSha256'] == instrument['noteModelSha256']
assert voice['schemaVersion'] == 2
assert voice['attackAnalysisVersion'] == 'stem-high-flux-1'
assert instrument['analysisVersion'] == 'mixed-note-events-2-candidates8'
assert instrument['timingVersion'] == 'input-window-time-v2'
assert len(instrument['featureNames']) == 22 and instrument['events']
duration = metadata['sampleFrames'] / metadata['sampleRate']
assert abs(len(voice['rms']) * voice['rmsStep'] - duration) < .03
assert voice['pitchTimes'][-1] >= duration - .5
assert 0 <= voice['pitchTimes'][0] < .03
assert all(0 <= event['time'] < event['endTime'] <= duration + 1e-6 for event in instrument['events'])
assert all(0 <= attack <= duration for attack in voice['vocalAttackTimes'])
report = dict(
    passed=True, verifiedAtUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
    sourceFileSha256=SOURCE_SHA, sourcePcmSha256=metadata['sourcePcmSha256'],
    duration=duration, requestedBpm=128, sampleRate=metadata['sampleRate'], channels=metadata['channels'],
    sampleFrames=metadata['sampleFrames'], voiceFeatureSha256=voice['featureSha256'],
    instrumentalFeatureSha256=instrument['featureSha256'],
    voiceEvidenceFileSha256=sha(voice_path), instrumentalEvidenceFileSha256=sha(instrument_path),
    voiceSchema=voice['schemaVersion'], voiceAnalysis=voice['analysisVersion'],
    vocalPitchFrames=len(voice['pitchTimes']), vocalRmsFrames=len(voice['rms']),
    vocalEntries=len(voice['voiceStarts']), vocalAttacks=len(voice['vocalAttackTimes']),
    vocalPitchTimespan=[voice['pitchTimes'][0], voice['pitchTimes'][-1]],
    vocalRmsCoverageErrorSeconds=abs(len(voice['rms']) * voice['rmsStep'] - duration),
    vocalAttackTimespan=[voice['vocalAttackTimes'][0], voice['vocalAttackTimes'][-1]],
    instrumentalAnalysis=instrument['analysisVersion'], instrumentalEvents=len(instrument['events']),
    instrumentalEventsTimespan=[instrument['events'][0]['time'], max(e['endTime'] for e in instrument['events'])],
    separatorModelSha256=voice['separatorModelSha256'], noteModelSha256=voice['noteModelSha256'],
    analysisScriptSha256={name:sha(ROOT.parent/'Tools/RhythmAnalysis'/name) for name in ('analyze_vocal_holds.py', 'analyze_instrumental_notes.py')},
    audioUploaded=False,
    limitation='Source-separated voice and inferred notes are measured estimates; no human listening was performed by this analysis.'
)
(OUT/'analysis.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf8')
print(json.dumps(report, indent=2))
