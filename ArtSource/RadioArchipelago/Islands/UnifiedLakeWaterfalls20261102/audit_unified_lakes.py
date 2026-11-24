"""Independent FBX validation of one-piece lake/outlet/fall topology and payload."""
import bpy,pathlib,json,hashlib,struct,array,collections,math,sys
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent

def load(p):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(p),use_custom_normals=True,use_custom_props=True);bpy.context.view_layer.update()
def signature(ob):
 h=hashlib.sha256();h.update(struct.pack('<16f',*(x for row in ob.matrix_world for x in row)))
 if ob.type!='MESH':return h.hexdigest()
 m=ob.data
 for coll,key,width,fmt in[(m.vertices,'co',3,'f'),(m.loops,'vertex_index',1,'I')]:
  a=array.array(fmt,[0])*(len(coll)*width);coll.foreach_get(key,a);h.update(a.tobytes())
 for p in m.polygons:h.update(struct.pack('<4I',p.loop_start,p.loop_total,p.material_index,int(p.use_smooth)))
 for uv in m.uv_layers:
  a=array.array('f',[0])*(len(uv.data)*2);uv.data.foreach_get('uv',a);h.update(a.tobytes())
 for color in m.color_attributes:
  a=array.array('f',[0])*(len(color.data)*4);color.data.foreach_get('color',a);h.update(a.tobytes())
 for n in m.corner_normals:h.update(struct.pack('<3f',*n.vector))
 h.update(json.dumps([m.name if m else None for m in m.materials]).encode());return h.hexdigest()
def body_records(ob,start):
 m=ob.data;uv=m.uv_layers[0];color=m.color_attributes.active_color;result={}
 for loop in m.loops:
  u,v=uv.data[loop.index].uv
  if v<start-.00001:continue
  p=ob.matrix_world@m.vertices[loop.vertex_index].co
  key=tuple(round(float(x),5)for x in p)+(round(float(u),7),round(float(v),5))
  result[key]=dict(position=tuple(p),uv=(float(u),float(v)),alpha=float(color.data[loop.index].color[3]),normal=tuple(m.corner_normals[loop.index].vector))
 return result

