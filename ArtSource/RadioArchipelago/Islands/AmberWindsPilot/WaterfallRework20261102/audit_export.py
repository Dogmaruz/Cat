"""Read-only FBX roundtrip audit of continuous sheets and untouched island."""
import bpy,pathlib,json,hashlib,math,struct,array
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
BASE=HERE.parent/'STAGING/amber_winds_island.fbx'
NEW=HERE/'STAGING/amber_winds_island.fbx'
SYSTEMS=[('lower','Amber_DownstreamCreek_0','Amber_VoluminousFallingWater_0_'),('R3_canyon','Amber_DownstreamCreek_1','Amber_VoluminousFallingWater_1_'),('raised_side','RA_Amber_SideCascade_RaisedStreamAndRoll','RA_Amber_SideCascade_RaisedFall_Layer')]
affected={s for _,s,_ in SYSTEMS}|{p+str(i)for _,_,p in SYSTEMS for i in range(3)}

def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True,use_custom_props=True)
    bpy.context.view_layer.update()

def sign(ob):
    h=hashlib.sha256()
    h.update(struct.pack('<16f',*(x for row in ob.matrix_world for x in row)))
    if ob.type!='MESH':return h.hexdigest()
    m=ob.data
    for coll,key,width,fmt in [(m.vertices,'co',3,'f'),(m.loops,'vertex_index',1,'I')]:
        a=array.array(fmt,[0])*(len(coll)*width);coll.foreach_get(key,a);h.update(a.tobytes())
    for p in m.polygons:h.update(struct.pack('<4I',p.loop_start,p.loop_total,p.material_index,int(p.use_smooth)))
    for uv in m.uv_layers:
        a=array.array('f',[0])*(len(uv.data)*2);uv.data.foreach_get('uv',a);h.update(a.tobytes())
    for attr in m.color_attributes:
        a=array.array('f',[0])*(len(attr.data)*4);attr.data.foreach_get('color',a);h.update(a.tobytes())
    for normal in m.corner_normals:h.update(struct.pack('<3f',*normal.vector))
    h.update(json.dumps([m.name if m else None for m in m.materials]).encode())
    return h.hexdigest()

load(BASE);old={ob.name:sign(ob)for ob in bpy.context.scene.objects if ob.name not in affected}
load(NEW);new={ob.name:sign(ob)for ob in bpy.context.scene.objects if ob.name not in affected}
assert old==new,dict(added=sorted(set(new)-set(old)),removed=sorted(set(old)-set(new)),changed=[n for n in old if n in new and old[n]!=new[n]])
reports=[]
for label,stream,prefix in SYSTEMS:
    ob=bpy.data.objects[prefix+'0'];m=ob.data;uv=m.uv_layers.active;normals=m.corner_normals
    bad=zero=0;minimum=1;edge_counts={};directions=[]
    for face in m.polygons:
        for li in face.loop_indices:
            n=normals[li].vector;zero+=n.length<.99;d=face.normal.dot(n);bad+=d<0;minimum=min(minimum,d)
        ids=list(face.vertices)
        for a,b in zip(ids,ids[1:]+ids[:1]):key=tuple(sorted((a,b)));edge_counts[key]=edge_counts.get(key,0)+1
        # The linear interpolation's longitudinal direction is derivative of
        # world position along +V; a vertical curtain must always descend.
        uvs=[uv.data[i].uv for i in face.loop_indices];ps=[ob.matrix_world@m.vertices[m.loops[i].vertex_index].co for i in face.loop_indices]
        a,b,c=uvs;dp0=ps[1]-ps[0];dp1=ps[2]-ps[0];du0=b-a;du1=c-a
        det=du0.x*du1.y-du0.y*du1.x
        assert abs(det)>1e-8
        along=(dp1*du0.x-dp0*du1.x)/det
        normal=(ob.matrix_world.to_3x3().inverted().transposed()@face.normal).normalized()
        if abs(normal.z)<.15:directions.append(along.z)
    assert not bad and not zero,(label,bad,zero,minimum)
    assert all(n in(1,2)for n in edge_counts.values())
    assert directions and max(directions)<-.5,(label,'non-downstream curtain',max(directions))
    reports.append(dict(id=label,node=ob.name,faces=len(m.polygons),vertices=len(m.vertices),
        negative_corner_normals=bad,nonunit_normals=zero,minimum_face_corner_dot=minimum,
        nonmanifold_edges=0,vertical_flow_derivative_z_range=[min(directions),max(directions)],
        uniform_rgb=list(m.color_attributes['Color'].data[0].color[:3]),
        one_material=len(m.materials)==1,material=m.materials[0].name))
result=dict(sha256=hashlib.sha256(NEW.read_bytes()).hexdigest(),
    imported_nonwater_object_signatures_identical=True,checked_untouched_objects=len(old),
    removed_mesh_aliases=[s for _,s,_ in SYSTEMS]+[p+str(i)for _,_,p in SYSTEMS for i in(1,2)],
    sheets=reports,old_nonwater_signatures=old,new_nonwater_signatures=new)
(HERE/'export_audit.json').write_text(json.dumps(result,indent=2))
print('CONTINUOUS_FBX_AUDIT_PASS',json.dumps({k:v for k,v in result.items()if not k.endswith('signatures')}),flush=True)
