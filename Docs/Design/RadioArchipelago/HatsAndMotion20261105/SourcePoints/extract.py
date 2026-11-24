from pathlib import Path
import bpy,numpy as np,json
P=Path(r'C:/Users/Derek/Games_develop/Cat');S=P/'ArtSource/RadioArchipelago/ProcessedModular';O=P/'Docs/Design/RadioArchipelago/HatsAndMotion20261105/SourcePoints'
for cat in ['Characters','Hats']:
 for d in sorted((S/cat).iterdir()):
  if not d.is_dir():continue
  files=list(d.glob('*.blend'))
  if not files:continue
  bpy.ops.wm.open_mainfile(filepath=str(files[0]),load_ui=False)
  pts=[]
  for ob in bpy.context.scene.objects:
   if ob.type=='MESH':
    for v in ob.data.vertices:
     p=ob.matrix_world@v.co;pts.append((p.x,p.z,-p.y))
  a=np.asarray(pts);np.save(O/(cat+'_'+d.name+'.npy'),a)
  print(cat,d.name,len(a),a.min(0).tolist(),a.max(0).tolist(),flush=True)
