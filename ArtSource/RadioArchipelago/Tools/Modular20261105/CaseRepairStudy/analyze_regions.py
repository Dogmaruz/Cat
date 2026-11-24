from pathlib import Path
import numpy as np,json
p=Path(__file__).parent
v=np.load(p/'case-face-centers.npy');c=np.load(p/'case-face-colors.npy')
brown=(c[:,0]>c[:,1]*1.05)&(c[:,0]>c[:,2]*1.25)
rows=[]
for side in (-1,1):
 for region,mask in [('upper-core',(np.abs(v[:,0])>.20)&(np.abs(v[:,0])<.38)&(v[:,2]>.57)),
                     ('upper-outer',(np.abs(v[:,0])>=.38)&(v[:,2]>.60)),
                     ('lower-core',(np.abs(v[:,0])>.20)&(np.abs(v[:,0])<.38)&(v[:,2]<.25)),
                     ('lower-outer',(np.abs(v[:,0])>=.38)&(v[:,2]<.25))]:
  a=v[mask&brown&(v[:,0]*side>0)&(v[:,1]<.08)]
  rows.append({'side':side,'region':region,'faces':len(a),'lo':a.min(0).tolist() if len(a) else None,'hi':a.max(0).tolist() if len(a) else None})
(p/'local-regions.json').write_text(json.dumps(rows,indent=2))
print(json.dumps(rows,indent=2))
