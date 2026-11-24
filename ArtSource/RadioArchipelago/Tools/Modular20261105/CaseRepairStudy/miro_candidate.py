from pathlib import Path
import bpy,bmesh,json,sys,math,numpy as np
from mathutils import Vector
HERE=Path(__file__).parent
OUT=HERE/'MiroGardenCandidate';OUT.mkdir(exist_ok=True)
BASE=HERE.parent/'StraplessCases/Before/Miro_Garden'
sys.path.insert(0,str(HERE.parents[1]/'Modular20261104'))
import pack_inspect as source
bpy.ops.wm.open_mainfile(filepath=str(BASE/'editable.blend'),load_ui=False)
case=bpy.data.objects['Case'];meta=json.loads((BASE/'Meta.json').read_text())
mat=case.data.materials[0];node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
image=node.inputs['Base Color'].links[0].from_node.image
pixels=np.empty(image.size[0]*image.size[1]*4,dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(image.size[1],image.size[0],4)
texuv=np.asarray([list(l.uv) for l in case.data.uv_layers.active.data]);xy=np.clip((texuv*np.asarray(image.size[:])).astype(int),0,np.array(image.size[:])-1)
rgb=pixels[xy[:,1],xy[:,0],:3];error=((rgb-np.asarray((.34,.53,.38)))**2).sum(1)
swatch=Vector(texuv[int(np.argmin(error))])
bm=bmesh.new();bm.from_mesh(case.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
source_boundaries=len([e for e in bm.edges if e.is_boundary])
# Split the entire source at every selection boundary before dropping any
# polygons: adjacent source faces keep a shared edge, with no T junctions.
for axis,values in [(0,(-.35,-.20,.20,.35)),(1,(-.015,)),(2,(.23,.26,.57,.63,.83))]:
 for value in values:
  normal=Vector((0,0,0));normal[axis]=1
  co=Vector((0,0,0));co[axis]=value
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=co,plane_no=normal,clear_inner=False,clear_outer=False)
def mask(p):
 x=abs(p.x)
 if p.y>=-.015:return False
 return (x>.20 and x<.35 and (p.z>.57 or p.z<.26)) or (x>=.35 and (p.z>.63 or p.z<.23)) or (p.z>.83 and x>.16)
selected=[f for f in bm.faces if mask(f.calc_center_median())]
removed=len(selected);bmesh.ops.delete(bm,geom=selected,context='FACES')
for v in list(bm.verts):
 if not v.link_faces:bmesh.ops.delete(bm,geom=[v],context='VERTS')
boundary=[e for e in bm.edges if e.is_boundary]
uv=bm.loops.layers.uv.active
caps=bmesh.ops.holes_fill(bm,edges=boundary,sides=0)['faces']
for f in caps:
 f.smooth=True
 for l in f.loops:l[uv].uv=swatch
bmesh.ops.triangulate(bm,faces=caps)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(case.data);bm.free();case.data.update()
for ob in list(bpy.context.scene.objects):
 if ob.name.startswith(('Harness.','StrapFront.','StrapUpper.')):
  bpy.data.objects.remove(ob,do_unlink=True);continue
 if ob.type=='MESH':ob.hide_render=ob!=case
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'editable.blend'))
report={'removedFaces':removed,'sourceBoundaryEdges':source_boundaries,'capFaces':len(caps),'radioFacadeYProtected':-.015,'method':'split all planes; exact face deletion inside four mount volumes; close resulting cap loops; no Boolean modifiers'}
(OUT/'study.json').write_text(json.dumps(report,indent=2))
c=source.light_camera();sc=bpy.context.scene;sc.cycles.samples=8
sc.render.resolution_x=680;sc.render.resolution_y=680;c.data.ortho_scale=1.35
target=Vector((0,0,.5))
for view,pos in [('contact',(0,-6,.6)),('side',(-6,0,.6)),('radio',(0,6,.6)),('top',(0,0,6))]:
 c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler()
 sc.render.filepath=str(OUT/(view+'.png'));bpy.ops.render.render(write_still=True)
print('CASE_LOCAL_CANDIDATE',json.dumps(report),flush=True)
