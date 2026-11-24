"""Verify Nori-no publication preserves all 24 preceding catalog rows and source songs."""
from pathlib import Path
import hashlib
import json

ROOT = next(parent / 'DancingCatsClone' for parent in Path(__file__).resolve().parents
            if (parent / 'DancingCatsClone/Assets').is_dir())
OUT = ROOT / 'Library/NoriNoChart/Audio'


def rows(path):
    text = path.read_bytes().decode('utf-8-sig')
    decoder = json.JSONDecoder()
    index = text.index('[') + 1
    result = []
    while True:
        while text[index].isspace() or text[index] == ',':
            index += 1
        if text[index] == ']':
            return result
        start = index
        record, index = decoder.raw_decode(text, index)
        result.append((record, text[start:index]))


before = rows(Path(__file__).with_name('baseline.catalog.json'))
after = rows(ROOT / 'Assets/Resources/CustomData/songs.json')
assert len(before) == 24
assert [raw for row, raw in before] == [raw for row, raw in after if row['id'] != 'NoriNo']
nori = [row for row, raw in after if row['id'] == 'NoriNo']
assert len(after) == 25 and len(nori) == 1 and nori[0]['catalogId'] == 20004
protected = json.loads(Path(__file__).with_name('baseline.protected.json').read_text())
for relative, expected in protected.items():
    assert hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() == expected, relative
report = dict(passed=True, unchangedExistingCatalogRows=24,
              unchangedProtectedLumiTikoFiles=len(protected), newCatalogId=20004)
(OUT / 'preservation.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
