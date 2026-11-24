"""Package untouched generated references; the overview is additional, never a modeling input."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
manifest_path = ROOT / 'manifest.json'
if not manifest_path.exists():
    manifest_path = ROOT / 'manifest-draft.json'
data = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 23)
canvas = Image.new('RGB', (1600, 930), '#f3efe6')
draw = ImageDraw.Draw(canvas)
for i, concept in enumerate(data['concepts']):
    x = 20 + i % 4 * 395
    y = 15 + i // 4 * 455
    image = Image.open(concept['views']['front']['selected']).convert('RGB')
    image.thumbnail((375, 405))
    canvas.paste(image, (x + (375 - image.width) // 2, y))
    draw.text((x + 4, y + 412), f"{i+1:02d}. {concept['name']}", fill='#273a4c', font=font)
canvas.save(ROOT / 'overview-fronts.png')
print('OVERVIEW', ROOT / 'overview-fronts.png')
if manifest_path.name == 'manifest.json':
    checks = []
    for concept in data['concepts']:
        for view in ('front', 'back', 'right', 'left'):
            entry = concept['views'][view]
            source, selected = Path(entry['source']), Path(entry['selected'])
            source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
            selected_hash = hashlib.sha256(selected.read_bytes()).hexdigest()
            with Image.open(selected) as image:
                image.verify()
            with Image.open(selected) as image:
                size = list(image.size)
            checks.append(dict(concept=concept['id'], view=view, size=size,
                               sha256=selected_hash, originalPreserved=source_hash == selected_hash))
    report = dict(passed=len(checks) == 28 and all(c['originalPreserved'] for c in checks),
                  separateViews=len(checks), concepts=len(data['concepts']), images=checks,
                  limitation='AI-generated matching reference views, not metric CAD or a shared rendered 3D mesh.')
    (ROOT / 'verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('VERIFIED', report['passed'], report['separateViews'])
    if not report['passed']:
        raise SystemExit(1)
