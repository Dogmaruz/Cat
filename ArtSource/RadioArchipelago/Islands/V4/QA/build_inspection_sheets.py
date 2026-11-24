"""Read canonical V4 PNGs and make labelled inspection copies in QA only."""
from pathlib import Path
import sys,json
from PIL import Image,ImageOps,ImageDraw,ImageFont

BASE=Path(__file__).resolve().parents[1]
selected=sys.argv[1].split(',') if len(sys.argv)>1 else None
worlds=[]
for folder in sorted((BASE/'Output').iterdir()):
    if not folder.is_dir() or not (folder/'manifest.json').is_file():continue
    if selected and not any(folder.name.startswith(value) for value in selected):continue
    if not all((folder/(view+'.png')).is_file() for view in ('top','isometric')):continue
    worlds.append((folder,json.loads((folder/'manifest.json').read_text(encoding='utf8'))))
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',23)
for start in range(0,len(worlds),4):
    group=worlds[start:start+4];tile,label=1000,54
    canvas=Image.new('RGB',(tile*2,(tile+label)*len(group)),'#fffaf2');draw=ImageDraw.Draw(canvas)
    for row,(folder,manifest) in enumerate(group):
        for col,(view,name) in enumerate((('top','Сверху'),('isometric','Объёмный вид'))):
            with Image.open(folder/(view+'.png')) as image:
                image=ImageOps.contain(image.convert('RGB'),(tile,tile),Image.Resampling.LANCZOS)
            x,y=col*tile,row*(tile+label)
            draw.text((x+18,y+13),f"V4 · {folder.name[:2]} {manifest['title']} · {name}",font=font,fill='#263b42')
            canvas.paste(image,(x+(tile-image.width)//2,y+label+(tile-image.height)//2))
    target=BASE/'QA'/('inspection_'+'_'.join(folder.name[:2] for folder,_ in group)+'.jpg')
    canvas.save(target,quality=95);print(target)
