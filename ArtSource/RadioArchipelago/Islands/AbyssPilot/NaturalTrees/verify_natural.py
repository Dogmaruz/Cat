import bpy,pathlib,sys,json,hashlib,math,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_natural as n
a=n.a;PATH=n.PATH;bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(PATH),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update()
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];bad=[];top=[];leaf=[];counts={};bvhs=[];maxverts=0
for ob in meshes:
 m=ob.data;cat=ob.get('unity_material_bucket');counts[cat]=counts.get(cat,0)+sum(len(p.vertices)-2 for p in m.polygons);maxverts=max(maxverts,len(m.vertices));assert len(m.vertices)<=58000,(ob.name,len(m.vertices));values=np.empty(len(m.loops)*3,np.float32);m.corner_normals.foreach_get('vector',values);values=values.reshape(-1,3);invalid=int(np.sum(~np.isfinite(values).all(axis=1)|(np.abs(np.linalg.norm(values,axis=1)-1)>.001)))
 if invalid:bad.append(dict(mesh=ob.name,corners=invalid))
 color=m.color_attributes.get('Color');assert color,ob.name;co=np.empty(len(color.data)*4,np.float32);color.data.foreach_get('color',co);co=co.reshape(-1,4);assert np.isfinite(co).all()
 if cat=='TreeLeaves':
  assert m.uv_layers.active,ob.name;uv=np.empty(len(m.loops)*2,np.float32);m.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,2);assert np.isfinite(uv).all();assert uv.min()>-.0001 and uv.max()<1.0001;error=float(np.max(np.abs(co[:,3]-uv[:,1])));assert error<.00001,(ob.name,error);leaf.append(dict(mesh=ob.name,uv_min=float(uv.min()),uv_max=float(uv.max()),alpha_min=float(co[:,3].min()),alpha_max=float(co[:,3].max()),alpha_vs_leaf_v_error=error))
 if cat=='TreeBark' or ob.get('structural_closed_shell') or cat=='Cliffs':
  stat=a.topology(m);top.append(dict(mesh=ob.name,**stat));assert stat['boundary_edges']==0 and stat['nonmanifold_edges']==0,(ob.name,stat)
 if ob.get('natural_tree_pass'):bvhs.append((ob,BVHTree.FromObject(ob,bpy.context.evaluated_depsgraph_get())))
hits=[]
for s in np.arange(0,a.road.length,.5):
 p,normal=a.road.sample(float(s))
 for lane in(-3.5,-2,0,2,3.5):
  q=p+normal*lane;q.z=a.play(float(s))+1.001
  for ob,bvh in bvhs:
   inverse=ob.matrix_world.inverted();hit,nn,i,d=bvh.ray_cast(inverse@q,inverse.to_3x3()@Vector((0,0,1)),45)
   if hit is not None:hits.append(dict(distance=float(s),lane=lane,object=ob.name,hit=list(ob.matrix_world@hit)));break
anchors=[]
for name,pos in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
 ob=bpy.data.objects[name];error=math.dist(ob.matrix_world.translation,pos);assert error<.001;anchors.append(dict(name=name,error=error))
src=json.loads((HERE/'natural_report.json').read_text());report=dict(pass_normals=not bad,pass_structural_and_bark_topology=True,pass_leaf_uv_alpha=bool(leaf),pass_new_forest_flight_clearance=not hits,invalid_normals=bad,closed_topology=top,leaf_channels=leaf,above_tile_hits=hits,mesh_batches=len(meshes),max_vertices_per_mesh=maxverts,triangle_budget=sum(counts.values()),triangle_budget_by_bucket=counts,anchors=anchors,fbx_sha256=n.sha(PATH),fbx_bytes=PATH.stat().st_size,checkpoint_sha256=n.sha(HERE/'pre_natural_trees_checkpoint.fbx'),nonforest_mesh_hashes_unchanged_in_process=src['nonforest_hashes_unchanged'],nonforest_meshes_preserved=len(src['unchanged_nonforest_meshes']),trees_replaced=src['trees_replaced'],ground_anchors_max_gap=max(p['ground_gap'] for p in src['new_tree_placements']))
# Compare the actual FBX roundtrip against the untouched checkpoint, not source alone.
def saved(o):
 m=o.data;co=np.empty(len(m.vertices)*3,np.float64);m.vertices.foreach_get('co',co);co=co.reshape(-1,3);world=np.asarray(o.matrix_world);co=co@world[:3,:3].T+world[:3,3];indices=np.empty(len(m.loops),np.int32);m.loops.foreach_get('vertex_index',indices);c=m.color_attributes.get('Color');col=np.empty(len(c.data)*4,np.float32);c.data.foreach_get('color',col);return co,indices,col
final={name:saved(bpy.data.objects[name]) for name in src['unchanged_nonforest_meshes']};bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(HERE/'pre_natural_trees_checkpoint.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update();preserved=[]
for name,(co,indices,colors) in final.items():
 old,oldindices,oldcolors=saved(bpy.data.objects[name]);assert np.array_equal(indices,oldindices),(name,'polygonindices changed');error=float(np.max(np.linalg.norm(co-old,axis=1)));colorerror=float(np.max(np.abs(colors-oldcolors)));assert error<.0001,(name,error);assert colorerror<.00001,(name,colorerror);preserved.append(dict(mesh=name,world_coordinate_max_error=error,corner_rgb_alpha_max_error=colorerror,polygon_indices_unchanged=True))
report['actual_fbx_nonforest_preservation']=preserved;report['pass_actual_fbx_nonforest_preservation']=True
(HERE/'verification.json').write_text(json.dumps(report,indent=2));print('NATURAL_VERIFIED',json.dumps({k:v for k,v in report.items() if k not in('closed_topology','leaf_channels','above_tile_hits')}),flush=True);print('FOREST_HITS',hits[:10],flush=True);assert not bad;assert not hits,hits[:10];assert len(meshes)<=350;assert sum(counts.values())<=7000000
