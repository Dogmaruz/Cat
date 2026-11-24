"""Read-only all-curtain body/first-row normals audit of blend or exported FBX."""
import bpy,pathlib,sys,json,math
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
from water_seam_shading import SYSTEMS,samples
fbx='--fbx'in sys.argv
if fbx:bpy.ops.import_scene.fbx(filepath=str(HERE/'STAGING/amber_winds_island.fbx'),use_custom_normals=True)
else:bpy.ops.wm.open_mainfile(filepath=str(HERE/'amber_winds_preview.blend'))
results=[]
for label,stream_name,prefix in SYSTEMS:
    stream=samples(bpy.data.objects[stream_name]);vmax=max(s[2][1]for s in stream);end=[s for s in stream if abs(s[2][1]-vmax)<1e-4]
    p0=next(s[1]for s in end if abs(s[2][0])<1e-6);p1=next(s[1]for s in end if abs(s[2][0]-1)<1e-6);side=(p1-p0).normalized();outward=Vector((side.y,-side.x,0)).normalized()
    for layer in range(3):
        ob=bpy.data.objects[prefix+str(layer)];data=ob.data;uv=data.uv_layers.active;nm=ob.matrix_world.to_3x3().inverted().transposed();v0=min(x.uv.y for x in uv.data)
        all_min=1;body_min=1;all_negative=0;body_negative=0;body_count=0;zero_count=0;outward_min=1;face_outward_min=1;rows={}
        for poly in data.polygons:
            physical=(nm@poly.normal).normalized();face_outward_min=min(face_outward_min,physical.dot(outward))
            for index in poly.loop_indices:
                normal=nm@data.corner_normals[index].vector
                if normal.length<1e-8:zero_count+=1;continue
                normal.normalize();coord=uv.data[index].uv;dot=normal.dot(physical);all_min=min(all_min,dot);all_negative+=dot<0
                outward_min=min(outward_min,normal.dot(outward))
                if coord.y-v0>.80001:body_count+=1;body_min=min(body_min,dot);body_negative+=dot<0
                row_key=round(float(coord.y-v0),5);rows.setdefault(row_key,{}).setdefault(round(float(coord.x),6),normal.copy())
        rowkeys=sorted(rows)[:3];transitions=[]
        for a,b in zip(rowkeys,rowkeys[1:]):
            keys=set(rows[a])&set(rows[b]);dots=[rows[a][u].dot(rows[b][u])for u in keys];mids=[((rows[a][u]+rows[b][u])*.5).length for u in keys]
            transitions.append(dict(rows_metres=[a,b],matched_u_count=len(keys),min_dot=min(dots),min_midpoint_normal_length=min(mids)))
        report=dict(id=label,layer=layer,node=ob.name,has_custom_normals=data.has_custom_normals,
          corners=len(data.loops),all_corner_face_dot_min=all_min,all_negative_count=all_negative,
          body_excludes_first_metres=.8,body_corners=body_count,body_corner_face_dot_min=body_min,body_negative_count=body_negative,
          zero_corner_normals=zero_count,min_corner_dot_physical_outward=outward_min,min_face_dot_physical_outward=face_outward_min,
          first_three_rows_metres=rowkeys,row_transitions=transitions)
        assert all_negative==body_negative==zero_count==0 and body_min>0 and outward_min>0 and all(x['min_dot']>0 and x['min_midpoint_normal_length']>.7 for x in transitions),report
        results.append(report)
output=dict(status='PASS',input='actual FBX authored-normal reimport'if fbx else'frozen source blend',curtains=results,
 summary=dict(curtains=len(results),body_corners=sum(x['body_corners']for x in results),negative_body_corners=sum(x['body_negative_count']for x in results),
 min_body_corner_face_dot=min(x['body_corner_face_dot_min']for x in results),min_first_rows_dot=min(t['min_dot']for x in results for t in x['row_transitions']),
 min_first_rows_midpoint_length=min(t['min_midpoint_normal_length']for x in results for t in x['row_transitions'])))
(HERE/('curtain_body_normals_fbx.json'if fbx else'curtain_body_normals_blend.json')).write_text(json.dumps(output,indent=2))
print('CURTAIN_BODY_NORMALS_PASS',json.dumps(dict(input=output['input'],**output['summary'])),flush=True)
