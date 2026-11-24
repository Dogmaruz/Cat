"""Read-only canonical island -> isolated elevation study, using original optimized LOD.

Never opens or writes the user's interactive Blender session. Canonical .blend,
FBX, scene, prefab and route XZ data are fingerprinted and remain untouched.
"""
import bpy,bmesh,math,json,pathlib,sys,hashlib,shutil,random,collections,time,bisect
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

HERE=pathlib.Path(__file__).resolve().parent
REPO=HERE.parents[3]
V4=HERE.parent/'V4'
SRC=V4/'Output/01_emerald_ravine'
OLD=REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/Emerald'
OUT=REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/EmeraldRelief'
PROOF=HERE/'Proof'
sys.path.insert(0,str(V4/'UnityExport'))
import export_emerald as ex
import export_cobbles as cob

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
manifest=json.loads((SRC/'manifest.json').read_text(encoding='utf8'))
road=cob.ClosedRoad(manifest['road']['points'])
route_kd=KDTree(len(road.points))
for i,p in enumerate(road.points):route_kd.insert((p.x,p.y,0),i)
route_kd.balance()

def clamp(t):return max(0,min(1,t))
def smooth(t):t=clamp(t);return t*t*(3-2*t)
def hill(x,y,cx,cy,height,inner,outer):
 return height*(1-smooth((math.hypot(x-cx,y-cy)-inner)/(outer-inner)))
def ground(x,y):return hill(x,y,64,22,8,20,56)+hill(x,y,-45,-18,2.8,8,38)
def nearest_s(x,y):
 _,i,_=route_kd.find((x,y,0));best=(1e30,0)
 for a in ((i-1)%len(road.points),i):
  b=(a+1)%len(road.points);p=road.points[a];v=road.points[b]-p
  t=clamp(((x-p.x)*v.x+(y-p.y)*v.y)/(v.x*v.x+v.y*v.y));q=p+v*t
  d=(q.x-x)**2+(q.y-y)**2
  if d<best[0]:best=(d,road.arc[a]+t*(road.arc[a+1]-road.arc[a]))
 return best[1]
def bridge(s):
 t=(s-81.5)/18
 return 2*math.sin(math.pi*t) if 0<t<1 else 0
def road_h(s):
 p,_=road.sample(s);return ground(p.x,p.y)+bridge(s)
def terrain_h(x,y):
 s=nearest_s(x,y);p,_=road.sample(s);distance=math.hypot(p.x-x,p.y-y)
 weight=1-smooth((distance-3.3)/2.7)
 h=ground(x,y)*(1-weight)+ground(p.x,p.y)*weight
 if 238<=s<=272:h-=.24*weight
 return h
def point(s,cross=0,z=None):
 p,n=road.sample(s);q=p+n*cross;q.z=road_h(s) if z is None else z;return q

def add_material(name,bucket):
 mat=bpy.data.materials.new(name);mat.use_nodes=True;mat.diffuse_color=(1,1,1,1)
 bs=ex.node_bsdf(mat);bs.inputs['Base Color'].default_value=(1,1,1,1);bs.inputs['Roughness'].default_value=.88
 if bucket=='Metal':bs.inputs['Metallic'].default_value=.65;bs.inputs['Roughness'].default_value=.36
 attr=mat.node_tree.nodes.new('ShaderNodeVertexColor');attr.layer_name='Color';mat.node_tree.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
 mat['unity_material_bucket']=bucket;return mat

def add_mesh(name,verts,faces,color,category,col,materials,smooth=False):
 m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();m.materials.append(materials[category])
 rgba=tuple(cob.linear(c) for c in color[:3])+(1,)
 attr=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');attr.data.foreach_set('color',list(rgba)*len(m.loops))
 for p in m.polygons:p.use_smooth=smooth
 o=bpy.data.objects.new(name,m);col.objects.link(o);o['unity_material_bucket']=category
 return o

