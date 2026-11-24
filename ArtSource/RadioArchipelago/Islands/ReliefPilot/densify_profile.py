import sys,pathlib,json
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_relief as r
stations=sorted(set([round(float(s),6) for s in r.np.arange(0,r.road.length,.5)]+[round(float(s),6) for start,end in ((80.5,83),(98,100.5)) for s in r.np.arange(start,end+.01,.1)]))
keys=[dict(distance=s,height=r.road_h(s)) for s in stations];keys.append(dict(distance=r.road.length,height=keys[0]['height']))
(r.OUT/'ground_elevation.json').write_text(json.dumps(dict(length=r.road.length,closed=True,keys=keys),indent=2))
print('PROFILE_DENSIFIED',len(keys),flush=True)
