"""Enlarge only the whitelisted seed4781 cottage in the preserved natural scenery."""
import pathlib,sys,json,hashlib,shutil,math,collections
import bpy,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import inspect_cottage as scan
a=scan.a
FACTOR=2.5
BASE_SHIFT=1.1
target=scan.site+scan.n*BASE_SHIFT
final_points=[target+(q-scan.site)*FACTOR for q in scan.points]
# The original flat pad only supported the dollhouse. Keep its floor anchor;
# a closed tapered stone footing below the enlarged base joins the same cliff.
ALONG_SHIFT=0
target.z=scan.site.z
final_points=[target+(q-scan.site)*FACTOR for q in scan.points]
min_lane=float('inf')
for q in final_points:
 s=a.r.nearest_s(q.x,q.y);p,n=a.road.sample(s)
 if abs(s-478)<18 and q.z<a.play(s)+3:
  min_lane=min(min_lane,abs((q-p).dot(n)))
print('COTTAGE_CANDIDATE',json.dumps(dict(min_lane=min_lane,target=list(target))),flush=True)
assert min_lane>3.35,('Cottage crosses the full-size support lateral envelope',min_lane)
target_path=a.OUT/'emerald_abyss_island.fbx'
checkpoint=HERE/'before_emerald_abyss_island.fbx'
if not checkpoint.exists():shutil.copyfile(target_path,checkpoint)
assert hashlib.sha256(target_path.read_bytes()).hexdigest()=='787658b95388a6da6b8171496a92704f968bfd83ae9cace2a119875bf1fcf204','Unexpected scenery baseline'
def mesh_payload(o,exclude=None):
 m=o.data;v=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',v);v=v.reshape(-1,3)
 if exclude:v=np.delete(v,sorted(exclude),axis=0)
 ids=np.empty(len(m.loops),np.int32);m.loops.foreach_get('vertex_index',ids)
 h=hashlib.sha256(v.tobytes()+ids.tobytes())
 for layer in m.color_attributes:
  data=np.empty(len(layer.data)*4,np.float32);layer.data.foreach_get('color',data);h.update(data.tobytes())
 for layer in m.uv_layers:
  data=np.empty(len(layer.data)*2,np.float32);layer.data.foreach_get('uv',data);h.update(data.tobytes())
 return h.hexdigest()
changes=[];baseline={o.name:mesh_payload(o) for o in scan.base if o.type=='MESH'}
before_bounds=scan.report['bounds_min'],scan.report['bounds_max']
for o in scan.base:
 if o.type!='MESH':continue
 box=[o.matrix_world@Vector(c) for c in o.bound_box]
 if any(max(q[k] for q in box)<scan.lo[k] or min(q[k] for q in box)>scan.hi[k] for k in range(3)):continue
 ids={v.index for v in o.data.vertices if all(scan.lo[k]<=(o.matrix_world@v.co)[k]<=scan.hi[k] for k in range(3)) and scan.kd.find(o.matrix_world@v.co)[2]<.0001}
 if not ids:continue
 outside=mesh_payload(o,ids);inverse=o.matrix_world.inverted();normal_data=[tuple(n.vector) for n in o.data.corner_normals]
 for i in ids:
  v=o.data.vertices[i];world=o.matrix_world@v.co;v.co=inverse@(target+(world-scan.site)*FACTOR)
 o.data.update();o.data.normals_split_custom_set(normal_data)
 assert outside==mesh_payload(o,ids),'Unrelated vertices/attributes changed in '+o.name
 changes.append(dict(object=o.name,vertices=len(ids),unmatched_payload_unchanged=True))
changed_names={c['object'] for c in changes}
assert sum(c['vertices'] for c in changes)==scan.report['matched_vertices']
# Extend one existing stone batch, keeping its imported name and material binding.
rock=bpy.data.objects['RA_Rocks_-02_-03_01'];old=rock.data
old_topology=a.topology(old)
old_name=old.name;old_count=len(old.vertices);old_corners=len(old.loops)
verts=[tuple(v.co) for v in old.vertices];faces=[tuple(p.vertices) for p in old.polygons]
old_colors={layer.name:[tuple(x.color) for x in layer.data] for layer in old.color_attributes}
old_uv={layer.name:[tuple(x.uv) for x in layer.data] for layer in old.uv_layers}
normals=[tuple(x.vector) for x in old.corner_normals];smooth=[p.use_smooth for p in old.polygons]
inverse=rock.matrix_world.inverted();base=len(verts)
hw=6.38*.7/2;hd=5.12*.7/2;c=.18
outline=[(-hw+c,-hd),(hw-c,-hd),(hw,-hd+c),(hw,hd-c),(hw-c,hd),(-hw+c,hd),(-hw,hd-c),(-hw,-hd+c)]
for depth,size,inward in [(0,1.03,0),(-1.25,.94,.38),(-3.5,.74,1.2)]:
 for x,y in outline:
  q=target+scan.t*(x*size)+scan.n*(y*size-inward)+Vector((0,0,depth+.10))
  verts.append(tuple(inverse@q))
