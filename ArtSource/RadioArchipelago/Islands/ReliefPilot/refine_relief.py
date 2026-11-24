"""Review-driven derivative-only road shoulder grading and anchor normalization."""
import bpy,json,pathlib,sys,math
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import build_relief as r
bpy.ops.wm.open_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'))
root=bpy.data.objects['EmeraldReliefIsland'];island=[root]+list(root.children_recursive);island_set=set(island)
cobbles=[o for o in bpy.context.scene.objects if o not in island_set]
maximum=0;affected=0
for o in island:
 if o.type!='MESH' or o.get('unity_material_bucket')!='Terrain':continue
 m=o.data;matrix=o.matrix_world.copy();inv=matrix.inverted()
 for v in m.vertices:
  w=matrix@v.co;delta=r.terrain_h(w.x,w.y)-r.ground(w.x,w.y)
  if abs(delta)>.00001:affected+=1;maximum=max(maximum,abs(delta))
  w.z+=delta;v.co=inv@w
 m.update()
r.export_fbx(r.OUT/'emerald_relief_island.fbx',island)
for name in ('RA_Origin','RA_X10','RA_North10'):
 bpy.data.objects[name].name='IslandOnly_'+name
 for o in cobbles:
  if o.name.startswith(name+'.'):o.name=name
r.export_fbx(r.OUT/'emerald_relief_cobbles.fbx',cobbles)
for name in ('RA_Origin','RA_X10','RA_North10'):
 for o in cobbles:
  if o.name==name:o.name='CobblesOnly_'+name
 bpy.data.objects['IslandOnly_'+name].name=name
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'))
report=json.loads((HERE/'relief_report.json').read_text());report['review_refinement']=dict(road_shoulders_flattened=True,stair_underbed_cut=.24,affected_terrain_vertices=affected,max_vertical_correction=maximum,cobble_anchor_names_normalized=True)
(HERE/'relief_report.json').write_text(json.dumps(report,indent=2));print('RELIEF_REFINED',report['review_refinement'],flush=True)
# Review cameras are genuine Blender renders of the same derivative geometry.
scene=bpy.context.scene
for m in bpy.data.materials:
 if m.name.startswith('RA_Emerald') and m.use_nodes:
  bs=r.ex.node_bsdf(m)
  if bs:
   attr=m.node_tree.nodes.new('ShaderNodeVertexColor');attr.layer_name='Color';m.node_tree.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
scene.world=bpy.data.worlds.new('Refined daylight');scene.world.use_nodes=True;bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.63,.76,.91,1);bg.inputs['Strength'].default_value=.65
ld=bpy.data.lights.new('Refined sun','SUN');ld.energy=2.8;ld.angle=.12;lo=bpy.data.objects.new('Refined sun',ld);scene.collection.objects.link(lo);lo.rotation_euler=(.55,-.4,-.8)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for device in prefs.devices:device.use=device.type=='CUDA'
 scene.cycles.device='GPU'
except:pass
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
for name,target,offset,scale in [('wood_stairs',(43,36,7),(-12,22,31),42),('hill_house',(60,23,8),(53,-72,43),83),('stone_bridge',(13,-6,.8),(-22,-26,15),34),('overview',(4,-2,-5),(180,-230,190),215),('top',(5,0,0),(0,0,250),205)]:
 data=bpy.data.cameras.new('Refined '+name);data.type='ORTHO';data.ortho_scale=scale;camera=bpy.data.objects.new(data.name,data);scene.collection.objects.link(camera)
 camera.location=Vector(target)+Vector(offset);camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();scene.camera=camera
 scene.render.filepath=str(r.PROOF/(name+'.png'));bpy.ops.render.render(write_still=True);print('REFINED_PROOF',name,flush=True)
