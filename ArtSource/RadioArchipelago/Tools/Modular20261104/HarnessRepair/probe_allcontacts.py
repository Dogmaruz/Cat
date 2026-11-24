from pathlib import Path
import bpy,json,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=Path('C:/Users/Derek/Games_develop/Cat');rows=[]
for p in sorted((R/'ArtSource/RadioArchipelago/ProcessedModular/Backpacks').iterdir()):
 if not (p/'editable.blend').exists():continue
 bpy.ops.wm.open_mainfile(filepath=str(p/'editable.blend'));m=json.loads((p/'Meta.json').read_text());o=bpy.data.objects['Case'];v=[o.matrix_world@x.co for x in o.data.vertices];b=BVHTree.FromPolygons(v,[list(f.vertices) for f in o.data.polygons]);ys=[];h=m['caseBoundsMax'][2]-m['caseBoundsMin'][2]
 for x in [0,-.04,.04]:
  for z in [h*.35,h*.45,h*.55,h*.65]:
   q,n,i,d=b.ray_cast(Vector((x,-3,z)),Vector((0,1,0)),6)
   if q:ys.append(q.y)
 rows.append({'id':p.name,'old':m['contactPlaneY'],'actual':float(np.median(ys)),'diff':float(np.median(ys))-m['contactPlaneY']})
(R/'Docs/Design/RadioArchipelago/ModularCast20261104/HarnessRepair/all-pad-contacts.json').write_text(json.dumps(rows,indent=2))
