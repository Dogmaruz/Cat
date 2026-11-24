"""Read-only retained facade winding audit. Never edits or exports an asset."""
from pathlib import Path
import bpy,json,sys,math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
P=Path(__file__).parent;OUT=P/'WindingAudit';OUT.mkdir(exist_ok=True)
DIRECTIONS=[Vector(v).normalized() for v in [(0,1,0),(1,0,0),(-1,0,0),(0,0,1),(3,6,2.4),(-3,6,2.4)]]
rows=[]
names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sorted(d.name for d in (P/'Loft').iterdir() if d.is_dir())
for name in names:
 bpy.ops.wm.open_mainfile(filepath=str(P/'Before'/name/'editable.blend'),load_ui=False)
 ob=bpy.data.objects['Case'];mesh=ob.data
 source=BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices],[list(f.vertices) for f in mesh.polygons])
 triangles=[([mesh.vertices[i].co.copy() for i in f.vertices],[Vector((*mesh.uv_layers.active.data[i].uv,0)) for i in f.loop_indices]) for f in mesh.polygons]
 def reference(q,uv):
  hits=source.find_nearest_range(q,.00005)
  if not hits:return None
  def err(hit):
   p,n,index,d=hit;v,t=triangles[index]
   if len(v)!=3:return d+100
   return (barycentric_transform(p,*v,*t)-Vector((*uv,0))).length+d
  result=min(hits,key=err)
  return (*result,err(result))
 bpy.ops.wm.open_mainfile(filepath=str(P/'Loft'/name/'editable.blend'),load_ui=False)
 ob=bpy.data.objects['Case'];mesh=ob.data;meta=json.loads((P/'Loft'/name/'Meta.json').read_text());repair=meta['repairs']['straplessCase20261105'];plane=repair.get('cutPlaneY',repair.get('padTrimPlaneY'))
 if plane is None:raise RuntimeError('Missing cut plane '+name)
 row={'id':name,'cutPlaneY':plane,'faces':len(mesh.polygons),'matchedRetainedFacadeFaces':0,'uvConfirmedSourceFaces':0,'reversedRetainedFaces':0,'reversedRetainedArea':0.,'visibleReversedFaces':0,'visibleReversedArea':0.,'excludedInteriorReversedFaces':0,'excludedNewOrRemappedFaces':0,'examples':[]}
 for f in mesh.polygons:
  q=f.center
  if q.y<plane+.008:continue
  uv=sum((mesh.uv_layers.active.data[i].uv.copy() for i in f.loop_indices),Vector((0,0)))/len(f.loop_indices)
  result=reference(q,uv)
  if result is None:row['excludedNewOrRemappedFaces']+=1;continue
  p,n,index,d,uvError=result;row['matchedRetainedFacadeFaces']+=1
  if uvError<.01:row['uvConfirmedSourceFaces']+=1
  if f.normal.dot(n)>=-.3:continue
  row['reversedRetainedFaces']+=1;row['reversedRetainedArea']+=f.area
  exposed=False
  for direction in DIRECTIONS:
   if n.dot(direction)<.15:continue
   hit=source.ray_cast(p+direction*4,-direction,4.003)
   if hit[0] is not None and (hit[0]-p).length<.0015:exposed=True;break
  if exposed:
   row['visibleReversedFaces']+=1;row['visibleReversedArea']+=f.area
   if len(row['examples'])<8:row['examples'].append({'point':list(q),'dot':f.normal.dot(n),'area':f.area,'uvError':uvError})
  else:row['excludedInteriorReversedFaces']+=1
 # Tiny triangulated seam slivers can be numerous while covering less than
 # one mobile pixel. Report physical exposed area, not raw sliver count.
 row['significantOutwardRisk']=row['visibleReversedArea']>.0025 or (row['visibleReversedFaces']>20 and row['visibleReversedArea']>.0002)
 rows.append(row);(OUT/(name+'.json')).write_text(json.dumps(row,indent=2));print('WINDING_AUDIT',name,row['matchedRetainedFacadeFaces'],row['reversedRetainedFaces'],row['visibleReversedFaces'],round(row['visibleReversedArea'],7),row['significantOutwardRisk'],flush=True)
(OUT/'all20.json').write_text(json.dumps({'readOnly':True,'significantOutwardCases':[r['id'] for r in rows if r['significantOutwardRisk']],'entries':rows},indent=2))
