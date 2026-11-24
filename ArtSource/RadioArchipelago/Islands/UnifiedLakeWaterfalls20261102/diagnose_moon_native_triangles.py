import bpy,pathlib,json,math,sys
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
FILE=HERE/'STAGING/Moon/moon_resonance_island.fbx'
blend='--blend'in sys.argv
bpy.ops.wm.read_factory_settings(use_empty=True)
if blend:bpy.ops.wm.open_mainfile(filepath=str(HERE/'STAGING/Moon/moon_unified_lake_waterfalls.blend'))
else:bpy.ops.import_scene.fbx(filepath=str(FILE),use_custom_normals=True,use_custom_props=True)
ob=bpy.data.objects['RA_Moon_Waterfall_+00_+03_01'];m=ob.data;m.calc_loop_triangles();records=[]
for i,t in enumerate(m.loop_triangles):
 ps=[ob.matrix_world@m.vertices[j].co for j in t.vertices]
 cross=(ps[1]-ps[0]).cross(ps[2]-ps[0]);area=cross.length*.5
 ids=list(t.loops);uv=[tuple(m.uv_layers[0].data[j].uv)for j in ids];mask=[tuple(m.uv_layers[1].data[j].uv)for j in ids]
 records.append(dict(triangle=i,polygon=t.polygon_index,vertices=list(t.vertices),area=area,
  minimum_edge=min((a-b).length for a,b in zip(ps,ps[1:]+ps[:1])),positions=[list(p)for p in ps],uv=uv,mask=mask,
  kind='body'if all(v[0]>.999 for v in mask)else'lake/outlet'))
records.sort(key=lambda x:x['area'])
result=dict(triangles=len(records),vertices=len(m.vertices),exact_zero=sum(r['area']==0 for r in records),below1e_8=sum(r['area']<1e-8 for r in records),below1e_6=sum(r['area']<1e-6 for r in records),smallest=records[:12])
(HERE/('moon_blend_triangle_diagnosis.json'if blend else'moon_native_triangle_diagnosis.json')).write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2),flush=True)
