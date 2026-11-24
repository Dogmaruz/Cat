"""One connected lake/outlet/fall surface; no overlaid water reaches."""
import bpy,pathlib,json,hashlib,math,struct,array,collections,bisect,sys
from mathutils import Vector, Matrix
from mathutils.geometry import delaunay_2d_cdt,barycentric_transform
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;ISLANDS=HERE.parent
CONFIG={
 'Coral':dict(folder=ISLANDS/'UnifiedWaterfalls20261102/STAGING/Coral',blend='coral_continuous_waterfalls.blend',fbx='coral_oasis_island.fbx',sha='7b5617164bdf969d41221d9a83c5ff6c288dc91795b7c41353079ae654344ce2',pool='RA_Coral_Water',triangles=False),
 'Moon':dict(folder=ISLANDS/'UnifiedWaterfalls20261102/STAGING/Moon',blend='moon_continuous_waterfalls.blend',fbx='moon_resonance_island.fbx',sha='fba3247c094803b1a51b519e528afc3b3b1b139b40bf728320b4ef1941cd9c5f',pool='RA_Moon_Water',triangles=False),
 'Amber':dict(folder=ISLANDS/'AmberWindsPilot/WaterfallRework20261102',blend='amber_winds_continuous.blend',fbx='amber_winds_island.fbx',sha='18cfb754ba06d63a01a95c069ff5d7dbc5142e7415b42c6f19bf48187e45305f',pool='RA_Amber_Water',triangles=True)
}
sha=lambda p:hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def signature(ob):
 h=hashlib.sha256();h.update(struct.pack('<16f',*(x for row in ob.matrix_world for x in row)))
 h.update(json.dumps(dict(type=ob.type,parent=ob.parent.name if ob.parent else None,props={k:ob[k]for k in ob.keys()}),sort_keys=True).encode())
 if ob.type!='MESH':return h.hexdigest()
 m=ob.data
 for coll,key,w,fmt in [(m.vertices,'co',3,'f'),(m.loops,'vertex_index',1,'I')]:
  a=array.array(fmt,[0])*(len(coll)*w);coll.foreach_get(key,a);h.update(a.tobytes())
 for p in m.polygons:h.update(struct.pack('<4I',p.loop_start,p.loop_total,p.material_index,int(p.use_smooth)))
 for layer in m.uv_layers:
  h.update(layer.name.encode());a=array.array('f',[0])*(len(layer.data)*2);layer.data.foreach_get('uv',a);h.update(a.tobytes())
 for layer in m.color_attributes:
  h.update((layer.name+layer.domain+layer.data_type).encode());a=array.array('f',[0])*(len(layer.data)*4);layer.data.foreach_get('color',a);h.update(a.tobytes())
 for n in m.corner_normals:h.update(struct.pack('<3f',*n.vector))
 h.update(json.dumps([m.name if m else None for m in m.materials]).encode());return h.hexdigest()
def key(p):return tuple(round(float(v),5)for v in p[:2])
def rings(ob):
 rows=collections.defaultdict(dict);uv=ob.data.uv_layers.active
 for l in ob.data.loops:
  u,v=uv.data[l.index].uv;p=ob.matrix_world@ob.data.vertices[l.vertex_index].co
  rows[round(float(v),5)][round(float(u),6)]=(p,float(v))
 return [(next(iter(row.values()))[1],[row[u][0]for u in sorted(row)])for v,row in sorted(rows.items())]
def normal_map(ob):
 result={}
 for l in ob.data.loops:result[key(ob.matrix_world@ob.data.vertices[l.vertex_index].co)+(round((ob.matrix_world@ob.data.vertices[l.vertex_index].co).z,5),)]=ob.data.corner_normals[l.index].vector.copy()
 return result
def alpha_map(ob):
 result={};color=ob.data.color_attributes.active_color
 for l in ob.data.loops:
  p=ob.matrix_world@ob.data.vertices[l.vertex_index].co
  result[key(p)+(round(p.z,5),)]=float(color.data[l.index].color[3])
 return result
def centroid(row):return sum(row,Vector((0,0,0)))/len(row)
def triangle_faces(faces):
 for f in faces:
  for j in range(1,len(f)-1):yield(f[0],f[j],f[j+1])
def bounds(ps):return[[min(p[k]for p in ps),max(p[k]for p in ps)]for k in range(3)]

