"""Correct the lower lip metadata anchor; source and FBX remain untouched."""
import bpy,pathlib,json,math
HERE=pathlib.Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'amber_winds_continuous.blend'))
p=HERE/'continuous_sheet_handoff.json';report=json.loads(p.read_text())
sheet=report['sheets'][0];obj=bpy.data.objects[sheet['node']]
target_z=sheet['source_top'][2]-1.5
uv=obj.data.uv_layers.active
candidates=[obj.matrix_world@obj.data.vertices[loop.vertex_index].co for loop in obj.data.loops if abs(uv.data[loop.index].uv.x-.5)<1e-6]
point=min(candidates,key=lambda p:abs(p.z-target_z))
assert abs(point.z-target_z)<1e-5
sheet['source_lip']=list(point);sheet['unity_lip']=[point.x,point.z,-point.y]
p.write_text(json.dumps(report,indent=2))
print('LOWER_LIP_METADATA',sheet['unity_lip'])
