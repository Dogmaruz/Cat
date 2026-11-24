"""Grounded massive path fragments, cliff rock piles and real wind-weighted grass."""
import bpy,bmesh,pathlib,sys,json,random,math,collections,hashlib,numpy as np,time
from mathutils import Vector,Matrix,Euler
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE.parent));sys.path.insert(0,str(HERE.parent/'IsletPolish'));import polish_islets as p
import scenery
a=p.a;PATH=a.OUT/'emerald_abyss_island.fbx';CELL=32;RNG=random.Random(284917)

class Batch:
 def __init__(self,key):self.key=key;self.v=[];self.f=[];self.c=[];self.closed=True
 def append(self,verts,faces,colors):
  offset=len(self.v);self.v.extend(verts)
  for f,col in zip(faces,colors):self.f.append(tuple(offset+i for i in f));self.c.extend(col if isinstance(col,list) else[col]*len(f))
 def flush(self):
  if not self.f:return None
  cat,cx,cy=self.key;serial[self.key]+=1;name=f'RA_{cat}_Living_{cx:+03d}_{cy:+03d}_{serial[self.key]:02d}'
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.v,[],self.f);mesh.update();mesh.materials.append(materials[cat]);color=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');color.data.foreach_set('color',np.asarray(self.c,np.float32).reshape(-1))
  obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj);obj['unity_material_bucket']=cat;obj['living_pass']=True
  if cat=='Grass':obj['vertex_alpha_role']='Wind weight: root0, mid0.55, tip1; never opacity'
  output.append(obj);self.v=[];self.f=[];self.c=[];return obj
def put(cat,verts,faces,colors,position):
 key=(cat,math.floor(position.x/CELL),math.floor(position.y/CELL));batch=batches.setdefault(key,Batch(key))
 if len(batch.v)+len(verts)>58000:batch.flush()
 batch.append(verts,faces,colors)
def rgba(rgb,alpha=1):return tuple(a.r.cob.linear(float(c)) for c in rgb)+(alpha,)
def hashmesh(o):
 co=np.empty(len(o.data.vertices)*3,np.float32);o.data.vertices.foreach_get('co',co);ids=np.empty(len(o.data.loops),np.int32);o.data.loops.foreach_get('vertex_index',ids);return hashlib.sha256(co.tobytes()+ids.tobytes()).hexdigest()

def ground_bvh(objects):
 vertices=[];faces=[];sources=[]
 for obj in objects:
  mesh=obj.data;offset=len(vertices);vertices.extend(obj.matrix_world@v.co for v in mesh.vertices)
  for face in mesh.polygons:
   mat=mesh.materials[face.material_index] if face.material_index<len(mesh.materials) else None
   if mat and mat.name.split('.')[0]=='RA_Emerald_Terrain':faces.append(tuple(offset+i for i in face.vertices));sources.append(obj.name)
 return BVHTree.FromPolygons(vertices,faces,all_triangles=False),sources
def floor(x,y,expected=None):
 hit,normal,index,distance=grass_bvh.ray_cast(Vector((x,y,90)),Vector((0,0,-1)),160)
 if hit is None or normal.z<.55:return None
 source=grass_sources[index]
 if 'Mainland' in source and hit.z<-.45:return None
 if expected and source!=expected:return None
 return hit,normal,source
def nearroute(q):
 s=a.r.nearest_s(q.x,q.y);point,normal=a.road.sample(s);return s,(q-point).dot(normal)
def closed_rock(target,scale,rotation,seed):
 objects=scenery.make_rock(location=(0,0,0),scale=1,kind='slate' if seed%6==0 else 'boulder',palette={'rock':(.37,.42,.43),'style':'forest'},seed=seed)
 base=next(o for o in objects if 'fracture_planes' in o.name);center=sum((Vector(c) for c in base.bound_box),Vector())/8
 transform=Matrix.Translation(target)@Euler(rotation,'XYZ').to_matrix().to_4x4()@Matrix.Diagonal((*scale,1))@Matrix.Translation(-center)
 verts=[transform@v.co for v in base.data.vertices];lower=0
 for q in verts:
  s,cross=nearroute(q)
  if abs(cross)<4:lower=max(lower,q.z-(a.play(s)-1.3))
 if lower>0:verts=[q-Vector((0,0,lower)) for q in verts]
 colors={i:a.ex.color_info(m)[0] for i,m in enumerate(base.data.materials)};faces=[tuple(f.vertices) for f in base.data.polygons];cols=[tuple(colors[f.material_index][:3])+(1,) for f in base.data.polygons]
 put('Cliffs',[tuple(v) for v in verts],faces,cols,target)
 for obj in objects:bpy.data.objects.remove(obj,do_unlink=True)

