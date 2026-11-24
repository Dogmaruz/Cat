from pathlib import Path
import bpy,bmesh,json,sys,math,shutil,numpy as np
from mathutils import Vector
HERE=Path(__file__).parent
OUT=HERE/'MiroGardenCandidate'/'Miro_Garden';OUT.mkdir(exist_ok=True)
BASE=HERE.parent/'StraplessCases/Before/Miro_Garden'
sys.path.insert(0,str(HERE.parents[1]/'Modular20261104'))
import pack_inspect as source
bpy.ops.wm.open_mainfile(filepath=str(BASE/'editable.blend'),load_ui=False)
case=bpy.data.objects['Case'];meta=json.loads((BASE/'Meta.json').read_text())
mat=case.data.materials[0];node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
image=node.inputs['Base Color'].links[0].from_node.image
pixels=np.empty(image.size[0]*image.size[1]*4,dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(image.size[1],image.size[0],4)
texuv=np.asarray([list(l.uv) for l in case.data.uv_layers.active.data]);xy=np.clip((texuv*np.asarray(image.size[:])).astype(int),0,np.array(image.size[:])-1)
rgb=pixels[xy[:,1],xy[:,0],:3]
def swatch(target):
 index=int(np.argmin(((rgb-np.asarray(target))**2).sum(1)))
 return Vector((xy[index]+.5)/np.asarray(image.size[:]))
green=swatch((.34,.53,.38))
leather=swatch((.55,.27,.10));thread=swatch((.78,.67,.48));brass=swatch((.68,.48,.20));uvname=case.data.uv_layers.active.name
plane=-.025
bm=bmesh.new();bm.from_mesh(case.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=(0,plane,0),plane_no=(0,1,0),clear_inner=False,clear_outer=False)
# The front leaf, wooden speakers and their metalwork retain every source face
# on the far side of the plane. Preserve the original central roof mechanism;
# the whole harness-bearing return and its old padding are removed.
# The section also crosses raised roof dials. Their upper points are not the
# enamel housing silhouette: the return must stop below the original bearing.
outline=[(float(v.co.x),min(.755,float(v.co.z))) for v in bm.verts if abs(v.co.y-plane)<1e-5 and v.co.z<.825]
def keep(p):
 return p.y>=plane-1e-7 or (abs(p.x)<.17 and p.z>.70)
removed=[f for f in bm.faces if not keep(f.calc_center_median())]
removed_count=len(removed);bmesh.ops.delete(bm,geom=removed,context='FACES')
for v in list(bm.verts):
 if not v.link_faces:bmesh.ops.delete(bm,geom=[v],context='VERTS')
# Close the surviving original part so its normals do not rely on face winding
# of an open generated source. The new housing overlaps this internal plane.
uv=bm.loops.layers.uv.active
caps=bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)['faces']
for f in caps:
 f.smooth=False
 for l in f.loops:l[uv].uv=green
bmesh.ops.triangulate(bm,faces=caps)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(case.data);bm.free();case.data.update()
def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
points=sorted(set(outline));lower=[];upper=[]
for p in points:
 while len(lower)>=2 and cross(lower[-2],lower[-1],p)<=0:lower.pop()
 lower.append(p)
for p in reversed(points):
 while len(upper)>=2 and cross(upper[-2],upper[-1],p)<=0:upper.pop()
 upper.append(p)
hull=lower[:-1]+upper[:-1]
# Actual source section, densely resampled along its perimeter. The front ring
# coincides with the source silhouette; progressively inset back rings create
# a soft enclosure, not a box or a Boolean union of overlapping solids.
lengths=[math.dist(a,b) for a,b in zip(hull,hull[1:]+hull[:1])];total=sum(lengths)
samples=[]
for i in range(192):
 distance=total*i/192;segment=0
 while distance>lengths[segment]:distance-=lengths[segment];segment+=1
 t=distance/lengths[segment];a=hull[segment];b=hull[(segment+1)%len(hull)]
 samples.append(Vector((a[0]*(1-t)+b[0]*t,0,a[1]*(1-t)+b[1]*t)))
