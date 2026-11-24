import bpy,pathlib,json,math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'amber_winds_preview.blend'))
trees={};counts={}
for category in('Terrain','Cliffs'):
    vv=[];ff=[];intruding=0
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH'or ob.get('amber_bucket')!=category:continue
        base=len(vv);vertices=[ob.matrix_world@v.co for v in ob.data.vertices];vv.extend(vertices)
        for p in ob.data.polygons:
            ff.append(tuple(base+i for i in p.vertices))
            center=sum((vertices[i]for i in p.vertices),Vector())/len(p.vertices)
            if math.hypot((center.x-81)/26,(center.y-102)/19)<.98:intruding+=1
    trees[category]=BVHTree.FromPolygons(vv,ff,all_triangles=True);counts[category]=intruding
fwd=Vector((11,-14,0)).normalized();side=Vector((-fwd.y,fwd.x,0));end=Vector((65,118,15.1));samples=[]
for progress in(-1,0,.3,.6,1,1.4,2):
    row=dict(progress=progress,samples=[])
    for offset in(-2.6,-1.3,0,1.3,2.6):
        p=end+fwd*progress+side*offset;v=dict(offset=offset)
        for category,tree in trees.items():
            point,normal,face,d=tree.ray_cast(Vector((p.x,p.y,1000)),Vector((0,0,-1)),2000)
            v[category]=None if point is None else dict(height=round(point.z,4),normal=tuple(round(x,3)for x in normal))
        row['samples'].append(v)
    samples.append(row)
result=dict(triangle_centres_inside_98percent_canyon=counts,mouth=samples)
(HERE/'canyon_diagnosis.json').write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)
