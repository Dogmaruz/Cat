import bpy,bmesh,json,pathlib,sys
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_relief as r
bpy.ops.wm.open_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'))
root=bpy.data.objects['EmeraldReliefIsland'];col=root.users_collection[0]
for o in list(root.children_recursive):
 if o.name.startswith('Relief_'):bpy.data.objects.remove(o,do_unlink=True)
materials={k:bpy.data.materials['RA_Emerald_'+k] for k in list(r.ex.BUCKETS)+['BridgeDeck','StepTreads'] if bpy.data.materials.get('RA_Emerald_'+k)}
source=(HERE/'build_relief.py').read_text();fragment=source.split(' rng=random.Random(928472)\n',1)[1].split(' for name,pos in ',1)[0]
fragment='rng=random.Random(928472)\n'+'\n'.join(line[1:] if line.startswith(' ') else line for line in fragment.splitlines())
scope=dict(vars(r));scope.update(root=root,col=col,materials=materials,output=[])
exec(fragment,scope)
parts=scope['output']
for o in parts:
 bm=bmesh.new();bm.from_mesh(o.data)
 # Small real edge bevels separate neighboring stone and timber pieces.
 bmesh.ops.bevel(bm,geom=list(bm.edges),offset=.018 if o.get('unity_material_bucket')!='BridgeDeck' else .025,segments=1,affect='EDGES')
 bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
island=[root]+list(root.children_recursive)
r.export_fbx(r.OUT/'emerald_relief_island.fbx',island)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'))
report=json.loads((HERE/'relief_report.json').read_text());report['profile_keys']=1071;report['solid_structures_rebuilt']=True
report['island_triangles']=sum(len(o.data.polygons) for o in island if o.type=='MESH');report['total_triangles']=report['island_triangles']+report['cobble_triangles']
(HERE/'relief_report.json').write_text(json.dumps(report,indent=2));print('SOLID_STRUCTURES_REPAIRED',len(parts),report['total_triangles'],flush=True)
