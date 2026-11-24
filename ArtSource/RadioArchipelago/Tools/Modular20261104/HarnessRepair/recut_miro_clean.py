from pathlib import Path
import bpy,bmesh,json,sys,numpy as np
from mathutils import Vector
R=Path('C:/Users/Derek/Games_develop/Cat');sys.path.insert(0,str(R/'ArtSource/RadioArchipelago/Tools/Modular20261104'))
import pack_author as author
import pack_inspect as src
OUT=R/'ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/StagedClean'
QA=R/'Docs/Design/RadioArchipelago/ModularCast20261104/HarnessRepair'
for name,cut in [('Miro',.015),('Miro_Garden',-.225)]:
 original=R/'ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/Before'/name
 bpy.ops.wm.open_mainfile(filepath=str(original/'editable.blend'))
 case=bpy.data.objects['Case'];root=bpy.data.objects['BackpackRoot'];top=bpy.data.objects['TopPivot'];rotor=bpy.data.objects['TopRotor'];mat=case.data.materials[0]
 pieces=[case,bpy.data.objects['Harness.L'],bpy.data.objects['Harness.R']]
 bpy.ops.object.select_all(action='DESELECT')
 for ob in pieces:ob.select_set(True)
 bpy.context.view_layer.objects.active=case;bpy.ops.object.join()
 bm=bmesh.new();bm.from_mesh(case.data)
 beforemeta=json.loads((original/'Meta.json').read_text());oldplane=beforemeta['contactPlaneY']-.025*beforemeta['rawToPreparedScale']
 obsolete=[f for f in bm.faces if all(abs(v.co.y-oldplane)<.00002 for v in f.verts)]
 bmesh.ops.delete(bm,geom=obsolete,context='FACES');bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
 print('OBSOLETE_CAPS_REMOVED',name,len(obsolete),flush=True)
 author.slice_split(bm,Vector((0,cut,0)),Vector((0,1,0)))
 if name in ('Miro','Miro_Garden'):
  faces=[f for f in bm.faces if any(abs(v.co.x)>.15 and v.co.y<.20 and v.co.z>.67 for v in f.verts)]
  geom=set(faces)
  for f in faces:geom.update(f.edges);geom.update(f.verts)
  ret=bmesh.ops.bisect_plane(bm,geom=list(geom),dist=1e-6,plane_co=Vector((0,0,.83)),plane_no=Vector((0,0,1)),clear_inner=False,clear_outer=False)
  edges=[e for e in ret['geom_cut'] if isinstance(e,bmesh.types.BMEdge)]
  bmesh.ops.split_edges(bm,edges=edges)
 groups=author.components(bm);parts=[[],[],[]]
 for group in groups:
  lo,hi=author.bb_faces(group)
  strap=hi.y<=cut+2e-5 or (name in ('Miro','Miro_Garden') and lo.z>=.83-2e-5 and hi.y<.20 and abs((lo.x+hi.x)/2)>.15)
  dest=1 if (lo.x+hi.x)/2<0 else 2
  parts[dest if strap else 0].extend(group)
 # Preserve the existing prepared case vertices/UV except exactly extracted leather.
 newcase,casecaps=author.copy_faces(bm,parts[0],'NewCase',mat);left,lcaps=author.copy_faces(bm,parts[1],'NewHarness.L',mat);right,rcaps=author.copy_faces(bm,parts[2],'NewHarness.R',mat);bm.free()
 bpy.data.objects.remove(case,do_unlink=True)
 for ob,newname in [(newcase,'Case'),(left,'Harness.L'),(right,'Harness.R')]:ob.name=newname;ob.parent=root
 dst=OUT/name;dst.mkdir(parents=True,exist_ok=True)
 meta=json.loads((original/'Meta.json').read_text());a=np.array([list(v.co) for v in newcase.data.vertices]);meta['fixedBoundsMin']=a.min(0).tolist();meta['fixedBoundsMax']=a.max(0).tolist()
 if name=='Miro':
  meta['caseBoundsMin']=a.min(0).tolist();body=a[a[:,2]<1.01];meta['caseBoundsMax']=body.max(0).tolist();meta['contactPlaneY']=.039
 meta['caseBoundsMax'][2]=.83
 meta['repairs']['obsoleteCutCapsRemoved']=len(obsolete)
 meta['repairs']['sourceStrapReextraction20261104']={'cutPlaneY':cut,'upperCutZ':.83 if name=='Miro_Garden' else None,'case':casecaps,'left':lcaps,'right':rcaps}
 meta['outputTriangles']={o.name:sum(len(f.vertices)-2 for f in o.data.polygons) for o in [newcase,left,right,rotor]}
 (dst/'Meta.json').write_text(json.dumps(meta,indent=2))
 bpy.ops.wm.save_as_mainfile(filepath=str(dst/'editable.blend'))
 bpy.ops.object.select_all(action='DESELECT')
 for o in [root,top,newcase,left,right,rotor]:o.select_set(True)
 bpy.context.view_layer.objects.active=root;bpy.ops.export_scene.fbx(filepath=str(dst/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
 for ob in [left,right,rotor]:ob.hide_render=True
 c=src.light_camera();bpy.context.scene.cycles.samples=4;bpy.context.scene.render.resolution_x=400;bpy.context.scene.render.resolution_y=500;c.data.ortho_scale=1.7
 target=Vector((0,0,.6));c.location=target+Vector((3,-4,1.8));c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();bpy.context.scene.render.filepath=str(QA/(name+'_case_reextracted_clean.png'));bpy.ops.render.render(write_still=True)
 print('REPAIRED',name,json.dumps(meta['outputTriangles']),flush=True)

