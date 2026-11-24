"""Read-only imported-water vs actual authored Terrain/Cliffs geometry witness."""
import bpy,pathlib,json,math,collections
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent
levels=('Coral','Moon')
results=[]
def unity(p):return[p.x,p.z,-p.y]
for label in levels:
 folder=HERE/'STAGING'/label
 bpy.ops.wm.open_mainfile(filepath=str(folder/(label.lower()+'_unified_lake_waterfalls.blend')));bpy.context.view_layer.update()
 rock=[ob for ob in bpy.context.scene.objects if ob.type=='MESH'and any(m and any(k in m.name for k in('_Terrain','_Cliffs'))for m in ob.data.materials)]
 verts=[];faces=[];owners=[]
 for ob in rock:
  start=len(verts);verts.extend(ob.matrix_world@v.co for v in ob.data.vertices)
  for p in ob.data.polygons:faces.append(tuple(start+i for i in p.vertices));owners.append(ob.name)
 tree=BVHTree.FromPolygons(verts,faces,all_triangles=False)
 meta=json.loads((folder/'handoff.json').read_text())
 for info in meta['sheets']:
  ob=bpy.data.objects[info['node']];m=ob.data;rows=collections.defaultdict(dict)
  for loop in m.loops:
   u,v=m.uv_layers[0].data[loop.index].uv;mask=m.uv_layers[1].data[loop.index].uv.x
   if mask<.999 or v<info['metric_start_v']-.0001:continue
   rows[round(v,5)][round(u,6)]=ob.matrix_world@m.vertices[loop.vertex_index].co
  centres=[(v,vals[.5],vals)for v,vals in sorted(rows.items())if .5 in vals]
  top=max(c.z for v,c,row in centres);bottom=min(c.z for v,c,row in centres)
  samples=[]
  for fraction in(.05,.25,.5,.75,.95):
   desired=top+(bottom-top)*fraction;v,c,row=min(centres,key=lambda x:abs(x[1].z-desired))
   transverse=[]
   for u in(.15625,.5,.84375):
    p=row[u];hit,n,face,d=tree.find_nearest(p)
    record=dict(u=u,source_position=list(p),unity_position=unity(p),nearest_surface_distance=d,nearest_node=owners[face],nearest_surface=list(hit),nearest_surface_normal=list(n),signed_nearest_dot=(p-hit).dot(n))
    directions=[]
    for angle in(0,45,90,135,180,225,270,315):
     a=math.radians(angle);direction=Vector((math.cos(a),math.sin(a),0))
     h,normal,index,distance=tree.ray_cast(p+direction*.0001,direction,250)
     # Visibility from a remote horizontal exterior sample, with full rock
     # enabled. This is an occlusion witness, not a gameplay camera change.
     outside=p+direction*200;entry,en,ei,ed=tree.ray_cast(outside,-direction,200)
     directions.append(dict(azimuth=angle,first_exit_distance=distance if h is not None else None,first_node=owners[index]if h is not None else None,
       first_normal_dot_direction=normal.dot(direction)if h is not None else None,
       exterior_blocked=entry is not None and ed<199.999,front_node=owners[ei]if entry is not None else None,
       body_depth_behind_front=200-ed if entry is not None else None))
    up,un,ui,ud=tree.ray_cast(p+Vector((0,0,.0001)),Vector((0,0,1)),250)
    down,dn,di,dd=tree.ray_cast(p+Vector((0,0,-.0001)),Vector((0,0,-1)),250)
    record.update(horizontal_rays=directions,blocked_azimuths=sum(r['exterior_blocked']for r in directions),
      cap_above_distance=ud if up is not None else None,cap_above_node=owners[ui]if up is not None else None,
      rock_below_distance=dd if down is not None else None,rock_below_node=owners[di]if down is not None else None)
    if u==.5:
     winding=[]
     for angle in(11,56,101,146,191,236,281,326):
      a=math.radians(angle);direction=Vector((math.cos(a),math.sin(a),.071)).normalized()
      origin=p+direction*.0002;travel=0;signsum=0;count=0;intersections=[]
      for iteration in range(200):
       h,normal,index,distance=tree.ray_cast(origin,direction,600-travel)
       if h is None:break
       dot=normal.dot(direction)
       if abs(dot)>1e-6:signsum+=1 if dot>0 else -1
       intersections.append(dict(node=owners[index],distance=travel+distance,normal_dot=dot))
       count+=1;travel+=distance+.0005;origin=h+direction*.0005
       if travel>=600:break
      winding.append(dict(azimuth=angle,signed_exit_minus_entry=signsum,intersection_count=count,first_hits=intersections[:4]))
     record['winding_rays']=winding
     record['winding_sums']=[r['signed_exit_minus_entry']for r in winding]
    transverse.append(record)
   samples.append(dict(drop_fraction=fraction,row_v=v,source_height=c.z,transverse=transverse))
  results.append(dict(level=label,node=info['node'],top_height=top,bottom_height=bottom,source_scope='Actual source Terrain and Cliffs triangles; no hidden-water render, shader override, route/camera/scenery mutation.',samples=samples))
(HERE/'body_occlusion_audit.json').write_text(json.dumps(results,indent=2))
summary=[]
for r in results:
 summary.append(dict(level=r['level'],node=r['node'],centres=[dict(drop=s['drop_fraction'],height=s['source_height'],nearest=s['transverse'][1]['nearest_surface_distance'],
   signed=s['transverse'][1]['signed_nearest_dot'],blocked=s['transverse'][1]['blocked_azimuths'],cap=s['transverse'][1]['cap_above_distance'],node=s['transverse'][1]['nearest_node'])for s in r['samples']]))
(HERE/'body_occlusion_summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2),flush=True)
