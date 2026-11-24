from pathlib import Path
import bpy,numpy as np,json
p=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(p.parent/'StraplessCases/Before/Miro_Garden/editable.blend'),load_ui=False)
o=bpy.data.objects['Case'];m=o.data.materials[0]
node=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');tex=node.inputs['Base Color'].links[0].from_node
image=tex.image;px=np.empty(image.size[0]*image.size[1]*4,dtype=np.float32);image.pixels.foreach_get(px);px=px.reshape(image.size[1],image.size[0],4)
out={'uvlayers':[(l.name,l.active_render) for l in o.data.uv_layers],'activeUV':o.data.uv_layers.active.name,'image':image.filepath,'size':image.size[:],'extension':tex.extension,'vectorLinks':[(l.from_node.type,l.from_socket.name) for l in tex.inputs['Vector'].links]}
uv=np.asarray([list(l.uv) for l in o.data.uv_layers.active.data]);out['uvlo']=uv.min(0).tolist();out['uvhi']=uv.max(0).tolist()
for mode in ('clamp','wrap'):
 coords=uv if mode=='clamp' else uv%1
 xy=np.clip((coords*np.asarray(image.size[:])).astype(int),0,np.array(image.size[:])-1);rgb=px[xy[:,1],xy[:,0],:3]
 idx=int(np.argmin(((rgb-np.array((.34,.53,.38)))**2).sum(1)))
 out[mode]={'uv':uv[idx].tolist(),'coord':coords[idx].tolist(),'rgb':rgb[idx].tolist()}
(p/'uv-inspection.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
