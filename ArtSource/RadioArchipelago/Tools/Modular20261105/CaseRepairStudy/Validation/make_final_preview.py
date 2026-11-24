"""Layout-only gallery from final native Unity captures, with input provenance."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

CAT = Path(__file__).resolve().parents[6]
root = CAT / 'Docs/Design/RadioArchipelago/BodyHarness20261105'
native = root / 'Native'
specification = [('Rumi','Руми'),('Tiko','Тико'),('Nori','Нори'),('Lumen','Люмен')]
font = ImageFont.truetype(r'C:/Windows/Fonts/segoeui.ttf',24)
small = ImageFont.truetype(r'C:/Windows/Fonts/segoeui.ttf',19)
title = ImageFont.truetype(r'C:/Windows/Fonts/segoeuib.ttf',29)
cell_w, cell_h, row_h, header = 360,333,383,85
canvas = Image.new('RGB',(1080,header+4*row_h+16),(243,240,229))
draw = ImageDraw.Draw(canvas)
draw.text((22,12),'Постоянные лямки · сменные рюкзаки',font=title,fill=(27,45,68))
draw.text((22,51),'Кадры Unity — крупный вид посадки. Верх антенн вне кадра.',font=small,fill=(70,80,91))
crop = (90,350,630,850)
records = []
for row,(name,label) in enumerate(specification):
    y = header+row*row_h
    draw.text((14,y+3),label,font=font,fill=(27,45,68))
    for col,(view,caption) in enumerate([('front','Спереди'),('side','Сбоку'),('back','Сзади')]):
        path = native/f'{name}__backpack_{name}__rest__{view}.png'
        with Image.open(path) as image:
            canvas.paste(image.convert('RGB').crop(crop).resize((cell_w,cell_h),Image.Resampling.LANCZOS),
                         (col*cell_w,y+35))
        draw.text((col*cell_w+14,y+row_h-21),caption,font=small,fill=(70,80,91))
        records.append(dict(input=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                            crop=crop,resize=[cell_w,cell_h]))
output = root/'final-preview.png'
canvas.save(output)
(root/'final-preview-provenance.json').write_text(json.dumps(dict(
    sourceReport=json.loads((native/'report.json').read_text()),
    operations='Layout only: identical crop and resize for all twelve native frames, captions and neutral background; no model retouching.',
    output=str(output),outputSHA256=hashlib.sha256(output.read_bytes()).hexdigest(),records=records),
    ensure_ascii=False,indent=2),encoding='utf-8')
print(output)
