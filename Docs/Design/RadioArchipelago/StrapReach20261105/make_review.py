from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parent
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',20)
title=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',28)

def tile_sheet(items,name,columns=4):
    width,height=350,420
    result=Image.new('RGB',(columns*width,((len(items)+columns-1)//columns)*height+55),'#efede4')
    draw=ImageDraw.Draw(result)
    draw.text((20,12),name,font=title,fill='#203140')
    for index,(path,label) in enumerate(items):
        x,y=(index%columns)*width,(index//columns)*height+55
        im=Image.open(path).convert('RGB').crop((90,335,500,755))
        im.thumbnail((width-14,height-45),Image.Resampling.LANCZOS)
        result.paste(im,(x+(width-im.width)//2,y))
        draw.text((x+15,y+height-40),label,font=font,fill='#203140')
    result.save(root/(name+'.png'))

tile_sheet([(root/'Before/Rumi__backpack_Rumi_Orbit__side.png','До'),
            (root/'After/Rumi__backpack_Rumi_Orbit__side.png','Теперь')], 'Руми — посадка лямок',2)
pairs=json.loads((root/'After/report.json').read_text(encoding='utf-8'))['pairs']
for view in ('side','front','back'):
    tile_sheet([(root/'After'/next(f for f in p['images'] if f.endswith('__'+view+'.png')),
                p['hero']+' / '+p['item'].removeprefix('backpack_')) for p in pairs if p['kind']=='Backpack'],
               'owners-'+view,5)
print('Created comparison and three owner sheets from native Unity captures.')