center=Vector((0,0,(min(p.z for p in samples)+max(p.z for p in samples))*.5))
verts=[];faces=[];rings=[]
for offset,scale in [(+.003,1),(-.028,1),(-.075,.993),(-.12,.973),(-.153,.935),(-.173,.885)]:
 ring=[]
 for p in samples:
  q=center+(p-center)*scale;q.y=plane+offset
  ring.append(len(verts));verts.append(tuple(q))
 rings.append(ring)
for a,b in zip(rings,rings[1:]):
 for i in range(len(a)):
  j=(i+1)%len(a);faces.append((a[i],a[j],b[j],b[i]))
faces.append(tuple(reversed(rings[0])));faces.append(tuple(rings[-1]))
mesh=bpy.data.meshes.new('Clean case return from actual source cross-section');mesh.from_pydata(verts,[],faces);mesh.update()
shell=bpy.data.objects.new('Clean rounded case return',mesh);bpy.context.collection.objects.link(shell);shell.parent=case.parent;mesh.materials.append(mat)
layer=mesh.uv_layers.new(name=uvname)
for l in layer.data:l.uv=green
for f in mesh.polygons:f.use_smooth=len(f.vertices)==4
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
def make_part(name,verts,faces,texel):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
 ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);ob.parent=case.parent;mesh.materials.append(mat)
 layer=mesh.uv_layers.new(name=uvname)
 for l in layer.data:l.uv=texel
 for f in mesh.polygons:f.use_smooth=len(f.vertices)<=4
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 return ob

# A new padded contact cushion replaces the original whole harness-bearing
# return; no old tab, loop or cut-off source padding fragment remains behind it.
padverts=[];padfaces=[];padrings=[];padcenter=.42
def pad_xy(t,rho):
 c=math.cos(t);s=math.sin(t)
 return (.225*rho*math.copysign(abs(c)**.72,c),padcenter+.280*rho*math.copysign(abs(s)**.72,s))
for rho,y in [(1,-.194),(.985,-.207),(.95,-.223),(.89,-.237),(.72,-.249),(.48,-.257),(.22,-.261),(.08,-.263)]:
 ring=[]
 for i in range(128):
  t=i*2*math.pi/128;x,z=pad_xy(t,rho)
  # Subtle central waist seam between two soft lumbar lobes.
  yy=y+.008*math.exp(-((z-padcenter)/.033)**2)*(1-rho*.4)
  ring.append(len(padverts));padverts.append((x,yy,z))
 padrings.append(ring)
for a,b in zip(padrings,padrings[1:]):
 for i in range(128):j=(i+1)%128;padfaces.append((a[i],a[j],b[j],b[i]))
padfaces.append(tuple(reversed(padrings[0])))
centerindex=len(padverts);padverts.append((0,-.255,padcenter))
for i in range(128):padfaces.append((padrings[-1][i],padrings[-1][(i+1)%128],centerindex))
pad=make_part('New soft leather contact cushion',padverts,padfaces,leather)

stitchverts=[];stitchfaces=[]
for i in range(64):
 t=i*2*math.pi/64;x,z=pad_xy(t,.912);x2,z2=pad_xy(t+.009,.912)
 tangent=Vector((x2-x,0,z2-z)).normalized();across=Vector((0,-1,0));other=tangent.cross(across).normalized()
 center=Vector((x,-.237,z));start=len(stitchverts)
 for end in (-1,1):
  for k in range(6):
   angle=k*math.pi/3;p=center+tangent*(end*.0048)+.0013*(across*math.cos(angle)+other*math.sin(angle));stitchverts.append(tuple(p))
 for k in range(6):j=(k+1)%6;stitchfaces.append((start+k,start+j,start+6+j,start+6+k))
 stitchfaces.append(tuple(start+k for k in reversed(range(6))));stitchfaces.append(tuple(start+6+k for k in range(6)))
stitches=make_part('Cushion saddle stitching',stitchverts,stitchfaces,thread)

