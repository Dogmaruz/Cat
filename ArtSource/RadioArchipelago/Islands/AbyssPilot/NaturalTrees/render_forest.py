import bpy,pathlib,sys,math
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_natural as n
a=n.a;bpy.ops.wm.open_mainfile(filepath=str(HERE/'emerald_abyss_natural.blend'));scene=bpy.context.scene
for material in bpy.data.materials:
 if material.use_nodes:
  bs=a.ex.node_bsdf(material)
  if bs:
   vc=material.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color';material.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Alpha'].default_value=1
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='CUDA'
 scene.cycles.device='GPU'
except:pass
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
p,normal=a.road.sample(51.885985);t=Vector((normal.y,-normal.x,0));eye=p-t*6+normal*.4;eye.z=a.play(51.885985)+3;target,_=a.road.sample(61.8);target.z=a.play(61.8)+1.6
spec=next(s for s in n.p.SPECS if s['distance']==420);c,t0,nn=n.p.frame(spec);isleteye=c-t0*20+nn*21+Vector((0,0,7));aim=c+Vector((0,0,3))
for name,position,aim,lens in [('natural_forest_player_height',eye,target,28),('natural_islet_trees',isleteye,aim,36)]:
 data=bpy.data.cameras.new(name);data.type='PERSP';data.lens=lens;data.clip_start=.1;data.clip_end=600;camera=bpy.data.objects.new(name,data);scene.collection.objects.link(camera);camera.location=position;camera.rotation_euler=(aim-camera.location).to_track_quat('-Z','Y').to_euler();scene.camera=camera;scene.render.filepath=str(HERE/(name+'.png'));bpy.ops.render.render(write_still=True);print('NATURAL_PROOF',name,flush=True)
