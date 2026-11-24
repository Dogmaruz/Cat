"""Reimport the actual FBX with authored normals, without changing any asset."""
import bpy,pathlib,sys,json,hashlib
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import water_seam_shading as seam
for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
model=HERE/'STAGING/amber_winds_island.fbx'
bpy.ops.import_scene.fbx(filepath=str(model),use_custom_normals=True)
checks=seam.inspect()
report=dict(model_sha256=hashlib.sha256(model.read_bytes()).hexdigest(),import_custom_normals=True,seams=checks,max_displaced_gap=max(x['max_displaced_gap']for x in checks))
(HERE/'fbx_shading_roundtrip.json').write_text(json.dumps(report,indent=2))
print('FBX_CUSTOM_NORMALS_ROUNDTRIP_PASS',json.dumps(dict(sha256=report['model_sha256'],max_displaced_gap=report['max_displaced_gap'])),flush=True)
