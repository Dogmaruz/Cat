"""Local audio-only vocal evidence. No chart, song identifier or network upload is used.

Models are optional downloads with pinned SHA-256; see THIRD_PARTY_NOTICES.md.
Dependencies: numpy, scipy, onnxruntime. --deps can name an isolated pip --target folder.
"""
import argparse,hashlib,json,math,struct,sys,urllib.request
from pathlib import Path

SEP_SHA='ce74ef3b6a6024ce44211a07be9cf8bc6d87728cc852a68ab34eb8e58cde9c8b'
NOTE_SHA='2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec'
TRANSCRIPTION_VERSION='input-window-time-v2'
ANALYSIS_VERSION='vocal-phrase-2-window-time'
MODELS={
 'Kim_Vocal_2.onnx':('https://github.com/TRvlvr/model_repo/releases/download/all_public_uvr_models/Kim_Vocal_2.onnx',SEP_SHA),
 'nmp.onnx':('https://raw.githubusercontent.com/spotify/basic-pitch/main/basic_pitch/saved_models/icassp_2022/nmp.onnx',NOTE_SHA)}

def digest(data):return hashlib.sha256(data).hexdigest()

def evidence_digest(e):
    quantize=lambda value:int(math.floor(value*100000000+.5))
    chunks=[b'RadioArchipelagoVoiceEvidence/quantized-1e8/1\n',(e['sourcePcmSha256']+'\n'+e['sourceFileSha256']+'\n').encode('ascii'),struct.pack('<iiiq',e['sampleRate'],e['channels'],e['sampleFrames'],quantize(e['rmsStep']))]
    for key in ['pitchTimes','pitchConfidence','onset','rms','voiceStarts']:
        values=e[key];chunks.extend([struct.pack('<i',len(values)),struct.pack('<'+'q'*len(values),*(quantize(value)for value in values))])
    chunks.extend([struct.pack('<i',len(e['pitch'])),struct.pack('<'+'i'*len(e['pitch']),*e['pitch']),(e['separatorModelSha256']+'\n'+e['noteModelSha256']+'\n').encode('ascii')])
    return digest(b''.join(chunks))

def model_files(folder,download):
    folder.mkdir(parents=True,exist_ok=True)
    for name,(url,expected)in MODELS.items():
        p=folder/name
        if not p.exists():
            if not download:raise FileNotFoundError(f'{p}; use --download-models to fetch public models locally.')
            with urllib.request.urlopen(url)as response:
                if int(response.headers.get('Content-Length',0))>200*1048576:raise ValueError('Unexpected model size')
                data=response.read(200*1048576+1)
            if digest(data)!=expected:raise ValueError('Downloaded model hash mismatch: '+name)
            p.write_bytes(data)
        if digest(p.read_bytes())!=expected:raise ValueError('Model hash mismatch: '+name)

