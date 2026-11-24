from pathlib import Path
import bpy,json,numpy as np
P=Path(r'C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/Tools/Modular20261105/StraplessCases')
rows=[]
for d in sorted((P/'Before').iterdir()):
 bpy.ops.wm.open_mainfile(filepath=str(d/'editable.blend'),load_ui=False)
 o=bpy.data.objects['Case'];v=np.array([list(x.co) for x in o.data.vertices]);np.save(P/'Review'/(d.name+'-source.npy'),v)
 m=json.loads((d/'Meta.json').read_text());m['rows']=[]
 for z in np.arange(0,m['caseBoundsMax'][2]+.2,.05):
  a=v[(v[:,2]>=z)&(v[:,2]<z+.05)]
  if len(a):m['rows'].append({'z':round(float(z),3),'v':len(a),'ymin':float(a[:,1].min()),'ymax':float(a[:,1].max()),'xmin':float(a[:,0].min()),'xmax':float(a[:,0].max())})
 rows.append(m)
(P/'probe.json').write_text(json.dumps(rows,indent=2))