def cliff_piles(mainland):
 bvh=BVHTree.FromObject(mainland,bpy.context.evaluated_depsgraph_get());matrix=mainland.matrix_world;inv=matrix.inverted();count=0;anchors=[]
 for start,end in ((119,191),(396,507)):
  for s in np.arange(start,end,4.2):
   point,n=a.road.sample(float(s))
   for side in(-1,1):
    for level,depth in enumerate((3.3,10,18,26)):
     origin=point.copy();origin.z=a.r.ground(point.x,point.y)-depth;direction=n*side
     hit,normal,index,dist=bvh.ray_cast(inv@origin,inv.to_3x3()@direction,45)
     if hit is None or abs(normal.z)>.75:continue
     hit=matrix@hit;normal=(matrix.to_3x3().inverted().transposed()@normal).normalized();target=hit+normal*RNG.uniform(.35,1.05);target.z+=RNG.uniform(-.7,.7)
     scale=(RNG.uniform(1.7,2.8),RNG.uniform(1.5,2.4),RNG.uniform(2.9,4.5));rotation=(RNG.uniform(-.9,.9),RNG.uniform(-1,1),RNG.uniform(0,math.tau))
     closed_rock(target,scale,rotation,94700+count);anchors.append(dict(distance=float(s),side=side,depth=depth,hit=list(hit)));count+=1
 # Fill the remaining visible bare planes with surface-area-weighted clusters.
 # Sampling actual structural triangles keeps every new mass rooted in the wall.
 mainland.data.calc_loop_triangles();triangles=[];weights=[];existing=[Vector(x['hit']) for x in anchors]
 for tri in mainland.data.loop_triangles:
  face=mainland.data.polygons[tri.polygon_index];mat=mainland.data.materials[face.material_index]
  if mat.name.split('.')[0]!='RA_Emerald_Cliffs':continue
  vv=[matrix@mainland.data.vertices[i].co for i in tri.vertices];cross=(vv[1]-vv[0]).cross(vv[2]-vv[0]);area=cross.length*.5
  if area<.05:continue
  normal=cross.normalized();center=sum(vv,Vector())/3;s,lateral=nearroute(center)
  if abs(normal.z)>.8 or not(116<s<194 or 393<s<510) or abs(lateral)>25 or center.z>a.r.ground(center.x,center.y)+2:continue
  triangles.append((vv,normal));weights.append(area)
 for attempt in range(2200):
  if count>=210 or not triangles:break
  vv,normal=RNG.choices(triangles,weights=weights,k=1)[0];u,v=RNG.random(),RNG.random()
  if u+v>1:u,v=1-u,1-v
  hit=vv[0]+(vv[1]-vv[0])*u+(vv[2]-vv[0])*v;s,lateral=nearroute(hit)
  if abs(lateral)>25 or hit.z<a.r.ground(hit.x,hit.y)-31 or min((hit-x).length for x in existing)<3.3:continue
  target=hit+normal*RNG.uniform(.35,.95);scale=(RNG.uniform(1.7,2.8),RNG.uniform(1.5,2.4),RNG.uniform(2.9,4.5));rotation=(RNG.uniform(-.9,.9),RNG.uniform(-1,1),RNG.uniform(0,math.tau));closed_rock(target,scale,rotation,94700+count)
  anchors.append(dict(distance=s,side='surface',depth=a.r.ground(hit.x,hit.y)-hit.z,hit=list(hit)));existing.append(hit);count+=1
 return dict(rock_models=count,actual_wall_ray_or_surface_anchors=anchors)

