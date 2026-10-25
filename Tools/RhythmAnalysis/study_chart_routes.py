"""Read-only recovered-map spatial study. Never writes Assets or changes timing."""
import collections, hashlib, itertools, json, math, pathlib, statistics
ROOT=pathlib.Path(__file__).resolve().parents[2]
OUT=ROOT/'DancingCatsClone/Library/ChartChoreographyV1/Reference'
OUT.mkdir(parents=True,exist_ok=True)
MAPS=ROOT/'DancingCatsClone/Assets/Resources/OriginalData/Maps'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def quantiles(vals):
    vals=sorted(vals)
    return {str(q):round(vals[round((len(vals)-1)*q)],5) for q in [0,.25,.5,.75,.9,.95,1]} if vals else {}
def histogram(vals,decimals=2):
    return dict(sorted(collections.Counter(str(round(v,decimals))for v in vals).items(),key=lambda p:float(p[0])))
def impact_x(segment):
    x=segment['x']
    if segment.get('motion',{}).get('updateType')==3: return math.sin(2*math.pi*x/5)
    return x
def exit_x(segment):
    path=segment.get('path',[])
    return path[-1]['x'] if segment['kind']=='Follow' and path else impact_x(segment)
def summary(path):
    m=json.loads(path.read_text(encoding='utf8'))
    ss=[s for s in m['segments']if s['kind']in['Jump','BigJump','Hold','Follow']]
    segs=sorted(ss,key=lambda s:s['audioTime'])
    ts=[];rows=[]
    bins=collections.defaultdict(list)
    for a,b in zip(segs,segs[1:]):
        dt=b['audioTime']-a['endAudioTime'];dx=impact_x(b)-exit_x(a)
        name='<.18' if dt<.18 else '.18-.30' if dt<.3 else '.30-.50' if dt<.5 else '.50-.80' if dt<.8 else '>=.80'
        bins[name].append((dt,dx))
        ts.append(dx)
        rows.append({'previous':a['sourceIndex'],'next':b['sourceIndex'],'interval':dt,'dx':dx,
                     'from':exit_x(a),'to':impact_x(b),'previousKind':a['kind'],'nextKind':b['kind']})
    moving=[]
    for e in m['elements']:
        if e['updateType']:
            moving.append({k:e.get(k)for k in ['sourceIndex','kind','audioTime','pos','updateType','msLeft','begin','end','mstartPos','mfendPos']})
    directions=[0 if abs(dx)<.08 else 1 if dx>0 else -1 for dx in ts]
    moving_groups=[list(group) for moving,group in itertools.groupby(segs,key=lambda s:s.get('motion',{}).get('updateType')==3)if moving]
    side=[0 if abs(impact_x(s))<.12 else 1 if impact_x(s)>0 else -1 for s in segs]
    groups=[(key,len(list(vals)))for key,vals in itertools.groupby(side)]
    ret={'id':m['id'],'sourcePath':str(path.relative_to(ROOT)).replace('\\','/'),'sourceSha256':sha(path),
         'speed':m['forwardSpeed'],'preroll':m['delayTime'],'playableSupports':len(segs),
         'supportKinds':dict(collections.Counter(s['kind']for s in segs)),
         'elementUpdateTypes':dict(collections.Counter(str(e['updateType'])for e in m['elements'])),
         'motionByKind':dict(collections.Counter(s['kind']+':'+str(s.get('motion',{}).get('updateType',0))for s in segs)),
         'rawXHistogram':histogram([s['x']for s in segs]),
         'landingXHistogram':histogram([impact_x(s)for s in segs]),
         'laneAbsDeltaQuantiles':quantiles([abs(v)for v in ts]),
         'lateralSpeedQuantiles':quantiles([abs(r['dx'])/r['interval']for r in rows if r['interval']>.001]),
         'sameLaneTransitions':sum(d==0 for d in directions),
         'crossCentreTransitions':sum(r['from']*r['to']<-.04 for r in rows),
         'directionReversals':sum(a*b<0 for a,b in zip(directions,directions[1:])),
         'nonzeroDirectionTransitions':sum(a!=0 and b!=0 for a,b in zip(directions,directions[1:])),
         'sameSideRuns':dict(collections.Counter(str(size)for _,size in groups)),
         'rhythmBins':{k:{'count':len(vals),'medianAbsDx':round(statistics.median(abs(v[1])for v in vals),4),
                         'sameLane':sum(abs(dx)<.08 for dt,dx in vals),
                         'dxQuantiles':quantiles([abs(dx)for dt,dx in vals])}for k,vals in bins.items()},
         'movingRuns':[len(group)for group in moving_groups],
         'movingGroupUniformFlags':sum(len(set(s['motion']['msLeft']for s in group))==1 for group in moving_groups),
         'movingGroupMixedFlags':sum(len(set(s['motion']['msLeft']for s in group))>1 for group in moving_groups),
         'movingElements':moving,'transitions':rows}
    return ret

