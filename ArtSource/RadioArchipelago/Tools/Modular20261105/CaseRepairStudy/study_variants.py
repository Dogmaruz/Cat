from pathlib import Path
import bpy,json,sys,numpy as np
from mathutils import Vector
HERE=Path(__file__).parent;OUT=HERE/'FinalVariants';OUT.mkdir(exist_ok=True)
sys.path.insert(0,str(HERE.parents[1]/'Modular20261104'));import pack_inspect as source
for name in ('Lumen_Glider','Fira_Beacon'):
 folder=OUT/name;folder.mkdir(exist_ok=True)
 original=HERE.parent/'StraplessCases/Before'/name
 bpy.ops.wm.open_mainfile(filepath=str(original/'editable.blend'),load_ui=False)
 case=bpy.data.objects['Case'];meta=json.loads((original/'Meta.json').read_text())
 for ob in bpy.context.scene.objects:
  if ob.type=='MESH':ob.hide_render=ob!=case
 a=np.asarray([list(v.co) for v in case.data.vertices]);np.save(folder/'source-points.npy',a)
 rows=[]
 for z in np.arange(0,meta['caseBoundsMax'][2]+.2,.05):
  v=a[(a[:,2]>=z)&(a[:,2]<z+.05)]
  if len(v):rows.append({'z':float(z),'lo':v.min(0).tolist(),'hi':v.max(0).tolist(),'count':len(v)})
 (folder/'source-rows.json').write_text(json.dumps(rows,indent=2))
 c=source.light_camera();sc=bpy.context.scene;sc.cycles.samples=6;sc.render.resolution_x=600;sc.render.resolution_y=700;c.data.ortho_scale=1.6
 target=Vector((0,0,meta['caseBoundsMax'][2]*.5))
 for view,pos in [('contact',(0,-6,.6)),('side',(-6,0,.6)),('radio',(0,6,.6)),('top',(0,0,6))]:
  c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(folder/('original-'+view+'.png'));bpy.ops.render.render(write_still=True)
