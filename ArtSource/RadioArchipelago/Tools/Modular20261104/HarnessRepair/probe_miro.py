from pathlib import Path
import bpy,json,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=Path('C:/Users/Derek/Games_develop/Cat')
for name in ['Miro','Miro_Garden']:
 bpy.ops.wm.open_mainfile(filepath=str(R/'ArtSource/RadioArchipelago/ProcessedModular/Backpacks'/name/'editable.blend'))
 o=bpy.data.objects['Case'];v=[o.matrix_world@p.co for p in o.data.vertices];t=[list(p.vertices) for p in o.data.polygons];bv=BVHTree.FromPolygons(v,t)
 out=[]
 for x in [0,.05,-.05,.1,-.1]:
  for z in [.3,.4,.5,.6,.7]:
   p,n,i,d=bv.ray_cast(Vector((x,-3,z)),Vector((0,1,0)),6)
   out.append([x,z,list(p) if p else None])
 print('CONTACT',name,json.dumps(out),flush=True)
