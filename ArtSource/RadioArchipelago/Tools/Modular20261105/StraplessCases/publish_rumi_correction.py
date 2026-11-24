from pathlib import Path
import json,hashlib,shutil
P=Path(__file__).parent;TARGET=Path(r'C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/ProcessedModular/Backpacks')
files=('Model.fbx','Meta.json','BaseColor.png','Normal.png','MetallicSmoothness.png','editable.blend')
def hashes(folder):return {n:hashlib.sha256((folder/n).read_bytes()).hexdigest() for n in files}
report=json.loads((P/'validation-Rumi.json').read_text());assert report['passed']
surface=json.loads((P/'RumiFrontCorrection/surface-proof.json').read_text());assert next(x for x in surface if x['variant']=='corrected')['offSourceFacadeSamples']==0
others={d.name:hashes(d) for d in TARGET.iterdir() if d.is_dir() and d.name!='Rumi'}
source=P/'Loft/Rumi';target=TARGET/'Rumi'
for name in files:shutil.copy2(source/name,target/name)
assert hashes(source)==hashes(target)
assert all(hashes(TARGET/name)==value for name,value in others.items())
manifest=json.loads((P/'published-manifest.json').read_text())
for row in manifest['items']:
 if row['id']=='Rumi':
  row['sha256']=hashes(target);row['keptAcceptedPilot']=False;row['frontCapRegressionCorrected']=True
assert all(hashes(TARGET/r['id'])==r['sha256'] for r in manifest['items'])
(P/'published-manifest.json').write_text(json.dumps(manifest,indent=2))
staged=json.loads((P/'payload-hashes.json').read_text())
for row in staged:
 if row['id']=='Rumi':row['files']=hashes(source)
(P/'payload-hashes.json').write_text(json.dumps(staged,indent=2))
(P/'RumiFrontCorrection/published.json').write_text(json.dumps({'passed':True,'onlyRumiChanged':True,'other19Unchanged':True,'sha256':hashes(target)},indent=2))
print('PUBLISHED_RUMI_FRONT_CORRECTION; other19 payloads byte-unchanged',flush=True)
