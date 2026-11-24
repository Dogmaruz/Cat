from pathlib import Path
from PIL import Image,ImageDraw
r=Path('Docs/Design/RadioArchipelago/ModularCast20261104/HarnessRepair');names=sorted(p.stem[:-9] for p in r.glob('*_caseonly.png'));out=Image.new('RGB',(360*5,475*4),(240,236,222));d=ImageDraw.Draw(out)
for i,n in enumerate(names):
 im=Image.open(r/(n+'_caseonly.png')).convert('RGB');out.paste(im,(i%5*360,i//5*475));d.text((i%5*360+12,i//5*475+444),n,fill=(20,20,20))
out.save(r/'all20-source-cases.png')
