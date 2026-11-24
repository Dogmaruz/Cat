"""Inspection-only overview of all ten unchanged canonical isometric renders."""
from pathlib import Path
import json
from PIL import Image, ImageOps, ImageDraw, ImageFont

BASE = Path(__file__).resolve().parents[1]
FOLDERS = sorted(folder for folder in (BASE/'Output').iterdir() if folder.is_dir())
assert len(FOLDERS) == 10, 'All ten scene folders are required'
assert all((folder/'manifest.json').is_file() and (folder/'isometric.png').is_file() for folder in FOLDERS), 'All ten worlds must be complete'
font_path = 'C:/Windows/Fonts/segoeui.ttf'
font = ImageFont.truetype(font_path, 24)
heading = ImageFont.truetype(font_path, 40)
tile, label, header = 640, 52, 86
sheet = Image.new('RGB', (tile*5, header+(tile+label)*2), '#fffcf7')
draw = ImageDraw.Draw(sheet)
draw.text((30,20), 'Радиоархипелаг · десять согласованных 3D-островов', font=heading, fill='#263b42')
for index, folder in enumerate(FOLDERS):
    x, y = (index%5)*tile, header+(index//5)*(tile+label)
    title = json.loads((folder/'manifest.json').read_text(encoding='utf8'))['title']
    draw.text((x+18,y+12), f'{index+1:02d}  {title}', font=font, fill='#263b42')
    with Image.open(folder/'isometric.png') as source:
        image = ImageOps.contain(source.convert('RGB'), (tile,tile), Image.Resampling.LANCZOS)
    sheet.paste(image, (x+(tile-image.width)//2, y+label+(tile-image.height)//2))
target = BASE/'QA'/'all-islands-overview.jpg'
sheet.save(target, quality=96)
print(target)
