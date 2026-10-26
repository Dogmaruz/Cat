"""Reproduce the experimental interval-relative short-Hold policy on eight known recordings.

Only this offline trainer reads annotations. It emits proof inside ignored Library;
source measurement and Unity inference receive no recording ID or reference times.
"""
import argparse,collections,hashlib,json,pathlib,sys
ROOT=pathlib.Path(__file__).resolve().parents[2];PROJECT=ROOT/'DancingCatsClone'
if '--deps' in sys.argv:sys.path.insert(0,sys.argv[sys.argv.index('--deps')+1])
else:sys.path.insert(0,str(PROJECT/'Library/OriginalChartCalibration/v4/ml-deps'))
import numpy as np
import train_short_support_style as shared

ADDITIONAL={
 'IJustWannaDancePop':dict(sourceFileSha256='bea4e85a21807ee88b90b745209a8057dedaf9a0fea82671cb0f8056f87ccc40',referenceMapSha256='9283d289245f52a5e0ce3a29c61a46fcd16f84298a96e81ccceaf69fdaa1b376',beatPeriod=.4600000000000009),
 'MoonlightCloudsPiano':dict(sourceFileSha256='9d99efd063bb86889578244439258c1f1692de1f5939e9a8693aa0dfbf9d7ed6',referenceMapSha256='25752621169c96f26c49e211e03373fdf27584eeef912950a642f022e60761ef',beatPeriod=.6799999999999997)}

def match(row,hold):
 a,b=row['time'],row['end'];c,d=hold['audioTime'],hold['endAudioTime']
 if b<=a or d<=c or b-a>.450001 or d-c>.450001:return False
 return abs(a-c)<=.08 and abs(b-d)<=.08 and max(0,min(b,d)-max(a,c))/(max(b,d)-min(a,c))>=.5

def evaluate(got,truth):
 """Maximum-cardinality one-to-one interval matching, as in the Editor study."""
 result={}
 for song,holds in truth.items():
  proposals=[row for row in got if row['song']==song];edges=[[i for i,h in enumerate(holds)if match(row,h)]for row in proposals];owner={}
  def augment(index,seen):
   for target in edges[index]:
    if target in seen:continue
    seen.add(target)
    if target not in owner or augment(owner[target],seen):owner[target]=index;return True
   return False
  for i in range(len(proposals)):augment(i,set())
  matches=[dict(time=proposals[p]['time'],end=proposals[p]['end'],refStart=holds[h]['audioTime'],refEnd=holds[h]['endAudioTime'])for h,p in sorted(owner.items())]
  tp=len(matches);n=len(proposals);total=len(holds)
  result[song]=dict(reference=total,proposed=n,tp=tp,fp=n-tp,fn=total-tp,f1=2*tp/max(1,n+total),matches=matches)
 return result

def setup():
 shared.IDS=shared.IDS[:6]+list(ADDITIONAL);shared.INPUTS.update(ADDITIONAL)

def proposals(tree,rows,threshold=.9):
 return shared.proposals(tree,[row for row in rows if not row.get('reattackInside',False)],threshold)

def release_features(voice,a,b,scale=None):
 """Only the selected note body and first 75ms of release are observed."""
 vr=np.asarray(voice['rms']);vt=np.asarray(voice['pitchTimes']);vq=np.asarray(voice['pitchConfidence']);vp=np.asarray(voice['pitch'])
 scale=max(.001,float(np.quantile(vr,.95)))if scale is None else scale
 def energy(lo,hi):
  first=max(0,int(np.ceil(lo/.01-1e-7)));last=min(len(vr),int(np.ceil(hi/.01-1e-7)));return float(vr[first:last].mean())if last>first else 0.
 body=energy(a+.03,b-.025);tail=energy(b+.015,b+.075);early=energy(a+.01,a+.06);late=energy(b-.07,b-.02);before=energy(a-.09,a-.025)
 mask=(vt>=a+.025)&(vt<b-.025);pitch=vp[mask];q=vq[mask];strong=pitch[q>=.4];mode=collections.Counter(strong).most_common(1)[0][0]if len(strong)else 0
 stability=float(np.mean(abs(strong-mode)<=1))if len(strong)else 0.
 first=max(0,int(np.ceil(a/.01-1e-7)));last=min(len(vr),int(np.ceil(b/.01-1e-7)));peak=vr[first:last]
 return [body/scale,tail/scale,min(8,body/max(.0001,tail)),early/scale,late/max(.0001,early),float(np.mean(q>=.4))if len(q)else 0.,stability,early/max(.0001,before),float(peak.max())/scale if len(peak)else 0.,float(q.mean())if len(q)else 0.]

