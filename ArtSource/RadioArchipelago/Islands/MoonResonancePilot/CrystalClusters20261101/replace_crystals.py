"""Replace only original Crystal solids and their three isolated copper-band batches."""
import bpy, bmesh, pathlib, sys, json, hashlib, shutil, collections, math
import numpy as np
HERE=pathlib.Path(__file__).resolve().parent;SOURCE=HERE.parent
sys.path.insert(0,str(SOURCE))
import build_moon_resonance as api
import crystal_cluster_design as design

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
target=api.OUT/'moon_resonance_island.fbx'
assert sha(target)in('07adcf7d27946f561c7d878acd98dae817776a30efee9509ae714a1e477bec0c','61c7fcf48bae8e023d527dccaf33845c8f5249f6bcd2248c3598e6b8c155299f'),'Unexpected Moon baseline'
backup=HERE/'before_moon_resonance_island.fbx'
if not backup.exists():
 assert sha(target)=='07adcf7d27946f561c7d878acd98dae817776a30efee9509ae714a1e477bec0c'
 shutil.copyfile(target,backup)
assert sha(backup)=='07adcf7d27946f561c7d878acd98dae817776a30efee9509ae714a1e477bec0c'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'moon_resonance_preview.blend'))
base=[o for o in bpy.context.scene.objects if o.type=='MESH'and o.name.startswith('RA_Moon_')]
keep=[o for o in bpy.context.scene.objects if o.type=='EMPTY']
crystals=[o for o in base if o.get('moon_bucket')=='Crystal']
bands=[o for o in base if o.name in('RA_Moon_Metal_-01_+02_01','RA_Moon_Metal_+02_+01_01','RA_Moon_Metal_-03_+00_01')]
assert len(crystals)==3 and sum(len(o.data.polygons)for o in crystals)==440
assert len(bands)==3 and sum(len(o.data.polygons)for o in bands)==8734
modified_names={o.name for o in crystals+bands}

def payload(o):
 m=o.data;h=hashlib.sha256()
 for attr,size,dtype,prop in[(m.vertices,3,np.float32,'co'),(m.loops,1,np.int32,'vertex_index')]:
  values=np.empty(len(attr)*size,dtype);attr.foreach_get(prop,values);h.update(values.tobytes())
 smooth=np.empty(len(m.polygons),np.bool_);m.polygons.foreach_get('use_smooth',smooth);h.update(smooth.tobytes())
 for layer in m.color_attributes:
  data=np.empty(len(layer.data)*4,np.float32);layer.data.foreach_get('color',data);h.update(data.tobytes())
 for layer in m.uv_layers:
  data=np.empty(len(layer.data)*2,np.float32);layer.data.foreach_get('uv',data);h.update(data.tobytes())
 return h.hexdigest()
baseline={o.name:payload(o)for o in base if o.name not in modified_names}
api.prepare_cap_index();groups=design.design(api)
assert len(groups)>=20,len(groups)
assert set(g['count']for g in groups)==set(range(2,8))
print('NEW_DRUSES',json.dumps([{k:v for k,v in g.items()if k not in('vertices','faces','colors','shards')}for g in groups]),flush=True)
centres=[sum((o.matrix_world@v.co for v in o.data.vertices),start=api.Vector((0,0,0)))/len(o.data.vertices)for o in crystals]
outputs=[dict(v=[],f=[],c=[])for o in crystals]
for g in groups:
 p=api.Vector(g['position']);i=min(range(len(centres)),key=lambda i:(p-centres[i]).length)
 output=outputs[i];offset=len(output['v']);output['v'].extend(g['vertices']);output['f'].extend(tuple(offset+j for j in f)for f in g['faces']);output['c'].extend(g['colors'])
for o,data in zip(crystals,outputs):
 assert data['f']
 old=o.data;name=old.name;matrix=o.matrix_world.inverted()
 m=bpy.data.meshes.new(name+' hexagonal druses');m.from_pydata([tuple(matrix@api.Vector(p))for p in data['v']],[],data['f']);m.update()
 for material in old.materials:m.materials.append(material)
 layer=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');layer.data.foreach_set('color',np.asarray(data['c'],np.float32).reshape(-1))
 uv=m.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',[c for loop in m.loops for c in(data['v'][loop.vertex_index][0],data['v'][loop.vertex_index][1])])
 bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free();m.update()
 o.data=m;bpy.data.meshes.remove(old);m.name=name
 # Recalculation on complete new solids retains planar six-sided shading.
 for polygon in m.polygons:polygon.use_smooth=False
for o in bands:bpy.data.objects.remove(o,do_unlink=True)
for name,digest in baseline.items():assert payload(bpy.data.objects[name])==digest,('Unrelated mesh changed',name)
for o in list(bpy.context.scene.objects):
 if o.type!='EMPTY'and o not in base:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'moon_resonance_hexagonal_druses.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
 if o.type in('MESH','EMPTY'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=True,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
report=dict(old_crystals=11,old_crystal_triangles=440,removed_copper_band_triangles=8734,new_clusters=len(groups),new_crystals=sum(g['count']for g in groups),counts_per_cluster=sorted(set(g['count']for g in groups)),true_hexagonal_sides=6,
 new_crystal_triangles=sum(len(o.data.polygons)for o in crystals),min_route_clearance=min(g['minimum_route_distance']for g in groups),
 untouched_meshes=len(baseline),untouched_mesh_payloads_preserved=True,changed_batches=sorted(modified_names),source_sha256=sha(backup),new_sha256=sha(target),
 groups=[{k:v for k,v in g.items()if k not in('vertices','faces','colors')}for g in groups])
(HERE/'crystal-clusters.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('CRYSTALS_EXPORTED',json.dumps({k:v for k,v in report.items()if k!='groups'}),flush=True)
