import bpy,json,math
from pathlib import Path
root=Path(r'C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/ProcessedModular/Hats')
out=[]
for d in root.iterdir():
 if not d.is_dir(): continue
 bpy.ops.wm.open_mainfile(filepath=str(d/(d.name+'.blend')))
 pts=[o.matrix_world@v.co for o in bpy.context.scene.objects if o.type=='MESH' for v in o.data.vertices]
 h=max(p.z for p in pts); s=sorted(p.z for p in pts)
 front=sorted(p.z for p in pts if abs(p.x)<.22 and p.y<-.02)
 central=sorted(p.z for p in pts if abs(p.x)<.18 and abs(p.y)<.18)
 out.append(dict(id=d.name,max=h,all=[s[int((len(s)-1)*q)] for q in [.05,.1,.15,.2,.3,.5,.7,.9]],front=[front[int((len(front)-1)*q)] for q in [0,.1,.5,.9]],central=[central[int((len(central)-1)*q)] for q in [0,.1,.5,.9]] if central else []))
Path(r'C:/Users/Derek/Games_develop/Cat/Docs/Design/RadioArchipelago/AccessoryTailoring20261104/hat-source-rim.json').write_text(json.dumps(out,indent=2))

