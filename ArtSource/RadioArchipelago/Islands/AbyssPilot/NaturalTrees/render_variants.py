import bpy,pathlib,sys,json,math
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import botanical as b
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene
materials={}
for k in ('TreeBark','TreeLeaves'):
 m=bpy.data.materials.new('RA_Emerald_'+k);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');vc=m.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color';m.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.86;materials[k]=m
for index in range(6):
 bark,leaf,meta=b.make_variant(index)
 for cat,geo in [('TreeBark',bark),('TreeLeaves',leaf)]:
  obj=geo.object(meta['name']+'_'+cat,materials[cat]);obj.location=(index%3*5.7,index//3*8,0)
scene.world=bpy.data.worlds.new('Botanical soft blue');scene.world.use_nodes=True;bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.65,.77,.88,1);bg.inputs['Strength'].default_value=.7
data=bpy.data.lights.new('Sun','SUN');data.energy=2.5;data.angle=.15;ob=bpy.data.objects.new('Sun',data);scene.collection.objects.link(ob);ob.rotation_euler=(.4,-.5,-.7)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='CUDA'
 scene.cycles.device='GPU'
except:pass
scene.render.resolution_x=1800;scene.render.resolution_y=1200;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
for name,pos,aim,scale in [('six_botanical_variants',(17,-26,15),(5.5,3.5,3),22),('close_branch_leaves',(4,-7,5),(1,0,3.7),6.5)]:
 data=bpy.data.cameras.new(name);data.type='ORTHO';data.ortho_scale=scale;camera=bpy.data.objects.new(name,data);scene.collection.objects.link(camera);camera.location=pos;camera.rotation_euler=(Vector(aim)-camera.location).to_track_quat('-Z','Y').to_euler();scene.camera=camera;scene.render.filepath=str(HERE/(name+'.png'));bpy.ops.render.render(write_still=True);print('NATURAL_VARIANT_PROOF',name,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'botanical_variants.blend'))
