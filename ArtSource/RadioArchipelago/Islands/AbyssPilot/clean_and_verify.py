"""Vectorized decoration cleanup, final FBX channels, anchors, shell and corridor proof."""
import bpy,bmesh,pathlib,sys,json,collections,math,numpy as np,hashlib
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_abyss as a
PATH=a.OUT/'emerald_abyss_island.fbx'
def read():
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(PATH),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update()
def clean():
 read();removed=collections.Counter()
 for o in list(bpy.context.scene.objects):
  if o.type!='MESH':continue
  if o.get('structural_closed_shell'):continue
  m=o.data;n=len(m.vertices);f=len(m.polygons);v=np.empty(n*3,np.float64);m.vertices.foreach_get('co',v);v=v.reshape(-1,3)
  ids=np.empty(len(m.loops),np.int32);m.loops.foreach_get('vertex_index',ids);ids=ids.reshape(-1,3);norms=np.empty(len(m.loops)*3,np.float64);m.corner_normals.foreach_get('vector',norms);norms=norms.reshape(-1,3)
  e1=v[ids[:,1]]-v[ids[:,0]];e2=v[ids[:,2]]-v[ids[:,0]];e3=e2-e1;cross=np.cross(e1,e2);area2=np.linalg.norm(cross,axis=1);long=np.maximum.reduce((np.sum(e1*e1,axis=1),np.sum(e2*e2,axis=1),np.sum(e3*e3,axis=1)))
  invalid=np.any(np.abs(np.linalg.norm(norms,axis=1).reshape(-1,3)-1)>.01,axis=1);category=o.get('unity_material_bucket') or m.materials[0].name.removeprefix('RA_Emerald_').split('.')[0]
  bad=np.zeros(len(m.polygons),bool)
  if category in ('Architecture','Metal','Rocks','Wood'):bad=invalid|(area2<1e-8)|(area2/np.maximum(long,1e-30)<.0001)
  if not np.any(bad):continue
  keep=~bad;removed[category]+=int(np.sum(bad));color=m.color_attributes.get('Color');cv=np.empty(len(color.data)*4,np.float32);color.data.foreach_get('color',cv);cv=cv.reshape(-1,3,4)[keep].reshape(-1);uv=[]
  for layer in m.uv_layers:
   values=np.empty(len(layer.data)*2,np.float32);layer.data.foreach_get('uv',values);uv.append((layer.name,values.reshape(-1,3,2)[keep].reshape(-1)))
  soft=np.empty(f,np.bool_);m.polygons.foreach_get('use_smooth',soft);new=bpy.data.meshes.new(m.name+'_valid');new.from_pydata(v,[],ids[keep]);new.update()
  for mat in m.materials:new.materials.append(mat)
  attr=new.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');attr.data.foreach_set('color',cv)
  for name,values in uv:layer=new.uv_layers.new(name=name);layer.data.foreach_set('uv',values)
  new.polygons.foreach_set('use_smooth',soft[keep]);normal=norms.reshape(-1,3,3)[keep].reshape(-1,3);normal/=np.maximum(np.linalg.norm(normal,axis=1)[:,None],1e-20);new.normals_split_custom_set(normal);o.data=new
 a.r.export_fbx(PATH,list(bpy.context.scene.objects));print('ABYSS_DECOR_CLEANED',dict(removed),flush=True)
 return dict(removed)
