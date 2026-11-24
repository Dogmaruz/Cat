"""Source-only shading continuity; positions and UVs are never modified."""
import bpy,bmesh,math,json,hashlib,struct,array
from mathutils import Vector

SYSTEMS=[('lower','Amber_DownstreamCreek_0','Amber_VoluminousFallingWater_0_'),
 ('R3_canyon','Amber_DownstreamCreek_1','Amber_VoluminousFallingWater_1_'),
 ('raised_side','RA_Amber_SideCascade_RaisedStreamAndRoll','RA_Amber_SideCascade_RaisedFall_Layer')]

def smooth(value):
    t=min(1,max(0,value));return t*t*t*(10+t*(-15+6*t))

def samples(ob):
    mesh=ob.data;uv=mesh.uv_layers.active;attr=mesh.color_attributes.get('Color');normal_matrix=ob.matrix_world.to_3x3().inverted().transposed()
    return[(loop.index,ob.matrix_world@mesh.vertices[loop.vertex_index].co,tuple(uv.data[loop.index].uv),
       (normal_matrix@mesh.corner_normals[loop.index].vector).normalized(),tuple(attr.data[loop.index].color))for loop in mesh.loops]

def geometry_uv_signature():
    """Canonical positions/UV associations of EVERY frozen scene mesh."""
    nodes={}
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH':continue
        mesh=ob.data;vertices=array.array('f',[0])*(len(mesh.vertices)*3);mesh.vertices.foreach_get('co',vertices)
        h=hashlib.sha256();h.update(vertices.tobytes());h.update(struct.pack('<16f',*(v for row in ob.matrix_world for v in row)))
        uv=mesh.uv_layers.active
        if uv and (ob.name.startswith('Amber_DownstreamCreek_')or ob.name.startswith('Amber_VoluminousFallingWater_')or ob.name.startswith('RA_Amber_SideCascade_RaisedStream')or ob.name.startswith('RA_Amber_SideCascade_RaisedFall')):
            # Winding changes corner order, never a UV's vertex association.
            records=sorted((loop.vertex_index,float(uv.data[loop.index].uv.x),float(uv.data[loop.index].uv.y))for loop in mesh.loops)
            for record in records:h.update(struct.pack('<Iff',*record))
        elif uv:
            coords=array.array('f',[0])*(len(mesh.loops)*2);uv.data.foreach_get('uv',coords);h.update(coords.tobytes())
            indices=array.array('I',[0])*len(mesh.loops);mesh.loops.foreach_get('vertex_index',indices);h.update(indices.tobytes())
        nodes[ob.name]=dict(sha256=h.hexdigest(),vertices=len(mesh.vertices),corners=len(mesh.loops),triangles=sum(len(p.vertices)-2 for p in mesh.polygons))
    return nodes

def apply(reverse_existing_faces=False):
    bpy.context.view_layer.update();result=[]
    for label,stream_name,fall_prefix in SYSTEMS:
        stream=bpy.data.objects[stream_name];stream_samples=samples(stream);vmax=max(row[2][1]for row in stream_samples)
        row=[s for s in stream_samples if abs(s[2][1]-vmax)<1e-4]
        p0=next(s[1]for s in row if abs(s[2][0])<1e-6);p1=next(s[1]for s in row if abs(s[2][0]-1)<1e-6)
        across=(p1-p0).normalized();shared=Vector((across.y,-across.x,0)).normalized()
        colors=sorted({s[2][0]:s[4]for s in row}.items())
        def joint_color(u):
            if u<=colors[0][0]:return colors[0][1]
            for (a,ca),(b,cb)in zip(colors,colors[1:]):
                if u<=b:
                    t=(u-a)/(b-a);return tuple(x+(y-x)*t for x,y in zip(ca,cb))
            return colors[-1][1]
        objects=[stream]+[bpy.data.objects[fall_prefix+str(layer)]for layer in range(3)]
        for index,ob in enumerate(objects):
            if ob.get('amber_seam_shading_revision')==1:continue
            if index and reverse_existing_faces:
                bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
            data=ob.data;current=samples(ob);normals=[tuple(n.vector)for n in data.corner_normals]
            shared_local=(ob.matrix_world.to_3x3().transposed()@shared).normalized();color_attr=data.color_attributes['Color'];uv=data.uv_layers.active
            start_v=min(s[2][1]for s in current)if index else None
            for loop_index,point,coord,normal,color in current:
                if index:
                    distance=coord[1]-start_v;weight=1-smooth(distance/.8)
                    if distance<=2.0:
                        tint=joint_color(coord[0]);blend=smooth(distance/2.0);color_attr.data[loop_index].color=tuple(a*(1-blend)+b*blend for a,b in zip(tint,color))
                else:distance=vmax-coord[1];weight=1-smooth(distance/.8)
                if weight>0:
                    original=Vector(normals[loop_index]);normals[loop_index]=tuple(original.lerp(shared_local,weight).normalized())
            data.normals_split_custom_set(normals);data.update();ob['amber_seam_shading_revision']=1
        result.append(dict(id=label,shared_source_normal=list(shared),shared_unity_y=shared.z,body_color_blend_metres=2.0,normal_blend_metres=.8))
    bpy.context.view_layer.update();return result

