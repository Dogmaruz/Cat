import bpy,bmesh,pathlib,collections,json
HERE=pathlib.Path(__file__).resolve().parent
SRC=HERE.parents[3]/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/EmeraldRelief/emerald_relief_island.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(SRC),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('unity_material_bucket')=='Terrain']
bm=bmesh.new()
for o in objects:
 temp=o.data.copy();temp.transform(o.matrix_world);bm.from_mesh(temp);bpy.data.meshes.remove(temp)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00003);bm.normal_update()
stats=dict(vertices=len(bm.verts),faces=len(bm.faces),edges=len(bm.edges),boundary=sum(e.is_boundary for e in bm.edges),wire=sum(e.is_wire for e in bm.edges),nonmanifold=sum(not e.is_manifold for e in bm.edges),minz=min(v.co.z for v in bm.verts),maxz=max(v.co.z for v in bm.verts))
print('TERRAIN_TOPOLOGY',json.dumps(stats),flush=True)
bm.free()