def dataset():
 setup();rows=[];truth={};names=[]
 for song in shared.IDS:
  entry=shared.INPUTS[song];beat=entry['beatPeriod']
  evidence=json.loads((PROJECT/'Assets/Clone/Editor/InstrumentEvidence'/(entry['sourceFileSha256']+'.notes.json')).read_text());events=evidence['events']
  voice=json.loads((PROJECT/'Assets/Clone/Editor/AudioEvidence'/(entry['sourceFileSha256']+'.voice.json')).read_text())
  reference_path=PROJECT/'Assets/Resources/OriginalData/Maps'/(song+'.timeline.json')
  if hashlib.sha256(reference_path.read_bytes()).hexdigest()!=entry['referenceMapSha256']:raise ValueError('Calibration reference changed')
  reference=json.loads(reference_path.read_text());holds=[h for h in reference['segments']if h['kind']=='Hold'and h['duration']<=.45];truth[song]=holds
  for key in ['rms','pitchTimes','pitchConfidence','pitch']:voice[key]=np.asarray(voice[key])
  scale=max(.001,float(np.quantile(voice['rms'],.95)))
  by_onset=collections.defaultdict(list);by_pitch=collections.defaultdict(list)
  for note in events:
   by_onset[note['time']].append(note)
   if note['features'][21]>.5 and note['features'][0]>=.5:by_pitch[note['midi']].append(note['time'])
  names=evidence['featureNames']+['durationToBeat','durationToLocalGap','body','release','bodyToRelease','early','decay','voicedFraction','pitchStability','onsetContrast','peak','confidence']
  for event in events:
   a=event['time']
   anchors=[n for n in by_onset[a]if (n['midi']-event['midi'])%12==0 and n['features'][19]<=event['features'][19]]
   reattacks=[time for anchor in anchors for time in by_pitch[anchor['midi']]if a+.08<=time<a+.41]
   for b in shared.endpoint_candidates(event,events,beat):
    features=list(event['features'])
    for i in [5,10,12,13,14]:features[i]/=beat
    features += [(b-a)/beat,(b-a)/max(.06,event['features'][14])]+release_features(voice,a,b,scale)
    row=dict(song=song,time=a,end=b,midi=event['midi'],features=features,reattackInside=any(t<b-.04 for t in reattacks));row['y']=int(any(match(row,h)for h in holds));rows.append(row)
 return rows,truth,names

def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--deps',type=pathlib.Path);parser.add_argument('--output',type=pathlib.Path,default=PROJECT/'Library/ChartGenerationV10/ShortHolds/training-proof');parser.add_argument('--leave-one-song-out',action='store_true');args=parser.parse_args()
 output=args.output.resolve()
 if (PROJECT/'Library').resolve() not in output.parents:raise ValueError('Training output must remain inside project Library')
 setup();inputs=shared.provenance();rows,truth,names=dataset();x=np.array([row['features']for row in rows]);y=np.array([row['y']for row in rows]);songs=np.array([row['song']for row in rows])
 tree=shared.tree_fit(x,y,8,16,1,songs);report=evaluate(proposals(tree,rows,.9),truth)
 result=dict(version='short-support-experimental-v2-relative-release',experimental=True,defaultEnabled=False,maxDepth=8,minLeaf=16,positiveWeight=1,acceptancePrecision=.9,positiveLeafSupport='at least two positives from at least two recordings',featureNames=names,trainingInputs=inputs,candidateRows=len(rows),positiveRows=int(y.sum()),tree=tree,positiveRules=shared.positive_rules(tree,names),calibration=report,metric='Hold<=.45s; onset/end80ms; IoU>=.5; maximum-cardinality one-to-one',voiceWindow='body onset+30ms to exit-25ms; release exit+15ms to exit+75ms; peak and pitch inside selected interval only; other source/context features and global RMS normalization remain offline',independentReattackGuard='same-MIDI or equal/stronger same-onset octave anchor; sourceAttack>.5 and neuralOnset>=.5 inside [start+80ms,end-40ms)',trainerSourceSha256=hashlib.sha256(pathlib.Path(__file__).read_bytes().replace(b'\r\n',b'\n')).hexdigest(),sharedTrainerSourceSha256=hashlib.sha256(pathlib.Path(shared.__file__).read_bytes().replace(b'\r\n',b'\n')).hexdigest())
 output.mkdir(parents=True,exist_ok=True);path=output/'short-support-release-v2.json'
 if args.leave_one_song_out:
  result['leaveOneSongOut']={}
  for song in shared.IDS:
   ix=songs!=song;model=shared.tree_fit(x[ix],y[ix],8,16,1,songs[ix]);score=evaluate(proposals(model,[row for row in rows if row['song']==song],.9),{song:truth[song]})[song];result['leaveOneSongOut'][song]=score;print('LOO',song,score['tp'],score['fp'],flush=True)
 path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8');print(path,flush=True)
if __name__=='__main__':main()
