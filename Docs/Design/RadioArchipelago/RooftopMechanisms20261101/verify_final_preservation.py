"""Verify the 405-file baseline with only the sixteen requested visual revisions."""
import hashlib
import json
import os
from pathlib import Path

folder = Path(__file__).resolve().parent
repo = folder.parents[3]
design = folder.parent
baseline = json.loads((design / 'CloudsFinishMenu20261101/Before/preserved-assets.json').read_text(encoding='utf-8-sig'))

def key(path):
    return os.path.normcase(os.path.abspath(path))

expected = {}
for row in json.loads((design / 'LivingWater20261101/material-changes.json').read_text(encoding='utf-8-sig')):
    expected[key(row['path'])] = row['sha256'].upper()
for row in json.loads((folder / 'protected-changes.json').read_text(encoding='utf-8-sig')):
    expected[key(row['Path'])] = row['Hash'].upper()

allowed = set()
for island, stem in [('EmeraldAbyss', 'emerald_abyss'), ('CoralOasis', 'coral_oasis'), ('MoonResonance', 'moon_resonance')]:
    allowed.add(key(repo / f'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/{island}/{stem}_island.fbx'))
    for ext in ['asset', 'prefab']:
        allowed.add(key(repo / f'DancingCatsClone/Assets/RadioArchipelago/Content/Islands/{island}/{island}.{ext}'))
for island, mat in [('EmeraldAbyss', 'RA_Emerald_Water'), ('CoralOasis', 'RA_Coral_Foam'),
                    ('CoralOasis', 'RA_Coral_Water'), ('CoralOasis', 'RA_Coral_Waterfall'),
                    ('MoonResonance', 'RA_Moon_Foam'), ('MoonResonance', 'RA_Moon_Water'),
                    ('MoonResonance', 'RA_Moon_Waterfall')]:
    allowed.add(key(repo / f'DancingCatsClone/Assets/RadioArchipelago/Content/Islands/{island}/{mat}.mat'))

errors = []
if set(expected) != allowed:
    errors.append({'manifest_scope_mismatch': sorted(set(expected) ^ allowed)})
changed, unchanged = [], 0
for row in baseline:
    path = Path(row['Path'])
    if not path.is_file():
        errors.append({'path': str(path), 'reason': 'missing'})
        continue
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    actual = digest.hexdigest().upper()
    target = expected.get(key(path), row['Hash'].upper())
    if actual != target:
        errors.append({'path': str(path), 'expected': target, 'actual': actual})
    if actual == row['Hash'].upper():
        unchanged += 1
    else:
        changed.append(str(path))

result = {'pass': not errors, 'protectedFiles': len(baseline), 'unchanged': unchanged,
          'authorizedChanged': len(changed), 'expectedChanged': len(allowed),
          'changedPaths': changed, 'errors': errors}
(folder / 'final-preservation.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'changedPaths'}, indent=2))
raise SystemExit(bool(errors))
