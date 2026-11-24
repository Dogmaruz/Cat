"""Forest-only derivative: exact old-tree LOD vertex whitelist, botanical replacements."""
import bpy,pathlib,sys,json,math,collections,hashlib,time,numpy as np
from mathutils import Vector,Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));sys.path.insert(0,str(HERE.parent));sys.path.insert(0,str(HERE.parent/'LivingPass'));sys.path.insert(0,str(HERE.parent/'IsletPolish'))
import botanical as bot,build_living as living,polish_islets as p
a=living.a;PATH=a.OUT/'emerald_abyss_island.fbx';CELL=64
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def meshhash(o):
 m=o.data;v=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',v);ids=np.empty(len(m.loops),np.int32);m.loops.foreach_get('vertex_index',ids);c=m.color_attributes.get('Color');colors=np.empty(len(c.data)*4,np.float32) if c else np.empty(0)
 if c:c.data.foreach_get('color',colors)
 return hashlib.sha256(v.tobytes()+ids.tobytes()+colors.tobytes()).hexdigest()
class Batch:
 def __init__(self,key):self.key=key;self.clear()
 def clear(self):self.v=[];self.f=[];self.c=[];self.uv=[];self.norm=[];self.smooth=[]
 def add(self,v,f,c,uv,norm,smooth):
  offset=len(self.v);self.v.extend(v);self.f.extend(tuple(offset+i for i in face) for face in f);self.c.extend(c);self.uv.extend(uv);self.norm.extend(norm);self.smooth.extend(smooth)
 def flush(self):
  if not self.f:return
  cat,x,y=self.key;serial[self.key]+=1;name=f'RA_{cat}_Natural_{x:+03d}_{y:+03d}_{serial[self.key]:02d}';me=bpy.data.meshes.new(name);me.from_pydata(self.v,[],self.f);me.update();me.materials.append(materials[cat]);attr=me.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');attr.data.foreach_set('color',np.asarray(self.c,np.float32).reshape(-1))
  if self.uv:uv=me.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',np.asarray(self.uv,np.float32).reshape(-1))
  for f,s in zip(me.polygons,self.smooth):f.use_smooth=s
  if self.norm:me.normals_split_custom_set(self.norm)
  ob=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(ob);ob['unity_material_bucket']=cat
  if cat in('TreeBark','TreeLeaves'):ob['natural_tree_pass']=True
  if cat=='TreeLeaves':ob['leaf_uv_contract']='U across0..1 / V base-to-tip0..1; Color.a=V wind only; 6-triangle curved shell, two sided'
  output.append(ob);self.clear()
def put(cat,position,v,f,c,uv,norm=None,smooth=None):
 key=(cat,math.floor(position[0]/CELL),math.floor(position[1]/CELL));b=batches.setdefault(key,Batch(key))
 if len(b.v)+len(v)>58000:b.flush()
 b.add(v,f,c,uv,norm or [],smooth if smooth is not None else [True]*len(f))
def payload(o,faces):
 m=o.data;used=sorted({vi for face in faces for vi in face.vertices});remap={vi:i for i,vi in enumerate(used)};v=[tuple(o.matrix_world@m.vertices[i].co) for i in used];ff=[tuple(remap[i] for i in f.vertices) for f in faces];li=[i for f in faces for i in f.loop_indices];attr=m.color_attributes.get('Color');c=[tuple(attr.data[i if attr.domain=='CORNER' else m.loops[i].vertex_index].color) for i in li];uv=[tuple(m.uv_layers.active.data[i].uv) for i in li] if m.uv_layers.active else [(0,0)]*len(li)
 normals=m.corner_normals;normalmatrix=o.matrix_world.to_3x3().inverted().transposed();n=[tuple((normalmatrix@normals[i].vector).normalized()) for i in li];return v,ff,c,uv,n,[f.use_smooth for f in faces]
