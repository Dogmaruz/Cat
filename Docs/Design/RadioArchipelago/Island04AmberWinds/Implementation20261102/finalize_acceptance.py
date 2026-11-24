"""Package final evidence without touching Unity assets or rerunning gameplay."""
from pathlib import Path
import hashlib
import json
import shutil

root = Path.cwd()
folder = Path(__file__).parent
out = folder / 'FinalAcceptance'
native = root / 'DancingCatsClone/Library/RadioArchipelago/AmberWinds'
water = native / 'WaterNative'

for name in ('direction-aggregate.json', 'direction-aggregate.md',
             'stream-direction.json', 'stream-direction-roi.json',
             'roll210-direction.json', 'side-roll-direction.json',
             'side-roll-direction-roi.json', 'waterfall-direction.json'):
    shutil.copy2(water / name, out / name)
shutil.copy2(root / 'Docs/Design/RadioArchipelago/FlowingWater20261102/native-c565-footprints.json', out / 'native-c565-footprints.json')
shutil.copy2(folder / 'root-preservation.json', out / 'root-preservation.json')
shutil.copy2(root / 'DancingCatsClone/Library/MusicalChartReview/response.json', out / 'editor-final-state.json')

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

play = read(native / 'PlayReview/play-review.json')
terrain = read(native / 'terrain-clearance.json')
island = read(native / 'island-checks.json')
menu = read(out / 'menu-checks.json')
menu_live = read(out / 'menu-live-state.json')
editor = read(out / 'editor-final-state.json')
assert play['pass'] and play['won'] and not play['boundedReview']
assert play['scoredScoreContacts'] == play['requiredScoreContacts'] == 346
assert play['errors'] == [] and play['inputRestored'] and play['profileRestored']
assert island['passed'] and island['coralPreservationGuard'] and island['moonPreservationGuard']
assert terrain['passed'] and terrain['corridorViolations'] == terrain['tileViolations'] == terrain['structureViolations'] == 0
assert menu['passed'] and menu_live['state'] == 'passed'
assert menu['visibleIds'] == ['LumiLai', 'TikoTa', 'NoriNo', 'RadioTrio']
assert editor['success'] and not editor['playing'] and not editor['compiling'] and not editor['sceneDirty']

for feature in ('roll210', 'waterfall'):
    direction = read(out / (feature + '-direction.json'))
    assert direction['pass'] is True
    assert len(direction['samples']) == 3
    assert all(row['status'] == 'confirmed downstream' for row in direction['samples'])

hashes = read(out / 'source-hashes.json')
for path, expected in hashes.items():
    assert hashlib.sha256((root / path).read_bytes()).hexdigest() == expected, path

code_paths = {}
for p in (folder / 'UnityStaging').glob('*.cs'):
    sub = 'Runtime/UI' if p.name in ('RadioIslandMenuPolicy.cs', 'RadioIslandMenuCatalog.cs', 'RadioMenuPresenter.cs') else 'Editor'
    installed = root / 'DancingCatsClone/Assets/RadioArchipelago' / sub / p.name
    assert p.read_bytes() == installed.read_bytes(), p.name
    code_paths[p.name] = hashlib.sha256(p.read_bytes()).hexdigest()
(folder / 'staged-code-hashes.json').write_text(json.dumps(code_paths, indent=2) + '\n', encoding='utf-8')

