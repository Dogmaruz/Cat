"""Source-only Coral/Moon waterfall migration; current non-water geometry is frozen."""
import bpy, pathlib, json, hashlib, math, struct, array, sys, collections
from mathutils import Vector, Matrix

HERE=pathlib.Path(__file__).resolve().parent
ISLANDS=HERE.parent
SOURCE=ISLANDS/'RooftopMechanisms20261101/STAGING'
CONFIG={
 'Coral':dict(blend='coral_rooftops.blend',fbx='coral_oasis_island.fbx',expected='6fb98555cb33402282d55f41eeb7e064c4d812d72083a999d85d7c478c9da7b2'),
 'Moon':dict(blend='moon_rooftops.blend',fbx='moon_resonance_island.fbx',expected='c2407e489e3ec0e85322e66897e8379f3828b143d09111781df8944fc38912be')
}
COLS=32
sha=lambda p:hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()

def sign(ob):
    h=hashlib.sha256();h.update(struct.pack('<16f',*(x for row in ob.matrix_world for x in row)))
    h.update(json.dumps(dict(parent=ob.parent.name if ob.parent else None,type=ob.type,props={k:ob[k]for k in ob.keys()}),sort_keys=True).encode())
    if ob.type!='MESH':return h.hexdigest()
    m=ob.data
    for coll,key,width,fmt in [(m.vertices,'co',3,'f'),(m.loops,'vertex_index',1,'I')]:
        a=array.array(fmt,[0])*(len(coll)*width);coll.foreach_get(key,a);h.update(a.tobytes())
    for p in m.polygons:h.update(struct.pack('<4I',p.loop_start,p.loop_total,p.material_index,int(p.use_smooth)))
    for uv in m.uv_layers:
        h.update(uv.name.encode());a=array.array('f',[0])*(len(uv.data)*2);uv.data.foreach_get('uv',a);h.update(a.tobytes())
    for attr in m.color_attributes:
        h.update((attr.name+attr.domain+attr.data_type).encode());a=array.array('f',[0])*(len(attr.data)*4);attr.data.foreach_get('color',a);h.update(a.tobytes())
    for n in m.corner_normals:h.update(struct.pack('<3f',*n.vector))
    h.update(json.dumps([m.name if m else None for m in m.materials]).encode());return h.hexdigest()

def freeze_materials():
    return {m.name:dict(diffuse=list(m.diffuse_color),nodes=[(n.name,n.type)for n in m.node_tree.nodes] if m.node_tree else [])for m in bpy.data.materials}