def build_one(old,info,pool_faces,label):
 rr=rings(old);cols=len(rr[0][1])-1;z=rr[0][1][cols//2].z
 # Join at an original flat ribbon row beyond all upstream pool overlap. Coral
 # high needs its full reach; Amber retains the entire approved curved body.
 if label=='Amber':threshold={'lower':9.7,'R3_canyon':10.0,'raised_side':5.3}[info['id']]
 elif label=='Coral'and info['id']=='high_spring':threshold=3.4
 else:threshold=.95
 flat=[i for i,(v,row)in enumerate(rr)if max(abs(p.z-z)for p in row)<.0001]
 ji=next(i for i in flat if rr[i][0]>=threshold)
 join_v,join_row=rr[ji];toprows=rr[:ji+1];bodyrows=rr[ji:]
 assert all(len(row)==cols+1 for v,row in rr)
 ps=[];fs=[];lookup={};original_uv={}
 def add(p,uv=None):
  k=key(p)
  if k not in lookup:lookup[k]=len(ps);ps.append(Vector((p.x,p.y)))
  if uv is not None:original_uv[k]=uv
  return lookup[k]
 for face in pool_faces:fs.append(tuple(add(p)for p in face))
 patch_ps=[];patch_uv=[];patch_faces=[]
 for v,row in toprows:
  patch_ps.extend(Vector((p.x,p.y,0))for p in row);patch_uv.extend(Vector((j/cols,v,0))for j in range(cols+1))
  for j,p in enumerate(row):add(p,(j/cols,v))
 for i in range(len(toprows)-1):
  for j in range(cols):a=i*(cols+1)+j;patch_faces.append((a,a+1,a+cols+2,a+cols+1))
 for face in patch_faces:fs.append(tuple(add(patch_ps[i],tuple(patch_uv[i][:2]))for i in face))
 # Boundary constraints of each original surface are inserted into one CDT.
 # Filtering against their union removes internal double faces altogether.
 pool_count=len(pool_faces);counts=collections.Counter(tuple(sorted((a,b)))for f in fs[:pool_count]for a,b in zip(f,f[1:]+f[:1]))
 edges=[e for e,n in counts.items()if n==1]
 pcounts=collections.Counter(tuple(sorted((a,b)))for f in fs[pool_count:]for a,b in zip(f,f[1:]+f[:1]))
 edges.extend(e for e,n in pcounts.items()if n==1)
 source_bvh=BVHTree.FromPolygons([Vector((p.x,p.y,0))for p in ps],list(triangle_faces(fs)),all_triangles=True)
 patch_triangles=list(triangle_faces(patch_faces));patch_bvh=BVHTree.FromPolygons(patch_ps,patch_triangles,all_triangles=True)
 coords,_,triangles,*_=delaunay_2d_cdt(ps,edges,[],0,.00001,False)
 # CDT may average coincident constraint coordinates within its epsilon.
 # Snap only this known shared border back to the exact accepted ribbon row.
 for p in join_row:
  closest=min(range(len(coords)),key=lambda i:(coords[i]-Vector((p.x,p.y))).length_squared)
  assert (coords[closest]-Vector((p.x,p.y))).length<.0001,(label,old.name,'mouth vertex missing after CDT')
  coords[closest]=Vector((p.x,p.y))
 valid=[]
 for f in triangles:
  c=sum((coords[i]for i in f),Vector((0,0)))/len(f)
  hit,*_=source_bvh.ray_cast(Vector((c.x,c.y,1)),Vector((0,0,-1)),2)
  if hit is not None:valid.append(tuple(f))
 assert valid,'Empty lake union'
 used=sorted({i for f in valid for i in f})
 for p in join_row:
  closest=min(used,key=lambda i:(coords[i]-Vector((p.x,p.y))).length_squared)
  assert (coords[closest]-Vector((p.x,p.y))).length<.0001,(label,old.name,'used mouth vertex missing after CDT')
  coords[closest]=Vector((p.x,p.y))
 # Float32 points along an oblique straight mouth can deviate by micrometres.
 # CDT occasionally triangulates these collinear border points into slivers.
 # They have no surface area in the authored cross-section and would give the
 # welded mouth three incident faces. Remove ONLY such known-mouth slivers.
 mouth_keys={key(p)for p in join_row}
 def mouth_sliver(f):
  if not all(key(coords[i])in mouth_keys for i in f):return False
  a,b,c=[coords[i]for i in f[:3]]
  return abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))<.0001
 valid=[f for f in valid if not mouth_sliver(f)]
 used=sorted({i for f in valid for i in f})
 remap={old:i for i,old in enumerate(used)}
 verts=[Vector((coords[i].x,coords[i].y,z))for i in used];faces=[tuple(remap[i]for i in f)for f in valid]
 centreline=[centroid(row)for v,row in toprows]
 def patch_hit(p):
  q=Vector((p.x,p.y,0));hit,n,face,d=patch_bvh.ray_cast(q+Vector((0,0,1)),Vector((0,0,-1)),2)
  if hit is None:
   nearest,n,face,d=patch_bvh.find_nearest(q)
   if nearest is None or d>.0001:return None
   hit=nearest
  return hit,face
 def mouth_point(p):
  q=Vector((p.x,p.y,0));best=None
  for j,(a,b)in enumerate(zip(join_row,join_row[1:])):
   a=Vector((a.x,a.y,0));b=Vector((b.x,b.y,0));delta=b-a
   t=min(1,max(0,(q-a).dot(delta)/delta.length_squared));distance=(q-a.lerp(b,t)).length
   if best is None or distance<best[0]:best=(distance,(j+t)/cols)
  return best
 def flow_uv(p):
  distance,u=mouth_point(p)
  if distance<.0001:return(u,join_v)
  if key(p)in original_uv:return original_uv[key(p)]
  patch=patch_hit(p)
  if patch is not None:
   hit,face=patch;ids=patch_triangles[face];value=barycentric_transform(hit,*[patch_ps[i]for i in ids],*[patch_uv[i]for i in ids]);return(value.x,value.y)
  best=None
  for i,(a,b)in enumerate(zip(centreline,centreline[1:])):
   delta=b-a;t=(p-a).dot(delta)/delta.length_squared
   # Extrapolate the initial reach backwards so broad upstream lakes have V<0.
   t=min(1,max(t,-100 if i==0 else 0));c=a.lerp(b,t);distance=(p-c).length_squared
   if best is None or distance<best[0]:best=(distance,i,t,c)
  _,i,t,c=best;v=toprows[i][0]+(toprows[i+1][0]-toprows[i][0])*t
  across=toprows[i][1][-1]-toprows[i][1][0];u=.5+(p-c).dot(across)/across.length_squared
  return(u,v)
 uv0=[];uv1=[];alpha=[];pool_vertices=0
 for p in verts:
  u,v=flow_uv(p);patch=patch_hit(p)
  mask=0
  if patch is not None:
   t=min(1,max(0,v/join_v));mask=t*t*(3-2*t)
  distance,mouth_u=mouth_point(p)
  if distance<.0001:mask=1;u,v=mouth_u,join_v
  # A clamped nearest-centreline V is a lake coordinate, not evidence that a
  # point belongs to the physical mouth. Only geometry may grant full mask1.
  if mask>.999 and abs(v-join_v)<.0001:assert distance<.0001,(label,old.name,'false full-mask mouth',list(p),u,v,distance)
  if mask<.0001:pool_vertices+=1
  uv0.append((u,v));uv1.append((mask,1));alpha.append(1)
 # Exact shared vertices at the mouth. Every later accepted body row retains
 # its original world positions, UV and downstream alpha fade.
 position_ids={key(p)+(round(p.z,5),):i for i,p in enumerate(verts)}
 body_indices=[];old_normals=normal_map(old);old_alphas=alpha_map(old);desired_normals={}
 for ri,(v,row)in enumerate(bodyrows):
  indices=[]
  for j,p in enumerate(row):
   k=key(p)+(round(p.z,5),)
   if ri==0:
    if k not in position_ids:raise RuntimeError(('CDT did not retain mouth vertex',label,old.name,j,list(p)))
    index=position_ids[k];uv0[index]=(j/cols,v);uv1[index]=(1,1)
   else:
    index=len(verts);verts.append(p.copy());uv0.append((j/cols,v));uv1.append((1,1))
    alpha.append(old_alphas[k])
   if k in old_normals:desired_normals[index]=old_normals[k]
   indices.append(index)
  body_indices.append(indices)
 body_flip=(bodyrows[0][1][1]-bodyrows[0][1][0]).cross(bodyrows[1][1][0]-bodyrows[0][1][0]).z<0
 for a,b in zip(body_indices,body_indices[1:]):
  for j in range(cols):
   f=(a[j],a[j+1],b[j+1],b[j]);faces.append(tuple(reversed(f))if body_flip else f)
 # Exact coincident CDT vertices can survive as two zero-area lake triangles.
 # Unity strips these deterministically; remove them in source as well. No
 # epsilon merge, nonzero sliver deletion or approved-body face edit is allowed.
 canonical={};vertex_map={}
 for i,p in enumerate(verts):
  attributes=struct.pack('<7f',*uv0[i],*uv1[i],alpha[i],0,0)
  k=tuple(p)+(attributes,)
  vertex_map[i]=canonical.setdefault(k,i)
 cleaned=[];zero_faces=[]
 for i,face in enumerate(faces):
  remapped=tuple(vertex_map[j]for j in face);a,b,c=[verts[j]for j in remapped[:3]]
  if len(set(remapped))<3 or(b-a).cross(c-a).length==0:
   assert all(uv1[j][0]<.999 for j in face),(label,old.name,'zero face in approved body')
   zero_faces.append(i)
  else:cleaned.append(remapped)
 planar_count=len(valid)-sum(i<len(valid)for i in zero_faces)
 if zero_faces:
  kept=sorted({i for f in cleaned for i in f});compact={old:i for i,old in enumerate(kept)}
  faces=[tuple(compact[i]for i in f)for f in cleaned]
  body_indices=[[compact[vertex_map[i]]for i in row]for row in body_indices]
  desired_normals={compact[vertex_map[i]]:n for i,n in desired_normals.items()}
  plane_vertex_count=sum(i<len(used)for i in kept)
  verts=[verts[i]for i in kept];uv0=[uv0[i]for i in kept];uv1=[uv1[i]for i in kept];alpha=[alpha[i]for i in kept]
  pool_vertices=sum(mask<.0001 for mask,unused in uv1)
 else:plane_vertex_count=len(used)
 # Ensure all faces follow the same real upward/outward side.
 for i,f in enumerate(faces):
  a,b,c=[verts[k]for k in f[:3]]
  if i<planar_count and(b-a).cross(c-a).z<0:faces[i]=tuple(reversed(f))
 data=bpy.data.meshes.new(old.name+'_UnifiedLakeAndFall');data.from_pydata(verts,[],faces);data.update();data.materials.append(old.data.materials[0])
 for name,values in [('UVMap',uv0),('LakeRegion',uv1)]:
  layer=data.uv_layers.new(name=name);layer.data.foreach_set('uv',[v for l in data.loops for v in values[l.vertex_index]])
 color=data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');color.data.foreach_set('color',[c for l in data.loops for c in(.15,.55,.57,alpha[l.vertex_index])])
 for p in data.polygons:p.use_smooth=True
 data.normals_split_custom_set([tuple(desired_normals.get(l.vertex_index,Vector((0,0,1))))for l in data.loops])
 name=old.name;parent=old.parent;properties={k:old[k]for k in old.keys()};bpy.data.objects.remove(old,do_unlink=True)
 ob=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(ob);ob.parent=parent;ob.matrix_world=Matrix.Identity(4)
 for k,v in properties.items():ob[k]=v
 ob['unified_lake_flow']=True
 if'continuous_upstream_blend_metres'in ob:del ob['continuous_upstream_blend_metres']
 edges=collections.Counter();adj=collections.defaultdict(set)
 for f in data.polygons:
  ids=list(f.vertices)
  for a,b in zip(ids,ids[1:]+ids[:1]):edges[tuple(sorted((a,b)))]+=1;adj[a].add(b);adj[b].add(a)
 unseen=set(adj);components=0
 while unseen:
  components+=1;stack=[unseen.pop()]
  while stack:
   for q in adj[stack.pop()]:
    if q in unseen:unseen.remove(q);stack.append(q)
 assert components==1,(label,name,'disconnected union',components)
 if any(n>2 for n in edges.values()):
  invalid=[dict(indices=list(e),count=n,positions=[list(verts[i])for i in e],uv=[uv0[i]for i in e],
   faces=[dict(index=i,kind='plane'if i<len(valid)else'body',coords=[list(verts[j])for j in f],uvs=[uv0[j]for j in f])for i,f in enumerate(faces)if all(j in f for j in e)])for e,n in edges.items()if n>2]
  (HERE/(label+'-'+name+'-nonmanifold.json')).write_text(json.dumps(invalid,indent=2))
  raise RuntimeError((label,name,'nonmanifold',len(invalid),invalid[:2]))
 for a,b in zip(body_indices[0],body_indices[0][1:]):assert edges[tuple(sorted((a,b)))]==2,(label,name,'unwelded mouth',a,b)
 def unity(p):return[p.x,p.z,-p.y]
 record=dict(id=info['id'],node=name,material=data.materials[0].name,vertices=len(verts),triangles=sum(len(f)-2 for f in faces),components=components,
  lake_vertices=pool_vertices,lake_uv_v_min=min(v for(u,v),(mask,_)in zip(uv0,uv1)if mask<.001),lake_bounds=bounds(verts[:plane_vertex_count]),exact_zero_lake_faces_removed=len(zero_faces),
  uv_v=[min(v for u,v in uv0),rr[-1][0]],metric_start_v=join_v,metric_start_unity=unity(centroid(join_row)),
  unity_head=unity(centroid(rr[0][1])),unity_bottom=unity(centroid(rr[-1][1])),lake_level=z,
  alpha='1 on all lake/outlet vertices; smooth fade only in final3m of falling body',
  body_rows=len(bodyrows),columns=cols,body_vertex_positions_and_uv_preserved=True,body_source_start_v=join_v,
  mouth_vertices=cols+1,mouth_edges_shared_twice=True,old_body_normals_copied=True)
 return record