results=[]
requested=sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else('Coral','Moon','Amber')
for label in requested:
 folder=HERE/'STAGING'/label;meta=json.loads((folder/'handoff.json').read_text());target=set(meta['affected_nodes']);file=pathlib.Path(meta['output_model']).name
 load(meta['input_model']);old={ob.name:signature(ob)for ob in bpy.context.scene.objects if ob.name not in target}
 old_bodies={s['node']:body_records(bpy.data.objects[s['node']],s['metric_start_v'])for s in meta['sheets']}
 load(folder/file);new={ob.name:signature(ob)for ob in bpy.context.scene.objects if ob.name not in target}
 assert old==new,dict(level=label,added=list(set(new)-set(old)),removed=list(set(old)-set(new)),changed=[n for n in old if n in new and old[n]!=new[n]])
 sheets=[]
 for info in meta['sheets']:
  ob=bpy.data.objects[info['node']];m=ob.data;uv=m.uv_layers[0];region=m.uv_layers[1];color=m.color_attributes.active_color
  assert len(m.materials)==1
  assert len(m.uv_layers)>=2
  edges=collections.Counter();adj=collections.defaultdict(set);bad=zero=0;mindot=1;directions=[];lake_vertices=set();lake_v=[];mouth_pairs=collections.Counter()
  for f in m.polygons:
   ids=list(f.vertices)
   for a,b in zip(ids,ids[1:]+ids[:1]):edges[tuple(sorted((a,b)))]+=1;adj[a].add(b);adj[b].add(a)
   for li in f.loop_indices:
    n=m.corner_normals[li].vector;d=f.normal.dot(n);bad+=d<-.0001;zero+=n.length<.99;mindot=min(mindot,d)
    mask=region.data[li].uv.x
    if mask<.001:lake_vertices.add(m.loops[li].vertex_index);lake_v.append(uv.data[li].uv.y)
    if mask<.999:assert color.data[li].color[3]>.999,(label,ob.name,'translucent lake seam')
   if all(region.data[li].uv.x>.999 for li in f.loop_indices):
    us=[uv.data[li].uv for li in f.loop_indices];ps=[ob.matrix_world@m.vertices[m.loops[li].vertex_index].co for li in f.loop_indices]
    a,b,c=us[:3];x,y=b-a,c-a;det=x.x*y.y-x.y*y.x
    if abs(det)>1e-8:
     along=((ps[2]-ps[0])*x.x-(ps[1]-ps[0])*y.x)/det
     n=(ob.matrix_world.to_3x3().inverted().transposed()@f.normal).normalized()
     if abs(n.z)<.15:directions.append(along.z)
  unseen=set(adj);components=0
  while unseen:
   components+=1;stack=[unseen.pop()]
   while stack:
    for q in adj[stack.pop()]:
     if q in unseen:unseen.remove(q);stack.append(q)
  assert components==1,(label,ob.name,'disconnected',components)
  assert max(edges.values())<=2
  assert not bad and not zero,(label,ob.name,'invalid normals',bad,zero,mindot)
  assert len(lake_vertices)>20 and min(lake_v)<-.1,(label,ob.name,'missing broad negative-V lake',len(lake_vertices),min(lake_v))
  assert directions and max(directions)<-.5,(label,ob.name,'non-downstream vertical flow',max(directions))
  after_body=body_records(ob,info['metric_start_v']);before_body=old_bodies[info['node']]
  # Regression: a clamped V must never classify an off-mouth lake vertex as
  # the first full body row. Check actual positions against authored U columns.
  authored={round(v['uv'][0],7):Vector(v['position'])for v in before_body.values()if abs(v['uv'][1]-info['metric_start_v'])<.0001}
  assert len(authored)==33,(label,ob.name,'original authored mouth does not have33columns',len(authored))
  mouth_errors=[];mouth_extra_us=[]
  for l in m.loops:
   u,v=uv.data[l.index].uv
   if abs(v-info['metric_start_v'])>.0001 or region.data[l.index].uv.x<.999:continue
   assert -.00001<=u<=1.00001,(label,ob.name,'mouth U outofrange',u)
   j=min(31,max(0,int(math.floor(u*32))));left=authored[round(j/32,7)];right=authored[round((j+1)/32,7)]
   expected=left.lerp(right,min(1,max(0,u*32-j)));p=ob.matrix_world@m.vertices[l.vertex_index].co
   error=(p-expected).length;mouth_errors.append(error)
   assert error<.0001,(label,ob.name,'false mouth classification',u,v,list(p),error)
   if abs(u*32-round(u*32))>.00001:mouth_extra_us.append(float(u))
  missing=set(before_body)-set(after_body);assert not missing,(label,ob.name,'body vertices/UV changed',len(missing),list(missing)[:1])
  dp=max(abs(a-b)for key,v in before_body.items()for a,b in zip(v['position'],after_body[key]['position']))
  du=max(abs(a-b)for key,v in before_body.items()for a,b in zip(v['uv'],after_body[key]['uv']))
  da=max(abs(v['alpha']-after_body[key]['alpha'])for key,v in before_body.items())
  dn=max(abs(a-b)for key,v in before_body.items()for a,b in zip(v['normal'],after_body[key]['normal']))
  assert dp<.00003 and du<.00003 and da<.00001,(label,ob.name,dp,du,da)
  assert dn<.002,(label,ob.name,'body normal drift',dn)
  sheets.append(dict(node=ob.name,components=components,materials=len(m.materials),vertices=len(m.vertices),faces=len(m.polygons),lake_vertices=len(lake_vertices),lake_v_min=min(lake_v),
   negative_normals=bad,nonunit_normals=zero,minimum_face_corner_normal_dot=mindot,body_downstream_z_range=[min(directions),max(directions)],
   original_body_samples=len(before_body),max_body_position_error=dp,max_body_uv_error=du,max_body_alpha_error=da,max_body_normal_component_error=dn))
  sheets[-1].update(mouth_fullmask_samples=len(mouth_errors),maximum_mouth_interpolation_error=max(mouth_errors),genuine_extra_mouth_u=sorted(set(mouth_extra_us)))
 result=dict(level=label,passed=True,output_sha256=hashlib.sha256((folder/file).read_bytes()).hexdigest(),untouched_imported_objects=len(old),untouched_render_signatures_exact=True,
  consumed_pool_nodes=meta['consumed_pool_nodes'],sheets=sheets,old_signatures=old,new_signatures=new)
 (folder/'export_audit.json').write_text(json.dumps(result,indent=2));results.append(result)
 print('UNIFIED_LAKE_FBX_AUDIT_PASS',json.dumps({k:v for k,v in result.items()if not k.endswith('signatures')}),flush=True)
(HERE/'export_audits.json').write_text(json.dumps(results,indent=2))
