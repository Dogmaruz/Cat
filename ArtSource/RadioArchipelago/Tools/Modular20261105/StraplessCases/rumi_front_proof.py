"""Matched native views and surface-sample check for the Rumi cap regression."""
from pathlib import Path
import bpy,json,sys,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).parent;sys.path.insert(0,str(P));import clean_cases as c
OUT=P/'RumiFrontCorrection';OUT.mkdir(exist_ok=True)
before=P/'Before/Rumi';staged=P/'Loft/Rumi';published=c.ROOT/'ArtSource/RadioArchipelago/ProcessedModular/Backpacks/Rumi'
bpy.ops.wm.open_mainfile(filepath=str(before/'editable.blend'),load_ui=False)
ob=bpy.data.objects['Case'];source=BVHTree.FromPolygons([v.co.copy() for v in ob.data.vertices],[list(f.vertices) for f in ob.data.polygons]);meta=json.loads((before/'Meta.json').read_text());plane=meta['contactPlaneY']+.11;pivot=Vector(meta['topPivot']);roof=min(meta['caseBoundsMax'][2]*.92,pivot.z-.07)
report=[]
for label,folder in [('source',before),('rejected',published),('corrected',staged)]:
 bpy.ops.wm.open_mainfile(filepath=str(folder/'editable.blend'),load_ui=False)
 ob=bpy.data.objects['Case'];off=[];tested=0
 for f in ob.data.polygons:
  # Seat is a deliberate replacement fixture. Main facade samples must remain
  # on the original surface; an invented cap over the grille violates this.
  for w in [(1/3,1/3,1/3),(.6,.2,.2),(.2,.6,.2)]:
   if len(f.vertices)!=3:continue
   a,b,d=(ob.data.vertices[i].co for i in f.vertices);q=a*w[0]+b*w[1]+d*w[2]
   seat=abs(q.x-pivot.x)<.16 and abs(q.y-pivot.y)<.16 and q.z>roof-.035
   if q.y<=plane+.012 or seat:continue
   tested+=1;hit=source.find_nearest(q)
   if hit[0] is None or hit[3]>.0001:off.append({'point':list(q),'distance':float(hit[3]) if hit[0] else None})
 report.append({'variant':label,'testedFacadeSamples':tested,'offSourceFacadeSamples':len(off),'maximumDeviation':max([x['distance'] or 0 for x in off],default=0),'examples':off[:12]})
 for o in bpy.context.scene.objects:
  if o.type=='MESH':o.hide_render=o!=ob
 camera=c.src.light_camera();sc=bpy.context.scene;sc.cycles.samples=12;sc.render.resolution_x=640;sc.render.resolution_y=720
 camera.data.ortho_scale=1.45;target=Vector((0,0,.55))
 for view,pos in [('front',(0,6,.1)),('roof-front',(3,6,2.4))]:
  camera.location=target+Vector(pos);camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(OUT/(label+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
(OUT/'surface-proof.json').write_text(json.dumps(report,indent=2));print('RUMI_SURFACE_PROOF',json.dumps([{k:v for k,v in r.items() if k!='examples'} for r in report]),flush=True)
