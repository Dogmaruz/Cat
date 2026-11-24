from pathlib import Path
import bpy,bmesh,sys,json,numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent));import pack_inspect as a;import pack_author as auth
out=[]
for name in ('Tiko','Nori'):
 folder=a.OUT/'Backpacks'/name;bpy.ops.wm.open_mainfile(filepath=str(folder/'editable.blend'),load_ui=False)
 rotor=bpy.data.objects['TopRotor'];pivot=bpy.data.objects['TopPivot']
 for ob in bpy.context.scene.objects:
  if ob.type=='MESH':ob.hide_render=ob!=rotor
 points=np.array([list(rotor.matrix_world@v.co) for v in rotor.data.vertices]);local=np.array([list(v.co) for v in rotor.data.vertices])
 bm=bmesh.new();bm.from_mesh(rotor.data);groups=auth.components(bm);parts=[]
 for g in groups:
  lo,hi=auth.bb_faces(g);parts.append({'faces':len(g),'localMin':list(lo),'localMax':list(hi),'worldMin':[float(lo[i]+pivot.location[i]) for i in range(3)],'worldMax':[float(hi[i]+pivot.location[i]) for i in range(3)]})
 bm.free()
 out.append({'id':name,'pivot':list(pivot.location),'rotorWorldBounds':[points.min(0).tolist(),points.max(0).tolist()],'rotorLocalBounds':[local.min(0).tolist(),local.max(0).tolist()],'verticesBelowJointBy1mm':int((points[:,2]<pivot.location.z-.001).sum()),'components':sorted(parts,key=lambda p:-p['faces'])})
 c=a.light_camera();c.data.ortho_scale=1.5;bpy.context.scene.render.resolution_x=600;bpy.context.scene.render.resolution_y=600
 target=Vector((0,0,1.17))
 for view,pos in [('back',(3,6,2.5)),('front',(-3,-6,2.5)),('side',(-6,0,2.5))]:
  c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();bpy.context.scene.render.filepath=str(a.QA/(name+'_ROTORONLY_'+view+'.png'));bpy.ops.render.render(write_still=True)
(a.QA/'rotor-only-probe.json').write_text(json.dumps(out,indent=2))
