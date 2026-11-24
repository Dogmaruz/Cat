from pathlib import Path
import bpy,bmesh,json
p=Path(r'C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/Tools/Modular20261105/StraplessCases/Before')
for d in p.iterdir():
 bpy.ops.wm.open_mainfile(filepath=str(d/'editable.blend'),load_ui=False)
 o=bpy.data.objects['Case'];bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
 print('STATS',d.name,len(bm.verts),len(bm.faces),sum(e.is_boundary for e in bm.edges),sum(not e.is_manifold for e in bm.edges),sum(f.calc_area()<1e-9 for f in bm.faces),flush=True)

