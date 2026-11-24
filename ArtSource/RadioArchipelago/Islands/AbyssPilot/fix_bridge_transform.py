import bpy,pathlib,sys,json
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_abyss as a
PATH=a.OUT/'emerald_abyss_island.fbx';bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(PATH),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update()
for o in list(bpy.context.scene.objects):
 if o.type=='MESH' and o.get('unity_material_bucket')=='BridgeDeck':bpy.data.objects.remove(o,do_unlink=True)
old=set(bpy.context.scene.objects);bpy.ops.import_scene.fbx(filepath=str(a.r.OUT/'emerald_relief_island.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update();new=set(bpy.context.scene.objects)-old
bridges=[]
for o in new:
 if o.type=='MESH' and o.get('unity_material_bucket')=='BridgeDeck':
  world=o.matrix_world.copy();o.parent=None;o.matrix_world=world;bridges.append(o)
  for i,m in enumerate(o.data.materials):o.data.materials[i]=bpy.data.materials.get('RA_Emerald_BridgeDeck') or m
for o in new:
 if o not in bridges:bpy.data.objects.remove(o,do_unlink=True)
bpy.context.view_layer.update();points=[o.matrix_world@Vector(c) for o in bridges for c in o.bound_box];bounds=dict(min=[min(p[i] for p in points) for i in range(3)],max=[max(p[i] for p in points) for i in range(3)])
print('BRIDGE_WORLD_BOUNDS',bounds,flush=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(PATH),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
report=json.loads((HERE/'abyss_report.json').read_text());report['bridge_world_transform_verified_from_relief_fbx']=bounds;(HERE/'abyss_report.json').write_text(json.dumps(report,indent=2))
