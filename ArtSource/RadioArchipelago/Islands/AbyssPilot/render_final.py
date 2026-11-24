"""Actual final-FBX evidence, including flight-eye-height views and deep undersides."""
import bpy,pathlib,sys,json,math
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_abyss as a
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(a.OUT/'emerald_abyss_island.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
scene=bpy.context.scene
for m in bpy.data.materials:
 if m.use_nodes:
  bs=a.ex.node_bsdf(m)
  if bs:
   vc=m.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color';m.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.88 if 'Metal' not in m.name else .36
scene.world=bpy.data.worlds.new('Abyss pale blue daylight');scene.world.use_nodes=True;bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.60,.76,.91,1);bg.inputs['Strength'].default_value=.7
sun_data=bpy.data.lights.new('Abyss sun','SUN');sun_data.energy=2.8;sun_data.angle=.12;sun=bpy.data.objects.new('Abyss sun',sun_data);scene.collection.objects.link(sun);sun.rotation_euler=(.55,-.4,-.8)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='CUDA'
 scene.cycles.device='GPU'
except:pass
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
views=[]
def camera(name,position,target,ortho=None,lens=26):
 data=bpy.data.cameras.new(name);data.type='ORTHO' if ortho else 'PERSP';data.lens=lens;data.clip_start=.12;data.clip_end=600
 if ortho:data.ortho_scale=ortho
 obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=position;obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler();scene.camera=obj
 scene.render.filepath=str(a.PROOF/(name+'.png'));bpy.ops.render.render(write_still=True);views.append(dict(name=name,position=list(obj.location),target=list(target),projection=data.type,lens=lens));print('ABYSS_FINAL_PROOF',name,flush=True)
def player(name,s,look):
 p,n=a.road.sample(s);target,_=a.road.sample(look);t=Vector((n.y,-n.x,0));p-=t*2.5;p.z=a.play(s)+3.5;target.z=a.play(look)+1.1;camera(name,p,target,lens=22)
if '--final-two' in sys.argv:
 camera('floating_islets_final',Vector((-10,-55,-6))+Vector((-15,-85,36)),(-10,-55,-6),ortho=117)
 camera('stone_bridge_final',Vector((13,-6,.8))+Vector((-22,-26,15)),(13,-6,.8),ortho=34)
 bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_final.blend'));(HERE/'proof_cameras.json').write_text(json.dumps(views,indent=2))
 report=json.loads((HERE/'abyss_report.json').read_text());report['final_verification']=json.loads((HERE/'verification.json').read_text());report['final_native_file']='emerald_abyss_final.blend';report['proofs_show_final_serialized_geometry']=True;report['legacy_proofs']='Proof_PreCoastalOpening: previous coast geometry, not final';report['coastal_open_to_outside']=True
 (HERE/'abyss_report.json').write_text(json.dumps(report,indent=2));print('ABYSS_FINAL_NATIVE_SAVED',flush=True);sys.exit(0)
player('ravine_player_height',130,153)
player('sky_player_height',411,435)
player('sky_descent_player_height',442,477)
camera('floating_islets',Vector((-10,-55,-6))+Vector((-15,-85,36)),(-10,-55,-6),ortho=117)
camera('overview',Vector((4,-2,-5))+Vector((180,-230,190)),(4,-2,-5),ortho=215)
camera('solid_cliff_undersides',(-35,-111,-29),(-9,-45,-13),lens=30)
camera('grass_hill_house',Vector((60,23,8))+Vector((53,-72,43)),(60,23,8),ortho=83)
camera('ravine_side',(-66,33,6),(-24,31,-8),lens=28)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_final.blend'));(HERE/'proof_cameras.json').write_text(json.dumps(views,indent=2))
report=json.loads((HERE/'abyss_report.json').read_text());report['final_verification']=json.loads((HERE/'verification.json').read_text());report['final_native_file']='emerald_abyss_final.blend';report['proofs_show_final_serialized_geometry']=True
(HERE/'abyss_report.json').write_text(json.dumps(report,indent=2));print('ABYSS_FINAL_NATIVE_SAVED',flush=True)
