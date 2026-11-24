"""Single continuous waterfall sheets from the immutable c565 source.

Run with Blender --background --python this_file. No Assets/Editor writes.
Only the three stream + three curtain systems are replaced. The rest of the
island is byte/semantic signed, not rebuilt or re-exported through the builder.
"""
import bpy, pathlib, json, hashlib, math, bisect, array, struct
from mathutils import Vector

HERE = pathlib.Path(__file__).resolve().parent
SOURCE = HERE.parent
OUT = HERE / 'STAGING'
EXPECTED = 'c5656b305295a74bffc93d0428b111a95e6768e0ef42b9cd956ec612ac88fc3a'
SYSTEMS = [
    ('lower', 'Amber_DownstreamCreek_0', 'Amber_VoluminousFallingWater_0_'),
    ('R3_canyon', 'Amber_DownstreamCreek_1', 'Amber_VoluminousFallingWater_1_'),
    ('raised_side', 'RA_Amber_SideCascade_RaisedStreamAndRoll', 'RA_Amber_SideCascade_RaisedFall_Layer'),
]
COLS = 32

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def signature(obj):
    """Complete render-relevant source signature (including all corner normals)."""
    h = hashlib.sha256()
    h.update(struct.pack('<16f', *(v for row in obj.matrix_world for v in row)))
    h.update(json.dumps(dict(parent=obj.parent.name if obj.parent else None,
        props={k: obj[k] for k in obj.keys()}, type=obj.type), sort_keys=True).encode())
    if obj.type != 'MESH': return h.hexdigest()
    data = obj.data
    for collection, prop, width, fmt in [(data.vertices,'co',3,'f'),(data.loops,'vertex_index',1,'I')]:
        a = array.array(fmt, [0]) * (len(collection)*width)
        collection.foreach_get(prop,a); h.update(a.tobytes())
    for p in data.polygons: h.update(struct.pack('<4I',p.loop_start,p.loop_total,p.material_index,int(p.use_smooth)))
    for uv in data.uv_layers:
        h.update(uv.name.encode()); a=array.array('f',[0])*(len(uv.data)*2);uv.data.foreach_get('uv',a);h.update(a.tobytes())
    for attr in data.color_attributes:
        h.update((attr.name+attr.domain+attr.data_type).encode());a=array.array('f',[0])*(len(attr.data)*4);attr.data.foreach_get('color',a);h.update(a.tobytes())
    for n in data.corner_normals: h.update(struct.pack('<3f',*n.vector))
    h.update(json.dumps([m.name if m else None for m in data.materials]).encode())
    return h.hexdigest()

def rings(obj):
    uv=obj.data.uv_layers.active; byv={}
    for loop in obj.data.loops:
        u,v=uv.data[loop.index].uv; p=obj.matrix_world @ obj.data.vertices[loop.vertex_index].co
        byv.setdefault(round(float(v),5),{}).setdefault(round(float(u),6),p)
    return [(v,sorted(row.items())) for v,row in sorted(byv.items())]

def at_u(row,u):
    us=[item[0] for item in row]
    i=min(max(bisect.bisect_right(us,u)-1,0),len(row)-2)
    a,pa=row[i];b,pb=row[i+1]
    return pa.lerp(pb,min(1,max(0,(u-a)/(b-a))))

