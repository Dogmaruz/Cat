"""Read-only correspondence of new flow ribbons to unchanged animated pool surfaces."""
import bpy,pathlib,json,math,collections
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent
results=[]
for label in ('Coral','Moon'):
 folder=HERE/'STAGING'/label
 bpy.ops.wm.open_mainfile(filepath=str(folder/(label.lower()+'_continuous_waterfalls.blend')))
 pools=[ob for ob in bpy.context.scene.objects if ob.type=='MESH'and any(m and m.name=='RA_'+label+'_Water'for m in ob.data.materials)]
 verts=[];faces=[]
 for ob in pools:
  start=len(verts);verts.extend(ob.matrix_world@v.co for v in ob.data.vertices)
  faces.extend(tuple(start+i for i in p.vertices)for p in ob.data.polygons)
 tree=BVHTree.FromPolygons(verts,faces,all_triangles=False)
 handoff=json.loads((folder/'handoff.json').read_text())
 for info in handoff['sheets']:
  ob=bpy.data.objects[info['node']];uv=ob.data.uv_layers.active
  rows=collections.defaultdict(list)
  for loop in ob.data.loops:
   vertex=ob.data.vertices[loop.vertex_index];u,v=uv.data[loop.index].uv
   rows[round(v,5)].append((u,ob.matrix_world@vertex.co))
  overlap=[]
  for v,row in sorted(rows.items()):
   hits=[]
   for u,p in row:
    pos,n,face,distance=tree.ray_cast(Vector((p.x,p.y,p.z+.3)),Vector((0,0,-1)),.6)
    if pos is not None and abs(pos.z-p.z)<.20:hits.append(dict(u=float(u),position=list(p),pool_height=pos.z,rest_gap=p.z-pos.z))
   if hits:
    overlap.append(dict(v=v,min_gap=min(h['rest_gap']for h in hits),max_gap=max(h['rest_gap']for h in hits),u_range=[min(h['u']for h in hits),max(h['u']for h in hits)],vertex_hits=len(hits),first=hits[0]))
  r=dict(level=label,node=info['node'],rest_near_pool_v_range=[min(x['v']for x in overlap),max(x['v']for x in overlap)] if overlap else None,
   rest_gap_range=[min(x['min_gap']for x in overlap),max(x['max_gap']for x in overlap)] if overlap else None,rows=overlap)
  results.append(r)
(HERE/'upstream_overlap_diagnosis.json').write_text(json.dumps(results,indent=2))
print(json.dumps([{k:v for k,v in x.items()if k!='rows'}for x in results],indent=2),flush=True)
