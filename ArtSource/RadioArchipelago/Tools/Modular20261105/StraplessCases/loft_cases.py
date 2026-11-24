"""Contour-fitted clean back returns; source radio shell stays untouched."""
from pathlib import Path
import bpy,bmesh,json,sys,math
import numpy as np
from mathutils import Vector
P=Path(__file__).parent;sys.path.insert(0,str(P));import clean_cases as c;import outline_cases as o
sys.path.insert(0,str(c.ROOT/'ArtSource/RadioArchipelago/Tools/Modular20261104'));import pack_author
OUT=P/'Loft';OUT.mkdir(exist_ok=True)

def make_return(case,profile,rings,texel,label):
 # Profile is the actual case section, not a nominal square/ellipse. Every ring
 # follows the same individual shape, including the softly rounded roof.
 pts=np.asarray(profile);center=(pts.min(0)+pts.max(0))*.5
 verts=[];faces=[];n=len(profile)
 for y,scale in rings:
  for x,z in (pts-center)*scale+center:verts.append((x,y(x,z) if callable(y) else y,z))
 for k in range(len(rings)-1):
  for i in range(n):j=(i+1)%n;faces.append((k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i))
 faces.append(tuple(reversed(range(n))));faces.append(tuple((len(rings)-1)*n+i for i in range(n)))
 mesh=bpy.data.meshes.new(label);mesh.from_pydata(verts,[],faces);mesh.update();mesh.materials.append(case.data.materials[0])
 ob=bpy.data.objects.new(label,mesh);bpy.context.collection.objects.link(ob);ob.parent=case.parent
 uv=mesh.uv_layers.new(name=case.data.uv_layers.active.name)
 for l in uv.data:l.uv=texel
 for f in mesh.polygons:f.use_smooth=len(f.vertices)==4
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 bpy.ops.object.select_all(action='DESELECT');case.select_set(True);ob.select_set(True);bpy.context.view_layer.objects.active=case;bpy.ops.object.join()

