from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
P=Path(__file__).parent
fs=sorted((P/'Review').glob('*-source.npy'))
im=Image.new('RGB',(1600,1800),'white');d=ImageDraw.Draw(im)
for j,f in enumerate(fs):
 ox=j%5*320;oy=j//5*450
 v=np.load(f);a=v[v[:,1]<.20]
 d.text((ox+10,oy+5),f.stem.replace('-source',''),fill='black')
 for z in np.arange(0,1.4,.1):
  sy=oy+420-z*270;d.line((ox+10,sy,ox+300,sy),fill='#dddddd');d.text((ox+10,sy),str(round(z,1)),fill='black')
 for y in np.arange(-.4,.41,.1):
  sx=ox+160+y*350;d.line((sx,oy+30,sx,oy+430),fill='#dddddd');d.text((sx,oy+430),str(round(y,1)),fill='black')
 for y,z,x in a[:,[1,2,0]]:
  d.point((ox+160+y*350,oy+420-z*270),fill=(int(100+max(0,x)*250),50,int(100+max(0,-x)*250)))
im.save(P/'Review/side-points.png')
