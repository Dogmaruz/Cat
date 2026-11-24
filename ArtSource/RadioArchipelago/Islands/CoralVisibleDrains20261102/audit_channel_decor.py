import bpy,pathlib,json,collections,math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE.parent/'UnifiedLakeWaterfalls20261102/STAGING/Coral/coral_unified_lake_waterfalls.blend'))
objects=[ob for ob in bpy.context.scene.objects if ob.type=='MESH'];verts=[];faces=[]
for ob in objects:
 if not any(m and'_Terrain'in m.name for m in ob.data.materials):continue
 off=len(verts);verts.extend(ob.matrix_world@v.co for v in ob.data.vertices);faces.extend(tuple(off+i for i in f.vertices)for f in ob.data.polygons)
tree=BVHTree.FromPolygons(verts,faces,all_triangles=False)
out=[]
for label,cx,y0,y1,half,water in [('high',67,-2,4.0,3.15,6.726),('low',44,-83,-73,3.7,.456)]:
 records=[]
 for ob in objects:
  mats=[m.name for m in ob.data.materials if m]
  if any(any(k in m for k in('Terrain','Cliffs','Water','Foam'))for m in mats):continue
  ps=[]
  for v in ob.data.vertices:
   p=ob.matrix_world@v.co
   if abs(p.x-cx)>half or not y0<=p.y<=y1:continue
   hit,n,f,d=tree.ray_cast(Vector((p.x,p.y,500)),Vector((0,0,-1)),1000)
   if hit is not None and abs(p.z-hit.z)<.25 and hit.z>water-.2:ps.append(list(p))
  if ps:records.append(dict(node=ob.name,materials=mats,near_ground_vertices=len(ps),bounds=[[min(p[k]for p in ps),max(p[k]for p in ps)]for k in range(3)],examples=ps[:5]))
 out.append(dict(channel=label,decor=records))
(HERE/'channel_decor_audit.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2),flush=True)
