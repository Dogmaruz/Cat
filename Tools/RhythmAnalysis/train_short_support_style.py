"""Reproduce the opt-in experimental short-support style from six calibration recordings.

Only this offline training program reads reference labels. The source evidence
producer and Unity classifier never read a reference chart or recording ID.
Writes only inside the project's ignored Library folder; does not publish levels.
"""
import argparse,hashlib,importlib.util,pathlib,json,sys,collections,math
PROJECT=pathlib.Path(__file__).resolve().parents[2]/'DancingCatsClone'
if '--deps' in sys.argv:sys.path.insert(0,sys.argv[sys.argv.index('--deps')+1])
else:sys.path.insert(0,str(PROJECT/'Library/OriginalChartCalibration/v4/ml-deps'))
import numpy as np
IDS=['UnstoppableCat054','TwinkleLittleStar','MoonlightPop','LABUBUSongsPop','DisconnectPop','CanCanPiano']

INPUTS={'UnstoppableCat054': {'sourceFileSha256': '7fdf15c8df324f3dff96d1992248081611f0751a2ad454013f719d15bcf91d6b', 'referenceMapSha256': '277e5fd678a41323273dd786f32450291338d4edf4752d9ff83939b52548de90', 'beatPeriod': 0.519999999999996}, 'TwinkleLittleStar': {'sourceFileSha256': '0cd8dc69f01e117562bf8d21afa17225c5594e7e1ba0920444275953fddec44f', 'referenceMapSha256': '96657140ead6377636747c6619bd3e060c60bfe41f32276c4436dbf6b15754db', 'beatPeriod': 0.490000000000002}, 'MoonlightPop': {'sourceFileSha256': '35cb4620f0b6436f8c918538ecc6e3e81ef6180a2fb4ba4fd39931156909b843', 'referenceMapSha256': 'ae158d469109c32c621cbe915062e3ff5129abe1c1001343f557f8ba0e7b3804', 'beatPeriod': 0.5800000000000001}, 'LABUBUSongsPop': {'sourceFileSha256': 'dadc0644e3f1b2fe9d9f10f19b0df81eff5dcf087cc382d9a44e74a2a6a57190', 'referenceMapSha256': '1e9b3f9363637f19b2bcaa45fe361a6ab13ac8c4b9c3ad45d50a6d1d58ef108f', 'beatPeriod': 0.6099999999999994}, 'DisconnectPop': {'sourceFileSha256': '50e457b74a6c5189494d99db4c3b8cac6480c937e43cca03a270d71a39272c5f', 'referenceMapSha256': '2fdf744d7ad60b2df044a803cd5ef3877f6615f4b2c2938864864822f9f11b2a', 'beatPeriod': 0.46999999999999886}, 'CanCanPiano': {'sourceFileSha256': '8f53952f9f5002ed5f6b313ab170bb317e1494605d3cad8b183a0e78353ddcd3', 'referenceMapSha256': '2e04a04cc56346f6a4c6e5f832df5a55eb1abd52b16d1d1f61aee5120a6ee081', 'beatPeriod': 0.3699999999999975}}

def endpoint(e,events):
    f=e['features'];duration=f[10]
    if not .18<=duration<=.45 and .18<=f[5]<=.45:duration=f[5]
    if not .18<=duration<=.45:return None
    end=e['time']+duration
    physical=sorted(set(n['time']for n in events if (n['features'][21]>.5 or n['features'][0]>=.3)and e['time']+.18<=n['time']<=e['time']+.45))
    if physical:
        near=min(physical,key=lambda t:abs(t-end))
        if abs(near-end)<=.10:end=near
    return end

def endpoint_candidates(e,events,beat):
    actual=[e['features'][10],e['features'][5]]
    durations=[d for d in actual if .18<=d<=.45]
    durations += [beat*subdivision for subdivision in [.25,.5,.75,1,1.5]if .18<=beat*subdivision<=.45 and min(abs(beat*subdivision-d)for d in actual)<=.12]
    end=endpoint(e,events)
    if end is not None:durations.append(end-e['time'])
    return sorted(set(round(e['time']+d,8)for d in durations))

