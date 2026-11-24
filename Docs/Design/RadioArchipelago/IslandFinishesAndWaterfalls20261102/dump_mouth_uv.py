import bpy
import json
from pathlib import Path

root=Path(__file__).resolve().parents[4]
fbx=root/'ArtSource/RadioArchipelago/Islands/UnifiedLakeWaterfalls20261102/STAGING/Coral/coral_oasis_island.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(fbx))
obj=bpy.data.objects['RA_Coral_Waterfall_+01_-01_01']
mesh=obj.data
items={}
for loop in mesh.loops:
    uv=mesh.uv_layers[0].data[loop.index].uv
    mask=mesh.uv_layers[1].data[loop.index].uv.x
    if mask<.999 or abs(uv.y-3.419787883758545)>.0001:
        continue
    p=obj.matrix_world@mesh.vertices[loop.vertex_index].co
    key=(float(uv.x),tuple(float(v) for v in p))
    items[key]=dict(u=key[0],v=float(uv.y),position=list(key[1]),grid_error=abs(uv.x-round(uv.x*32)/32))
result=sorted(items.values(),key=lambda item:item['u'])
out=Path(__file__).with_name('mouth-uv-dump.json')
out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('MOUTH_DUMP',json.dumps(dict(unique=len(result),maximum_grid_error=max(i['grid_error'] for i in result),extra=[i for i in result if i['grid_error']>1e-5])))
