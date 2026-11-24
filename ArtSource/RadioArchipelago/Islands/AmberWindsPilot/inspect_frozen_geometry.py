"""Independent Blender inspection of the exported scene, without altering it."""
import bpy,runpy,pathlib,json,collections,math
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
env=runpy.run_path(str(HERE/'build_amber_winds.py'),run_name='amber_geometry_inspector')
bpy.ops.wm.open_mainfile(filepath=str(HERE/'amber_winds_preview.blend'))
nearest,play=env['nearest'],env['play']
counts=collections.Counter();near=[];minimum=1e9;intruding_shell_faces=0
for obj in bpy.context.scene.objects:
    if obj.type!='MESH':continue
    category=obj.get('amber_bucket','Unknown')
    for poly in obj.data.polygons:
        name=obj.data.materials[poly.material_index].name
        counts[name]+=len(poly.vertices)-2
        if category=='Cliffs':
            centre=sum((obj.matrix_world@obj.data.vertices[i].co for i in poly.vertices),Vector())/len(poly.vertices)
            if math.hypot((centre.x-81)/26,(centre.y-102)/19)<.98:intruding_shell_faces+=1
    if category not in('Architecture','Wood','Metal','Fabric')or obj.get('amber_rotor'):continue
    for vertex in obj.data.vertices:
        p=obj.matrix_world@vertex.co;dist,station=nearest(p.x,p.y)
        if play(station)-1.8<p.z<play(station)+10.5:
            minimum=min(minimum,dist)
            if dist<8.5 and len(near)<30:near.append(dict(node=obj.name,position=list(p),distance=dist,station=station))
mechanisms=json.loads((HERE/'STAGING/RooftopMotion.json').read_text())['mechanisms'];rigids=[]
for rotor in mechanisms:
    ob=bpy.data.objects.get(rotor['nodeName']);assert ob is not None and ob.type=='MESH'
    pivot=rotor['pivot'];actual=ob.matrix_world.translation
    error=math.dist((actual.x,actual.z,-actual.y),(pivot['x'],pivot['y'],pivot['z']))
    assert error<1e-4,(rotor['nodeName'],error)
    assert len(ob.data.materials)==2
    rigids.append(dict(node=ob.name,pivotError=error,triangles=sum(len(p.vertices)-2 for p in ob.data.polygons)))
def water_samples(name):
    ob=bpy.data.objects.get(name);assert ob is not None,name
    return[(ob.matrix_world@ob.data.vertices[loop.vertex_index].co,tuple(ob.data.uv_layers.active.data[loop.index].uv))for loop in ob.data.loops]
seams=[]
for label,stream_name,fall_name in [('lower','Amber_DownstreamCreek_0','Amber_VoluminousFallingWater_0_0'),('R3_canyon','Amber_DownstreamCreek_1','Amber_VoluminousFallingWater_1_0'),('raised_side','RA_Amber_SideCascade_RaisedStreamAndRoll','RA_Amber_SideCascade_RaisedFall_Layer0')]:
    stream=water_samples(stream_name);fall=water_samples(fall_name);sv=max(uv[1]for p,uv in stream);fv=min(uv[1]for p,uv in fall)
    sr=[(p,u)for p,u in stream if abs(u[1]-sv)<1e-4];fr=[(p,u)for p,u in fall if abs(u[1]-fv)<1e-4]
    s0=next(p for p,u in sr if abs(u[0])<1e-6);s1=next(p for p,u in sr if abs(u[0]-1)<1e-6)
    f0=next(p for p,u in fr if abs(u[0])<1e-6);f1=next(p for p,u in fr if abs(u[0]-1)<1e-6)
    centre=(s0+s1)*.5;side=(s1-s0).normalized();forward=Vector((side.y,-side.x,0));bottom_v=max(u[1]for p,u in fall);bottom=[p for p,u in fall if abs(u[1]-bottom_v)<1e-4]
    seam=dict(id=label,stream_node=stream_name,fall_node=fall_name,stream_downstream_uv_metres=sv,curtain_top_uv_metres=fv,uv_v_delta_metres=fv-sv,
      u0_position_error=(f0-s0).length,u1_position_error=(f1-s1).length,width_error=abs((s1-s0).length-(f1-f0).length),
      longitudinal_error=max(abs((p-centre).dot(forward))for p,u in fr),height_error=max(abs(p.z-centre.z)for p,u in fr),
      v_increases_downward=bottom_v>fv and sum(p.z for p in bottom)/len(bottom)<centre.z)
    seam['layer_v_origin_deltas']=[];seam['layer_u_orientation_aligned']=[]
    for layer in range(3):
        layer_samples=water_samples(fall_name[:-1]+str(layer));v0=min(u[1]for p,u in layer_samples);top=[(p,u)for p,u in layer_samples if abs(u[1]-v0)<1e-4]
        p0=next(p for p,u in top if abs(u[0])<1e-6);p1=next(p for p,u in top if abs(u[0]-1)<1e-6)
        seam['layer_v_origin_deltas'].append(v0-sv);seam['layer_u_orientation_aligned'].append((p1-p0).dot(side)>0)
    assert all(abs(x)<1e-4 for x in seam['layer_v_origin_deltas'])and all(seam['layer_u_orientation_aligned']),seam
    assert seam['v_increases_downward']and max(abs(seam[k])for k in('uv_v_delta_metres','u0_position_error','u1_position_error','width_error','longitudinal_error','height_error'))<1e-4,seam
    seams.append(seam)
import water_seam_shading
shading=water_seam_shading.inspect()
result=dict(triangles=sum(counts.values()),material_triangles=dict(counts),rigid_meshes=rigids,canyon_intruding_shell_faces=intruding_shell_faces,upper_creek_fall_seam=seams[1],water_seams=seams,water_seam_shading=shading,
 static_structure_vertex_minimum_route_distance_in_flight_height_band=minimum,
 candidate_vertices_inside_8_5m_structure_envelope=near,
 limitation='Vertex prepass only; native triangle/camera sweep remains mandatory.')
(HERE/'source_geometry_inspection.json').write_text(json.dumps(result,indent=2))
print('FROZEN_GEOMETRY_INSPECTION',json.dumps(result),flush=True)