def dataset():
    rows=[];truth={};features=None
    for song in IDS:
        evidence=json.loads((PROJECT/'Assets/Clone/Editor/InstrumentEvidence'/(INPUTS[song]['sourceFileSha256']+'.notes.json')).read_text());events=evidence['events'];features=evidence['featureNames']+['selectedBeatDuration','durationToLocalGap']
        reference_path=PROJECT/'Assets/Resources/OriginalData/Maps'/(song+'.timeline.json')
        if hashlib.sha256(reference_path.read_bytes()).hexdigest()!=INPUTS[song]['referenceMapSha256']:
            raise ValueError('Calibration reference changed: '+song)
        reference=json.loads(reference_path.read_text())
        voicepath=PROJECT/'Assets/Clone/Editor/AudioEvidence'/(evidence['sourceFileSha256']+'.voice.json')
        voice=json.loads(voicepath.read_text());vt=np.array(voice['pitchTimes']);vq=np.array(voice['pitchConfidence']);vp=np.array(voice['pitch']);vr=np.array(voice['rms']);rt=np.arange(len(vr))*.01
        rmsScale=max(.001,float(np.quantile(vr,.95)))
        beat=INPUTS[song]['beatPeriod']
        features+=['voiceBody','voiceTail','voicePeak','voiceBodyTail','voiceConfidence','voicePitchStability','vocalAudibility','durationToBeat','previousGapToBeat','nextGapToBeat','voicePitchAgreement','voicePitchDifference']
        holds=[s for s in reference['segments']if s['kind']=='Hold'and s['duration']<=.45]
        truth[song]=holds
        expanded=[(event,end)for event in events for end in endpoint_candidates(event,events,beat)]
        for event,end in expanded:
            near=min(holds,key=lambda h:abs(h['audioTime']-event['time']))if holds else None
            overlap=max(0,min(end,near['endAudioTime'])-max(event['time'],near['audioTime']))if near else 0
            union=max(end,near['endAudioTime'])-min(event['time'],near['audioTime'])if near else 1
            positive=near is not None and abs(event['time']-near['audioTime'])<=.08 and abs(end-near['endAudioTime'])<=.08 and overlap/union>=.5
            a=event['time']
            mean=lambda arr,t,lo,hi:float(arr[(t>=a+lo)&(t<a+hi)].mean())if np.any((t>=a+lo)&(t<a+hi))else 0.
            body=mean(vr,rt,.04,.22);tail=mean(vr,rt,.25,.42);segment=vr[(rt>=a)&(rt<a+.3)];peak=float(segment.max())if len(segment)else 0.
            pis=vp[(vt>=a+.04)&(vt<a+.22)];mode=collections.Counter(pis).most_common(1)[0][0]if len(pis)else 0
            stability=float(np.mean(abs(pis-mode)<=1))if len(pis)else 0
            pitch_q=vq[(vt>=a+.04)&(vt<a+.22)]
            distances=np.minimum(abs(pis%12-event['midi']%12),12-abs(pis%12-event['midi']%12))
            agreement=float(np.mean((distances<=1)*pitch_q))if len(pis)else 0
            difference=float(np.average(distances,weights=pitch_q+.001))if len(pis)else 6
            measured=list(event['features'])
            for column in [5,10,12,13,14]:measured[column]/=beat
            features_row=measured+[(end-a)/beat,(end-a)/max(.06,event['features'][14]),
                body/rmsScale,tail/rmsScale,peak/rmsScale,min(8,body/max(.0001,tail)),mean(vq,vt,.04,.22),stability,
                float(body>=.0005),(end-a)/beat,event['features'][13]/beat,event['features'][12]/beat,agreement,difference]
            rows.append(dict(song=song,time=event['time'],end=end,midi=event['midi'],features=features_row,y=int(positive)))
    return rows,truth,features

