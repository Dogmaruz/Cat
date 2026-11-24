import bpy,pathlib,json,collections
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent
SOURCE=HERE.parent/'AmberWindsPilot/WaterfallRework20261102/amber_winds_continuous.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE));bpy.context.view_layer.update()
pools=[];flows=[]
for ob in bpy.context.scene.objects:
 if ob.type!='MESH':continue
 mats=[m.name for m in ob.data.materials if m]
 if 'RA_Amber_Water'in mats:pools.append(ob)
 if 'RA_Amber_Waterfall'in mats:flows.append(ob)
result=[]
for flow in flows:
 uv=flow.data.uv_layers.active;records=[]
 for pool in pools:
  ps=[pool.matrix_world@v.co for v in pool.data.vertices];fs=[tuple(p.vertices)for p in pool.data.polygons]
  tree=BVHTree.FromPolygons(ps,fs,all_triangles=False);hits=[]
  for l in flow.data.loops:
   u,v=uv.data[l.index].uv;p=flow.matrix_world@flow.data.vertices[l.vertex_index].co
   hit,n,face,d=tree.ray_cast(Vector((p.x,p.y,p.z+.2)),Vector((0,0,-1)),.4)
   if hit is not None and abs(p.z-hit.z)<.1:hits.append(dict(u=float(u),v=float(v),position=list(p),gap=p.z-hit.z))
  if hits:records.append(dict(pool=pool.name,vertex_hits=len(hits),uv_v_range=[min(h['v']for h in hits),max(h['v']for h in hits)],rest_gap_range=[min(h['gap']for h in hits),max(h['gap']for h in hits)],examples=hits[::max(1,len(hits)//5)][:5]))
 result.append(dict(flow=flow.name,overlaps=records))
(HERE/'amber_upstream_overlap.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2),flush=True)
