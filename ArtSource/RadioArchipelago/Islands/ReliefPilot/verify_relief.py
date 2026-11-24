import bpy,json,pathlib,math,sys,hashlib,collections
import numpy as np
HERE=pathlib.Path(__file__).resolve().parent
OUT=HERE.parents[3]/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/EmeraldRelief'
report=json.loads((HERE/'relief_report.json').read_text())
checks={};total=0
for filename in ('emerald_relief_island.fbx','emerald_relief_cobbles.fbx'):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(OUT/filename),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
 bpy.context.view_layer.update();meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];triangles=0;zeros=0;nonfinite=0;colored=0;max_normal_error=0
 for o in meshes:
  m=o.data;triangles+=len(m.polygons);assert all(len(p.vertices)==3 for p in m.polygons)
  coords=np.empty(len(m.vertices)*3,dtype=np.float32);m.vertices.foreach_get('co',coords);assert np.isfinite(coords).all()
  attr=m.color_attributes.get('Color');assert attr is not None;colored+=1
  vals=np.empty(len(attr.data)*4,dtype=np.float32);attr.data.foreach_get('color',vals);assert np.isfinite(vals).all()
  normal=np.empty(len(m.corner_normals)*3,dtype=np.float32);m.corner_normals.foreach_get('vector',normal);normal=normal.reshape(-1,3)
  assert np.isfinite(normal).all();lengths=np.linalg.norm(normal,axis=1);zeros+=int(np.sum(lengths<1e-5));max_normal_error=max(max_normal_error,float(np.max(np.abs(lengths[lengths>1e-5]-1))))
 anchors=[]
 for name,pos in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
  o=bpy.data.objects.get(name);assert o is not None,name;error=math.dist(o.matrix_world.translation,pos);assert error<.001;anchors.append(dict(name=name,error=error))
 checks[filename]=dict(meshes=len(meshes),triangles=triangles,colored_meshes=colored,zero_corner_normals=zeros,max_unit_normal_error=max_normal_error,anchors=anchors,sha256=hashlib.sha256((OUT/filename).read_bytes()).hexdigest(),bytes=(OUT/filename).stat().st_size)
 total+=triangles
profile=json.loads((OUT/'ground_elevation.json').read_text());keys=profile['keys'];assert profile['closed'] is True and keys[0]['distance']==0
assert keys[-1]['distance']==profile['length'] and keys[-1]['height']==keys[0]['height'];assert all(b['distance']>a['distance'] for a,b in zip(keys,keys[1:]))
original=OUT.parent/'Emerald/emerald_route.json';assert (OUT/'emerald_relief_route.json').read_bytes()==original.read_bytes()
assert all(hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()==value for p,value in report['frozen_inputs'].items())
normal_pass=all(c['zero_corner_normals']==0 and c['max_unit_normal_error']<.001 for c in checks.values())
result=dict(pass_roundtrip=True,pass_all_corner_normals=normal_pass,total_triangles=total,files=checks,profile_keys=len(keys),profile_seam_continuous=True,original_xz_route_byte_identical=True,frozen_inputs_unchanged=True)
(HERE/'verification.json').write_text(json.dumps(result,indent=2));print('RELIEF_VERIFIED',json.dumps(result),flush=True)
assert normal_pass,'Serialized corner normals still contain invalid values'