def clip_tail(ob,axis,cut,keep_greater):
    """Only a planar, isolated outlet tail is removed. All other faces survive."""
    data=ob.data;uv=data.uv_layers.active;colors=data.color_attributes.active_color
    verts=[];faces=[];uvs=[];rgba=[];normals=[];kept=clipped=removed=0
    def distance(v):return (v[0][axis]-cut)*(1 if keep_greater else -1)
    for poly in data.polygons:
        row=[]
        for li in poly.loop_indices:
            row.append((ob.matrix_world@data.vertices[data.loops[li].vertex_index].co,uv.data[li].uv.copy(),tuple(colors.data[li].color),data.corner_normals[li].vector.copy()))
        ds=[distance(v)for v in row]
        if min(ds)>=-1e-7:kept+=1
        elif max(ds)<-1e-7:removed+=1;continue
        else:
            clipped+=1;clipped_row=[]
            for a,b in zip(row,row[1:]+row[:1]):
                da,db=distance(a),distance(b)
                if da>=0:clipped_row.append(a)
                if (da>=0)!=(db>=0):
                    t=da/(da-db)
                    clipped_row.append((a[0].lerp(b[0],t),a[1].lerp(b[1],t),tuple(x+(y-x)*t for x,y in zip(a[2],b[2])),a[3].lerp(b[3],t).normalized()))
            row=clipped_row
        if len(row)<3:continue
        start=len(verts)
        for p,u,c,n in row:verts.append(tuple(p));uvs.append(tuple(u));rgba.append(c);normals.append(tuple(n))
        faces.append(tuple(range(start,start+len(row))))
    name=ob.name;parent=ob.parent;props={k:ob[k]for k in ob.keys()};old_mats=list(data.materials)
    if not faces:
        bpy.data.objects.remove(ob,do_unlink=True)
        return dict(node=name,retained_faces=kept,clipped_faces=clipped,removed_faces=removed,node_removed=True)
    mesh=bpy.data.meshes.new(name+'_OutletTailClipped');mesh.from_pydata(verts,[],faces);mesh.update()
    for mat in old_mats:mesh.materials.append(mat)
    layer=mesh.uv_layers.new(name='UVMap');layer.data.foreach_set('uv',[v for loop in mesh.loops for v in uvs[loop.vertex_index]])
    attr=mesh.color_attributes.new(name=colors.name,type='FLOAT_COLOR',domain='CORNER');attr.data.foreach_set('color',[v for loop in mesh.loops for v in rgba[loop.vertex_index]])
    for p in mesh.polygons:p.use_smooth=True
    mesh.normals_split_custom_set([normals[l.vertex_index]for l in mesh.loops])
    ob.data=mesh;ob.matrix_world=Matrix.Identity(4)
    assert min((v.co[axis]-cut)*(1 if keep_greater else -1)for v in mesh.vertices)>-1e-4
    return dict(node=name,retained_faces=kept,clipped_faces=clipped,removed_faces=removed,node_removed=False)

def sample_line(a,b):
    a,b=Vector(a),Vector(b);n=max(1,math.ceil((b-a).length/.30))
    return [a.lerp(b,i/n)for i in range(n)]

def rounded_tail(points):
    pts=[Vector(p)for p in points];out=[pts[0]];last=pts[0]
    for i in range(1,len(pts)-1):
        p=pts[i];before=(p-pts[i-1]);after=(pts[i+1]-p)
        trim=min(1.6,before.length*.3,after.length*.3)
        a=p-before.normalized()*trim;b=p+after.normalized()*trim
        out.extend(sample_line(last,a)[1:]);out.append(a)
        n=max(8,math.ceil((b-a).length/.2))
        out.extend([a*((1-t)**2)+p*(2*t*(1-t))+b*(t*t)for t in [j/n for j in range(1,n+1)]])
        last=b
    out.extend(sample_line(last,pts[-1])[1:]);out.append(pts[-1]);return out

def centres(head,arcstart,forward,radius,tail):
    head,arcstart,forward=Vector(head),Vector(arcstart),Vector(forward).normalized()
    d=(arcstart-head).length
    # A horizontal cubic meets the round lip tangentially. Its initial tangent
    # points down the existing channel; high Coral gently bends toward its lip.
    initial=(arcstart-head).normalized();p1=head+initial*d*.34;p2=arcstart-forward*d*.34
    top=[];n=max(12,math.ceil(d/.25))
    for j in range(n):
        t=j/n;top.append(head*(1-t)**3+p1*3*(1-t)**2*t+p2*3*(1-t)*t*t+arcstart*t**3)
    for j in range(33):
        theta=j/32*math.pi/2
        top.append(arcstart+forward*(radius*math.sin(theta))+Vector((0,0,-radius*(1-math.cos(theta)))))
    bottom=top[-1]
    # Start vertically, preserving tangent continuity at the quarter-circle.
    rest=rounded_tail([bottom,bottom+Vector((0,0,-.5)),*[Vector(p)for p in tail]])
    return top+rest[1:]

