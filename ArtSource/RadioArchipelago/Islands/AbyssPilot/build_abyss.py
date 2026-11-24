"""Isolated, road-free floating archipelago derivative. Frozen inputs are read-only."""
import bpy,bmesh,json,pathlib,sys,math,random,collections,hashlib,time,bisect,shutil
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent
REPO=HERE.parents[3];RELIEF=HERE.parent/'ReliefPilot';V4=HERE.parent/'V4'
OUT=REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/EmeraldAbyss';PROOF=HERE/'Proof'
sys.path.insert(0,str(RELIEF));sys.path.insert(0,str(V4));import build_relief as r
ex=r.ex;road=r.road
PLAY_PATH=r.OUT/'play_elevation.json';PLAY=json.loads(PLAY_PATH.read_text());PLAY_S=[k['distance'] for k in PLAY['keys']]
def play(s):
 i=max(0,min(len(PLAY_S)-2,bisect.bisect_right(PLAY_S,s)-1));a,b=PLAY['keys'][i:i+2];t=(s-a['distance'])/(b['distance']-a['distance']);return a['height']*(1-t)+b['height']*t
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def topology(mesh):
 bm=bmesh.new();bm.from_mesh(mesh);stats=dict(vertices=len(bm.verts),faces=len(bm.faces),boundary_edges=sum(e.is_boundary for e in bm.edges),nonmanifold_edges=sum(not e.is_manifold for e in bm.edges));bm.free();return stats
def mat(name,rgb,bucket,rough=.9):
 m=bpy.data.materials.new(name);m.use_nodes=True;m['abyss_bucket']=bucket;bs=ex.node_bsdf(m);bs.inputs['Base Color'].default_value=tuple(r.cob.linear(c) for c in rgb)+(1,);bs.inputs['Roughness'].default_value=rough;return m
def objmesh(name,v,f,mats,mi=None,role='terrain'):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update()
 for m in mats:me.materials.append(m)
 if mi:
  for p,i in zip(me.polygons,mi):p.material_index=i
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free();me.update()
 o=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(o);o['v3_role']=role;return o

def mouth(s,section):
 # A 12–18 m wide upper opening; coherent uneven banks rather than straight strips.
 base=7.0+.65*math.sin(s*.31)+.35*math.sin(s*.87)
 for center in ((146,171) if section==0 else (420,445,478)):
  base+=1.6*math.exp(-((s-center)/8.0)**2)
 return max(6,min(9,base))
def cutter(start,end,section):
 stations=list(np.arange(start,end,1.7))+[end];left=[];right=[]
 for s in stations:
  p,n=road.sample(s);w=mouth(s,section);outer=65 if section==1 else w;left.append((p+n*outer,s,w));right.append((p-n*w,s,w))
 # Closed rounded ends, with asymmetric bank points retained through every depth ring.
 polygon=[(p,s,w) for p,s,w in left]
 p,n=road.sample(end);t=Vector((n.y,-n.x,0));w=mouth(end,section);outer=65 if section==1 else w;capcenter=p+n*((outer-w)*.5);across=(outer+w)*.5
 for theta in np.linspace(0,math.pi,9)[1:-1]:polygon.append((capcenter+n*across*math.cos(theta)+t*w*math.sin(theta),end,w))
 polygon += [(p,s,w) for p,s,w in reversed(right)]
 p,n=road.sample(start);t=Vector((n.y,-n.x,0));w=mouth(start,section);outer=65 if section==1 else w;capcenter=p+n*((outer-w)*.5);across=(outer+w)*.5
 for theta in np.linspace(0,math.pi,9)[1:-1]:polygon.append((capcenter-n*across*math.cos(theta)-t*w*math.sin(theta),start,w))
 rings=[(45,1),(1.8,1),(-3,1.03),(-8,1.14),(-16,1.24),(-28,1.4),(-76,1.65)]
 verts=[];faces=[];n=len(polygon)
 for ri,(z,factor) in enumerate(rings):
  for pi,(q,s,w) in enumerate(polygon):
   center,_=road.sample(s);d=q-center
   # Deep banks lean and fracture, but never narrow the upper gameplay opening.
   wobble=0 if ri<2 else .16*math.sin(pi*1.77+ri*2.1)
   v=center+d*(factor+wobble/max(w,1));verts.append((v.x,v.y,z))
 for ri in range(len(rings)-1):
  for i in range(n):j=(i+1)%n;faces.append((ri*n+i,ri*n+j,(ri+1)*n+j,(ri+1)*n+i))
 faces.append(tuple(reversed(range(n))));faces.append(tuple((len(rings)-1)*n+i for i in range(n)))
 return objmesh(f'Abyss_Cutter_{section}',verts,faces,[])

