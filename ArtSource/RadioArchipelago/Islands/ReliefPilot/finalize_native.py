"""Final native source and genuine renders of the verified serialized geometry."""
import bpy,pathlib,sys,json,hashlib
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_relief as r
bpy.ops.wm.read_factory_settings(use_empty=True)
for name in ('emerald_relief_island.fbx','emerald_relief_cobbles.fbx'):
 bpy.ops.import_scene.fbx(filepath=str(r.OUT/name),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  for i,material in enumerate(o.data.materials):
   canonical=bpy.data.materials.get(material.name.split('.')[0])
   if canonical:o.data.materials[i]=canonical
r.render_proofs()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_relief_final.blend'))
report=json.loads((HERE/'relief_report.json').read_text());verification=json.loads((HERE/'verification.json').read_text())
report['verified_final_geometry']=verification;report['final_native_file']='emerald_relief_final.blend';report['proofs_show_final_serialized_geometry']=True
report['island_triangles']=verification['files']['emerald_relief_island.fbx']['triangles'];report['cobble_triangles']=verification['files']['emerald_relief_cobbles.fbx']['triangles'];report['total_triangles']=verification['total_triangles']
(HERE/'relief_report.json').write_text(json.dumps(report,indent=2));print('FINAL_NATIVE_SAVED',flush=True)