def sheet(label,spec):
    old=bpy.data.objects[spec['node']];name=old.name;parent=old.parent;props={k:old[k]for k in old.keys()}
    mat=old.data.materials[0]
    points=centres(spec['head'],spec['arcstart'],spec['forward'],spec['radius'],spec['tail'])
    across=Vector(spec['across']).normalized();dist=[0]
    for a,b in zip(points,points[1:]):dist.append(dist[-1]+(b-a).length)
    # Width increases only after the top of the roll where a Moon sheet leaves
    # its original narrow outlet. The whole upstream overlap stays inside water.
    arc_distance=min(range(len(points)),key=lambda i:(points[i]-Vector(spec['arcstart'])).length)
    rollstart=dist[arc_distance];width0,width1=spec['widths']
    verts=[];uvs=[];rgba=[]
    for i,(p,v)in enumerate(zip(points,dist)):
        w=width0+(width1-width0)*min(1,max(0,(v-rollstart)/(spec['radius']*1.6)))
        fade=min(1,max(0,(dist[-1]-v)/3));fade=fade*fade*(3-2*fade)
        # A .6m upstream overlap smoothly joins the unchanged pool shader.
        entry=min(1,max(0,v/.6));entry=entry*entry*(3-2*entry)
        for j in range(COLS+1):
            u=j/COLS;verts.append(tuple(p+across*((u-.5)*w)));uvs.append((u,v));rgba.append((.15,.55,.57,fade*entry))
    faces=[]
    for i in range(len(points)-1):
        for j in range(COLS):a=i*(COLS+1)+j;faces.append((a,a+1,a+COLS+2,a+COLS+1))
    a,b,c=[Vector(verts[i])for i in faces[0][:3]]
    if (b-a).cross(c-a).z<0:faces=[tuple(reversed(f))for f in faces]
    data=bpy.data.meshes.new(name+'_ContinuousFlow');data.from_pydata(verts,[],faces);data.update();data.materials.append(mat)
    uv=data.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',[v for loop in data.loops for v in uvs[loop.vertex_index]])
    color=data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');color.data.foreach_set('color',[v for loop in data.loops for v in rgba[loop.vertex_index]])
    for poly in data.polygons:poly.use_smooth=True
    data.update();bpy.data.objects.remove(old,do_unlink=True)
    ob=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(ob);ob.parent=parent;ob.matrix_world=Matrix.Identity(4)
    for k,v in props.items():ob[k]=v
    ob['continuous_flow']=True;ob['continuous_bottom_fade_metres']=3.0;ob['continuous_upstream_blend_metres']=.6
    edges=collections.Counter(tuple(sorted((a,b)))for p in data.polygons for a,b in zip(p.vertices,list(p.vertices[1:])+[p.vertices[0]]))
    assert all(v in(1,2)for v in edges.values())
    assert sum(v==1 for v in edges.values())==2*COLS+2*(len(points)-1)
    def unity(p):return [p.x,p.z,-p.y]
    return dict(id=spec['id'],node=name,material=mat.name,rows=len(points),columns=COLS,triangles=len(faces)*2,
        vertices=len(verts),uv_u=[0,1],uv_v=[0,dist[-1]],alpha='smoothstep upstream .6m and downstream final3m',
        source_head=list(points[0]),source_lip=list(points[arc_distance]),source_curtain_top=list(points[arc_distance+32]),source_bottom=list(points[-1]),
        unity_head=unity(points[0]),unity_lip=unity(points[arc_distance]),unity_curtain_top=unity(points[arc_distance+32]),unity_bottom=unity(points[-1]),
        widths=[width0,width1],roll_radius=spec['radius'],components=1,shared_internal_edges=True,receiving_basin=False)

