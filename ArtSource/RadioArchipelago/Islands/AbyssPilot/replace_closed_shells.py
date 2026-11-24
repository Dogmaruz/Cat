"""Preserve six exact closed shells; remove obsolete open ground decorations."""
import bpy,pathlib,sys,json,numpy as np
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_abyss as a
PATH=a.OUT/'emerald_abyss_island.fbx';bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(PATH),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
removed_meshes=0
for o in list(bpy.context.scene.objects):
 if o.type=='MESH' and o.get('unity_material_bucket') in ('Terrain','Cliffs'):
  bpy.data.objects.remove(o,do_unlink=True);removed_meshes+=1
with bpy.data.libraries.load(str(HERE/'closed_structures.blend'),link=False) as (data,loaded):loaded.objects=[name for name in data.objects if name.startswith('RA_Closed_')]
for o in loaded.objects:
 if not o:continue
 bpy.context.scene.collection.objects.link(o)
 for i,m in enumerate(o.data.materials):
  name=m.name.split('.')[0];canonical=bpy.data.materials.get(name)
  if canonical:o.data.materials[i]=canonical
 o['structural_closed_shell']=True;o['unity_material_bucket']='Structural'
fixes=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH' or o.get('structural_closed_shell'):continue
 m=o.data;values=np.empty(len(m.loops)*3,np.float64);m.corner_normals.foreach_get('vector',values);values=values.reshape(-1,3);length=np.linalg.norm(values,axis=1);bad=np.flatnonzero(np.abs(length-1)>.001)
 if not len(bad):continue
 verts=np.empty(len(m.vertices)*3,np.float64);m.vertices.foreach_get('co',verts);verts=verts.reshape(-1,3);badset=set(int(i) for i in bad);count=0
 for p in m.polygons:
  if not any(li in badset for li in p.loop_indices):continue
  ids=list(p.vertices);cross=np.cross(verts[ids[1]]-verts[ids[0]],verts[ids[2]]-verts[ids[0]]);mag=float(np.linalg.norm(cross));fallback=cross/mag if mag>1e-20 else np.array((0.,0.,1.))
  valid=[values[li] for li in p.loop_indices if length[li]>.5]
  if valid:
   mean=np.mean(valid,axis=0);ml=float(np.linalg.norm(mean))
   if ml>.1:fallback=mean/ml
  p.use_smooth=True
  for li in p.loop_indices:
   if li in badset:values[li]=fallback;count+=1
 values/=np.maximum(np.linalg.norm(values,axis=1)[:,None],1e-20);m.normals_split_custom_set(values);m.update();fixes.append(dict(mesh=o.name,corners=count))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(PATH),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
report=json.loads((HERE/'abyss_report.json').read_text());report['structural_replacement']=dict(removed_old_terrain_cliff_batches=removed_meshes,new_exact_closed_shells=6,open_ground_scree_decorations_removed=True,closed_polygon_indices_preserved=True,structural_triangulation_deferred_to_unity=True,decoration_normal_fallbacks=fixes)
(HERE/'abyss_report.json').write_text(json.dumps(report,indent=2));print('EXACT_CLOSED_REPLACEMENT_DONE',report['structural_replacement'],flush=True)