summary = {
    'integrationPassed': True,
    'sourceFbxSha256': 'c5656b305295a74bffc93d0428b111a95e6768e0ef42b9cd956ec612ac88fc3a',
    'approvedSong': 'RadioTrio',
    'durationSeconds': 85.76,
    'scene': island['scene'],
    'fullNativeRun': {key: play[key] for key in ('runId', 'boundedReview', 'pass', 'won', 'scoredScoreContacts', 'requiredScoreContacts', 'requiredHolds', 'requiredMovingTiles', 'requiredBigJumps', 'coinsCollectedOnGround', 'departureComplete', 'inputRestored', 'profileRestored', 'actualGeometryClips', 'actorOffscreenFrames', 'nextLandingOffscreenFrames', 'errors')},
    'terrain': {key: terrain[key] for key in ('corridorSamples', 'tileSamples', 'structureSamples', 'corridorViolations', 'tileViolations', 'structureViolations', 'actualFinishFloorSamples')},
    'menu': {'visible': menu['visibleIds'], 'underlyingCount': menu['underlyingCatalogCount'], 'hiddenCount': menu['hiddenCount'], 'nativeLivePass': True},
    'earlierIslandGuards': {'coral': True, 'moon': True},
    'waterDirectionEvidence': {
        'runId': '20261101-234211-5093157',
        'r3Lip35_48_65Confirmed': True,
        'newSideCurtainUpperMiddleLowerConfirmed': True,
        'confirmedUpstreamSamples': 0,
        'allRequestedROIsConfirmed': False,
        'unproved': ['near-bank flat stream: mask-limited and smaller-patch speed inconsistent', 'side lip35: even 5x5 patch below90% water-mask coverage', 'side lip48: positive smaller-patch drift speed inconsistent with intended carrier', 'side lip65: no recognizable packets in smaller patch'],
        'limits': 'These small/edge ROIs remain inconclusive and are never counted as passes. All6 principal lip/full-height curtain points are independently reproduced downstream. No further capture or runtime tuning was performed to force ambiguous samples green.'
    },
    'editorCleanEditMode': True,
    'buildsCreated': False,
    'phonePerformanceMeasured': False,
    'verification': 'Native full-song run and artifact/hash checks;6 offline packet tracker fixtures and independent reanalysis of final R3/side-curtain frames.'
}
(out / 'acceptance-summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')

