"""Remove only decorative source faces whose serialized corner normals are invalid."""
import bpy,bmesh,numpy as np,sys,pathlib,json
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_relief as r
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(r.OUT/'emerald_relief_island.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
removed=[]
for o in list(bpy.context.scene.objects):
 if o.type!='MESH':continue
 m=o.data;values=np.empty(len(m.corner_normals)*3,dtype=np.float32);m.corner_normals.foreach_get('vector',values);lengths=np.linalg.norm(values.reshape(-1,3),axis=1)
 bad=set(int(i) for i in np.flatnonzero(np.abs(lengths-1)>.01))
 if not bad:continue
 ids=[p.index for p in m.polygons if any(i in bad for i in p.loop_indices)]
 print('SERIALIZED_NORMAL_REPAIR',o.name,len(bad),'bad corners',len(ids),'faces',flush=True)
 assert not any(s in o.name for s in ('Terrain','Road','Bridge','Stair','Tread'))
 bm=bmesh.new();bm.from_mesh(m);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.faces[i] for i in ids],context='FACES');bm.to_mesh(m);bm.free();m.update();removed.append(dict(mesh=o.name,faces=len(ids)))
r.export_fbx(r.OUT/'emerald_relief_island.fbx',list(bpy.context.scene.objects))
report=json.loads((HERE/'relief_report.json').read_text());report['serialized_normal_face_repairs']=removed
(HERE/'relief_report.json').write_text(json.dumps(report,indent=2));print('SERIALIZED_NORMAL_REPAIR_COMPLETE',removed,flush=True)
