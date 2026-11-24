"""Independent old/new FBX roundtrip audit. No Assets/Editor writes."""
import bpy,pathlib,json,hashlib,struct,array,collections
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
SOURCE=HERE.parent/'RooftopMechanisms20261101/STAGING'
CONFIG={'Coral':'coral_oasis_island.fbx','Moon':'moon_resonance_island.fbx'}

def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True,use_custom_props=True)
    bpy.context.view_layer.update()

def sign(ob):
    h=hashlib.sha256();h.update(struct.pack('<16f',*(x for row in ob.matrix_world for x in row)))
    if ob.type!='MESH':return h.hexdigest()
    m=ob.data
    for coll,key,width,fmt in [(m.vertices,'co',3,'f'),(m.loops,'vertex_index',1,'I')]:
        a=array.array(fmt,[0])*(len(coll)*width);coll.foreach_get(key,a);h.update(a.tobytes())
    for p in m.polygons:h.update(struct.pack('<4I',p.loop_start,p.loop_total,p.material_index,int(p.use_smooth)))
    for uv in m.uv_layers:
        a=array.array('f',[0])*(len(uv.data)*2);uv.data.foreach_get('uv',a);h.update(a.tobytes())
    for attr in m.color_attributes:
        a=array.array('f',[0])*(len(attr.data)*4);attr.data.foreach_get('color',a);h.update(a.tobytes())
    for n in m.corner_normals:h.update(struct.pack('<3f',*n.vector))
    h.update(json.dumps([m.name if m else None for m in m.materials]).encode());return h.hexdigest()

def water_audit(ob):
    m=ob.data;uv=m.uv_layers.active;normals=m.corner_normals
    edges=collections.Counter();bad=zero=0;minimum=1;directions=[];adj=collections.defaultdict(set)
    for face in m.polygons:
        for li in face.loop_indices:
            n=normals[li].vector;zero+=n.length<.99;d=face.normal.dot(n);bad+=d<0;minimum=min(minimum,d)
        ids=list(face.vertices)
        for a,b in zip(ids,ids[1:]+ids[:1]):
            edges[tuple(sorted((a,b)))]+=1;adj[a].add(b);adj[b].add(a)
        us=[uv.data[li].uv for li in face.loop_indices]
        ps=[ob.matrix_world@m.vertices[m.loops[li].vertex_index].co for li in face.loop_indices]
        a,b,c=us[:3];dp0,dp1=ps[1]-ps[0],ps[2]-ps[0];du0,du1=b-a,c-a;det=du0.x*du1.y-du0.y*du1.x
        assert abs(det)>1e-8,(ob.name,'degenerate UV triangle')
        along=(dp1*du0.x-dp0*du1.x)/det
        normal=(ob.matrix_world.to_3x3().inverted().transposed()@face.normal).normalized()
        if abs(normal.z)<.15:directions.append(along.z)
    unseen=set(adj);components=0
    while unseen:
        components+=1;stack=[unseen.pop()]
        while stack:
            for q in adj[stack.pop()]:
                if q in unseen:unseen.remove(q);stack.append(q)
    assert components==1,(ob.name,components)
    assert all(n in(1,2)for n in edges.values())
    assert not bad and not zero,(ob.name,bad,zero,minimum)
    assert directions and max(directions)<-.5,(ob.name,'notdownstream',max(directions))
    assert len(m.materials)==1
    return dict(node=ob.name,vertices=len(m.vertices),faces=len(m.polygons),components=components,one_material=True,material=m.materials[0].name,
        negative_corner_normals=bad,nonunit_corner_normals=zero,minimum_face_corner_dot=minimum,
        vertical_downstream_derivative_z_range=[min(directions),max(directions)],
        color_alpha_range=[min(v.color[3]for v in m.color_attributes.active_color.data),max(v.color[3]for v in m.color_attributes.active_color.data)])