def run(label):
 cfg=CONFIG[label];src=cfg['folder'];out=HERE/'STAGING'/label;out.mkdir(parents=True,exist_ok=True)
 input_fbx=src/cfg['fbx'] if label!='Amber'else src/'STAGING'/cfg['fbx'];assert sha(input_fbx)==cfg['sha']
 bpy.ops.wm.open_mainfile(filepath=str(src/cfg['blend']));bpy.context.view_layer.update()
 meta=json.loads((src/('continuous_sheet_handoff.json'if label=='Amber'else'handoff.json')).read_text())
 infos=meta['sheets'];flows=[bpy.data.objects[x['node']]for x in infos]
 head_heights={f.name:centroid(rings(f)[0][1]).z for f in flows}
 pool_objects=[ob for ob in bpy.context.scene.objects if ob.type=='MESH'and any(m and m.name==cfg['pool']for m in ob.data.materials)]
 assigned={info['node']:[]for info in infos};consumed=[]
 for pool in pool_objects:
  groups=collections.defaultdict(list)
  for face in pool.data.polygons:
   points=[pool.matrix_world@pool.data.vertices[i].co for i in face.vertices];z=sum(p.z for p in points)/len(points)
   candidate=min(flows,key=lambda f:abs(head_heights[f.name]-z))
   if abs(head_heights[candidate.name]-z)<.15:groups[candidate.name].append(points)
   else:groups[None].append(points)
  if None in groups:
   assert len(groups)==1,(label,pool.name,'mixed unrelated pool requires separate remainder')
   continue
  consumed.append(pool.name)
  for name,faces in groups.items():assigned[name].extend(faces)
 affected=set(consumed)|{f.name for f in flows};before={o.name:signature(o)for o in bpy.context.scene.objects if o.name not in affected}
 for pool in pool_objects:
  if pool.name in consumed:bpy.data.objects.remove(pool,do_unlink=True)
 records=[]
 for flow,info in zip(flows,infos):
  assert assigned[flow.name],(label,flow.name,'missing lake')
  records.append(build_one(flow,info,assigned[flow.name],label))
 bpy.context.view_layer.update();after={o.name:signature(o)for o in bpy.context.scene.objects if o.name not in affected};assert before==after,'Non-water source changed'
 bpy.ops.object.select_all(action='DESELECT')
 for ob in bpy.context.scene.objects:
  if ob.type in('MESH','EMPTY'):ob.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(out/cfg['fbx']),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,
  apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=cfg['triangles'],mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
 bpy.ops.wm.save_as_mainfile(filepath=str(out/(label.lower()+'_unified_lake_waterfalls.blend')))
 manifest=(src/'STAGING'if label=='Amber'else src)/'RooftopMotion.json'
 if manifest.exists():(out/'RooftopMotion.json').write_bytes(manifest.read_bytes())
 historical_absent=['RA_Coral_Water_+01_-03_01']if label=='Coral'else[]
 result=dict(level=label,input_model=str(input_fbx),input_sha256=cfg['sha'],output_sha256=sha(out/cfg['fbx']),output_model=str(out/cfg['fbx']),output_bytes=(out/cfg['fbx']).stat().st_size,
  consumed_pool_nodes=consumed+historical_absent,already_absent_pool_nodes=historical_absent,affected_nodes=sorted(affected),untouched_objects=len(before),untouched_before=before,untouched_after=after,sheets=records)
 (out/'handoff.json').write_text(json.dumps(result,indent=2));assert sha(input_fbx)==cfg['sha']
 print('UNIFIED_LAKE_SOURCE_READY',json.dumps({k:v for k,v in result.items()if not k.startswith('untouched_')}),flush=True)

requested=sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else list(CONFIG)
for label in requested:run(label)