def stft(x):
    n=7680;x=np.pad(x,((0,0),(n//2,n//2)),mode='reflect');frames=np.lib.stride_tricks.sliding_window_view(x,n,axis=1)[:,::1024]
    s=np.fft.rfft(frames*np.hanning(n+1)[:-1],axis=-1).transpose(0,2,1)[:,:3072]
    return np.stack([s.real,s.imag],axis=1).reshape(1,4,3072,-1).astype(np.float32)

def istft(s):
    n=7680;s=s.reshape(2,2,s.shape[-2],s.shape[-1]);z=s[:,0]+1j*s[:,1];z=np.pad(z,((0,0),(0,n//2+1-z.shape[1]),(0,0)))
    frames=np.fft.irfft(z.transpose(0,2,1),n=n,axis=-1);win=np.hanning(n+1)[:-1];out=np.zeros((2,(frames.shape[1]-1)*1024+n));weight=np.zeros(out.shape[1])
    for i in range(frames.shape[1]):
        a=i*1024;out[:,a:a+n]+=frames[:,i]*win;weight[a:a+n]+=win*win
    return (out/np.maximum(weight,1e-8))[:,n//2:-n//2].astype(np.float32)

def session(path):
    opts=ort.SessionOptions();opts.intra_op_num_threads=4;opts.inter_op_num_threads=1
    return ort.InferenceSession(str(path),sess_options=opts,providers=['CPUExecutionProvider'])

def separate(audio,model):
    chunk=1024*255;trim=3840;gen=chunk-2*trim;peak=max(float(abs(audio).max()),1e-5);count=math.ceil(audio.shape[1]/gen)
    padded=np.pad(audio/peak,((0,0),(trim,count*gen-audio.shape[1]+trim)));engine=session(model);out=[]
    for i in range(count):
        spectrum=stft(padded[:,i*gen:i*gen+chunk]);spectrum[:,:,:3]=0
        pred=engine.run(None,{engine.get_inputs()[0].name:spectrum})[0];out.append(istft(pred)[:,trim:-trim])
        print(f'Vocal separation {i+1}/{count}',file=sys.stderr,flush=True)
    return np.concatenate(out,axis=1)[:,:audio.shape[1]]*peak*1.009

def transcribe(vocals,model):
    original=resample_poly(vocals.mean(axis=0),1,2).astype(np.float32);original*=min(5,.9/max(.01,float(abs(original).max())))
    rate=22050;frame_hop=256;window=43844;trim=15;pad=trim*frame_hop;hop=window-2*pad
    n=len(original);audio=np.r_[np.zeros(pad,np.float32),original];engine=session(model);arrays={k:[]for k in ['note','onset']};times=[]
    for a in range(0,n,hop):
        chunk=audio[a:a+window]
        if len(chunk)<window:chunk=np.pad(chunk,(0,window-len(chunk)))
        values=engine.run(['StatefulPartitionedCall:1','StatefulPartitionedCall:2'],{'serving_default_input_2:0':chunk[None,:,None]})
        if len(values)!=2 or any(v.ndim!=3 or v.shape[0]!=1 or v.shape[1]!=172 for v in values):
            raise ValueError('Unexpected pinned Basic Pitch model output shape')
        # Each native frame is centered at j*256 in THIS input window. Its
        # absolute coordinate includes the true window hop and removes initial
        # zero-padding. Flattened-frame correction accumulates the wrong offset
        # because edge trimming retains 142 frames, rather than 172.
        native_frames=np.arange(trim,172-trim);samples=a+native_frames*frame_hop-pad
        valid=(samples>=0)&(samples<n)
        times.append(samples[valid]/rate)
        for k,v in zip(arrays,values):arrays[k].append(v[0,trim:-trim][valid])
    for k in arrays:arrays[k]=np.concatenate(arrays[k],axis=0)
    arrays['times']=np.concatenate(times);arrays['timingVersion']=np.array(TRANSCRIPTION_VERSION)
    return arrays

def envelope_starts(energy):
    scale=np.maximum(percentile_filter(energy,90,size=150,mode='nearest'),np.percentile(energy,90)*.1);starts=[]
    for threshold in [.15,.3,.45]:
        active=binary_closing(energy/np.maximum(scale,1e-9)>=threshold,structure=np.ones(2));a=np.flatnonzero(np.diff(active.astype(int),prepend=0)==1);b=np.flatnonzero(np.diff(active.astype(int),append=0)==-1)
        starts.extend(float(i*.01)for i,j in zip(a,b)if .4<=(j-i)*.01<=1.8)
    return sorted(set(starts))

def source_attacks(x,rate):
    step=max(1,rate//11025);dsrate=rate/step;hop=round(dsrate*.01);mags=0
    for ch in range(x.shape[1]):
        samples=np.convolve(x[:,ch],np.ones(step*2+1)/(step*2+1),'same')[::step]
        if len(samples)<1024:return []
        frames=np.lib.stride_tricks.sliding_window_view(samples,1024)[::hop];mags+=abs(np.fft.rfft(frames*np.hanning(1024),axis=1))/x.shape[1]
    times=(np.arange(len(mags))*hop+512)/dsrate;freq=np.fft.rfftfreq(1024,1/dsrate);z=np.log1p(mags[:,(freq>=2500)&(freq<=5000)]*12);flux=np.maximum(0,np.diff(z,axis=0,prepend=z[:1])).mean(axis=1)
    mean=np.convolve(flux,np.ones(81),'same')/np.convolve(np.ones(len(flux)),np.ones(81),'same');peaks=np.flatnonzero((flux[1:-1]>=flux[:-2])&(flux[1:-1]>flux[2:]))+1
    peaks=[i for i in peaks if flux[i]>max(float(flux.max())*.02,mean[i]*1.6)];selected=[]
    for i in sorted(peaks,key=lambda i:-flux[i]):
        if all(abs(times[i]-times[j])>=.16-1e-6 for j in selected):selected.append(i)
    return sorted(float(times[i])for i in selected)

def build_evidence(source,rate,pcm,vocals,notes):
    # Pitch keeps its native neural frame times; RMS is exactly 100Hz. Keeping native
    # pitch timing avoids accumulating resampling error at the short legato boundaries.
    n=notes['note'][:,27:64];pitch=median_filter(n.argmax(axis=1)+48,size=5);confidence=uniform_filter1d(n.max(axis=1),5)
    filtered=sosfiltfilt(butter(4,350,fs=44100,btype='highpass',output='sos'),vocals.mean(axis=0))
    rms=np.sqrt(uniform_filter1d(filtered*filtered,round(.025*44100)))[::441]
    return dict(schemaVersion=1,featureQuantization=100000000,sourceFileSha256=digest(source.read_bytes()),sourcePcmSha256=digest(pcm.astype('<f4').tobytes()),sampleRate=rate,channels=pcm.shape[1],sampleFrames=pcm.shape[0],rmsStep=.01,
                pitchTimes=np.round(notes['times'],8).tolist(),pitch=pitch.tolist(),pitchConfidence=np.round(confidence,7).tolist(),onset=np.round(notes['onset'][:,34:64].max(axis=1),7).tolist(),rms=np.round(rms,8).tolist(),voiceStarts=envelope_starts(rms),
                separatorModelSha256=SEP_SHA,noteModelSha256=NOTE_SHA,analysisVersion=ANALYSIS_VERSION,audioUploaded=False)

def classify_legato(e,attacks):
    times=np.array(e['pitchTimes']);pitch=np.array(e['pitch']);conf=np.array(e['pitchConfidence']);energy=np.array(e['rms']);dt=float(np.median(np.diff(times)));result=[]
    for t in sorted(set(attacks+e['voiceStarts'])):
        start=int(np.searchsorted(times,t+.08))
        if start+8>=len(times):continue
        p=int(pitch[start]);initial=float(conf[start:start+8].mean())
        if not 55<=p<=72 or initial<.5:continue
        gapPeak=max(.005,float(energy[max(0,math.ceil(t/.01)):min(len(energy),math.ceil((t+.8)/.01))].max()));missing=0;end=start+1
        for end in range(start+1,min(len(times),start+round(1.8/dt))):
            missing=missing+1 if conf[end]<.35 else 0
            # Repair brief pitch ambiguity only while the separated voice is
            # physically present. A genuine quiet gap retains the shorter limit.
            if missing>=round(.12/dt)or(missing>=round(.06/dt)and energy[min(len(energy)-1,math.ceil(times[end]/.01))]<gapPeak*.1):end-=missing-1;break
        if not .45<=times[end]-t<=1.9:continue
        runs=[];a=start
        for i in range(start+1,end+1):
            if i==end or pitch[i]!=pitch[a]:
                if (i-a)*dt>=.06:runs.append(dict(pitch=int(pitch[a]),duration=(i-a)*dt,confidence=float(conf[a:i].mean()),at=float(times[a])))
                a=i
        if len(runs)<2:continue
        first=runs[0];release=[r for r in runs[1:]if r['confidence']>=.35 and first['pitch']-r['pitch']>=2 and r['duration']>=.07];strong=[r for r in runs if r['confidence']>=.4]
        descending=len(strong)>=3 and all(strong[i]['pitch']<=strong[i-1]['pitch']for i in range(1,3))and strong[0]['pitch']-strong[2]['pitch']>=5
        plateau=.3<=first['duration']<=.75 and bool(release)
        softRelease=.3<=first['duration']<=.75 and any(r['confidence']>=.35 and r['confidence']<=first['confidence']*.75 and first['pitch']-r['pitch']==1 and r['duration']>=.07 for r in runs[1:])
        ornament=len(strong)>=3 and .08<=strong[0]['duration']<=.28 and strong[1]['duration']<=.2 and 2<=strong[1]['pitch']-strong[0]['pitch']<=4 and abs(strong[2]['pitch']-strong[0]['pitch'])<=1 and strong[2]['duration']>=.3
        if not(plateau or descending or ornament or softRelease)or(not ornament and any(r['confidence']>=.4 and r['pitch']>first['pitch']+1 for r in runs)):continue
        if release:
            later=[r for r in runs if r['at']>release[0]['at']and r['pitch']>=release[0]['pitch']+2 and r['confidence']>=.35]
            if later:end=min(end,int(np.searchsorted(times,later[0]['at'])))
        ix=max(0,math.ceil(t/.01));limit=min(len(energy)-1,math.ceil(times[end]/.01));peak=max(float(energy[ix:limit+1].max()),.005);missing=0;amplitudeEnd=limit
        for j in range(ix+35,limit+1):
            missing=missing+1 if energy[j]<peak*.15 else 0
            if missing>=4:amplitudeEnd=j-missing+1;break
        # A phrase releases over a short perceptual tail. Prefer the nearest
        # measured physical rearticulation when it lies within that release window.
        finish=first['at']+first['duration'] if softRelease and not plateau else min(float(times[end]),amplitudeEnd*.01)+.06
        releases=[a for a in attacks if t+.45<=a<=t+1.4 and abs(a-finish)<=.18]
        if releases:finish=min(releases,key=lambda a:abs(a-finish))
        if not .45<=finish-t<=1.4:continue
        if any(min(finish,q['endTime'])-max(t,q['time'])>.25 for q in result):continue
        reason='Vocal-only continuous descending legato'if descending else 'Vocal plateau with downward release'
        if ornament:reason='Returning vocal ornament followed by a sustained body note'
        elif softRelease and not plateau:reason='Stable vocal plateau ending in a weakening semitone fall; release at the main note boundary'
        result.append(dict(kind='Hold',time=t,endTime=finish,confidence=float(min(1,initial)),reason=reason))
    return result

def register_ceiling(e):
    pitch=np.array(e['pitch']);confidence=np.array(e['pitchConfidence']);dt=float(np.median(np.diff(e['pitchTimes'])));values,counts=np.unique(pitch[confidence>=.45],return_counts=True);supported=values[counts*dt>=.2]
    return int(supported.max())if len(supported)else None

def upper_pickups(e):
    times=np.array(e['pitchTimes']);pitch=np.array(e['pitch']);q=np.array(e['pitchConfidence']);on=np.array(e['onset']);ceiling=register_ceiling(e)
    if ceiling is None:return []
    peaks=find_peaks(on,height=.45,prominence=.08,distance=7)[0];candidates=[]
    for onset in peaks:
        start=float(times[onset]);low=int(pitch[onset]);initial=float(q[onset:onset+5].mean())
        if low<ceiling-3 or low>=ceiling or initial<.25:continue
        for high in range(max(ceiling-1,low+1),min(ceiling+1,low+4)):
            upper=np.flatnonzero((times>=start+.04)&(times<=start+.25)&(pitch==high))
            if not len(upper):continue
            first=int(upper[0]);end=first
            while end+1<len(times)and pitch[end+1]==high:end+=1
            if times[end]-times[first]<.3 or float(q[first:end+1].mean())<.45:continue
            releases=[i for i in peaks if start+.45<=times[i]<=start+1.1 and pitch[i]==high and on[i]>=.7]
            if not releases:continue
            if not has_audible_continuity(e,start,float(times[releases[0]])):continue
            candidates.append(dict(kind='Hold',time=start,endTime=float(times[releases[0]]),confidence=float(on[onset]),reason='Brief pickup into the measured upper voice register; release at strong same-note rearticulation'));break
    selected=[]
    for candidate in sorted(candidates,key=lambda c:-c['confidence']):
        if not any(abs(candidate['time']-c['time'])<.15 for c in selected):selected.append(candidate)
    return sorted(selected,key=lambda c:c['time'])

def cadence_rest_fills(e,attacks,beatPeriod):
    if not .3<=beatPeriod<=1.2:return []
    times=np.array(e['pitchTimes']);pitch=np.array(e['pitch']);q=np.array(e['pitchConfidence']);rms=np.array(e['rms']);et=np.arange(len(rms))*.01;dt=float(np.median(np.diff(times)));ceiling=register_ceiling(e);candidates=[]
    if ceiling is None:return []
    for start in sorted(set(list(attacks)+e['voiceStarts'])):
        a=int(np.searchsorted(times,start+.08))
        if a+8>=len(times):continue
        initial=int(pitch[a]);confidence=float(q[a:a+8].mean())
        if initial<ceiling-2 or confidence<.5:continue
        upperEnd=a
        while upperEnd+1<len(times)and pitch[upperEnd+1]==initial and q[upperEnd+1]>=.35:upperEnd+=1
        plateau=times[upperEnd]-times[a]
        if not .3<=plateau<=.75:continue
        tail=upperEnd+1;releaseEnd=min(len(times),tail+round(.3/dt));released=np.flatnonzero((pitch[tail:releaseEnd]<=initial-2)&(pitch[tail:releaseEnd]>=initial-5)&(q[tail:releaseEnd]>=.35))
        if len(released)<round(.07/dt):continue
        begin=int(np.searchsorted(et,start));limit=min(len(rms)-1,int(np.searchsorted(et,start+1.3)));peak=max(float(rms[begin:limit].max()),.005);missing=0;voiceEnd=None
        for i in range(begin+35,limit):
            missing=missing+1 if rms[i]<peak*.15 else 0
            if missing>=6:voiceEnd=i-missing+1;break
        if voiceEnd is None:continue
        response=None
        for i in range(voiceEnd+6,min(len(rms)-6,voiceEnd+230)):
            if rms[i:i+5].mean()<peak*.2:continue
            j=min(len(times)-1,int(np.searchsorted(times,et[i]+.06)))
            if q[j]<.4:continue
            # The next confident vocal entry must be the lower answer; do not
            # skip an intervening higher syllable and call that interval quiet.
            if pitch[j]<=initial-5:response=i
            break
        if response is None or(response-voiceEnd)*.01<.6:continue
        quiet=rms[voiceEnd+6:max(voiceEnd+7,response-5)]
        if len(quiet)and np.percentile(quiet,90)>peak*.2:continue
        real=[a for a in attacks if abs(a-et[response])<=.1];measured=min(real,key=lambda a:abs(a-et[response]))if real else float(et[response]);finish=min(measured-beatPeriod/2,start+1.6)
        if finish-start<.9:continue  # The min above already enforces the upper bound.
        candidates.append(dict(kind='Hold',time=float(start),endTime=float(finish),confidence=confidence,reason='Musical support policy: upper cadence and genuine vocal rest, ending before the next measured lower answer'))
    selected=[]
    for candidate in candidates:
        if not any(min(candidate['endTime'],c['endTime'])-max(candidate['time'],c['time'])>.25 for c in selected):selected.append(candidate)
    return selected

def falling_glide_handoffs(e,attacks,confirmed,beatPeriod):
    if not .3<=beatPeriod<=1.2:return []
    times=np.array(e['pitchTimes']);pitch=np.array(e['pitch']);q=np.array(e['pitchConfidence']);dt=float(np.median(np.diff(times)));ceiling=register_ceiling(e);candidates=[]
    if ceiling is None:return []
    for start in sorted(set(list(attacks)+e['voiceStarts'])):
        a=int(np.searchsorted(times,start));b=min(len(times)-1,int(np.searchsorted(times,start+1.3)));runs=[];last=a
        for i in range(a+1,b+1):
            if i==b or pitch[i]!=pitch[last]:
                if(i-last)*dt>=.045:runs.append(dict(pitch=int(pitch[last]),duration=(i-last)*dt,confidence=float(q[last:i].mean()),at=float(times[last])))
                last=i
        if len(runs)<3:continue
        body=None
        for i in range(2,min(len(runs),5)):
            prefix=runs[:i];candidate=runs[i];length=sum(r['duration']for r in prefix);values=[r['pitch']for r in prefix];confidence=sum(r['confidence']*r['duration']for r in prefix)/length
            if not .1<=length<=.35 or max(values)-min(values)!=1 or min(values)<ceiling-5 or confidence>=.6:continue
            if not .3<=candidate['duration']<=.75 or candidate['confidence']<.5 or max(values)-candidate['pitch']<2 or candidate['at']-start>.4:continue
            body=candidate;break
        if body is None:continue
        nexts=[c for c in confirmed if start+.5<c['time']<start+1.5]
        if not nexts:continue
        nextCue=min(nexts,key=lambda c:c['time']);flight=max(beatPeriod/2,body['duration']);finish=min(body['at']+body['duration'],nextCue['time']-flight)
        if not .4<=finish-start<=1.1:continue
        if not has_audible_continuity(e,start,finish):continue
        candidates.append(dict(kind='Hold',time=float(start),endTime=float(finish),confidence=float(body['confidence']),reason='Musical support policy: falling upper-register glide into a plateau, reserving a note-length airborne handoff to the next confirmed support'))
    selected=[]
    for candidate in candidates:
        if not any(min(candidate['endTime'],c['endTime'])-max(candidate['time'],c['time'])>.25 for c in selected):selected.append(candidate)
    return selected

def has_audible_continuity(e,start,finish):
    step=e.get('rmsStep',.01);begin=max(0,math.ceil(start/step));end=min(len(e['rms']),math.ceil(finish/step))
    if end<=begin:return False
    peak=max(.005,max(e['rms'][begin:end]));missing=0
    for value in e['rms'][begin:end]:
        missing=missing+1 if value<peak*.1 else 0
        # Pitch confidence may persist through a real pause. Only the explicit
        # cadence/rest policy may extend support across such silence.
        if missing*step>=.06:return False
    return True

def classify(e,attacks,beatPeriod=0):
    result=classify_legato(e,attacks)
    for candidates in [upper_pickups(e),cadence_rest_fills(e,attacks,beatPeriod)]:
        for candidate in candidates:
            if not any(min(candidate['endTime'],c['endTime'])-max(candidate['time'],c['time'])>.25 for c in result):result.append(candidate)
    for candidate in falling_glide_handoffs(e,attacks,result,beatPeriod):
        if not any(min(candidate['endTime'],c['endTime'])-max(candidate['time'],c['time'])>.25 for c in result):result.append(candidate)
    return sorted(result,key=lambda c:c['time'])

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--input',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--deps',type=Path);parser.add_argument('--cache-dir',type=Path,required=True);parser.add_argument('--model-dir',type=Path);parser.add_argument('--download-models',action='store_true');parser.add_argument('--diagnostics',type=Path);parser.add_argument('--decoded-pcm',type=Path,help='Optional actual Unity GetData interleaved float32 little-endian PCM');parser.add_argument('--decoded-metadata',type=Path,help='Verified sourceFileSha256/sourcePcmSha256/sampleRate/channels/sampleFrames metadata');parser.add_argument('--beat-period',type=float,default=0,help='Measured generator pulse period; enables musical cadence/handoff policies. Zero disables those policies.');args=parser.parse_args()
    if args.deps:sys.path.insert(0,str(args.deps.resolve()))
    global np,ort,resample_poly,sosfiltfilt,butter,uniform_filter1d,percentile_filter,binary_closing,median_filter,find_peaks
    import numpy as np
    import onnxruntime as ort
    from scipy.signal import resample_poly,sosfiltfilt,butter,find_peaks
    from scipy.ndimage import uniform_filter1d,percentile_filter,binary_closing,median_filter
    from scipy.io import wavfile
    if bool(args.decoded_pcm)!=bool(args.decoded_metadata):raise ValueError('--decoded-pcm and --decoded-metadata must be supplied together')
    if args.decoded_pcm:
        meta=json.loads(args.decoded_metadata.read_text());data=args.decoded_pcm.read_bytes()
        if meta['sourceFileSha256']!=digest(args.input.read_bytes()):raise ValueError('Decoded PCM source file SHA-256 mismatch')
        if meta['sourcePcmSha256']!=digest(data):raise ValueError('Decoded PCM byte SHA-256 mismatch')
        rate=int(meta['sampleRate']);channels=int(meta['channels']);frames=int(meta['sampleFrames'])
        if rate<8000 or not 1<=channels<=2 or frames<rate or len(data)!=frames*channels*4:raise ValueError('Invalid decoded PCM shape/metadata')
        pcm=np.frombuffer(data,dtype='<f4').reshape(frames,channels).copy()
    else:
        rate,raw=wavfile.read(args.input)
        if raw.ndim==1:raw=raw[:,None]
        pcm=raw.astype(np.float32)
        if raw.dtype==np.uint8:pcm=(pcm-128)/128
        elif np.issubdtype(raw.dtype,np.integer):pcm/=float(max(abs(np.iinfo(raw.dtype).min),np.iinfo(raw.dtype).max))
    if not np.isfinite(pcm).all()or len(pcm)<rate:raise ValueError('Expected finite PCM longer than one second')
    work=args.cache_dir/digest(pcm.astype('<f4').tobytes());work.mkdir(parents=True,exist_ok=True);models=args.model_dir or args.cache_dir/'models';model_files(models,args.download_models)
    audio=pcm.T
    if len(audio)==1:audio=np.repeat(audio,2,axis=0)
    if len(audio)!=2:raise ValueError('Mono or stereo input required')
    if rate!=44100:
        g=math.gcd(rate,44100);audio=resample_poly(audio,44100//g,rate//g,axis=1).astype(np.float32)
    stem=work/'vocals.npz'
    if not stem.exists():np.savez_compressed(stem,samples=separate(audio,models/'Kim_Vocal_2.onnx'))
    # Keep the costly source separation cache. The old flattened-frame notes
    # remain historical; never silently reuse them with the corrected axis.
    vocals=np.load(stem)['samples'];prediction=work/('notes-'+TRANSCRIPTION_VERSION+'.npz')
    if not prediction.exists():np.savez_compressed(prediction,**transcribe(vocals,models/'nmp.onnx'))
    notes=np.load(prediction)
    if 'timingVersion'not in notes or str(notes['timingVersion'])!=TRANSCRIPTION_VERSION:raise ValueError('Transcription cache timing version mismatch; regenerate notes')
    evidence=build_evidence(args.input,rate,pcm,vocals,notes);evidence['featureSha256']=evidence_digest(evidence);args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(evidence,separators=(',',':'))+'\n')
    if args.diagnostics:
        holds=classify(evidence,source_attacks(pcm,rate),args.beat_period);args.diagnostics.parent.mkdir(parents=True,exist_ok=True);args.diagnostics.write_text(json.dumps(dict(sourcePcmSha256=evidence['sourcePcmSha256'],beatPeriod=args.beat_period,candidates=holds),indent=2)+'\n')
    print(str(args.output))

if __name__=='__main__':main()
