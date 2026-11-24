"""Extract only authored rooftop rotors from the latest preserved batched scenery.
Writes STAGING only. Existing FBX, blends, shaders and Unity assets remain untouched.
"""
import bpy, pathlib, sys, math, json, hashlib, collections, argparse
import numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree

HERE=pathlib.Path(__file__).resolve().parent; REPO=HERE.parents[3]; ISLANDS=HERE.parent
sys.path[:0]=[str(ISLANDS/'V4'),str(ISLANDS/'V4/UnityExport')]
import architecture
parser=argparse.ArgumentParser();parser.add_argument('--level',required=True);args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
LEVEL=args.level
CONFIG={
 'emerald':('Emerald','AbyssPilot/CottageScale20261101/emerald_abyss_full_cottage.blend','EmeraldAbyss/emerald_abyss_island.fbx','80ad45e4c2aa7e02aafbf7d9c7c5ef018026ac4d372d738bd9b2defe71de0e2b'),
 'coral':('Coral','CoralOasisPilot/coral_oasis_preview.blend','CoralOasis/coral_oasis_island.fbx','4cc6a188004f508ae440e79a69bf80e3e1488ed45768f82b30871be2b2a713fc'),
 'moon':('Moon','MoonResonancePilot/CrystalClusters20261101/moon_resonance_hexagonal_druses.blend','MoonResonance/moon_resonance_island.fbx','61c7fcf48bae8e023d527dccaf33845c8f5249f6bcd2248c3598e6b8c155299f')
}
label,source,fbx_rel,expected=CONFIG[LEVEL]
source=ISLANDS/source;input_fbx=REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands'/fbx_rel
archived=HERE/'Before'/pathlib.Path(fbx_rel).parent/pathlib.Path(fbx_rel).name
if archived.exists():input_fbx=archived
sha=lambda p:hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
input_sha=sha(input_fbx)
if expected:assert input_sha==expected,(LEVEL,input_sha)
OUT=HERE/'STAGING'/label;OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source));base=set(bpy.context.scene.objects)
meshes=[o for o in base if o.type=='MESH']
root=next(o for o in base if o.type=='EMPTY' and ('Island' in o.name or 'Emerald' in o.name))
groups=[]; fixed=[];captures=[]
def xyz(v):return dict(x=float(v[0]),y=float(v[2]),z=float(-v[1]))
def capture_antenna(self,x,y,z,size=1):
 start=len(self.objects);old_antenna(self,x,y,z,size)
 captures.append(dict(parts=[o for o in self.objects[start:] if o.name.endswith('aerial fork') or 'aerial terminal' in o.name],
                      pivot=Vector((x,y,z+1.12*size)),kind='fork',size=size))
def capture_dish(self,x,y,z,size=1):
 start=len(self.objects);old_dish(self,x,y,z,size)
 captures.append(dict(parts=self.objects[start:].copy(),pivot=Vector((x,y,z)),kind='dish',size=size))
old_antenna=architecture._Builder.antenna;old_dish=architecture._Builder.dish
architecture._Builder.antenna=capture_antenna;architecture._Builder.dish=capture_dish
def ground(x,y):
 verts=[];faces=[]
 for o in meshes:
  if 'Terrain' not in o.name:continue
  offset=len(verts);verts.extend(o.matrix_world@v.co for v in o.data.vertices)
  faces.extend(tuple(offset+i for i in p.vertices) for p in o.data.polygons)
 tree=BVHTree.FromPolygons(verts,faces,all_triangles=True)
 hit,_,_,_=tree.ray_cast(Vector((x,y,1000)),Vector((0,0,-1)),2000)
 assert hit is not None,(x,y)
 return hit.z
def building(kind,pos,yaw,scale,seed,palette=None,remount=False):
 start=len(captures)
 objects=architecture.build_building(kind,location=pos,yaw=yaw,scale=scale,seed=seed,palette=palette)
 transform=Matrix.Translation(Vector(pos))@Matrix.Rotation(yaw,4,'Z')@Matrix.Scale(scale,4)
 for item in captures[start:]:
  item['pivot']=transform@item['pivot']
  if item['kind']=='fork':
   item.update(id=len(groups),name='RA_Rooftop_'+label+'_'+str(len(groups)+1).zfill(2)+'_Fork',axis=Vector((0,0,1)),speed=[95,-115,75,-135][len(groups)%4])
   groups.append(item)
  elif remount:
   # Original dish pedestal was h+.68, but its upper red roof cap is h+1.435.
   # Seat the complete original assembly on that cap, not within its masonry.
   item['delta']=Vector((0,0,.755*scale));fixed.append(item)
 return objects

