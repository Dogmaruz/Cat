"""Replace Tiko's damaged inner case/body contact fan with a local closed patch.

Call apply(body, rig, source_body=None, character='Tiko') after torso/harness and
before the shoulder-joint repair. No files, images, other objects or rig data are
modified. The old case wall, cream body and gold strap keep separate materials.
"""
import bpy,bmesh,numpy as np,math,hashlib,pathlib,json
from mathutils import Vector
from mathutils.geometry import convex_hull_2d,delaunay_2d_cdt
import repair_smooth_torso_harness as r

def require(v,m):
 if not v:raise RuntimeError(m)

REVISION='tiko-cut-reconnect-native-case-v2'

def _validate_applied(body):
 """A second call validates the authored patch and leaves every byte alone."""
 report=json.loads(body['case_edge_repair_audit']);mesh=body.data
 tag=mesh.attributes.get('CaseEdgeMaterial');require(tag is not None,'Stamped case repair lost its semantic tags')
 faces=[f for f in mesh.polygons if tag.data[f.index].value>0]
 require(len(faces)==report['new_triangles'],'Stamped case patch triangle count changed')
 require(np.isfinite(np.asarray([v.co[:]for v in mesh.vertices])).all()and np.isfinite(np.asarray([u.uv[:]for u in mesh.uv_layers.active.data])).all(),'Stamped case repair has non-finite data')
 require(all(abs(sum(g.weight for g in v.groups)-1)<1e-5 for v in mesh.vertices),'Stamped case skin is no longer normalized')
 for f in faces:
  a,b,c=[mesh.uv_layers.active.data[li].uv for li in f.loop_indices]
  require(abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5>1e-12,'Stamped case UV became degenerate')
 if mesh.shape_keys and 'Blink'in mesh.shape_keys.key_blocks:
  require(max((mesh.shape_keys.key_blocks['Blink'].data[i].co-mesh.shape_keys.key_blocks['Basis'].data[i].co).length for f in faces for i in f.vertices)<1e-7,'Stamped case acquired Blink movement')
 bm=bmesh.new();bm.from_mesh(mesh);topology=(sum(e.is_boundary for e in bm.edges),sum(not e.is_manifold for e in bm.edges));bm.free()
 require(topology==(report['boundary_edges'],report['nonmanifold_edges']),'Stamped case topology changed')
 return {**report,'already_applied':True,'repeated_apply_changes':0}

