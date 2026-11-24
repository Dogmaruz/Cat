from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[4]
PROJECT = ROOT / 'DancingCatsClone'
OUT = ROOT / 'Docs/Design/RadioArchipelago/HatFittingWorkbench/Applied20261106'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fit_parts(text):
    parts = re.split(r'(?m)^    - equipmentId: ', text)
    return parts[0], {p.splitlines()[0]: p for p in parts[1:]}


def main():
    protected = json.loads((OUT / 'protected-before.json').read_text(encoding='utf8'))
    unexpected = [name for name, sha in protected.items()
                  if not (PROJECT / name).is_file() or digest(PROJECT / name) != sha]
    native = json.loads((OUT / 'runtime-verification.json').read_text(encoding='utf8'))
    preflight = json.loads((OUT / 'preflight.json').read_text(encoding='utf8'))
    original = json.loads((OUT / 'import-source.json').read_text(encoding='utf8'))
    complete = json.loads((OUT / 'complete-source.json').read_text(encoding='utf8'))
    source = json.loads((OUT / 'user-export-complete.json').read_text(encoding='utf8'))
    baseline = json.loads((ROOT / 'Docs/Design/RadioArchipelago/HatFittingWorkbench/Snapshots/20261105_210952_823_All.json').read_text(encoding='utf8'))
    old_rows = {(p['hero'], p['hat']): p for p in baseline['hats']}
    changed_roots = set()
    for pose in source['hats']:
        before = old_rows[(pose['hero'], pose['hat'])]
        if any(max(abs(pose[n][k] - before[n][k]) for k in ('x', 'y', 'z')) > 1e-5
               for n in ('position', 'euler', 'axisScale')):
            changed_roots.add(pose['hero'] + '/' + pose['hat'])
    changed_children = set(preflight['customizedPairs'])
    outside = []
    preserved_non_hat = 0
    for path in (OUT / 'Before').glob('*.prefab'):
        current = PROJECT / 'Assets/RadioArchipelago/Content/Modular/Characters' / path.name
        old_header, old_fits = fit_parts(path.read_text(encoding='utf8'))
        new_header, new_fits = fit_parts(current.read_text(encoding='utf8'))
        if old_header != new_header or old_fits.keys() != new_fits.keys():
            outside.append(path.name + '/outside-equipment-fits')
        for equipment, before in old_fits.items():
            if equipment.startswith('hat_') and equipment != 'hat_SignalDiadem':
                continue
            if new_fits.get(equipment) != before:
                outside.append(path.name + '/' + equipment)
            else:
                preserved_non_hat += 1
    offset = int((OUT / 'editor-log-final-import-offset.txt').read_text())
    log = (PROJECT / 'Logs/Editor.log').read_bytes()[offset:].decode('utf8', errors='replace')
    errors = [line for line in log.splitlines()
              if re.search(r'error CS\d+|Exception:|Assertion failed|MissingReferenceException', line, re.I)]
    dll = PROJECT / 'Library/ScriptAssemblies/Assembly-CSharp-Editor.dll'
    sources = [PROJECT / 'Assets/RadioArchipelago/Editor' / name for name in
               ('HatFittingWorkbench.cs', 'CuratedHatPoseAuthoring.cs', 'UserHatFitImport20261106.cs')]
    fresh = all(dll.stat().st_mtime > path.stat().st_mtime for path in sources)
    result = {
        'passed': False,
        'protectedFiles': len(protected),
        'unexpectedProtectedChanges': unexpected,
        'outsideHatFitChanges': outside,
        'preservedOtherEquipmentFits': preserved_non_hat,
        'originalUserExportPreserved': digest(OUT / 'user-export.json') == original['sha256'] == digest(Path(original['originalExport'])),
        'completeExportVerified': digest(OUT / 'user-export-complete.json') == complete['sha256'],
        'pairs': native['pairs'],
        'rootEditedPairs': len(changed_roots),
        'childEditedPairs': len(changed_children),
        'allEditedPairs': len(changed_roots | changed_children),
        'authoredVariants': native['authoredVariants'],
        'exactStoredPoseError': native['maximumStoredError'],
        'maximumRuntimeRootMatrixError': native['maximumRuntimeError'],
        'maximumRuntimeChildMatrixError': native['maximumChildError'],
        'maximumRuntimeRendererMatrixError': native['maximumRendererError'],
        'nativePreflightPassed': preflight['passed'],
        'nativeVerificationPassed': native['passed'],
        'scenePreserved': native['activeScenePreserved'],
        'editorCompilationFresh': fresh,
        'newErrorLines': errors,
    }
    result['passed'] = (not unexpected and not outside and not errors and fresh
                        and result['originalUserExportPreserved'] and result['completeExportVerified']
                        and preflight['passed'] and native['passed'] and native['pairs'] == 70
                        and len(changed_roots | changed_children) == 70)
    (OUT / 'final-verification.json').write_text(json.dumps(result, indent=2), encoding='utf8')
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if result['passed'] else 1)


if __name__ == '__main__':
    main()