def stone(q,width,length,yaw,height):
 N=8;outline=[]
 for i in range(N):
  angle=math.tau*i/N;xx=math.cos(angle)*length*.5*RNG.uniform(.86,1.10);yy=math.sin(angle)*width*.5*RNG.uniform(.86,1.10);outline.append((xx*math.cos(yaw)-yy*math.sin(yaw),xx*math.sin(yaw)+yy*math.cos(yaw)))
 samples=[floor(q.x+x,q.y+y) for x,y in outline]
 if any(x is None for x in samples):return False
 heights=[x[0].z for x in samples]
 if max(heights)-min(heights)>.48:return False
 verts=[];faces=[];cols=[];tone=RNG.uniform(.88,1.12);base=rgba(tuple(c*tone for c in(.46,.48,.46)))
 for radius,z in((.83,-.055),(1,.015),(.96,height-.05),(.75,height)):
  for (x,y),hit in zip(outline,samples):verts.append((q.x+x*radius,q.y+y*radius,hit[0].z+z))
 for ring in range(3):
  for i in range(N):j=(i+1)%N;faces.append((ring*N+i,ring*N+j,(ring+1)*N+j,(ring+1)*N+i));cols.append(base)
 faces.append(tuple(reversed(range(N))));cols.append(base);faces.append(tuple(3*N+i for i in range(N)));cols.append(base)
 put('Rocks',verts,faces,cols,q);return True
def broken_path():
 s=RNG.uniform(1,4);groups=[];count=0
 while s<a.road.length:
  group_size=RNG.choices((1,2,5,8),(3,3,3,2))[0];made=0
  for i in range(group_size):
   along=RNG.uniform(-1.45,1.45);point,n=a.road.sample(s+along);cross=RNG.uniform(-2.45,2.45);q=point+n*cross;hit=floor(q.x,q.y)
   if hit is None:continue
   q.z=hit[0].z
   if stone(q,RNG.uniform(.62,1.12),RNG.uniform(.75,1.30),RNG.uniform(0,math.tau),RNG.uniform(.18,.32)):made+=1;count+=1
  if made:groups.append(dict(distance=s,stones=made,along_radius=1.45))
  s+=RNG.uniform(5.8,11.5)
 return dict(stones=count,patches=groups,nominal_footprint=[.62,1.30],disconnected=True,all_outline_points_supported=True)

def tuft(q,normal,height_scale=1):
 support=[floor(q.x+math.cos(i*math.tau/4)*.30,q.y+math.sin(i*math.tau/4)*.30) for i in range(4)]
 if any(s is None for s in support):return False
 if max(s[0].z for s in support)-min(s[0].z for s in support)>.45:return False
 verts=[];faces=[];cols=[];blades=RNG.randint(4,7)
 for k in range(blades):
  theta=RNG.random()*math.tau;axis=Vector((math.cos(theta),math.sin(theta),0));side=Vector((-axis.y,axis.x,0));dx,dy=RNG.uniform(-.17,.17),RNG.uniform(-.17,.17);root=q+Vector((dx,dy,-(normal.x*dx+normal.y*dy)/max(normal.z,.1)-.015));h=RNG.uniform(.16,.68)*height_scale
  s,cross=nearroute(root)
  if abs(cross)<3.8:h=min(h,max(.08,a.play(s)+.73-root.z))
  width=RNG.uniform(.020,.040);thick=width*.16;lean=axis*RNG.uniform(.06,.23);bottom=len(verts);color=RNG.choice(((.27,.45,.15),(.34,.49,.18),(.22,.39,.13),(.40,.52,.22)))
  for level,center,scale,weight in((0,root,1,0),(1,root+lean*.42+Vector((0,0,h*.55)),.72,.55)):
   for x,y in((-width,-thick),(width,-thick),(0,thick)):
    p0=center+side*(x*scale)+axis*(y*scale);verts.append(tuple(p0))
  verts.append(tuple(root+lean+Vector((0,0,h))));weights=[0,0,0,.55,.55,.55,1]
  local=[(0,2,1),(0,1,4,3),(1,2,5,4),(2,0,3,5),(3,4,6),(4,5,6),(5,3,6)]
  for f in local:
   faces.append(tuple(bottom+i for i in f));cols.append([rgba(tuple(c*(.77+.23*weights[i]) for c in color),weights[i]) for i in f])
 put('Grass',verts,faces,cols,q);return True