def tree_fit(x,y,depth=5,min_leaf=8,weight=1,songs=None):
    songs=np.array(songs if songs is not None else ['all']*len(y))
    weights=np.array([1./np.sum(songs==song)for song in songs]);weights*=np.where(y>0,weight,1)
    def gini(count,total):
        return 2*count*(total-count)/max(1e-20,total*total)
    def fit(indices,level):
        count=int(y[indices].sum());n=len(indices);groups=collections.Counter(str(songs[i])for i in indices if y[i]);groupCount=sum(v>=2 for v in groups.values())
        node=dict(count=n,positive=count,probability=count/n,positiveGroups=groupCount)
        if level==depth or n<2*min_leaf or count in[0,n]:return node
        totalWeight=weights[indices].sum();positiveWeight=(weights[indices]*y[indices]).sum()
        prior=gini(positiveWeight,totalWeight);bestgain=0;best=None
        for j in range(x.shape[1]):
            order=indices[np.argsort(x[indices,j],kind='stable')];values=x[order,j];ys=y[order];cum=(ys*weights[order]).cumsum();cw=weights[order].cumsum()
            candidates=np.flatnonzero(values[:-1]<values[1:])+1
            candidates=candidates[(candidates>=min_leaf)&(candidates<=n-min_leaf)]
            if len(candidates)>100:candidates=candidates[np.linspace(0,len(candidates)-1,100).astype(int)]
            for cut in candidates:
                left=cum[cut-1];right=positiveWeight-left;leftWeight=cw[cut-1];rightWeight=totalWeight-leftWeight
                loss=(leftWeight*gini(left,leftWeight)+rightWeight*gini(right,rightWeight))/totalWeight;gain=prior-loss
                if gain>bestgain+1e-12:bestgain=gain;best=(j,float((values[cut-1]+values[cut])/2),order[:cut],order[cut:])
        if best is None:return node
        j,threshold,left,right=best;node.update(feature=j,threshold=threshold,left=fit(left,level+1),right=fit(right,level+1));return node
    return fit(np.arange(len(y)),0)

def predict(tree,row):
    while'feature'in tree:tree=tree['left']if row[tree['feature']]<=tree['threshold']else tree['right']
    return tree['probability']if tree.get('positiveGroups',0)>=2 else 0.

def proposals(tree,rows,threshold=.65):
    scored=[dict(r,probability=predict(tree,r['features']))for r in rows]
    scored=sorted([r for r in scored if r['probability']>=threshold],key=lambda r:(-r['probability'],-r['features'][0],r['time']))
    accepted=[]
    for r in scored:
        if not any(r['song']==p['song']and min(r['end'],p['end'])-max(r['time'],p['time'])>-.078 for p in accepted):accepted.append(r)
    return sorted(accepted,key=lambda r:(r['song'],r['time']))

def evaluate(got,truth):
    result={}
    for song,holds in truth.items():
        proposals=[r for r in got if r['song']==song];used=set();matches=[]
        for r in proposals:
            options=[(abs(r['time']-h['audioTime']),i,h)for i,h in enumerate(holds)if i not in used and abs(r['time']-h['audioTime'])<=.08 and abs(r['end']-h['endAudioTime'])<=.08 and max(0,min(r['end'],h['endAudioTime'])-max(r['time'],h['audioTime']))/(max(r['end'],h['endAudioTime'])-min(r['time'],h['audioTime']))>=.5]
            if options:
                _,i,h=min(options);used.add(i);matches.append(dict(time=r['time'],end=r['end'],refStart=h['audioTime'],refEnd=h['endAudioTime']))
        tp=len(matches);n=len(proposals);total=len(holds)
        result[song]=dict(reference=total,proposed=n,tp=tp,fp=n-tp,fn=total-tp,f1=2*tp/max(1,n+total),matches=matches)
    return result


def provenance():
    here=pathlib.Path(__file__).resolve().parent
    spec=importlib.util.spec_from_file_location('instrument',here/'analyze_instrumental_notes.py')
    instrument=importlib.util.module_from_spec(spec);spec.loader.exec_module(instrument)
    spec=importlib.util.spec_from_file_location('voice',here/'analyze_vocal_holds.py')
    voice_module=importlib.util.module_from_spec(spec);spec.loader.exec_module(voice_module)
    catalog=json.loads((PROJECT/'Assets/Resources/OriginalData/songs.json').read_text());result=[]
    for song in IDS:
        record=next(r for r in catalog if r['id']==song);entry=INPUTS[song]
        base=PROJECT/'Assets/Resources'/record['audioResource']
        audio=next(p for p in [base.with_suffix(ext)for ext in ['.wav','.ogg','.mp3']]if p.exists())
        if hashlib.sha256(audio.read_bytes()).hexdigest()!=entry['sourceFileSha256']:raise ValueError('Calibration source audio changed: '+song)
        notes_path=PROJECT/'Assets/Clone/Editor/InstrumentEvidence'/(entry['sourceFileSha256']+'.notes.json')
        voice_path=PROJECT/'Assets/Clone/Editor/AudioEvidence'/(entry['sourceFileSha256']+'.voice.json')
        notes=json.loads(notes_path.read_text());vocal=json.loads(voice_path.read_text())
        if notes['featureSha256']!=instrument.evidence_digest(notes) or vocal['featureSha256']!=voice_module.evidence_digest(vocal):
            raise ValueError('Calibration evidence integrity mismatch: '+song)
        if notes['sourceFileSha256']!=entry['sourceFileSha256'] or notes['sourcePcmSha256']!=vocal['sourcePcmSha256']:
            raise ValueError('Calibration PCM/file pairing mismatch: '+song)
        result.append(dict(id=song,**entry,sourcePcmSha256=notes['sourcePcmSha256'],instrumentFeatureSha256=notes['featureSha256'],voiceFeatureSha256=vocal['featureSha256']))
    return result

