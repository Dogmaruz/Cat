"""Read-only proof that a continuous rig retains the original rest surface/UV."""
import argparse
import json
import pathlib
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
import prepare_radio_rigs as rigtools

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--character',choices=rigtools.CHARACTERS,default='Tiko')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[])
name=args.character
folder=rigtools.LIBRARY/'Review'/name
bpy.ops.wm.open_mainfile(filepath=str(folder/'rig-review.blend'))
meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
assert len(meshes)==1,'Continuous rig must have exactly one mesh'
obj=meshes[0];mesh=obj.data
assert obj.get('arm_attachment_mode')=='continuous-source'
assert mesh.attributes.get('SocketCap') is None
points=[vertex.co.copy()for vertex in mesh.vertices]
centers=[]
for polygon in mesh.polygons:
    center=sum((points[i]for i in polygon.vertices),Vector())/len(polygon.vertices)
    uv=sum((mesh.uv_layers.active.data[i].uv for i in polygon.loop_indices),Vector((0,0)))/len(polygon.loop_indices)
    centers.append((center,uv))
graph=[set()for _ in mesh.vertices]
edge_use={}
for polygon in mesh.polygons:
    for a,b in polygon.edge_keys:
        graph[a].add(b);graph[b].add(a)
        key=tuple(sorted((a,b)));edge_use[key]=edge_use.get(key,0)+1
remaining=set(range(len(points)));components=[]
while remaining:
    component={remaining.pop()};queue=list(component)
    while queue:
        for other in graph[queue.pop()]&remaining:
            remaining.remove(other);component.add(other);queue.append(other)
    components.append(len(component))
rigtools.reset()
source=rigtools.import_source(name).data
source_edges={}
for polygon in source.polygons:
    for edge in polygon.edge_keys:
        edge=tuple(sorted(edge));source_edges[edge]=source_edges.get(edge,0)+1
source_boundary=[(source.vertices[a].co,source.vertices[b].co)for(a,b),count in source_edges.items()if count==1]
boundary_error=0.
for(a,b),count in edge_use.items():
    if count!=1:continue
    midpoint=(points[a]+points[b])/2
    distances=[]
    for start,end in source_boundary:
        delta=end-start
        t=max(0.,min(1.,(midpoint-start).dot(delta)/delta.length_squared))
        distances.append((midpoint-start-delta*t).length)
    boundary_error=max(boundary_error,min(distances)if distances else float('inf'))
bvh=BVHTree.FromPolygons([vertex.co for vertex in source.vertices],[list(p.vertices)for p in source.polygons],all_triangles=True)
surface_error=max(bvh.find_nearest(point)[3]for point in points)
uv_error=0.
for center,uv in centers:
    nearest,normal,index,distance=bvh.find_nearest(center)
    polygon=source.polygons[index]
    triangle=[source.vertices[i].co for i in polygon.vertices]
    source_uv=[Vector((*source.uv_layers.active.data[i].uv,0))for i in polygon.loop_indices]
    expected=barycentric_transform(nearest,*triangle,*source_uv)
    uv_error=max(uv_error,(uv-Vector(expected[:2])).length)
report={
    'character':name,'mesh_count':1,'connected_component_sizes':sorted(components,reverse=True),
    'boundary_edges':sum(value==1 for value in edge_use.values()),
    'edges_with_more_than_two_faces':sum(value>2 for value in edge_use.values()),
    'original_source_boundary_edges':len(source_boundary),
    'maximum_inherited_boundary_distance':boundary_error,
    'maximum_original_surface_distance':surface_error,
    'maximum_original_uv_interpolation_error':uv_error,
    'pass':len(components)==1 and all(value<=2 for value in edge_use.values())and boundary_error<1e-5 and surface_error<1e-5 and uv_error<1e-4,
}
(folder/'continuous-source-check.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('RADIO_CONTINUOUS_SOURCE',json.dumps(report),flush=True)
if not report['pass']:raise RuntimeError('Continuous source surface validation failed')
