import bpy,numpy as np,json,pathlib
for name in ['Rumi','Vela']:
 f=pathlib.Path('C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/ProcessedModular/Characters')/name
 bpy.ops.wm.open_mainfile(filepath=str(f/(name+'_Rigged.blend')));o=next(o for o in bpy.data.objects if o.type=='MESH');p=np.array([v.co[:] for v in o.data.vertices]);im=bpy.data.images.load(str(f/'BaseColor.png'));w,h=im.size;a=np.empty(w*h*4,np.float32);im.pixels.foreach_get(a);a=a.reshape((h,w,4)); rows=[]
 for l in o.data.loops:
  x,y,z=p[l.vertex_index]
  if abs(x)<.42 and 1.05<z<1.77 and y<-.315:
   u,v=o.data.uv_layers.active.data[l.index].uv;c=a[min(h-1,max(0,int(v*h))),min(w-1,max(0,int(u*w))),:3]
   rows.append([*p[l.vertex_index],*c])
 r=np.array(rows); inds=np.argsort(r[:,3:6].max(axis=1));print(name, 'darkest',json.dumps(r[inds[:30]].tolist()),flush=True)
