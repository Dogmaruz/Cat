from pathlib import Path
import bpy,bmesh,json,sys,numpy as np
from mathutils import Vector
HERE=Path(__file__).parent
BASE=HERE.parent/'StraplessCases/Before/Miro_Garden'
sys.path.insert(0,str(HERE.parents[1]/'Modular20261104'))
import pack_inspect as source
bpy.ops.wm.open_mainfile(filepath=str(BASE/'editable.blend'),load_ui=False)
case=bpy.data.objects['Case']
colors=np.load(HERE/'case-face-colors.npy')
centers=np.load(HERE/'case-face-centers.npy')
brown=(colors[:,0]>colors[:,1]*1.05)&(colors[:,0]>colors[:,2]*1.25)
x=np.abs(centers[:,0]);y=centers[:,1];z=centers[:,2]
upper=((x>.20)&(x<.38)&(z>.57))|((x>=.38)&(z>.64))
lower=((x>.20)&(x<.38)&(z<.25))|((x>=.38)&(z<.24))
patch=(upper|lower)&(y<-.015)
mask=(patch&brown)|((x>.16)&(z>.83)&(y<-.015))
bm=bmesh.new();bm.from_mesh(case.data);bm.faces.ensure_lookup_table()
selected=[f for f in bm.faces if mask[f.index]]
removed=len(selected)
bmesh.ops.delete(bm,geom=selected,context='FACES')
for v in list(bm.verts):
 if not v.link_faces:bmesh.ops.delete(bm,geom=[v],context='VERTS')
boundary=[e for e in bm.edges if e.is_boundary]
uv=bm.loops.layers.uv.active
mat=case.data.materials[0];node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
image=node.inputs['Base Color'].links[0].from_node.image
pixels=np.empty(image.size[0]*image.size[1]*4,dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(image.size[1],image.size[0],4)
texuv=np.asarray([list(l.uv) for l in case.data.uv_layers.active.data]);xy=np.clip((texuv*np.asarray(image.size[:])).astype(int),0,np.array(image.size[:])-1)
rgb=pixels[xy[:,1],xy[:,0],:3];error=((rgb-np.asarray((.34,.53,.38)))**2).sum(1)
swatch=Vector(texuv[int(np.argmin(error))])
caps=bmesh.ops.holes_fill(bm,edges=boundary,sides=0)['faces']
for f in caps:
 f.smooth=True
 for l in f.loops:l[uv].uv=swatch
bmesh.ops.triangulate(bm,faces=caps)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(case.data);bm.free();case.data.update()
for ob in list(bpy.context.scene.objects):
 if ob.name.startswith('Harness.'):
  bpy.data.objects.remove(ob,do_unlink=True)
  continue
 if ob.type=='MESH':ob.hide_render=ob!=case
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'MiroGarden-local-excision.blend'))
report={'removedFaces':removed,'protectedRadioFaces':int((y>=0).sum()),'radioFacesRemoved':int((mask&(y>=0)).sum()),'holeCaps':len(caps),'mask':'tan+brass inside upper/lower wearer-facing patches only; upper arche beyond true .83m case roof'}
(HERE/'local-excision.json').write_text(json.dumps(report,indent=2))
c=source.light_camera();sc=bpy.context.scene;sc.cycles.samples=8
sc.render.resolution_x=680;sc.render.resolution_y=680;c.data.ortho_scale=1.35
target=Vector((0,0,.5))
for view,pos in [('contact',(0,-6,.6)),('side',(-6,0,.6)),('radio',(0,6,.6)),('top',(0,0,6))]:
 c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler()
 sc.render.filepath=str(HERE/('MiroGarden-local-'+view+'.png'));bpy.ops.render.render(write_still=True)
print('CASE_LOCAL_EXCISION',json.dumps(report),flush=True)
