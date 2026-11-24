import bpy,json,pathlib,sys,collections,math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_coral_oasis as source
bpy.ops.wm.open_mainfile(filepath=str(HERE/'coral_oasis_preview.blend'))
verts=[];faces=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH'or obj.get('coral_bucket')!='Terrain':continue
    offset=len(verts);verts.extend(obj.matrix_world@v.co for v in obj.data.vertices)
    faces.extend(tuple(offset+i for i in p.vertices)for p in obj.data.polygons)
bvh=BVHTree.FromPolygons(verts,faces,all_triangles=True)
def surface(x,y):
    p,n,face,distance=bvh.ray_cast(Vector((x,y,10000)),Vector((0,0,-1)),20000)
    return p.z if p is not None else None
report=dict(unsupported_near_intro=[],spring_samples=[])
for obj in bpy.context.scene.objects:
    if obj.type!='MESH'or obj.get('coral_bucket')!='Rocks':continue
    data=obj.data;parent=list(range(len(data.vertices)))
    def find(x):
        while parent[x]!=x:parent[x]=parent[parent[x]];x=parent[x]
        return x
    for e in data.edges:
        a,b=[find(v)for v in e.vertices]
        if a!=b:parent[b]=a
    groups=collections.defaultdict(list)
    for i,v in enumerate(data.vertices):groups[find(i)].append(obj.matrix_world@v.co)
    for group in groups.values():
        lo=Vector(tuple(min(p[i]for p in group)for i in range(3)));hi=Vector(tuple(max(p[i]for p in group)for i in range(3)));center=(lo+hi)*.5
        distance,s=source.nearest(center.x,center.y)
        if not(60<s<100 and distance<12):continue
        height=surface(center.x,center.y)
        if height is None:report['unsupported_near_intro'].append(dict(mesh=obj.name,center=list(center),min=list(lo),max=list(hi),route_s=s,lateral_distance=distance,actual_terrain_hit=False))
for x,y in [(67,-7),(67+7,-7),(67-7,-7),(67,-7+5),(67,-7-5)]:
    h=surface(x,y);report['spring_samples'].append(dict(x=x,y=y,actual_terrain=h,water_height=6.72,water_depth=6.72-h if h is not None else None))
(HERE/('decor-support-after.json'if'--after'in sys.argv else'decor-support-before.json')).write_text(json.dumps(report,indent=2))
print('CORAL_DECOR_SUPPORT',json.dumps(report),flush=True)
