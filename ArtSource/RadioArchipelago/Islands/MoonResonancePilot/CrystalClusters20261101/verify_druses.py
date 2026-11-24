import bpy,bmesh,pathlib,json
HERE=pathlib.Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'moon_resonance_hexagonal_druses.blend'))
report=dict(passed=True,meshes=[],boundary_edges=0,nonmanifold_edges=0,degenerate_faces=0,smooth_faces=0,closed_components=0)
for ob in bpy.context.scene.objects:
 if ob.type!='MESH'or ob.get('moon_bucket')!='Crystal':continue
 bm=bmesh.new();bm.from_mesh(ob.data)
 boundary=sum(e.is_boundary for e in bm.edges);nonmanifold=sum(not e.is_manifold for e in bm.edges)
 degenerate=sum(f.calc_area()<1e-8 for f in bm.faces);smooth=sum(p.use_smooth for p in ob.data.polygons)
 remaining=set(bm.verts);components=0
 while remaining:
  stack=[remaining.pop()];components+=1
  while stack:
   v=stack.pop()
   for edge in v.link_edges:
    other=edge.other_vert(v)
    if other in remaining:remaining.remove(other);stack.append(other)
 report['meshes'].append(dict(name=ob.name,vertices=len(bm.verts),triangles=len(bm.faces),closed_components=components,volume=bm.calc_volume(signed=True)))
 report['boundary_edges']+=boundary;report['nonmanifold_edges']+=nonmanifold;report['degenerate_faces']+=degenerate;report['smooth_faces']+=smooth;report['closed_components']+=components;bm.free()
report['passed']=not any(report[k]for k in('boundary_edges','nonmanifold_edges','degenerate_faces','smooth_faces'))and report['closed_components']==122
(HERE/'topology.json').write_text(json.dumps(report,indent=2));print('DRUSE_TOPOLOGY',json.dumps(report),flush=True)
assert report['passed']
