"""Independent smooth S-loop design, source XY plane, metres."""
import numpy as np, math, json
CONTROLS=[(0,-80),(40,-80),(80,-80),(113,-55),(117,-20),(90,8),(50,30),(25,60),(35,90),(12,120),(-30,126),(-70,110),(-82,79),(-70,48),(-83,20),(-130,8),(-168,-15),(-178,-49),(-153,-80),(-120,-80),(-80,-80),(-40,-80)]
def make_route(length=750):
    controls=np.array(CONTROLS,dtype=float);dense=[]
    for i in range(len(controls)):
        p0,p1,p2,p3=[controls[j%len(controls)]for j in(i-1,i,i+1,i+2)]
        for t in np.linspace(0,1,128,endpoint=False):
            dense.append(((1-t)**3*p0+(3*t**3-6*t*t+4)*p1+(-3*t**3+3*t*t+3*t+1)*p2+t**3*p3)/6)
    dense=np.array(dense);d=np.linalg.norm(np.roll(dense,-1,axis=0)-dense,axis=1);scale=length/sum(d);dense*=scale;d*=scale
    arc=np.r_[0,np.cumsum(d)];stations=np.linspace(0,arc[-1],1365,endpoint=False)
    points=np.array([[np.interp(s,arc,np.r_[dense[:,j],dense[0,j]])for j in range(2)]for s in stations]);delta=points[0].copy();points-=delta
    ds=np.linalg.norm(np.roll(points,-1,axis=0)-points,axis=1);arc=np.r_[0,np.cumsum(ds)]
    return points,arc,float(arc[-1])
def metrics(points,arc):
    ab=points-np.roll(points,2,axis=0);bc=np.roll(points,-2,axis=0)-points;ac=ab+bc
    cross=np.abs(ab[:,0]*ac[:,1]-ab[:,1]*ac[:,0]);radii=np.linalg.norm(ab,axis=1)*np.linalg.norm(bc,axis=1)*np.linalg.norm(ac,axis=1)/(2*np.maximum(cross,1e-12))
    d=np.linalg.norm(points[:,None,:]-points[None,:,:],axis=2);s=np.abs(arc[:-1,None]-arc[None,:-1]);mask=np.minimum(s,arc[-1]-s)>40
    return dict(length=arc[-1],min_radius=float(min(radii)),min_nonlocal_distance_40m=float(min(d[mask])),bounds=[points.min(axis=0).tolist(),points.max(axis=0).tolist()],stations=[dict(distance=int(s),x=float(np.interp(s,arc,np.r_[points[:,0],points[0,0]])),y=float(np.interp(s,arc,np.r_[points[:,1],points[0,1]])))for s in range(0,int(arc[-1]),25)])
if __name__=='__main__':
    p,a,l=make_route();print(json.dumps(metrics(p,a),indent=2))