def resample_ring(row): return [at_u(row,j/COLS) for j in range(COLS+1)]
def centre(row): return row[COLS//2]

def lower_roll(srows,frows):
    """The old lower fall had a square lip; give it a real quarter-circle."""
    end=centre(srows[-1][1]);radius=1.5
    forward=centre(srows[-1][1])-centre(srows[-2][1]);forward.z=0;forward.normalize()
    cross=(srows[-1][1][-1]-srows[-1][1][0]);width=cross.length;cross.normalize()
    cutoff=srows[-1][0]-radius
    prior=[(v,row) for v,row in srows if v<cutoff-.0001]
    arcstart=end-forward*radius
    for i in range(25):
        theta=i/24*math.pi/2
        c=arcstart+forward*(radius*math.sin(theta))+Vector((0,0,-radius*(1-math.cos(theta))))
        row=[c+cross*((j/COLS-.5)*width) for j in range(COLS+1)]
        prior.append((cutoff+radius*theta,row))
    # Original sheet from 1.5m below lip continues without changing its base.
    remain=[(v,row)for v,row in frows if centre(row).z<end.z-radius-.0001]
    return prior+remain

def dense_rows(rows):
    """A half-metre longitudinal grid supports a soft animated silhouette."""
    result=[]
    for (_,a),(_,b) in zip(rows,rows[1:]):
        steps=max(1,math.ceil((centre(b)-centre(a)).length/.5))
        for k in range(steps):result.append([p.lerp(q,k/steps)for p,q in zip(a,b)])
    result.append(rows[-1][1]);return result

def export():
    bpy.ops.object.select_all(action='DESELECT')
    for ob in bpy.context.scene.objects:
        if ob.type in ('MESH','EMPTY'):ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/'amber_winds_island.fbx'),use_selection=True,
        object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,
        use_mesh_modifiers=False,use_triangles=True,mesh_smooth_type='OFF',bake_anim=False,
        add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')

