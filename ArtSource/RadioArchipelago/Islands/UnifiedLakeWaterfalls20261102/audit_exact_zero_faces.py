"""Read-only exact-zero triangle gate over all seven exported water surfaces."""
import bpy,pathlib,json,hashlib,sys
HERE=pathlib.Path(__file__).resolve().parent
requested=sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else('Coral','Moon','Amber')
results=[]
for label in requested:
 folder=HERE/'STAGING'/label;meta=json.loads((folder/'handoff.json').read_text());file=pathlib.Path(meta['output_model'])
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(file),use_custom_normals=True,use_custom_props=True)
 sheets=[]
 for info in meta['sheets']:
  ob=bpy.data.objects[info['node']];m=ob.data;m.calc_loop_triangles();zeros=[];minimum_nonzero=float('inf')
  for i,t in enumerate(m.loop_triangles):
   ps=[ob.matrix_world@m.vertices[j].co for j in t.vertices];area=(ps[1]-ps[0]).cross(ps[2]-ps[0]).length*.5
   if area==0:zeros.append(dict(triangle=i,polygon=t.polygon_index,positions=[list(p)for p in ps]))
   else:minimum_nonzero=min(minimum_nonzero,area)
  sheets.append(dict(node=ob.name,vertices=len(m.vertices),triangles=len(m.loop_triangles),exact_zero_triangles=len(zeros),minimum_nonzero_area=minimum_nonzero,zero_faces=zeros))
 result=dict(level=label,sha256=hashlib.sha256(file.read_bytes()).hexdigest(),passed=all(s['exact_zero_triangles']==0 for s in sheets),sheets=sheets)
 (folder/'exact_zero_faces.json').write_text(json.dumps(result,indent=2));results.append(result);print('EXACT_ZERO_GATE',json.dumps(result),flush=True)
(HERE/('exact_zero_faces-'+ '-'.join(requested)+'.json')).write_text(json.dumps(results,indent=2))
assert all(r['passed']for r in results),'Exact zero-area water triangles remain'
