import bpy,pathlib,sys,json
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_living as a
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(a.PATH),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');scene=bpy.context.scene
for m in bpy.data.materials:
 if m.use_nodes:
  bs=a.a.ex.node_bsdf(m)
  if bs:
   vc=m.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color';m.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Alpha'].default_value=1
scene.world=bpy.data.worlds.new('Living pale blue daylight');scene.world.use_nodes=True;bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.63,.77,.91,1);bg.inputs['Strength'].default_value=.75
ld=bpy.data.lights.new('Living sun','SUN');ld.energy=2.7;ld.angle=.12;lo=bpy.data.objects.new('Living sun',ld);scene.collection.objects.link(lo);lo.rotation_euler=(.55,-.4,-.8)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='CUDA'
 scene.cycles.device='GPU'
except:pass
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
p,n=a.a.road.sample(51.885985);t=Vector((n.y,-n.x,0));eye=p-t*6+n*.4;eye.z=a.a.play(51.885985)+3;target,_=a.a.road.sample(61.8);target.z=a.a.play(61.8)+.25
for name,position,aim,ortho,lens in [('grass_and_massive_path_fragments',eye,target,None,28),('living_cliff_piles',Vector((-10,-55,-6))+Vector((-15,-85,36)),Vector((-10,-55,-6)),117,28)]:
 data=bpy.data.cameras.new(name);data.type='ORTHO' if ortho else'PERSP';data.lens=lens;data.clip_start=.12;data.clip_end=600
 if ortho:data.ortho_scale=ortho
 camera=bpy.data.objects.new(name,data);scene.collection.objects.link(camera);camera.location=position;camera.rotation_euler=(aim-camera.location).to_track_quat('-Z','Y').to_euler();scene.camera=camera;scene.render.filepath=str(HERE/(name+'.png'));bpy.ops.render.render(write_still=True);print('LIVING_PROOF',name,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_living.blend'));report=json.loads((HERE/'living_report.json').read_text());report['final_verification']=json.loads((HERE/'verification.json').read_text());report['final_native_file']='emerald_abyss_living.blend';report['proofs']=['grass_and_massive_path_fragments.png','living_cliff_piles.png'];(HERE/'living_report.json').write_text(json.dumps(report,indent=2));print('LIVING_NATIVE_SAVED',flush=True)
