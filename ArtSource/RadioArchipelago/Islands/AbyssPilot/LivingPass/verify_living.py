import bpy,bmesh,pathlib,sys,json,hashlib,math,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_living as a
PATH=a.PATH;bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(PATH),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update()
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];bad=[];topology=[];counts={};alphas=[];new_bvhs=[];grass_points=[]
for o in objects:
 m=o.data;cat=o.get('unity_material_bucket');counts[cat]=counts.get(cat,0)+sum(len(f.vertices)-2 for f in m.polygons)
 coords=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',coords);assert np.isfinite(coords).all();assert len(m.vertices)<=58000
 values=np.empty(len(m.loops)*3,np.float32);m.corner_normals.foreach_get('vector',values);lengths=np.linalg.norm(values.reshape(-1,3),axis=1);n=int(np.sum(np.abs(lengths-1)>.001))
 if n:bad.append(dict(mesh=o.name,corners=n))
 attr=m.color_attributes.get('Color');assert attr;cv=np.empty(len(attr.data)*4,np.float32);attr.data.foreach_get('color',cv);cv=cv.reshape(-1,4);assert np.isfinite(cv).all()
 if cat=='Grass':alphas.append(dict(mesh=o.name,min=float(cv[:,3].min()),max=float(cv[:,3].max()),root_corners=int(np.sum(cv[:,3]<.001)),tip_corners=int(np.sum(cv[:,3]>.999)),mid_corners=int(np.sum(np.abs(cv[:,3]-.55)<.002))))
 if o.get('structural_closed_shell') or cat=='Cliffs' or o.get('living_pass'):
  stat=a.a.topology(m);topology.append(dict(mesh=o.name,**stat))
 if o.get('living_pass'):new_bvhs.append((o,BVHTree.FromObject(o,bpy.context.evaluated_depsgraph_get())))
alpha_pass=bool(alphas) and all(x['min']==0 and x['max']==1 and x['root_corners']>0 and x['tip_corners']>0 for x in alphas)
shell_pass=all(x['boundary_edges']==0 and x['nonmanifold_edges']==0 for x in topology)
hits=[]
for s in np.arange(0,a.a.road.length,.5):
 p,n=a.a.road.sample(float(s))
 for lane in(-3.5,-2,0,2,3.5):
  q=p+n*lane;q.z=a.a.play(float(s))+1.001
  for obj,bvh in new_bvhs:
   inv=obj.matrix_world.inverted();direction=inv.to_3x3()@Vector((0,0,1));hit,normal,index,distance=bvh.ray_cast(inv@q,direction,25)
   if hit is not None:hits.append(dict(distance=float(s),lane=lane,object=obj.name,hit=list(obj.matrix_world@hit)));break
main=bpy.data.objects['RA_Closed_Mainland'];main_hash=a.hashmesh(main);source=json.loads((HERE/'living_report.json').read_text());anchors=[]
for name,pos in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
 obj=bpy.data.objects.get(name);assert obj;error=math.dist(obj.matrix_world.translation,pos);anchors.append(dict(name=name,error=error));assert error<.001
report=dict(pass_normals=not bad,pass_shell_topology=shell_pass,pass_grass_alpha=alpha_pass,pass_added_geometry_flight_clearance=not hits,mesh_batches=len(objects),triangles=sum(counts.values()),triangle_budget_by_bucket=counts,invalid_normals=bad,closed_shells=topology,grass_alpha=alphas,above_tile_hits=hits,mainland_mesh_before=source['mainland_mesh_hash_before'],mainland_mesh_in_process=source['mainland_mesh_hash_after'],mainland_indices_and_geometry_unchanged_in_process=source['mainland_unchanged'],anchors=anchors,fbx_sha256=hashlib.sha256(PATH.read_bytes()).hexdigest(),fbx_bytes=PATH.stat().st_size,pre_living_checkpoint_sha256=hashlib.sha256((HERE/'pre_living_checkpoint.fbx').read_bytes()).hexdigest())
(HERE/'verification.json').write_text(json.dumps(report,indent=2));print('LIVING_VERIFIED',json.dumps({k:v for k,v in report.items() if k not in('closed_shells','grass_alpha','above_tile_hits')}),flush=True);print('LIVING_ABOVE_TILE_HITS',len(hits),hits[:5],flush=True)
assert not bad,bad;assert shell_pass;assert alpha_pass;assert not hits,hits[:10];assert len(objects)<=350 and sum(counts.values())<=7000000