if LEVEL=='emerald':
 sys.path.insert(0,str(ISLANDS/'ReliefPilot'));import build_relief as relief
 manifest=json.loads((ISLANDS/'V4/Output/01_emerald_ravine/manifest.json').read_text())
 for j,spec in enumerate(manifest['buildings']):
  pos=spec['position'].copy();pos[2]+=relief.ground(pos[0],pos[1])
  building(spec['kind'],pos,spec['yaw'],spec['scale'],j,palette={'style':'forest'})
 report=json.loads((ISLANDS/'AbyssPilot/CottageScale20261101/scale_report.json').read_text())
 inspection=json.loads((ISLANDS/'AbyssPilot/CottageScale20261101/inspection.json').read_text())
 building('forest_house',report['new_site'],inspection['yaw'],.7,4781)
elif LEVEL=='coral':
 sys.path.insert(0,str(ISLANDS/'CoralOasisPilot'));import build_coral_oasis as api
 for j,(kind,(x,y),scale,yaw) in enumerate([('adobe_house',(-6,-39),1.1,.15),('adobe_house',(118,-18),1.25,-.9),('adobe_tower',(1,73),1.25,0)]):
  objects=building(kind,(x,y,ground(x,y)-.05),yaw,scale,1260+j,remount=True)
  if j==2:
   parts=[o for o in objects if o.name.endswith('thick ring receiver') or o.name.endswith('receiver cradle fork')]
   groups.append(dict(id=len(groups),name='RA_Rooftop_Coral_03_Receiver',parts=parts,pivot=Vector((x,y,ground(x,y)-.05+7.35*scale)),axis=Vector((0,0,1)),speed=55,kind='receiver'))
 x,y=1,73;z=api.terrain_height(x,y)+12.2;parts=[]
 for side,radius in enumerate((3.7,3.45)):
  parts.append(api.tube('TMP_Roof_Ring',[(x+radius*math.cos(a),y+side*.16,z+radius*math.sin(a))for a in np.linspace(0,math.tau,97)],.13,'Metal',(.82,.57,.21),10))
 for angle in np.linspace(0,math.tau,8,endpoint=False):
  parts.append(api.tube('TMP_Roof_Spoke',[(x,y,z),(x+3.5*math.cos(angle),y,z+3.5*math.sin(angle))],.075,'Metal',(.66,.44,.17),8))
 groups.append(dict(id=len(groups),name='RA_Rooftop_Coral_04_Wheel',parts=parts,pivot=Vector((x,y,z)),axis=Vector((0,1,0)),speed=-80,kind='wheel'))
else:
 sys.path.insert(0,str(ISLANDS/'MoonResonancePilot'));import build_moon_resonance as api
 palette=dict(style='cloud',wall='#F1ECEB',roof='#A97561',wood='#766B84',stone='#A6A3B8',metal='#D2AE76',accent='#80BCBC',trim='#DBD4E5',glass='#46627F')
 for j,(kind,(x,y),scale,yaw) in enumerate(api.BUILD_SITES):
  # Moon's ground audit explicitly moved these roots from z-.08 back to z.
  building('cloud_workshop' if kind=='cloud_observatory' else kind,(x,y,ground(x,y)),yaw,2.0 if kind=='cloud_observatory' else scale,410+j,palette)
 x,y=-24,165;z=ground(x,y)+20.5;parts=[]
 for shift,r in [(0,7.8),(.32,7.5)]:
  parts.append(api.tube('TMP_Roof_Moon_Ring',[(x+r*math.cos(a),y+shift,z+r*math.sin(a))for a in np.linspace(0,math.tau,145)],.25,'Metal',(.82,.72,.57),12))
 for a in np.linspace(0,math.tau,12,endpoint=False):
  parts.append(api.tube('TMP_Roof_Moon_Tab',[(x+7.25*math.cos(a),y-.04,z+7.25*math.sin(a)),(x+8.05*math.cos(a),y-.04,z+8.05*math.sin(a))],.13,'Metal',(.94,.82,.58),8))
 groups.append(dict(id=len(groups),name='RA_Rooftop_Moon_04_Wheel',parts=parts,pivot=Vector((x,y,z)),axis=Vector((0,1,0)),speed=65,kind='wheel'))

