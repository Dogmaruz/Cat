"""Independent mixed-PCM note measurements for offline chart authoring.

No chart, song identifier or reference gesture enters these measurements.
Pinned note inference runs locally. Audio is never uploaded.
"""
import argparse,hashlib,importlib.util,json,math,struct,sys
from pathlib import Path
if '--deps'in sys.argv:sys.path.insert(0,sys.argv[sys.argv.index('--deps')+1])
import numpy as np
from scipy.ndimage import median_filter

FEATURE_NAMES=('neuralOnset','neuralPeak','neuralBody','neuralLate','neuralTail','neuralDuration',
    'harmonicPartials','harmonicBody','harmonicLate','harmonicTail','harmonicDuration','harmonicNovelty',
    'nextGap','previousGap','localGap','nextPitchDelta','previousPitchDelta','insideAttacks',
    'samePitchReattack','pitchRank','relativePitch','sourceAttack')
ANALYSIS_VERSION='mixed-note-events-2-candidates8'
TIMING_VERSION='input-window-time-v2'
NOTE_SHA='2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec'

def evidence_digest(e):
    q=lambda value:int(math.floor(value*100000000+.5))
    chunks=[b'RadioArchipelagoInstrumentEvidence/quantized-1e8/1\n',
        (e['sourcePcmSha256']+'\n'+e['sourceFileSha256']+'\n'+e['noteModelSha256']+'\n'+e['analysisVersion']+'\n'+e['timingVersion']+'\n').encode('ascii'),
        struct.pack('<iiiq',e['sampleRate'],e['channels'],e['sampleFrames'],e['featureQuantization']),
        struct.pack('<i',len(e['featureNames']))]
    for name in e['featureNames']:
        encoded=name.encode('utf8');chunks.extend([struct.pack('<i',len(encoded)),encoded])
    chunks.append(struct.pack('<i',len(e['events'])))
    for event in e['events']:
        chunks.append(struct.pack('<qqii',q(event['time']),q(event['endTime']),event['midi'],len(event['features'])))
        chunks.append(struct.pack('<'+'q'*len(event['features']),*(q(v)for v in event['features'])))
    return hashlib.sha256(b''.join(chunks)).hexdigest()

