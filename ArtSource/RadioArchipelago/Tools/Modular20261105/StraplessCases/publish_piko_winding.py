from pathlib import Path
import json,hashlib,shutil
P=Path(__file__).parent;TARGET=Path(r'C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/ProcessedModular/Backpacks')
files=('Model.fbx','Meta.json','BaseColor.png','Normal.png','MetallicSmoothness.png','editable.blend')
def hashes(folder):return {n:hashlib.sha256((folder/n).read_bytes()).hexdigest() for n in files}
assert json.loads((P/'validation-Piko.json').read_text())['passed']
proof=json.loads((P/'PikoWindingCorrection/fixed.json').read_text());assert proof['remainingOpposedSourceFaces']==0 and proof['positionsAndCornerUvUnchanged']
others={d.name:hashes(d) for d in TARGET.iterdir() if d.is_dir() and d.name!='Piko'}
source=P/'Loft/Piko';target=TARGET/'Piko'
for name in files:shutil.copy2(source/name,target/name)
assert hashes(source)==hashes(target)
assert all(hashes(TARGET/name)==value for name,value in others.items())
manifest=json.loads((P/'published-manifest.json').read_text())
for row in manifest['items']:
 if row['id']=='Piko':row['sha256']=hashes(target);row['sourceFacadeWindingCorrected']=True
assert all(hashes(TARGET/r['id'])==r['sha256'] for r in manifest['items'])
(P/'published-manifest.json').write_text(json.dumps(manifest,indent=2))
staged=json.loads((P/'payload-hashes.json').read_text())
for row in staged:
 if row['id']=='Piko':row['files']=hashes(source)
(P/'payload-hashes.json').write_text(json.dumps(staged,indent=2))
(P/'PikoWindingCorrection/published.json').write_text(json.dumps({'passed':True,'onlyPikoChanged':True,'other19Unchanged':True,'sha256':hashes(target)},indent=2))
print('PUBLISHED_PIKO_WINDING_CORRECTION; other19 payloads byte-unchanged',flush=True)
