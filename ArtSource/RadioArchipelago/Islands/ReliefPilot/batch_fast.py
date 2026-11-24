"""Retain the existing spatial batches and merge only the added structural pieces."""
import bpy,bmesh,pathlib,sys,json,math,collections,numpy as np
from mathutils import Vector,Matrix
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_relief as r
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(r.OUT/'emerald_relief_island.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];removed=collections.Counter();normfix=[]
# Original render batches are already spatial/material grouped. Clean only tiny
# decorative slivers with vectorized geometry/normal tests, preserving UV/colors.
for o in meshes:
 m=o.data;n=len(m.vertices);f=len(m.polygons)
 v=np.empty(n*3,np.float64);m.vertices.foreach_get('co',v);v=v.reshape(-1,3)
 ids=np.empty(len(m.loops),np.int32);m.loops.foreach_get('vertex_index',ids);ids=ids.reshape(-1,3)
 norms=np.empty(len(m.loops)*3,np.float64);m.corner_normals.foreach_get('vector',norms);norms=norms.reshape(-1,3)
 e1=v[ids[:,1]]-v[ids[:,0]];e2=v[ids[:,2]]-v[ids[:,0]];e3=e2-e1;cross=np.cross(e1,e2);area2=np.linalg.norm(cross,axis=1);long=np.maximum.reduce((np.sum(e1*e1,axis=1),np.sum(e2*e2,axis=1),np.sum(e3*e3,axis=1)))
 invalid=np.any(np.abs(np.linalg.norm(norms,axis=1).reshape(-1,3)-1)>.01,axis=1)
 category=o.get('unity_material_bucket') or m.materials[0].name.removeprefix('RA_Emerald_').split('.')[0]
 bad=area2<1e-8
 if category in ('Architecture','Metal','Rocks','Wood'):bad|=invalid|(area2/np.maximum(long,1e-30)<.0001)
 if not np.any(bad):continue
 keep=~bad;removed[category]+=int(np.sum(bad));print('FAST_DECOR_CLEAN',o.name,int(np.sum(bad)),flush=True)
 colors=m.color_attributes.get('Color');color=np.empty(len(colors.data)*4,np.float32);colors.data.foreach_get('color',color);color=color.reshape(-1,3,4)[keep].reshape(-1)
 uv=[]
 for layer in m.uv_layers:
  values=np.empty(len(layer.data)*2,np.float32);layer.data.foreach_get('uv',values);uv.append((layer.name,values.reshape(-1,3,2)[keep].reshape(-1)))
 soft=np.empty(f,np.bool_);m.polygons.foreach_get('use_smooth',soft);new=bpy.data.meshes.new(m.name+'_valid')
 new.from_pydata(v,[],ids[keep]);new.update()
 for material in m.materials:new.materials.append(material)
 attr=new.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');attr.data.foreach_set('color',color)
 for name,values in uv:layer=new.uv_layers.new(name=name);layer.data.foreach_set('uv',values)
 new.polygons.foreach_set('use_smooth',soft[keep]);normal=norms.reshape(-1,3,3)[keep].reshape(-1,3);normal/=np.maximum(np.linalg.norm(normal,axis=1)[:,None],1e-20);new.normals_split_custom_set(normal)
 o.data=new
# Merge only the 740 new small structural meshes, using native join to retain
# every corner color/normal and authored UV channel without copying millions of leaves.
groups=collections.defaultdict(list)
for o in meshes:
 if o.name.startswith('Relief_'):
  category=o.get('unity_material_bucket') or o.data.materials[0].name.removeprefix('RA_Emerald_').split('.')[0]
  center=o.matrix_world@sum((Vector(c) for c in o.bound_box),Vector())/8
  groups[(category,math.floor(center.x/24),math.floor(center.y/24))].append(o)
for key,objects in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=objects[0];o.name=f'RA_{key[0]}_{key[1]:+03d}_{key[2]:+03d}_Relief';o['unity_material_bucket']=key[0]
 assert len(o.data.vertices)<58000
output=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(output)<400
r.export_fbx(r.OUT/'emerald_relief_island.fbx',list(bpy.context.scene.objects))
report=json.loads((HERE/'relief_report.json').read_text());report['final_spatial_batching']=dict(meshes=len(output),cell_meters=24,max_vertices=58000,removed_decorative_sliver_faces=dict(removed),vertex_colors_preserved=True,uv0_and_uv1_preserved=True,source_spatial_batches_retained=True,joined_structural_batches=len(groups))
(HERE/'relief_report.json').write_text(json.dumps(report,indent=2));print('FAST_BATCH_FINAL',report['final_spatial_batching'],flush=True)