# Close the roof return directly below the unchanged original rotating joint.
bpy.ops.mesh.primitive_cube_add(size=1,location=(.022,-.080,.760))
collar=bpy.context.object;collar.name='Enamel bearing plinth';collar.dimensions=(.29,.19,.065)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
collar.data.materials.append(mat);collar.data.uv_layers.active.name=uvname
for l in collar.data.uv_layers.active.data:l.uv=green
modifier=collar.modifiers.new('Rounded bearing seat','BEVEL');modifier.width=.022;modifier.segments=4
bpy.context.view_layer.objects.active=collar;bpy.ops.object.modifier_apply(modifier=modifier.name)
for f in collar.data.polygons:f.use_smooth=True

bpy.ops.object.select_all(action='DESELECT');case.select_set(True)
for ob in (shell,pad,stitches,collar):ob.select_set(True)
bpy.context.view_layer.objects.active=case;bpy.ops.object.join()
caprepairs=0
for face in case.data.polygons:
 points=[case.data.vertices[i].co for i in face.vertices]
 if all(abs(p.z-meta['topPivot'][2])<1e-5 for p in points) and (face.center-Vector(meta['topPivot'])).length<.23:
  # These historical spindle split caps had averaged atlas UVs which produce
  # a black/striped patch. They are solid surfaces, not an open water-tightness
  # problem; give the little machined bearing cap one clean brass texel.
  for i in face.loop_indices:case.data.uv_layers.active.data[i].uv=brass
  caprepairs+=1
for ob in list(bpy.context.scene.objects):
 if ob.name.startswith(('Harness.','StrapFront.','StrapUpper.')):
  bpy.data.objects.remove(ob,do_unlink=True);continue
 if ob.type=='MESH':ob.hide_render=False
meta['harnessNames']=[]
for k in list(meta):
 if k.startswith('harness'):meta.pop(k)
actual=np.asarray([list(v.co) for v in case.data.vertices])
meta['fixedBoundsMin']=actual.min(0).tolist();meta['fixedBoundsMax']=actual.max(0).tolist()
meta['caseBoundsMin'][1]=float(actual[:,1].min());meta['contactPlaneY']=-.253
meta['outputTriangles']={'Case':sum(len(f.vertices)-2 for f in case.data.polygons),'TopRotor':sum(len(f.vertices)-2 for f in bpy.data.objects['TopRotor'].data.polygons)}
meta['repairs']['straplessCase20261105']={'method':'actual case cross-section loft, no Boolean operations','cutPlaneY':plane,'removedSourceFaces':removed_count,'radioProtectedY':plane,'hullVertices':len(hull),'bearingCapUvRepairs':caprepairs}
(OUT/'Meta.json').write_text(json.dumps(meta,indent=2))
for name in ('BaseColor.png','Normal.png','MetallicSmoothness.png'):shutil.copy2(BASE/name,OUT/name)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'editable.blend'))
bpy.ops.object.select_all(action='DESELECT')
for name in ('BackpackRoot','Case','TopPivot','TopRotor'):bpy.data.objects[name].select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['BackpackRoot']
bpy.ops.export_scene.fbx(filepath=str(OUT/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
report={'removedSourceFaces':removed_count,'cutPlaneY':plane,'hullVertices':len(hull),'ringSamples':len(samples),'radioProtectedY':plane,'sourceBodyHeight':.83,'caseVertices':len(case.data.vertices),'bearingCapUvRepairs':caprepairs}
(OUT/'study.json').write_text(json.dumps(report,indent=2))
for ob in bpy.context.scene.objects:
 if ob.type=='MESH':ob.hide_render=ob!=case
c=source.light_camera();sc=bpy.context.scene;sc.cycles.samples=8
sc.render.resolution_x=680;sc.render.resolution_y=680;c.data.ortho_scale=1.35
target=Vector((0,0,.5))
for view,pos in [('contact',(0,-6,.6)),('side',(-6,0,.6)),('radio',(0,6,.6)),('top',(0,0,6))]:
 c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler()
 sc.render.filepath=str(OUT/(view+'.png'));bpy.ops.render.render(write_still=True)
print('CASE_LOFT_CANDIDATE',json.dumps(report),flush=True)