def run():
    assert sha(SOURCE/'STAGING/amber_winds_island.fbx')==EXPECTED,'Frozen source model changed'
    source_files={str(p.relative_to(SOURCE)):sha(p)for p in [SOURCE/'amber_winds_preview.blend',*sorted((SOURCE/'STAGING').glob('*.json'))]}
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'amber_winds_preview.blend'))
    bpy.context.view_layer.update()
    affected={stream for _,stream,_ in SYSTEMS}|{prefix+str(i)for _,_,prefix in SYSTEMS for i in range(3)}
    before={ob.name:signature(ob)for ob in bpy.context.scene.objects if ob.name not in affected}
    records=[]
    for label,stream_name,prefix in SYSTEMS:
        stream=bpy.data.objects[stream_name];fall=bpy.data.objects[prefix+'0']
        srings=[(v,resample_ring(row))for v,row in rings(stream)]
        frings=[(v,resample_ring(row))for v,row in rings(fall)]
        assert abs(srings[-1][0]-frings[0][0])<1e-4
        restgap=max((a-b).length for a,b in zip(srings[-1][1],frings[0][1]))
        assert restgap<1e-4,(label,restgap)
        rows=lower_roll(srings,frings) if label=='lower' else srings+frings[1:]
        rr=dense_rows(rows);travel=rows[0][0];vmetres=[]
        for i,row in enumerate(rr):
            if i:travel+=(centre(row)-centre(rr[i-1])).length
            vmetres.append(travel)
        vertices=[tuple(p)for row in rr for p in row];uvs=[(j/COLS,v)for v in vmetres for j in range(COLS+1)];faces=[]
        for i in range(len(rr)-1):
            for j in range(COLS):
                a=i*(COLS+1)+j;faces.append((a,a+1,a+COLS+2,a+COLS+1))
        # Up-facing in the source horizontal reach; coherent winding follows
        # the same sheet through the arc and the entire falling curtain.
        a,b,c=[Vector(vertices[x])for x in faces[0][:3]]
        if (b-a).cross(c-a).z<0:faces=[tuple(reversed(f))for f in faces]
        data=bpy.data.meshes.new(prefix+'0_ContinuousSheet');data.from_pydata(vertices,[],faces);data.update()
        data.materials.append(bpy.data.materials['RA_Amber_Waterfall'])
        uv=data.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',[v for loop in data.loops for v in uvs[loop.vertex_index]])
        rgba=data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
        colors=[]
        for loop in data.loops:
            row=loop.vertex_index//(COLS+1);remaining=vmetres[-1]-vmetres[row]
            t=min(1,max(0,remaining/3));alpha=t*t*(3-2*t)
            colors.extend((.15,.55,.57,alpha))
        rgba.data.foreach_set('color',colors)
        for poly in data.polygons:poly.use_smooth=True
        data.update()
        name=fall.name;parent=fall.parent
        for old in [stream]+[bpy.data.objects[prefix+str(i)]for i in range(3)]:bpy.data.objects.remove(old,do_unlink=True)
        ob=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(ob);ob.parent=parent
        ob['amber_bucket']='Waterfall';ob['amber_feature_node']=True;ob['amber_continuous_flow']=True
        ob['source_stream_alias']=stream_name;ob['continuous_bottom_fade_metres']=3.0
        # A watertight grid is unnecessary for a thin water sheet; all internal
        # edges including the old joint must be connected to exactly2faces.
        edges={}
        for face in data.polygons:
            ids=list(face.vertices)
            for x,y in zip(ids,ids[1:]+ids[:1]):key=tuple(sorted((x,y)));edges[key]=edges.get(key,0)+1
        bad=[key for key,n in edges.items()if n not in(1,2)];assert not bad
        boundary=sum(n==1 for n in edges.values());assert boundary==2*COLS+2*(len(rr)-1)
        def unity(p):return[p.x,p.z,-p.y]
        # Anchor where downward curtain starts, not at upstream source mouth.
        liptarget=centre(srings[-1][1])+Vector((0,0,-1.5)) if label=='lower' else centre(srings[-1][1])
        lipidx=min(range(len(rr)),key=lambda i:(centre(rr[i])-liptarget).length)
        records.append(dict(id=label,node=name,removed_aliases=[stream_name,prefix+'1',prefix+'2'],
            material='RA_Amber_Waterfall',color=[.15,.55,.57],alpha='Smoothstep remaining downstream metres /3',
            rows=len(rr),columns=COLS,vertices=len(vertices),triangles=len(faces)*2,
            source_top=tuple(centre(rr[0])),source_lip=tuple(centre(rr[lipidx])),source_bottom=tuple(centre(rr[-1])),
            unity_top=unity(centre(rr[0])),unity_lip=unity(centre(rr[lipidx])),unity_bottom=unity(centre(rr[-1])),
            maximum_width=max((row[-1]-row[0]).length for row in rr),bottom_width=(rr[-1][-1]-rr[-1][0]).length,
            uv_v_range=[vmetres[0],vmetres[-1]],shared_internal_edges=True,components=1,
            roll_radius=1.5 if label in('lower','raised_side') else 3,
            old_join_position_error=restgap))
    bpy.context.view_layer.update()
    after={ob.name:signature(ob)for ob in bpy.context.scene.objects if not ob.get('amber_continuous_flow')}
    assert before==after,'A non-water source object changed'
    assert all(sha(SOURCE/path)==digest for path,digest in source_files.items())
    OUT.mkdir(parents=True,exist_ok=True)
    for name in ('amber_winds_route.json','play_elevation.json','ground_elevation.json','cloud_steps.json','RooftopMotion.json'):
        (OUT/name).write_bytes((SOURCE/'STAGING'/name).read_bytes())
    export();bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'amber_winds_continuous.blend'))
    assert all(sha(SOURCE/path)==digest for path,digest in source_files.items())
    report=dict(input_model_sha256=EXPECTED,input_files=source_files,source_model_unmodified=True,
        output_model_sha256=sha(OUT/'amber_winds_island.fbx'),output_bytes=(OUT/'amber_winds_island.fbx').stat().st_size,
        source_mesh_nodes=sum(ob.type=='MESH'for ob in bpy.context.scene.objects),
        unchanged_non_water_objects=len(before),non_water_objects_before=before,non_water_objects_after=after,
        sheets=records,sidecars={p.name:sha(p)for p in OUT.glob('*.json')})
    (HERE/'continuous_sheet_handoff.json').write_text(json.dumps(report,indent=2))
    print('CONTINUOUS_SHEETS_COMPLETE',json.dumps({k:v for k,v in report.items()if not k.startswith('non_water_objects')}),flush=True)

run()