def run(name):
 bpy.ops.wm.open_mainfile(filepath=str(c.BACKUP/name/'editable.blend'),load_ui=False)
 meta=json.loads((c.BACKUP/name/'Meta.json').read_text());case=bpy.data.objects['Case'];root=bpy.data.objects['BackpackRoot'];top=bpy.data.objects['TopPivot'];rotor=bpy.data.objects['TopRotor']
 for ob in list(bpy.context.scene.objects):
  if ob.name.startswith('Harness.'):bpy.data.objects.remove(ob,do_unlink=True)
 pad=meta['contactPlaneY'];h=meta['caseBoundsMax'][2];plane=pad+.11;protected=pad+.15
 if name=='Rumi_Orbit':plane=pad+.21;protected=pad+.27
 if name=='Nori_Nocturne':plane=pad+.21;protected=pad+.27
 if name=='Vela_Cloud':plane=pad+.23;protected=pad+.29
 def smooth(a,b,v):
  t=max(0,min(1,(v-a)/(b-a)));return t*t*(3-2*t)
 def return_front(x,z):
  return plane+.004
 enamel=c.source_swatch(case,c.ENAMEL[name]);sourceSignature=o.signature(case,protected)
 bm=bmesh.new();bm.from_mesh(case.data);uv=bm.loops.layers.uv.active
 face=min(bm.faces,key=lambda f:(f.calc_center_median()-Vector((0,pad,h*.5))).length_squared)
 leather=sum((l[uv].uv.copy() for l in face.loops),Vector((0,0)))/len(face.loops)
 pivot=Vector(meta['topPivot'])
 # Obtain the genuine core outline from the original front/side skin; exclude
 # only the central tall bearing. This constrains the new back to this model.
 a=np.asarray([list(v.co) for v in case.data.vertices]);v=a[(a[:,1]>max(0,pad+.04))&(a[:,2]>.005)&(a[:,2]<h)]
 v=v[~(((v[:,0]-pivot.x)**2+(v[:,1]-pivot.y)**2<.16**2)&(v[:,2]>h*.78))]
 body=o.hull(v[:,[0,2]]);planes=[]
 for aa,bb in zip(body,body[1:]+body[:1]):
  d=Vector((bb[0]-aa[0],0,bb[1]-aa[1]));n=Vector((d.z,0,-d.x)).normalized();planes.append((Vector((aa[0],0,aa[1])),n))
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
 o.subdivide(bm,(0,plane,0),(0,1,0))
 # Preserve the source bearing connected to its source roof seat. A centroid
 # allowlist avoids cropping the joint when the return plane crosses its rim.
 old=[f for f in bm.faces if f.calc_center_median().y<plane-1e-7 and not ((f.calc_center_median().xy-pivot.xy).length<.17 and f.calc_center_median().z>h*.98)]
 bmesh.ops.delete(bm,geom=old,context='FACES')
 loose=[v for v in bm.verts if not v.link_faces]
 if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
 roof=min(h*.92,pivot.z-.07)
 originalSection=[(v.co.x,min(v.co.z,roof)) for v in bm.verts if abs(v.co.y-plane)<2e-5 and .005<v.co.z<h and all((v.co-p).dot(n)<.001 for p,n in planes)]
 # All facade surfaces beyond the protected region remain, including source
 # corners and speakers. Only wearer-facing projections above the body outline
 # are removed; the original spindle is protected by its actual source pivot.
 for p,n in planes:o.subdivide(bm,p,n)
 o.subdivide(bm,(0,protected,0),(0,1,0))
 remove=[]
 for f in bm.faces:
  q=f.calc_center_median();post=(q.xy-pivot.xy).length<.17 and q.z>h*.98
  if q.y<protected and not post and any((q-p).dot(n)>.001 for p,n in planes):remove.append(f)
 bmesh.ops.delete(bm,geom=remove,context='FACES')
 loose=[v for v in bm.verts if not v.link_faces]
 if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
 # Boundary sections from the Case itself include its true casing silhouette.
 # Their convex core is clean even if tiny old fixture roots produced satellite
 # loops at the original section. It never extends into the front radio panel.
 profile=o.hull(originalSection)
 if len(profile)<8:raise RuntimeError('Insufficient real case section '+name)
 # Chaikin corner refinement only softens the contour. It stays inside the
 # measured case section, so no decorative facade is covered by the return.
 for _ in range(3):
  refined=[]
  for aa,bb in zip(profile,profile[1:]+profile[:1]):
   a=np.asarray(aa);b=np.asarray(bb);refined.extend(((a*.75+b*.25).tolist(),(a*.25+b*.75).tolist()))
  profile=refined
 # Rumi's generated input includes intentional/open speaker and roof edge
 # networks. A generic holes_fill invents faces across the upper grille. Its
 # closed replacement return already seals the rear cut without such fans.
 if name!='Rumi':o.fill(bm,enamel,True)
 if name=='Piko':
  # Open source islands must keep the original winding. Global orientation
  # can reverse the facade; Unity culls it while Blender previews both sides.
  bm.normal_update()
 else:bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 bm.to_mesh(case.data);bm.free();case.data.update()
 make_return(case,profile,[(return_front,1.002),(pad+.035,.993),(pad+.015,.965)],enamel,'Smooth fitted case back')
 # Small soft contact pad lies on the clean return; no full-depth generic box.
 make_return(case,profile,[(pad+.020,.81),(pad-.005,.83),(pad-.014,.805)],leather,'Soft contact pad')
 # A rounded low seat closes the return below the unchanged original joint;
 # raised dials are not allowed to inflate the new casing roof.
 seatBottom=roof-.018;seatTop=min(pivot.z-.01,h+.02)
 bpy.ops.mesh.primitive_cube_add(size=1,location=(pivot.x,pivot.y,(seatBottom+seatTop)*.5))
 seat=bpy.context.object;seat.name='Clean enamel bearing seat';seat.dimensions=(.25,.27 if name=='Rumi' else .19,seatTop-seatBottom)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);seat.data.materials.append(case.data.materials[0]);seat.data.uv_layers.active.name=case.data.uv_layers.active.name
 for l in seat.data.uv_layers.active.data:l.uv=enamel
 bevel=seat.modifiers.new('Soft machined seat edges','BEVEL');bevel.width=.022;bevel.segments=4;bpy.context.view_layer.objects.active=seat;bpy.ops.object.modifier_apply(modifier=bevel.name)
 for f in seat.data.polygons:f.use_smooth=True
 bpy.ops.object.select_all(action='DESELECT');case.select_set(True);seat.select_set(True);bpy.context.view_layer.objects.active=case;bpy.ops.object.join()
 # The spindle split is an original preparation cap, not part of the painted
 # case. Keep its exact geometry and give the cap a clean matching brass texel.
 brass=c.source_swatch(case,(.68,.48,.20));uvlayer=case.data.uv_layers.active.data
 for f in case.data.polygons:
  capNear=all(abs(case.data.vertices[i].co.z-pivot.z)<(.0015 if name=='Rumi' else 1e-5) for i in f.vertices)
  if capNear and (f.center-pivot).length<.23:
   for i in f.loop_indices:uvlayer[i].uv=brass
 afterSignature=o.signature(case,protected);missing=sourceSignature-afterSignature
 for key in list(meta):
  if key.startswith('harness'):meta.pop(key)
 meta['harnessNames']=[]
 meta['fixedBoundsMin'],meta['fixedBoundsMax']=c.bb(case);meta['contactPlaneY']=pad-.014
 meta['outputTriangles']={ob.name:sum(len(f.vertices)-2 for f in ob.data.polygons) for ob in (case,rotor)}
 report={'method':'actual case section contour loft; no source Boolean','originalPadY':pad,'cutPlaneY':plane,'protectedFrontY':protected,'sourceProtectedCorners':len(sourceSignature),'protectedCornersMissing':len(missing),'sectionProfile':profile}
 meta.setdefault('repairs',{})['straplessCase20261105']=report
 folder=OUT/name;folder.mkdir(exist_ok=True);(folder/'Meta.json').write_text(json.dumps(meta,indent=2));(folder/'validation.json').write_text(json.dumps(report,indent=2))
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/'editable.blend'))
 bpy.ops.object.select_all(action='DESELECT')
 for ob in (root,case,top,rotor):ob.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=str(folder/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
 c.render(name)
 print('LOFT_CASE',name,json.dumps(report),flush=True)

if __name__=='__main__':
 names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sorted(p.name for p in c.BACKUP.iterdir() if p.is_dir())
 for name in names:run(name)