handoff = '''# Fourth-island integration handoff — 2026-11-02

The approved RadioTrio song and fourth island «Янтарные ветра» are integrated in Unity. The full native gameplay run passed on the final source. The Editor is left in clean Edit Mode on `Assets/RadioArchipelago/Scenes/AmberWindsPilot.unity`. No build was created.

## Delivered

- New scene, definition, prefab, materials, route/elevation/rotor/cloud contracts under `AmberWinds`; no earlier island scene, route, map, music, definition, prefab or material was replaced.
- Autumn yellow/orange/red scenery, three visibly rotating windmills, organic shore dressing, large separated clouds, stepped floating-island crossing, and a raised mountain waterfall beside the approach. Its upper/middle/base are visible from the normal fixed camera at155/170/185m. The formerly squared projected lip is a smooth roll; its animation seam was corrected through authored normals/colors without changing geometry or UVs.
- User-approved original RadioTrio,85.76s. Published catalog entry20005 and original native-PCM-based v12 generator output; no rejected WayoWa audio is used. Chart has317 full-size supports,18 holds and346 score contacts. The route-conditioned chart has97 moving platforms and preserves every scoring timestamp.
- Four visible menu/Atlas entries in order: LumiLai, TikoTa, NoriNo, RadioTrio. All80 other catalog records remain hidden; all25 previous custom raw song rows are preserved verbatim. Static and actual live menu navigation checks pass.

## Fixed interfaces and provenance

- Final imported/source FBX SHA256 `c5656b305295a74bffc93d0428b111a95e6768e0ef42b9cd956ec612ac88fc3a`;3,140,774triangles/278mesh renderers. All5 gameplay sidecars stayed byte-identical through the bounded shading correction.
- RadioTrio MP3 SHA256 `b3a7249d3f28984bf190d6498a2cadee7335d3bf61f6c22679e69e911d8759f6`; decoded PCM SHA256 `e238c81e97f83bd2e7c1889eb1f377769161dcb1f57fa96b04f1b8e08715c50e`,3,782,016frames/44,100Hz stereo. User acceptance explicitly forwarded by root before publish/binding.
- Shared water shader SHA256 `e5595cb7296dfb46056edf00727f290127ba817871a78869e7737291d9ea3337`. New Stream AND Waterfall use authored downstream-metre UVs, current4.2, flow.65, runtime clock−1. Fall fade is below the authored body. Old7 material files remain unchanged.
- Route774.995m, StartDistance10m, DistanceScale1.2, LateralScale1.8. Final takeoff675.028m/landing681.028m/end715.828m; exact straight begins662m. Cloud scale1.6 is new-island-only; old default1 retains earlier behavior.
- Eleven staged C# files match their installed versions. Existing IslandPilotPlayReview is in `RadioArchipelago/Editor`, not `Clone/Editor`. The bridge remains `Clone/Editor/MusicalChartEditorSession.cs`. Root owns the precise RadioTrio0-or1 exceptions in old Coral/Moon guards and shared cloud-size fields; water agent owns the shader and direction analyzer.

## Native verification

- `FinalAcceptance/full-native-play-review.json`: run20261101-234737-711, full unsimplified85.76s audio/map, InputSystem pointer only;346/346 score contacts,18 required holds,97 moving tiles,2 big jumps including finish, win,20 signal sparks collected above ground, complete departure. Profile/input restored, errors[], zero actual geometry clips, zero actor or next-landing offscreen frames. Fixed-camera distance error below8µm. Instrumented Editor frame timings are not phone performance measurements.
- `terrain-clearance.json`:55,488 corridor samples,64,697 tile samples,33,915 structure samples, zero violations;2,788 actual finish-floor samples. Both earlier-island preservation guards pass. Derived map/elevation hashes remained identical through the final source shading correction.
- `imported-seam-continuity.json`:15 matched endpoint pairs across3joints have normaldot1, color error0 and position error0; actual frozen vertex animation maximum endpoint deviation about15.73µm. Real alpha material is retained; the diagnostic opaque clone was never saved.
- `native-water-clock.json`: four sets of14 actual-time frames, exact timestamps and one current runId20261101-234211-5093157; preview clock−1 and original scene restored. GIFs use those frame intervals. Occlusion-aware `side-cascade-visibility.json` verifies full-height waterfall visibility from the normal camera.
- `direction-aggregate.md/json`: recognizable packets confirm downstream motion at all3 R3 roll slopes35/48/65 degrees and new side-curtain upper/middle/lower. No sample is confirmed upstream. Four additional flat/small-lip ROIs remain explicitly inconclusive because of mask coverage or carrier-speed/texture ambiguity. They are not counted as passes; see the standard and smaller-ROI reports. Six tracker fixtures pass, and the two principal native reports were independently reproduced after review.
- `root-preservation.json`: root audit404 byte-identical files/8 authorized changes/0 unexpected changes within412-file baseline; all25 old raw song rows preserved. Parent will perform the final audit after queue handoff.

## Evidence and operation

`FinalAcceptance/acceptance-summary.json` is the compact machine-readable handoff. Adjacent files include final actual route-camera views, representative actual gameplay frames, full reports, current source hashes, and temporal GIFs. PreviousNativeRevision folders are explicitly rejected/prior revisions, not final evidence.

Queue actions: `amber-open`, `amber-checks`, `amber-capture`, `amber-playtest`; chart-free `amber-scenery-create`/`amber-scenery-capture`; transient `amber-scenery-water-live`; cloned-only `amber-roll-alpha-compare`; `island-menu-playtest`. The scenery paths use an unsaved source-backed definition with Chart=null and no song binding. The gameplay action runs the actual approved scene. No further paid generation, build, commit or push was made by this task.
'''
(folder / 'INTEGRATION_HANDOFF.md').write_text(handoff, encoding='utf-8')
(out / 'README.md').write_text('''# Final native acceptance evidence

Start with `acceptance-summary.json` and `../INTEGRATION_HANDOFF.md`. These files refer to final source c5656 and shader e5595 only. `full-native-play-review.json` is the full, unsimplified gameplay run; gameplay PNGs are captured from that run. `portrait-route-*.png` are separate actual fixed-camera scenery previews, not fake gameplay.

The four native-flow GIFs contain14 real-time frames each, with durations derived from their actual timestamps. `direction-aggregate.md` distinguishes six confirmed downstream lip/curtain samples from four unproved edge/small-ROI measurements; ambiguous rows are not passes. Full source PNG sequences, masks, exact frameTimes and trace remain in `DancingCatsClone/Library/RadioArchipelago/AmberWinds/`.

No mobile build or mobile performance measurement was made.
''', encoding='utf-8')
print(json.dumps({'pass': True, 'nativeGameplayRun': play['runId'], 'packetPrincipalSamplesConfirmed': 6, 'unprovedAdditionalROIs': 4, 'installedCodeFilesMatched': len(code_paths), 'finalEditor': editor['scenePath']}, indent=2))
