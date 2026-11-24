from pathlib import Path
import bpy,sys,json,numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent));import pack_inspect as a
out=[]
for name in ('Tiko','Nori'):
 folder=a.OUT/'Backpacks'/name;bpy.ops.wm.open_mainfile(filepath=str(folder/'editable.blend'),load_ui=False)
 meta=json.loads((folder/'Meta.json').read_text());case=bpy.data.objects['Case']
 for ob in bpy.context.scene.objects:
  if ob.type=='MESH':ob.hide_render=ob!=case
 points=np.array([list(v.co) for v in case.data.vertices]);front=points[points[:,1]<meta['contactPlaneY']-.04]
 bounds=lambda v:[v.min(0).tolist(),v.max(0).tolist()]
 out.append({'id':name,'caseOnlyBounds':bounds(points),'contactPlaneY':meta['contactPlaneY'],'caseVerticesAheadOfPadBy40mm':len(front),'aheadBounds':bounds(front) if len(front) else None})
 c=a.light_camera();c.data.ortho_scale=1.5;bpy.context.scene.render.resolution_x=600;bpy.context.scene.render.resolution_y=600
 target=Vector((0,0,.45))
 for view,pos in [('back',(3,6,2.5)),('front',(-3,-6,2.5)),('side',(-6,0,2.5))]:
  c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();bpy.context.scene.render.filepath=str(a.QA/(name+'_CASEONLY_'+view+'.png'));bpy.ops.render.render(write_still=True)
(a.QA/'case-only-probe.json').write_text(json.dumps(out,indent=2))
