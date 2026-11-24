from pathlib import Path
import numpy as n
P=Path('Docs/Design/RadioArchipelago/ModularCast20261104/HarnessRepair')
for f in sorted(P.glob('*_Harness.L.npy')):
 a=n.load(f);# BlenderZup toUnityYup
 a=a[:,[0,2,1]];a[:,2]*=-1
 xy=a[:,[0,2]];w,v=n.linalg.eigh(n.cov(xy.T));h=n.array([v[0,-1],0,v[1,-1]]);h*=1 if h[2]>=0 else -1
 u=a@h;y=a[:,1];center=n.array([(u.min()+u.max())*.5,(y.min()+y.max())*.5]);rad=n.array([(u.max()-u.min())*.5,(y.max()-y.min())*.5]);t=n.arctan2((u-center[0])/rad[0],(y-center[1])/rad[1])%(2*n.pi)
 c=[]
 for i in range(72):
  delta=n.abs(n.arctan2(n.sin(t-i*n.pi/36),n.cos(t-i*n.pi/36)));near=a[delta<n.radians(7.5)]
  if len(near)<4:near=a[n.argsort(delta)[:12]]
  c.append(n.median(near,axis=0))
 c=n.array(c)
 for _ in range(4):c=(n.roll(c,1,0)+c*2+n.roll(c,-1,0))*.25
 broad=[];normal=n.cross(n.array([0,1,0]),h)
 for i in range(72):
  tangent=c[(i+1)%72]-c[(i-1)%72];tangent/=max(n.linalg.norm(tangent),1e-8)
  radial=h*n.sin(i*n.pi/36)+n.array([0,1,0])*n.cos(i*n.pi/36);radial-=tangent*n.dot(radial,tangent);radial/=max(n.linalg.norm(radial),1e-8);nn=n.cross(tangent,radial);nn/=max(n.linalg.norm(nn),1e-8)
  mask=n.abs(n.arctan2(n.sin(t-i*n.pi/36),n.cos(t-i*n.pi/36)))<n.radians(10);d=a[mask]-c[i];x=d@radial;yy=d@nn;ang=.5*n.arctan2(2*n.sum(x*yy),n.sum(x*x)-n.sum(yy*yy))
  b=radial*n.cos(ang)+nn*n.sin(ang)
  if i and n.dot(b,broad[-1])<0:b=-b
  broad.append(b)
 dot=n.dot(broad[0],broad[-1])
 if dot<.8:print(f.stem,'closure',dot)
