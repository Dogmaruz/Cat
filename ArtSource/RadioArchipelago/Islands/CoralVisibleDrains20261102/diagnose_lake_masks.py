"""Read-only final FBX mask/UV diagnostics. Never exports or saves Blender state."""
import bpy,pathlib,json,hashlib,math,collections
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent
ISLANDS=HERE.parent
CONFIG=[
 ('Coral',HERE/'STAGING',ISLANDS/'RooftopMechanisms20261101/STAGING/Coral/coral_rooftops.blend'),
 ('Moon',ISLANDS/'UnifiedLakeWaterfalls20261102/STAGING/Moon',ISLANDS/'RooftopMechanisms20261101/STAGING/Moon/moon_rooftops.blend'),
 ('Amber',ISLANDS/'UnifiedLakeWaterfalls20261102/STAGING/Amber',ISLANDS/'AmberWindsPilot/WaterfallRework20261102/amber_winds_continuous.blend')]
def bounds(ps):
 return [[min(p[k]for p in ps),max(p[k]for p in ps)]for k in range(3)] if ps else None
def unity(p):return [p.x,p.z,-p.y]
def area(ps):return (ps[1]-ps[0]).cross(ps[2]-ps[0]).length*.5
result=[]
for label,folder,original in CONFIG:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 meta=json.loads((folder/'handoff.json').read_text());path=pathlib.Path(meta['output_model'])
 assert hashlib.sha256(path.read_bytes()).hexdigest()==meta['output_sha256']
 bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True)
 flows={s['node']:bpy.data.objects[s['node']] for s in meta['sheets']}
 with bpy.data.libraries.load(str(original),link=False)as(src,dst):
  dst.objects=[n for n in src.objects if n in meta['consumed_pool_nodes']]
 pools=[]
 for ob in dst.objects:
  if ob is None or ob.type!='MESH':continue
  ps=[ob.matrix_world@v.co for v in ob.data.vertices]
  faces=[tuple(p.vertices)for p in ob.data.polygons]
  heights=[sum(ps[i].z for i in f)/len(f)for f in faces]
  pools.append((ob.name,BVHTree.FromPolygons([Vector((p.x,p.y,0))for p in ps],faces,all_triangles=False),bounds(ps),heights))
 def hits(p):
  names=[]
  for name,bvh,box,heights in pools:
   hit,n,f,d=bvh.ray_cast(Vector((p.x,p.y,1)),Vector((0,0,-1)),2)
   # Compare XY footprint at the authored level. The accepted source lake
   # union raised Coral/Moon by6mm and Amber by35mm; original Coral lake has
   # its own25mm static ripple. These are not separate XY water footprints.
   if hit is not None and abs(heights[f]-p.z)<.061:names.append(name)
  return names
 island=dict(level=label,sha256=meta['output_sha256'],original_pools=[dict(name=n,bounds=b)for n,v,b,h in pools],sheets=[])
 for sheet in meta['sheets']:
  ob=flows[sheet['node']];m=ob.data;m.calc_loop_triangles();uv=m.uv_layers[0];reg=m.uv_layers[1]
  attrib={}
  for loop in m.loops:attrib[loop.vertex_index]=(tuple(uv.data[loop.index].uv),tuple(reg.data[loop.index].uv))
  categories=collections.defaultdict(list);inpool=collections.defaultdict(list);perpool=collections.defaultdict(collections.Counter)
  z=sheet['lake_level']
  for vi,(q,r)in attrib.items():
   p=ob.matrix_world@m.vertices[vi].co
   if abs(p.z-z)>.001:continue
   cat='full1'if r[0]>.99999 else'partial'if r[0]>.00001 else'lake0'
   rec=dict(position=list(p),unity=unity(p),uv=list(q),mask=r[0])
   categories[cat].append(rec)
   poolhits=hits(p)
   if poolhits:
    inpool[cat].append(rec)
    for name in poolhits:perpool[name][cat]+=1
  triangle_stats=collections.defaultdict(lambda:dict(count=0,area=0,uv_degenerate_count=0,uv_degenerate_area=0,uv_det_min=None,uv_det_max=0))
  for tri in m.loop_triangles:
   ps=[ob.matrix_world@m.vertices[i].co for i in tri.vertices]
   if max(abs(p.z-z)for p in ps)>.001:continue
   masks=[reg.data[l].uv.x for l in tri.loops]
   cat='full1'if min(masks)>.99999 else'partial'if max(masks)>.00001 else'lake0'
   c=sum(ps,Vector())/3;cat+=':original_pool'if hits(c)else':outside_original_pool'
   stat=triangle_stats[cat];a=area(ps);us=[uv.data[l].uv for l in tri.loops]
   det=abs((us[1].x-us[0].x)*(us[2].y-us[0].y)-(us[1].y-us[0].y)*(us[2].x-us[0].x))
   stat['count']+=1;stat['area']+=a;stat['uv_det_min']=min(det,stat['uv_det_min'])if stat['uv_det_min']is not None else det;stat['uv_det_max']=max(det,stat['uv_det_max'])
   if det<1e-8:stat['uv_degenerate_count']+=1;stat['uv_degenerate_area']+=a
  def summary(rs):
   return dict(count=len(rs),bounds=bounds([Vector(r['position'])for r in rs]),uv_bounds=[[min(r['uv'][i]for r in rs),max(r['uv'][i]for r in rs)]for i in range(2)]if rs else None,
    mask_range=[min(r['mask']for r in rs),max(r['mask']for r in rs)]if rs else None,
    minimum_mask_example=min(rs,key=lambda r:r['mask'])if rs else None,maximum_mask_example=max(rs,key=lambda r:r['mask'])if rs else None)
  rec=dict(node=ob.name,lake_level=z,metric_start_v=sheet['metric_start_v'],metric_start_unity=sheet['metric_start_unity'],
   flat_categories={k:summary(v)for k,v in categories.items()},inside_original_pool={k:summary(v)for k,v in inpool.items()},
   per_original_pool={k:dict(v)for k,v in perpool.items()},horizontal_triangles=dict(triangle_stats))
  island['sheets'].append(rec)
 result.append(island)
 print('LAKE_MASK_ISLAND',label,json.dumps(island),flush=True)
(HERE/'lake_mask_diagnosis.json').write_text(json.dumps(result,indent=2))
print('READONLY_LAKE_MASK_DIAGNOSIS_COMPLETE',flush=True)
