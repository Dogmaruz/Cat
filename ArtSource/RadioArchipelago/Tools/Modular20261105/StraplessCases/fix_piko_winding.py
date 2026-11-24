"""Restore source-surface orientation only; never orient an open case globally."""
from pathlib import Path
import bpy,bmesh,json,sys,shutil,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
P=Path(__file__).parent;sys.path.insert(0,str(P));import clean_cases as c
OUT=P/'PikoWindingCorrection';OUT.mkdir(exist_ok=True);folder=P/'Loft/Piko'
if not (OUT/'Before').exists():shutil.copytree(folder,OUT/'Before')
bpy.ops.wm.open_mainfile(filepath=str(P/'Before/Piko/editable.blend'),load_ui=False)
ob=bpy.data.objects['Case'];source=BVHTree.FromPolygons([v.co.copy() for v in ob.data.vertices],[list(f.vertices) for f in ob.data.polygons]);meta0=json.loads((P/'Before/Piko/Meta.json').read_text());plane=meta0['contactPlaneY']+.11
sourceTriangles=[([ob.data.vertices[i].co.copy() for i in f.vertices],[Vector((*ob.data.uv_layers.active.data[i].uv,0)) for i in f.loop_indices]) for f in ob.data.polygons]
def reference(q,uv):
 hits=source.find_nearest_range(q,.00005)
 if not hits:return None
 def error(hit):
  p,n,index,d=hit;vertices,texcoords=sourceTriangles[index]
  if len(vertices)!=3:return d+100
  mapped=barycentric_transform(p,*vertices,*texcoords)
  return (mapped-Vector((*uv,0))).length+d
 return min(hits,key=error)
bpy.ops.wm.open_mainfile(filepath=str(folder/'editable.blend'),load_ui=False)
ob=bpy.data.objects['Case']
def signature(ob):
 uv=ob.data.uv_layers.active.data
 return sorted(tuple(round(float(x),7) for x in (*ob.data.vertices[l.vertex_index].co,*uv[i].uv)) for i,l in enumerate(ob.data.loops))
original=signature(ob);flipIndices=[];matched=0
for f in ob.data.polygons:
 q=f.center
 if q.y<plane+.008:continue
 faceUv=sum((ob.data.uv_layers.active.data[i].uv.copy() for i in f.loop_indices),Vector((0,0)))/len(f.loop_indices)
 result=reference(q,faceUv)
 if result is None:continue
 hit,n,index,d=result
 matched+=1
 if f.normal.dot(n)<-.3:flipIndices.append(f.index)
bm=bmesh.new();bm.from_mesh(ob.data);bm.faces.ensure_lookup_table()
for index in flipIndices:bm.faces[index].normal_flip()
flipped=len(flipIndices)
# normal_update recomputes normals from winding. It never chooses/reverses
# island orientation, unlike recalc_face_normals on an open generated mesh.
bm.normal_update();bm.to_mesh(ob.data);bm.free();ob.data.update()
assert original==signature(ob),'Position/UV corner mapping changed'
remaining=0
for f in ob.data.polygons:
 if f.center.y<plane+.008:continue
 faceUv=sum((ob.data.uv_layers.active.data[i].uv.copy() for i in f.loop_indices),Vector((0,0)))/len(f.loop_indices)
 result=reference(f.center,faceUv)
 if result is not None and f.normal.dot(result[1])<-.3:remaining+=1
print('PIKO_REMAINING',flipped,remaining,flush=True)
assert remaining==0
meta=json.loads((folder/'Meta.json').read_text());meta['repairs']['straplessCase20261105']['sourceFacadeWindingRestored']={'flippedFaces':flipped,'matchedSourceFaces':matched,'remainingOpposedFaces':remaining,'positionsAndUvUnchanged':True}
(folder/'Meta.json').write_text(json.dumps(meta,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(folder/'editable.blend'))
bpy.ops.object.select_all(action='DESELECT')
for name in ('BackpackRoot','Case','TopPivot','TopRotor'):bpy.data.objects[name].select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['BackpackRoot']
bpy.ops.export_scene.fbx(filepath=str(folder/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
report={'matchedSourceFacadeFaces':matched,'flippedFaces':flipped,'remainingOpposedSourceFaces':remaining,'positionsAndCornerUvUnchanged':True,'globalReorientationUsed':False}
(OUT/'fixed.json').write_text(json.dumps(report,indent=2));c.render('Piko')
print('PIKO_WINDING_FIXED',json.dumps(report),flush=True)
