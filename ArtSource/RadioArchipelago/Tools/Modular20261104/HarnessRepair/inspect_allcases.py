from pathlib import Path
import bpy,json,numpy as np,sys
from mathutils import Vector
root=Path('C:/Users/Derek/Games_develop/Cat')
out=root/'Docs/Design/RadioArchipelago/ModularCast20261104/HarnessRepair';out.mkdir(parents=True,exist_ok=True)
rows=[]
for p in sorted((root/'ArtSource/RadioArchipelago/ProcessedModular/Backpacks').iterdir()):
 if not (p/'editable.blend').exists():continue
 bpy.ops.wm.open_mainfile(filepath=str(p/'editable.blend'))
 meta=json.loads((p/'Meta.json').read_text());row={'id':p.name,'parts':[]}
 for ob in bpy.context.scene.objects:
  if ob.type!='MESH':continue
  v=np.array([list(ob.matrix_world@x.co) for x in ob.data.vertices]); part={'name':ob.name,'verts':len(v),'min':v.min(0).tolist(),'max':v.max(0).tolist()}
  if ob.name=='Case':
   mask=v[:,1]<meta['contactPlaneY']-.04
   part['ahead40mm']=int(mask.sum());part['aheadBounds']=[v[mask].min(0).tolist(),v[mask].max(0).tolist()] if mask.any() else None
  if ob.name.startswith('Harness'):
   np.save(str(out/(p.name+'_'+ob.name+'.npy')),v)
  row['parts'].append(part)
 rows.append(row)
 # Render every source case for visual strap contamination review.
 for ob in list(bpy.context.scene.objects):
  if ob.type=='MESH' and ob.name!='Case':ob.hide_render=True
 sys.path.insert(0,str(root/'ArtSource/RadioArchipelago/Tools/Modular20261104'))
 import pack_inspect as src
 c=src.light_camera();bpy.context.scene.cycles.samples=4
 bpy.context.scene.render.resolution_x=360;bpy.context.scene.render.resolution_y=440
 c.data.ortho_scale=1.8
 target=Vector((0,0,.7));c.location=target+Vector((3,-4,1.8));c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();bpy.context.scene.render.filepath=str(out/(p.name+'_caseonly.png'));bpy.ops.render.render(write_still=True)
(out/'source-inspection.json').write_text(json.dumps(rows,indent=2))