extra=[tuple(range(base,base+8)),tuple(range(base+23,base+15,-1))]
for ring in range(2):
 for i in range(8):j=(i+1)%8;extra.append((base+ring*8+i,base+(ring+1)*8+i,base+(ring+1)*8+j,base+ring*8+j))
faces.extend(extra)
new=bpy.data.meshes.new(old_name+' enlarged cottage footing');new.from_pydata(verts,[],faces);new.update()
for material in old.materials:new.materials.append(material)
extra_colors=[]
for i,face in enumerate(extra):
 tone=(.43,.46,.43) if i%3==0 else (.36,.40,.39) if i%3==1 else (.40,.43,.40)
 color=tuple(a.r.cob.linear(x) for x in tone)+(1,)
 extra_colors.extend([color]*len(face))
for name,data in old_colors.items():
 layer=new.color_attributes.new(name=name,type='FLOAT_COLOR',domain='CORNER');layer.data.foreach_set('color',np.asarray(data+extra_colors,np.float32).reshape(-1))
for name,data in old_uv.items():
 layer=new.uv_layers.new(name=name);layer.data.foreach_set('uv',np.asarray(data+[(0,0)]*sum(len(f) for f in extra),np.float32).reshape(-1))
for poly,value in zip(new.polygons,smooth):poly.use_smooth=value
for poly in new.polygons[len(smooth):]:normals.extend([tuple(poly.normal)]*len(poly.loop_indices))
new.normals_split_custom_set(normals);rock.data=new;bpy.data.meshes.remove(old);new.name=old_name
assert len(new.vertices)==old_count+24 and len(new.loops)==old_corners+sum(len(f) for f in extra)
footing_mesh=bpy.data.meshes.new('Cottage footing closed verification');footing_mesh.from_pydata(verts[base:],[],[tuple(i-base for i in face) for face in extra]);footing_mesh.update()
footing_topology=a.topology(footing_mesh);bpy.data.meshes.remove(footing_mesh)
assert footing_topology['boundary_edges']==0 and footing_topology['nonmanifold_edges']==0,footing_topology
combined_topology=a.topology(new)
assert combined_topology['boundary_edges']==old_topology['boundary_edges'] and combined_topology['nonmanifold_edges']==old_topology['nonmanifold_edges']
for o in scan.objects:bpy.data.objects.remove(o,do_unlink=True)
untouched={o.name:baseline[o.name] for o in scan.base if o.type=='MESH' and o.name not in changed_names}
assert all(mesh_payload(bpy.data.objects[name])==digest for name,digest in untouched.items())
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_full_cottage.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
 if o.type in('MESH','EMPTY'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(target_path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
report=dict(factor=FACTOR,source_scale_before=.28,source_scale_after=.28*FACTOR,old_site=list(scan.site),new_site=list(target),normal_shift=BASE_SHIFT,along_shift=ALONG_SHIFT,
 old_bounds=before_bounds,new_bounds_min=[min(q[k] for q in final_points) for k in range(3)],new_bounds_max=[max(q[k] for q in final_points) for k in range(3)],
 min_low_body_route_lateral=min_lane,footing_added_vertices=24,footing_closed_topology=footing_topology,footing_bottom_height=target.z-3.4,changed_meshes=changes,untouched_meshes=len(untouched),
 untouched_geometry_attributes_unchanged=True,mixed_faces=0,previous_fbx_sha256=hashlib.sha256(checkpoint.read_bytes()).hexdigest(),fbx_sha256=hashlib.sha256(target_path.read_bytes()).hexdigest())
(HERE/'scale_report.json').write_text(json.dumps(report,indent=2));print('COTTAGE_SCALE_EXPORTED',json.dumps(report),flush=True)
