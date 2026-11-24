"""Layout-only contact sheets from a frozen case-review input bundle."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument('--snapshot', type=Path, required=True)
args = parser.parse_args()
manifest = json.loads((args.snapshot / 'snapshot.json').read_text(encoding='utf-8'))
rows = [row for row in manifest['entries'] if row['complete']]
font = ImageFont.truetype(r'C:/Windows/Fonts/segoeui.ttf', 18)
small = ImageFont.truetype(r'C:/Windows/Fonts/segoeui.ttf', 12)
heading = ImageFont.truetype(r'C:/Windows/Fonts/segoeui.ttf', 20)
w, h, label_h, header_h = 330, 405, 45, 38
target = args.snapshot / 'Sheets'; target.mkdir(parents=True, exist_ok=True)
for view in ['contact', 'side', 'radio']:
    canvas = Image.new('RGB', (5*w, 4*(h+label_h)+header_h), (239, 237, 229))
    draw = ImageDraw.Draw(canvas)
    draw.text((10, 5), 'LOFT ' + manifest['capturedUtc'] + ' | ' + view + ' | layout only', font=heading, fill=(30,40,50))
    for i, row in enumerate(rows):
        x, y = (i % 5)*w, header_h+(i // 5)*(h+label_h)
        image_info = row['images'][view]
        with Image.open(image_info['snapshot']) as image:
            canvas.paste(image.convert('RGB').resize((w,h), Image.Resampling.LANCZOS), (x,y))
        draw.text((x+7,y+h+2), row['id'], font=font, fill=(30,40,50))
        suffix = '' if image_info['renderedAfterBlendSave'] else ' STALE RENDER'
        draw.text((x+7,y+h+25), 'B:' + row['files']['editable.blend']['sha256'][:10] +
                  ' PNG:' + image_info['sha256'][:10] + suffix, font=small, fill=(60,60,60))
    filename = target / (view + '.png'); canvas.save(filename); print(filename)
