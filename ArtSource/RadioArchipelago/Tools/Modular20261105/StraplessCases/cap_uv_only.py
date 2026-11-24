"""Cosmetic cap correction for an already validated derivative, no mesh edit."""
from pathlib import Path
import bpy,json,sys
from mathutils import Vector
P=Path(__file__).parent;sys.path.insert(0,str(P));import clean_cases as c
names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['Nori']
for name in names:
 folder=P/'Loft'/name;bpy.ops.wm.open_mainfile(filepath=str(folder/'editable.blend'),load_ui=False)
 case=bpy.data.objects['Case'];meta=json.loads((folder/'Meta.json').read_text());pivot=Vector(meta['topPivot']);brass=c.source_swatch(case,(.68,.48,.20));uv=case.data.uv_layers.active.data;count=0
 for f in case.data.polygons:
  if all(abs(case.data.vertices[i].co.z-pivot.z)<1e-5 for i in f.vertices) and (f.center-pivot).length<.23:
   for i in f.loop_indices:uv[i].uv=brass
   count+=1
 meta['repairs']['straplessCase20261105']['bearingCapUvRepairs']=count
 meta['harnessNames']=[];(folder/'Meta.json').write_text(json.dumps(meta,indent=2))
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/'editable.blend'))
 bpy.ops.object.select_all(action='DESELECT')
 for n in ('BackpackRoot','Case','TopPivot','TopRotor'):bpy.data.objects[n].select_set(True)
 bpy.context.view_layer.objects.active=bpy.data.objects['BackpackRoot']
 bpy.ops.export_scene.fbx(filepath=str(folder/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
 c.render(name);print('CAP_UV_ONLY',name,count,flush=True)