def verify(removed):
 read();objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];counts=collections.Counter();normbad=0;maxerr=0;colors=0;struct=[]
 for o in objects:
  m=o.data;cat=o.get('unity_material_bucket') or m.materials[0].name.removeprefix('RA_Emerald_').split('.')[0]
  if cat=='Structural':
   for p in m.polygons:counts[m.materials[p.material_index].name.removeprefix('RA_Emerald_').split('.')[0]]+=len(p.vertices)-2
  else:counts[cat]+=sum(len(p.vertices)-2 for p in m.polygons)
  assert cat not in ('Road','Bridge','StepTreads');assert len(m.vertices)<=58000;assert m.color_attributes.get('Color');colors+=1
  coords=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',coords);assert np.isfinite(coords).all()
  val=np.empty(len(m.loops)*3,np.float32);m.corner_normals.foreach_get('vector',val);lengths=np.linalg.norm(val.reshape(-1,3),axis=1);invalid_count=int(np.sum(np.abs(lengths-1)>.001));normbad+=invalid_count;maxerr=max(maxerr,float(np.max(np.abs(lengths-1))))
  if invalid_count:print('ABYSS_INVALID_NORMALS',o.name,cat,invalid_count,flush=True)
  if cat in ('Terrain','Cliffs') or o.get('structural_closed_shell'):struct.append(o)
 # Continue the independent shell/corridor audit even if a decoration needs repair.
 assert len(objects)<=400 and sum(counts.values())<5000000
 anchors=[]
 for name,pos in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
  o=bpy.data.objects.get(name);assert o;error=math.dist(o.matrix_world.translation,pos);assert error<.001;anchors.append(dict(name=name,error=error))
 per_shell=[dict(name=o.name,**a.topology(o.data)) for o in struct]
 shell=dict(vertices=sum(s['vertices'] for s in per_shell),faces=sum(s['faces'] for s in per_shell),boundary_edges=sum(s['boundary_edges'] for s in per_shell),nonmanifold_edges=sum(s['nonmanifold_edges'] for s in per_shell),per_object=per_shell,method='Exact serialized polygon indices; no collapse or proximity weld')
 print('ABYSS_STRUCTURAL_SHELL',shell,flush=True)
 deps=bpy.context.evaluated_depsgraph_get();overhead=[];intersections=[];minimum=1e9;open_samples=0;surface_samples=0;caps=[];sideheight=[]
 for start,end in ((125,185),(402,501)):
  for s in np.arange(start,end+.01,.5):
   p,n=a.road.sample(float(s));height=a.play(float(s))
   for lane in (-3.5,-2,0,2,3.5):
    q=p+n*lane;q.z=height+1
    hit,location,normal,idx,obj,mat=bpy.context.scene.ray_cast(deps,q,Vector((0,0,1)),distance=35)
    if hit:overhead.append(dict(distance=float(s),lane=lane,height=height,object=obj.name,hit=list(location)))
    hit,location,normal,idx,obj,mat=bpy.context.scene.ray_cast(deps,q,Vector((0,0,-1)),distance=85)
    if hit:
     delta=q.z-location.z;minimum=min(minimum,delta);surface_samples+=1
     if delta<1.5:intersections.append(dict(distance=float(s),lane=lane,clearance=delta,object=obj.name,hit=list(location)))
     if (obj.get('unity_material_bucket')=='Terrain' or obj.get('structural_closed_shell')) and normal.z>.25 and location.z>height-2:caps.append(dict(distance=float(s),lane=lane,cap_below_profile=height-location.z))
    else:open_samples+=1
 # Centerline forward rays use the actual elevation curve and detect walls/canopies.
 for start,end in ((125,185),(402,501)):
  for s in np.arange(start,end-1,.5):
   p,n=a.road.sample(float(s));q,nn=a.road.sample(float(s+1));p.z=a.play(float(s))+1;q.z=a.play(float(s+1))+1
   for lane in(-2,0,2):
    origin=p+n*lane;target=q+nn*lane;direction=target-origin
    hit,loc,normal,idx,obj,mat=bpy.context.scene.ray_cast(deps,origin,direction.normalized(),distance=direction.length)
    if hit:intersections.append(dict(distance=float(s),lane=lane,forward_hit=obj.name,hit=list(loc)))
 report0=json.loads((HERE/'abyss_report.json').read_text());frozen=all(a.sha(pathlib.Path(p))==h for p,h in report0['frozen_inputs'].items())
 report=dict(pass_geometry=normbad==0 and len(objects)<=400,pass_flight_corridor=len(overhead)==0 and len(intersections)==0,mesh_batches=len(objects),triangles=sum(counts.values()),material_buckets=dict(counts),colored_meshes=colors,invalid_corner_normals=normbad,max_unit_normal_error=maxerr,anchors=anchors,structural_shell=shell,
  removed_decorative_sliver_faces=removed,corridor=dict(lane_offsets=[-3.5,-2,0,2,3.5],sample_spacing=.5,tile_elevation=1,minimum_downward_surface_clearance=minimum,open_sky_below_samples=open_samples,surface_below_samples=surface_samples,overhead_hits=overhead,intersection_hits=intersections,min_cap_below_profile=min([x['cap_below_profile'] for x in caps],default=None)),
  frozen_inputs_unchanged=frozen,fbx_sha256=a.sha(PATH),fbx_bytes=PATH.stat().st_size,route_and_profiles_byte_identical=all((a.OUT/name).read_bytes()==source.read_bytes() for name,source in [('emerald_abyss_route.json',a.r.OUT/'emerald_relief_route.json'),('ground_elevation.json',a.r.OUT/'ground_elevation.json'),('play_elevation.json',a.PLAY_PATH)]))
 (HERE/'verification.json').write_text(json.dumps(report,indent=2));print('ABYSS_VERIFIED',json.dumps({k:v for k,v in report.items() if k!='corridor'}),flush=True);print('ABYSS_CORRIDOR',len(overhead),len(intersections),minimum,open_samples,flush=True)
 assert normbad==0,(normbad,maxerr)
 assert report['pass_flight_corridor'],str(report['corridor'])[:1000]
 assert shell['boundary_edges']==0 and shell['nonmanifold_edges']==0,shell
 return report
if __name__=='__main__':
 if '--clean-only' in sys.argv:clean()
 else:verify({} if '--verify-only' in sys.argv else clean())
