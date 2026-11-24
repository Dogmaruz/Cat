"""Read the frozen blend only: matched seam corner normals and vertex colors."""
import bpy,json,pathlib,math
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'amber_winds_preview.blend'))

def srgb(value):return value*12.92 if value<=.0031308 else 1.055*value**(1/2.4)-.055
def row(name,end):
    ob=bpy.data.objects.get(name);assert ob is not None,name
    data=ob.data;uv=data.uv_layers.active;attr=data.color_attributes.get('Color')
    extreme=(max if end else min)(datum.uv.y for datum in uv.data)
    normal_matrix=ob.matrix_world.to_3x3().inverted().transposed();found={}
    for loop in data.loops:
        coord=uv.data[loop.index].uv
        if abs(coord.y-extreme)>1e-4:continue
        for sample_u in(0,.5,1):
            if abs(coord.x-sample_u)>1e-6 or sample_u in found:continue
            position=ob.matrix_world@data.vertices[loop.vertex_index].co
            normal=(normal_matrix@data.corner_normals[loop.index].vector).normalized()
            rgba=tuple(attr.data[loop.index].color)
            found[sample_u]=dict(position=list(position),normal=list(normal),linear_rgba=list(rgba),srgb_rgb=[srgb(v)for v in rgba[:3]])
    assert len(found)==3,(name,found.keys())
    return extreme,found

results=[]
for label,stream_name,fall_name in [('lower','Amber_DownstreamCreek_0','Amber_VoluminousFallingWater_0_0'),('R3_canyon','Amber_DownstreamCreek_1','Amber_VoluminousFallingWater_1_0'),('raised_side','RA_Amber_SideCascade_RaisedStreamAndRoll','RA_Amber_SideCascade_RaisedFall_Layer0')]:
    stream_v,stream=row(stream_name,True);fall_v,fall=row(fall_name,False);samples=[]
    for u in(0,.5,1):
        a,b=stream[u],fall[u];normal_dot=Vector(a['normal']).dot(Vector(b['normal']))
        samples.append(dict(u=u,position_delta=math.dist(a['position'],b['position']),normal_dot=normal_dot,
            linear_rgb_delta=math.dist(a['linear_rgba'][:3],b['linear_rgba'][:3]),srgb_rgb_delta=math.dist(a['srgb_rgb'],b['srgb_rgb']),stream=a,fall=b))
    results.append(dict(id=label,stream=stream_name,fall=fall_name,stream_v=stream_v,fall_v=fall_v,samples=samples,
        normal_dot_min=min(s['normal_dot']for s in samples),normal_dot_max=max(s['normal_dot']for s in samples)))
out=dict(source='frozen amber_winds_preview.blend; no model edits or export',seams=results)
(HERE/'water_seam_shading_diagnosis.json').write_text(json.dumps(out,indent=2))
for seam in results:
    s=seam['samples'][1]
    print(json.dumps(dict(id=seam['id'],normal_dots=[p['normal_dot']for p in seam['samples']],positions=[p['position_delta']for p in seam['samples']],stream_srgb=s['stream']['srgb_rgb'],fall_srgb=s['fall']['srgb_rgb'],linear_rgb_delta=s['linear_rgb_delta'],srgb_rgb_delta=s['srgb_rgb_delta'])),flush=True)
