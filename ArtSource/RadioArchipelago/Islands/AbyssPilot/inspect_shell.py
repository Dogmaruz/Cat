import bpy,bmesh,pathlib,json,collections
HERE=pathlib.Path(__file__).resolve().parent;OUT=HERE.parents[3]/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/EmeraldAbyss'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(OUT/'emerald_abyss_island.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
bm=bmesh.new()
for o in bpy.context.scene.objects:
 if o.type=='MESH' and o.get('unity_material_bucket') in ('Terrain','Cliffs'):
  tmp=o.data.copy();tmp.transform(o.matrix_world);bm.from_mesh(tmp);bpy.data.meshes.remove(tmp)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00006);bm.verts.index_update();bm.faces.index_update();seen=set();groups=[]
for v in bm.verts:
 if v in seen:continue
 stack=[v];vs=set();fs=set();es=set()
 while stack:
  x=stack.pop()
  if x in seen:continue
  seen.add(x);vs.add(x);fs.update(x.link_faces);es.update(x.link_edges)
  for e in x.link_edges:
   y=e.other_vert(x)
   if y not in seen:stack.append(y)
 groups.append(dict(vertices=len(vs),faces=len(fs),boundary=sum(e.is_boundary for e in es),nonmanifold=sum(not e.is_manifold for e in es),minz=min(x.co.z for x in vs),maxz=max(x.co.z for x in vs)))
groups.sort(key=lambda g:g['faces'],reverse=True)
print('STRUCTURAL_COMPONENTS',json.dumps(groups[:20]),flush=True);print('COMPONENT_TOTALS',len(groups),sum(g['faces'] for g in groups if g['boundary']),sum(g['boundary'] for g in groups),flush=True)