def specs(label):
    z=.006
    if label=='Coral':
        return [dict(id='high_spring',node='RA_Coral_Waterfall_+01_-01_01',head=(67,-10,6.72+z),arcstart=(68.6,-13,6.72+z),forward=(.70710678,-.70710678,0),radius=1.8,across=(1,0,0),widths=(2.3,2.3),tail=[(70,-15,2),(70,-15,-6),(69,-15,-18),(70,-16,-34)]),
            dict(id='low_outlet',node='RA_Coral_Waterfall_+01_-03_01',head=(44,-67.4,.45+z),arcstart=(44,-72.4,.45+z),forward=(0,-1,0),radius=1.6,across=(1,0,0),widths=(3.3,3.4),tail=[(44,-74,-7),(45,-74,-18),(45,-75,-31)])]
    low=134.2438201904297;high=37.02647399902344
    return [dict(id='low_lagoon',node='RA_Moon_Waterfall_+02_+01_01',head=(low-4.6,55,.82+z),arcstart=(low-.6,55,.82+z),forward=(1,0,0),radius=1.8,across=(0,1,0),widths=(3.9,5.2),tail=[(low+2.5,55,-7),(low+2.5,55,-20),(low+3.5,55,-38),(low+2.5,55,-58)]),
        dict(id='upper_spring',node='RA_Moon_Waterfall_+00_+03_01',head=(high-4.8,174,14.44+z),arcstart=(high-.5,174,14.44+z),forward=(1,0,0),radius=1.5,across=(0,1,0),widths=(3.6,3.7),tail=[(high+1.8,174,11),(high+2,174,3),(high+3,174,-12),(high+2,174,-32),(high+3,174,-54)])]

def run(label):
    cfg=CONFIG[label];src=SOURCE/label;out=HERE/'STAGING'/label;out.mkdir(parents=True,exist_ok=True)
    assert sha(src/cfg['fbx'])==cfg['expected']
    frozen={str(p):sha(p)for p in (src/cfg['fbx'],src/cfg['blend'],src/'RooftopMotion.json')}
    bpy.ops.wm.open_mainfile(filepath=str(src/cfg['blend']));bpy.context.view_layer.update()
    cases=specs(label);targets={s['node']for s in cases}
    pool_targets=['RA_Coral_Water_+01_-02_01','RA_Coral_Water_+01_-03_01']if label=='Coral'else['RA_Moon_Water_+02_+01_01','RA_Moon_Water_+00_+03_01']
    targets.update(pool_targets)
    before={ob.name:sign(ob)for ob in bpy.context.scene.objects if ob.name not in targets}
    clipped=[]
    for node in pool_targets:
        if label=='Coral':clipped.append(clip_tail(bpy.data.objects[node],1,-68,True))
        else:clipped.append(clip_tail(bpy.data.objects[node],0,(134.2438201904297-4)if'02_+01'in node else(37.02647399902344-4.2),False))
    records=[sheet(label,spec)for spec in cases]
    bpy.context.view_layer.update();after={ob.name:sign(ob)for ob in bpy.context.scene.objects if ob.name not in targets}
    assert before==after,'Non-target scenery changed'
    bpy.ops.object.select_all(action='DESELECT')
    for ob in bpy.context.scene.objects:
        if ob.type in('MESH','EMPTY'):ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(out/cfg['fbx']),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,
        mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
    bpy.ops.wm.save_as_mainfile(filepath=str(out/(label.lower()+'_continuous_waterfalls.blend')))
    (out/'RooftopMotion.json').write_bytes((src/'RooftopMotion.json').read_bytes())
    assert all(sha(p)==h for p,h in frozen.items())
    report=dict(level=label,input_model=str(src/cfg['fbx']),input_sha256=cfg['expected'],source_inputs_unchanged=True,frozen_inputs=frozen,
        output_model=str(out/cfg['fbx']),output_sha256=sha(out/cfg['fbx']),output_bytes=(out/cfg['fbx']).stat().st_size,
        untouched_objects=len(before),untouched_before=before,untouched_after=after,affected_nodes=sorted(targets),pool_tail_clips=clipped,
        sheets=records,mesh_nodes=sum(ob.type=='MESH'for ob in bpy.context.scene.objects))
    (out/'handoff.json').write_text(json.dumps(report,indent=2))
    print('CONTINUOUS_WATERFALL_SOURCE_READY',json.dumps({k:v for k,v in report.items()if not k.startswith('untouched_')}),flush=True)

requested=sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else list(CONFIG)
for label in requested:run(label)
