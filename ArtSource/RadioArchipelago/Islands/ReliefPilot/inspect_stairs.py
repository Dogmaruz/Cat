import bpy,sys,pathlib,json
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_relief as r
bpy.ops.wm.open_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'))
deps=bpy.context.evaluated_depsgraph_get()
for s in (240,250,255,265,270):
 for cross in(-2.5,-1.5,-.5,.5,1.5,2.5):
  p=r.point(s,cross);origin=p+Vector((0,0,80));hit,location,normal,face,obj,matrix=bpy.context.scene.ray_cast(deps,origin,Vector((0,0,-1)))
  print('STAIR_SAMPLE',s,cross,'expected',p.z,'hit',obj.name if obj else None,list(location),flush=True)
o=bpy.data.objects.get('Relief_Stair_tread_30_3');print('TREAD',o.name,[list(v.co) for v in o.data.vertices],len(o.data.polygons),[list(p.vertices) for p in o.data.polygons],list(o.data.color_attributes.get('Color').data[0].color),flush=True)