def source_spectrum(pcm,rate):
    if pcm.ndim!=2 or not np.isfinite(pcm).all()or pcm.shape[1]not in(1,2):raise ValueError('Expected finite interleaved mono/stereo PCM')
    stride=max(1,rate//11025);ar=rate/stride;size=1024;hop=max(1,round(ar*.01));magnitude=0
    for channel in range(pcm.shape[1]):
        data=np.convolve(pcm[:,channel],np.ones(stride*2+1)/(stride*2+1),'same')[::stride]
        if len(data)<size:return np.array([]),np.zeros((0,size//2+1))
        frames=np.lib.stride_tricks.sliding_window_view(data,size)[::hop]
        magnitude+=abs(np.fft.rfft(frames*np.hanning(size),axis=1))/pcm.shape[1]
    return (np.arange(len(magnitude))*hop+size/2)/ar,magnitude

def spectral_attacks(times,magnitude,bin_hz):
    if len(times)<3:return []
    freq=np.arange(magnitude.shape[1])*bin_hz;bright=(freq>=2500)&(freq<=5000)
    fraction=(magnitude[:,bright]**2).sum()/max(1e-20,(magnitude**2).sum())
    band=magnitude[:,bright]if fraction>.0004 else magnitude
    z=np.log1p(band*12);flux=np.maximum(0,np.diff(z,axis=0,prepend=z[:1])).mean(axis=1)
    local=np.convolve(flux,np.ones(81),'same')/np.convolve(np.ones(len(flux)),np.ones(81),'same')
    peaks=np.flatnonzero((flux[1:-1]>=flux[:-2])&(flux[1:-1]>flux[2:]))+1
    peaks=[i for i in peaks if flux[i]>max(float(flux.max())*.02,local[i]*(1.6 if fraction>.0004 else 1.2))]
    selected=[]
    for i in sorted(peaks,key=lambda i:-flux[i]):
        if all(abs(times[i]-times[j])>=.16-1e-6 for j in selected):selected.append(i)
    return sorted(float(times[i])for i in selected)

def neural_measurements(times,note,onset,start):
    def region(a,b,aggregate=np.mean):
        values=note[(times>=start+a)&(times<start+b)]
        return float(aggregate(values))if len(values)else 0.
    early=(times>=start-.025)&(times<=start+.13)
    on=float(onset[early].max())if early.any()else 0.
    peak=max(region(.01,.15,np.max),.001)
    first=int(np.searchsorted(times,start+.05));end=first;missing=0
    smooth=median_filter(note,size=3)
    for i in range(first,len(times)):
        if times[i]>start+2:break
        missing=missing+1 if smooth[i]<max(.12,.1+(peak-.1)*.35)else 0
        if missing>=3:end=max(first,i-2);break
        if times[i]>start+.12 and onset[i]>=max(.35,on*.7)and onset[i]>=onset[i-1]and onset[i]>onset[min(i+1,len(times)-1)]:end=i;break
        end=i
    duration=max(0,float(times[min(end,len(times)-1)]-start))
    repeat=onset[(times>=start+.12)&(times<start+min(duration,.55))]
    return dict(onset=on,peak=peak,body=region(.04,.18)/peak,late=region(.18,.30)/peak,
        tail=region(.32,.48)/peak,duration=duration,
        samePitch=float(repeat.max())if len(repeat)else 0.)

def harmonic_measurements(times,magnitude,bin_hz,start,midi):
    frequency=440*2**((midi-69)/12);indices=np.rint(frequency*np.arange(1,7)/bin_hz).astype(int)
    indices=indices[(indices>=3)&(indices+3<magnitude.shape[1])]
    if not len(indices):return dict(partials=0,body=0,late=0,tail=0,duration=0,novelty=0)
    tonal=np.maximum.reduce([magnitude[:,indices+d]for d in[-1,0,1]])
    continuum=(magnitude[:,indices-3]+magnitude[:,indices+3])/2
    tonal=np.maximum(0,tonal-continuum)
    region=lambda a,b:tonal[(times>=start+a)&(times<start+b)]
    prior=region(-.14,-.035);pre=prior.mean(axis=0)if len(prior)else np.zeros(len(indices))
    early=region(.02,.13);early=early.mean(axis=0)if len(early)else np.zeros(len(indices))
    novel=np.maximum(0,early-pre);partials=int((novel>np.maximum(.025,pre*.35)).sum())
    track=median_filter(np.maximum(0,tonal-pre).sum(axis=1),size=3)
    early_ix=(times>=start+.03)&(times<start+.12);peak=max(float(track[early_ix].max())if early_ix.any()else 0.,.001)
    first=int(np.searchsorted(times,start+.08));end=first;missing=0
    for i in range(first,len(times)):
        if times[i]>start+2:break
        missing=missing+1 if track[i]<peak*.35 else 0
        if missing>=3:end=max(first,i-2);break
        end=i
    average=lambda a,b:float(track[(times>=start+a)&(times<start+b)].mean()/peak)if np.any((times>=start+a)&(times<start+b))else 0.
    full=magnitude[(times>=start+.02)&(times<start+.13)].sum(axis=1)
    novelty=float(novel.sum()/max(.001,float(full.mean())if len(full)else 0.))
    return dict(partials=partials,body=average(.04,.18),late=average(.18,.30),tail=average(.32,.48),
        duration=max(0,float(times[min(end,len(times)-1)]-start)),novelty=novelty)

def context_features(events,index,target=None):
    event=events[index]if target is None else target;time=event['time'];previous=events[index-1]if index else None;following=events[index+1]if index+1<len(events)else None
    local=events[max(0,index-6):index+7];gaps=[b['time']-a['time']for a,b in zip(local,local[1:])if b['time']-a['time']>=.06]
    return dict(nextGap=following['time']-time if following else 2.,previousGap=time-previous['time']if previous else 2.,
        localGap=float(np.median(gaps))if gaps else .5,
        nextPitchDelta=following['midi']-event['midi']if following else 0,
        previousPitchDelta=event['midi']-previous['midi']if previous else 0,
        insideAttacks=sum(time+.08<e['time']<event['endTime']-.05 for e in events),
        relativePitch=event['midi']-float(np.median([e['midi']for e in events])))

def measure_events(pcm,rate,notes):
    times,magnitude=source_spectrum(pcm,rate);bin_hz=(rate/max(1,rate//11025))/1024
    physical=spectral_attacks(times,magnitude,bin_hz)
    nt=notes['times'];onsets=notes['onset'][:,27:64];activation=notes['note'][:,27:64]
    novelty=onsets.max(axis=1);local=np.convolve(novelty,np.ones(31),'same')/np.convolve(np.ones(len(novelty)),np.ones(31),'same')
    peaks=np.flatnonzero((novelty[1:-1]>=novelty[:-2])&(novelty[1:-1]>novelty[2:]))+1
    peaks=[i for i in peaks if novelty[i]>=.3 and novelty[i]>=local[i]*1.15]
    selected=[]
    for i in sorted(peaks,key=lambda i:-novelty[i]):
        if all(abs(nt[i]-nt[j])>=.078 for j in selected):selected.append(i)
    starts=sorted(float(nt[i])for i in selected)
    for a in physical:
        if a>.15 and not any(abs(a-b)<.06 for b in starts):starts.append(a)
    starts.sort();events=[];leading=[]
    for a in starts:
        ix=(nt>=a-.025)&(nt<=a+.10)
        if not ix.any():continue
        scores=onsets[ix].max(axis=0)*np.sqrt(activation[ix].max(axis=0))
        # Dense chords can rank a real melodic note below accompaniment or
        # a lingering octave. Keep eight hypotheses without lowering confidence;
        # source-confirmed gesture recognition still chooses among them later.
        pitches=[int(i)for i in np.argsort(-scores)[:8]if onsets[ix,i].max()>=.25]
        if not pitches:continue
        leading.append(dict(time=a,midi=pitches[0]+48,endTime=a))
        for rank,index in enumerate(pitches):
            midi=index+48;neural=neural_measurements(nt,activation[:,index],onsets[:,index],a)
            harmonic=harmonic_measurements(times,magnitude,bin_hz,a,midi)
            end=min(len(pcm)/rate,a+harmonic['duration'])
            if end<=a+.015:continue
            events.append(dict(time=a,endTime=end,midi=midi,rank=rank,neural=neural,harmonic=harmonic))
    for i,e in enumerate(leading):
        matches=[n['endTime']for n in events if abs(n['time']-e['time'])<1e-8 and n['rank']==0]
        e['endTime']=matches[0]if matches else e['time']
    leading_by_time={e['time']:i for i,e in enumerate(leading)}
    output=[]
    for event in events:
        n=event['neural'];h=event['harmonic'];context=context_features(leading,leading_by_time[event['time']],event)
        values=[n['onset'],n['peak'],n['body'],n['late'],n['tail'],n['duration'],
            h['partials'],h['body'],h['late'],h['tail'],h['duration'],h['novelty'],
            context['nextGap'],context['previousGap'],context['localGap'],context['nextPitchDelta'],context['previousPitchDelta'],
            context['insideAttacks'],n['samePitch'],event['rank'],context['relativePitch'],float(any(abs(event['time']-a)<=.05 for a in physical))]
        output.append(dict(time=round(event['time'],8),endTime=round(event['endTime'],8),midi=event['midi'],
            features=[round(max(-128,min(128,float(v))),8)for v in values]))
    return sorted(output,key=lambda e:(e['time'],e['midi']))

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['input','decoded-pcm','decoded-metadata','output','cache-dir','model-dir']:parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--deps',type=Path);args=parser.parse_args()
    if args.deps:sys.path.insert(0,str(args.deps.resolve()))
    import onnxruntime as ort
    from scipy.signal import resample_poly
    metadata=json.loads(args.decoded_metadata.read_text());raw=args.decoded_pcm.read_bytes()
    source_hash=hashlib.sha256(args.input.read_bytes()).hexdigest();pcm_hash=hashlib.sha256(raw).hexdigest()
    if source_hash!=metadata['sourceFileSha256']or pcm_hash!=metadata['sourcePcmSha256']:raise ValueError('Source provenance mismatch')
    rate=metadata['sampleRate'];channels=metadata['channels'];frames=metadata['sampleFrames']
    pcm=np.frombuffer(raw,dtype='<f4').reshape(-1,channels)
    if len(pcm)!=frames or not np.isfinite(pcm).all()or channels not in(1,2):raise ValueError('Invalid decoded PCM')
    model=args.model_dir/'nmp.onnx'
    if hashlib.sha256(model.read_bytes()).hexdigest()!=NOTE_SHA:raise ValueError('Pinned note model digest mismatch')
    spec=importlib.util.spec_from_file_location('instrument_voice',Path(__file__).with_name('analyze_vocal_holds.py'))
    voice=importlib.util.module_from_spec(spec);spec.loader.exec_module(voice);voice.np=np;voice.ort=ort;voice.resample_poly=resample_poly
    folder=args.cache_dir/pcm_hash;folder.mkdir(parents=True,exist_ok=True);cache=folder/'mixed-notes-separate-channels-v1.npz'
    if cache.exists():
        notes=dict(np.load(cache))
        if str(notes.get('timingVersion'))!=TIMING_VERSION or str(notes.get('sourcePcmSha256'))!=pcm_hash:raise ValueError('Mixed-note cache provenance mismatch')
    else:
        x=pcm.T if rate==44100 else resample_poly(pcm.T,44100,rate,axis=1)
        weights=np.sqrt((x*x).mean(axis=1));weights/=max(1e-20,weights.sum());notes=None
        for ch,weight in enumerate(weights):
            if weight<1e-6:continue
            result=voice.transcribe(np.repeat(x[ch:ch+1],2,axis=0),model)
            if notes is None:notes={k:(v*weight if k in('note','onset')else v)for k,v in result.items()}
            else:
                if not np.array_equal(notes['times'],result['times']):raise ValueError('Channel inference timing mismatch')
                for k in ['note','onset']:notes[k]+=result[k]*weight
        if notes is None:raise ValueError('Silent source has no note evidence')
        notes['sourcePcmSha256']=np.array(pcm_hash);np.savez_compressed(cache,**notes)
    evidence=dict(schemaVersion=1,featureQuantization=100000000,sourceFileSha256=source_hash,sourcePcmSha256=pcm_hash,
        sampleRate=rate,channels=channels,sampleFrames=frames,noteModelSha256=NOTE_SHA,timingVersion=TIMING_VERSION,
        analysisVersion=ANALYSIS_VERSION,audioUploaded=False,featureNames=list(FEATURE_NAMES),events=measure_events(pcm,rate,notes))
    evidence['featureSha256']=evidence_digest(evidence);args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(evidence,separators=(',',':'))+'\n')
    print(str(args.output),len(evidence['events']),flush=True)

if __name__=='__main__':main()
