"""Replace only five islet cores and spatially batch their native rock/flower additions."""
import bpy,pathlib,sys,json,math,collections,numpy as np,hashlib,bmesh
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import polish_islets as p
a=p.a;PATH=a.OUT/'emerald_abyss_island.fbx'
def geo_hash(o):
 m=o.data;co=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',co);ids=np.empty(len(m.loops),np.int32);m.loops.foreach_get('vertex_index',ids)
 return dict(vertices=len(m.vertices),polygons=len(m.polygons),hash=hashlib.sha256(co.tobytes()+ids.tobytes()).hexdigest())
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(HERE/'pre_polish_checkpoint.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update()
main=bpy.data.objects['RA_Closed_Mainland'];main_before=geo_hash(main);base=set(bpy.context.scene.objects)
for o in list(base):
 if o.type=='MESH' and o.name.startswith('RA_Closed_') and o!=main:bpy.data.objects.remove(o,do_unlink=True)
base=set(bpy.context.scene.objects);objects=p.build(p.SPECS);cores=[o for o in objects if o.get('structural_closed_shell')];decor=[o for o in objects if not o.get('structural_closed_shell')]
for o in cores:
 for i,m in enumerate(o.data.materials):o.data.materials[i]=bpy.data.materials.get(m.name.split('.')[0]) or m
categories={k:bpy.data.materials.get('RA_Emerald_'+k) or a.r.add_material('RA_Emerald_'+k,k) for k in('Cliffs','Foliage','Wood')};batches={};serial=collections.Counter();output=[];counts=collections.Counter()
for oi,o in enumerate(decor):
 mesh=o.data;matrix=o.matrix_world.copy();world=[matrix@v.co for v in mesh.vertices];category=o.get('abyss_bucket','Cliffs');colors={i:a.ex.color_info(m)[0] for i,m in enumerate(mesh.materials)};remaps={}
 center=sum(world,Vector())/len(world);object_key=(category,math.floor(center.x/24),math.floor(center.y/24))
 if object_key in batches and len(batches[object_key].v)+len(mesh.vertices)>58000:
  output.append(batches[object_key].flush())
 for poly in mesh.polygons:
  c=colors[poly.material_index];key=object_key
  if key not in batches:serial[key]+=1;batches[key]=a.ex.Batch(f'RA_{category}_Polish_{key[1]:+03d}_{key[2]:+03d}_{serial[key]:02d}',category,bpy.context.scene.collection,categories[category])
  batch=batches[key]
  if len(batch.v)+len(poly.vertices)>58000:
   output.append(batch.flush());serial[key]+=1;batches[key]=batch=a.ex.Batch(f'RA_{category}_Polish_{key[1]:+03d}_{key[2]:+03d}_{serial[key]:02d}',category,bpy.context.scene.collection,categories[category]);remaps.pop(key,None)
  remap=remaps.setdefault(key,{});face=[]
  for vi in poly.vertices:
   if vi not in remap:remap[vi]=len(batch.v);batch.v.append(tuple(world[vi]))
   face.append(remap[vi]);batch.colors.append(tuple(c[:3])+(1,));batch.bank.append(0);batch.uv.append((world[vi].x,world[vi].y))
  batch.f.append(tuple(face));batch.soft.append(poly.use_smooth);counts[category]+=len(poly.vertices)-2
for batch in batches.values():
 o=batch.flush()
 if o:output.append(o)
for o in decor:bpy.data.objects.remove(o,do_unlink=True)
fallback=[]
for o in output+cores:
 m=o.data;values=np.empty(len(m.loops)*3,np.float64);m.corner_normals.foreach_get('vector',values);values=values.reshape(-1,3);length=np.linalg.norm(values,axis=1);bad=set(int(i) for i in np.flatnonzero(np.abs(length-1)>.001))
 if bad:
  for poly in m.polygons:
   if not any(li in bad for li in poly.loop_indices):continue
   normal=poly.normal.copy()
   if normal.length<.01:normal=Vector((0,0,1))
   else:normal.normalize()
   poly.use_smooth=True
   for li in poly.loop_indices:
    if li in bad:values[li]=normal
  values/=np.maximum(np.linalg.norm(values,axis=1)[:,None],1e-20);m.normals_split_custom_set(values);fallback.append(dict(mesh=o.name,corners=len(bad)))
 if o.get('unity_material_bucket')=='Cliffs' or o.get('structural_closed_shell'):
  stat=a.topology(m);assert stat['boundary_edges']==0 and stat['nonmanifold_edges']==0,(o.name,stat)
assert geo_hash(main)==main_before,'Mainland mesh changed'
final=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(final)<=300;triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in final);assert triangles<5000000,triangles
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(PATH),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
report=dict(schema='abyss_islet_art_polish/1',pre_polish_sha256=hashlib.sha256((HERE/'pre_polish_checkpoint.fbx').read_bytes()).hexdigest(),mainland_mesh_before=main_before,mainland_mesh_after=geo_hash(main),mainland_unchanged=True,rolling_meadow_caps=5,native_rock_models_per_islet=35,added_spatial_batches=len(output),mesh_batches=len(final),estimated_triangles=triangles,new_material_triangle_counts=dict(counts),normal_fallbacks=fallback,new_closed_structures=[dict(name=o.name,**a.topology(o.data)) for o in cores+output if o.get('structural_closed_shell') or o.get('unity_material_bucket')=='Cliffs'])
(HERE/'polish_report.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_polished_preview.blend'));print('POLISH_MERGED',json.dumps({k:v for k,v in report.items() if k!='new_closed_structures'}),flush=True)