def positive_rules(tree,names,path=()):
    if 'feature' in tree:
        f=tree['feature'];threshold=tree['threshold']
        return positive_rules(tree['left'],names,path+((f,names[f],'<=',threshold),))+positive_rules(tree['right'],names,path+((f,names[f],'>',threshold),))
    if tree['probability']<.9 or tree.get('positiveGroups',0)<2:return []
    return [dict(conditions=path,trainingRows=tree['count'],positiveRows=tree['positive'],positiveGroups=tree['positiveGroups'],trainingPrecision=tree['probability'])]

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--deps',type=pathlib.Path)
    parser.add_argument('--output',type=pathlib.Path,default=PROJECT/'Library/ChartGenerationV9/training-proof')
    parser.add_argument('--leave-one-song-out',action='store_true');args=parser.parse_args()
    output=args.output.resolve();library=(PROJECT/'Library').resolve()
    if library not in output.parents:raise ValueError('Training proof output must remain inside project Library')
    inputs=provenance();rows,truth,names=dataset()
    x=np.array([r['features']for r in rows]);y=np.array([r['y']for r in rows])
    model=tree_fit(x,y,6,8,2,[r['song']for r in rows]);report=evaluate(proposals(model,rows,.9),truth)
    recipe=dict(version='short-support-experimental-v1',experimental=True,defaultEnabled=False,
        algorithm='weighted CART; source-only inference features',maxDepth=6,minLeaf=8,positiveWeight=2,
        rowWeight='inverse candidate row count of each calibration recording',acceptancePrecision=.9,
        positiveLeafSupport='at least two positive examples in at least two recordings',
        metric='Hold duration <=.45s; start/end <=80ms; interval IoU >=.5; one-to-one matching',
        measuredReleasePolicy='NN/harmonic release; nearby source attack; pulse fractions .25,.5,.75,1,1.5 only within120ms of measured release',
        temporalFeatureIndicesNormalizedByBeat=[5,10,12,13,14],featureNames=names,trainingInputs=inputs,
        producerSourceSha256=hashlib.sha256(pathlib.Path(__file__).with_name('analyze_instrumental_notes.py').read_bytes().replace(b'\r\n',b'\n')).hexdigest(),
        trainerSourceSha256=hashlib.sha256(pathlib.Path(__file__).read_bytes().replace(b'\r\n',b'\n')).hexdigest(),
        numpyVersion=np.__version__,candidateRows=len(rows),positiveCandidateRows=int(y.sum()),positiveRules=positive_rules(model,names),
        calibration=report,tree=model)
    if args.leave_one_song_out:
        loo={}
        for song in IDS:
            ix=np.array([r['song']!=song for r in rows]);training=[r for r in rows if r['song']!=song]
            fitted=tree_fit(x[ix],y[ix],6,8,2,[r['song']for r in training])
            loo[song]=evaluate(proposals(fitted,[r for r in rows if r['song']==song],.9),{song:truth[song]})[song]
            print('LOO',song,loo[song]['tp'],loo[song]['fp'],flush=True)
        recipe['leaveOneSongOut']=loo
    output.mkdir(parents=True,exist_ok=True)
    (output/'short-support-style-v1.json').write_text(json.dumps(recipe,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(output/'short-support-style-v1.json',flush=True)
if __name__=='__main__':main()
