"""Publish approved case payloads; the accepted Rumi pilot remains untouched."""
from pathlib import Path
import json,hashlib,shutil
P=Path(__file__).parent
TARGET=Path(r'C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/ProcessedModular/Backpacks')
validation=json.loads((P/'validation-all.json').read_text())
assert validation['passed'] and len(validation['entries'])==20
names=('Model.fbx','Meta.json','BaseColor.png','Normal.png','MetallicSmoothness.png','editable.blend')
manifest=[]
for folder in sorted((P/'Loft').iterdir()):
 target=TARGET/folder.name
 assert target.is_dir() and target.resolve().is_relative_to(TARGET.resolve())
 for name in names:
  assert (folder/name).is_file()
  if folder.name!='Rumi':shutil.copy2(folder/name,target/name)
 hashes={name:hashlib.sha256((target/name).read_bytes()).hexdigest() for name in names}
 source={name:hashlib.sha256((folder/name).read_bytes()).hexdigest() for name in names}
 assert hashes==source,folder.name
 manifest.append({'id':folder.name,'target':str(target),'sha256':hashes,'publishedMatchesApproved':True,'keptAcceptedPilot':folder.name=='Rumi'})
assert len(manifest)==20
(P/'published-manifest.json').write_text(json.dumps({'passed':True,'count':20,'items':manifest},indent=2))
print('PUBLISHED_STRAPLESS_CASES',len(manifest),'six exact payload hashes per item; Rumi pilot unchanged',flush=True)