def pool_faces(ob,axis,cut,keep_greater,only_unchanged):
    """Exact rendered-corner payloads, independent of remapped vertex indices."""
    m=ob.data;uv=m.uv_layers.active;colors=m.color_attributes.active_color;out=collections.Counter()
    for face in m.polygons:
        points=[ob.matrix_world@m.vertices[m.loops[li].vertex_index].co for li in face.loop_indices]
        if only_unchanged and min((p[axis]-cut)*(1 if keep_greater else -1)for p in points)<1e-5:continue
        records=[]
        for li,p in zip(face.loop_indices,points):
            normal=(ob.matrix_world.to_3x3().inverted().transposed()@m.corner_normals[li].vector).normalized()
            payload=list(p)+list(uv.data[li].uv)+list(colors.data[li].color)
            records.append(tuple(round(v,6)for v in payload))
        out[tuple(sorted(records))]+=1
    return out

def pool_normals(ob,axis,cut,keep_greater,only_unchanged):
    m=ob.data;out={}
    for face in m.polygons:
        ps=[ob.matrix_world@m.vertices[m.loops[li].vertex_index].co for li in face.loop_indices]
        if only_unchanged and min((p[axis]-cut)*(1 if keep_greater else -1)for p in ps)<1e-5:continue
        corners=sorted([(tuple(round(v,6)for v in p),tuple(m.corner_normals[li].vector))for p,li in zip(ps,face.loop_indices)])
        out[tuple(p for p,n in corners)]=[n for p,n in corners]
    return out

results=[]
for label,file in CONFIG.items():
    folder=HERE/'STAGING'/label;handoff=json.loads((folder/'handoff.json').read_text());affected=set(handoff['affected_nodes'])
    load(SOURCE/label/file);old={ob.name:sign(ob)for ob in bpy.context.scene.objects if ob.name not in affected}
    pool_specs=[('RA_Coral_Water_+01_-02_01',1,-68,True)] if label=='Coral' else [('RA_Moon_Water_+02_+01_01',0,134.2438201904297-4,False),('RA_Moon_Water_+00_+03_01',0,37.02647399902344-4.2,False)]
    pool_before={node:pool_faces(bpy.data.objects[node],axis,cut,greater,True)for node,axis,cut,greater in pool_specs}
    normals_before={node:pool_normals(bpy.data.objects[node],axis,cut,greater,True)for node,axis,cut,greater in pool_specs}
    load(folder/file);new={ob.name:sign(ob)for ob in bpy.context.scene.objects if ob.name not in affected}
    assert old==new,dict(level=label,added=sorted(set(new)-set(old)),removed=sorted(set(old)-set(new)),changed=[n for n in old if n in new and old[n]!=new[n]])
    pool_reports=[]
    for node,axis,cut,greater in pool_specs:
        full=pool_faces(bpy.data.objects[node],axis,cut,greater,False)
        missing=pool_before[node]-full
        assert not missing,(label,node,'pool outside outlet changed',len(missing))
        after_normals=pool_normals(bpy.data.objects[node],axis,cut,greater,False)
        max_normal_delta=max(abs(x-y)for key,ns in normals_before[node].items()for a,b in zip(ns,after_normals[key])for x,y in zip(a,b))
        assert max_normal_delta<.001,(label,node,'excessive custom normal recompression',max_normal_delta)
        pool_reports.append(dict(node=node,unclipped_original_faces=sum(pool_before[node].values()),unclipped_positions_uv_colors_exact=True,
            maximum_normal_component_delta=max_normal_delta,normal_delta_bound=.001,
            normal_note='Copied custom normals are compressed again by Blender when the tiny outlet-tail target batch is rebuilt; this bound is explicit, not called exact.'))
    sheets=[water_audit(bpy.data.objects[s['node']])for s in handoff['sheets']]
    result=dict(level=label,passed=True,sha256=hashlib.sha256((folder/file).read_bytes()).hexdigest(),untouched_render_signatures_identical=True,
        untouched_objects=len(old),affected_nodes=handoff['affected_nodes'],sheets=sheets,
        unchanged_rooftop_manifest=(folder/'RooftopMotion.json').read_bytes()==(SOURCE/label/'RooftopMotion.json').read_bytes(),
        preserved_pool_geometry=pool_reports,
        old_nonwater_signatures=old,new_nonwater_signatures=new)
    (folder/'export_audit.json').write_text(json.dumps(result,indent=2));results.append(result)
    print('CONTINUOUS_WATERFALL_FBX_AUDIT_PASS',json.dumps({k:v for k,v in result.items()if not k.endswith('signatures')}),flush=True)
(HERE/'export_audits.json').write_text(json.dumps(results,indent=2))
