from pathlib import Path
import json,shutil,hashlib
P=Path(__file__).parent
rows=[]
for folder in sorted((P/'Loft').iterdir()):
 before=P/'Before'/folder.name
 meta=json.loads((folder/'Meta.json').read_text());meta['harnessNames']=[]
 (folder/'Meta.json').write_text(json.dumps(meta,indent=2))
 for name in ('BaseColor.png','Normal.png','MetallicSmoothness.png'):
  target=folder/name
  if not target.exists():shutil.copy2(before/name,target)
  assert hashlib.sha256(target.read_bytes()).hexdigest()==hashlib.sha256((before/name).read_bytes()).hexdigest()
 rows.append({'id':folder.name,'files':{name:hashlib.sha256((folder/name).read_bytes()).hexdigest() for name in ('Model.fbx','Meta.json','BaseColor.png','Normal.png','MetallicSmoothness.png','editable.blend')}})
(P/'payload-hashes.json').write_text(json.dumps(rows,indent=2))
print('NORMALIZED_STRAPLESS',len(rows),flush=True)