def block(name,a,b,halfwidth,bottom,top,color,category,col,materials):
 # Rectangular section with a top following the endpoints; four UV-free solid sides.
 tangent=(b-a).normalized();n=Vector((-tangent.y,tangent.x,0));n.normalize()
 verts=[]
 for p,z in ((a,bottom[0]),(b,bottom[1]),(a,top[0]),(b,top[1])):
  for sign in (-1,1):q=p+n*(halfwidth*sign);verts.append((q.x,q.y,z))
 faces=[(0,2,3,1),(4,5,7,6),(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3)]
 return add_mesh(name,verts,faces,color,category,col,materials)

def beam(name,a,b,width,color,category,col,materials):
 axis=b-a;length=axis.length;center=(a+b)*.5
 verts=[Vector((x*width*.5,y*width*.5,z*length*.5)) for z in (-1,1) for y in (-1,1) for x in (-1,1)]
 rot=axis.to_track_quat('Z','Y');verts=[tuple(rot@v+center) for v in verts]
 return add_mesh(name,verts,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],color,category,col,materials)

def export_fbx(path,objects):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,
  apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=True,
  mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')

def build():
 start=time.monotonic();OUT.mkdir(parents=True,exist_ok=True);PROOF.mkdir(exist_ok=True)
 frozen={str(p):sha(p) for p in (SRC/'island.blend',OLD/'emerald_island.fbx',OLD/'emerald_cobbles.fbx',OLD/'emerald_route.json')}
 bpy.ops.wm.open_mainfile(filepath=str(SRC/'island.blend'))
 originals=[o for o in bpy.context.scene.objects if o.type=='MESH']
 col=bpy.data.collections.new('Emerald Relief Pilot derivative');bpy.context.scene.collection.children.link(col)
 root=bpy.data.objects.new('EmeraldReliefIsland',None);col.objects.link(root)
 categories=list(ex.BUCKETS)+['BridgeDeck','StepTreads']
 materials={k:add_material('RA_Emerald_'+k,k) for k in categories}
 cache={};optimization=collections.Counter();colors={};batches={};serial=collections.Counter();output=[];counts=collections.Counter()
 rigid_count=0;rigid_max_error=0.;skipped=collections.Counter()
 for number,obj in enumerate(sorted(originals,key=lambda o:o.name)):
  mesh=ex.simplified_mesh(obj,cache,optimization);matrix=obj.matrix_world.copy();base=matrix.translation
  role=obj.get('v3_role','');rigid=(abs(base.x)+abs(base.y)>.001 and role!='road')
  if rigid:rigid_count+=1
  shift=ground(base.x,base.y) if rigid else 0
  world=[]
  for v in mesh.vertices:
   w=matrix@v.co
   if role=='road':w.z+=road_h(nearest_s(w.x,w.y))
   elif role=='water':pass
   elif rigid:w.z+=shift
   else:w.z+=terrain_h(w.x,w.y) if 'CONNECTED_FLOATING_TERRAIN' in obj.name else ground(w.x,w.y)
   world.append(w)
  uv=mesh.uv_layers.active.data if mesh.uv_layers.active else None;bank=mesh.attributes.get('river_bank_mix');object_remaps={}
  for poly in mesh.polygons:
   material=mesh.materials[poly.material_index] if poly.material_index<len(mesh.materials) else None
   group=ex.bucket(obj,material)
   center=matrix@poly.center;s=nearest_s(center.x,center.y) if role=='road' else -1
   if group=='Bridge' or (role=='road' and 238<=s<=272):skipped[group]+=len(poly.vertices)-2;continue
   if material and material.name not in colors:colors[material.name]=ex.color_info(material)
   rgba,slate=colors[material.name] if material else ((.5,.5,.5,1),None)
   key=(group,math.floor(center.x/ex.CELL),math.floor(center.y/ex.CELL))
   if key not in batches:
    serial[key]+=1;batches[key]=ex.Batch(f'RA_{group}_{key[1]:+03d}_{key[2]:+03d}_{serial[key]:02d}',group,col,materials[group])
   batch=batches[key]
   if len(batch.v)+len(poly.vertices)>ex.MAX_BATCH_VERTICES:
    o=batch.flush();o.parent=root;output.append(o);serial[key]+=1
    batches[key]=batch=ex.Batch(f'RA_{group}_{key[1]:+03d}_{key[2]:+03d}_{serial[key]:02d}',group,col,materials[group]);object_remaps.pop(key,None)
   remap=object_remaps.setdefault(key,{});face=[]
   for li,vid in zip(poly.loop_indices,poly.vertices):
    if vid not in remap:remap[vid]=len(batch.v);batch.v.append(tuple(world[vid]))
    face.append(remap[vid]);weight=bank.data[vid].value if bank and bank.domain=='POINT' else 0
    color=tuple(rgba[i]*(1-weight)+slate[i]*weight for i in range(3))+(1,) if slate else tuple(rgba[:3])+(1,)
    batch.colors.append(color);batch.bank.append(weight);batch.uv.append(tuple(uv[li].uv) if uv else (world[vid].x,world[vid].y))
   batch.f.append(tuple(face));batch.soft.append(poly.use_smooth);counts[group]+=len(poly.vertices)-2
  if number%400==0:print('RELIEF_SOURCE',number,len(originals),sum(counts.values()),flush=True)
 for batch in batches.values():
  o=batch.flush()
  if o:o.parent=root;output.append(o)
 for obj in list(bpy.context.scene.objects):
  if obj not in set(col.objects):bpy.data.objects.remove(obj,do_unlink=True)
 print('RELIEF_BASE_COMPLETE',len(output),flush=True)
 rng=random.Random(928472)
 # Stone barrel arch: structural 65 cm voussoirs, wide carriageway and capped parapets.
 bridge_parts=[]
 for i in range(36):
  a=81.5+i*.5;b=a+.5;ha=road_h(a);hb=road_h(b)
  bridge_parts.append(block(f'Relief_Bridge_barrel_voussoir_{i:02d}',point(a),point(b),3.17,(ha-.80,hb-.80),(ha-.08,hb-.08),(.39+rng.uniform(-.025,.025),.43,.43),'BridgeDeck',col,materials))
  for side in (-1,1):
   pa=point(a,side*3.06);pb=point(b,side*3.06)
   bridge_parts.append(block(f'Relief_Bridge_parapet_{side}_{i:02d}',pa,pb,.21,(ha-.18,hb-.18),(ha+.57,hb+.57),(.46+rng.uniform(-.035,.035),.48,.46),'BridgeDeck',col,materials))
   bridge_parts.append(block(f'Relief_Bridge_coping_{side}_{i:02d}',point(a+.015,side*3.06),point(b-.015,side*3.06),.28,(ha+.57,hb+.57),(ha+.73,hb+.73),(.55,.56,.52),'BridgeDeck',col,materials))
 for station in (81.1,99.9):
  for side in(-1,1):
   bridge_parts.append(block('Relief_Bridge_battered_abutment',point(station-.8,side*2.7),point(station+.8,side*2.7),.70,(-1.5,-1.5),(.35,.35),(.38,.43,.42),'BridgeDeck',col,materials))
 # Broad timber staircase: discrete solid treads, cross-board seams, outer stringers and posts.
 stair_parts=[];N=60;step=(272-238)/N
 for i in range(N):
  a=238+i*step;b=a+step;h=road_h((a+b)*.5)+.06
  for side in range(6):
   center=(side-2.5)*.98;tone=rng.uniform(.94,1.06)
   stair_parts.append(block(f'Relief_Stair_tread_{i:02d}_{side}',point(a-.015,center),point(b-.015,center),.48,(h-.27,h-.27),(h,h),tuple(c*tone for c in(.46,.30,.16)),'StepTreads',col,materials))
  for side in(-1,1):
   ha=road_h(a)-.23;hb=road_h(b)-.23
   stair_parts.append(block(f'Relief_Stair_stringer_{i:02d}_{side}',point(a,side*3.13),point(b,side*3.13),.14,(ha-.34,hb-.34),(ha,hb),(.27,.18,.10),'Wood',col,materials))
 for i in range(13):
  s=238+34*i/12;h=road_h(s)
  for side in(-1,1):
   p=point(s,side*3.16);bottom=p.copy();bottom.z=h-.5;top=p.copy();top.z=h+1
   stair_parts.append(beam('Relief_Stair_guard_post',bottom,top,.20,(.36,.23,.13),'Wood',col,materials))
   a=bottom.copy();a.z=h+.7;b=a.copy();b.z+=.085
   stair_parts.append(beam('Relief_Stair_copper_post_collar',a,b,.225,(.60,.42,.23),'Metal',col,materials))
   if i<12:
    q=point(s+34/12,side*3.16);q.z=road_h(s+34/12)+.85;p.z=h+.85
    stair_parts.append(beam('Relief_Stair_guard_rail',p,q,.14,(.36,.24,.13),'Wood',col,materials))
 output+=bridge_parts+stair_parts
 for o in bridge_parts+stair_parts:o.parent=root
 for name,pos in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
  o=bpy.data.objects.new(name,None);col.objects.link(o);o.location=pos;o.parent=root
 # Triangulate and correct tiny/duplicate source polygons without any decimation.
 clean=collections.Counter()
 for o in output:
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.faces.ensure_lookup_table()
  bad=[f for f in bm.faces if f.calc_area()<1e-10]
  if bad:bmesh.ops.delete(bm,geom=bad,context='FACES');clean['tiny_faces_removed']+=len(bad)
  bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
 export_fbx(OUT/'emerald_relief_island.fbx',list(col.objects))
 print('RELIEF_ISLAND_EXPORTED',flush=True)
 # Import only the existing optimized native cobbles; elevate each solid without altering X/Y.
 island_names=set(o.name for o in bpy.context.scene.objects)
 for name in ('RA_Origin','RA_X10','RA_North10'):
  bpy.data.objects[name].name='IslandOnly_'+name
 island_names=set(o.name for o in bpy.context.scene.objects)
 bpy.ops.import_scene.fbx(filepath=str(OLD/'emerald_cobbles.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
 cob_objects=[o for o in bpy.context.scene.objects if o.name not in island_names];cob_meshes=[o for o in cob_objects if o.type=='MESH']
 removed=0
 for o in cob_meshes:
  m=o.data;matrix=o.matrix_world.copy();inv=matrix.inverted();verts=[];inside=[]
  for v in m.vertices:
   w=matrix@v.co;s=nearest_s(w.x,w.y);inside.append(238.15<=s<=271.85);w.z+=road_h(s);v.co=inv@w
  bm=bmesh.new();bm.from_mesh(m);bm.verts.ensure_lookup_table()
  bad=[f for f in bm.faces if all(inside[v.index] for v in f.verts)]
  removed+=len(bad)
  bmesh.ops.delete(bm,geom=bad,context='FACES');bm.to_mesh(m);bm.free();m.update()
  o['unity_material_bucket']='Road'
 for o in cob_objects:
  if o.type=='MESH':
   for i in range(len(o.data.materials)):o.data.materials[i]=materials['Road']
 export_fbx(OUT/'emerald_relief_cobbles.fbx',cob_objects)
 for name in ('RA_Origin','RA_X10','RA_North10'):
  for o in cob_objects:
   if o.name==name:o.name='CobblesOnly_'+name
  bpy.data.objects['IslandOnly_'+name].name=name
 # For these dense samples, normal interpolation stays below 2 cm geometric error.
 stations=sorted(set([round(float(s),6) for s in np.arange(0,road.length,.5)]+[round(float(s),6) for start,end in ((80.5,83),(98,100.5)) for s in np.arange(start,end+.01,.1)]))
 keys=[dict(distance=s,height=road_h(s)) for s in stations]
 keys.append(dict(distance=road.length,height=keys[0]['height']))
 profile=dict(length=road.length,closed=True,keys=keys)
 (OUT/'ground_elevation.json').write_text(json.dumps(profile,indent=2),encoding='utf8')
 shutil.copyfile(OLD/'emerald_route.json',OUT/'emerald_relief_route.json')
 bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'))
 report=dict(schema='radio_archipelago_relief_pilot/1',frozen_inputs=frozen,frozen_inputs_unchanged=all(sha(pathlib.Path(p))==h for p,h in frozen.items()),
  output_files=['emerald_relief_island.fbx','emerald_relief_cobbles.fbx','ground_elevation.json'],coordinate_mapping='Blender(x,y,z) -> aligned Unity(x,z,-y); retain original prefab X reflection',
  east_hill=dict(center=[64,22],height=8,inner_radius=20,outer_radius=56),west_hill=dict(center=[-45,-18],height=2.8,inner_radius=8,outer_radius=38),
  bridge=dict(start=81.5,end=99.5,crown_s=90.5,crown_height=2,width=6.34,barrel_thickness=.72),stairs=dict(start=238,end=272,treads=N,width=5.9),
  road_xz_unchanged=True,route_length=road.length,profile_keys=len(keys),profile_endpoint_height_equal=keys[0]['height']==keys[-1]['height'],
  rigid_objects_shifted_at_base=rigid_count,tree_and_building_stretching=False,source_optimization=dict(optimization),skipped_source_triangles=dict(skipped),cleaning=dict(clean),
  island_triangles=sum(len(o.data.polygons) for o in output),cobble_triangles=sum(len(o.data.polygons) for o in cob_meshes),cobble_stair_triangles_removed=removed,
  material_buckets={k:sum(len(o.data.polygons) for o in output if o.get('unity_material_bucket')==k) for k in categories},
  ground_materials=['RA_Emerald_Terrain','RA_Emerald_Road','RA_Emerald_BridgeDeck','RA_Emerald_StepTreads'],
  max_height=max(k['height'] for k in keys),min_height=min(k['height'] for k in keys),elapsed_seconds=time.monotonic()-start)
 report['total_triangles']=report['island_triangles']+report['cobble_triangles'];assert report['total_triangles']<5500000
 assert report['frozen_inputs_unchanged'];(HERE/'relief_report.json').write_text(json.dumps(report,indent=2))
 print('RELIEF_COMPLETE',json.dumps(report),flush=True)
 render_proofs()

def render_proofs():
 scene=bpy.context.scene
 for m in bpy.data.materials:
  if m.name.startswith('RA_Emerald') and m.use_nodes:
   bs=ex.node_bsdf(m)
   if bs:
    attr=m.node_tree.nodes.new('ShaderNodeVertexColor');attr.layer_name='Color';m.node_tree.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
 scene.world=bpy.data.worlds.new('Relief daylight');scene.world.use_nodes=True
 bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.63,.76,.91,1);bg.inputs['Strength'].default_value=.65
 ld=bpy.data.lights.new('Relief sun','SUN');ld.energy=2.8;ld.angle=.12
 lo=bpy.data.objects.new('Relief sun',ld);scene.collection.objects.link(lo);lo.rotation_euler=(.55,-.4,-.8)
 scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True
 try:
  prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
  for device in prefs.devices:device.use=device.type=='CUDA'
  scene.cycles.device='GPU'
 except:pass
 scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
 scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
 for name,target,offset,scale in [
  ('overview',(4,-2,-5),(180,-230,190),215),
  ('hill_house',(60,23,8),(53,-72,43),83),
  ('wood_stairs',(43,36,7),(-12,22,31),42),
  ('stone_bridge',(13,-6,.8),(-22,-26,15),34),
  ('top',(5,0,0),(0,0,250),205)]:
  camd=bpy.data.cameras.new('Relief '+name);camd.type='ORTHO';camd.ortho_scale=scale
  camera=bpy.data.objects.new(camd.name,camd);scene.collection.objects.link(camera);camera.location=Vector(target)+Vector(offset)
  camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();scene.camera=camera
  scene.render.filepath=str(PROOF/(name+'.png'));bpy.ops.render.render(write_still=True);print('RELIEF_PROOF',name,flush=True)

if __name__=='__main__':
 if '--render-only' in sys.argv:
  bpy.ops.wm.open_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'));render_proofs()
 else:build()