def real_grass():
 count=0;attempts=0;area_counts=collections.Counter()
 def cluster(x,y,kind,expected=None,size=3):
  nonlocal count,attempts
  for j in range(size):
   qx=x+RNG.uniform(-.62,.62);qy=y+RNG.uniform(-.62,.62);attempts+=1;hit=floor(qx,qy,expected)
   if hit is None:continue
   q,normal,source=hit
   if tuft(q,normal,RNG.uniform(.72,1.18)):count+=1;area_counts[kind]+=1
 for s in np.arange(0,a.road.length,.55):
  point,n=a.road.sample(float(s))
  for cross in(-7,-3.8,0,3.8,7):
   if RNG.random()>.78:continue
   q=point+n*(cross+RNG.uniform(-1.2,1.2));cluster(q.x,q.y,'mainland route banks',size=RNG.randint(3,5))
 for spec in p.SPECS:
  center,t,n=p.frame(spec)
  for u in np.arange(-spec['radius_along']*1.12,spec['radius_along']*1.12,.44):
   for v in np.arange(-spec['radius_across']*1.12,spec['radius_across']*1.12,.44):
    if RNG.random()>.78:continue
    q=center+t*float(u)+n*float(v);cluster(q.x,q.y,'floating meadows','RA_Polished_'+str(spec['distance']),RNG.randint(3,5))
 return dict(tufts=count,placement_attempts=attempts,by_area=dict(area_counts),blades_per_tuft=[4,7],blade_height=[.12,.80],rgba_alpha='Root0 / mid0.55 / tip1, wind only; material opaque')

if __name__=='__main__':
 start=time.monotonic();bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(HERE/'pre_living_checkpoint.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update();originals=[o for o in bpy.context.scene.objects if o.type=='MESH'];main=bpy.data.objects['RA_Closed_Mainland'];main_hash=hashmesh(main)
 structural=[o for o in originals if o.get('structural_closed_shell')];grass_bvh,grass_sources=ground_bvh(structural)
 materials={cat:bpy.data.materials.get('RA_Emerald_'+cat) or a.r.add_material('RA_Emerald_'+cat,cat) for cat in('Cliffs','Rocks','Grass')};batches={};serial=collections.Counter();output=[]
 rocks=cliff_piles(main);print('LIVING_CLIFFS_READY',rocks['rock_models'],flush=True);stones=broken_path();print('LIVING_STONES_READY',stones['stones'],flush=True);grass=real_grass();print('LIVING_GRASS_READY',grass,flush=True)
 for batch in batches.values():batch.flush()
 repairs=[]
 for o in bpy.context.scene.objects:
  if o.type!='MESH':continue
  mesh=o.data;val=np.empty(len(mesh.loops)*3,np.float64);mesh.corner_normals.foreach_get('vector',val);val=val.reshape(-1,3);lengths=np.linalg.norm(val,axis=1);bad=set(int(i) for i in np.flatnonzero(np.abs(lengths-1)>.001))
  if bad:
   for face in mesh.polygons:
    if not any(i in bad for i in face.loop_indices):continue
    normal=face.normal.copy();normal=normal.normalized() if normal.length>.01 else Vector((0,0,1));face.use_smooth=True
    for i in face.loop_indices:
     if i in bad:val[i]=normal
   val/=np.maximum(np.linalg.norm(val,axis=1)[:,None],1e-20);mesh.normals_split_custom_set(val);repairs.append(dict(mesh=o.name,corners=len(bad)))
  if o.get('living_pass') and o.get('unity_material_bucket') in('Cliffs','Rocks','Grass'):
   stat=a.topology(mesh);assert stat['boundary_edges']==0 and stat['nonmanifold_edges']==0,(o.name,stat)
 assert hashmesh(main)==main_hash
 total=[o for o in bpy.context.scene.objects if o.type=='MESH'];tri=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in total);assert tri<=7000000,tri
 bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(PATH),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
 report=dict(schema='abyss_living_pass/1',pre_living_sha256=hashlib.sha256((HERE/'pre_living_checkpoint.fbx').read_bytes()).hexdigest(),mainland_mesh_hash_before=main_hash,mainland_mesh_hash_after=hashmesh(main),mainland_unchanged=True,cliff_piles=rocks,broken_stones=stones,grass=grass,mesh_batches=len(total),triangle_budget=tri,new_batches=len(output),normal_fallbacks=repairs,elapsed_seconds=time.monotonic()-start)
 (HERE/'living_report.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_living_preview.blend'));print('LIVING_EXPORTED',len(total),tri,time.monotonic()-start,flush=True)
