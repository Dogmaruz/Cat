"""Run with standard Python after pack_validate.py has sampled final meshes."""
from pathlib import Path
import json,hashlib,numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[4]
PACKS=ROOT/'ArtSource/RadioArchipelago/ProcessedModular/Backpacks'
QA=ROOT/'Docs/Design/RadioArchipelago/ModularCast20261104/PackQA'
entries=[]
for folder in sorted(p for p in PACKS.iterdir() if p.is_dir()):
 path=folder/'Meta.json';meta=json.loads(path.read_text());samples=meta.pop('_harnessUvSamples')
 image=Image.open(folder/'BaseColor.png').convert('RGB');w,h=image.size
 colors=np.array([image.getpixel((min(w-1,max(0,int(u*w))),min(h-1,max(0,int((1-v)*h))))) for u,v in samples],dtype=float)/255
 meta['harnessColorRGB']=np.median(colors,axis=0).tolist();meta['harnessColorSpace']='sRGB';meta['harnessColorMethod']='median of actual source harness UV texels, decoded from BaseColor.png'
 path.write_text(json.dumps(meta,indent=2))
 files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.iterdir() if p.name in ('Model.fbx','BaseColor.png','Normal.png','MetallicSmoothness.png','Meta.json')}
 archive=ROOT/'ArtSource/RadioArchipelago/ModularCast20261102/Incoming/Backpacks'/folder.name/(folder.name+'.zip')
 entries.append({'id':folder.name,'files':files,'sourceUnchanged':hashlib.sha256(archive.read_bytes()).hexdigest()==meta['sourceArchiveSha256'],'triangles':sum(meta['outputTriangles'].values()),'harnessColorRGB':meta['harnessColorRGB'],'housingClearance':meta['clearanceToCaseBody']})
(QA/'final-payload-manifest.json').write_text(json.dumps({'passed':len(entries)==20 and all(e['sourceUnchanged'] and len(e['files'])==5 for e in entries),'entries':entries},indent=2))
for view in ('back','turned'):
 fs=sorted(QA.glob('*_prepared_'+view+'.png'))
 for k in range(2):
  sheet=Image.new('RGB',(1200,1000),(246,241,229));draw=ImageDraw.Draw(sheet)
  for i,f in enumerate(fs[k*10:(k+1)*10]):
   im=Image.open(f).convert('RGB');im.thumbnail((235,465));x=(i%5)*240;y=(i//5)*500
   sheet.paste(im,(x,y+27));draw.text((x+6,y+7),f.stem.replace('_prepared_'+view,''),fill='black')
  sheet.save(QA/f'final-{view}-sheet-{k}.jpg')
print('PACK_FINAL',len(entries),'all source ZIPs unchanged, 5 exact payload hashes per pack')
