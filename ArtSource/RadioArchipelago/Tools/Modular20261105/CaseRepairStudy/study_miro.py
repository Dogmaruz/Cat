from pathlib import Path
import bpy, json, sys, numpy as np
from mathutils import Vector
HERE=Path(__file__).parent
BASE=HERE.parent/'StraplessCases/Before/Miro_Garden'
sys.path.insert(0,str(HERE.parents[1]/'Modular20261104'))
import pack_inspect as source
bpy.ops.wm.open_mainfile(filepath=str(BASE/'editable.blend'),load_ui=False)
case=bpy.data.objects['Case']
meta=json.loads((BASE/'Meta.json').read_text())
for ob in bpy.context.scene.objects:
 if ob.type=='MESH':ob.hide_render=ob!=case
v=np.array([list(x.co) for x in case.data.vertices])
np.save(HERE/'case-points.npy',v)
mat=case.data.materials[0]
node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
image=node.inputs['Base Color'].links[0].from_node.image
px=np.empty(image.size[0]*image.size[1]*4,dtype=np.float32);image.pixels.foreach_get(px);px=px.reshape(image.size[1],image.size[0],4)
fcenters=[];colors=[]
for f in case.data.polygons:
 uv=np.mean([case.data.uv_layers.active.data[i].uv[:] for i in f.loop_indices],axis=0)
 xy=np.clip((uv*np.array(image.size[:])).astype(int),0,np.array(image.size[:])-1)
 fcenters.append(f.center[:]);colors.append(px[xy[1],xy[0],:3])
np.save(HERE/'case-face-centers.npy',np.asarray(fcenters))
np.save(HERE/'case-face-colors.npy',np.asarray(colors))
out=[]
for ob in (case,bpy.data.objects['Harness.L'],bpy.data.objects['Harness.R']):
 p=np.asarray([list(x.co) for x in ob.data.vertices])
 rows=[]
 for z in np.arange(.60,1.08,.04):
  a=p[(p[:,2]>=z)&(p[:,2]<z+.04)]
  if len(a):rows.append({'z':float(z),'v':len(a),'lo':a.min(0).tolist(),'hi':a.max(0).tolist()})
 out.append({'name':ob.name,'rows':rows})
(HERE/'source-regions.json').write_text(json.dumps(out,indent=2))
c=source.light_camera();sc=bpy.context.scene;sc.cycles.samples=8
sc.render.resolution_x=680;sc.render.resolution_y=680;c.data.ortho_scale=1.35
target=Vector((0,0,.5))
for view,pos in [('contact',(0,-6,.6)),('side',(-6,0,.6)),('radio',(0,6,.6)),('top',(0,0,6))]:
 c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler()
 sc.render.filepath=str(HERE/('MiroGarden-original-'+view+'.png'));bpy.ops.render.render(write_still=True)
print('CASE_STUDY_COMPLETE',flush=True)
