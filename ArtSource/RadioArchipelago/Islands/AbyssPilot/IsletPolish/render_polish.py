import bpy,pathlib,sys,json
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import polish_islets as p
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(p.a.OUT/'emerald_abyss_island.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
scene=bpy.context.scene
for m in bpy.data.materials:
 if m.use_nodes:
  bs=p.a.ex.node_bsdf(m)
  if bs:
   vc=m.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color';m.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color'])
scene.world=bpy.data.worlds.new('Polished blue daylight');scene.world.use_nodes=True;bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.63,.77,.91,1);bg.inputs['Strength'].default_value=.75
ld=bpy.data.lights.new('Polished sun','SUN');ld.energy=2.7;ld.angle=.12;lo=bpy.data.objects.new('Polished sun',ld);scene.collection.objects.link(lo);lo.rotation_euler=(.55,-.4,-.8)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='CUDA'
 scene.cycles.device='GPU'
except:pass
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
spec=next(x for x in p.SPECS if x['distance']==478);center,t,n=p.frame(spec);target=center.copy();target.z=spec['cap_height']
for name,aim,offset,scale in [('polished_islet_bodies',Vector((-10,-55,-6)),Vector((-15,-85,36)),117),('polished_meadow_cottage',target,n*16+t*10+Vector((0,0,14)),21)]:
 data=bpy.data.cameras.new(name);data.type='ORTHO';data.ortho_scale=scale;cam=bpy.data.objects.new(name,data);scene.collection.objects.link(cam);cam.location=aim+offset;cam.rotation_euler=(aim-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam;scene.render.filepath=str(HERE/(name+'.png'));bpy.ops.render.render(write_still=True);print('FINAL_POLISH_PROOF',name,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_polished.blend'));report=json.loads((HERE/'polish_report.json').read_text());report['final_verification']=json.loads((HERE/'verification.json').read_text());report['final_native_file']='emerald_abyss_polished.blend';report['final_proofs']=['polished_islet_bodies.png','polished_meadow_cottage.png'];(HERE/'polish_report.json').write_text(json.dumps(report,indent=2));print('POLISH_NATIVE_SAVED',flush=True)