OUT.mkdir(parents=True,exist_ok=True)
reports=[summary(p)for p in sorted(MAPS.glob('*.json'))]
focus=['UnstoppableCat054','UnityPop054','UnityPop','LABUBUSongsPop','TwinkleLittleStar','MoonlightPop']
all_motion=collections.Counter();all_kind_motion=collections.Counter()
for r in reports:all_motion.update(r['elementUpdateTypes']);all_kind_motion.update(r['motionByKind'])
all_transitions=[t for r in reports for t in r['transitions']]
moving_elements=[e for r in reports for e in r['movingElements']if e['updateType']==3]
moving_runs=[n for r in reports for n in r['movingRuns']]
aggregate={'transitions':len(all_transitions),
           'sameLaneTransitions':sum(abs(t['dx'])<.08 for t in all_transitions),
           'crossCentreTransitions':sum(t['from']*t['to']<-.04 for t in all_transitions),
           'absoluteDeltaQuantiles':quantiles([abs(t['dx'])for t in all_transitions]),
           'mapsWithTrueMovingSupports':sum(any(r['motionByKind'].get(k,0)>0 for k in ['Jump:3','BigJump:3'])for r in reports),
           'movingRuns':{'count':len(moving_runs),'lengthHistogram':dict(collections.Counter(str(n)for n in moving_runs)), 'quantiles':quantiles(moving_runs),
                         'uniformFlags':sum(r['movingGroupUniformFlags']for r in reports),'mixedFlags':sum(r['movingGroupMixedFlags']for r in reports)},
           'movingLeftFlag':dict(collections.Counter(str(e['msLeft'])for e in moving_elements)),
           'movingRawX':histogram([e['pos']['x']for e in moving_elements]),
           'allMotionKind':dict(collections.Counter(e['kind']+':'+str(e['updateType'])for r in reports for e in r['movingElements'])),
           'rhythmBins':{}}
for name,lo,hi in [('<.18',0,.18),('.18-.30',.18,.30),('.30-.50',.30,.5),('.50-.80',.5,.8),('>=.80',.8,999)]:
    ts=[t for t in all_transitions if lo<=t['interval']<hi]
    aggregate['rhythmBins'][name]={'count':len(ts),'sameLane':sum(abs(t['dx'])<.08 for t in ts),
                                'deltaHistogram':histogram([abs(t['dx'])for t in ts]),
                                'absoluteDeltaQuantiles':quantiles([abs(t['dx'])for t in ts])}
out={'sourceMapCount':len(reports),'originalHashes':{r['sourcePath']:r['sourceSha256']for r in reports},
     'allElementUpdateTypes':dict(all_motion),'allSupportMotionByKind':dict(all_kind_motion),'aggregate':aggregate,
     'maps':[r if r['id']in focus else{k:v for k,v in r.items()if k not in['movingElements','transitions']}for r in reports]}
(OUT/'study.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
assert all(sha(ROOT/p)==h for p,h in out['originalHashes'].items())
print('Maps',len(reports),'motion',dict(all_motion),'kinds',dict(all_kind_motion))
print('Aggregate',json.dumps(aggregate,ensure_ascii=False))
for r in reports:
    if r['id'] in focus:
        print(r['id'],r['playableSupports'],r['rawXHistogram'],'motion',r['motionByKind'],
              'sameLane',r['sameLaneTransitions'],'cross',r['crossCentreTransitions'],
              'reversal',r['directionReversals'],'nonzero',r['nonzeroDirectionTransitions'],r['rhythmBins'])
print(OUT/'study.json')
