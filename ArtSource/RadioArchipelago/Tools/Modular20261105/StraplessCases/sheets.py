from pathlib import Path
from PIL import Image,ImageDraw
P=Path(__file__).parent/'Review'
for view in ('contact','side','radio'):
 fs=sorted(P.glob('*_'+view+'.png'))
 im=Image.new('RGB',(1800,1900),'#f0ede3');d=ImageDraw.Draw(im)
 for i,f in enumerate(fs):
  a=Image.open(f).convert('RGB');a.thumbnail((360,450));x=i%5*360;y=i//5*475
  im.paste(a,(x,y));d.text((x+10,y+451),f.stem.replace('_'+view,''),fill='black')
 im.save(P/('all20-'+view+'.png'))
