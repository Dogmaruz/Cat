from pathlib import Path
import bpy,bmesh,sys,json
sys.path.insert(0,str(Path(__file__).parent))
import pack_author as a
report=[]
for folder in sorted((a.src.OUT/'Backpacks').iterdir()):
 if not folder.is_dir():continue
 bpy.ops.wm.open_mainfile(filepath=str(folder/'editable.blend'),load_ui=False)
 z=bpy.data.objects['TopPivot'].location.z
 if folder.name=='Fira_Beacon':continue
 bm=bmesh.new();bm.from_mesh(bpy.data.objects['Case'].data)
 bad=[]
 for g in a.components(bm):
  lo,hi=a.bb_faces(g)
  if lo.z>=z-.00001 and hi.z>z+.001:bad.append({'faces':len(g),'bounds':[list(lo),list(hi)]})
 report.append({'id':folder.name,'parts':bad});bm.free()
(a.src.QA/'orphan-probe.json').write_text(json.dumps(report,indent=2))
print('ORPHANS',json.dumps([x for x in report if x['parts']]),flush=True)
