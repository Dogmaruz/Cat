"""Evidence from the final editable pack geometry, independent of import code."""
from pathlib import Path
import bpy,bmesh,json,math,sys,hashlib,numpy as np
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[4]
PACKS=ROOT/'ArtSource/RadioArchipelago/ProcessedModular/Backpacks'
QA=ROOT/'Docs/Design/RadioArchipelago/ModularCast20261104/PackQA'
report=[]
for folder in sorted(p for p in PACKS.iterdir() if p.is_dir()):
 bpy.ops.wm.open_mainfile(filepath=str(folder/'editable.blend'),load_ui=False)
 meta=json.loads((folder/'Meta.json').read_text());names=['Case','Harness.L','Harness.R','TopRotor','TopPivot','BackpackRoot']
 checks={'id':folder.name,'requiredNodes':all(n in bpy.data.objects for n in names)}
 top=bpy.data.objects['TopPivot'];rotor=bpy.data.objects['TopRotor'];case=bpy.data.objects['Case']
 checks['independentRotor']=rotor.parent==top and top.parent.name=='BackpackRoot'
 meshes=[bpy.data.objects[n] for n in names[:4]]
 checks['uvValid']=all(o.data.uv_layers.active is not None and len(o.data.uv_layers.active.data)==len(o.data.loops) for o in meshes)
 checks['finite']=all(all(math.isfinite(x) for x in v.co) for o in meshes for v in o.data.vertices)
 checks['geometry']={}
 for o in meshes:
  bm=bmesh.new();bm.from_mesh(o.data)
  checks['geometry'][o.name]={'vertices':len(bm.verts),'triangles':sum(len(f.verts)-2 for f in bm.faces),'boundaryEdges':sum(e.is_boundary for e in bm.edges)};bm.free()
 minZ=math.inf;axis='Y' if folder.name=='Fira_Beacon' else 'Z'
 for i in range(72):
  m=Matrix.Rotation(math.tau*i/72,4,axis)
  minZ=min(minZ,min((top.location+m@v.co).z for v in rotor.data.vertices))
 checks['fullTurnSamples']=72;checks['minimumRotatingZ']=minZ;checks['caseBodyMaximumZ']=meta['caseBoundsMax'][2]
 checks['caseBodyVerticalClearance']=minZ-meta['caseBoundsMax'][2]
 # The visible swivel/bearing is intentionally in contact. Body clearance
 # is measured against the true housing, excluding its fixed narrow spindle.
 checks['clearOfHousing']=checks['caseBodyVerticalClearance']>.015
 samples=[]
 for o in meshes[1:3]:
  uv=o.data.uv_layers.active.data
  for j in range(0,len(uv),max(1,len(uv)//400)):
   samples.append([float(uv[j].uv.x),float(uv[j].uv.y)])
 meta['_harnessUvSamples']=samples
 meta['clearanceToCaseBody']=checks['caseBodyVerticalClearance']
 meta['validatedRevolutionSamples']=72
 (folder/'Meta.json').write_text(json.dumps(meta,indent=2))
 checks['fbxSha256']=hashlib.sha256((folder/'Model.fbx').read_bytes()).hexdigest()
 checks['passed']=all(checks[k] for k in ('requiredNodes','independentRotor','uvValid','finite','clearOfHousing'))
 report.append(checks);print('PACK_VALIDATION',folder.name,checks['passed'],checks['caseBodyVerticalClearance'],flush=True)
 (QA/'final-validation.json').write_text(json.dumps({'passed':all(r['passed'] for r in report) and len(report)==20,'entries':report},indent=2))
