"""Check protected game content, independent accessory poses and native media."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[4]
PROJECT = ROOT / 'DancingCatsClone'
OUT = ROOT / 'Docs/Design/RadioArchipelago/HatsAndMotion20261105'


def main():
    protected = json.loads((OUT / 'protected-before.json').read_text(encoding='utf8'))
    changed = []
    expected_runtime_changes = []
    for name, digest in protected.items():
        path = PROJECT / name
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            text = path.read_text(encoding='utf8') if path.is_file() else ''
            reverted = text
            if name.endswith('/Runtime/Modular/CourierEquipmentDefinition.cs'):
                reverted = text.replace('        // Optional authored hat shape for this hero; other equipment uses its catalog prefab.\n        public GameObject prefabOverride;\n', '')
            elif name.endswith('/Runtime/Modular/CourierWardrobeRig.cs'):
                reverted = text.replace('            var prefab = kind == CourierEquipmentKind.Hat && fit != null && fit.prefabOverride != null\n                ? fit.prefabOverride : definition.prefab;\n            var instance = Instantiate(prefab, bodyRig.visualPivot, false);', '            var instance = Instantiate(definition.prefab, bodyRig.visualPivot, false);')
            original_matched = reverted != text and any(
                hashlib.sha256(candidate.encode('utf8')).hexdigest() == digest
                for candidate in (reverted, reverted.replace('\n', '\r\n')))
            if original_matched:
                expected_runtime_changes.append(dict(file=name, reason='Optional pre-authored fitted hat prefab; no other source text changed.', afterSha256=hashlib.sha256(path.read_bytes()).hexdigest()))
            else:
                changed.append(name)
    originals = json.loads((OUT / 'nonhat-fits-before.json').read_text(encoding='utf8'))
    nonhat_changes = []
    for hero, before in originals.items():
        source = (PROJECT / f'Assets/RadioArchipelago/Content/Modular/Characters/{hero}.prefab').read_text(encoding='utf8')
        chunks = re.split(r'(?m)^    - equipmentId: ', source)
        current = {c.splitlines()[0]: c for c in chunks[1:] if not c.startswith('hat_')}
        for item in sorted(set(before) | set(current)):
            current_value = current.get(item)
            if current_value is not None:
                # Unity writes the new optional field to existing fits. Only a null
                # reference is allowed for unchanged backpacks and scarves.
                current_value = re.sub(r'(?m)^      prefabOverride: \{fileID: 0\}\n', '', current_value)
            if before.get(item) != current_value:
                nonhat_changes.append(f'{hero}/{item}')
    native = OUT / 'Motion/Native'
    manifest = json.loads((native / 'manifest.json').read_text(encoding='utf8'))
    missing = []
    for clip in manifest['clips']:
        for i in range(clip['frames']):
            p = native / clip['frameFolder'] / f'frame-{i:04d}.png'
            if not p.is_file():
                missing.append(str(p.relative_to(OUT)))
        for hero in manifest['heroes']:
            p = OUT / 'Videos' / f"{hero['assetId']}--{clip['id']}.mp4"
            if not p.is_file() or p.stat().st_size < 1000:
                missing.append(str(p.relative_to(OUT)))
    report = dict(protectedFiles=len(protected), protectedChanges=changed,
                  expectedRuntimeChanges=expected_runtime_changes,
                  nonHatFitChanges=nonhat_changes, sourceCaptureStatus=manifest['status'],
                  activeScenePreserved=manifest['activeScenePreserved'],
                  heroes=len(manifest['heroes']), states=len(manifest['clips']),
                  sourceFrames=sum(c['frames'] for c in manifest['clips']),
                  videos=len(list((OUT / 'Videos').glob('*.mp4'))), missingMedia=missing)
    hat_native = json.loads((OUT / 'Hats/After/report.json').read_text(encoding='utf8'))
    hats = json.loads((OUT / 'Hats/After-checks.json').read_text(encoding='utf8'))
    harness = json.loads((OUT.parent / 'BodyHarness20261105/checks.json').read_text(encoding='utf8'))
    recipes = [json.loads(p.read_text(encoding='utf8')) for p in (OUT / 'GeneratedHatFits').glob('*/*.json')]
    geometry_safe = len(recipes) == 5 and all(
        r['minimumJacobianDeterminant'] >= .12 and
        all(r[k] == 0 for k in ['nonfiniteVertices', 'nonfiniteNormals', 'nonfiniteTangents'])
        for r in recipes)
    report['hatValidation'] = dict(pairs=hats['pairs'], nativeViews=hat_native['images'],
        structuralPassed=hats['structuralPassed'],
        checksAfterCapture=hats['capturedUtc'] > hat_native['capturedUtc'],
        eyeOccludedSamples=sum(o['eyeOccludedSamples'] for o in hats['observations']),
        generatedVariants=len(recipes), generatedGeometrySafe=geometry_safe,
        activeScenePreserved=hats['activeScenePreserved'] and hat_native['activeScenePreserved'])
    report['backpackValidation'] = dict(pairs=harness['pairs'], passed=harness['passed'],
        activeScenePreserved=harness['activeScenePreserved'])
    report['passed'] = (not changed and not nonhat_changes and not missing
                        and report['sourceCaptureStatus'] == 'complete'
                        and report['activeScenePreserved'] and report['sourceFrames'] == 717
                        and report['heroes'] == 10 and report['states'] == 13
                        and hats['pairs'] == 80 and hats['structuralPassed']
                        and report['hatValidation']['checksAfterCapture']
                        and report['hatValidation']['eyeOccludedSamples'] == 0
                        and geometry_safe and report['hatValidation']['activeScenePreserved']
                        and harness['passed'] and harness['pairs'] == 200)
    (OUT / 'preservation-and-media-checks.json').write_text(json.dumps(report, indent=2), encoding='utf8')
    print(json.dumps(report, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
