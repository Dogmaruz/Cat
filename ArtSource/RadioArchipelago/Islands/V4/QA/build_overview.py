"""Inspection-only V4 overview from all ten unchanged native iso renders."""
from pathlib import Path
import json
from PIL import Image,ImageOps,ImageDraw,ImageFont

BASE=Path(__file__).resolve().parents[1]
folders=sorted(folder for folder in (BASE/'Output').iterdir() if folder.is_dir())
assert len(folders)==10,'Ten V4 scene folders are required'
assert all((folder/'manifest.json').is_file() and (folder/'isometric.png').is_file() for folder in folders),'All ten scenes must be complete'
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',24)
heading=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',40)
tile,label,header=640,52,86
canvas=Image.new('RGB',(tile*5,header+(tile+label)*2),'#fffaf2');draw=ImageDraw.Draw(canvas)
draw.text((30,20),'Радиоархипелаг · V4 · десять детальных 3D-островов',font=heading,fill='#263b42')
for index,folder in enumerate(folders):
    x,y=(index%5)*tile,header+(index//5)*(tile+label)
    title=json.loads((folder/'manifest.json').read_text(encoding='utf8'))['title']
    draw.text((x+18,y+12),f'{index+1:02d}  {title}',font=font,fill='#263b42')
    with Image.open(folder/'isometric.png') as source:
        image=ImageOps.contain(source.convert('RGB'),(tile,tile),Image.Resampling.LANCZOS)
    canvas.paste(image,(x+(tile-image.width)//2,y+label+(tile-image.height)//2))
target=BASE/'QA'/'all-islands-overview.jpg';canvas.save(target,quality=96);print(target)
