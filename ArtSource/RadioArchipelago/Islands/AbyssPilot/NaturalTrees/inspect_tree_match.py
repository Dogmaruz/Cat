import bpy,pathlib,sys,collections,json,time,numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
HERE=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent));import build_abyss as a
bpy.ops.wm.open_mainfile(filepath=str(HERE.parent/'LivingPass/emerald_abyss_living.blend'))
base=set(bpy.context.scene.objects);main=bpy.data.objects['RA_Closed_Mainland']
with bpy.data.libraries.load(str(a.r.SRC/'island.blend'),link=False) as (fr,to):
 to.objects=[n for n in fr.objects if n.startswith('V4_Tree_') and not any(w in n.lower() for w in ('shrub','bush'))]
sources=[o for o in to.objects if o]
for o in sources:bpy.context.scene.collection.objects.link(o)
bpy.context.view_layer.update();placements=a.relocate_rigid_objects(main,sources);sources=list(set(bpy.context.scene.objects)-base);bpy.context.view_layer.update()
cache={};stats=collections.Counter();points=[];sites={};details=[]
for o in sources:
 if not o.name in bpy.data.objects:continue
 mesh=a.ex.simplified_mesh(o,cache,stats);world=[o.matrix_world@v.co for v in mesh.vertices];points+=world
 if 'branchwork' in o.name:
  co=np.array([tuple(v) for v in world]);sites[o.name]=dict(base=list(o.matrix_world.translation),height=float(co[:,2].max()-co[:,2].min()),radius=float(max(np.ptp(co[:,0]),np.ptp(co[:,1]))*.5))
 details.append(dict(name=o.name,vertices=len(world)))
kd=KDTree(len(points))
for i,q in enumerate(points):kd.insert(q,i)
kd.balance();results=[]
for o in base:
 if o.type!='MESH' or o.get('unity_material_bucket') not in ('Wood','Foliage'):continue
 co=[o.matrix_world@v.co for v in o.data.vertices];matched=[kd.find(v)[2]<.001 for v in co];faces=[f.index for f in o.data.polygons if all(matched[i] for i in f.vertices)]
 results.append(dict(name=o.name,category=o.get('unity_material_bucket'),matched_faces=len(faces),matched_vertices=sum(matched),totalfaces=len(o.data.polygons),triangles=sum(len(o.data.polygons[i].vertices)-2 for i in faces)))
report=dict(source_objects=details,placements=placements,sites=sites,matching=results,matched_triangles=sum(x['triangles'] for x in results),stats=dict(stats))
(HERE/'tree_matching.json').write_text(json.dumps(report,indent=2));print('TREE_MATCH',len(sites),len(points),report['matched_triangles'],flush=True)
