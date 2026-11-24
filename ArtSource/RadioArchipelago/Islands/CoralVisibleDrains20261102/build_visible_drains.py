"""Bounded Coral drain correction. Other islands and Assets are read-only."""
import bpy,pathlib,json,math,hashlib,array,struct,types,sys,collections
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;ISLANDS=HERE.parent
BASE=ISLANDS/'UnifiedLakeWaterfalls20261102/STAGING/Coral'
EXPECTED='f64e9bd7b0e51ff365169112c89ab5d04cc54a1e341815abaf7573ea8c81170b'
OUT=HERE/'STAGING';OUT.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
assert sha(BASE/'coral_oasis_island.fbx')==EXPECTED
# Reuse the reviewed union/topology implementation without its executable jobs.
helper=types.ModuleType('lake_geometry');helper.__file__=str(ISLANDS/'UnifiedLakeWaterfalls20261102/build_unified_lakes.py')
code=pathlib.Path(helper.__file__).read_text().split('requested=sys.argv')[0]
code=code.replace("if label=='Amber':threshold=", "if 'join_threshold'in info:threshold=info['join_threshold']\n elif label=='Amber':threshold=")
exec(compile(code,helper.__file__,'exec'),helper.__dict__);helper.HERE=HERE
bpy.ops.wm.open_mainfile(filepath=str(BASE/'coral_unified_lake_waterfalls.blend'));bpy.context.view_layer.update()
old_meta=json.loads((BASE/'handoff.json').read_text());water_names={s['node']for s in old_meta['sheets']}
before={ob.name:helper.signature(ob)for ob in bpy.context.scene.objects if ob.name not in water_names}

SPECS=[dict(id='high_spring',node='RA_Coral_Waterfall_+01_-01_01',x=67,head_y=-7,lip_y=3.6,forward=1,width=2.3,height=6.72599983215332,radius=2.2,bottom=-34,join_threshold=7.2,
            carve_y=[-2.2,4.0],carve_half_width=3.15),
       dict(id='low_outlet',node='RA_Coral_Waterfall_+01_-03_01',x=44,head_y=-67.4,lip_y=-82,forward=-1,width=3.4,height=.4560000002384186,radius=2.3,bottom=-31,join_threshold=7.2,
            carve_y=[-83,-73],carve_half_width=3.7)]
def smooth(t):t=min(1,max(0,t));return t*t*(3-2*t)
def delta_for(p,ground):
 result=0
 for spec in SPECS:
  lo,hi=spec['carve_y'];d=abs(p.x-spec['x']);half=spec['width']*.5
  if not lo<p.y<hi or d>=spec['carve_half_width']:continue
  lateral=1-smooth((d-half)/(spec['carve_half_width']-half))
  longitudinal=smooth((p.y-lo)/.6)*smooth((hi-p.y)/.6)
  target=spec['height']-.35+.08*min(1,d/max(half,.1))**2
  result=min(result,min(0,target-ground)*lateral*longitudinal)
 # Close the obsolete south mouth of the high spring at its original ellipse
 # boundary. Otherwise moving the outlet north would leave a dry, low channel
 # beyond the old pool edge and make the unchanged lake appear suspended.
 if -15.4<p.y<-12.5 and abs(p.x-68)<2.25:
  r2=((p.x-67)/7.8)**2+((p.y+7)/6)**2
  weight=smooth((r2-.97)/.03)*(1-smooth((abs(p.x-68)-1.6)/.65))*smooth((p.y+15.4)/.5)
  result=max(result,max(0,6.906-ground)*weight)
 return result

terrain=[ob for ob in bpy.context.scene.objects if ob.type=='MESH'and any(m and'_Terrain'in m.name for m in ob.data.materials)]
verts=[];faces=[]
for ob in terrain:
 start=len(verts);verts.extend(ob.matrix_world@v.co for v in ob.data.vertices);faces.extend(tuple(start+i for i in p.vertices)for p in ob.data.polygons)
