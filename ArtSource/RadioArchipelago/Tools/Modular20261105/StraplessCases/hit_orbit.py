from pathlib import Path
import bpy,json,sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).parent;sys.path.insert(0,str(P));import clean_cases as c
bpy.ops.wm.open_mainfile(filepath=str(P/'Loft/Rumi_Orbit/editable.blend'),load_ui=False)
case=bpy.data.objects['Case'];c.render('Rumi_Orbit')
top=c.bb(case)[1][2];target=Vector((0,0,top*.5));position=target+Vector((-6,0,2));rotation=(target-position).to_track_quat('-Z','Y')
points=[case.matrix_world@v.co for v in case.data.vertices];bvh=BVHTree.FromPolygons(points,[list(f.vertices) for f in case.data.polygons])
for x,y in [(231,252),(236,430),(232,260),(232,420)]:
 local=Vector(((x/440-.5)*1.55*440/540,(.5-y/540)*1.55,0));origin=position+rotation@local;direction=rotation@Vector((0,0,-1));p,n,i,d=bvh.ray_cast(origin,direction,15)
 print('HIT',x,y,list(p) if p else None,i,flush=True)
