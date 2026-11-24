"""Authorized bounded patch of the frozen source: normals/colors/winding only."""
import bpy,pathlib,sys,json,hashlib,time
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import water_seam_shading as seam
OUT=HERE/'STAGING';MODEL=OUT/'amber_winds_island.fbx'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
expected='d0b36ff88c24eb86fa8387205bc5eab5537ee286a6a8c2c7453534597a4ed819'
assert sha(MODEL)==expected,'Frozen source changed before authorized shading patch'
protected=json.loads((HERE/'pre_canyon_fix_contracts.json').read_text())
assert all(sha(OUT/name)==digest for name,digest in protected.items())
bpy.ops.wm.open_mainfile(filepath=str(HERE/'amber_winds_preview.blend'))
before=seam.geometry_uv_signature();authored=seam.apply(reverse_existing_faces=True);after=seam.geometry_uv_signature()
assert before==after,'Position/UV associations changed during shading-only patch'
checks=seam.inspect()
shader=HERE.parents[3]/'DancingCatsClone/Assets/RadioArchipelago/Art/CoralWater.shader'
bpy.ops.object.select_all(action='DESELECT')
for ob in bpy.context.scene.objects:
    if ob.type in('MESH','EMPTY'):ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(MODEL),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=True,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'amber_winds_preview.blend'))
assert all(sha(OUT/name)==digest for name,digest in protected.items())
result=dict(input_model_sha256=expected,output_model_sha256=sha(MODEL),all_mesh_positions_and_uv_associations_unchanged=True,checked_meshes=len(before),
 shader_sha256=sha(shader),shader_vertex_parameters=dict(flowSpeed=.65,foamLayer=0,fallValues=[0,1],times=[0,.13,.37,1,1.7,2.8,5.25,9.8,13.7,21],uSamples=[0,.25,.5,.75,1]),
 authored=authored,seam_checks=checks,geometry_uv_signatures=after,protected_sidecars=protected)
(HERE/'seam_shading_patch_report.json').write_text(json.dumps(result,indent=2))
report=json.loads((HERE/'amber_winds_report.json').read_text());report['export']['sha256']=result['output_model_sha256'];report['export']['bytes']=MODEL.stat().st_size;report['seam_shading_correction']={k:v for k,v in result.items()if k!='geometry_uv_signatures'}
(HERE/'amber_winds_report.json').write_text(json.dumps(report,indent=2))
print('WATER_SHADING_PATCH_FROZEN',json.dumps(dict(sha256=result['output_model_sha256'],bytes=MODEL.stat().st_size,checked_meshes=len(before),max_displaced_gap=max(x['max_displaced_gap']for x in checks),all_positions_uv_unchanged=True)),flush=True)