ground_bvh=BVHTree.FromPolygons(verts,faces,all_triangles=False)
def ground_at(p):
 hit,n,f,d=ground_bvh.ray_cast(Vector((p.x,p.y,500)),Vector((0,0,-1)),1000)
 if hit is not None:return hit.z
 # For matching outer cliff rim coordinates just outside a cap edge, use only
 # the nearest actual top triangle's height, not a nominal terrain function.
 nearest,n,f,d=ground_bvh.find_nearest(Vector((p.x,p.y,p.z)))
 return nearest.z if nearest is not None and d<3 else None

sculpt=[];far_payloads={}
for ob in list(bpy.context.scene.objects):
 if ob.type!='MESH':continue
 cliff=any(m and'_Cliffs'in m.name for m in ob.data.materials)
 top=ob in terrain
 if not cliff and not top:continue
 m=ob.data;old_positions=[v.co.copy()for v in m.vertices];old_normals=[n.vector.copy()for n in m.corner_normals];changed=[]
 for vertex in m.vertices:
  p=ob.matrix_world@vertex.co
  inside_new=any(s['carve_y'][0]<p.y<s['carve_y'][1]and abs(p.x-s['x'])<s['carve_half_width']for s in SPECS)
  inside_old=-15.4<p.y<-12.5 and abs(p.x-68)<2.25
  if not inside_new and not inside_old:continue
  ground=ground_at(p)
  if ground is None:continue
  delta=delta_for(p,ground)
  if cliff:delta*=smooth((p.z-(ground-3))/3)
  if abs(delta)<1e-6:continue
  p.z+=delta;vertex.co=ob.matrix_world.inverted()@p;changed.append(vertex.index)
 if not changed:continue
 changed_set=set(changed);m.update()
 # Preserve every unchanged corner exactly. Only changed vertex corners receive
 # new geometric normals, keeping far scenery's UV/colour/normal payload intact.
 sums=[Vector((0,0,0))for v in m.vertices]
 for poly in m.polygons:
  ids=list(poly.vertices);a,b,c=[m.vertices[i].co for i in ids[:3]];cross=(b-a).cross(c-a)
  for i in ids:sums[i]+=cross
 normals=[]
 for loop in m.loops:
  if loop.vertex_index not in changed_set:normals.append(tuple(old_normals[loop.index]));continue
  normal=sums[loop.vertex_index].normalized()if top else m.polygons[next(p.index for p in m.polygons if loop.index in p.loop_indices)].normal
  normals.append(tuple(normal if normal.length>.5 else old_normals[loop.index]))
 m.normals_split_custom_set(normals)
 far_positions_exact=all(tuple(v.co)==tuple(old_positions[v.index])for v in m.vertices if v.index not in changed_set);assert far_positions_exact
 deltas=[(m.vertices[i].co-old_positions[i]).length for i in changed]
 sculpt.append(dict(node=ob.name,changed_vertices=len(changed),total_vertices=len(m.vertices),maximum_displacement=max(deltas),
  changed_indices=changed,old_positions=[list(old_positions[i])for i in changed],new_positions=[list(m.vertices[i].co)for i in changed],
  far_positions_exact=far_positions_exact,vertex_topology_unchanged=True,uv_colors_materials_unchanged=True))

# Extract only original pool/outlet polygons; the rejected old high south reach
# is intentionally absent. Library objects are never linked to this scene.
original=ISLANDS/'RooftopMechanisms20261101/STAGING/Coral/coral_rooftops.blend'
with bpy.data.libraries.load(str(original),link=False)as(source,dest):dest.objects=[name for name in source.objects if name.startswith('RA_Coral_Water_')]
pool_faces={s['id']:[]for s in SPECS}
for ob in dest.objects:
 if ob is None:continue
 for poly in ob.data.polygons:
  ps=[ob.matrix_world@ob.data.vertices[i].co for i in poly.vertices];height=sum(p.z for p in ps)/len(ps)
  pool_faces['high_spring'if height>3 else'low_outlet'].append(ps)
for ob in dest.objects:
 if ob:bpy.data.objects.remove(ob,do_unlink=True)