def apply(body,rig,source_body=None,character='Tiko'):
 if character!='Tiko':return {'character':character,'changed':False}
 if body.get('case_edge_repair_revision')==REVISION:return _validate_applied(body)
 require(not body.get('case_edge_repair_revision'),'A different case repair is already stamped')
 own=source_body is None;source=source_body or r.original_body('Tiko');r.resolve_source_maps('Tiko',source)
 mesh=body.data;parts=r.source_parts('Tiko',source);inspection=r.inspect_source('Tiko',body,source);colors=r.face_colors(source)
 fit=r.SourceSurfaceFit('Tiko',source,1,inspection['sides']['1']['cap_bounds'],colors)
 pos0=np.array([v.co[:]for v in mesh.vertices]);normal0=[n.vector.copy()for n in mesh.corner_normals]
 uv0=[u.uv.copy()for u in mesh.uv_layers.active.data]
 keys0={k.name:np.array([v.co[:]for v in k.data])for k in mesh.shape_keys.key_blocks}if mesh.shape_keys else{}
 skin0=[[(g.group,g.weight)for g in v.groups]for v in mesh.vertices]
 images={role:pathlib.Path(bpy.path.abspath(r.paint_image(source,role).filepath))for role in('BaseColor','Normal','MetallicSmoothness')}
 hashes0={role:hashlib.sha256(path.read_bytes()).hexdigest()for role,path in images.items()}
 all_maps={f'{name}/{role}.png':r.PROCESSED/name/(role+'.png')for name in('Nori','Tiko','Lumen')for role in('BaseColor','Normal','MetallicSmoothness')}
 all_hashes0={name:hashlib.sha256(path.read_bytes()).hexdigest()for name,path in all_maps.items()}
 bm=bmesh.new();bm.from_mesh(mesh);bm.faces.ensure_lookup_table();bm.verts.ensure_lookup_table()
 fid=bm.faces.layers.int['OriginalSurfaceFace'];vid=bm.verts.layers.int['OriginalSurfaceVertex'];uv=bm.loops.layers.uv.active
 provenance=bm.faces.layers.int.new('CaseEdgeInputFace');vp=bm.verts.layers.int.new('CaseEdgeInputVertex');normal=bm.loops.layers.float_vector.new('CaseEdgeCornerNormal')
 material_tag=bm.faces.layers.int.new('CaseEdgeMaterial');parttag=bm.faces.layers.int['TripoSurfacePart'];patch=bm.faces.layers.int['SmoothTorsoPatchSide']
 lookup={};encoded=[tuple(i.value)for i in mesh.attributes['custom_normal'].data]
 for v in bm.verts:v[vp]=v.index+1
 for f,p in zip(bm.faces,mesh.polygons):
  f[provenance]=f.index+1
  for l,li in zip(f.loops,p.loop_indices):l[normal]=normal0[li];lookup[(f[provenance],l.vert[vp])]=li
 selected=set()
 for f in bm.faces:
  x,y,z=f.calc_center_median();o=f[fid]-1;part=int(parts[o])if o>=0 else -1
  if not(.55<z<1.05):continue
  if part==0 and .14<x<.255 and .38<y<.435:selected.add(f)
  if part in(-1,12,16)and .1<x<.42 and .29<y<.425 and all(v.co.y>.26 and .48<v.co.z<1.08 for v in f.verts):selected.add(f)
  if o==7134:selected.add(f)
 # Fill only bounded islands attached to this local deletion, never unrelated
 # disconnected source details such as antenna ornaments.
 unseen=set(bm.faces)-selected
 while unseen:
  seed=unseen.pop();component={seed};queue=[seed]
  while queue:
   f=queue.pop()
   for e in f.edges:
    for other in e.link_faces:
     if other in unseen:unseen.remove(other);component.add(other);queue.append(other)
  if len(component)<20 and any(any(other in selected for other in e.link_faces)for f in component for e in f.edges):selected|=component
 edges=[e for e in bm.edges if sum(f in selected for f in e.link_faces)==1]
 require(all(sum(v in e.verts for e in edges)==2 for v in{v for e in edges for v in e.verts}),'Case patch boundary is not a loop')
 remaining=set(edges);e=min(remaining,key=lambda e:e.index);remaining.remove(e);loop=[e.verts[0],e.verts[1]]
 while loop[-1]!=loop[0]:
  options=[e for e in loop[-1].link_edges if e in remaining];require(len(options)==1,'Case boundary branches');e=options[0];remaining.remove(e);loop.append(e.other_vert(loop[-1]))
 loop.pop();require(not remaining,'Case patch has multiple loops')
 # Match the boundary orientation of the removed surface.
 e=bm.edges.get((loop[0],loop[1]));old=next(f for f in e.link_faces if f in selected)
 if not any(l.vert==loop[0]and l.link_loop_next.vert==loop[1]for l in old.loops):loop.reverse()
 removed=sorted(f[fid]-1 for f in selected if f[fid]>0);removed_input={f[provenance]for f in selected}
 edge_info=[]
 for i,a in enumerate(loop):
  b=loop[(i+1)%len(loop)];edge=bm.edges.get((a,b));out=next(f for f in edge.link_faces if f not in selected)
  oldid=out[fid]-1;part=int(parts[oldid])if oldid>=0 else -1
  ns={l.vert:l[normal].copy()for l in out.loops}
  edge_info.append((part,ns[a],ns[b]))
 topology0=(sum(e.is_boundary for e in bm.edges),sum(not e.is_manifold for e in bm.edges))
 # A private convex footprint keeps the retained folded anchors on an inner
 # connection ring.  The visible replacement is one continuous manifold patch.
 points=[Vector((v.co.y,v.co.z))for v in loop];hull=[points[i]for i in convex_hull_2d(points)]
 area=lambda p:sum(a.x*b.y-b.x*a.y for a,b in zip(p,p[1:]+p[:1]))
 if area(points)*area(hull)<0:hull.reverse()
 start=min(range(len(hull)),key=lambda i:(hull[i]-points[0]).length_squared);hull=hull[start:]+hull[:start]
 cumulative=np.r_[0,np.cumsum([(a-b).length for a,b in zip(hull,hull[1:]+hull[:1])])]
 lengths=np.r_[0,np.cumsum([(a.co-b.co).length for a,b in zip(loop,loop[1:]+loop[:1])])]
 seam=.405
 def field(y,z):
  cloth=fit.point(y,z).x;t=max(0.,min(1.,(y-.375)/.055));smooth=t*t*(3-2*t)
  # Rounded native case lip approaches the preserved green wall at x~.25.
  return Vector((cloth*(1-smooth)+.246*smooth,y,z))
 def fieldnormal(y,z):
  d=1e-4;dy=(field(y+d,z).x-field(y-d,z).x)/(2*d);dz=(field(y,z+d).x-field(y,z-d).x)/(2*d)
  return Vector((1,-dy,-dz)).normalized()
 coords=[]
 for i in range(len(loop)):
  distance=lengths[i]/lengths[-1]*cumulative[-1];j=min(len(hull)-1,int(np.searchsorted(cumulative,distance,side='right')-1));t=(distance-cumulative[j])/(cumulative[j+1]-cumulative[j]);coords.append(hull[j].lerp(hull[(j+1)%len(hull)],t))
 # Insert material-seam boundary intersections before the constrained fill.
 expanded=[];segments=[]
 for i,a in enumerate(coords):
  b=coords[(i+1)%len(coords)];expanded.append(a);seq=[len(expanded)-1]
  if(a.x-seam)*(b.x-seam)<-1e-10:
   q=a.lerp(b,(seam-a.x)/(b.x-a.x));expanded.append(q);seq.append(len(expanded)-1)
  segments.append(seq)
 # Remove faces and their now-unused interior edges/vertices.
 old_boundary=set(loop);bmesh.ops.delete(bm,geom=list(selected),context='FACES')
 for e in list(bm.edges):
  if not e.link_faces and not(e.verts[0]in old_boundary and e.verts[1]in old_boundary):bm.edges.remove(e)
 for v in list(bm.verts):
  if not v.link_edges and v not in old_boundary:bm.verts.remove(v)
 deform=bm.verts.layers.deform.verify();group=body.vertex_groups['body'].index
 def vertex(q):
  v=bm.verts.new(field(q.x,q.y));v[vid]=0;v[vp]=0;v[deform][group]=1.
  for layer in bm.verts.layers.shape.values():v[layer]=v.co
  return v
 private=[vertex(q)for q in expanded];newfaces=[];casefaces=[];clothfaces=[]
 def face(vertices,is_case,native=None):
  f=bm.faces.new(vertices);f[fid]=0;f[provenance]=0;f.smooth=True
  for l in f.loops:l[normal]=fieldnormal(l.vert.co.y,l.vert.co.z)
  if native:
   for v,n in native.items():next(l for l in f.loops if l.vert==v)[normal]=n
  newfaces.append(f);(casefaces if is_case else clothfaces).append(f);return f
 for i,a in enumerate(loop):
  b=loop[(i+1)%len(loop)];indices=segments[i];nxt=segments[(i+1)%len(loop)][0];chain=[private[j]for j in indices]+[private[nxt]]
  part,na,nb=edge_info[i]
  # A fan around this short strip preserves every original boundary edge.
  fs=face([a,b]+list(reversed(chain)),part==0,{a:na,b:nb})
 # Dense genuine surface samples, with an explicit body/case material seam.
 p2=list(expanded);bound=[(i,(i+1)%len(expanded))for i in range(len(expanded))]
 low=np.min([tuple(p)for p in expanded],axis=0);high=np.max([tuple(p)for p in expanded],axis=0)
 for y in np.arange(low[0]+.008,high[0],.012):
  for z in np.arange(low[1]+.008,high[1],.018):
   if r._inside((float(y),float(z)),[tuple(p)for p in expanded]):p2.append(Vector((float(y),float(z))))
 on=[i for i,p in enumerate(p2)if abs(p.x-seam)<1e-6]
 if len(on)==2:bound.append(tuple(on))
 verts2,edges2,faces2,origv,orige,origf=delaunay_2d_cdt(p2,bound,[list(range(len(expanded)))],1,1e-7)
 mapped=[]
 for q,orig in zip(verts2,origv):
  match=next((i for i in orig if i<len(expanded)),None);mapped.append(private[match]if match is not None else vertex(q))
 for tri in faces2:
  vs=[mapped[i]for i in tri]
  if len(vs)!=3:continue
  if area([Vector((v.co.y,v.co.z))for v in vs])*area(points)<0:vs.reverse()
  face(vs,sum(v.co.y for v in vs)/3>seam)
 # Triangulation interpolates all existing UV/shape/deform/custom layers.
 bmesh.ops.triangulate(bm,faces=newfaces,quad_method='BEAUTY',ngon_method='BEAUTY')
 bm.normal_update()
 newfaces=[f for f in bm.faces if f[provenance]==0]
 casefaces=[f for f in newfaces if f.calc_center_median().y>seam and min(v.co.y for v in f.verts)>.37]
 clothfaces=[f for f in newfaces if f not in casefaces]
 for f in newfaces:
  f[material_tag]=1 if f in casefaces else 2;f[parttag]=1 if f in casefaces else 13;f[patch]=0 if f in casefaces else 1
 # A native atlas swatch per small triangle keeps material semantics exact.
 color=r.image_pixels(r.paint_image(source));norm=r.image_pixels(r.paint_image(source,'Normal'));packed=r.image_pixels(r.paint_image(source,'MetallicSmoothness'));h,w=color.shape[:2];red,green,blue=color[:,:,0],color[:,:,1],color[:,:,2]
 greenmask=(green>red*1.12)&(blue>red*.72)&(green>.40)&(green<.69)&(norm[:,:,2]>.93)
 bodymask=(red>.6)&(red<.84)&(green>.52)&(blue>green*.81)&(red<green*1.25)&(norm[:,:,2]>.95)&(packed[:,:,0]<.03)&((1-packed[:,:,3])>.66)&((1-packed[:,:,3])<.82)
 scale=math.sqrt(inspection['sides']['1']['uv_area_per_world_area_median']);windows={}
 negative=[f for f in bm.faces if f[fid]-1 in(2700,2701,2703)]
 require(len(negative)==3,'Vent-side corner source IDs are incomplete')
 for f in newfaces+negative:
  iscase=f in casefaces or f in negative;points3=np.array([v.co[:]for v in f.verts]);axis=int(np.argmax(np.abs(f.normal)))if f.normal.length>0 else 0;axes=[a for a in range(3)if a!=axis];q=np.array([l[uv].uv[:]for l in f.loops])if f in negative else points3[:,axes]*scale;q-=q.min(0);extent=q.max(0);ww=max(3,int(math.ceil(extent[0]*w/4))+2);hh=max(3,int(math.ceil(extent[1]*h/4))+2);key=(iscase,hh,ww)
  if key not in windows:
   mask=greenmask if iscase else bodymask;blocks=mask.reshape(h//4,4,w//4,4).all(axis=(1,3));target=(.43,.59,.46)if iscase else(.75,.67,.58);cost=np.sum((color[::4,::4,:3]-np.array(target))**2,axis=2);invalid=r._rectangle_sums((~blocks).astype(np.int32),hh,ww);scores=r._rectangle_sums(cost,hh,ww)/(hh*ww);scores[invalid!=0]=np.inf;y,x=np.unravel_index(np.argmin(scores),scores.shape);require(np.isfinite(scores[y,x]),f'No native material swatch {key} scale{scale}');windows[key]=Vector(((x+1)*4/w,(y+1)*4/h))
  for l,p in zip(f.loops,q):l[uv].uv=Vector(p)+windows[key]
 bm.normal_update();require((sum(e.is_boundary for e in bm.edges),sum(not e.is_manifold for e in bm.edges))==topology0,'Local case patch opened topology')
 bm.to_mesh(mesh);bm.free();mesh.update()
 desired=[];restore=[]
 attr=mesh.attributes['CaseEdgeCornerNormal'];inp=mesh.attributes['CaseEdgeInputFace'];vinput=mesh.attributes['CaseEdgeInputVertex']
 for f in mesh.polygons:
  for li in f.loop_indices:
   desired.append(attr.data[li].vector[:]);oldli=lookup.get((inp.data[f.index].value,vinput.data[mesh.loops[li].vertex_index].value))
   if oldli is not None:restore.append((li,oldli))
 mesh.normals_split_custom_set(desired)
 # Custom normals are encoded relative to a corner fan. Reusing old encoded
 # pairs after a topological edit can flip a retained normal by nearly 180deg.
 # Keep the exact raw pair only where it still decodes to the old physical
 # normal; otherwise retain Blender's newly encoded physical target.
 encoded_new=[tuple(i.value)for i in mesh.attributes['custom_normal'].data]
 physical_new=[n.vector.copy()for n in mesh.corner_normals]
 for li,oldli in restore:mesh.attributes['custom_normal'].data[li].value=encoded[oldli]
 mesh.update()
 physical_raw=[n.vector.copy()for n in mesh.corner_normals]
 for li,oldli in restore:
  if(physical_raw[li]-normal0[oldli]).length>(physical_new[li]-normal0[oldli]).length+1e-8:mesh.attributes['custom_normal'].data[li].value=encoded_new[li]
 mesh.update()
 normal_delta=max((mesh.corner_normals[li].vector-normal0[oldli]).length for li,oldli in restore)
 position_delta=0.;key_delta=0.;skin_errors=0;uv_delta=0.;negative_set={2700,2701,2703}
 for v in mesh.vertices:
  oldi=vinput.data[v.index].value-1
  if oldi<0:continue
  position_delta=max(position_delta,float(np.max(np.abs(np.array(v.co)-pos0[oldi]))));skin_errors+=skin0[oldi]!=[(g.group,g.weight)for g in v.groups]
  for key in mesh.shape_keys.key_blocks if mesh.shape_keys else[]:key_delta=max(key_delta,float(np.max(np.abs(np.array(key.data[v.index].co)-keys0[key.name][oldi]))))
 for f in mesh.polygons:
  if inp.data[f.index].value<=0 or mesh.attributes['OriginalSurfaceFace'].data[f.index].value-1 in negative_set:continue
  for li in f.loop_indices:
   oldli=lookup[(inp.data[f.index].value,vinput.data[mesh.loops[li].vertex_index].value)];uv_delta=max(uv_delta,(mesh.uv_layers.active.data[li].uv-uv0[oldli]).length)
 require(position_delta==0 and key_delta==0 and skin_errors==0 and uv_delta==0,'Case edge repair escaped its local patch')
 require(normal_delta<.002,'Retained physical custom normals changed beyond native quantization')
 require(np.isfinite(np.array([v.co[:]for v in mesh.vertices])).all()and np.isfinite(np.array([u.uv[:]for u in mesh.uv_layers.active.data])).all(),'Non-finite case geometry or UV')
 new_uv_areas=[]
 for f in mesh.polygons:
  if inp.data[f.index].value>0:continue
  a,b,c=[mesh.uv_layers.active.data[li].uv for li in f.loop_indices];new_uv_areas.append(abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5)
 require(min(new_uv_areas)>1e-12,'Degenerate native UV triangle in case contact')
 blink_new=0.
 if mesh.shape_keys and 'Blink'in mesh.shape_keys.key_blocks:
  blink_new=max((mesh.shape_keys.key_blocks['Blink'].data[v.index].co-mesh.shape_keys.key_blocks['Basis'].data[v.index].co).length for v in mesh.vertices if vinput.data[v.index].value==0)
 require(blink_new<1e-7,'New case contact acquired Blink motion')
 require(all(abs(sum(g.weight for g in v.groups)-1)<1e-5 and all(math.isfinite(g.weight)for g in v.groups)for v in mesh.vertices),'Non-normalized case skin')
 require(hashes0=={role:hashlib.sha256(path.read_bytes()).hexdigest()for role,path in images.items()},'Immutable source map changed')
 require(all_hashes0=={name:hashlib.sha256(path.read_bytes()).hexdigest()for name,path in all_maps.items()},'One of the nine immutable source maps changed')
 report={'revision':'tiko-cut-reconnect-native-case-v2','character':'Tiko','root_cause':'Source part0 lip folds over the retained part16/old harness stub, while the generated cloth fan bridges those folds. Native contact UV also mixes cloth/gold/green. The local fan is removed and reconnected with dense curved cream cloth and a narrow native green case lip.','removed_faces':len(selected),'removed_source_faces':removed,'new_triangles':len(newfaces),'body_material_triangles':len(clothfaces),'case_material_triangles':len(casefaces),'original_boundary_vertices':len(loop),'native_body_case_seam_y':seam,'boundary_edges':topology0[0],'nonmanifold_edges':topology0[1],'introduced_boundary_edges':0,'vent_corner_uv_source_faces':sorted(negative_set),'retained_position_max_delta':position_delta,'retained_uv_max_delta_except_three_vent_faces':uv_delta,'retained_physical_normal_max_delta':normal_delta,'retained_key_max_delta':key_delta,'retained_skin_errors':skin_errors,'new_blink_delta':blink_new,'minimum_new_uv_triangle_area':min(new_uv_areas),'source_textures_modified':0,'source_texture_sha256':hashes0,'all_nine_original_png_unchanged':True}
 body['case_edge_repair_revision']=REVISION;body['case_edge_repair_audit']=json.dumps(report,sort_keys=True)
 if own:bpy.data.objects.remove(source,do_unlink=True)
 return report