# Build a point whitelist from exactly the original authored components.
# Objects created above are guides only; all actual visible geometry/attributes
# are copied from the existing exported batches.
deps=bpy.context.evaluated_depsgraph_get();deps.update()
points=[];point_group=[];guide_counts=[]
for group in groups+fixed:
 group['marker']=len(guide_counts)+1
 count=0
 for ob in group['parts']:
  data=bpy.data.meshes.new_from_object(ob.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps) if ob.modifiers else ob.data
  for vertex in data.vertices:points.append(ob.matrix_world@vertex.co);point_group.append(group['marker']);count+=1
  if data!=ob.data:bpy.data.meshes.remove(data)
 guide_counts.append(count)
# Ground-support auditing used the original terrain triangles before the last
# export round trip. Recover only a constant sub-centimetre vertical offset;
# exact X/Y and the full 3D whitelist are still required below.
native_points=[o.matrix_world@v.co for o in meshes if any(k in o.name for k in ('Metal','Architecture','Rocks')) for v in o.data.vertices]
native_kd=KDTree(len(native_points))
for i,p in enumerate(native_points):native_kd.insert(p,i)
native_kd.balance()
for group in groups+fixed:
 ids=[i for i,tag in enumerate(point_group)if tag==group['marker']]
 sample=ids[::max(1,len(ids)//15)]
 diffs=[native_kd.find(points[i])[0]-points[i]for i in sample]
 aligned=[d.z for d in diffs if abs(d.x)<.0001 and abs(d.y)<.0001]
 if len(aligned)>=3:
  bias=float(np.median(aligned))
  if abs(bias)<.05 and max(aligned)-min(aligned)<.0001:
   shifted=[points[i]+Vector((0,0,bias))for i in ids]
   coverage=sum(native_kd.find(p)[2]<.0003 for p in shifted)/len(shifted)
   if coverage>.999:
    for i in ids:points[i].z+=bias
    group['pivot'].z+=bias;group['guide_vertical_alignment']=bias
kd=KDTree(len(points))
for i,p in enumerate(points):kd.insert(p,i)
kd.balance()
by_marker={g['marker']:g for g in groups+fixed}; bounds=[(min(p[k]for p in points)-.001,max(p[k]for p in points)+.001) for k in range(3)]
before={};changed=[];face_sets=collections.defaultdict(list);moved_counts=collections.Counter();matched_counts=collections.Counter()
tagged_by_mesh={};unrelated_before={}
def unrelated_payload(m,tagged):
 h=hashlib.sha256()
 ids=[v.index for v in m.vertices if v.index not in tagged]
 h.update(np.asarray([tuple(m.vertices[i].co)for i in ids],np.float32).tobytes())
 for p in m.polygons:
  if any(i in tagged for i in p.vertices):continue
  h.update(np.asarray(p.vertices,np.int32).tobytes());h.update(bytes([p.use_smooth]))
  for layer in m.uv_layers:h.update(np.asarray([tuple(layer.data[i].uv)for i in p.loop_indices],np.float32).tobytes())
  for layer in m.color_attributes:h.update(np.asarray([tuple(layer.data[i].color)for i in p.loop_indices],np.float32).tobytes())
 return h.hexdigest()
def payload(o):
 m=o.data;h=hashlib.sha256()
 for data,prop,width,dtype in [(m.vertices,'co',3,np.float32),(m.loops,'vertex_index',1,np.int32),(m.polygons,'use_smooth',1,np.bool_)]:
  v=np.empty(len(data)*width,dtype);data.foreach_get(prop,v);h.update(v.tobytes())
 for layer in m.uv_layers:
  v=np.empty(len(layer.data)*2,np.float32);layer.data.foreach_get('uv',v);h.update(v.tobytes())
 for layer in m.color_attributes:
  v=np.empty(len(layer.data)*4,np.float32);layer.data.foreach_get('color',v);h.update(v.tobytes())
 return h.hexdigest()
for ob in meshes:before[ob.name]=payload(ob)
for ob in meshes:
 if not any(k in ob.name for k in ('Metal','Architecture','Rocks')):continue
 data=ob.data;world=[ob.matrix_world@v.co for v in data.vertices];tags={}
 for i,p in enumerate(world):
  if any(not lo<=p[k]<=hi for k,(lo,hi) in enumerate(bounds)):continue
  _,near,distance=kd.find(p)
  if distance<.0003:
   tag=point_group[near];tags[i]=tag;matched_counts[tag]+=1
 if not tags:continue
 tagged_by_mesh[ob.name]=set(tags)
 unrelated_before[ob.name]=unrelated_payload(data,set(tags))
 normals=[tuple(n.vector) for n in data.corner_normals]
 remove=[]
 for poly in data.polygons:
  marks={tags.get(i,0) for i in poly.vertices}
  assert len(marks)==1,('Mixed boundary face',LEVEL,ob.name,poly.index,marks)
  tag=next(iter(marks))
  if not tag:continue
  group=by_marker[tag]
  if group.get('delta') is not None:continue
  face_sets[tag].append((ob,poly.index));remove.append(poly.index)
 # Keep all unrelated vertices, corners, material bindings and custom normals.
 inverse=ob.matrix_world.inverted()
 for i,tag in tags.items():
  group=by_marker[tag]
  if group.get('delta') is not None:
   data.vertices[i].co=inverse@(world[i]+group['delta']);moved_counts[tag]+=1
 data.update();data.normals_split_custom_set(normals)
 changed.append(dict(mesh=ob.name,matched_vertices=len(tags),extracted_faces=len(remove)))
 ob['_rooftop_remove_faces']=remove
 print('MATCH',LEVEL,ob.name,len(tags),len(remove),flush=True)
for tag,group in by_marker.items():
 if not matched_counts[tag]:
  all_points=[o.matrix_world@v.co for o in meshes if any(k in o.name for k in ('Metal','Architecture','Rocks')) for v in o.data.vertices]
  tree=KDTree(len(all_points))
  for i,q in enumerate(all_points):tree.insert(q,i)
  tree.balance()
  proto=[p for p,t in zip(points,point_group)if t==tag]
  near=[(tuple(p),tuple(tree.find(p)[0]-p),tree.find(p)[2])for p in proto[::max(1,len(proto)//5)]]
  print('UNMATCHED',tag,group.get('name','dish'),tuple(group['pivot']),near,flush=True)
 assert matched_counts[tag]>0,('Authored rooftop was not found',LEVEL,group.get('name',group['kind']),guide_counts[tag-1])
 if group.get('delta') is None:assert face_sets[tag],('No complete rotor faces',tag)
 print('GROUP',tag,group.get('name','dish remount'),matched_counts[tag],guide_counts[tag-1],flush=True)

new_objects=[];entries=[]
for group in groups:
 vertices=[];faces=[];colors=[];uvs=[];normals=[];soft=[];materials=[];sources=[];material_indices=[]
 pivot=group['pivot'];remaps={}
 for ob,index in face_sets[group['marker']]:
  m=ob.data;poly=m.polygons[index]
  material=m.materials[poly.material_index]
  if material not in materials:
   materials.append(material)
   candidates=[(o,slot)for o in meshes for slot,mat in enumerate(o.data.materials)if mat==material and len(o.data.polygons)>len(o.get('_rooftop_remove_faces',[]))]
   material_source,slot=min(candidates,key=lambda x:(x[0].name in {r['mesh']for r in changed},x[0].name))
   sources.append((material_source.name,slot))
  material_indices.append(materials.index(material))
  remap=remaps.setdefault(ob.name,{})
  face=[]
  for loop,vid in zip(poly.loop_indices,poly.vertices):
   if vid not in remap:
    remap[vid]=len(vertices);vertices.append(tuple((ob.matrix_world@m.vertices[vid].co)-pivot))
   face.append(remap[vid])
   color=m.color_attributes.active_color
   colors.append(tuple(color.data[loop].color) if color else(1,1,1,1))
   uv=m.uv_layers.active;uvs.append(tuple(uv.data[loop].uv)if uv else(0,0))
   normals.append(tuple(m.corner_normals[loop].vector))
  faces.append(tuple(face));soft.append(poly.use_smooth)
 m=bpy.data.meshes.new(group['name']);m.from_pydata(vertices,[],faces);m.update()
 for mat in materials:m.materials.append(mat)
 for p,idx,smooth in zip(m.polygons,material_indices,soft):p.material_index=idx;p.use_smooth=smooth
 layer=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');layer.data.foreach_set('color',np.asarray(colors,np.float32).reshape(-1))
 layer=m.uv_layers.new(name='UVMap');layer.data.foreach_set('uv',np.asarray(uvs,np.float32).reshape(-1))
 m.normals_split_custom_set(normals)
 node=bpy.data.objects.new(group['name'],m);bpy.context.scene.collection.objects.link(node);node.parent=root;node.matrix_world=Matrix.Translation(pivot)
 node['radio_rooftop_mechanism']=True;new_objects.append(node)
 entries.append(dict(nodeName=node.name,pivot=xyz(pivot),axis=xyz(group['axis']),degreesPerSecond=group['speed'],materialSources=[dict(nodeName=name,slot=int(slot))for name,slot in sources],kind=group['kind']))
# Rebuild only the original batches that gave rotor faces, leaving every other
# batch byte-identical in authored geometry/UV/color/smoothing.
for ob in meshes:
 remove=set(ob.get('_rooftop_remove_faces',[]))
 if not remove:continue
 old=ob.data;old_name=old.name
 keep=[p for p in old.polygons if p.index not in remove]
 faces=[tuple(p.vertices)for p in keep]
 loops=[i for p in keep for i in p.loop_indices]
 m=bpy.data.meshes.new(old_name+' fixed roof remainder');m.from_pydata([tuple(v.co)for v in old.vertices],[],faces);m.update()
 for mat in old.materials:m.materials.append(mat)
 for poly,original in zip(m.polygons,keep):poly.material_index=original.material_index;poly.use_smooth=original.use_smooth
 for old_layer in old.color_attributes:
  layer=m.color_attributes.new(name=old_layer.name,type='FLOAT_COLOR',domain='CORNER')
  layer.data.foreach_set('color',np.asarray([tuple(old_layer.data[i].color)for i in loops],np.float32).reshape(-1))
 for old_layer in old.uv_layers:
  layer=m.uv_layers.new(name=old_layer.name);layer.data.foreach_set('uv',np.asarray([tuple(old_layer.data[i].uv)for i in loops],np.float32).reshape(-1))
 m.normals_split_custom_set([tuple(old.corner_normals[i].vector)for i in loops])
 ob.data=m;bpy.data.meshes.remove(old);m.name=old_name
changed_names={r['mesh']for r in changed}
for row in changed:
 ob=bpy.data.objects[row['mesh']]
 row['unmatched_geometry_attributes_exact']=unrelated_payload(ob.data,tagged_by_mesh[ob.name])==unrelated_before[ob.name]
 assert row['unmatched_geometry_attributes_exact'],('Unrelated batch payload changed',ob.name)
untouched={name:digest for name,digest in before.items()if name not in changed_names}
assert all(payload(bpy.data.objects[name])==digest for name,digest in untouched.items())
for ob in list(bpy.context.scene.objects):
 if ob not in base and ob not in new_objects:bpy.data.objects.remove(ob,do_unlink=True)
for ob in meshes:
 if '_rooftop_remove_faces'in ob:del ob['_rooftop_remove_faces']
empty_remainders=[]
for ob in list(bpy.context.scene.objects):
 if ob.type!='MESH' or len(ob.data.polygons):continue
 # A fully extracted batch must not export an invalid zero-position MeshFilter.
 # Keep its semantic hierarchy name as a harmless empty transform instead.
 name=ob.name;parent=ob.parent;matrix=ob.matrix_world.copy()
 placeholder=bpy.data.objects.new(name+'_fixed_transform',None);bpy.context.scene.collection.objects.link(placeholder)
 placeholder.parent=parent;placeholder.matrix_world=matrix
 bpy.data.objects.remove(ob,do_unlink=True);placeholder.name=name
 empty_remainders.append(name)
manifest=dict(schema=1,island=label,mechanisms=entries,dishes=[dict(pivot=xyz(g['pivot']+g['delta']),scale=g['size'])for g in fixed])
(OUT/'RooftopMotion.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(label.lower()+'_rooftops.blend')))
bpy.ops.object.select_all(action='DESELECT')
for ob in bpy.context.scene.objects:
 if ob.type in('MESH','EMPTY'):ob.select_set(True)
target=OUT/pathlib.Path(fbx_rel).name
bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
report=dict(level=LEVEL,input_fbx=str(input_fbx),input_sha256=input_sha,output_sha256=sha(target),source_blend=str(source),rotors=len(entries),rotor_triangles=sum(len(p.vertices)-2 for o in new_objects for p in o.data.polygons),changed_batches=changed,untouched_meshes=len(untouched),untouched_payloads_exact=True,remounted_dishes=len(fixed),remount_delta_world=[list(g['delta'])for g in fixed],empty_remainders_to_transforms=empty_remainders,mechanisms=entries)
(OUT/'authoring-proof.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('ROOFTOPS_READY',json.dumps({k:v for k,v in report.items()if k not in('changed_batches','mechanisms')}),flush=True)

