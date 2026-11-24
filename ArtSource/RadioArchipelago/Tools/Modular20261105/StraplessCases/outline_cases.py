"""Deterministic local face clipping, preserving every protected source corner.

No solid Boolean touches a source case: generated meshes may contain internal
surfaces which make solid classification ambiguous. Global subdivisions keep
UV interpolation exact; removal is explicitly restricted to wearer-side roots.
"""
from pathlib import Path
import bpy,bmesh,json,sys,math
import numpy as np
from mathutils import Vector
P=Path(__file__).parent;sys.path.insert(0,str(P));import clean_cases as c
OUT=P/'Final';OUT.mkdir(exist_ok=True)

def hull(points):
 p=sorted(set((round(float(x),7),round(float(z),7)) for x,z in points))
 def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
 lower=[]
 for a in p:
  while len(lower)>1 and cross(lower[-2],lower[-1],a)<=0:lower.pop()
  lower.append(a)
 upper=[]
 for a in reversed(p):
  while len(upper)>1 and cross(upper[-2],upper[-1],a)<=0:upper.pop()
  upper.append(a)
 return lower[:-1]+upper[:-1]

def subdivide(bm,center,normal):
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=center,plane_no=normal,clear_inner=False,clear_outer=False)

def fill(bm,texel,soft=False):
 uv=bm.loops.layers.uv.active
 edges=[e for e in bm.edges if e.is_boundary]
 fs=bmesh.ops.holes_fill(bm,edges=edges,sides=0)['faces'] if edges else []
 for f in fs:
  f.smooth=soft
  for l in f.loops:l[uv].uv=texel
 result=bmesh.ops.triangulate(bm,faces=fs)
 fs=[f for f in result['faces'] if f.is_valid]
 if soft and fs:
  edges=set(e for f in fs for e in f.edges)
  boundary={v for e in edges if any(f not in fs for f in e.link_faces) for v in e.verts}
  subdivision=bmesh.ops.subdivide_edges(bm,edges=list(edges),cuts=2,use_grid_fill=True)
  interior={v for x in subdivision['geom_inner'] for v in (x.verts if isinstance(x,bmesh.types.BMFace) else x.verts if isinstance(x,bmesh.types.BMEdge) else [x]) if isinstance(v,bmesh.types.BMVert) and v not in boundary}
  for _ in range(10):bmesh.ops.smooth_vert(bm,verts=list(interior),factor=.5,use_axis_x=True,use_axis_y=True,use_axis_z=True)
  for v in interior:
   for f in v.link_faces:
    f.smooth=True
    for l in f.loops:l[uv].uv=texel
 return fs

def signature(ob,threshold):
 m=ob.data;uv=m.uv_layers.active.data
 return {tuple(round(float(v),5) for v in (*m.vertices[l.vertex_index].co,*uv[i].uv)) for i,l in enumerate(m.loops) if m.vertices[l.vertex_index].co.y>threshold}

