import bpy,json,pathlib
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
FILE=HERE.parents[3]/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/CoralOasis/coral_oasis_island.fbx'
bpy.ops.import_scene.fbx(filepath=str(FILE),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
bpy.context.view_layer.update();reports=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or 'Terrain' not in obj.name:continue
    mesh=obj.data;world=[obj.matrix_world@v.co for v in mesh.vertices];up=down=0
    for p in mesh.polygons:
        a,b,c=[world[i]for i in p.vertices[:3]];normal=(b-a).cross(c-a)
        if normal.z>1e-6:up+=1
        elif normal.z< -1e-6:down+=1
    reports.append(dict(mesh=obj.name,up=up,down=down))
report=dict(up=sum(r['up']for r in reports),down=sum(r['down']for r in reports),batches=reports)
(HERE/'winding-diagnostic.json').write_text(json.dumps(report,indent=2))
print('CORAL_WINDING',json.dumps(report),flush=True)
