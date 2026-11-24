import bpy,pathlib,json,math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent
SOURCE=HERE.parent/'UnifiedLakeWaterfalls20261102/STAGING/Coral/coral_unified_lake_waterfalls.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE));bpy.context.view_layer.update()
objects=[ob for ob in bpy.context.scene.objects if ob.type=='MESH'and any(m and'_Terrain'in m.name for m in ob.data.materials)]
verts=[];faces=[]
for ob in objects:
 off=len(verts);verts.extend(ob.matrix_world@v.co for v in ob.data.vertices);faces.extend(tuple(off+i for i in p.vertices)for p in ob.data.polygons)
tree=BVHTree.FromPolygons(verts,faces,all_triangles=False)
def ground(p):
 hit,n,f,d=tree.ray_cast(Vector((p.x,p.y,500)),Vector((0,0,-1)),1000)
 return hit.z if hit is not None else None
cases=[]
for label,origin,width,water,angles in [('high',(67,-7),2.3,6.725999832,(45,67.5,90,112.5,135,315)),('low',(44,-72.4),3.4,.456,(250,270,290))]:
 for angle in angles:
  a=math.radians(angle);fwd=Vector((math.cos(a),math.sin(a)));across=Vector((-fwd.y,fwd.x));start=Vector(origin)
  rows=[];exit=None
  for i in range(161):
   s=i*.5;p=start+fwd*s;gs=[ground(p+across*(u*width))for u in(-.5,-.25,0,.25,.5)]
   rows.append(dict(distance=s,position=list(p),ground=gs))
   if i>2 and all(z is None for z in gs):
    if all(ground(p+fwd*extra+across*(u*width))is None for extra in(.5,1,2)for u in(-.5,0,.5)):
     exit=s;break
  if exit is None:continue
  values=[z for r in rows for z in r['ground']if z is not None]
  cases.append(dict(id=label,angle=angle,origin=origin,width=width,water_level=water,first_clear_distance=exit,
   first_clear_xy=list(start+fwd*exit),ground_height_range=[min(values),max(values)],rows=rows))
(HERE/'drain_candidates.json').write_text(json.dumps(cases,indent=2))
print(json.dumps([{k:v for k,v in c.items()if k!='rows'}for c in cases],indent=2),flush=True)
