"""Freeze read-only QA inputs so later cleanup iterations cannot change evidence."""
import hashlib
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1] / 'StraplessCases'
STAMP = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
OUTPUT = HERE / ('LoftSnapshot-' + STAMP)


def copy_stable(source, target):
    stat_before = source.stat()
    payload = source.read_bytes()
    stat_after = source.stat()
    if stat_before.st_mtime_ns != stat_after.st_mtime_ns or stat_before.st_size != stat_after.st_size:
        raise RuntimeError('File changed while reading: ' + str(source))
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(payload)
    return dict(original=str(source.resolve()), snapshot=str(target.resolve()),
                sha256=hashlib.sha256(payload).hexdigest(), bytes=len(payload),
                modifiedUtc=datetime.fromtimestamp(stat_before.st_mtime, timezone.utc).isoformat(),
                modifiedNs=stat_before.st_mtime_ns)


rows = []
for folder in sorted((ROOT / 'Loft').iterdir()):
    if not folder.is_dir():
        continue
    row = dict(id=folder.name, complete=False, files={}, images={})
    try:
        for filename in ['editable.blend', 'Model.fbx', 'Meta.json']:
            row['files'][filename] = copy_stable(folder / filename, OUTPUT / folder.name / filename)
        for view in ['contact', 'side', 'radio']:
            image = copy_stable(ROOT / 'Review' / (folder.name + '_' + view + '.png'),
                                OUTPUT / '_Review' / (folder.name + '_' + view + '.png'))
            image['renderedAfterBlendSave'] = image['modifiedNs'] >= row['files']['editable.blend']['modifiedNs']
            row['images'][view] = image
        row['complete'] = True
    except Exception as error:
        row['error'] = repr(error)
    rows.append(row)
OUTPUT.mkdir(parents=True, exist_ok=True)
(OUTPUT / 'snapshot.json').write_text(json.dumps(dict(capturedUtc=STAMP, source=str((ROOT/'Loft').resolve()),
    entries=rows, limitation='Image hashes freeze the reviewed PNGs. Render timestamps are compared to blend save timestamps, but do not cryptographically prove which blend produced each render.'), indent=2), encoding='utf-8')
(HERE / 'latest-loft-snapshot.txt').write_text(str(OUTPUT.resolve()), encoding='utf-8')
print(str(OUTPUT.resolve()))
print('Complete payloads:', sum(row['complete'] for row in rows), 'of', len(rows))
for row in rows:
    if not row['complete'] or not all(i['renderedAfterBlendSave'] for i in row['images'].values()):
        print('QA_INPUT_ATTENTION', row['id'], row.get('error', 'one or more images predate this blend'))
