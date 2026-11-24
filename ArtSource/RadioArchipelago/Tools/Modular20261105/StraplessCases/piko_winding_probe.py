"""Compare facade winding to the immutable, correctly imported original."""
from pathlib import Path
import bpy,json,sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P/'Before/Piko/editable.blend'),load_ui=False)
ob=bpy.data.objects['Case'];source=BVHTree.FromPolygons([v.co.copy() for v in ob.data.vertices],[list(f.vertices) for f in ob.data.polygons]);meta=json.loads((P/'Before/Piko/Meta.json').read_text());plane=meta['contactPlaneY']+.11
bpy.ops.wm.open_mainfile(filepath=str(P/'Loft/Piko/editable.blend'),load_ui=False)
ob=bpy.data.objects['Case'];matched=0;opposed=[];new=0
for f in ob.data.polygons:
 q=f.center
 if q.y<plane+.008:continue
 hit,n,index,d=source.find_nearest(q)
 if hit is None or d>.00005:new+=1;continue
 matched+=1
 if f.normal.dot(n)<-.3:opposed.append({'face':f.index,'point':list(q),'dot':f.normal.dot(n),'distance':d})
report={'matchedSourceFacadeFaces':matched,'opposedSourceFaces':len(opposed),'newFacesOutsideSource':new,'opposed':opposed}
(P/'PikoWindingCorrection').mkdir(exist_ok=True)
(P/'PikoWindingCorrection/probe.json').write_text(json.dumps(report,indent=2))
print('PIKO_WINDING',matched,len(opposed),'bounds',[[min(x['point'][i] for x in opposed),max(x['point'][i] for x in opposed)] for i in range(3)] if opposed else [],flush=True)
