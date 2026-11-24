import bpy,bmesh,pathlib,json
HERE=pathlib.Path(__file__).resolve().parent
SRC=HERE.parent/'V4/Output/01_emerald_ravine/island.blend'
bpy.ops.wm.open_mainfile(filepath=str(SRC))
o=next(o for o in bpy.context.scene.objects if 'CONNECTED_FLOATING_TERRAIN' in o.name)
bm=bmesh.new();bm.from_mesh(o.data);bm.normal_update()
stats=dict(vertices=len(bm.verts),faces=len(bm.faces),edges=len(bm.edges),boundary=sum(e.is_boundary for e in bm.edges),wire=sum(e.is_wire for e in bm.edges),nonmanifold=sum(not e.is_manifold for e in bm.edges),minz=min(v.co.z for v in bm.verts),maxz=max(v.co.z for v in bm.verts),materials=[m.name for m in o.data.materials],material_counts={str(i):sum(len(p.vertices)-2 for p in o.data.polygons if p.material_index==i) for i in range(len(o.data.materials))})
print('SOURCE_TERRAIN_TOPOLOGY',json.dumps(stats),flush=True)
bm.free()
