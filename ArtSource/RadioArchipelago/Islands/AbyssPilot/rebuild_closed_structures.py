import bpy,bmesh,pathlib,sys,json,numpy as np
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_abyss as a
bpy.ops.wm.open_mainfile(filepath=str(a.r.SRC/'island.blend'));terrain=next(o for o in bpy.context.scene.objects if 'CONNECTED_FLOATING_TERRAIN' in o.name)
for o in list(bpy.context.scene.objects):
 if o!=terrain:bpy.data.objects.remove(o,do_unlink=True)
cliffs=[a.mat('Abyss_closed_cliff_'+str(i),c,'Cliffs') for i,c in enumerate(((.34,.39,.41),(.39,.43,.43),(.27,.33,.36),(.42,.43,.39),(.30,.38,.39)))];grass=a.mat('Abyss_closed_grass',(.29,.42,.20),'Terrain')
a.deform_and_carve(terrain,cliffs);terrain.name='RA_Closed_Mainland'
for name,s,offset,along,across,depth,kind in [('Low grass shard',146,2.5,5.2,3.8,23,'rough'),('Ravine needle',171,-2.4,4.7,3.6,29,'spire'),('High terraced garden',420,2.8,8.5,4.5,27,'terrace'),('Leaning radio spire',445,-2.6,5.0,3.6,35,'spire'),('Copper cottage isle',478,2.8,6.8,4.5,25,'rough')]:
 o,spec=a.islet('RA_Closed_'+name,s,offset,along,across,depth,kind,cliffs,grass)
materials={cat:a.r.add_material('RA_Emerald_'+cat,cat) for cat in ('Terrain','Cliffs')};stats=[]
for o in list(bpy.context.scene.objects):
 if o.type!='MESH':continue
 mesh=o.data;before=a.topology(mesh)
 # Keep the canonical closed polygon indices. Unity triangulates these at import.
 # Collapsing tiny boolean junctions can create nonmanifold edges, so no dissolve.
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
 after=a.topology(mesh);assert after['boundary_edges']==0 and after['nonmanifold_edges']==0,(o.name,after)
 source_mats=list(mesh.materials);colors={m.name:a.ex.color_info(m) for m in source_mats};bank=mesh.attributes.get('river_bank_mix');rgba=[];categories=[]
 for p in mesh.polygons:
  old=source_mats[p.material_index];bucket=old.get('abyss_bucket') or ('Cliffs' if any(s in old.name.lower() for s in ('rock','strata')) else 'Terrain');categories.append(0 if bucket=='Terrain' else 1);color,slate=colors[old.name]
  for li,vi in zip(p.loop_indices,p.vertices):
   w=bank.data[vi].value if bank and bank.domain=='POINT' else 0
   c=tuple(color[j]*(1-w)+slate[j]*w for j in range(3))+(1,) if slate else tuple(color[:3])+(1,);rgba.extend(c)
 mesh.materials.clear();mesh.materials.append(materials['Terrain']);mesh.materials.append(materials['Cliffs'])
 for p,category in zip(mesh.polygons,categories):p.material_index=category
 color=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');color.data.foreach_set('color',rgba)
 if not mesh.uv_layers:mesh.uv_layers.new(name='UVMap')
 for li in mesh.loops:mesh.uv_layers[0].data[li.index].uv=(mesh.vertices[li.vertex_index].co.x,mesh.vertices[li.vertex_index].co.y)
 o['unity_material_bucket']='Structural';o['structural_closed_shell']=True
 normals=[]
 for p in mesh.polygons:
  normal=p.normal.copy()
  if normal.length<.5:normal=(0,0,1)
  else:normal.normalize()
  normals.extend([tuple(normal)]*len(p.loop_indices))
 mesh.normals_split_custom_set(normals)
 val=np.empty(len(mesh.loops)*3,np.float32);mesh.corner_normals.foreach_get('vector',val);errors=np.abs(np.linalg.norm(val.reshape(-1,3),axis=1)-1);assert float(np.max(errors))<.001,(o.name,float(np.max(errors)))
 stats.append(dict(name=o.name,before=before,after=after));print('CLOSED_STRUCTURE_READY',o.name,after,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'closed_structures.blend'));(HERE/'closed_structure_stats.json').write_text(json.dumps(stats,indent=2))