def run(name):
 bpy.ops.wm.open_mainfile(filepath=str(c.BACKUP/name/'editable.blend'),load_ui=False)
 meta=json.loads((c.BACKUP/name/'Meta.json').read_text());case=bpy.data.objects['Case'];root=bpy.data.objects['BackpackRoot'];top=bpy.data.objects['TopPivot'];rotor=bpy.data.objects['TopRotor']
 originalTriangles=sum(len(f.vertices)-2 for f in case.data.polygons)
 for ob in list(bpy.context.scene.objects):
  if ob.name.startswith('Harness.'):bpy.data.objects.remove(ob,do_unlink=True)
 h=meta['caseBoundsMax'][2];pad=meta['contactPlaneY'];protected=pad+.15;sourceSignature=signature(case,protected)
 enamel=c.source_swatch(case,c.ENAMEL[name]);bm=bmesh.new();bm.from_mesh(case.data);uv=bm.loops.layers.uv.active
 face=min(bm.faces,key=lambda f:(f.calc_center_median()-Vector((0,pad,h*.5))).length_squared)
 leather=sum((l[uv].uv.copy() for l in face.loops),Vector((0,0)))/len(face.loops)
 pivot=Vector(meta['topPivot'])
 # The source radio-facing silhouette supplies the correct box/oval profile.
 # Fixed bearings are excluded from this body outline, never from the case.
 a=np.array([list(v.co) for v in case.data.vertices]);frontY=max(pad+.06,float(meta['caseBoundsMax'][1])*.65)
 valid=a[(a[:,1]>frontY)&(a[:,2]<h)&(a[:,2]>.01)]
 valid=valid[~(((valid[:,0]-pivot.x)**2+(valid[:,1]-pivot.y)**2<.16**2)&(valid[:,2]>h*.78))]
 outline=hull(valid[:,[0,2]]);planes=[]
 for aa,bb in zip(outline,outline[1:]+outline[:1]):
  d=Vector((bb[0]-aa[0],0,bb[1]-aa[1]));normal=Vector((d.z,0,-d.x)).normalized()
  planes.append((Vector((aa[0],0,aa[1])),normal))
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
 c.trim(bm,Vector((0,pad+.018,0)),Vector((0,1,0)));fill(bm,leather)
 # Every clipping plane subdivides the complete mesh. This avoids T junctions
 # at a local selection border; outside source triangles remain on their exact
 # original surface with their source UV corners preserved.
 cuts=[((0,pad+.115,0),(0,1,0)),((0,pad+.075,0),(0,1,0)),((0,0,h*.76),(0,0,1)),((0,0,h*.25),(0,0,1))]
 for x in (-.405,-.21,.21,.405):cuts.append(((x,0,0),(1,0,0)))
 for p,n in cuts:subdivide(bm,p,n)
 for p,n in planes:subdivide(bm,p,n)
 removed=[]
 for f in bm.faces:
  q=f.calc_center_median();bearing=(q.xy-pivot.xy).length<.15 and q.z>h*.76
  if bearing:continue
  corner=abs(q.x)>.21 and (q.z>h*.76 or q.z<h*.25)
  side=abs(q.x)>.405 and h*.2<q.z<h*.84
  outside=any((q-p).dot(n)>.001 for p,n in planes)
  if ((corner and q.y<pad+.115) or (side and q.y<pad+.075) or (outside and q.y<protected)):
   removed.append(f)
 bmesh.ops.delete(bm,geom=removed,context='FACES')
 loose=[v for v in bm.verts if not v.link_faces]
 if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
 bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
 fill(bm,enamel,True)
 # Generated input already contains a few tiny unclosed creases. Resolve
 # residual edge networks after the locally clipped return has been welded.
 edges=[e for e in bm.edges if e.is_boundary]
 if edges:
  result=bmesh.ops.triangle_fill(bm,edges=edges,use_beauty=True)
  for f in result.get('geom',[]):
   if isinstance(f,bmesh.types.BMFace):
    f.smooth=True
    for l in f.loops:l[uv].uv=enamel
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(case.data);bm.free();case.data.update()
 # The clean compact pad covers only the wearer contact face. There is no
 # replacement case volume extending towards or across the source radio front.
 c.back_panel(case,meta,pad+.018,leather,True)
 afterSignature=signature(case,protected);missing=sourceSignature-afterSignature
 meta['harnessNames']=[]
 for key in list(meta):
  if key.startswith('harness'):meta.pop(key)
 meta['fixedBoundsMin'],meta['fixedBoundsMax']=c.bb(case);meta['contactPlaneY']=pad-.012
 meta['outputTriangles']={o.name:sum(len(f.vertices)-2 for f in o.data.polygons) for o in (case,rotor)}
 report={'method':'local UV-preserving global subdivisions and masked face removal; no source Boolean','originalCaseTriangles':originalTriangles,'sourceProtectedY':protected,'protectedSourceCorners':len(sourceSignature),'protectedCornersMissing':len(missing),'removedRootCells':len(removed),'bodyOutline':outline}
 meta.setdefault('repairs',{})['straplessCase20261105']=report
 folder=OUT/name;folder.mkdir(exist_ok=True);(folder/'Meta.json').write_text(json.dumps(meta,indent=2));(folder/'validation.json').write_text(json.dumps(report,indent=2))
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/'editable.blend'))
 bpy.ops.object.select_all(action='DESELECT')
 for ob in (root,case,top,rotor):ob.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=str(folder/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
 c.render(name)
 print('OUTLINE_CASE',name,json.dumps(report),flush=True)

if __name__=='__main__':
 names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sorted(p.name for p in c.BACKUP.iterdir() if p.is_dir())
 for name in names:run(name)
