from pathlib import Path
import bpy,json
from mathutils import Vector
p=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(p/'MiroGardenCandidate/Miro_Garden/editable.blend'),load_ui=False)
o=bpy.data.objects['Case'];out=[]
for x in (-.04,0,.04):
 for y in (-.14,-.10,-.06,-.02,.02):
  hit,co,n,idx=o.ray_cast(Vector((x,y,2)),Vector((0,0,-1)))
  row={'x':x,'y':y,'hit':hit,'co':list(co),'normal':list(n),'face':idx}
  if hit:
   f=o.data.polygons[idx];layer=o.data.uv_layers.active;uv=sum((layer.data[i].uv for i in f.loop_indices),Vector((0,0)))/len(f.loop_indices)
   row['uv']=list(uv);row['material']=f.material_index
  out.append(row)
(p/'roof-rays.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
