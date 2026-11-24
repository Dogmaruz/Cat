import bpy,pathlib,sys,json,numpy as np,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));sys.path.insert(0,str(HERE.parent));import polish_islets as p;import clean_and_verify as audit
report=audit.verify({});deps=bpy.context.evaluated_depsgraph_get();anchors=[]
for s,u,v,name in [(420,-4.5,3.4,'inherited meadow tree A'),(420,4.6,3.4,'inherited meadow tree B'),(478,.3,3.2,'inherited copper cottage')]:
 spec=next(x for x in p.SPECS if x['distance']==s);q=p.world(spec,u,v);oldbase=p.grade(spec,u,v)-(1.0 if s==420 else .86)
 core=bpy.data.objects['RA_Polished_'+str(s)];bvh=BVHTree.FromObject(core,deps);local=core.matrix_world.inverted()@(q+Vector((0,0,40)));hit,normal,_,_=bvh.ray_cast(local,core.matrix_world.to_3x3().inverted()@Vector((0,0,-1)),80);assert hit is not None
 hitworld=core.matrix_world@hit;gap=oldbase-hitworld.z;anchors.append(dict(name=name,footpoint=[q.x,q.y,oldbase],new_cap_z=hitworld.z,gap=gap));assert -.10<gap<.10,(name,gap)
main=bpy.data.objects['RA_Closed_Mainland'];final=np.array([tuple(main.matrix_world@v.co) for v in main.data.vertices],np.float64);polyids=[tuple(poly.vertices) for poly in main.data.polygons]
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(HERE/'pre_polish_checkpoint.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update();main=bpy.data.objects['RA_Closed_Mainland'];before=np.array([tuple(main.matrix_world@v.co) for v in main.data.vertices],np.float64)
assert [tuple(poly.vertices) for poly in main.data.polygons]==polyids;error=float(np.max(np.linalg.norm(final-before,axis=1)));assert error<.0001,error
result=dict(final_asset_verification=report,inherited_props_grounding=anchors,mainland_polygon_indices_unchanged=True,mainland_world_vertex_max_error=error,pre_polish_checkpoint_sha256=hashlib.sha256((HERE/'pre_polish_checkpoint.fbx').read_bytes()).hexdigest())
(HERE/'verification.json').write_text(json.dumps(result,indent=2));print('POLISH_FINAL_VERIFIED',json.dumps({k:v for k,v in result.items() if k!='final_asset_verification'}),flush=True)
