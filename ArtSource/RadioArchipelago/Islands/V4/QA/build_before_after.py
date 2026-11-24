"""Compare native V3/V4 render outputs; no repainting or source file edits."""
from pathlib import Path
import json,sys
from PIL import Image,ImageOps,ImageDraw,ImageFont

BASE=Path(__file__).resolve().parents[1]
ids=sys.argv[1].split(',') if len(sys.argv)>1 else ['01_emerald_ravine','10_cloud_observatory']
folders=[]
for value in ids:
    matches=[folder for folder in (BASE/'Output').iterdir() if folder.is_dir() and folder.name.startswith(value)]
    assert len(matches)==1,(value,matches)
    folders.append(matches[0])
tile,label,header=1000,54,78
canvas=Image.new('RGB',(tile*2,header+(tile+label)*len(folders)),'#fffaf2');draw=ImageDraw.Draw(canvas)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',25)
heading=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',34)
draw.text((26,18),'Радиоархипелаг · V3 → V4 · рендеры реальных 3D-сцен',font=heading,fill='#263b42')
for row,folder in enumerate(folders):
    title=json.loads((folder/'manifest.json').read_text(encoding='utf8'))['title']
    for col,version in enumerate(('V3','V4')):
        source=BASE.parent/version/'Output'/folder.name/'isometric.png'
        assert source.is_file(),source
        with Image.open(source) as image:
            image=ImageOps.contain(image.convert('RGB'),(tile,tile),Image.Resampling.LANCZOS)
        x,y=col*tile,header+row*(tile+label)
        draw.text((x+18,y+13),f'{version} · {title}',font=font,fill='#263b42')
        canvas.paste(image,(x+(tile-image.width)//2,y+label+(tile-image.height)//2))
target=BASE/'QA'/('before-after_'+'_'.join(folder.name[:2] for folder in folders)+'.jpg')
canvas.save(target,quality=96);print(target)
