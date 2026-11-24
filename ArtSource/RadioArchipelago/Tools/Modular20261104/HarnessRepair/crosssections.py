import numpy as n
from pathlib import Path
P=Path('Docs/Design/RadioArchipelago/ModularCast20261104/HarnessRepair')
for k in ['Tiko','Rumi','Miro','Fira','Nori','Soli_Helio']:
 a=n.load(P/(k+'_Harness.L.npy'));w,vs=n.linalg.eigh(n.cov(a[:,:2].T));h=vs[:,-1];h*=1 if h[1]<0 else -1
 q=n.stack([-h[1],h[0]]);u=a[:,:2]@h;v=a[:,2];b=a[:,:2]@q
 U=(u.max()+u.min())/2;V=(v.max()+v.min())/2;ru=(u.max()-u.min())/2;rv=(v.max()-v.min())/2
 t=n.arctan2((u-U)/ru,(v-V)/rv);rr=n.sqrt(((u-U)/ru)**2+((v-V)/rv)**2)
 vals=[]
 for i in range(72):
  mask=abs(n.arctan2(n.sin(t-i*n.pi/36),n.cos(t-i*n.pi/36)))<n.pi/36
  if mask.sum()<5:continue
  o=n.array([n.sin(i*n.pi/36)/ru,n.cos(i*n.pi/36)/rv]);o/=n.linalg.norm(o)
  rad=u[mask]*o[0]+v[mask]*o[1]
  vals.append([n.quantile(rad,.9)-n.quantile(rad,.1),n.quantile(b[mask],.9)-n.quantile(b[mask],.1)])
 print(k,'radial/planeNormal',n.median(vals,axis=0))