def build():
 global materials,batches,serial,output
 start=time.monotonic();bpy.ops.wm.open_mainfile(filepath=str(HERE.parent/'LivingPass/emerald_abyss_living.blend'));base=set(bpy.context.scene.objects);originals=[o for o in base if o.type=='MESH'];main=bpy.data.objects['RA_Closed_Mainland'];untouched={o.name:meshhash(o) for o in originals if o.get('unity_material_bucket') not in('Wood','Foliage')};checkpoint=sha(HERE/'pre_natural_trees_checkpoint.fbx')
 with bpy.data.libraries.load(str(a.r.SRC/'island.blend'),link=False) as (fr,to):to.objects=[n for n in fr.objects if n.startswith('V4_Tree_') and not any(w in n.lower() for w in('shrub','bush'))]
 for o in to.objects:
  if o:bpy.context.scene.collection.objects.link(o)
 sources=list(set(bpy.context.scene.objects)-base);bpy.context.view_layer.update();relocation=a.relocate_rigid_objects(main,sources);sources=list(set(bpy.context.scene.objects)-base);bpy.context.view_layer.update()
 # Reproduce the two inherited islet trees, which were generated outside V4.
 spec=next(s for s in p.SPECS if s['distance']==420)
 import scenery
 for j,u in enumerate((-4.5,4.6)):
  q=p.world(spec,u,3.4);q.z=p.grade(spec,u,3.4)-1
  sources+=scenery.make_tree(location=q,scale=.85,kind='conifer',seed=8241+j)
 bpy.context.view_layer.update();cache={};stats=collections.Counter();points=[];sites={};source_ranges=[]
 for o in sources:
  mesh=a.ex.simplified_mesh(o,cache,stats);world=[o.matrix_world@v.co for v in mesh.vertices];begin=len(points);points.extend(world);source_ranges.append((o.name,begin,len(points)));sitekey=tuple(round(x,3) for x in o.matrix_world.translation);site=sites.setdefault(sitekey,dict(base=tuple(o.matrix_world.translation),points=[],kind='pine' if 'pine' in o.name or 'conifer' in o.name else 'broadleaf',names=[]));site['points'].extend(world);site['names'].append(o.name)
 kd=KDTree(len(points))
 for i,q in enumerate(points):kd.insert(q,i)
 kd.balance();print('NATURAL_SOURCE_READY',len(sites),len(points),flush=True)
 materials={cat:bpy.data.materials.get('RA_Emerald_'+cat) or a.r.add_material('RA_Emerald_'+cat,cat) for cat in('Wood','Foliage','TreeBark','TreeLeaves')};batches={};serial=collections.Counter();output=[];removed=[];survivor_corners=0;survivor_sum=np.zeros(7,np.float64)
 for o in originals:
  if o.get('unity_material_bucket') not in('Wood','Foliage'):continue
  world=[o.matrix_world@v.co for v in o.data.vertices];matched=[kd.find(q)[2]<.001 for q in world];keep=[f for f in o.data.polygons if not all(matched[i] for i in f.vertices)];drop=[f for f in o.data.polygons if all(matched[i] for i in f.vertices)];removed.append(dict(object=o.name,triangles=sum(len(f.vertices)-2 for f in drop),surviving_polygons=len(keep)))
  if keep:
   data=payload(o,keep);center=sum((Vector(q) for q in data[0]),Vector())/len(data[0]);put(o.get('unity_material_bucket'),center,*data);survivor_corners+=len(data[2])
  bpy.data.objects.remove(o,do_unlink=True)
 for o in sources:bpy.data.objects.remove(o,do_unlink=True)
 print('NATURAL_OLD_REMOVED',sum(x['triangles'] for x in removed),flush=True)
 structural=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('structural_closed_shell')];floorbvh,floor_sources=living.ground_bvh(structural)
 variants=[bot.make_variant(i) for i in range(6)];placed=[];unsupported=[]
 for index,(key,site) in enumerate(sorted(sites.items())):
  old=Vector(site['base']);hit,normal,poly,d=floorbvh.ray_cast(old+Vector((0,0,70)),Vector((0,0,-1)),150)
  if hit is None or normal.z<.45:unsupported.append(dict(base=list(old),reason='no stable ground'));continue
  sitepoints=site['points'];height=max(q.z for q in sitepoints)-min(q.z for q in sitepoints);oldradius=max(math.hypot(q.x-old.x,q.y-old.y) for q in sitepoints);variant=(4+index%2) if site['kind']=='pine' else index%4;bark,leaf,meta=variants[variant];allv=np.asarray(bark.v+leaf.v);radius=float(np.max(np.linalg.norm(allv[:,:2],axis=1)));sx=oldradius*.94/radius;sy=sx*(.91+.09*math.sin(index*2.4));sz=height/max(allv[:,2]);s=a.r.nearest_s(old.x,old.y);rp,rn=a.road.sample(s);cross=(old-rp).dot(rn);limit=max(.12,abs(cross)-4.20)
  if oldradius*.94>limit:sx*=limit/(oldradius*.94);sy*=limit/(oldradius*.94)
  yaw=index*2.399963;transform=Matrix.Translation((old.x,old.y,hit.z+.02))@Matrix.Rotation(yaw,4,'Z')@Matrix.Diagonal((sx,sy,sz,1));attempt=0
  while True:
   worldv=[transform@Vector(v) for v in bark.v+leaf.v];danger=False
   for q in worldv:
    _,_,dist=a.r.route_kd.find((q.x,q.y,0))
    if dist<4.05:
     ss=a.r.nearest_s(q.x,q.y);rr,_=a.road.sample(ss)
     if math.hypot(q.x-rr.x,q.y-rr.y)<3.85 and q.z>a.play(ss)+.2:danger=True;break
   if not danger or attempt>=5:break
   sx*=.77;sy*=.77;transform=Matrix.Translation((old.x,old.y,hit.z+.02))@Matrix.Rotation(yaw,4,'Z')@Matrix.Diagonal((sx,sy,sz,1));attempt+=1
  assert not danger,('Tree clearance',index,old)
  for cat,geo in [('TreeBark',bark),('TreeLeaves',leaf)]:put(cat,old,[tuple(transform@Vector(v)) for v in geo.v],geo.f,geo.c,geo.uv)
  placed.append(dict(original_names=site['names'],base=[old.x,old.y,hit.z+.02],support_z=hit.z,ground_gap=.02,variant=meta['name'],leaves=meta['leaves'],height=height,footprint_radius=oldradius,xy_scale=[sx,sy],clearance_shrink_steps=attempt))
 for b in batches.values():b.flush()
 for name,h in untouched.items():assert meshhash(bpy.data.objects[name])==h,('Nonforest changed',name)
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];tri=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in meshes);barktop=[];normbad=[]
 for o in meshes:
  if o.get('unity_material_bucket')=='TreeBark':
   stat=a.topology(o.data);barktop.append(dict(name=o.name,**stat));assert stat['boundary_edges']==0 and stat['nonmanifold_edges']==0
  vals=np.empty(len(o.data.loops)*3,np.float32);o.data.corner_normals.foreach_get('vector',vals);bad=int(np.sum(np.abs(np.linalg.norm(vals.reshape(-1,3),axis=1)-1)>.001))
  if bad:normbad.append(dict(name=o.name,corners=bad))
 assert not normbad,normbad
 report=dict(schema='abyss_natural_tree_pass/1',checkpoint_sha256=checkpoint,tree_sites=len(sites),trees_replaced=len(placed),unsupported_sites=unsupported,variants=[meta for _,_,meta in variants],old_tree_triangles_removed=sum(x['triangles'] for x in removed),deleted_tree_polygons=removed,preserved_low_vegetation_corners=survivor_corners,unchanged_nonforest_meshes=untouched,nonforest_hashes_unchanged=True,source_shrub_pairs_excluded=258,new_tree_placements=placed,mesh_batches=len(meshes),triangle_budget=tri,new_batches=len(output),bark_closed_topology=barktop,invalid_normals=normbad,source_relocation=relocation,elapsed_seconds=time.monotonic()-start)
 (HERE/'natural_report.json').write_text(json.dumps(report,indent=2));assert len(meshes)<=350,(len(meshes),tri);assert tri<7200000,tri
 bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_abyss_natural.blend'));print('NATURAL_NATIVE_READY',len(meshes),tri,len(placed),time.monotonic()-start,flush=True)
 # Only the explicitly authorized derivative FBX is written.
 bpy.ops.object.select_all(action='DESELECT')
 for o in bpy.context.scene.objects:
  if o.type in('MESH','EMPTY'):o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(PATH),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
 report['fbx_sha256']=sha(PATH);report['fbx_bytes']=PATH.stat().st_size;(HERE/'natural_report.json').write_text(json.dumps(report,indent=2));print('NATURAL_FBX_EXPORTED',report['fbx_sha256'],flush=True)
if __name__=='__main__':build()