def new_ribbon(spec):
 old=bpy.data.objects[spec['node']];parent=old.parent;material=old.data.materials[0];properties={k:old[k]for k in old.keys()};name=old.name
 points=[];x=spec['x'];z=spec['height'];fwd=spec['forward'];radius=spec['radius']
 length=abs(spec['lip_y']-spec['head_y']);steps=math.ceil(length/.25)
 for i in range(steps):points.append(Vector((x,spec['head_y']+fwd*length*i/steps,z)))
 for i in range(33):
  angle=i/32*math.pi/2;points.append(Vector((x,spec['lip_y']+fwd*radius*math.sin(angle),z-radius*(1-math.cos(angle)))))
 end=points[-1];drop=end.z-spec['bottom'];steps=math.ceil(drop/.3)
 for i in range(1,steps+1):
  t=i/steps;points.append(Vector((x+.12*math.sin(t*7),end.y+fwd*.4*smooth(t*4),end.z-drop*t)))
 travel=[0]
 for a,b in zip(points,points[1:]):travel.append(travel[-1]+(b-a).length)
 vs=[];uvs=[];colors=[];fs=[];cols=32
 for p,v in zip(points,travel):
  remain=travel[-1]-v;alpha=smooth(remain/3)
  for j in range(cols+1):vs.append(tuple(p+Vector(((j/cols-.5)*spec['width'],0,0))));uvs.append((j/cols,v));colors.append((.15,.55,.57,alpha))
 for i in range(len(points)-1):
  for j in range(cols):a=i*(cols+1)+j;f=(a,a+1,a+cols+2,a+cols+1);fs.append(f if fwd>0 else tuple(reversed(f)))
 mesh=bpy.data.meshes.new(name+'_ExteriorDrainRibbon');mesh.from_pydata(vs,[],fs);mesh.update();mesh.materials.append(material)
 uv=mesh.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',[v for l in mesh.loops for v in uvs[l.vertex_index]])
 color=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');color.data.foreach_set('color',[v for l in mesh.loops for v in colors[l.vertex_index]])
 for p in mesh.polygons:p.use_smooth=True
 bpy.data.objects.remove(old,do_unlink=True);ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob);ob.parent=parent;ob.matrix_world=Matrix.Identity(4)
 for k,v in properties.items():ob[k]=v
 return ob

records=[]
for spec in SPECS:
 ribbon=new_ribbon(spec);info=dict(id=spec['id'],join_threshold=spec['join_threshold'])
 record=helper.build_one(ribbon,info,pool_faces[spec['id']],'Coral')
 record['body_relocated_for_exterior_visibility']=True;record['body_vertex_positions_and_uv_preserved']=False
 records.append(record)
bpy.context.view_layer.update();changed_names={r['node']for r in sculpt}
after={ob.name:helper.signature(ob)for ob in bpy.context.scene.objects if ob.name not in water_names and ob.name not in changed_names}
assert after=={n:s for n,s in before.items()if n not in changed_names},'Nonwater outside sculpted nodes changed'
bpy.ops.object.select_all(action='DESELECT')
for ob in bpy.context.scene.objects:
 if ob.type in('MESH','EMPTY'):ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'coral_oasis_island.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,
 apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'coral_visible_drains.blend'))
(OUT/'RooftopMotion.json').write_bytes((BASE/'RooftopMotion.json').read_bytes())
result=dict(level='Coral',input_model=str(BASE/'coral_oasis_island.fbx'),input_sha256=EXPECTED,output_model=str(OUT/'coral_oasis_island.fbx'),output_sha256=sha(OUT/'coral_oasis_island.fbx'),
  consumed_pool_nodes=old_meta['consumed_pool_nodes'],affected_nodes=sorted(water_names|changed_names),sculpted_nodes=sorted(changed_names),sculpt=sculpt,corridors=SPECS,
  obsolete_high_south_bank_closure=dict(x_bounds=[65.75,70.25],y_bounds=[-15.4,-12.5],outside_original_pool_r2=.97,target_bank_height=6.906),
 untouched_objects=len(after),untouched_before={n:s for n,s in before.items()if n not in changed_names},untouched_after=after,sheets=records)
(OUT/'handoff.json').write_text(json.dumps(result,indent=2));assert sha(BASE/'coral_oasis_island.fbx')==EXPECTED
print('VISIBLE_DRAINS_READY',json.dumps({k:v for k,v in result.items()if k not in('sculpt','untouched_before','untouched_after')}),flush=True)
