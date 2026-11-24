import bpy,pathlib,sys,numpy as np,bmesh,json
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_relief as r
bpy.ops.wm.open_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'))
root=bpy.data.objects['EmeraldReliefIsland'];island=[root]+list(root.children_recursive);repaired=[]
for o in island:
 if o.type!='MESH':continue
 m=o.data;normals=np.empty(len(m.corner_normals)*3,dtype=np.float32);m.corner_normals.foreach_get('vector',normals)
 lengths=np.linalg.norm(normals.reshape(-1,3),axis=1);bad=np.flatnonzero(lengths<.9)
 if len(bad):
  badset=set(int(i) for i in bad);polys=[p.index for p in m.polygons if any(i in badset for i in p.loop_indices)]
  print('NORMAL_REPAIR',o.name,'corners',len(bad),'polys',len(polys),flush=True)
  for p in polys:m.polygons[p].use_smooth=False
  m.update();normals2=np.empty(len(m.corner_normals)*3,dtype=np.float32);m.corner_normals.foreach_get('vector',normals2);lengths2=np.linalg.norm(normals2.reshape(-1,3),axis=1)
  leftovers=np.flatnonzero(lengths2<.9)
  if len(leftovers):
   badset=set(int(i) for i in leftovers);delete_ids=[p.index for p in m.polygons if any(i in badset for i in p.loop_indices)]
   print('NORMAL_TINY_POLYGONS_REMOVED',len(delete_ids),flush=True)
   bm=bmesh.new();bm.from_mesh(m);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.faces[i] for i in delete_ids],context='FACES');bm.to_mesh(m);bm.free();m.update()
  repaired.append(dict(mesh=o.name,bad_corners=len(bad),flat_faces=len(polys),removed_faces=len(delete_ids) if len(leftovers) else 0))
r.export_fbx(r.OUT/'emerald_relief_island.fbx',island)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'emerald_relief_preview.blend'))
report=json.loads((HERE/'relief_report.json').read_text());report['normal_repairs']=repaired;report['island_triangles']=sum(len(o.data.polygons) for o in island if o.type=='MESH');report['total_triangles']=report['island_triangles']+report['cobble_triangles']
(HERE/'relief_report.json').write_text(json.dumps(report,indent=2));print('NORMAL_REPAIRS_COMPLETE',len(repaired),flush=True)
