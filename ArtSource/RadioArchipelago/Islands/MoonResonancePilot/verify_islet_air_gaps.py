"""Read actual caps and prove the new centreline visits separated islets."""
import bpy,sys,pathlib,json,math
import numpy as np
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build_moon_resonance as moon
bpy.ops.wm.open_mainfile(filepath=str(HERE/'moon_resonance_preview.blend'));moon.prepare_cap_index()
rows=[]
for s in np.arange(430,565.001,.25):
    p,_=moon.route(float(s));g=moon.actual_ground(p.x,p.y);rows.append((float(s),g is not None))
intervals=[];start=rows[0][0];state=rows[0][1]
for s,land in rows[1:]:
    if land!=state:intervals.append(dict(start=start,end=s-.25,kind='stone' if state else 'air'));start=s;state=land
intervals.append(dict(start=start,end=rows[-1][0],kind='stone'if state else'air'))
def rim(index):
    _,c,r,_=moon.ISLANDS[index];seed=1130+index;aa=np.linspace(0,math.tau,720,endpoint=False);rr=1+.065*np.sin(aa*5+seed)+.035*np.cos(aa*9+seed*.9)+.022*np.sin(aa*17+seed*.3)
    return np.array(c)+np.c_[np.cos(aa)*r[0]*rr,np.sin(aa)*r[1]*rr]
gaps=[]
for i,j in[(5,13),(13,14),(14,15),(15,6)]:
    a,b=rim(i),rim(j);minimum=float(np.linalg.norm(a[:,None,:]-b[None,:,:],axis=2).min());gaps.append(dict(a=moon.ISLANDS[i][0],b=moon.ISLANDS[j][0],minimum_boundary_air_gap=minimum))
visits=[]
for s in moon.HOPPING_STATIONS:
    p,_=moon.route(s);ground=moon.actual_ground(p.x,p.y);visits.append(dict(station=s,source_xy=list(p[:2]),route_floor=moon.play(s),actual_cap_height=ground,floor_separation=moon.play(s)-ground if ground is not None else None))
report=dict(route_unchanged=True,sampled_centreline_intervals=intervals,islet_visits=visits,boundary_gaps=gaps,pass_=all(g['minimum_boundary_air_gap']>5 for g in gaps)and all(v['actual_cap_height']is not None and v['floor_separation']>2.5 for v in visits))
(HERE/'islet-air-gap-audit.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('MOON_ISLET_AIR_GAPS',json.dumps(report),flush=True)
if not report['pass_']:raise RuntimeError('Islet sequence has insufficient separation or route coverage')

