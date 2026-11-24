"""Create inspection-only contact sheets; canonical renders remain untouched."""
from pathlib import Path
import json
import sys
from PIL import Image, ImageOps, ImageDraw, ImageFont

BASE = Path(__file__).resolve().parents[1]
QA = BASE / 'QA'
QA.mkdir(exist_ok=True)
worlds = []
selected = sys.argv[1].split(',') if len(sys.argv) > 1 else None
for folder in sorted((BASE / 'Output').iterdir()):
    if selected and not any(folder.name.startswith(value) for value in selected):
        continue
    if not (folder / 'manifest.json').is_file():
        continue
    manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf8'))
    if all((folder / f'{view}.png').is_file() for view in ('top','isometric')):
        worlds.append((folder, manifest))
font_path = Path('C:/Windows/Fonts/segoeui.ttf')
font = ImageFont.truetype(str(font_path), 24) if font_path.exists() else ImageFont.load_default()
for start in range(0, len(worlds), 4):
    group = worlds[start:start+4]
    size, label = 900, 54
    sheet = Image.new('RGB', (size*2, (size+label)*len(group)), '#f4f1ec')
    draw = ImageDraw.Draw(sheet)
    for row, (folder, manifest) in enumerate(group):
        for col, view in enumerate(('top','isometric')):
            with Image.open(folder / f'{view}.png') as source:
                tile = ImageOps.contain(source.convert('RGB'), (size,size), Image.Resampling.LANCZOS)
            x, y = col*size, row*(size+label)
            sheet.paste(tile, (x+(size-tile.width)//2,y+label+(size-tile.height)//2))
            draw.text((x+18,y+13), f'{folder.name} / {view}', fill='#172127', font=font)
    target = QA / ('inspection_'+'_'.join(folder.name[:2] for folder,_ in group)+'.jpg')
    sheet.save(target, quality=94)
    print(target)
