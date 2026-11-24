"""Fresh Blender process verifies the sealed second-island FBX; never writes assets."""
import bpy, math, pathlib, json, hashlib
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
REPO=HERE.parents[3]
FILE=REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/CoralOasis/coral_oasis_island.fbx'
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(FILE),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
bpy.context.view_layer.update()
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
report=dict(passed=False,sha256=hashlib.sha256(FILE.read_bytes()).hexdigest(),mesh_batches=len(meshes),triangles=0,nonfinite=0,invalid_indices=0,missing_vertex_colors=0,missing_uv=0,terrain_up=0,terrain_down=0,water_up=0,water_down=0,unexpected_materials=[],anchors=[])
for obj in meshes:
    data=obj.data;report['triangles']+=sum(len(p.vertices)-2 for p in data.polygons)
    report['nonfinite']+=sum(not all(math.isfinite(c)for c in v.co)for v in data.vertices)
    report['invalid_indices']+=sum(any(v<0 or v>=len(data.vertices)for v in p.vertices)for p in data.polygons)
    report['missing_vertex_colors']+=int(not data.color_attributes)
    report['missing_uv']+=int(not data.uv_layers)
    if 'Terrain' in obj.name or 'Water_' in obj.name:
        world=[obj.matrix_world@v.co for v in data.vertices]
        prefix='terrain' if 'Terrain'in obj.name else'water'
        for polygon in data.polygons:
            a,b,c=[world[i]for i in polygon.vertices[:3]];normal=(b-a).cross(c-a)
            if normal.z>1e-6:report[prefix+'_up']+=1
            elif normal.z< -1e-6:report[prefix+'_down']+=1
    for material in data.materials:
        if material is None or not material.name.startswith('RA_Coral_'):report['unexpected_materials'].append(material.name if material else 'null')
for name,expected in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
    obj=bpy.data.objects.get(name);position=obj.matrix_world.translation if obj else Vector((100000,0,0))
    report['anchors'].append(dict(name=name,position=list(position),error=(position-Vector(expected)).length))
report['passed']=report['triangles']>1000000 and all(report[k]==0 for k in('nonfinite','invalid_indices','missing_vertex_colors','missing_uv','terrain_down','water_down')) and report['terrain_up']>70000 and not report['unexpected_materials'] and all(a['error']<.002 for a in report['anchors'])
(HERE/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('CORAL_FRESH_ROUNDTRIP',json.dumps(report),flush=True)
assert report['passed'],report
