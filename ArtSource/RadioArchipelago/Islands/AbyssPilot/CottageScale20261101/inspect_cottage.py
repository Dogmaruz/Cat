import bpy, pathlib, sys, math, json, collections
from mathutils import Vector
from mathutils.kdtree import KDTree
HERE=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent))
import build_abyss as a
import architecture
bpy.ops.wm.open_mainfile(filepath=str(HERE.parent/'NaturalTrees/emerald_abyss_natural.blend'))
base=set(bpy.context.scene.objects)
spec=next(s for s in json.loads((HERE.parent/'abyss_report.json').read_text())['islets'] if s['distance']==478)
p,n=a.road.sample(spec['distance']);t=Vector((n.y,-n.x,0));site=Vector(spec['center'])+n*3.2+t*.3
site.z=a.play(a.r.nearest_s(site.x,site.y))-.90+.04
direction=-n;yaw=math.atan2(direction.y,direction.x)+math.pi*.5
objects=architecture.build_building('forest_house',location=site,yaw=yaw,scale=.28,seed=4781)
bpy.context.view_layer.update()
points=[];parts=[];cache={};stats=collections.Counter()
for o in objects:
 m=a.ex.simplified_mesh(o,cache,stats)
 v=[o.matrix_world@vert.co for vert in m.vertices];points.extend(v)
 parts.append(dict(name=o.name,vertices=len(v),bounds_min=[min(q[k] for q in v) for k in range(3)],bounds_max=[max(q[k] for q in v) for k in range(3)]))
kd=KDTree(len(points))
for i,q in enumerate(points):kd.insert(q,i)
kd.balance();matched=[];boundary=[];union=[]
lo=[min(q[k] for q in points)-.001 for k in range(3)];hi=[max(q[k] for q in points)+.001 for k in range(3)]
for o in base:
 if o.type!='MESH':continue
 box=[o.matrix_world@Vector(c) for c in o.bound_box]
 if any(max(q[k] for q in box)<lo[k] or min(q[k] for q in box)>hi[k] for k in range(3)):continue
 v=[o.matrix_world@vert.co for vert in o.data.vertices]
 ids={i for i,q in enumerate(v) if all(lo[k]<=q[k]<=hi[k] for k in range(3)) and kd.find(q)[2]<.0001}
 if not ids:continue
 complete=sum(all(i in ids for i in poly.vertices) for poly in o.data.polygons)
 mixed=[poly.index for poly in o.data.polygons if any(i in ids for i in poly.vertices) and not all(i in ids for i in poly.vertices)]
 matched.append(dict(name=o.name,matched_vertices=len(ids),complete_faces=complete,mixed_faces=len(mixed)))
 union.extend(v[i] for i in ids)
 boundary.extend(dict(name=o.name,polygon=i) for i in mixed[:3])
report=dict(site=list(site),yaw=yaw,normal=list(n),tangent=list(t),spec=spec,source_vertices=len(points),matched_vertices=len(union),matched=matched,boundary=boundary,parts=parts,
 bounds_min=[min(q[k] for q in union) for k in range(3)],bounds_max=[max(q[k] for q in union) for k in range(3)])
(HERE/'inspection.json').write_text(json.dumps(report,indent=2))
print('COTTAGE_INSPECTION',json.dumps({k:v for k,v in report.items() if k not in('parts','spec')}),flush=True)
