import sys, pathlib, json, bpy, numpy as np
from mathutils import Vector
sys.path.insert(0,str(pathlib.Path(__file__).parent))
import pack_inspect as p
all=[]
for name in sorted(x.name for x in p.IN.iterdir() if x.is_dir()):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 d=p.extract(name);bpy.ops.import_scene.fbx(filepath=str(next(d.glob('*.fbx'))),use_image_search=False,use_anim=False)
 ob=next(o for o in bpy.context.scene.objects if o.type=='MESH')
 bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 v=np.array([list(v.co) for v in ob.data.vertices]);lo=v.min(axis=0);hi=v.max(axis=0);v=(v-np.array([(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]]))*2.4/(hi[2]-lo[2])
 np.save(p.QA/(name+'-vertices.npy'),v)
 rows=[]
 for z in np.arange(.8,2.4,.05):
  pts=v[(v[:,2]>z)&(v[:,2]<z+.05)]
  if len(pts):rows.append({'z':round(float(z),2),'count':len(pts),'min':pts.min(axis=0).tolist(),'max':pts.max(axis=0).tolist()})
 all.append({'id':name,'rows':rows})
(p.QA/'slices.json').write_text(json.dumps(all,indent=2))