def smoothstep(a,b,x):
    t=min(1,max(0,(x-a)/(b-a)));return t*t*(3-2*t)

def shader_displace(source_position,source_normal,u,seconds,fall):
    """Exact frozen CoralWater.shader Vert math, in aligned Unity coordinates."""
    p=Vector((source_position.x,source_position.z,-source_position.y));n=Vector((source_normal.x,source_normal.z,-source_normal.y));time=seconds*.65
    wave=math.sin(p.x*.48+p.z*.31-time*1.5)*.045+math.sin(p.x*(-.28)+p.z*.71+time*1.1)*.025
    horizontal=smoothstep(.45,.9,abs(n.y))*(1-fall);p.y+=wave*horizontal
    falling=max(fall,1-smoothstep(.35,.85,abs(n.y)))
    return p+n*(math.sin(p.y*.8+time*2.1+u*5)*.065*falling)

def inspect():
    output=[]
    for label,stream_name,fall_prefix in SYSTEMS:
        stream=samples(bpy.data.objects[stream_name]);sv=max(s[2][1]for s in stream);sr=[s for s in stream if abs(s[2][1]-sv)<1e-4]
        fall=samples(bpy.data.objects[fall_prefix+'0']);fv=min(s[2][1]for s in fall);fr=[s for s in fall if abs(s[2][1]-fv)<1e-4]
        comparisons=[];max_gap=0
        for u in(0,.25,.5,.75,1):
            a=next(s for s in sr if abs(s[2][0]-u)<1e-6);b=next(s for s in fr if abs(s[2][0]-u)<1e-6)
            gaps=[(shader_displace(a[1],a[3],u,t,0)-shader_displace(b[1],b[3],u,t,1)).length for t in(0,.13,.37,1,1.7,2.8,5.25,9.8,13.7,21)]
            max_gap=max(max_gap,*gaps)
            comparisons.append(dict(u=u,normal_dot=a[3].dot(b[3]),normal_unity_y_stream=a[3].z,normal_unity_y_fall=b[3].z,
              position_delta=(a[1]-b[1]).length,uv_delta=math.dist(a[2],b[2]),rgba_delta=math.dist(a[4],b[4]),sampled_displacement_gap=max(gaps)))
        layers=[]
        for layer in range(3):
            ob=bpy.data.objects[fall_prefix+str(layer)];fs=samples(ob);v0=min(s[2][1]for s in fs);top=[s for s in fs if abs(s[2][1]-v0)<1e-4]
            mid=next(s for s in top if abs(s[2][0]-.5)<1e-6);smid=next(s for s in sr if abs(s[2][0]-.5)<1e-6)
            layers.append(dict(node=ob.name,has_custom_normals=ob.data.has_custom_normals,centre_normal_dot=mid[3].dot(smid[3]),centre_rgba_delta=math.dist(mid[4],smid[4]),v_delta=v0-sv))
        entry=dict(id=label,comparisons=comparisons,layers=layers,max_displaced_gap=max_gap)
        assert max_gap<1e-4 and all(s['normal_dot']>.99999 and s['rgba_delta']<1e-6 and s['position_delta']<1e-4 and s['uv_delta']<1e-4 for s in comparisons),entry
        assert all(s['centre_normal_dot']>.99999 and s['centre_rgba_delta']<1e-6 and abs(s['v_delta'])<1e-4 for s in layers),entry
        output.append(entry)
    return output