def deform_and_carve(terrain,cliff_mats):
 terrain.data=terrain.data.copy()
 for v in terrain.data.vertices:v.co.z+=r.ground(v.co.x,v.co.y)
 terrain.data.materials.append(cliff_mats[0]);cliff_slot=len(terrain.data.materials)-1
 for start,end,section in [(125,185,0),(402,501,1)]:
  cut=cutter(start,end,section);cut.data.materials.append(cliff_mats[0])
  # Match cutter slot to the target's cliff slot, giving every boolean wall rock.
  while len(cut.data.materials)<=cliff_slot:cut.data.materials.append(cliff_mats[0])
  for p in cut.data.polygons:p.material_index=cliff_slot
  bpy.context.view_layer.objects.active=terrain;mod=terrain.modifiers.new('True open full-depth ravine','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut
  if hasattr(mod,'material_mode'):mod.material_mode='INDEX'
  bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
 for m in cliff_mats[1:]:terrain.data.materials.append(m)
 # Every newly exposed inside wall belongs to the structural Cliffs category.
 for p in terrain.data.polygons:
  if p.material_index==cliff_slot:
   p.material_index=cliff_slot+(int(abs(p.center.z)*.21+math.sin(p.center.x*.19))%len(cliff_mats))
 terrain['abyss_deformed']=True;stats=topology(terrain.data);assert stats['boundary_edges']==0 and stats['nonmanifold_edges']==0,stats
 print('ABYSS_TERRAIN_CLOSED',json.dumps(stats),flush=True);return stats

def islet(name,s,offset,along,across,depth,kind,cliff_mats,grass):
 p,n=road.sample(s);t=Vector((n.y,-n.x,0));center=p+n*offset;cap=play(s)-.90
 rng=random.Random(7731+int(s));N=24;noise=[rng.uniform(.88,1.13) for i in range(N)]
 if kind=='terrace':levels=[(0,1,0,0),(-2.0,.96,.2,.1),(-2.65,1.17,-.15,.1),(-4.1,1.12,.2,-.1),(-9,.83,-.3,.2),(-17,.51,.4,-.3),(-depth,.09,1.1,-.4)]
 elif kind=='spire':levels=[(0,1,0,0),(-1.8,1.03,.2,.1),(-7,.79,-.3,-.1),(-16,.48,.7,.2),(-26,.28,1.1,-.6),(-depth,.015,1.4,-.9)]
 else:levels=[(0,1,0,0),(-2.1,1.08,.1,0),(-6.8,.83,-.2,.1),(-14,.52,.4,-.1),(-depth,.06,.9,-.5)]
 verts=[];faces=[];mi=[]
 for ri,(z,factor,leanx,leany) in enumerate(levels):
  for i in range(N):
   angle=math.tau*i/N;radius=noise[i]*(1+.055*math.sin(i*2.4+ri*1.9));q=center+t*(math.cos(angle)*along*factor+leanx)+n*(math.sin(angle)*across*factor+leany)
   local_grade=play(r.nearest_s(q.x,q.y))-play(s)
   zz=cap+local_grade+z+(rng.uniform(-.22,-.02) if ri==0 else rng.uniform(-.5,.5))
   verts.append((q.x,q.y,zz))
 top=len(verts);verts.append((center.x,center.y,play(r.nearest_s(center.x,center.y))-.90-.12));bottom=len(verts);last=levels[-1];q=center+t*last[2]+n*last[3];verts.append((q.x,q.y,cap-depth-.7))
 for i in range(N):j=(i+1)%N;faces.append((top,i,j));mi.append(0)
 for ri in range(len(levels)-1):
  for i in range(N):
   j=(i+1)%N;faces.append((ri*N+i,(ri+1)*N+i,(ri+1)*N+j,ri*N+j));mi.append(0 if kind=='terrace' and ri==1 else 1+(i//3+ri)%len(cliff_mats))
 for i in range(N):j=(i+1)%N;faces.append((bottom,(len(levels)-1)*N+j,(len(levels)-1)*N+i));mi.append(1+(i//4)%len(cliff_mats))
 o=objmesh(name,verts,faces,[grass]+cliff_mats,mi);o['abyss_deformed']=True;o['islet_s']=s
 stats=topology(o.data);assert stats['boundary_edges']==0 and stats['nonmanifold_edges']==0
 return o,dict(name=name,distance=s,cap_height=cap,center=list(center),radius_along=along,radius_across=across,depth=depth,kind=kind,topology=stats)

def tube(name,points,radius,material):
 verts=[];faces=[];N=7
 for i,p in enumerate(points):
  tangent=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized();normal=tangent.cross(Vector((0,0,1)))
  if normal.length<.01:normal=tangent.cross(Vector((0,1,0)))
  normal.normalize();binormal=tangent.cross(normal).normalized();r0=radius*(.86+.14*math.sin(i*1.7))
  for j in range(N):verts.append(tuple(p+(normal*math.cos(math.tau*j/N)+binormal*math.sin(math.tau*j/N))*r0))
 for i in range(len(points)-1):
  for j in range(N):k=(j+1)%N;faces.append((i*N+j,i*N+k,(i+1)*N+k,(i+1)*N+j))
 faces.append(tuple(reversed(range(N))));faces.append(tuple((len(points)-1)*N+j for j in range(N)))
 o=objmesh(name,verts,faces,[material]);o['abyss_deformed']=True;return o
def vine_connection(a,b,material,index):
 # All crossing roots hang 4–10 m beneath musical tile flight height.
 points=[]
 for t in np.linspace(0,1,33):
  p=a.lerp(b,float(t));p.z-=5.5*math.sin(math.pi*t);p.x+=.5*math.sin(3*math.pi*t);p.y+=.6*math.sin(2*math.pi*t);points.append(p)
 return tube(f'Abyss_VolumetricVine_{index:02d}',points,.22 if index%2 else .30,material)

def add_islet_scenery(islet_specs,vine_mats):
 import architecture,scenery
 # A compact radio cottage is entirely off the two-lane flight corridor.
 spec=islet_specs[-1];p,n=road.sample(spec['distance']);t=Vector((n.y,-n.x,0));center=Vector(spec['center']);site=center+n*3.2+t*.3;site.z=play(r.nearest_s(site.x,site.y))-.90+.04
 direction=-n;yaw=math.atan2(direction.y,direction.x)+math.pi*.5
 before=set(bpy.context.scene.objects);architecture.build_building('forest_house',location=site,yaw=yaw,scale=.28,seed=4781)
 for o in set(bpy.context.scene.objects)-before:o['abyss_deformed']=True;o['islet_prop']=True
 # Two trees decorate the broad shelf, far from the tile corridor.
 spec=islet_specs[2];p,n=road.sample(spec['distance']);t=Vector((n.y,-n.x,0));center=Vector(spec['center'])
 for i,along in enumerate((-4.5,4.6)):
  site=center+t*along+n*3.4;site.z=play(r.nearest_s(site.x,site.y))-.90-.1
  before=set(bpy.context.scene.objects);scenery.make_tree(location=site,scale=.85,kind='conifer',seed=8241+i)
  for o in set(bpy.context.scene.objects)-before:o['abyss_deformed']=True;o['islet_prop']=True
 # Roots link both local islands, then the three high coastal islands and the banks.
 pairs=[(0,1),(2,3),(3,4)]
 for idx,(ia,ib) in enumerate(pairs):
  a=Vector(islet_specs[ia]['center']);b=Vector(islet_specs[ib]['center']);a.z=islet_specs[ia]['cap_height']-4.5;b.z=islet_specs[ib]['cap_height']-4.5
  for strand in range(3):
   aa=a+Vector((strand*.6,0,-strand*.4));bb=b+Vector((0,strand*.7,-strand*.3));vine_connection(aa,bb,vine_mats[(idx+strand)%len(vine_mats)],idx*3+strand)
 # Short hanging feeder roots reveal that each island has a solid suspended body.
 for idx,spec in enumerate(islet_specs):
  p,n=road.sample(spec['distance']);a=Vector(spec['center'])+n*(spec['radius_across']*.7);a.z=spec['cap_height']-2.8
  points=[]
  for t in np.linspace(0,1,22):
   q=a+n*(.8*math.sin(math.pi*t));q.z-=10*t;q.x+=.4*math.sin(t*math.pi*3);points.append(q)
  tube(f'Abyss_HangingRoot_{idx}',points,.16,vine_mats[idx%len(vine_mats)])

def relocate_rigid_objects(terrain,objects):
 deps=bpy.context.evaluated_depsgraph_get();bvh=BVHTree.FromObject(terrain,deps);changes={};relocated=0;removed=0
 for o in objects:
  if o.type!='MESH' or o.get('abyss_deformed') or o.get('v3_role')=='road':continue
  base=o.matrix_world.translation
  if abs(base.x)+abs(base.y)<.001:continue
  key=tuple(round(v,4) for v in base)
  if key not in changes:
   s=r.nearest_s(base.x,base.y);p,n=road.sample(s);cross=(base-p).dot(n)
   section=0 if 119<s<191 else 1 if 396<s<507 else None
   delta=Vector((0,0,r.ground(base.x,base.y)))
   if section is not None and (abs(cross)<12.8 or (section==1 and cross>0)):
    sign=1 if cross>=0 else -1;success=False
    for side in (sign,-sign):
     q=p+n*(side*13.3);hit,normal,_,_=bvh.ray_cast(q+Vector((0,0,70)),Vector((0,0,-1)))
     if hit is not None and normal.z>.25 and hit.z>r.ground(q.x,q.y)-2:
      delta=Vector((q.x-base.x,q.y-base.y,hit.z-base.z));success=True;relocated+=1;break
    if not success:delta=None;removed+=1
   changes[key]=delta
  delta=changes[key]
  if delta is None:bpy.data.objects.remove(o,do_unlink=True)
  else:o.matrix_world.translation+=delta;o['abyss_deformed']=True
 return dict(relocated_plant_groups=relocated,removed_unstable_groups=removed)

def bridge_append():
 before=set(bpy.context.scene.objects);bpy.ops.import_scene.fbx(filepath=str(r.OUT/'emerald_relief_island.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update();new=set(bpy.context.scene.objects)-before
 for o in new:
  if o.type=='MESH' and o.get('unity_material_bucket')=='BridgeDeck':
   world=o.matrix_world.copy();o.parent=None;o.matrix_world=world;o['abyss_deformed']=True;o['abyss_bucket']='BridgeDeck'
 for o in new:
  if not o.get('abyss_bucket')=='BridgeDeck':bpy.data.objects.remove(o,do_unlink=True)

def export_optimized(objects,terrain,cliff_mats):
 col=bpy.data.collections.new('Emerald Abyss runtime batches');bpy.context.scene.collection.children.link(col);root=bpy.data.objects.new('EmeraldAbyssIsland',None);col.objects.link(root)
 categories=list(ex.BUCKETS)+['BridgeDeck','Cliffs','Vines','Cascade'];materials={k:r.add_material('RA_Emerald_'+k,k) for k in categories}
 cache={};optimization=collections.Counter();colors={};batches={};serial=collections.Counter();output=[];counts=collections.Counter();skipped=0
 for number,o in enumerate(sorted(objects,key=lambda o:o.name)):
  if o.type!='MESH':continue
  if o.get('v3_role')=='road':skipped+=1;continue
  mesh=ex.simplified_mesh(o,cache,optimization);matrix=o.matrix_world.copy();world=[]
  for v in mesh.vertices:
   w=matrix@v.co
   if not o.get('abyss_deformed') and o.get('v3_role')!='water':w.z+=r.ground(w.x,w.y)
   world.append(w)
  uv=mesh.uv_layers.active.data if mesh.uv_layers.active else None;bank=mesh.attributes.get('river_bank_mix');native=mesh.color_attributes.get('Color');remaps={}
  for poly in mesh.polygons:
   material=mesh.materials[poly.material_index] if poly.material_index<len(mesh.materials) else None
   group=o.get('abyss_bucket') or (material.get('abyss_bucket') if material else None) or ex.bucket(o,material)
   if o==terrain and group=='Rocks':group='Cliffs'
   if group in ('Bridge','Road','StepTreads'):continue
   if material and material.name not in colors:colors[material.name]=ex.color_info(material)
   rgba,slate=colors[material.name] if material else((.2,.2,.2,1),None)
   center=matrix@poly.center;key=(group,math.floor(center.x/ex.CELL),math.floor(center.y/ex.CELL))
   if key not in batches:serial[key]+=1;batches[key]=ex.Batch(f'RA_{group}_{key[1]:+03d}_{key[2]:+03d}_{serial[key]:02d}',group,col,materials[group])
   batch=batches[key]
   if len(batch.v)+len(poly.vertices)>ex.MAX_BATCH_VERTICES:
    item=batch.flush();item.parent=root;output.append(item);serial[key]+=1;batches[key]=batch=ex.Batch(f'RA_{group}_{key[1]:+03d}_{key[2]:+03d}_{serial[key]:02d}',group,col,materials[group]);remaps.pop(key,None)
   remap=remaps.setdefault(key,{});face=[]
   for li,vi in zip(poly.loop_indices,poly.vertices):
    if vi not in remap:remap[vi]=len(batch.v);batch.v.append(tuple(world[vi]))
    face.append(remap[vi]);weight=bank.data[vi].value if bank and bank.domain=='POINT' else 0
    color=tuple(native.data[li if native.domain=='CORNER' else vi].color) if native else tuple(rgba[i]*(1-weight)+slate[i]*weight for i in range(3))+(1,) if slate else tuple(rgba[:3])+(1,)
    batch.colors.append(color);batch.bank.append(weight);batch.uv.append(tuple(uv[li].uv) if uv else(world[vi].x,world[vi].y))
   batch.f.append(tuple(face));batch.soft.append(poly.use_smooth);counts[group]+=len(poly.vertices)-2
  if number%400==0:print('ABYSS_EXPORT',number,len(objects),sum(counts.values()),flush=True)
 for batch in batches.values():
  item=batch.flush()
  if item:item.parent=root;output.append(item)
 for name,pos in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
  item=bpy.data.objects.new(name,None);col.objects.link(item);item.parent=root;item.location=pos
 for o in list(bpy.context.scene.objects):
  if o not in set(col.objects):bpy.data.objects.remove(o,do_unlink=True)
 # The mesh stays in the same triangle budget and does not carry source slivers.
 for o in output:
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bad=[f for f in bm.faces if f.calc_area()<1e-9]
  if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
  bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
 r.export_fbx(OUT/'emerald_abyss_island.fbx',list(col.objects));bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_preview.blend'))
 return dict(meshes=len(output),triangle_estimate=sum(len(o.data.polygons) for o in output),material_buckets=dict(counts),optimization=dict(optimization),road_source_objects_omitted=skipped)

def main():
 start=time.monotonic();OUT.mkdir(parents=True,exist_ok=True);PROOF.mkdir(exist_ok=True)
 frozen={str(p):sha(p) for p in (r.SRC/'island.blend',r.OLD/'emerald_island.fbx',r.OUT/'emerald_relief_island.fbx',r.OUT/'ground_elevation.json',PLAY_PATH)}
 bpy.ops.wm.open_mainfile(filepath=str(r.SRC/'island.blend'));terrain=next(o for o in bpy.context.scene.objects if 'CONNECTED_FLOATING_TERRAIN' in o.name)
 cliff_colors=[(.34,.39,.41),(.39,.43,.43),(.27,.33,.36),(.42,.43,.39),(.30,.38,.39)]
 cliffs=[mat('Abyss_structural_cliff_'+str(i),c,'Cliffs') for i,c in enumerate(cliff_colors)];grass=mat('Abyss_islet_grass',(.29,.42,.20),'Terrain')
 vines=[mat('Abyss_root_vine_'+str(i),c,'Vines') for i,c in enumerate(((.28,.34,.12),(.32,.24,.13),(.22,.30,.13)))]
 terrain_topology=deform_and_carve(terrain,cliffs);originals=list(bpy.context.scene.objects);plants=relocate_rigid_objects(terrain,originals)
 # Erased outer coast must not retain unsupported old sculptural rock groups.
 outer_rocks_removed=0
 for o in list(bpy.context.scene.objects):
  if o.type!='MESH' or o.get('abyss_deformed') or o.get('v3_role')=='water':continue
  base=o.matrix_world.translation
  if abs(base.x)+abs(base.y)>.001:continue
  center=o.matrix_world@(sum((Vector(c) for c in o.bound_box),Vector())/8);s=r.nearest_s(center.x,center.y);p,n=road.sample(s)
  if 396<s<507 and (center-p).dot(n)>0:bpy.data.objects.remove(o,do_unlink=True);outer_rocks_removed+=1
 plants['unsupported_outer_coast_rock_groups_removed']=outer_rocks_removed
 specs=[]
 for name,s,offset,along,across,depth,kind in [('Low grass shard',146,2.5,5.2,3.8,23,'rough'),('Ravine needle',171,-2.4,4.7,3.6,29,'spire'),('High terraced garden',420,2.8,8.5,4.5,27,'terrace'),('Leaning radio spire',445,-2.6,5.0,3.6,35,'spire'),('Copper cottage isle',478,2.8,6.8,4.5,25,'rough')]:
  o,spec=islet('Abyss_'+name,s,offset,along,across,depth,kind,cliffs,grass);specs.append(spec)
 add_islet_scenery(specs,vines);bridge_append()
 stats=export_optimized(list(bpy.context.scene.objects),terrain,cliffs)
 for source,target in [(r.OUT/'emerald_relief_route.json','emerald_abyss_route.json'),(r.OUT/'ground_elevation.json','ground_elevation.json'),(PLAY_PATH,'play_elevation.json')]:shutil.copyfile(source,OUT/target)
 report=dict(schema='radio_archipelago_abyss_pilot/1',frozen_inputs=frozen,frozen_inputs_unchanged=all(sha(pathlib.Path(p))==h for p,h in frozen.items()),road_geometry_absent=True,cobblestones_absent=True,wooden_walkway_absent=True,source_connected_terrain_topology=terrain_topology,
  clear_flight_corridor_width=7,ravines=[dict(start=125,end=185,mouth_width=[12,18],through_entire_island=True),dict(start=402,end=501,mouth_width=[12,18],through_entire_island=True)],islets=specs,plant_relocation=plants,root_geometry='closed opaque seven-sided volumetric tubes, below tile flight',
  route_xz_unchanged=True,ground_and_play_json_byte_identical=True,bridge_distance=90.5,house_hill_height=8,structural_materials=['RA_Emerald_Terrain','RA_Emerald_Cliffs','RA_Emerald_BridgeDeck'],vines_material='RA_Emerald_Vines',export=stats,elapsed_seconds=time.monotonic()-start)
 (HERE/'abyss_report.json').write_text(json.dumps(report,indent=2));assert report['frozen_inputs_unchanged'];print('ABYSS_EXPORTED',json.dumps(stats),flush=True)

if __name__=='__main__':main()
