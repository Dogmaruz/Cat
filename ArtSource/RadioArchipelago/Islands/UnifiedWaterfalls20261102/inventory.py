import bpy, pathlib, json, hashlib
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
SOURCE=HERE.parent/'RooftopMechanisms20261101/STAGING'
out={}
for label,file in [('Emerald','emerald_rooftops.blend'),('Coral','coral_rooftops.blend'),('Moon','moon_rooftops.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/label/file))
    bpy.context.view_layer.update()
    rows=[]
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH':continue
        mats=[m.name for m in ob.data.materials if m]
        if not any(any(k in m for k in ('Water','Stream','Foam')) for m in mats):continue
        ps=[ob.matrix_world@v.co for v in ob.data.vertices]
        uv=ob.data.uv_layers.active
        row=dict(name=ob.name,materials=mats,vertices=len(ps),polygons=len(ob.data.polygons),
            bounds=[[min(p[k] for p in ps),max(p[k] for p in ps)] for k in range(3)],
            uv_range=[[min(x.uv[k] for x in uv.data),max(x.uv[k] for x in uv.data)] for k in range(2)] if uv else None,
            properties={k:ob[k] for k in ob.keys()})
        rows.append(row)
    out[label]=rows
(HERE/'inventory.json').write_text(json.dumps(out,indent=2))
print(json.dumps(out,indent=2),flush=True)
