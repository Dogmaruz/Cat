"""Local audio-only vocal evidence. No chart, song identifier or network upload is used.

Models are optional downloads with pinned SHA-256; see THIRD_PARTY_NOTICES.md.
Dependencies: numpy, scipy, onnxruntime. --deps can name an isolated pip --target folder.
"""
import argparse,hashlib,json,math,struct,sys,urllib.request
from pathlib import Path

SEP_SHA='ce74ef3b6a6024ce44211a07be9cf8bc6d87728cc852a68ab34eb8e58cde9c8b'
NOTE_SHA='2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec'
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
    n=len(original);audio=np.r_[np.zeros(3840,np.float32),original];engine=session(model);arrays={k:[]for k in ['note','onset']}
    for a in range(0,len(audio),36164):
        chunk=audio[a:a+43844]
        if len(chunk)<43844:chunk=np.pad(chunk,(0,43844-len(chunk)))
        values=engine.run(['StatefulPartitionedCall:1','StatefulPartitionedCall:2'],{'serving_default_input_2:0':chunk[None,:,None]})
        for k,v in zip(arrays,values):arrays[k].append(v[:,15:-15])
    for k in arrays:
        v=np.concatenate(arrays[k]);arrays[k]=v.reshape(-1,v.shape[-1])[:int(n*86/22050)]
    ix=np.arange(len(arrays['note']));arrays['times']=ix*256/22050-((256/22050)*(172-43844/256)+.0018)*np.floor(ix/172)
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
                separatorModelSha256=SEP_SHA,noteModelSha256=NOTE_SHA,analysisVersion='vocal-phrase-1',audioUploaded=False)

def classify(e,attacks):
    times=np.array(e['pitchTimes']);pitch=np.array(e['pitch']);conf=np.array(e['pitchConfidence']);energy=np.array(e['rms']);dt=float(np.median(np.diff(times)));result=[]
    for t in sorted(set(attacks+e['voiceStarts'])):
        start=int(np.searchsorted(times,t+.08))
        if start+8>=len(times):continue
        p=int(pitch[start]);initial=float(conf[start:start+8].mean())
        if not 55<=p<=72 or initial<.5:continue
        missing=0;end=start+1
        for end in range(start+1,min(len(times),start+round(1.8/dt))):
            missing=missing+1 if conf[end]<.35 else 0
            if missing>=round(.06/dt):end-=missing-1;break
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
        if not(plateau or descending)or any(r['confidence']>=.4 and r['pitch']>first['pitch']+1 for r in runs):continue
        if release:
            later=[r for r in runs if r['at']>release[0]['at']and r['pitch']>=release[0]['pitch']+2 and r['confidence']>=.35]
            if later:end=min(end,int(np.searchsorted(times,later[0]['at'])))
        ix=max(0,math.ceil(t/.01));limit=min(len(energy)-1,math.ceil(times[end]/.01));peak=max(float(energy[ix:limit+1].max()),.005);missing=0;amplitudeEnd=limit
        for j in range(ix+35,limit+1):
            missing=missing+1 if energy[j]<peak*.15 else 0
            if missing>=4:amplitudeEnd=j-missing+1;break
        # A phrase releases over a short perceptual tail. Prefer the nearest
        # measured physical rearticulation when it lies within that release window.
        finish=min(float(times[end]),amplitudeEnd*.01)+.06
        releases=[a for a in attacks if t+.45<=a<=t+1.4 and abs(a-finish)<=.18]
        if releases:finish=min(releases,key=lambda a:abs(a-finish))
        if not .45<=finish-t<=1.4:continue
        if any(min(finish,q['endTime'])-max(t,q['time'])>.25 for q in result):continue
        result.append(dict(kind='Hold',time=t,endTime=finish,confidence=float(min(1,initial)),reason='Vocal-only continuous descending legato'if descending else 'Vocal plateau with downward release'))
    return result

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--input',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--deps',type=Path);parser.add_argument('--cache-dir',type=Path,required=True);parser.add_argument('--model-dir',type=Path);parser.add_argument('--download-models',action='store_true');parser.add_argument('--diagnostics',type=Path);parser.add_argument('--decoded-pcm',type=Path,help='Optional actual Unity GetData interleaved float32 little-endian PCM');parser.add_argument('--decoded-metadata',type=Path,help='Verified sourceFileSha256/sourcePcmSha256/sampleRate/channels/sampleFrames metadata');args=parser.parse_args()
    if args.deps:sys.path.insert(0,str(args.deps.resolve()))
    global np,ort,resample_poly,sosfiltfilt,butter,uniform_filter1d,percentile_filter,binary_closing,median_filter
    import numpy as np
    import onnxruntime as ort
    from scipy.signal import resample_poly,sosfiltfilt,butter
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
    vocals=np.load(stem)['samples'];prediction=work/'notes.npz'
    if not prediction.exists():np.savez_compressed(prediction,**transcribe(vocals,models/'nmp.onnx'))
    evidence=build_evidence(args.input,rate,pcm,vocals,np.load(prediction));evidence['featureSha256']=evidence_digest(evidence);args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(evidence,separators=(',',':'))+'\n')
    if args.diagnostics:
        holds=classify(evidence,source_attacks(pcm,rate));args.diagnostics.parent.mkdir(parents=True,exist_ok=True);args.diagnostics.write_text(json.dumps(dict(sourcePcmSha256=evidence['sourcePcmSha256'],candidates=holds),indent=2)+'\n')
    print(str(args.output))

if __name__=='__main__':main()
